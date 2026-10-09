using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Dialogue;

/// <summary>
/// Sword-to-sword banter (user request 2026-10-09): when every sword of a pair below is out in the same combat, the
/// one that arrives last may start their exchange (<see cref="Chance"/>, at most once per combat, played by
/// <see cref="SwordTalk"/>; it replaces that sword's summon line).
///
/// Lines (events table): MAGICSWORDSMAN-SWORD_TALK.BANTER.&lt;PAIR&gt;.&lt;n&gt;.&lt;i&gt;.&lt;SPEAKER&gt;
///   n = variant, i = line order (0, 1, 2 ...), SPEAKER = a sword key (GRAM, TYRFING ...) or ENSIFER.
///   Exactly one speaker per line index.
/// </summary>
public static class SwordBanter
{
    public const double Chance = 0.45;
    public const string Ensifer = "ENSIFER";
    private const int MaxVariants = 8;
    private const int MaxLines = 10;

    public sealed record Pair(string Key, IReadOnlyList<SwordId> Swords);

    public static readonly IReadOnlyList<Pair> Pairs =
    [
        new("GRAM_TYRFING", [SwordId.Gram, SwordId.Tyrfing]),                 // Norse rivals: hero's blade vs cursed blade
        new("DURANDAL_DAINSLEIF", [SwordId.Durandal, SwordId.Dainsleif]),     // the saint scolds the bloodthirsty one
        new("DURANDAL_TYRFING", [SwordId.Durandal, SwordId.Tyrfing]),         // holy relic vs dwarven curse
        new("KUSANAGI_ONIMARU", [SwordId.Kusanagi, SwordId.Onimaru]),         // countrymen
        new("CALADBOLG_CLAIOMHSOLAIS", [SwordId.Caladbolg, SwordId.ClaiomhSolais]), // Irish kin
        new("SKOFNUNG_GRAM", [SwordId.Skofnung, SwordId.Gram]),               // the twelve souls whisper
        new("SKOFNUNG_DAINSLEIF", [SwordId.Skofnung, SwordId.Dainsleif]),
        new("COUPLE_DAINSLEIF", [SwordId.Ganjiang, SwordId.Moye, SwordId.Dainsleif]), // Ganjiang & Moye tease others
        new("COUPLE_ONIMARU", [SwordId.Ganjiang, SwordId.Moye, SwordId.Onimaru]),
        new("COUPLE_CLAIOMHSOLAIS", [SwordId.Ganjiang, SwordId.Moye, SwordId.ClaiomhSolais]),
        new("COUPLE_GRAM", [SwordId.Ganjiang, SwordId.Moye, SwordId.Gram]),
    ];

    private static string Prefix(Pair pair) => $"{SwordAffinity.LocPrefix}.BANTER.{pair.Key}";

    /// <summary>Pairs <paramref name="arrived"/> completes now that it is out with <paramref name="present"/>.</summary>
    public static List<Pair> PairsFor(SwordId arrived, IReadOnlyCollection<SwordId> present) =>
        Pairs.Where(p => p.Swords.Contains(arrived) && p.Swords.All(s => s == arrived || present.Contains(s)))
            .ToList();

    /// <summary>Base keys (…&lt;n&gt;) of the pair's variants that exist in the loc table.</summary>
    public static List<string> Variants(Pair pair)
    {
        var list = new List<string>();
        for (var n = 0; n < MaxVariants; n++)
        {
            var k = $"{Prefix(pair)}.{n}";
            if (Lines(k).Count == 0) break;
            list.Add(k);
        }

        return list;
    }

    /// <summary>The variant's lines in order: (loc key, speaking sword or null for Ensifer).</summary>
    public static List<(string Key, SwordId? Speaker)> Lines(string variantKey)
    {
        var result = new List<(string, SwordId?)>();
        for (var i = 0; i < MaxLines; i++)
        {
            (string, SwordId?)? found = null;
            var ens = $"{variantKey}.{i}.{Ensifer}";
            if (LocString.Exists(SwordAffinity.LocTable, ens)) found = (ens, null);
            else
            {
                foreach (var sword in Enum.GetValues<SwordId>())
                {
                    var k = $"{variantKey}.{i}.{SwordTalk.SwordKey(sword)}";
                    if (!LocString.Exists(SwordAffinity.LocTable, k)) continue;
                    found = (k, sword);
                    break;
                }
            }

            if (found == null) break;
            result.Add(found.Value);
        }

        return result;
    }
}
