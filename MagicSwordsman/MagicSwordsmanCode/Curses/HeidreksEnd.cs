using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 하이드레크의 최후 — Tyrfing's forge-failure curse (content doc §3.1). Unplayable. When you draw this card, lose 2 HP
/// (Void pattern: AfterCardDrawn with card == this; HP loss like Hemokinesis, unblockable).
/// Source line: 「하이드레크 자신도 노예들에게 살해된다」.
/// </summary>
public sealed class HeidreksEnd : SwordCurseCard
{
    public const int HpLoss = 5; // 사용자 결정 2026-10-04: blockable 5 damage

    public override SwordId Sword => SwordId.Tyrfing;

    public HeidreksEnd()
    {
        WithVar("HpLoss", HpLoss);
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != this) return;
        await CreatureCmd.Damage(choiceContext, Owner.Creature, HpLoss, ValueProp.Unpowered | ValueProp.Move, this);
    }
}
