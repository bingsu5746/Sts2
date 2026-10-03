using BaseLib.Utils;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 스바프를라미의 최후 — 1st Tyrfing deed curse. At the end of your turn, if it is in your hand, lose 2 HP (ignores
/// block). 「주인 스바프를라미도 죽인다. 실제로 스바프를라미는 이 검으로 죽고」.
/// </summary>
public sealed class SvafrlamisEnd : TyrfingDeedCurse
{
    public const int HpLoss = 2;

    public override bool HasTurnEndInHandEffect => true;

    public SvafrlamisEnd()
    {
        WithVar("HpLoss", HpLoss);
    }

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.Damage(choiceContext, Owner.Creature, HpLoss, DamageProps.cardHpLoss, this);
    }
}
