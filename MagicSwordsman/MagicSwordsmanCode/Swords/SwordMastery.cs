using MagicSwordsman.MagicSwordsmanCode.Cards.Caladbolg;
using MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;
using MagicSwordsman.MagicSwordsmanCode.Cards.Dainsleif;
using MagicSwordsman.MagicSwordsmanCode.Cards.Durandal;
using MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;
using MagicSwordsman.MagicSwordsmanCode.Cards.Gram;
using MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;
using MagicSwordsman.MagicSwordsmanCode.Cards.Onimaru;
using MagicSwordsman.MagicSwordsmanCode.Cards.Skofnung;
using MagicSwordsman.MagicSwordsmanCode.Cards.Tyrfing;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Swords;

/// <summary>
/// "검의 완성" — reward for bringing a sword to the max level (user request 2026-10-09 "검 최대 강화시 보상").
///
/// The first time in a run that a sword (a pair counts as one: 간장·막야) reaches level 5, its legend card — the Rare
/// card named after the sword's level-5 stage (content doc §1.4) — is added to the deck, and a popup shows it
/// (local player only). Once per sword per run (run counter <see cref="ClaimedKey"/> on Mangeomchong): losing the sword
/// and bringing it back to 5 again gives nothing more. The legend card is a normal card of that sword, so it is stored
/// with the sword's other cards if the sword is released and comes back with it, and it already hits at level-5 numbers.
///
/// Called from <see cref="Mangeomchong.SetLevel"/> (every level change: rest-site forge, events, ...), after the swords'
/// own OnLevelChanged (so Gram's 니벨룽의 보물 is added first).
/// Shown to the player in: the popup, the sword token card's hover tips (<see cref="TokenTips"/>, used wherever a sword
/// is picked), and the rest-site forge pages (<see cref="ForgeLine"/>).
/// Text: events table, MAGICSWORDSMAN-SWORD_MASTERY.* and MAGICSWORDSMAN-SWORD_LORE.&lt;SWORD&gt;.MASTERY
/// (fragment loc_fragments/upgrade_events.&lt;lang&gt;.events.json).
/// </summary>
public static class SwordMastery
{
    private const string Prefix = "MAGICSWORDSMAN-SWORD_MASTERY";

    /// <summary>Run counter key (Mangeomchong) set once the sword's mastery reward was given.</summary>
    public static string ClaimedKey(SwordId sword) =>
        $"mastery.{SwordRegistry.GroupLeader(sword).ToString().ToLowerInvariant()}";

    /// <summary>The canonical legend card of a sword (pairs use the leader). Null for a sword without one.</summary>
    public static CardModel? LegendCard(SwordId sword) => SwordRegistry.GroupLeader(sword) switch
    {
        SwordId.Gram => ModelDb.Card<GramFafnirSlayer>(),                  // 파프니르를 죽인 그람 -> 용 사냥
        SwordId.Ganjiang => ModelDb.Card<TwinMatedPair>(),                 // 삼 년 만의 완성 -> 한 쌍
        SwordId.Kusanagi => ModelDb.Card<KusanagiThreeRegalia>(),          // 삼종신기 -> 삼신기
        SwordId.Tyrfing => ModelDb.Card<TyrfingAlwaysVictorious>(),        // 언제나 이기는 칼 -> 필승
        SwordId.Dainsleif => ModelDb.Card<DainsleifHjadningavig>(),        // 햐드닝아비그 -> 영원한 전쟁
        SwordId.Durandal => ModelDb.Card<DurandalStPetersTooth>(),         // 흠집 없는 칼 -> 철벽
        SwordId.Skofnung => ModelDb.Card<SkofnungTwelveSouls>(),           // 열두 광전사 -> 열두 혼의 해방
        SwordId.Onimaru => ModelDb.Card<OnimaruFirstAmongFive>(),          // 다섯 중 으뜸 -> 천하오검
        SwordId.ClaiomhSolais => ModelDb.Card<SolaisSwordOfLight>(),       // 빛의 검
        SwordId.Caladbolg => ModelDb.Card<CaladbolgThreeHilltops>(),       // 세 언덕을 벤 검 -> 산 가르기
        _ => null,
    };

    public static bool IsClaimed(Mangeomchong tomb, SwordId sword) => tomb.GetRunCounter(ClaimedKey(sword)) > 0;

    /// <summary>Hook called by Mangeomchong.SetLevel after the level of <paramref name="sword"/> changed.</summary>
    internal static async Task AfterLevelChanged(Mangeomchong tomb, SwordId sword, int oldLevel, int newLevel)
    {
        if (oldLevel >= SwordRegistry.MaxLevel || newLevel < SwordRegistry.MaxLevel) return;
        sword = SwordRegistry.GroupLeader(sword);
        if (IsClaimed(tomb, sword) || LegendCard(sword) is not { } canonical) return;

        tomb.SetRunCounter(ClaimedKey(sword), 1);
        var owner = tomb.Owner;
        var card = owner.RunState.CreateCard(canonical, owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck));
        tomb.Flash();
        MainFile.Logger.Info($"[SwordMastery] {sword} reached level {newLevel}: added {canonical.Id.Entry}");

        if (LocalContext.IsMe(owner)) SwordMasteryPopup.Show(sword, canonical);
    }

    // ------------------------------------------------------------------ text for the UI

    public static LocString Text(string part) => new(SwordLore.Table, $"{Prefix}.{part}");

    /// <summary>Hover tips on a sword token: what level 5 gives (or that it was already given) + the card itself.</summary>
    public static IEnumerable<IHoverTip> TokenTips(CardModel token, SwordId sword)
    {
        if (LegendCard(sword) is not { } legend) return [];
        var tomb = token.IsMutable ? token.Owner?.GetRelic<Mangeomchong>() : null;
        var desc = Text(tomb != null && IsClaimed(tomb, sword) ? "TIP_CLAIMED" : "TIP");
        desc.Add("Card", legend.Title);
        desc.Add("Line", SwordLore.Line(sword, "MASTERY"));
        return [new HoverTip(Text("TIP_TITLE"), desc), HoverTipFactory.FromCard(legend)];
    }

    /// <summary>One line for the rest-site forge pages: "5단계 완성 보상: 「card」" (or already received).</summary>
    public static string ForgeLine(Mangeomchong tomb, SwordId sword)
    {
        if (LegendCard(sword) is not { } legend) return "";
        try
        {
            var line = Text(IsClaimed(tomb, sword) ? "FORGE_LINE_CLAIMED" : "FORGE_LINE");
            line.Add("Card", legend.Title);
            return line.GetFormattedText();
        }
        catch (Exception)
        {
            return "";
        }
    }
}
