using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Durandal;

/// <summary>
/// Base of every 뒤랑달 card (content doc §2.6): sets <see cref="Sword"/>, a pure per-hit damage bonus hook for this
/// card's own powered attacks (used by the real hit and the preview) and an optional level-threshold cost.
/// </summary>
public abstract class DurandalCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : MagicSwordCard(cost, type, rarity, target)
{
    public sealed override SwordId? Sword => SwordId.Durandal;

    /// <summary>Extra damage per hit of THIS card's powered attacks. Must be pure (preview).</summary>
    protected virtual decimal BonusDamage(Creature? target) => 0m;

    /// <summary>Energy cost at a Durandal level (e.g. "3단계부터 비용 1"); null = printed cost.</summary>
    protected virtual int? CostAtLevel(int level) => null;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        var bonus = base.ModifyDamageAdditive(target, amount, props, dealer, cardSource);
        if (ReferenceEquals(cardSource, this) && props.IsPoweredAttack()) bonus += BonusDamage(target);
        return bonus;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || CostAtLevel(SwordLevel) is not { } c || c >= originalCost) return false;
        modifiedCost = c;
        return true;
    }
}
