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

/// <summary>누아다의 횃불 (Basic): 5 Block (+1/L). Gain 1 Light. Source: 「'누아다의 횃불'로도 불린다」.</summary>
public sealed class SolaisNuadasTorch : SolaisCard
{
    public SolaisNuadasTorch() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithBlock(5);
        WithBlockPerLevel(1);
        WithPower<SolaisLightPower>(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await SolaisLight.Gain(choiceContext, Owner, DynamicVars["SolaisLightPower"].IntValue, this);
    }
}
