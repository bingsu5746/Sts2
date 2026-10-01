using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Cheongunmun.CheongunmunCode.Cards.Basic;

public sealed class YukhapGeombeop : CheongunmunCard
{
    public YukhapGeombeop() : base(0, CardType.Attack, CardRarity.Basic, TargetType.AllEnemies)
    {
        WithDamage(6, 3);
        WithNaegongCost(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}
