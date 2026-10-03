using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Curses;

/// <summary>
/// 오딘이 꺾은 칼날 — Gram's forge-failure curse (content doc §3.1). Unplayable.
/// While in your hand, your attacks deal 2 less damage (per hit; same powered-attack filter as StrengthPower).
/// Lore (research doc): Odin himself broke Gram.
/// </summary>
public sealed class OdinsBrokenBlade : SwordCurseCard
{
    public override SwordId Sword => SwordId.Gram;

    public OdinsBrokenBlade()
    {
        WithVar("Penalty", 2);
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (!IsMutable || Owner == null || dealer == null || dealer != Owner.Creature) return 0m;
        if (Pile?.Type != PileType.Hand) return 0m;
        if (!props.IsPoweredAttack()) return 0m;
        return -DynamicVars["Penalty"].IntValue;
    }
}
