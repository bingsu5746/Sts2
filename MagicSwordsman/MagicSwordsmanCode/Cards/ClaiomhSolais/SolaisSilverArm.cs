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
/// 은팔의 누아다 (Rare power): when Light is lost as Claíomh Solais' cost, gain 4 Block per Light (5 from level 3).
/// Whenever you 【발도】, draw 2 cards. Source: 「투아하 데 다난의 첫 왕 은팔의 누아다」.
/// </summary>
public sealed class SolaisSilverArm : SolaisCard
{
    public SolaisSilverArm() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<SolaisSilverArmPower>(1);
        WithVar("Cards2", SolaisSilverArmPower.DrawPerDrawCut);
        WithTip(typeof(SolaisLightPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SolaisSilverArmPower>(choiceContext, Owner.Creature,
            DynamicVars["SolaisSilverArmPower"].BaseValue, Owner.Creature, this);
    }
}
