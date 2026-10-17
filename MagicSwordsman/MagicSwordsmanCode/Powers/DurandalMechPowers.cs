using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

// 뒤랑달 메커니즘 파워 (2026-10-17): the flat per-hit reduction becomes something cards can raise, reflect and use.

/// <summary>
/// 단단한 날 (DurandalHardEdge): until your next turn, Durandal's reduction is Amount higher while its effect is active
/// (read by <see cref="DurandalBehavior.BonusReduction"/>). Each enemy attack hit that this brings to 0 damage (before
/// Block) gives 1 extra card next turn (<see cref="DrawCardsNextTurnPower"/>), at most <see cref="MaxDraws"/> per turn.
/// Removal: as FlameBarrierPower / DurandalUnscathedPower (after the opposing side's turn).
/// </summary>
public sealed class DurandalHardEdgePower : MagicSwordsmanPower
{
    public const int MaxDraws = 2;

    private sealed class Data
    {
        public int Draws;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!DurandalBehavior.IsEnemyAttackOn(Owner, target, props, dealer) || !Owner.IsAlive) return;
        if (Owner.Player is not { } player || !DurandalBehavior.IsSwordEffectActive(player, SwordId.Durandal, out _)) return;
        if (result.TotalDamage > 0) return;
        var data = GetInternalData<Data>();
        if (data.Draws >= MaxDraws) return;
        data.Draws++;
        Flash();
        await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, Owner, 1, Owner, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side != side) await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 튕겨 나는 강철 (DurandalSpringingSteel): this combat, whenever an enemy attack hits you while Durandal's effect is
/// active, deal damage equal to Durandal's current reduction (<see cref="DurandalBehavior.ActiveReduction"/>) to the
/// attacker. Trigger point and props: the game's ThornsPower (BeforeDamageReceived, Unpowered | SkipHurtAnim).
/// Single: a second copy adds nothing.
/// </summary>
public sealed class DurandalSpringingSteelPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!DurandalBehavior.IsEnemyAttackOn(Owner, target, props, dealer) || dealer is not { IsAlive: true }) return;
        if (Owner.Player is not { } player) return;
        var damage = DurandalBehavior.ActiveReduction(player);
        if (damage <= 0) return;
        Flash();
        await CreatureCmd.Damage(choiceContext, dealer, damage, ValueProp.Unpowered | ValueProp.SkipHurtAnim, Owner, null);
    }
}

/// <summary>
/// 롤랑의 최후 (DurandalRolandsEnd): this combat you can switch to Durandal even at 30% HP or less (checked in
/// <see cref="DurandalBehavior.CanBecomeCurrent"/>), and while your HP is at or below 30% Durandal's reduction is
/// <see cref="LowHpBonus"/> higher (<see cref="DurandalBehavior.BonusReduction"/>). Single.
/// </summary>
public sealed class DurandalRolandsEndPower : MagicSwordsmanPower
{
    public const int LowHpBonus = 2;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}
