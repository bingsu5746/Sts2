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
    /// Ganjiang) and not owned. The run's still-sealed first sword (Mangeomchong.SealedFirstSword) counts as owned here:
    /// the run-start pick rolls its three candidates before the roulette awakens it, and must not offer it.
    /// Unlike <see cref="SwordRegistry.RollAcquisitionCandidates"/> this ignores free slots on purpose: content doc §6.1
    /// lets the player release a sword when Mangeomchong is full.
    /// </summary>
    public static List<SwordId> Unowned(Mangeomchong tomb) =>
        SwordRegistry.AllSwords
            .Where(s => SwordRegistry.GetDefinition(s).OfferedByAcquisition)
            .Select(SwordRegistry.GroupLeader)
            .Distinct()
            .Where(s => !SwordRegistry.WithPartners(s).Any(p => tomb.Owns(p) || tomb.IsSealedFirstSword(p)))
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

    /// <summary>
    /// "{Effect} · 대가 {Cost}" — ONE line under a sword button (2026-10-15: the old two-line "tagline + cost" made the
    /// act-3 page overflow; the full effect and cost stay in the button's token hover tip). Numbers of a new (level 0)
    /// sword. 간장·막야's first-time Max HP price keeps its lore line.
    /// </summary>
    /// <summary>
    /// Title of a sword button: "{Name} · {Role}" (user feedback 2026-10-17: the role must be visible while choosing; the
    /// title row keeps the one-line-per-sword layout of 2026-10-15).
    /// </summary>
    public static LocString OptionTitle(SwordId sword)
    {
        var title = SwordLore.Generic("OPTION_TITLE");
        title.Add("Name", SwordLore.Name(sword));
        title.Add("Role", SwordRole.Loc(sword, pair: true));
        return title;
    }

    public static LocString OptionDescription(SwordId sword, Mangeomchong? tomb)
    {
        var desc = SwordLore.Generic("OPTION");
        var effect = SwordEffectText.FirstLine(SwordEffectText.Effect(sword, null, 0));
        if (sword == SwordId.Ganjiang)
            effect += " / " + SwordEffectText.FirstLine(SwordEffectText.Effect(SwordId.Moye, null, 0));
        desc.Add("Effect", effect);
        if (sword == SwordId.Ganjiang && tomb != null && CostsMaxHp(tomb, sword))
            desc.Add("Cost", SwordLore.Line(sword, "COST"));
        else
            desc.Add("Cost", SwordEffectText.Cost(sword, tomb?.Owner, 0));
        return desc;
    }

    /// <summary>
    /// Run-start pick: the sword's story page — origin, its role, what it does as the current sword (level-0 numbers) and
    /// its cost. (2026-10-17: the "현재 검일 때" line used to show the lore tagline instead of the effect.)
    /// </summary>
    public static LocString StartAcquiredText(SwordId sword)
    {
        var text = SwordLore.Generic("ACQUIRED_START");
        text.Add("Sword", SwordLore.Name(sword));
        text.Add("Origin", SwordLore.Line(sword, "ORIGIN"));
        text.Add("Tagline", SwordLore.Line(sword, "TAGLINE"));
        text.Add("Role", SwordRole.Loc(sword, pair: true));
        text.Add("Effect", SwordEffectText.Effect(sword, null, 0, withPartner: true));
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
