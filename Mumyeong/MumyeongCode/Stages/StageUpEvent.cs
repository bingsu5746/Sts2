using BaseLib.Abstracts;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
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

    private async Task Synchronize() => await Reward(SoulStage.Synchronization, false);

    private async Task FocusRemove() => await Reward(SoulStage.Focus, false);

    private async Task FocusUpgrade() => await Reward(SoulStage.Focus, true);

    private async Task Descend() => await Reward(SoulStage.Descent, false);

    private async Task Reward(SoulStage stage, bool upgrade)
    {
        if (Core is not { Figure: not null } core) { await Leave(); return; }
        await StageRewards.Apply(Owner!, core, stage, upgrade, new BlockingPlayerChoiceContext());
        Finish(stage);
    }

    private void Finish(SoulStage stage)
    {
        var end = L10NLookup($"{PageKey("DONE")}.description");
        end.Add("Stage", StageRules.StageName(stage));
        end.Add("Figure", Core?.Figure is { } f ? FigureRegistry.Name(f) : "");
        SetEventFinished(end);
    }
}
