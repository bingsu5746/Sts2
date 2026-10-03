using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Dainsleif;

/// <summary>
/// 회그니 왕 (Uncommon skill): draw 2 cards (3 from level 3). If Dainsleif returned 2 or more cards to the draw pile
/// this turn, draw 1 more.
/// </summary>
public sealed class DainsleifHogni : DainsleifCard
{
    public const int ReturnedThreshold = 2;

    public DainsleifHogni() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithCards(2);
    }

    protected override bool ShouldGlowGoldInternal =>
        IsMutable && Owner != null && DainsleifReturn.ReturnedThisTurn(Owner) >= ReturnedThreshold;

    protected override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var draw = DynamicVars.Cards.IntValue + (SwordLevel >= 3 ? 1 : 0);
        if (DainsleifReturn.ReturnedThisTurn(Owner) >= ReturnedThreshold) draw++;
        await CardPileCmd.Draw(choiceContext, draw, Owner);
    }
}
