using Godot;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 상처 (Skofnung wound) — debuff on an enemy. Spec §7 [확정]: at the start of the enemy's turn it loses HP equal to
/// its wounds (ignores block; wounds never decrease — timing/props as the game's PoisonPower). When it reaches
/// <see cref="BurstThreshold"/> wounds it bursts: HP loss of 20 + 4 × (Skofnung level of the player who pushed it
/// over), then the wounds are cleared. Threshold check: AfterPowerAmountChanged on itself (game OutbreakPower
/// reacts to power changes the same way).
/// Source line: 「이 검에 난 상처는 짝이 되는 '스코프눙 돌'로 문지르지 않으면 낫지 않는다」.
/// </summary>
public sealed class SkofnungWoundPower : MagicSwordsmanPower
{
    public const int BurstThreshold = 12;
    public const int BurstBase = 20;
    public const int BurstPerLevel = 4;

    private sealed class Data
    {
        public bool Bursting;
    }

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // Same label colour as PoisonPower (a debuff whose number is damage, not a duration).
    public override Color AmountLabelColor => PowerModel._normalAmountLabelColor;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", BurstThreshold),
        new DynamicVar("Burst", BurstBase),
    ];

    protected override object InitInternalData() => new Data();

    public static int BurstDamageFor(int skofnungLevel) => BurstBase + BurstPerLevel * Math.Max(0, skofnungLevel);

    /// <summary>Burst damage for wounds applied by <paramref name="applier"/> (its owner's Skofnung level).</summary>
    public static int BurstDamageFrom(Creature? applier) =>
        BurstDamageFor(applier?.Player is { } p ? SwordCombat.LevelOf(p, SwordId.Skofnung) : 0);

    protected override string SmartDescriptionLocKey
    {
        get
        {
            if (IsMutable) DynamicVars["Burst"].BaseValue = BurstDamageFrom(Applier);
            return base.SmartDescriptionLocKey;
        }
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner) || !Owner.IsAlive || Amount <= 0) return;
        Flash();
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner, Amount,
            ValueProp.Unblockable | ValueProp.Unpowered, null, null);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (!ReferenceEquals(power, this) || amount <= 0m || Amount < BurstThreshold) return;
        await Burst(choiceContext, applier ?? Applier);
    }

    /// <summary>Bursts now (HP loss, wounds cleared, 북방 최고의 검 triggers). Safe against re-entry.</summary>
    public async Task Burst(PlayerChoiceContext choiceContext, Creature? applier)
    {
        var data = GetInternalData<Data>();
        if (data.Bursting || !Owner.IsAlive) return;
        data.Bursting = true;
        try
        {
            var damage = BurstDamageFrom(applier);
            var combatState = Owner.CombatState;
            Flash();
            VfxCmd.PlayOnCreature(Owner, "vfx/vfx_attack_slash");
            await CreatureCmd.Damage(choiceContext, Owner, damage, ValueProp.Unblockable | ValueProp.Unpowered, null,
                null);
            if (Owner.IsAlive && Owner.GetPower<SkofnungWoundPower>() == this) await PowerCmd.Remove(this);

            if (combatState == null) return;
            foreach (var player in combatState.Players.ToList())
            {
                var finest = player.Creature.GetPower<SkofnungFinestInNorthPower>();
                if (finest != null) await finest.OnWoundBurst(choiceContext);
            }
        }
        finally
        {
            data.Bursting = false;
        }
    }
}

/// <summary>
/// 북방 최고의 검 (SkofnungFinestInNorth): whenever a wound bursts, gain Amount energy and draw 2 cards per stack
/// (3 from Skofnung level 4).
/// </summary>
public sealed class SkofnungFinestInNorthPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SkofnungWoundPower>()];

    public static int CardsPerStack(Player? player) =>
        player != null && SwordCombat.LevelOf(player, SwordId.Skofnung) >= 4 ? 3 : 2;

    public async Task OnWoundBurst(PlayerChoiceContext choiceContext)
    {
        if (Owner.Player is not { } player || !Owner.IsAlive) return;
        Flash();
        await PlayerCmd.GainEnergy(Amount, player);
        await CardPileCmd.Draw(choiceContext, CardsPerStack(player) * Amount, player);
    }
}

/// <summary>
/// 왕을 지킨 열두 혼 (SkofnungKingsGuard): at the start of your turn, apply Amount wounds to every enemy.
/// </summary>
public sealed class SkofnungKingsGuardPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SkofnungWoundPower>()];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || !Owner.IsAlive || Owner.CombatState is not { } combatState) return;
        var enemies = combatState.HittableEnemies.ToList();
        if (enemies.Count == 0) return;
        Flash();
        await PowerCmd.Apply<SkofnungWoundPower>(choiceContext, enemies, Amount, Owner, null);
    }
}
