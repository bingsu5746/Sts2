using Mumyeong.MumyeongCode.Figures;
using MegaCrit.Sts2.Core.Localization;

namespace Mumyeong.MumyeongCode.Events;

/// <summary>
/// Scenes of the 황천 영사실 (spec §4, §8). A scene is a situation + three cuts (시선 → 행동 → 유언); every cut has one
/// line per figure, written from that figure's value tags (game interpretation, not historical claims).
/// Text: events table, <c>MUMYEONG-THEATER.&lt;SCENE&gt;.DESC</c> and <c>MUMYEONG-THEATER.&lt;SCENE&gt;.&lt;CUT&gt;.&lt;FIGURE&gt;</c>.
/// To add a scene: add its key here and its 1 + 3 × (number of figures) lines in both languages.
/// </summary>
public static class TheaterScenes
{
    public const string Table = "events";
    public const string Prefix = "MUMYEONG-THEATER";

    public static readonly IReadOnlyList<string> All =
    [
        "BRIDGE", "SIEGE", "STORM", "TEMPLE", "SCAFFOLD", "SNOWMARCH",
        "FLOOD", "WELL", "MINE", "PLAGUE", "BORDER", "CORONATION",
        "ARENA", "AMBUSH", "MUTINY", "FAMINE", "DUEL", "EXILE",
        "FALSE_ORDER", "RIVER", "TRIBUNAL", "HOSTAGE", "LAST_CAMP", "TOWER",
    ];

    public static readonly IReadOnlyList<string> Cuts = ["GAZE", "ACTION", "WORDS"];

    public static LocString Description(string scene) => new(Table, $"{Prefix}.{scene}.DESC");

    public static LocString Line(string scene, int cut, FigureId figure) =>
        LocString.GetIfExists(Table, $"{Prefix}.{scene}.{Cuts[cut]}.{FigureRegistry.Key(figure)}") ??
        new LocString(Table, $"{Prefix}.MISSING_LINE");

    public static LocString Generic(string part) => new(Table, $"{Prefix}.{part}");
}
