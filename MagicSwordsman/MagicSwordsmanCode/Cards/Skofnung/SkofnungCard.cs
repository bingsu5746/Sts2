using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Skofnung;

/// <summary>
/// Base of every 스코프눙 card (content doc §2.7): sets <see cref="Sword"/>, a pure per-hit damage bonus hook for this
/// card's own powered attacks and an optional level-threshold cost. The turn-1 play ban (햇빛 금기) comes from
/// SkofnungBehavior.CanPlayCard through MagicSwordCard.IsPlayable.
/// </summary>
public abstract class SkofnungCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : MagicSwordCard(cost, type, rarity, target)
{
    public sealed override SwordId? Sword => SwordId.Skofnung;

    /// <summary>Extra damage per hit of THIS card's powered attacks. Must be pure (preview).</summary>
    protected virtual decimal BonusDamage(Creature? target) => 0m;

    /// <summary>Energy cost at a Skofnung level (e.g. "3단계부터 비용 0"); null = printed cost.</summary>
    protected virtual int? CostAtLevel(int level) => null;

    /// <summary>Wounds on a creature (0 if none).</summary>
    protected static int WoundsOn(Creature? creature) => creature?.GetPowerAmount<SkofnungWoundPower>() ?? 0;

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
