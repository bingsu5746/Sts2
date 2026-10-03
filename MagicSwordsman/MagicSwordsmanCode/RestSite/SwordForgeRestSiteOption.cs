using BaseLib.Abstracts;
using MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;
using MagicSwordsman.MagicSwordsmanCode.Events.Forge;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.RestSite;

/// <summary>
/// Rest-site option "마검 강화" added by Mangeomchong (pattern: Girya -> LiftRestSiteOption).
/// Flow (all with the game's own card-selection screens, cancellable):
///   1. pick an owned sword (sword token cards; a pair shows its leader, e.g. 간장·막야 -> Ganjiang)
///   2. the sword's story intro (Events/Forge/ForgeStory.cs)
///   3. pick 안전 강화 (+1) / 도박 강화 +2 / 도박 강화 +3 (gambles that would exceed level 5 are not offered)
///   4. roll with the run's Niche Rng, apply, show the sword's result card.
/// The old PickMode/ShowResult helpers (plain ForgeModeToken / ForgeResultToken) are kept as a fallback.
/// Returning false from OnSelect (cancel) leaves the rest site open, like the game's Smith option.
/// Localization: rest_site_ui.json  OPTION_MAGICSWORDSMAN_FORGE.name / .description
/// </summary>
public sealed class SwordForgeRestSiteOption(Player owner) : CustomRestSiteOption(owner)
{
    public const string Id = "MAGICSWORDSMAN_FORGE";

    public override string OptionId => Id;

    // TODO(art): dedicated icon. Reuses the game's Smith icon (path verified in SmithRestSiteOption).
    public override string? CustomIconPath => ImageHelper.GetImagePath("ui/rest_site/option_smith.png");

    private Mangeomchong? Relic => Owner.GetRelic<Mangeomchong>();

    public override bool IsEnabled => Relic is { } r && SwordForge.UpgradeableSwords(r).Count > 0;

    public override async Task<bool> OnSelect()
    {
        var relic = Relic;
        if (relic == null) return false;
        var candidates = SwordForge.UpgradeableSwords(relic);
        if (candidates.Count == 0) return false;

        var choiceContext = new BlockingPlayerChoiceContext();

        // 1) sword
        var sword = await PickSword(choiceContext, candidates);
        if (sword == null) return false;

        // 2) the sword's story intro (Events/Forge/ForgeStory.cs, spec §4 "검마다 강화 이벤트가 다름"); skip = cancel
        var level = relic.GetLevel(sword.Value);
        if (!await ForgeStory.ShowIntro(Owner, sword.Value, level, choiceContext)) return false;

        // 3) mode (story phrases + odds after relic modifiers)
        var mode = await ForgeStory.PickMode(Owner, sword.Value, level, choiceContext);
        if (mode == null) return false;

        // 4) roll + apply (game RNG, never System.Random). Per-player stream: in multiplayer every client runs each
        // player's OnSelect, in different orders (RestSiteSynchronizer runs remote choices when their message
        // arrives), so a shared stream like RunState.Rng.Niche would give each client a different result.
        var result = await ForgeStory.Apply(Owner, sword.Value, mode.Value, Owner.PlayerRng.Rewards);
        relic.Flash();
        MainFile.Logger.Info($"[Forge] {sword} {mode}: {result.Outcome}");

        await ForgeStory.ShowResult(Owner, result, choiceContext);
        return true;
    }

    private Task<SwordId?> PickSword(PlayerChoiceContext ctx, IReadOnlyList<SwordId> candidates) =>
        SwordAcquisition.PickSword(Owner, candidates, ctx, canSkip: true,
            new LocString("card_selection", "MAGICSWORDSMAN-CHOOSE_SWORD_TO_FORGE"));

    private async Task<ForgeMode?> PickMode(PlayerChoiceContext ctx, int level)
    {
        var canon = new List<CardModel> { ModelDb.Card<ForgeSafeToken>() };
        if (SwordForge.IsAllowed(ForgeMode.GamblePlus2, level)) canon.Add(ModelDb.Card<ForgeGamble2Token>());
        if (SwordForge.IsAllowed(ForgeMode.GamblePlus3, level)) canon.Add(ModelDb.Card<ForgeGamble3Token>());

        var tokens = ChoiceTokenCard.CreateForSelection(Owner, canon);
        try
        {
            var picked = await CardSelectCmd.FromChooseACardScreen(ctx, tokens, Owner, canSkip: true);
            return (picked as ForgeModeToken)?.Mode;
        }
        finally
        {
            ChoiceTokenCard.DisposeSelection(Owner, tokens);
        }
    }

    private async Task ShowResult(PlayerChoiceContext ctx, ForgeOutcome outcome)
    {
        var tokens = ChoiceTokenCard.CreateForSelection(Owner, [ModelDb.Card<ForgeResultToken>()]);
        try
        {
            if (tokens[0] is ForgeResultToken result) result.Outcome = outcome;
            await CardSelectCmd.FromChooseACardScreen(ctx, tokens, Owner, canSkip: true);
        }
        finally
        {
            ChoiceTokenCard.DisposeSelection(Owner, tokens);
        }
    }
}
