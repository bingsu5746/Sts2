using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Common;

/// <summary>
/// Shared rules of the common (non-sword) card pool, content doc §4. Common cards never change the current sword
/// (content doc §0.4 #2) except the "검 바꾸기" family, which goes through <see cref="ChooseAndSwitch"/>
/// (same path as the starter card SwordSwap: <see cref="SwordCombat.SwitchCandidates"/> +
/// <see cref="SwordAcquisition.PickSword"/> + <see cref="SwordCombat.SwitchTo"/> with SwapEffect).
/// </summary>
public static class CommonCardRules
{
    /// <summary>Prompt for the sword grid when more than 3 swords can be chosen (main card_selection table).</summary>
    public static LocString SwitchPrompt => new("card_selection", "MAGICSWORDSMAN-CHOOSE_SWORD_TO_SWITCH");

    /// <summary>Prompt for "summon a sword that is not out yet" choices (g5 card_selection fragment).</summary>
    public static LocString SummonPrompt => new("card_selection", "MAGICSWORDSMAN-CHOOSE_SWORD_TO_SUMMON");

    /// <summary>Prompt for "this combat, treat a sword's level as higher" choices (g5 card_selection fragment).</summary>
    public static LocString TemperPrompt => new("card_selection", "MAGICSWORDSMAN-CHOOSE_SWORD_TO_TEMPER");

    /// <summary>The card's owner while the card is a real combat card; null for canonical cards / out of combat.</summary>
    public static Player? CombatOwner(CardModel card)
    {
        if (!card.IsMutable) return null;
        var owner = card.Owner;
        return owner?.PlayerCombatState == null ? null : owner;
    }

    /// <summary>"나온 검 수" (content doc §0.3): swords floating around this combat (summoned + emerged, vault excluded).</summary>
    public static int PresentCount(Player? player) =>
        player == null ? 0 : SwordCombat.Get(player)?.Present.Count ?? 0;

    /// <summary>Present swords other than the current one (검진).</summary>
    public static int PresentNotCurrentCount(Player? player)
    {
        var state = player == null ? null : SwordCombat.Get(player);
        if (state == null) return 0;
        var n = state.Present.Count;
        if (state.Current is { } cur && state.Present.Contains(cur)) n--;
        return Math.Max(0, n);
    }

    /// <summary>How many times the current sword changed this turn (framework counter).</summary>
    public static int SwitchesThisTurn(Player? player) => player == null ? 0 : SwordCombat.SwitchesThisTurn(player);

    public static bool SwitchedThisTurn(Player? player) => SwitchesThisTurn(player) > 0;

    /// <summary>Static multiplier helpers for calculated vars (the game requires static lambdas).</summary>
    public static decimal PresentCountOf(CardModel card) => PresentCount(CombatOwner(card));

    public static decimal SwitchesThisTurnOf(CardModel card) => SwitchesThisTurn(CombatOwner(card));

    public static decimal SwitchedThisTurnOf(CardModel card) => SwitchedThisTurn(CombatOwner(card)) ? 1m : 0m;

    /// <summary>
    /// "보유 검 하나로 전환": choose one of the swords that may become current now (excluding the current one;
    /// automatic when only one) and switch to it, summoning it if it is not out yet (Mangeomchong bonus applies).
    /// Returns the new current sword, or null when nothing switched.
    /// </summary>
    public static async Task<SwordId?> ChooseAndSwitch(Player player, PlayerChoiceContext choiceContext)
    {
        var candidates = SwordCombat.SwitchCandidates(player);
        if (candidates.Count == 0) return null;
        var chosen = candidates.Count == 1
            ? candidates[0]
            : await SwordAcquisition.PickSword(player, candidates, choiceContext, canSkip: false, SwitchPrompt);
        if (chosen is not { } sword) return null;
        return await SwordCombat.SwitchTo(player, sword, choiceContext, SwitchReason.SwapEffect) ? sword : null;
    }

    /// <summary>Owned swords that have not come out this combat and could become current now (소환 대상).</summary>
    public static IReadOnlyList<SwordId> NotYetOutCandidates(Player player)
    {
        var relic = player.GetRelic<Mangeomchong>();
        var state = SwordCombat.Get(player);
        if (relic == null || state == null) return [];
        return relic.OwnedSwords
            .Where(s => !state.Present.Contains(s) && !state.Summoned.Contains(s))
            .Where(s => SwordCombat.CanSwitchTo(player, s, SwitchReason.SwapEffect))
            .ToList();
    }

