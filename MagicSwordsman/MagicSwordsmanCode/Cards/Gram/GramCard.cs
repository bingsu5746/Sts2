using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Gram;

/// <summary>
/// Base class of every Gram card, including the starting card 부서진 칼날 (Cards/Basic/BrokenBlade).
///
/// Content doc §0.2: Gram cards scale at HALF the normal rate (the current-sword effect +L already raises every
/// attack): cost 0-1 attacks +⌊L/2⌋, cost 2 attacks +1/L, multi-hit +1 per hit from level 4.
/// Subclasses describe their level scaling with <see cref="LevelDamageBonus"/> (per hit, own powered attacks,
/// applied through the damage hook so the combat preview is exact) and <see cref="WithHalfLevelVar"/> for other
/// numbers. <see cref="IsCombo"/> implements 【연속】 (current sword was already Gram right before this card).
/// <see cref="CostAtLevel"/> lets a card become cheaper from a level threshold (applied by GramBehavior).
/// </summary>
public abstract class GramCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : MagicSwordCard(cost, type, rarity, target)
{
    private SwordId? _swordBeforePlay;

    public sealed override SwordId? Sword => SwordId.Gram;

    // ------------------------------------------------------------------ level scaling

    /// <summary>Extra damage per hit of this card's own powered attacks at the given Gram level.</summary>
    protected virtual int LevelDamageBonus(int level) => 0;

    /// <summary>Extra damage per hit against a specific target (e.g. elites). Must be pure (preview).</summary>
    protected virtual int TargetDamageBonus(Creature? target) => 0;

    /// <summary>Energy cost at a Gram level, or null for the printed cost. Used by GramBehavior.TryModifyEnergyCost.</summary>
    public virtual int? CostAtLevel(int level) => null;

    /// <summary>Text variable {name} = baseValue + ⌊Gram level / 2⌋ (base value outside combat).</summary>
    protected GramCard WithHalfLevelVar(string name, int baseValue)
    {
        WithCalculatedVar(name, baseValue, 1, static (card, _) => card is MagicSwordCard m ? m.SwordLevel / 2 : 0);
        return this;
    }

    /// <summary>Play-time value of a <see cref="WithHalfLevelVar"/> number.</summary>
    protected int HalfLevelVar(string name) => DynamicVars[name + "Base"].IntValue + SwordLevel / 2;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        var bonus = base.ModifyDamageAdditive(target, amount, props, dealer, cardSource);
        if (!ReferenceEquals(cardSource, this) || !props.IsPoweredAttack()) return bonus;
        return bonus + LevelDamageBonus(SwordLevel) + TargetDamageBonus(target);
    }

    // ------------------------------------------------------------------ 【연속】

    /// <summary>【연속】: the current sword was already Gram right before this card was played.</summary>
    protected bool IsCombo => _swordBeforePlay == SwordId.Gram;

    /// <summary>Remembers the current sword right before this card's effect (the switch happens inside OnPlay).</summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this) && IsMutable && Owner != null)
            _swordBeforePlay = SwordCombat.CurrentSword(Owner);
        return base.BeforeCardPlayed(cardPlay);
    }

    /// <summary>Cards with a 【연속】 effect glow while Gram is the current sword.</summary>
    protected virtual bool HasComboEffect => false;

    protected override bool ShouldGlowGoldInternal =>
        HasComboEffect && IsMutable && Owner != null && SwordCombat.CurrentSword(Owner) == SwordId.Gram;
}
