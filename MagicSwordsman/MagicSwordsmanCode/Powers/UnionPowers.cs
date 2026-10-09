using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 성유물의 가호 (【조합】 성유물의 빛, Claíomh Solais + Durandal): until your next turn, enemy attack hits against you
/// deal Amount less (Durandal's full reduction at its level when the card was played). Does nothing while Durandal's
/// own reduction is active (current, or inherited by Kusanagi), so the two never stack. Reduction pattern:
/// DurandalBehavior / DurandalHiddenBeneathPower; removal: FlameBarrierPower (after the opposing side's turn ends).
/// Icon: falls back to the generic power icon until art exists.
/// </summary>
public sealed class UnionRelicGracePower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (!DurandalBehavior.IsEnemyAttackOn(Owner, target, props, dealer)) return 0m;
        if (Owner.Player is not { } player) return 0m;
        if (DurandalBehavior.IsSwordEffectActive(player, SwordId.Durandal, out _)) return 0m;
        return -Math.Min(Amount, Math.Max(0m, amount));
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side != side) await PowerCmd.Remove(this);
    }
}
