using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;

/// <summary>
/// Base of every 쿠사나기 card: Sword = Kusanagi, plus two small helpers used by several cards
/// (a card-specific damage bonus that also shows in the combat preview, and a level-threshold cost).
/// </summary>
public abstract class KusanagiCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : MagicSwordCard(cost, type, rarity, target)
{
    public override SwordId? Sword => SwordId.Kusanagi;

    /// <summary>Extra damage per hit of THIS card's powered attacks (evaluated for the preview too: keep it pure).</summary>
    protected virtual decimal BonusDamage(Creature? target) => 0m;

    /// <summary>Cost override for the current sword level (e.g. "3단계부터 비용 0"); null = printed cost.</summary>
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

/// <summary>
/// 정화 (purification) helpers — content doc §2.3: Basic/Common/Uncommon reduce ONE debuff type by 1
/// (PowerCmd.ModifyAmount, i.e. PowerCmd.Decrement), Rare removes one completely (PowerCmd.Remove); status/curse
/// cards are exhausted with CardCmd.Exhaust. The debuff picked is the visible debuff with the largest amount
/// (the game has no power-selection screen); negative-amount debuffs (e.g. Strength below 0) move 1 toward 0.
/// </summary>
public static class KusanagiPurify
{
    public static bool IsStatusOrCurse(CardModel card) => card.Type is CardType.Status or CardType.Curse;

    public static PowerModel? StrongestDebuff(Creature creature) =>
        creature.Powers
            .Where(p => p.IsVisible && p.TypeForCurrentAmount == PowerType.Debuff)
            .OrderByDescending(p => Math.Abs(p.Amount))
            .FirstOrDefault();

    public static bool HasDebuff(Creature creature) => StrongestDebuff(creature) != null;

    /// <summary>Reduce one debuff type by 1. Returns false if there was none.</summary>
    public static async Task<bool> ReduceOneDebuff(PlayerChoiceContext choiceContext, Creature creature, CardModel source)
    {
        var debuff = StrongestDebuff(creature);
        if (debuff == null) return false;
        var offset = debuff.AllowNegative && debuff.Amount < 0 ? 1m : -1m;
        await PowerCmd.ModifyAmount(choiceContext, debuff, offset, null, source);
        return true;
    }

    /// <summary>Remove one debuff type completely. Returns false if there was none.</summary>
    public static async Task<bool> RemoveOneDebuff(Creature creature)
    {
        var debuff = StrongestDebuff(creature);
        if (debuff == null) return false;
        await PowerCmd.Remove(debuff);
        return true;
    }

    /// <summary>Choose 1 status/curse card in hand and exhaust it (nothing happens if there is none).</summary>
    public static async Task<bool> ExhaustOneFromHand(PlayerChoiceContext choiceContext, Player player, CardModel source)
    {
        if (!PileType.Hand.GetPile(player).Cards.Any(IsStatusOrCurse)) return false;
        var chosen = (await CardSelectCmd.FromHand(choiceContext, player,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1), IsStatusOrCurse, source)).FirstOrDefault();
        if (chosen == null) return false;
        await CardCmd.Exhaust(choiceContext, chosen);
        return true;
    }

    /// <summary>Exhaust every status/curse card in hand. Returns how many were exhausted.</summary>
    public static async Task<int> ExhaustAllFromHand(PlayerChoiceContext choiceContext, Player player)
    {
        var cards = PileType.Hand.GetPile(player).Cards.Where(IsStatusOrCurse).ToList();
        foreach (var card in cards) await CardCmd.Exhaust(choiceContext, card);
        return cards.Count;
    }

    /// <summary>Choose 1 status/curse card from the draw or discard pile and exhaust it.</summary>
    public static async Task<bool> ExhaustOneFromDrawOrDiscard(PlayerChoiceContext choiceContext, Player player)
    {
        var options = PileType.Draw.GetPile(player).Cards
            .Concat(PileType.Discard.GetPile(player).Cards)
            .Where(IsStatusOrCurse)
            .ToList();
        if (options.Count == 0) return false;
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, options, player,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1))).FirstOrDefault();
        if (chosen == null) return false;
        await CardCmd.Exhaust(choiceContext, chosen);
        return true;
    }

    /// <summary>Status/curse cards of this player exhausted so far this combat (combat history).</summary>
    public static int ExhaustedThisCombat(Player player) =>
        CombatManager.Instance.History.Entries.OfType<CardExhaustedEntry>()
            .Count(e => e.Card.Owner == player && IsStatusOrCurse(e.Card));
}
