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
/// Base of Tyrfing's three "악행" curses ([확정] spec §7: the first 3 uses of Tyrfing cards in a run each add a
/// permanent curse; content doc §3.2 / 0.4 #9). Eternal (cannot be removed from the deck: CardModel.IsRemovable)
/// and Unplayable. When one is exhausted in combat (e.g. by Kusanagi's purification), Tyrfing card attacks deal +3
/// damage for the rest of that combat (<see cref="TyrfingVictoryPower"/>) — 「든 자에게 언제나 승리를 준다」.
/// </summary>
public abstract class TyrfingDeedCurse : SwordCurseCard
{
    public const int ExhaustBonus = 3;

    public override SwordId Sword => SwordId.Tyrfing;

    protected TyrfingDeedCurse()
    {
        WithKeywords(CardKeyword.Eternal);
        WithVar("Bonus", ExhaustBonus);
        WithTip(new TooltipSource(_ => HoverTipFactory.FromPower<TyrfingVictoryPower>()));
    }

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card != this) return;
        await PowerCmd.Apply<TyrfingVictoryPower>(choiceContext, Owner.Creature, ExhaustBonus, Owner.Creature, this);
    }
}
