using BaseLib.Patches.Content;
using MagicSwordsman.MagicSwordsmanCode.Cards.Basic;
using MagicSwordsman.MagicSwordsmanCode.Cards.Caladbolg;
using MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;
using MagicSwordsman.MagicSwordsmanCode.Cards.Common;
using MagicSwordsman.MagicSwordsmanCode.Cards.Durandal;
using MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;
using MagicSwordsman.MagicSwordsmanCode.Cards.Gram;
using MagicSwordsman.MagicSwordsmanCode.Cards.Onimaru;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards;

/// <summary>
/// Tooltip-only keywords for the mod's own terms. BaseLib's [CustomEnum] generates a CardKeyword value for each
/// field (BaseLib.Patches.Content.GenEnumValues); with no KeywordProperties attribute the keyword adds no card text
/// (AutoKeywordPosition.None) and BaseLib's HoverTipFactory.FromKeyword patch builds the tip from
/// card_keywords "MAGICSWORDSMAN-&lt;NAME&gt;.title/.description".
/// The keywords are NOT added to the cards' keyword sets; cards only show them as hover tips (TipsFor), so no game
/// logic that inspects card keywords is affected. The card text keeps writing the term in [gold].
/// </summary>
public static class MagicSwordsmanKeywords
{
    [CustomEnum("COMBO")] public static CardKeyword Combo;
    [CustomEnum("PAIR")] public static CardKeyword Pair;
    [CustomEnum("TWIN_SWORD")] public static CardKeyword TwinSword;
    [CustomEnum("COMMAND")] public static CardKeyword Command;
    [CustomEnum("DRAW_CUT")] public static CardKeyword DrawCut;
    [CustomEnum("CURRENT_SWORD")] public static CardKeyword CurrentSword;

    /// <summary>
    /// Which terms each card's text uses (generated from the kor card descriptions at integration time).
    /// Read lazily: the CardKeyword values only exist after BaseLib's enum generation (ModelDb.Init prefix).
    /// </summary>
    private static Dictionary<Type, CardKeyword[]>? _map;

    private static Dictionary<Type, CardKeyword[]> Map => _map ??= new()
    {
        [typeof(Cards.Basic.SwordSwap)] = [CurrentSword],
        [typeof(Cards.Caladbolg.CaladbolgLetheBlade)] = [CurrentSword],
        [typeof(Cards.ClaiomhSolais.SolaisFourTreasures)] = [CurrentSword],
        [typeof(Cards.ClaiomhSolais.SolaisInescapable)] = [DrawCut],
        [typeof(Cards.ClaiomhSolais.SolaisIrresistible)] = [DrawCut],
        [typeof(Cards.ClaiomhSolais.SolaisMagTuired)] = [DrawCut],
        [typeof(Cards.ClaiomhSolais.SolaisSheathOfDeath)] = [DrawCut],
        [typeof(Cards.ClaiomhSolais.SolaisSilverArm)] = [DrawCut],
        [typeof(Cards.Common.AfterimageCut)] = [CurrentSword],
        [typeof(Cards.Common.BladeGale)] = [CurrentSword],
        [typeof(Cards.Common.CallSword)] = [CurrentSword],
        [typeof(Cards.Common.Deflection)] = [CurrentSword],
        [typeof(Cards.Common.DoubleSwap)] = [CurrentSword],
        [typeof(Cards.Common.FlipGrip)] = [CurrentSword],
        [typeof(Cards.Common.FlyingSwap)] = [CurrentSword],
        [typeof(Cards.Common.GuardSwap)] = [CurrentSword],
        [typeof(Cards.Common.Hone)] = [CurrentSword],
        [typeof(Cards.Common.LinkedStrike)] = [CurrentSword],
        [typeof(Cards.Common.OneBladeFocus)] = [CurrentSword],
        [typeof(Cards.Common.QuickThrust)] = [CurrentSword],
        [typeof(Cards.Common.SwordDance)] = [CurrentSword],
        [typeof(Cards.Common.SwordFormation)] = [CurrentSword],
        [typeof(Cards.Common.SwordHeart)] = [CurrentSword],
        [typeof(Cards.Common.SwordResonance)] = [CurrentSword],
        [typeof(Cards.Common.SwordSaint)] = [CurrentSword],
        [typeof(Cards.Durandal.DurandalHiddenBeneath)] = [CurrentSword],
        [typeof(Cards.Durandal.DurandalTenBlows)] = [CurrentSword],
        [typeof(Cards.GanjiangMoye.GanjiangAssassin)] = [Pair],
        [typeof(Cards.GanjiangMoye.GanjiangChisVengeance)] = [Pair],
        [typeof(Cards.GanjiangMoye.GanjiangSlash)] = [Pair],
        [typeof(Cards.GanjiangMoye.GanjiangThreeKingsTomb)] = [Pair],
        [typeof(Cards.GanjiangMoye.MoyeBellows)] = [Pair],
        [typeof(Cards.GanjiangMoye.MoyeGuard)] = [Pair],
        [typeof(Cards.GanjiangMoye.MoyeHairAndNails)] = [Pair],
        [typeof(Cards.GanjiangMoye.MoyeRipplePattern)] = [Pair],
        [typeof(Cards.GanjiangMoye.MoyeUnmeltingIron)] = [Pair],
        [typeof(Cards.GanjiangMoye.MoyesSacrifice)] = [Pair],
        [typeof(Cards.GanjiangMoye.TwinMatedPair)] = [Pair, TwinSword],
        [typeof(Cards.GanjiangMoye.TwinSwordDance)] = [Pair, TwinSword],
        [typeof(Cards.GanjiangMoye.TwinYanpingDragons)] = [Pair, TwinSword],
        [typeof(Cards.Gram.GramBalmungNothung)] = [CurrentSword],
        [typeof(Cards.Gram.GramBladeBetween)] = [Combo],
        [typeof(Cards.Gram.GramFafnirsHeart)] = [CurrentSword],
        [typeof(Cards.Gram.GramFleeceCutter)] = [Combo],
        [typeof(Cards.Gram.GramKeptShards)] = [Combo],
        [typeof(Cards.Onimaru.OnimaruFirstAmongFive)] = [Command],
        [typeof(Cards.Onimaru.OnimaruLeapFromSheath)] = [Command, Combo],
        [typeof(Cards.Onimaru.OnimaruOniHunt)] = [Command],
        [typeof(Cards.Onimaru.OnimaruOniSlash)] = [Command],
        [typeof(Cards.Onimaru.OnimaruTaiheiki)] = [Command],
        [typeof(Curses.MasterlessBlade)] = [Command],
    };

    public static IEnumerable<IHoverTip> TipsFor(CardModel card) =>
        Map.TryGetValue(card.GetType(), out var keywords)
            ? keywords.Select(HoverTipFactory.FromKeyword)
            : [];
}
