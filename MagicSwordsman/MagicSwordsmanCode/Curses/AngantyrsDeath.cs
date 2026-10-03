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
/// 앙간튀르의 죽음 — 3rd Tyrfing deed curse. When you draw this card, discard another random card from your hand
/// (game Rng.CombatCardSelection, like TrueGrit's random pick). 「하이드레크의 형제 앙간튀르가 희생되며」.
/// </summary>
public sealed class AngantyrsDeath : TyrfingDeedCurse
{
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != this) return;
        var others = PileType.Hand.GetPile(Owner).Cards.Where(c => c != this).ToList();
        if (others.Count == 0) return;
        var victim = Owner.RunState.Rng.CombatCardSelection.NextItem(others);
        if (victim != null) await CardCmd.Discard(choiceContext, victim);
    }
}
