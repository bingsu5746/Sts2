using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Relics;

namespace Mumyeong.MumyeongCode.Stages;

/// <summary>
/// Stage rules (spec §5): an act gate opens the next stage, mastery (figure cards played) or the figure's descent task
/// lets the soul actually enter it. Stage-ups only happen through <see cref="StageUpEvent"/> (act change, ? event
/// PROCEED, rest-site option "필름을 돌린다").
/// </summary>
public static class StageRules
{
    public const int SyncMastery = 25;
    public const int SyncMasteryResonance = 15;
    public const int FocusMastery = 70;
    public const int FocusMasteryResonance = 55;

    /// <summary>Act index (0 = act 1) from which the stage can be entered.</summary>
    public const int SyncActIndex = 1;
    public const int FocusActIndex = 2;
    public const int DescentActIndex = 2;

    /// <summary>Run counter set when the descent task was completed during the 집중 stage.</summary>
    public const string TaskDoneKey = "descent.task";

    public static int MasteryNeeded(SoulCore core, SoulStage stage)
    {
        var resonance = core.ContractKind == ContractType.Resonance;
        return stage switch
        {
            SoulStage.Synchronization => resonance ? SyncMasteryResonance : SyncMastery,
            SoulStage.Focus => resonance ? FocusMasteryResonance : FocusMastery,
            _ => 0,
        };
    }

    public static int ActIndexOf(SoulCore core) => core.Owner?.RunState.CurrentActIndex ?? 0;

    /// <summary>The stage the soul may enter now, or null.</summary>
    public static SoulStage? NextReadyStage(SoulCore core)
    {
        if (core.Figure == null) return null;
        var act = ActIndexOf(core);
        return core.CurrentStage switch
        {
            SoulStage.Possession when act >= SyncActIndex &&
                                      core.Mastery >= MasteryNeeded(core, SoulStage.Synchronization) => SoulStage.Synchronization,
            SoulStage.Synchronization when act >= FocusActIndex &&
                                           core.Mastery >= MasteryNeeded(core, SoulStage.Focus) => SoulStage.Focus,
            SoulStage.Focus when act >= DescentActIndex && core.GetRunCounter(TaskDoneKey) > 0 => SoulStage.Descent,
            _ => null,
        };
    }

    public static string StageName(SoulStage stage) =>
        FigureRegistry.Generic($"STAGE.{stage.ToString().ToUpperInvariant()}").GetFormattedText();

    public static string ContractName(ContractType contract) =>
        FigureRegistry.Generic($"CONTRACT.{contract.ToString().ToUpperInvariant()}").GetFormattedText();

    /// <summary>Tooltip lines of the 코어 (figure, contract, stage, what the next stage needs).</summary>
    public static string StatusText(SoulCore core)
    {
        if (core.Figure is not { } figure) return "\n" + FigureRegistry.Generic("STATUS.NO_FIGURE").GetFormattedText();

        var lines = new List<string>();
        var head = FigureRegistry.Generic("STATUS.HEAD");
        head.Add("Figure", FigureRegistry.Name(figure));
        head.Add("Contract", ContractName(core.ContractKind));
        head.Add("Stage", StageName(core.CurrentStage));
        lines.Add(head.GetFormattedText());

        var next = core.CurrentStage switch
        {
            SoulStage.Possession => SoulStage.Synchronization,
            SoulStage.Synchronization => SoulStage.Focus,
            SoulStage.Focus => SoulStage.Descent,
            _ => (SoulStage?)null,
        };
        if (next is { } n)
        {
            var ready = NextReadyStage(core) == n;
            if (n == SoulStage.Descent)
            {
                var line = FigureRegistry.Generic(ready ? "STATUS.READY" : "STATUS.TASK");
                line.Add("Stage", StageName(n));
                line.Add("Task", FigureRegistry.Text(figure, "TASK").GetFormattedText());
                lines.Add(line.GetFormattedText());
            }
            else
            {
                var line = FigureRegistry.Generic(ready ? "STATUS.READY" : "STATUS.MASTERY");
                line.Add("Stage", StageName(n));
                line.Add("Mastery", Math.Min(core.Mastery, MasteryNeeded(core, n)));
                line.Add("Needed", MasteryNeeded(core, n));
                line.Add("Act", (n == SoulStage.Synchronization ? SyncActIndex : FocusActIndex) + 1);
                lines.Add(line.GetFormattedText());
            }
        }
        else
        {
            lines.Add(FigureRegistry.Generic("STATUS.COMPLETE").GetFormattedText());
        }

        return "\n" + string.Join("\n", lines);
    }
}
