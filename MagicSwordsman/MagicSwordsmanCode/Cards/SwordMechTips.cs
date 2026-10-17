using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Cards;

/// <summary>
/// Hover tips that explain a sword's core mechanic in plain words for a new player (mech2 pass, 2026-10-17, user request
/// "현재의 검들의 메커니즘을 제대로 반영하는 카드들"). Same pattern as UnionCatalog.TipFor: a plain HoverTip built from the
/// card_keywords table ("MAGICSWORDSMAN-MECH_&lt;NAME&gt;.title/.description"), so no CardKeyword enum value is needed and
/// the cards' keyword sets stay untouched. Add them to a card with <c>WithTip(SwordMechTips.X)</c>.
/// </summary>
public static class SwordMechTips
{
    private static IHoverTip Tip(string name) =>
        new HoverTip(new LocString("card_keywords", $"MAGICSWORDSMAN-MECH_{name}.title"),
            new LocString("card_keywords", $"MAGICSWORDSMAN-MECH_{name}.description"));

    /// <summary>그람의 현재 검 효과: 공격 피해 +강화 단계 (5단계에 처음 닿으면 니벨룽의 보물).</summary>
    public static TooltipSource GramLevel => new(_ => Tip("GRAM_LEVEL"));

    /// <summary>The Nibelung curse card itself.</summary>
    public static TooltipSource Nibelung => new(_ => HoverTipFactory.FromCard<NibelungTreasure>());

    /// <summary>간장 / 막야의 현재 검 효과 (공격 피해 / 방어도).</summary>
    public static TooltipSource PairBlades => new(_ => Tip("PAIR_BLADES"));

    /// <summary>【짝】 keyword.</summary>
    public static TooltipSource Pair => new(_ => HoverTipFactory.FromKeyword(MagicSwordsmanKeywords.Pair));

    /// <summary>쿠사나기의 계승.</summary>
    public static TooltipSource Inherit => new(_ => Tip("INHERIT"));

    /// <summary>쿠사나기 카드의 정화.</summary>
    public static TooltipSource Purify => new(_ => Tip("PURIFY"));

    /// <summary>쿠사나기 바꿔 들기 제한 (전투당 2번).</summary>
    public static TooltipSource KusanagiLimit => new(_ => Tip("KUSANAGI_LIMIT"));

    /// <summary>오니마루의 자동 공격과 자세.</summary>
    public static TooltipSource AutoAttack => new(_ => Tip("AUTO_ATTACK"));

    /// <summary>【명령】 keyword.</summary>
    public static TooltipSource Command => new(_ => HoverTipFactory.FromKeyword(MagicSwordsmanKeywords.Command));

    /// <summary>칼라드볼그의 현재 검 효과: 최대 3명, 적이 1명뿐이면 피해 감소.</summary>
    public static TooltipSource ThreeHills => new(_ => Tip("THREE_HILLS"));

    /// <summary>현재 검 keyword.</summary>
    public static TooltipSource CurrentSword => new(_ => HoverTipFactory.FromKeyword(MagicSwordsmanKeywords.CurrentSword));
}
