using MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.RestSite;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace MagicSwordsman.MagicSwordsmanCode.Events.Forge;

/// <summary>Result of one forge attempt plus story facts the result screen needs.</summary>
public readonly record struct ForgeStoryResult(ForgeOutcome Outcome, bool GotNibelung);

/// <summary>
/// Per-sword "마검 강화 이벤트" story for the rest-site option (spec §4 [확정] "검마다 강화 이벤트(스토리)가 다름";
/// content doc §5). Used by <see cref="SwordForgeRestSiteOption"/>:
///   sword pick -> <see cref="ShowIntro"/> (2-3 intro lines + current level name; skip = 그만둔다)
///   -> <see cref="PickMode"/> (안전 / 도박 +2 / 도박 +3 with the sword's own phrase and the real odds incl. relics)
///   -> <see cref="Apply"/> -> <see cref="ShowResult"/> (the sword's success / failure / shatter line + result).
/// Only the game's choose-a-card screen is used (token cards), like the rest of the framework's forge UI.
/// Text: <see cref="SwordLore"/> keys (events table) + token keys in cards.json (fragment g6).
/// </summary>
public static class ForgeStory
{
    /// <summary>Intro screen. Returns false when the player skips (= 그만둔다 "아직은 때가 아니다").</summary>
    public static async Task<bool> ShowIntro(Player owner, SwordId sword, int level, PlayerChoiceContext ctx)
    {
        var tokens = ChoiceTokenCard.CreateForSelection(owner, [ModelDb.Card<ForgeStoryIntroToken>()]);
        try
        {
            if (tokens[0] is ForgeStoryIntroToken intro)
            {
                intro.Sword = sword;
                intro.Level = level;
            }

            return await CardSelectCmd.FromChooseACardScreen(ctx, tokens, owner, canSkip: true) != null;
        }
        finally
        {
            ChoiceTokenCard.DisposeSelection(owner, tokens);
        }
    }

    /// <summary>
    /// Mode pick with the sword's phrases. Gambles that would exceed level 5 are not offered (spec §4 [임시]; the
    /// choose-a-card screen cannot show a greyed-out card). Odds include relics (레긴의 모루) via SwordForge.OddsFor.
    /// </summary>
    public static async Task<ForgeMode?> PickMode(Player owner, SwordId sword, int level, PlayerChoiceContext ctx)
    {
        var canon = new List<CardModel> { ModelDb.Card<ForgeStorySafeToken>() };
        if (SwordForge.IsAllowed(ForgeMode.GamblePlus2, level)) canon.Add(ModelDb.Card<ForgeStoryGamble2Token>());
        if (SwordForge.IsAllowed(ForgeMode.GamblePlus3, level)) canon.Add(ModelDb.Card<ForgeStoryGamble3Token>());

        var tokens = ChoiceTokenCard.CreateForSelection(owner, canon);
        try
        {
            foreach (var token in tokens.OfType<ForgeStoryModeToken>())
            {
                token.Sword = sword;
                var odds = SwordForge.OddsFor(owner, sword, token.Mode);
                token.DynamicVars["Success"].BaseValue = odds.Success;
                token.DynamicVars["Failure"].BaseValue = odds.Failure;
                token.DynamicVars["Shatter"].BaseValue = odds.Shatter;
            }

            var picked = await CardSelectCmd.FromChooseACardScreen(ctx, tokens, owner, canSkip: true);
            return (picked as ForgeModeToken)?.Mode;
        }
        finally
        {
            ChoiceTokenCard.DisposeSelection(owner, tokens);
        }
    }

    /// <summary>SwordForge.Apply + "did this attempt give 니벨룽의 보물" (GramBehavior sets its run counter once).</summary>
    public static async Task<ForgeStoryResult> Apply(Player owner, SwordId sword, ForgeMode mode, Rng rng)
    {
        var tomb = owner.GetRelic<Mangeomchong>();
        var before = tomb?.GetRunCounter(GramBehavior.NibelungCounter) ?? 0;
        var outcome = await SwordForge.Apply(owner, sword, mode, rng);
        var after = tomb?.GetRunCounter(GramBehavior.NibelungCounter) ?? 0;
        return new ForgeStoryResult(outcome, before == 0 && after > 0);
    }

    public static async Task ShowResult(Player owner, ForgeStoryResult result, PlayerChoiceContext ctx)
    {
        var tokens = ChoiceTokenCard.CreateForSelection(owner, [ModelDb.Card<ForgeStoryResultToken>()]);
        try
        {
            if (tokens[0] is ForgeStoryResultToken token) token.Result = result;
            await CardSelectCmd.FromChooseACardScreen(ctx, tokens, owner, canSkip: true);
        }
        finally
        {
            ChoiceTokenCard.DisposeSelection(owner, tokens);
        }
    }
}
