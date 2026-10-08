using MagicSwordsman.MagicSwordsmanCode.RestSite;
using MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords;

/// <summary>
/// Helpers for content that gives swords (act 1/2 boss events, ? rooms, shops — spec §5).
/// Uses only the game's card-selection screens with sword token cards.
/// </summary>
public static class SwordAcquisition
{
    /// <summary>
    /// Shows the candidates (pairs as their leader, e.g. 간장·막야 -> Ganjiang token) and acquires the chosen one.
    /// If Mangeomchong has no room, the player is asked to release an owned sword first (Gram cannot be released);
    /// cancelling that aborts. Returns the acquired sword or null (skipped / aborted / nothing to offer).
    /// TODO(art): spec §5 [임시] origin slideshow on acquisition (full version only the first time).
    /// </summary>
    public static async Task<SwordId?> OfferChoice(Player player, IReadOnlyList<SwordId> candidates,
        PlayerChoiceContext choiceContext, bool canSkip = true)
    {
        var relic = player.GetRelic<Mangeomchong>();
        if (relic == null) return null;
        var offer = candidates.Select(SwordRegistry.GroupLeader).Distinct().Where(s => !relic.Owns(s)).ToList();
        if (offer.Count == 0) return null;

        var picked = await PickSword(player, offer, choiceContext, canSkip,
            new LocString("card_selection", "MAGICSWORDSMAN-CHOOSE_SWORD_TO_ACQUIRE"));
        if (picked is not { } sword) return null;

        return await AcquireWithRelease(player, relic, sword, choiceContext) ? sword : null;
    }

    /// <summary>
    /// Acquires <paramref name="sword"/> (and its partner). When Mangeomchong is full the player picks owned swords
    /// to release (Gram cannot be released). The picks are only collected at first; they are released only once the
    /// reserved room is enough for the new sword, so cancelling a later prompt loses nothing.
    /// Returns false when already owned, the player cancels, or nothing can make room.
    /// </summary>
    public static async Task<bool> AcquireWithRelease(Player player, Mangeomchong relic, SwordId sword,
        PlayerChoiceContext choiceContext)
    {
        sword = SwordRegistry.GroupLeader(sword);
        if (SwordRegistry.WithPartners(sword).Any(relic.Owns)) return false;

        var needed = SwordRegistry.GroupSlotCost(sword);
        var pending = new List<SwordId>();
        int Reserved() => relic.FreeSlots + pending.Sum(SwordRegistry.GroupSlotCost);

        while (Reserved() < needed)
        {
            var releasable = relic.OwnedSwords
                .Select(SwordRegistry.GroupLeader).Distinct()
                .Where(s => SwordRegistry.GetDefinition(s).CanBeLost)
                .Where(s => !pending.Contains(s))
                .ToList();
            if (releasable.Count == 0) return false;
            var release = await PickSword(player, releasable, choiceContext, canSkip: true,
                new LocString("card_selection", "MAGICSWORDSMAN-CHOOSE_SWORD_TO_RELEASE"));
            if (release is not { } r) return false; // nothing released yet
            pending.Add(r);
        }

        foreach (var r in pending) await relic.LoseSword(r);
        return await relic.AcquireSword(sword);
    }

    /// <summary>Acquisition with random candidates rolled from the given game Rng (spec §5: 3 candidates).</summary>
    public static async Task<SwordId?> OfferRandom(Player player, int count, MegaCrit.Sts2.Core.Random.Rng rng,
        PlayerChoiceContext choiceContext, bool canSkip = true)
    {
        var candidates = SwordRegistry.RollAcquisitionCandidates(player, count, rng);
        if (candidates.Count == 0)
        {
            // Everything that fits is owned: allow full-slot candidates so the player may swap.
            var relic = player.GetRelic<Mangeomchong>();
            candidates = SwordRegistry.AllSwords
                .Where(s => SwordRegistry.GetDefinition(s).OfferedByAcquisition && relic != null && !relic.Owns(s))
                .Take(count).ToList();
        }

        return await OfferChoice(player, candidates, choiceContext, canSkip);
    }

    /// <summary>Generic "pick one sword" screen (≤3: choose-a-card screen, otherwise a grid).</summary>
    public static async Task<SwordId?> PickSword(Player player, IReadOnlyList<SwordId> swords,
        PlayerChoiceContext choiceContext, bool canSkip, LocString gridPrompt)
    {
        if (swords.Count == 0) return null;
        var tokens = ChoiceTokenCard.CreateForSelection(player, swords.Select(SwordTokenCard.CanonicalFor));
        try
        {
            CardModel? picked;
            if (tokens.Count <= 3)
            {
                // the prompt doubles as the screen title (instead of "카드를 선택하세요") unless a caller set one
                using (ChooseScreenText.IsActive ? null : ChooseScreenText.Use(gridPrompt.GetRawText()))
                    picked = await CardSelectCmd.FromChooseACardScreen(choiceContext, tokens, player, canSkip);
            }
            else
            {
                var prefs = new CardSelectorPrefs(gridPrompt, 1) { Cancelable = canSkip };
                picked = (await CardSelectCmd.FromSimpleGrid(choiceContext, tokens, player, prefs)).FirstOrDefault();
            }

            return (picked as SwordTokenCard)?.Sword;
        }
        finally
        {
            ChoiceTokenCard.DisposeSelection(player, tokens);
        }
    }
}
