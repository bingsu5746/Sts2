using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

// 칼라드볼그 powers (content doc §2.10, §3.1). Spreading rules: Swords/Behaviors/CaladbolgBehavior.cs (CaladbolgSpread).

/// <summary>
/// 레테의 검 (CaladbolgLetheBlade): this turn, your next Amount single-target attack(s) hit up to 3 enemies whatever
/// the current sword is, with no single-enemy penalty. Consumed by each single-target card attack; gone at the end
/// of your turn (ReboundPower pattern).
/// </summary>
public sealed class CaladbolgLethePower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeAttack(AttackCommand command)
    {
        var player = Owner.Player;
        if (player != null) CaladbolgSpread.TryExpand(command, player);
        return Task.CompletedTask;
    }

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (!CaladbolgSpread.IsEligible(command, Owner)) return;
        await PowerCmd.Decrement(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 공중에서 커지는 칼 (CaladbolgGrowingBlade): this combat, whenever one of your attacks hits 2 or more enemies, gain
/// Amount Block.
/// </summary>
public sealed class CaladbolgGrowingBladePower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command.Attacker != Owner || Owner.IsDead) return;
        if (CaladbolgSpread.DistinctTargetsHit(command) < 2) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}

/// <summary>
/// 울스터의 영웅 (CaladbolgUlsterHero): this combat, Caladbolg's current effect hits EVERY enemy instead of up to 3,
/// and its single-enemy damage reduction is halved. Read by CaladbolgBehavior / CaladbolgSpread.
/// </summary>
public sealed class CaladbolgUlsterHeroPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>
/// 치지 못한 왕의 방패 (curse UnstruckKingsShield) — on an ENEMY: at the start of its next turn (after its block was
/// cleared, Creature.AfterTurnStart -> ClearBlock) it gains Amount Block, then this power goes away.
/// Why not "gain Block at the end of your turn": enemy block is cleared at the start of the enemy's own turn, so
/// Block given at the end of the player's turn would vanish before it could matter.
/// </summary>
public sealed class UnstruckKingsShieldPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner)) return;
        if (Owner.IsAlive)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
        }

        await PowerCmd.Remove(this);
    }
}
