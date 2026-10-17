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
///    Scars are bursting (12+) passes nothing on: the burst already "spent" them.
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
        if (creature.Side == Owner.Side) return Task.CompletedTask;
        var scars = creature.GetPowerAmount<SkofnungWoundPower>();
        if (scars > 0 && scars < SkofnungWoundPower.BurstThreshold) GetInternalData<Data>().Dying[creature] = scars;
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
