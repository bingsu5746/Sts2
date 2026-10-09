using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Random;

namespace MagicSwordsman.MagicSwordsmanCode.Events;

/// <summary>Shared rules of the sword acquisition events (content doc §6). Framework APIs only.</summary>
public static class SwordEventHelper
{
    public static Mangeomchong? Tomb(Player player) => player.GetRelic<Mangeomchong>();

    /// <summary>
    /// Swords an event may offer: offerable (every sword incl. Gram since 2026-10-09; not Moye — the pair is offered as
    /// Ganjiang) and not owned.
    /// Unlike <see cref="SwordRegistry.RollAcquisitionCandidates"/> this ignores free slots on purpose: content doc §6.1
    /// lets the player release a sword when Mangeomchong is full.
    /// </summary>
    public static List<SwordId> Unowned(Mangeomchong tomb) =>
        SwordRegistry.AllSwords
            .Where(s => SwordRegistry.GetDefinition(s).OfferedByAcquisition)
            .Select(SwordRegistry.GroupLeader)
            .Distinct()
            .Where(s => !SwordRegistry.WithPartners(s).Any(tomb.Owns))
            .ToList();

    /// <summary>Up to <paramref name="count"/> distinct unowned swords, rolled with a game Rng.</summary>
    public static List<SwordId> Roll(Mangeomchong tomb, int count, Rng rng)
    {
        var pool = Unowned(tomb);
        var result = new List<SwordId>();
        while (result.Count < count && pool.Count > 0)
        {
            var pick = pool[rng.NextInt(pool.Count)];
            pool.Remove(pick);
            result.Add(pick);
        }

        return result;
    }

    /// <summary>간장·막야 cost Max HP only on the first acquisition of the run (GanjiangBehavior.OnAcquired).</summary>
    public static bool CostsMaxHp(Mangeomchong tomb, SwordId sword) =>
        SwordRegistry.GroupLeader(sword) == SwordId.Ganjiang && !tomb.EverOwned(SwordId.Ganjiang);

    public static int MaxHpCost => GanjiangBehavior.MaxHpCost;

    /// <summary>
    /// Acquires <paramref name="sword"/> (and its partner). If Mangeomchong is full the player chooses owned swords to
    /// release (their cards go to storage, spec §2 [확정]; Gram can be released too since 2026-10-09). Releases are applied only when the
    /// acquisition is certain (see <see cref="SwordAcquisition.AcquireWithRelease"/>). Returns false when the player
    /// cancels or nothing can make room; in that case no sword was lost.
    /// A successful acquisition then offers 1 of 3 of the sword's cards (SwordCardPick) unless
    /// <paramref name="offerCardPick"/> is false.
    /// </summary>
    public static async Task<bool> Acquire(Player player, SwordId sword, PlayerChoiceContext choiceContext,
        bool offerCardPick = true)
    {
        var tomb = Tomb(player);
        if (tomb == null) return false;
        return await SwordAcquisition.AcquireWithRelease(player, tomb, sword, choiceContext, offerCardPick);
    }

    /// <summary>"{Tagline}\n대가: {Cost}" — description of a sword button (content doc §6.1 / §6.3).</summary>
    public static LocString OptionDescription(SwordId sword)
    {
        var desc = SwordLore.Generic("OPTION");
        desc.Add("Tagline", SwordLore.Line(sword, "TAGLINE"));
        desc.Add("Cost", SwordLore.Line(sword, "COST"));
        return desc;
    }

    /// <summary>Run-start pick: the sword's story page — origin, what it does as the current sword, and its cost.</summary>
    public static LocString StartAcquiredText(SwordId sword)
    {
        var text = SwordLore.Generic("ACQUIRED_START");
        text.Add("Sword", SwordLore.Name(sword));
        text.Add("Origin", SwordLore.Line(sword, "ORIGIN"));
        text.Add("Tagline", SwordLore.Line(sword, "TAGLINE"));
        text.Add("Cost", SwordLore.Line(sword, "COST"));
        return text;
    }

    /// <summary>Text after an acquisition: origin lines the first time (spec §5 [임시] 기원 연출), a short line otherwise.</summary>
    public static LocString AcquiredText(SwordId sword, bool firstTime)
    {
        var text = SwordLore.Generic(firstTime ? "ACQUIRED_FIRST" : "ACQUIRED_AGAIN");
        text.Add("Sword", SwordLore.Name(sword));
        text.Add("Origin", SwordLore.Line(sword, "ORIGIN"));
        return text;
    }
}
