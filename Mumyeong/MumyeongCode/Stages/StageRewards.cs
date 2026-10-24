using Mumyeong.MumyeongCode.Events;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Mumyeong.MumyeongCode.Stages;

/// <summary>
/// Stage-up rewards (spec §5), shared by <see cref="StageUpEvent"/> and <see cref="FilmReelRestSiteOption"/>.
/// The stage is written to the 코어 first, so a cancelled card screen cannot grant the stage twice.
/// </summary>
public static class StageRewards
{
    public const int UpgradeCount = 2;

    public static async Task Apply(Player player, SoulCore core, SoulStage stage, bool upgradeInsteadOfRemove,
        PlayerChoiceContext choiceContext)
    {
        if (core.Figure is not { } figure) return;
        core.SetStage(stage);
        MainFile.Logger.Info($"[Stage] {figure} -> {stage}");
        switch (stage)
        {
            case SoulStage.Synchronization:
                await FigureCardPick.Offer(player, figure, choiceContext);
                break;
            case SoulStage.Focus:
                if (upgradeInsteadOfRemove) await UpgradeCards(player);
                else await RemoveCard(player);
                break;
            case SoulStage.Descent:
                await RelicCmd.Obtain(FigureRegistry.DescentRelic(figure).ToMutable(), player);
                break;
        }
    }

    public static async Task RemoveCard(Player player)
    {
        var cards = (await CardSelectCmd.FromDeckForRemoval(player,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1), _ => true)).ToList();
        if (cards.Count > 0) await CardPileCmd.RemoveFromDeck(cards);
    }

    public static async Task UpgradeCards(Player player)
    {
        var prompt = new LocString("card_selection", "MUMYEONG-UPGRADE_PROMPT");
        var cards = (await CardSelectCmd.FromDeckForUpgrade(player, new CardSelectorPrefs(prompt, 0, UpgradeCount))).ToList();
        if (cards.Count > 0) CardCmd.Upgrade(cards, CardPreviewStyle.EventLayout);
    }
}
