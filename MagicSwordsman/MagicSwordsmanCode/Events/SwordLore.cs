using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Events;

/// <summary>
/// Per-sword story text shared by the acquisition events and the rest-site forge story (content doc §1.4, §5.2, §6.3).
/// All keys live in the "events" table (fragment loc_fragments/g6/&lt;lang&gt;/events.json):
///   MAGICSWORDSMAN-SWORD_LORE.&lt;SWORD&gt;.&lt;PART&gt;
/// &lt;SWORD&gt; = the group leader's SwordId in upper case (Moye -> GANJIANG, the pair shares one text).
/// Parts: NAME, TAGLINE, COST, ORIGIN (acquisition) / FORGE_INTRO, FORGE_SAFE, FORGE_GAMBLE, FORGE_SUCCESS,
/// FORGE_FAILURE, FORGE_SHATTER (forge) / STAGE0, STAGE1 (levels 1-2), STAGE3 (3-4), STAGE5 (level names).
/// Every line is taken from the research report (docs/magic-sword-research.docx) via the content doc.
/// </summary>
public static class SwordLore
{
    public const string Table = "events";
    public const string Prefix = "MAGICSWORDSMAN-SWORD_LORE";

    public static string SwordKey(SwordId sword) => SwordRegistry.GroupLeader(sword).ToString().ToUpperInvariant();

    /// <summary>The sword's line; falls back to the sword's token title (NAME) or an empty string.</summary>
    public static LocString Line(SwordId sword, string part) =>
        LocString.GetIfExists(Table, $"{Prefix}.{SwordKey(sword)}.{part}") ?? new LocString(Table, $"{Prefix}.EMPTY");

    /// <summary>Generic (not per-sword) line, e.g. FORGE_GAMBLE3, ACQUIRE_SHORT.</summary>
    public static LocString Generic(string part) => new(Table, $"{Prefix}.{part}");

    public static LocString Name(SwordId sword) => Line(sword, "NAME");

    public static string NameText(SwordId sword)
    {
        try
        {
            return Name(sword).GetFormattedText();
        }
        catch (Exception)
        {
            return SwordRegistry.DisplayName(sword);
        }
    }

    /// <summary>Level-name key for a level 0..5 (content doc §1.4: 0 / 1-2 / 3-4 / 5).</summary>
    public static string StagePart(int level) => level switch
    {
        <= 0 => "STAGE0",
        <= 2 => "STAGE1",
        <= 4 => "STAGE3",
        _ => "STAGE5",
    };

    public static LocString StageName(SwordId sword, int level) => Line(sword, StagePart(level));
}
