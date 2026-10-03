using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Onimaru;

// 오니마루 구니쓰나 — 자율 퇴마 · 명령 · 10장 (content doc §2.8). Basic 2 · Common 3 · Uncommon 3 · Rare 2.
// 명령 4장, 종류 파워 5장. The auto attack itself (turn end, 25% wild kind in normal fights, elite/boss bonus) is in
// OnimaruBehavior / OnimaruAttack. Lore lines (research doc): 「검이 스스로 움직여 오니를 베었다는 이야기가 『태평기』에
// 실려 있다」 「쇼군가 3대 보검」 「천하오검 중 하나」 「주인의 손을 거치지 않고 스스로 움직여 요괴를 베었다는 '자율 퇴마'
// 전설」 「교호 명물첩에서 다섯 자루 중 '으뜸'」.

internal static class OnimaruTips
{
    public static TooltipSource Iai => new(_ => HoverTipFactory.FromPower<OnimaruIaiPower>());
    public static TooltipSource Ranbu => new(_ => HoverTipFactory.FromPower<OnimaruRanbuPower>());
    public static TooltipSource Giri => new(_ => HoverTipFactory.FromPower<OnimaruGiriPower>());
    public static TooltipSource Taima => new(_ => HoverTipFactory.FromPower<OnimaruTaimaPower>());
    public static TooltipSource Goei => new(_ => HoverTipFactory.FromPower<OnimaruGoeiPower>());
}

/// <summary>오니 베기 (Basic): 【명령】. (Iai at level 0 with the current bonus = 6 = Strike.)</summary>
public sealed class OnimaruOniSlash : OnimaruCard
{
    public OnimaruOniSlash() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithKindValueVar("IaiDamage", OnimaruKind.Iai);
    }

    protected override Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Command(choiceContext, cardPlay);
}

/// <summary>호위 (Basic power): kind → 호위 (Block). Strike once with it right away.</summary>
public sealed class OnimaruGoei : OnimaruCard
{
    public OnimaruGoei() : base(1, CardType.Power, CardRarity.Basic, TargetType.Self)
    {
        WithKindValueVar("AutoBlock", OnimaruKind.Goei);
        WithTip(OnimaruTips.Goei);
    }

    protected override Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ChangeKind(choiceContext, OnimaruKind.Goei, strikeNow: true);
}

/// <summary>
/// 거합 (Common power, cost 0): kind → 거합 (back to the default [확정]). This combat 거합 deals +2 damage (+3 from
/// level 3); stacks when played again. No immediate attack.
/// </summary>
public sealed class OnimaruIai : OnimaruCard
{
    public OnimaruIai() : base(0, CardType.Power, CardRarity.Common, TargetType.Self)
    {
        WithCalculatedVar("IaiBonus", 2, static (card, _) => card is MagicSwordCard { SwordLevel: >= 3 } ? 1 : 0);
        WithTip(OnimaruTips.Iai);
    }

    protected override Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ChangeKind(choiceContext, OnimaruKind.Iai, strikeNow: false, iaiAdd: SwordLevel >= 3 ? 3 : 2);
}

/// <summary>베기 (Common power): kind → 베기 (all enemies). Strike once right away.</summary>
public sealed class OnimaruGiri : OnimaruCard
{
    public OnimaruGiri() : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
        WithKindValueVar("AutoDamage", OnimaruKind.Giri);
        WithTip(OnimaruTips.Giri);
    }

    protected override Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ChangeKind(choiceContext, OnimaruKind.Giri, strikeNow: true);
}

/// <summary>오니를 쫓다 (Common): 【명령】. Draw 1 card.</summary>
public sealed class OnimaruOniHunt : OnimaruCard
{
    public OnimaruOniHunt() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithCards(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Command(choiceContext, cardPlay);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

/// <summary>난무 (Uncommon power): kind → 난무 (random enemies, several hits). Strike once right away.</summary>
public sealed class OnimaruRanbu : OnimaruCard
{
    public OnimaruRanbu() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithKindValueVar("AutoDamage", OnimaruKind.Ranbu);
        WithRanbuHitsVar("AutoHits");
        WithTip(OnimaruTips.Ranbu);
    }

    protected override Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ChangeKind(choiceContext, OnimaruKind.Ranbu, strikeNow: true);
}

/// <summary>퇴마 (Uncommon power): kind → 퇴마 (one random enemy + Weak 1). Strike once right away.</summary>
public sealed class OnimaruTaima : OnimaruCard
{
    public OnimaruTaima() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithKindValueVar("AutoDamage", OnimaruKind.Taima);
        WithTip(OnimaruTips.Taima);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<WeakPower>()));
    }

    protected override Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ChangeKind(choiceContext, OnimaruKind.Taima, strikeNow: true);
}

/// <summary>태평기의 기록 (Uncommon, cost 2): 【명령】 twice.</summary>
public sealed class OnimaruTaiheiki : OnimaruCard
{
    public OnimaruTaiheiki() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithVar("Times", 2);
    }

    protected override Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Command(choiceContext, cardPlay, DynamicVars["Times"].IntValue);
}

/// <summary>다섯 중 으뜸 (Rare, cost 2): 【명령】 three times.</summary>
public sealed class OnimaruFirstAmongFive : OnimaruCard
{
    public OnimaruFirstAmongFive() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithVar("Times", 3);
    }

    protected override Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Command(choiceContext, cardPlay, DynamicVars["Times"].IntValue);
}

/// <summary>
/// 스스로 움직이는 칼 (Rare power, cost 2; 1 from level 3): this combat Onimaru also auto-attacks at the start of
/// your turn (2 auto attacks per turn).
/// </summary>
public sealed class OnimaruSelfMovingBlade : OnimaruCard
{
    public OnimaruSelfMovingBlade() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<OnimaruSelfMovingBladePower>()));
    }

    protected override int? CostAtLevel(int level) => level >= 3 ? 1 : null;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.HasPower<OnimaruSelfMovingBladePower>()) return;
        await PowerCmd.Apply<OnimaruSelfMovingBladePower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
}
