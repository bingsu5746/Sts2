using MagicSwordsman.MagicSwordsmanCode.Cards.Durandal;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 뒤랑달. Spec §7 [확정]: current effect = flat reduction of attack damage taken, per hit (grows with level);
/// cards = defense ("맞을 때 방어도 누적" = 흠집 하나 없이 / 열 번의 내리침, "피해 상한" = 성 베드로의 이빨).
/// Cost: cannot switch to Durandal at HP &lt;= 30% (its cards still resolve, the current sword stays).
/// Kusanagi inherits half the reduction (content doc §1.3: 1,1,1,1,2,2).
/// Numbers: content doc §1.1 (2,2,3,3,4,5).
/// </summary>
public sealed class DurandalBehavior : SwordBehavior
{
    /// <summary>Content doc §1.1: reduction per enemy attack hit at levels 0..5.</summary>
    private static readonly int[] ReductionByLevel = [2, 2, 3, 3, 4, 5];

    public override SwordId Id => SwordId.Durandal;

    public override IEnumerable<CardModel> StarterCards =>
        [ModelDb.Card<DurandalRelicHilt>(), ModelDb.Card<DurandalAngelsBlade>()];

    /// <summary>롤랑의 바위 (content doc §3.1).</summary>
    public override CardModel? FailureCurse => ModelDb.Card<RolandsRock>();

    /// <summary>Full reduction per hit at a Durandal level (0..5).</summary>
    public static int ReductionFor(int level) => ReductionByLevel[Math.Clamp(level, 0, ReductionByLevel.Length - 1)];

    /// <summary>Half reduction (rounded down, min 1) — Kusanagi inheritance and 몸 밑에 숨긴 검.</summary>
    public static int HalfReductionFor(int level) => Math.Max(1, ReductionFor(level) / 2);

    /// <summary>Spec cost [확정]: HP 30% or less -> cannot switch to Durandal (cards still resolve, no switch).</summary>
    public override bool CanBecomeCurrent(SwordContext ctx, SwitchReason reason) =>
        ctx.Creature.CurrentHp * 10 > ctx.Creature.MaxHp * 3;

    /// <summary>
    /// Incoming enemy attack hits against the owner deal (2,2,3,3,4,5 by level) less; Kusanagi: half.
    /// Verified in the game source: Hook.ModifyDamage sums additive modifiers per hit (before Vulnerable-style
    /// multipliers and before block) and clamps the final value with Math.Max(0, ...); we also never subtract more
    /// than the running amount. Monster intents use the same hook, so the shown intent damage is reduced too.
    /// </summary>
    public override decimal ModifyDamageAdditive(SwordContext ctx, Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (!IsEnemyAttackOn(ctx.Creature, target, props, dealer)) return 0m;
        var reduction = ctx.Scale(ReductionFor(ctx.Level));
        return -Math.Min(reduction, Math.Max(0m, amount));
    }

    /// <summary>A powered attack (card or monster move) from an opponent hitting <paramref name="owner"/>.</summary>
    public static bool IsEnemyAttackOn(Creature owner, Creature? target, ValueProp props, Creature? dealer) =>
        target == owner && dealer != null && dealer != owner && dealer.Side != owner.Side && props.IsPoweredAttack();

    /// <summary>
    /// Whether <paramref name="sword"/>'s current-sword effect is running for <paramref name="player"/> right now:
    /// it is the current sword, or Kusanagi is current and inherits it (<paramref name="inherited"/> = true).
    /// Used by g3 powers / Onimaru that need to know "is this sword current" outside the behavior hooks.
    /// </summary>
    public static bool IsSwordEffectActive(Player player, SwordId sword, out bool inherited)
    {
        inherited = false;
        var current = SwordCombat.CurrentSword(player);
        if (current == null) return false;
        if (current == sword) return true;
        if (current != SwordId.Kusanagi) return false;
        var kusanagi = SwordRegistry.Get(SwordId.Kusanagi);
        if (kusanagi.GetInheritedSword(SwordCombat.ContextFor(player, SwordId.Kusanagi)) != sword) return false;
        if (!SwordRegistry.Get(sword).CanBeInherited) return false;
        inherited = true;
        return true;
    }
}
