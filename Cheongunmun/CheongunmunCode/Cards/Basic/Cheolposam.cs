using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Cheongunmun.CheongunmunCode.Cards.Basic;

public sealed class Cheolposam : CheongunmunCard
{
    public Cheolposam() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithBlock(5, 3);
        WithTags(CardTag.Defend);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
    }
}
