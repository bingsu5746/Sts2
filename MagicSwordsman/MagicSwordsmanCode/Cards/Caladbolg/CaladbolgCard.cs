using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Caladbolg;

/// <summary>
/// Base of every 칼라드볼그 card: Sword = Caladbolg, a card-specific block bonus that also shows in the combat preview,
/// and a level-threshold cost. Spreading to 3 enemies / the single-enemy penalty come from Caladbolg's current
/// effect (CaladbolgBehavior), which every Caladbolg card switches to before its own effect.
/// </summary>
public abstract class CaladbolgCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : MagicSwordCard(cost, type, rarity, target)
{
    public sealed override SwordId? Sword => SwordId.Caladbolg;

    /// <summary>Hittable enemies right now (0 outside combat).</summary>
    protected int EnemyCount => IsMutable && Owner?.PlayerCombatState != null
        ? CaladbolgSpread.HittableEnemyCount(Owner.Creature)
        : 0;

    /// <summary>Extra block of THIS card (preview too: keep it pure).</summary>
    protected virtual decimal BonusBlock() => 0m;

    /// <summary>Cost override for the current sword level; null = printed cost.</summary>
    protected virtual int? CostAtLevel(int level) => null;

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        var bonus = base.ModifyBlockAdditive(target, block, props, cardSource, cardPlay);
        if (ReferenceEquals(cardSource, this)) bonus += BonusBlock();
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
