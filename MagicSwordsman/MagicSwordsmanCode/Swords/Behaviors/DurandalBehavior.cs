using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 뒤랑달. Spec §7 [확정]: current effect = flat reduction of damage taken (grows with level);
/// cards = defense ("맞을 때 방어도 누적", "피해 상한" are card effects). Cost: cannot switch to Durandal at HP &lt;= 30%.
/// Kusanagi inherits half the reduction.
/// </summary>
public sealed class DurandalBehavior : SwordBehavior
{
    /// <summary>[임시] reduction = BaseReduction + level.</summary>
    public const int BaseReduction = 1;

    public override SwordId Id => SwordId.Durandal;

    // TODO(content: Durandal): starter cards (2) and a Durandal-specific failure curse.
    public override IEnumerable<CardModel> StarterCards => [];
    public override CardModel? FailureCurse => null;

    /// <summary>Spec cost [확정]: HP 30% or less -> cannot switch to Durandal (cards still resolve, no switch).</summary>
    public override bool CanBecomeCurrent(SwordContext ctx, SwitchReason reason) =>
        ctx.Creature.CurrentHp * 10 > ctx.Creature.MaxHp * 3;

    /// <summary>
    /// [임시] Incoming powered attacks against the owner deal (BaseReduction + level) less.
    /// TODO(content: Durandal): verify in-game that a negative additive modifier on incoming damage behaves like
    /// a flat reduction (applied before block, final damage not below 0). If not, move this to
    /// ModifyHpLostBeforeOsty in a dedicated power.
    /// </summary>
    public override decimal ModifyDamageAdditive(SwordContext ctx, Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target != ctx.Creature || dealer == ctx.Creature || !props.IsPoweredAttack()) return 0m;
        var reduction = ctx.Scale(BaseReduction + ctx.Level);
        return -Math.Min(reduction, amount);
    }
}
