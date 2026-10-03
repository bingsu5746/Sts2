using System.Runtime.CompilerServices;
using MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Relics;

/// <summary>
/// Shop sword offer (spec §5 [확정]; content doc §6.5 [Claude] numbers): on entering a merchant, 8% chance that one
/// character-card slot holds "칼집째 놓인 검" (<see cref="SheathedSwordCard"/>) for 180 gold ±10%. Buying it gives a
/// random unowned sword (same rules as the acquisition events: release a sword when Mangeomchong is full, Ganjiang·
/// Moye cost max HP on the first acquisition). If the acquisition does not happen (release cancelled) the gold is
/// refunded.
///
/// Game APIs (decompiled source): MerchantCardEntry.Populate/UpdateEntry call Hook.ModifyMerchantCardCreationResults
/// once per entry (so this must be idempotent), MerchantEntry.Cost calls Hook.ModifyMerchantPrice while the current
/// room is a MerchantRoom, MerchantEntry.OnTryPurchaseWrapper adds the card to the deck and then calls
/// Hook.AfterItemPurchased. MerchantRoom.EnterInternal creates the inventories after RunManager pushed the room, so
/// CurrentRoom is the MerchantRoom while entries are populated. Choice context for the release screen follows the
/// game's LeadPaperweight relic (BlockingPlayerChoiceContext outside combat).
/// Single player only (see the multiplayer TODO in ModifyCreationResults).
/// TODO(test): untested in game, especially the release screen opening on top of the merchant UI.
/// </summary>
public static class SwordShopOffer
{
    public const int ChancePercent = 8;

    /// <summary>The merchant room in which the 8% roll was already made for this player.</summary>
    private static readonly ConditionalWeakTable<Player, WeakReference<AbstractRoom>> Decided = new();

    public static void ModifyCreationResults(Mangeomchong tomb, Player player, List<CardCreationResult> cards)
    {
        if (player != tomb.Owner) return;
        if (player.RunState.CurrentRoom is not MerchantRoom room) return;

        // TODO(multiplayer): offer disabled with more than one player. A merchant purchase runs only on the buyer's
        // client (NMerchantCard -> MerchantEntry.OnTryPurchaseWrapper -> Hook.AfterItemPurchased); other clients only
        // receive RewardSynchronizer messages for the obtained card and the lost gold. Removing the sheathed sword,
        // the sword roll, AcquireSword (saved OwnedSwordIds / deck cards), the Ganjiang Max HP cost and the refund
        // would happen on the buyer's machine only and desync. Needs its own synced message to enable.
        if (player.RunState.Players.Count > 1) return;
        if (cards.Any(c => c.Card is SheathedSwordCard)) return; // already this entry's offer (UpdateEntry)

        if (Decided.TryGetValue(player, out var weak) && weak.TryGetTarget(out var decidedRoom) &&
            ReferenceEquals(decidedRoom, room))
            return; // one roll per merchant visit; restocked entries never become a sword
        Decided.AddOrUpdate(player, new WeakReference<AbstractRoom>(room));

        if (SwordEventHelper.Unowned(tomb).Count == 0) return;
        // Per-player stream (like the game's PaelsTooth / DustyTome): only this player's actions advance it.
        var rng = player.PlayerRng.Rewards;
        if (rng.NextInt(100) >= ChancePercent) return;

        var target = cards.FirstOrDefault(c => !c.HasBeenModified);
        if (target == null) return;
        var offer = (SheathedSwordCard)player.RunState.CreateCard(ModelDb.Card<SheathedSwordCard>(), player);
        offer.ShopPrice = (int)Math.Round(SheathedSwordCard.SwordShopPrice * rng.NextFloat(0.9f, 1.1f));
        target.ModifyCard(offer, tomb);
    }

    public static decimal ModifyPrice(Mangeomchong tomb, Player player, MerchantEntry entry, decimal cost)
    {
        if (player != tomb.Owner) return cost;
        if (entry is not MerchantCardEntry { CreationResult.Card: SheathedSwordCard offer } cardEntry) return cost;
        return cardEntry.IsOnSale ? offer.ShopPrice / 2 : offer.ShopPrice;
    }

    public static async Task AfterPurchased(Mangeomchong tomb, Player player, MerchantEntry entry, int goldSpent)
    {
        if (player != tomb.Owner || entry is not MerchantCardEntry) return;
        // CreationResult is already cleared/restocked here, so look for the bought offer in the deck.
        var bought = player.Deck.Cards.OfType<SheathedSwordCard>().ToList();
        if (bought.Count == 0) return;
        await CardPileCmd.RemoveFromDeck(bought.Cast<CardModel>().ToList(), showPreview: false);

        var acquired = false;
        var roll = SwordEventHelper.Roll(tomb, 1, player.PlayerRng.Rewards);
        if (roll.Count > 0)
            acquired = await SwordEventHelper.Acquire(player, roll[0], new BlockingPlayerChoiceContext());
        if (!acquired && goldSpent > 0)
            await PlayerCmd.GainGold(goldSpent, player);
    }
}
