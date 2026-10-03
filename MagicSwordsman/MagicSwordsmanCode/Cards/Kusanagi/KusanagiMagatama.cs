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

/// <summary>곡옥 (Common): cost 0. Reduce one of your debuffs by 1. Draw 1 card (2 from level 3).</summary>
public sealed class KusanagiMagatama : KusanagiCard
{
    public KusanagiMagatama() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithCards(1);
    }

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await KusanagiPurify.ReduceOneDebuff(choiceContext, Owner.Creature, this);
        var draw = DynamicVars.Cards.IntValue + (SwordLevel >= 3 ? 1 : 0);
        await CardPileCmd.Draw(choiceContext, draw, Owner);
    }
}
