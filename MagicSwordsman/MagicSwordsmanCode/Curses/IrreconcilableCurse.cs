using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 화해할 수 없음 — Dainsleif's forge-failure curse (content doc §3.1). Unplayable. At the end of your turn, if it is in
/// your hand, it is not discarded: it is shuffled into a random position of your draw pile (keeps coming back).
/// The game collects turn-end cards into a separate list before calling OnTurnEndInHand (CombatManager.DoTurnEnd),
/// so moving the card there is safe; the hand flush happens afterwards.
/// Source line: 「화해가 불가능해져 전투가 영원히 되풀이된다」.
/// </summary>
public sealed class IrreconcilableCurse : SwordCurseCard
{
    public override SwordId Sword => SwordId.Dainsleif;

    public override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await CardPileCmd.Add(this, PileType.Draw, CardPilePosition.Random);
    }
}
