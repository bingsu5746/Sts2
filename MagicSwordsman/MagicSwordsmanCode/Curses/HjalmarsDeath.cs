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
/// 햐르마르의 죽음 — 2nd Tyrfing deed curse. While it is in your hand, Block you gain from cards is reduced by 2.
/// 「영웅 햐르마르 … 가 희생되며」.
/// </summary>
public sealed class HjalmarsDeath : TyrfingDeedCurse
{
    public const int BlockPenalty = 2;

    public HjalmarsDeath()
    {
        WithVar("BlockLoss", BlockPenalty);
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!IsMutable || Pile?.Type != PileType.Hand || cardSource == null) return 0m;
        if (target != Owner.Creature) return 0m;
        return -BlockPenalty; // CreatureCmd.GainBlock clamps the final amount at 0
    }
}
