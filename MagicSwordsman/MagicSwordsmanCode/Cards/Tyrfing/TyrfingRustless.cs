using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tyrfing;

/// <summary>녹슬지 않는 날 (Common): 7 Block (+1/L). If you have a curse in hand/draw/discard pile, draw 1 card.</summary>
public sealed class TyrfingRustless : TyrfingCard
{
    public TyrfingRustless() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(7);
        WithBlockPerLevel(1);
        WithCards(1);
    }

    protected override bool ShouldGlowGoldInternal => CursesInPiles() > 0;

    protected override async Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (CursesInPiles() > 0) await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
