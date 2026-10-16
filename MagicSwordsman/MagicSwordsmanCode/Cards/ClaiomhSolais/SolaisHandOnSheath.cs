using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// 칼집 위의 손 (Common): Retain. 7 Block (+1/L). Then you may spend 1 Light to give another card in your hand Retain
/// this turn (choose 0 or 1 card; Light is only spent when a card is chosen).
/// Reworked 2026-10-16 (user request: Light spending too samey): it was "7 Block +1 per Light (max +5)", a passive
/// per-Light scale. Now Light buys patience — keeping a card for the turn you end with Claíomh Solais in hand.
/// Retain this turn: game CardModel.GiveSingleTurnRetain (WellLaidPlansPower pattern).
/// </summary>
public sealed class SolaisHandOnSheath : SolaisCard
{
    public const int LightCost = 1;

    public SolaisHandOnSheath() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(7);
        WithBlockPerLevel(1);
        WithVar("LightCost", LightCost);
        WithKeywords(CardKeyword.Retain);
        WithTip(typeof(SolaisLightPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (Light < LightCost) return;
        var hand = PileType.Hand.GetPile(Owner).Cards;
        if (!hand.Any(c => !ReferenceEquals(c, this) && !c.ShouldRetainThisTurn)) return;
        var chosen = (await CardSelectCmd.FromHand(choiceContext, Owner,
                new CardSelectorPrefs(SelectionScreenPrompt, 0, 1),
                c => !ReferenceEquals(c, this) && !c.ShouldRetainThisTurn, this))
            .FirstOrDefault();
        if (chosen == null) return;
        if (!await SolaisLightSpend.TrySpend(choiceContext, Owner, LightCost, this)) return;
        chosen.GiveSingleTurnRetain();
    }
}
