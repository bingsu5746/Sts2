using Mumyeong.MumyeongCode.Character;
using Mumyeong.MumyeongCode.Figures;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace Mumyeong.MumyeongCode.Events;

/// <summary>
/// "그의 기술 하나를 익힌다": 3 random non-Basic cards of the figure, pick 1 or skip (contract, 동기화 reward).
/// Same pattern as 엔시페르 SwordCardPick / the game's LeadPaperweight: CardFactory.CreateForReward from the player's
/// own reward stream + CardSelectCmd.FromChooseACardScreen (synced choice in multiplayer).
/// </summary>
public static class FigureCardPick
{
    public const int ChoiceCount = 3;

    private static Func<CardModel, bool> FilterFor(FigureId figure) =>
        card => FigureRegistry.IsCardOf(card, figure) &&
                card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare;

    public static async Task Offer(Player player, FigureId figure, PlayerChoiceContext choiceContext,
        CardRarityOddsType odds = CardRarityOddsType.RegularEncounter)
    {
        var pool = ModelDb.CardPool<MumyeongCardPool>();
        var filter = FilterFor(figure);
        var available = pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(filter).Select(c => c.Id).Distinct().Count();
        if (available == 0) return;

        var options = new CardCreationOptions([pool], CardCreationSource.Other, odds, filter)
            .WithFlags(CardCreationFlags.NoUpgradeRoll | CardCreationFlags.NoCardPoolModifications);
        var cards = CardFactory.CreateForReward(player, Math.Min(ChoiceCount, available), options)
            .Select(r => r.Card).ToList();
        if (cards.Count == 0) return;

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, cards, player, canSkip: true);
        if (chosen != null) CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(chosen, PileType.Deck));
        MainFile.Logger.Info($"[FigureCardPick] {figure}: offered {string.Join(",", cards.Select(c => c.Id))}, " +
                             $"picked {chosen?.Id.ToString() ?? "nothing"}");
    }
}
