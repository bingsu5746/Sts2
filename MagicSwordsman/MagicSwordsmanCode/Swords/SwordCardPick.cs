using MagicSwordsman.MagicSwordsmanCode.Cards;
using MagicSwordsman.MagicSwordsmanCode.Cards.Union;
using MagicSwordsman.MagicSwordsmanCode.Character;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MagicSwordsman.MagicSwordsmanCode.RestSite;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace MagicSwordsman.MagicSwordsmanCode.Swords;

/// <summary>
/// 사용자 결정 2026-10-09 ("검 얻을 때마다 뽑기로 한 장 정도는 고를 수 있게"): after a sword is acquired (and its
/// starter cards / stored cards went into the deck), the player is offered 3 random cards from that sword's own card
/// pool (간장·막야: the pair's pool), picks 1 or skips.
///
/// Same pattern as the game's LeadPaperweight relic: CardFactory.CreateForReward (non-Basic cards, normal card-reward
/// rarity odds with base odds since the source is not an encounter, no upgrade roll) + CardSelectCmd.FromChooseACardScreen,
/// which reserves a synced choice id: in multiplayer only the acquiring player sees the screen and the other clients wait
/// for the synced index. The cards are rolled from the player's own PlayerRng.Rewards stream, identical on every client.
/// 【조합】 cards are not part of a sword's own pool (they need a second sword and appear in normal rewards).
/// Header "「{Sword}」의 기술을 하나 익힌다" and a "넘기기" skip button via <see cref="ChooseScreenText"/>.
/// </summary>
public static class SwordCardPick
{
    public const int ChoiceCount = 3;

    /// <summary>Non-Basic cards of the sword (and its partner), excluding 【조합】 cards.</summary>
    private static Func<CardModel, bool> FilterFor(SwordId sword)
    {
        var group = SwordRegistry.WithPartners(sword);
        return card => card is MagicSwordCard { Sword: { } s } and not UnionCard && group.Contains(s) &&
                       card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare;
    }

    public static async Task Offer(Player player, SwordId sword, PlayerChoiceContext choiceContext)
    {
        await WaitForPopups();
        sword = SwordRegistry.GroupLeader(sword);
        var pool = ModelDb.CardPool<MagicSwordsmanCardPool>();
        var filter = FilterFor(sword);
        var available = pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(filter).Select(c => c.Id).Distinct().Count();
        if (available == 0) return;

        // NoCardPoolModifications: other relics (Prismatic Shard...) must not add foreign pools to this pick.
        var options = new CardCreationOptions([pool], CardCreationSource.Other, CardRarityOddsType.RegularEncounter, filter)
            .WithFlags(CardCreationFlags.NoUpgradeRoll | CardCreationFlags.NoCardPoolModifications);
        var cards = CardFactory.CreateForReward(player, Math.Min(ChoiceCount, available), options)
            .Select(r => r.Card).ToList();
        if (cards.Count == 0) return;

        var header = new LocString("card_selection", "MAGICSWORDSMAN-SWORD_CARD_PICK_HEADER");
        header.Add("Sword", SwordLore.NameText(sword));
        var skip = new LocString("card_selection", "MAGICSWORDSMAN-SWORD_CARD_PICK_SKIP");

        CardModel? chosen;
        using (ChooseScreenText.Use(header.GetFormattedText(), skip: skip.GetFormattedText()))
            chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, cards, player, canSkip: true);

        if (chosen != null) CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(chosen, PileType.Deck));

        foreach (var card in cards.Where(c => c != chosen))
            player.RunState.CurrentMapPointHistoryEntry?.GetEntry(player.NetId).CardChoices
                .Add(new CardChoiceHistoryEntry(card, wasPicked: false));
        MainFile.Logger.Info($"[SwordCardPick] {sword}: offered {string.Join(",", cards.Select(c => c.Id))}, " +
                             $"picked {chosen?.Id.ToString() ?? "nothing"}");
    }

    private static readonly string[] PopupNames =
        ["MagicSwordAcquiredPopup", "MagicSwordUnionUnlockedPopup", "MagicSwordMasteryPopup", "MagicSwordUnionPopupWaiter"];

    /// <summary>
    /// The new-sword / new-combo / mastery popups sit on a higher canvas layer than the choose-a-card screen, so the
    /// pick waits until they are closed (or at most 60 s) instead of opening underneath them. Presentation only: the
    /// popups exist only on the acquiring player's client, so other clients go straight on to wait for the choice.
    /// </summary>
    private static async Task WaitForPopups()
    {
        try
        {
            if (Godot.Engine.GetMainLoop() is not Godot.SceneTree tree) return;
            for (var waited = 0.0; waited < 60.0; waited += 0.1)
            {
                var open = false;
                foreach (var n in PopupNames)
                    if (tree.Root.GetNodeOrNull(n) is { } node && !node.IsQueuedForDeletion()) { open = true; break; }
                if (!open) return;
                await tree.ToSignal(tree.CreateTimer(0.1), Godot.SceneTreeTimer.SignalName.Timeout);
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordCardPick] popup wait: {e.Message}");
        }
    }
}
