using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

// 클라이브 솔라시 메커니즘 카드 2장 (2026-10-17). After the 2026-10-16 rework Light already had many gain/spend
// patterns; two were still missing: MULTIPLYING the stack (광휘) and playing around the COST — Light fading when
// Solais is not current at the end of the turn (빛 갈무리). Common 1 · Uncommon 1.

/// <summary>
/// 광휘 (Uncommon skill, Exhaust; cost 0 from level 3): double your Light (with no Light, gain 1). Catalyst-style
/// amplifier for 【발도】 / 빛의 검 / 왕위 복귀. Light has no cap (content doc §2.9).
/// </summary>
public sealed class SolaisRadiance : SolaisCard
{
    public SolaisRadiance() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithTip(typeof(SolaisLightPower));
    }

    protected override int? CostAtLevel(int level) => level >= 3 ? 0 : null;

    protected override bool ShouldGlowGoldInternal => Light >= 2;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var light = SolaisLight.Get(Owner);
        await SolaisLight.Gain(choiceContext, Owner, light > 0 ? light : 1, this);
    }
}

/// <summary>
/// 빛 갈무리 (Common skill, cost 0): draw 1 card. At the end of this turn your Light does not fade even if Claíomh
/// Solais is not your current sword (<see cref="SolaisShadedLightPower"/>, consumed by ClaiomhSolaisBehavior). It does
/// not charge Light either — only the current-sword effect does that.
/// </summary>
public sealed class SolaisShadedLight : SolaisCard
{
    public SolaisShadedLight() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(1);
        WithTip(typeof(SolaisLightPower));
        WithTip(SwordMechanicTips.Effect(SwordId.ClaiomhSolais));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        if (!Owner.Creature.HasPower<SolaisShadedLightPower>())
            await PowerCmd.Apply<SolaisShadedLightPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
}