    /// <summary>
    /// Choose one sword that is not out yet and switch to it (summon + current). Null when there is none.
    /// </summary>
    public static async Task<SwordId?> ChooseAndSummon(Player player, PlayerChoiceContext choiceContext)
    {
        var candidates = NotYetOutCandidates(player);
        if (candidates.Count == 0) return null;
        var chosen = candidates.Count == 1
            ? candidates[0]
            : await SwordAcquisition.PickSword(player, candidates, choiceContext, canSkip: false, SummonPrompt);
        if (chosen is not { } sword) return null;
        return await SwordCombat.SwitchTo(player, sword, choiceContext, SwitchReason.SwapEffect) ? sword : null;
    }

    /// <summary>Whether the card is a sword card of <paramref name="sword"/> (Ganjiang and Moye count as one group).</summary>
    public static bool IsCardOfSword(CardModel card, SwordId sword) =>
        card is MagicSwordCard { Sword: { } s } && SwordRegistry.GroupLeader(s) == SwordRegistry.GroupLeader(sword);

    /// <summary>Sword card of an owned sword that has not come out yet this combat (소환의 자세).</summary>
    public static bool IsCardOfNotYetSummonedSword(Player player, CardModel card)
    {
        if (card is not MagicSwordCard { Sword: { } s }) return false;
        var relic = player.GetRelic<Mangeomchong>();
        var state = SwordCombat.Get(player);
        if (relic == null || state == null || !relic.Owns(s)) return false;
        return !state.Present.Contains(s) && !state.Summoned.Contains(s);
    }

    /// <summary>
    /// Choose 1 card matching <paramref name="filter"/> in a combat pile and put it into the hand (pattern: game card
    /// Hologram; the draw pile is shown sorted by the game, so its order is not revealed). Null when none matches.
    /// </summary>
    public static async Task<CardModel?> PickFromPileToHand(Player player, PileType pileType,
        PlayerChoiceContext choiceContext, LocString prompt, Func<CardModel, bool> filter)
    {
        var pile = pileType.GetPile(player);
        if (!pile.Cards.Any(filter)) return null;
        var picked = (await CardSelectCmd.FromCombatPile(choiceContext, pile, player,
            new CardSelectorPrefs(prompt, 1), filter)).FirstOrDefault();
        if (picked != null) await CardPileCmd.Add(picked, PileType.Hand);
        return picked;
    }

    /// <summary>
    /// 칼날 세우기 / 담금질 물: the current sword (or, with no current sword, one owned sword the player chooses)
    /// counts as <paramref name="levels"/> levels higher for the rest of this combat (max 5).
    /// Returns the sword that was tempered, or null when nothing could be raised.
    /// </summary>
    public static async Task<SwordId?> TemperSword(Player player, int levels, PlayerChoiceContext choiceContext)
    {
        var state = SwordCombat.Get(player);
        var relic = player.GetRelic<Mangeomchong>();
        if (state == null || relic == null || levels <= 0) return null;

        SwordId? target = state.Current;
        if (target == null)
        {
            var candidates = relic.OwnedSwords
                .Where(s => SwordCombat.LevelOf(player, s) < SwordRegistry.MaxLevel)
                .DistinctBy(s => SwordRegistry.GetDefinition(s).LevelOwner)
                .ToList();
            if (candidates.Count == 0) return null;
            target = candidates.Count == 1
                ? candidates[0]
                : await SwordAcquisition.PickSword(player, candidates, choiceContext, canSkip: false, TemperPrompt);
        }

        if (target is not { } sword) return null;
        await AddCombatLevels(player, sword, levels, choiceContext);
        return sword;
    }

    /// <summary>
    /// "이번 전투 동안 단계를 +N으로 취급(최대 5)": adds to the per-combat level bonus of the sword's LevelOwner
    /// (<see cref="SwordCombat.CombatLevelBonusKey"/>, read by <see cref="SwordCombat.LevelOf"/>) without passing 5,
    /// then refreshes the "현재 검" power. Returns the levels actually gained.
    /// </summary>
    public static async Task<int> AddCombatLevels(Player player, SwordId sword, int levels,
        PlayerChoiceContext choiceContext)
    {
        var state = SwordCombat.Get(player);
        if (state == null || levels <= 0) return 0;
        var before = SwordCombat.LevelOf(player, sword);
        var gained = Math.Min(SwordRegistry.MaxLevel, before + levels) - before;
        if (gained > 0)
            state.AddCounter(SwordRegistry.GetDefinition(sword).LevelOwner, SwordCombat.CombatLevelBonusKey, gained);
        await SwordCombat.EnsureCurrentSwordPower(player, choiceContext);
        MainFile.Logger.Info($"[Common] {sword}: level {before} -> {before + gained} this combat");
        return gained;
    }
}
