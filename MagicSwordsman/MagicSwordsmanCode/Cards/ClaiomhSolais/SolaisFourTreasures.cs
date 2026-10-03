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
/// 네 가지 보물 (Uncommon power): when you end your turn with Claíomh Solais current, charge 1 more Light
/// (the [확정] base charge of 1 still happens). From level 3 also gain 3 Block then. 「네 가지 보물 중 하나」.
/// </summary>
public sealed class SolaisFourTreasures : SolaisCard
{
    public SolaisFourTreasures() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<SolaisFourTreasuresPower>(1);
        WithVar("Block3", SolaisFourTreasuresPower.BlockPerStack);
        WithTip(typeof(SolaisLightPower));
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SolaisFourTreasuresPower>(choiceContext, Owner.Creature,
            DynamicVars["SolaisFourTreasuresPower"].BaseValue, Owner.Creature, this);
    }
}
