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

/// <summary>들불 헤치기 (Common): 4 Block (+1/L). Exhaust all status/curse cards in hand, draw 1 per card.</summary>
public sealed class KusanagiWildfire : KusanagiCard
{
    public KusanagiWildfire() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(4);
        WithBlockPerLevel(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        var exhausted = await KusanagiPurify.ExhaustAllFromHand(choiceContext, Owner);
        if (exhausted > 0) await CardPileCmd.Draw(choiceContext, exhausted, Owner);
    }
}
