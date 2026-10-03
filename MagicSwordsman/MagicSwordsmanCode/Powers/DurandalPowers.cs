using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 흠집 하나 없이 (DurandalUnscathed): until the start of your next turn, whenever an enemy attack hits you gain
/// Amount Block. Trigger: game FlameBarrierPower (AfterDamageReceived, powered attack, dealer set); removal: same as
/// FlameBarrierPower (after the opposing side's turn ends).
/// </summary>
public sealed class DurandalUnscathedPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!DurandalBehavior.IsEnemyAttackOn(Owner, target, props, dealer) || !Owner.IsAlive) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side != side) await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 열 번의 내리침 (DurandalTenBlows): this combat, whenever an enemy attack hits you gain Amount Block (+1 while
/// Durandal's effect is active: current, or inherited by Kusanagi). At most <see cref="MaxTriggersPerTurn"/> times per
/// turn (counter reset at the start of every side's turn). Internal-data pattern: game OutbreakPower.
/// </summary>
public sealed class DurandalTenBlowsPower : MagicSwordsmanPower
{
    public const int MaxTriggersPerTurn = 10;
    public const int CurrentSwordBonus = 1;

    private sealed class Data
    {
        public int TriggersThisTurn;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    protected override object InitInternalData() => new Data();

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!DurandalBehavior.IsEnemyAttackOn(Owner, target, props, dealer) || !Owner.IsAlive) return;
        var data = GetInternalData<Data>();
        if (data.TriggersThisTurn >= MaxTriggersPerTurn) return;
        data.TriggersThisTurn++;

        var block = Amount;
        if (Owner.Player is { } player && DurandalBehavior.IsSwordEffectActive(player, SwordId.Durandal, out _))
            block += CurrentSwordBonus;
        Flash();
        await CreatureCmd.GainBlock(Owner, block, ValueProp.Unpowered, null);
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        GetInternalData<Data>().TriggersThisTurn = 0;
        return Task.CompletedTask;
    }
}

/// <summary>
/// 성 베드로의 이빨 (DurandalStPetersTooth): until the start of your next turn, damage you take at a time is capped
/// at Amount. Game pattern: HardToKillPower (ModifyDamageCap + AfterModifyingDamageAmount flash); removal as
/// FlameBarrierPower.
/// </summary>
public sealed class DurandalStPetersToothPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner ? Amount : decimal.MaxValue;

    public override Task AfterModifyingDamageAmount(CardModel? cardSource)
    {
        Flash();
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side != side) await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 몸 밑에 숨긴 검 (DurandalHiddenBeneath): this combat, while Durandal's own reduction is NOT active (another sword
/// is current and Kusanagi is not inheriting Durandal), enemy attack hits against you are reduced by half of
/// Durandal's reduction (rounded down, min 1) at its current level. Does not stack with the full effect.
/// </summary>
public sealed class DurandalHiddenBeneathPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (!DurandalBehavior.IsEnemyAttackOn(Owner, target, props, dealer)) return 0m;
        if (Owner.Player is not { } player) return 0m;
        if (DurandalBehavior.IsSwordEffectActive(player, SwordId.Durandal, out _)) return 0m;
        var reduction = DurandalBehavior.HalfReductionFor(SwordCombat.LevelOf(player, SwordId.Durandal));
        return -Math.Min(reduction, Math.Max(0m, amount));
    }
}
