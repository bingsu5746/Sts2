using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

/// <summary>
/// 칼집에 손을 얹다 (Common): Retain. 7 Block (+1/L) + 1 per Light (max +5).
/// A Retain card so the turn can end with Claíomh Solais current.
/// </summary>
public sealed class SolaisHandOnSheath : SolaisCard
{
    public const int MaxLightBonus = 5;

    public SolaisHandOnSheath() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(7);
        WithBlockPerLevel(1);
        WithVar("MaxBonus", MaxLightBonus);
        WithKeywords(CardKeyword.Retain);
        WithTip(typeof(SolaisLightPower));
    }

    protected override decimal BonusBlock() => Math.Min(Light, MaxLightBonus);

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }
}
