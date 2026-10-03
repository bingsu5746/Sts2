using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Dainsleif;

/// <summary>
/// Base of every 다인슬레이프 card.
/// The game picks a played card's result pile BEFORE OnPlay (CardModel.OnPlayWrapper), i.e. before this card switches
/// the current sword to Dainsleif. So a Dainsleif card that is about to switch in applies Dainsleif's return rule to
/// ITSELF here (cards in the Play pile receive combat hooks); when Dainsleif is already current the behavior does it.
/// </summary>
public abstract class DainsleifCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : MagicSwordCard(cost, type, rarity, target)
{
    public override SwordId? Sword => SwordId.Dainsleif;

    /// <summary>Extra damage per hit of THIS card's powered attacks (also used by the preview: keep it pure).</summary>
    protected virtual decimal BonusDamage(Creature? target) => 0m;

    /// <summary>Cost override for the current sword level (e.g. "3단계부터 비용 1"); null = printed cost.</summary>
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

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card,
        bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
    {
        if (!ReferenceEquals(card, this) || Owner == null) return (pileType, position);
        var (effective, preview) = SwordCombat.EffectiveSwordFor(Owner, this);
        if (!preview || effective != SwordId.Dainsleif) return (pileType, position); // behavior handles "already current"
        return DainsleifReturn.Decide(Owner, this, resources, pileType, position, inherited: false);
    }
}
