using BaseLib.Abstracts;
using Mumyeong.MumyeongCode.Events;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Relics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace Mumyeong.MumyeongCode.Stages;

/// <summary>
/// 「영사실의 다음 릴」 — a stage-up (spec §5). Opened by <see cref="EventTriggers"/> (act change, ? event PROCEED) or
/// by the rest-site option <see cref="FilmReelRestSiteOption"/> when <see cref="StageRules.NextReadyStage"/> is set.
/// One more cut of the figure's memory is shown, then the stage reward:
///   동기화 = 1-of-3 figure card, 집중 = remove 1 card or upgrade 2 cards, 강림 = the figure's relic.
/// </summary>
public sealed class StageUpEvent : CustomEventModel
{
    public const int UpgradeCount = 2;

    private SoulStage? _stage;

    public StageUpEvent() : base(autoAdd: false)
    {
    }

    public override bool IsAllowed(IRunState runState) => false;

    // TODO(art): own film-reel portrait. Placeholder: the game's "Grave of the Forgotten" event art.
    public override string? CustomInitialPortraitPath => ImageHelper.GetImagePath("events/grave_of_the_forgotten.png");

    private string PageKey(string page) => $"{Id.Entry}.pages.{page}";

    private SoulCore? Core => Owner?.GetRelic<SoulCore>();

    protected override Task BeforeEventStarted(bool isPreFinished)
    {
        _stage = Core is { } core ? StageRules.NextReadyStage(core) : null;
        return Task.CompletedTask;
    }

    public override LocString InitialDescription =>
        IsMutable && Owner != null ? Description() : base.InitialDescription;

    private LocString Description()
    {
        if (Core is not { Figure: { } figure } || _stage is not { } stage)
            return L10NLookup($"{PageKey("NOTHING")}.description");

        var desc = L10NLookup($"{PageKey("INITIAL")}.description");
        desc.Add("Figure", FigureRegistry.Name(figure));
        desc.Add("Stage", StageRules.StageName(stage));
        desc.Add("Memory", FigureRegistry.Text(figure, $"MEMORY_{(int)stage}").GetFormattedText());
        return desc;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (Core is not { Figure: not null } || _stage is not { } stage)
            return [new EventOption(this, Leave, $"{PageKey("NOTHING")}.options.LEAVE")];

        return stage switch
        {
            SoulStage.Synchronization => [new EventOption(this, Synchronize, $"{PageKey("INITIAL")}.options.SYNC")],
            SoulStage.Focus =>
            [
                new EventOption(this, FocusRemove, $"{PageKey("INITIAL")}.options.FOCUS_REMOVE"),
                new EventOption(this, FocusUpgrade, $"{PageKey("INITIAL")}.options.FOCUS_UPGRADE"),
            ],
            SoulStage.Descent => [new EventOption(this, Descend, $"{PageKey("INITIAL")}.options.DESCENT")],
            _ => [new EventOption(this, Leave, $"{PageKey("NOTHING")}.options.LEAVE")],
        };
    }

    private Task Leave()
    {
        SetEventFinished(L10NLookup($"{PageKey("NOTHING")}.description"));
        return Task.CompletedTask;
    }

    private async Task Synchronize()
    {
        if (Core is not { Figure: { } figure } core) { await Leave(); return; }
        core.SetStage(SoulStage.Synchronization);
        await FigureCardPick.Offer(Owner!, figure, new BlockingPlayerChoiceContext());
        Finish(SoulStage.Synchronization);
    }

    private async Task FocusRemove()
    {
        if (Core is not { } core) { await Leave(); return; }
        core.SetStage(SoulStage.Focus);
        var cards = (await CardSelectCmd.FromDeckForRemoval(Owner!,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1), _ => true)).ToList();
        if (cards.Count > 0) await CardPileCmd.RemoveFromDeck(cards);
        Finish(SoulStage.Focus);
    }

    private async Task FocusUpgrade()
    {
        if (Core is not { } core) { await Leave(); return; }
        core.SetStage(SoulStage.Focus);
        var prompt = new LocString("card_selection", "MUMYEONG-UPGRADE_PROMPT");
        var cards = (await CardSelectCmd.FromDeckForUpgrade(Owner!, new CardSelectorPrefs(prompt, 0, UpgradeCount))).ToList();
        if (cards.Count > 0) CardCmd.Upgrade(cards, CardPreviewStyle.EventLayout);
        Finish(SoulStage.Focus);
    }

    private async Task Descend()
    {
        if (Core is not { Figure: { } figure } core) { await Leave(); return; }
        core.SetStage(SoulStage.Descent);
        await RelicCmd.Obtain(FigureRegistry.DescentRelic(figure).ToMutable(), Owner!);
        Finish(SoulStage.Descent);
    }

    private void Finish(SoulStage stage)
    {
        var end = L10NLookup($"{PageKey("DONE")}.description");
        end.Add("Stage", StageRules.StageName(stage));
        end.Add("Figure", Core?.Figure is { } f ? FigureRegistry.Name(f) : "");
        SetEventFinished(end);
    }
}
