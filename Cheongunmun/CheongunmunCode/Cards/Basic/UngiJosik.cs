using BaseLib.Extensions;
using Cheongunmun.CheongunmunCode.Resources;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Cheongunmun.CheongunmunCode.Cards.Basic;

public sealed class UngiJosik : CheongunmunCard
{
    private const string NaegongVar = "Naegong";

    public UngiJosik() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithVar(NaegongVar, 1, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Owner.PlayerCombatState?.GetResource<Naegong>().Gain(DynamicVars[NaegongVar].IntValue);
        return Task.CompletedTask;
    }
}
