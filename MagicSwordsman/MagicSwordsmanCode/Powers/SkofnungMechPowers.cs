using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 떠나지 않는 혼 (SkofnungLingeringSouls):
///  - whenever a Spectral Scar bursts (<see cref="SkofnungWoundPower.Burst"/> calls <see cref="OnWoundBurst"/>), every
///    OTHER living enemy gains Amount Scars. Bursts caused by this chain do not chain again (loop guard), so one burst
///    spreads at most once.
///  - whenever an enemy with Scars dies, its Scars move to a random other living enemy (BeforeDeath reads the amount
///    while the power is still there — GremlinHorn-style AfterDeath applies it). A creature that dies while its own
///    Scars are bursting (<see cref="SkofnungWoundPower.IsBursting"/>; also an early burst such as 울부짖는 칼 at 7+)
///    passes nothing on: the burst already "spent" them.
///  - multiplayer: when several players own this power, only <see cref="HandlerIn"/> (the first player in
///    combatState.Players who is alive and has it) spreads / relocates, so each burst or death is handled once.
/// </summary>
public sealed class SkofnungLingeringSoulsPower : MagicSwordsmanPower
{
    private sealed class Data
    {
        public bool Chaining;
        public readonly Dictionary<Creature, int> Dying = new();
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SkofnungWoundPower>()];

    protected override object InitInternalData() => new Data();

    /// <summary>
    /// The one instance that handles bursts and deaths in this combat: the power of the first player in
    /// <c>combatState.Players</c> (fixed order on every client) who is alive and has it; null if none.
    /// </summary>
    public static SkofnungLingeringSoulsPower? HandlerIn(ICombatState? combatState)
    {
        if (combatState == null) return null;
        foreach (var player in combatState.Players)
        {
            if (!player.Creature.IsAlive) continue;
            var souls = player.Creature.GetPower<SkofnungLingeringSoulsPower>();
            if (souls != null) return souls;
        }
        return null;
    }

    private List<Creature> OtherEnemies(Creature except) =>
        Owner.CombatState?.HittableEnemies.Where(e => e != except && e.IsAlive).ToList() ?? [];

    public async Task OnWoundBurst(PlayerChoiceContext choiceContext, Creature burster)
    {
        var data = GetInternalData<Data>();
        if (data.Chaining || !Owner.IsAlive) return;
        var others = OtherEnemies(burster);
        if (others.Count == 0) return;
        data.Chaining = true;
        try
        {
            Flash();
            await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, others, Amount, Owner, null);
        }
        finally
        {
            data.Chaining = false;
        }
    }

    public override Task BeforeDeath(Creature creature)
    {
        if (creature.Side == Owner.Side || HandlerIn(Owner.CombatState) != this) return Task.CompletedTask;
        var wound = creature.GetPower<SkofnungWoundPower>();
        // Killed by its own burst (whatever the threshold was): those Scars are spent, nothing moves.
        if (wound == null || wound.IsBursting || wound.Amount <= 0) return Task.CompletedTask;
        GetInternalData<Data>().Dying[creature] = wound.Amount;
        return Task.CompletedTask;
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented,
        float deathAnimLength)
    {
        var data = GetInternalData<Data>();
        if (!data.Dying.Remove(creature, out var scars) || scars <= 0 || !Owner.IsAlive) return;
        var others = OtherEnemies(creature);
        if (others.Count == 0 || Owner.Player is not { } player) return;
        var target = player.RunState.Rng.CombatTargets.NextItem(others);
        if (target == null) return;
        Flash();
        await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, target, scars, Owner, null);
    }
}
