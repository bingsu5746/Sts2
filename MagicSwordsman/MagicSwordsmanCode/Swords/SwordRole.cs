using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Swords;

/// <summary>
/// One-line ROLE of each sword ("이 검이 뭐 하는 검인지") for first-time players (user feedback 2026-10-17 "이 검이 뭐하는
/// 검인지나 검의 고유 능력 이런 게 잘 보이지 않고, 처음에 캐릭터를 하는 사람들은 이해가 잘 안 갈 것 같아").
/// The lines only restate the rules already in code (current-sword effect of each SwordBehavior, spec §7) in plain
/// words; they carry no numbers (the live numbers come from <see cref="SwordEffectText"/> right below them).
///
/// Shown in: the "new sword" popup, the 만검총 sword viewer, the run-start / tomb pick option titles, the sword token
/// cards, the current-sword power tooltip, the current-sword tag under the health bar, the floating-sword tooltip and
/// the sword-card hover tip (<see cref="SwordCardTip"/>).
/// Texts: events table, MAGICSWORDSMAN-SWORD_ROLE.&lt;SWORD&gt; (SwordId upper case) and .PAIR (간장·막야 as one offer),
/// fragment loc_fragments/clarity.&lt;lang&gt;.events.json.
/// </summary>
public static class SwordRole
{
    public const string Table = "events";
    private const string Prefix = "MAGICSWORDSMAN-SWORD_ROLE";

    /// <summary>The role key: the sword itself, or PAIR for 간장·막야 shown as one group.</summary>
    private static string Key(SwordId sword, bool pair) =>
        pair && SwordRegistry.GroupLeader(sword) == SwordId.Ganjiang ? "PAIR" : sword.ToString().ToUpperInvariant();

    public static LocString Loc(SwordId sword, bool pair = false) =>
        LocString.GetIfExists(Table, $"{Prefix}.{Key(sword, pair)}") ?? new LocString(Table, "MAGICSWORDSMAN-SWORD_LORE.EMPTY");

    /// <summary>
    /// Plain role text ("" when missing). <paramref name="pair"/>: 간장·막야 as one group (offers, popup, viewer page);
    /// false = the single blade (current sword, its own cards / token).
    /// </summary>
    public static string Text(SwordId sword, bool pair = false)
    {
        try { return Loc(sword, pair).GetFormattedText(); }
        catch (Exception) { return ""; }
    }

    /// <summary>The role in the colour every role line uses ([aqua]), for rich-text tooltips. "" when missing.</summary>
    public static string Rich(SwordId sword, bool pair = false)
    {
        var text = Text(sword, pair);
        return text.Length == 0 ? "" : $"[aqua]{text}[/aqua]";
    }
}
