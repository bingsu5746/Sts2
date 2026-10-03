using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 용광로의 제물 — Ganjiang/Moye forge-failure curse (content doc §3.1). Unplayable.
/// At the end of your turn, if this is in your hand, lose 2 HP (ignores Block).
/// Turn-end-in-hand pattern: game curse Decay (HasTurnEndInHandEffect / OnTurnEndInHand); HP loss props as BloodWall.
/// Lore (research doc): "사람의 기(氣)가 부족하다" — a part of a person was thrown into the furnace.
/// </summary>
public sealed class FurnaceOffering : SwordCurseCard
{
    public override SwordId Sword => SwordId.Ganjiang;

    public FurnaceOffering()
    {
        WithVar("HpLoss", 2);
    }

    public override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars["HpLoss"].IntValue,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this);
    }
}
