using BaseLib.Abstracts;
using MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;
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
///   2. pick 안전 강화 (+1) / 도박 강화 +2 / 도박 강화 +3 (gambles that would exceed level 5 are not offered)
///   3. roll with the run's Niche Rng, apply, show a result card.
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

        // 2) mode
        var level = relic.GetLevel(sword.Value);
        var mode = await PickMode(choiceContext, level);
        if (mode == null) return false;

        // 3) roll + apply (game RNG, never System.Random)
        var outcome = await SwordForge.Apply(Owner, sword.Value, mode.Value, Owner.RunState.Rng.Niche);
        relic.Flash();
        MainFile.Logger.Info($"[Forge] {sword} {mode}: {outcome}");

        await ShowResult(choiceContext, outcome);
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
