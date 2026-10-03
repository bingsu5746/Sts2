using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;

/// <summary>구름 모으기 (Basic): 6 Block (+1/L). Reduce one of your debuffs by 1.</summary>
public sealed class KusanagiGatheringClouds : KusanagiCard
{
    public KusanagiGatheringClouds() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithBlock(6);
        WithBlockPerLevel(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await KusanagiPurify.ReduceOneDebuff(choiceContext, Owner.Creature, this);
    }
}
