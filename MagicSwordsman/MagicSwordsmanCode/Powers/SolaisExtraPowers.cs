using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using MagicSwordsman.MagicSwordsmanCode.Cards.ClaiomhSolais;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 빛의 검 (SolaisSwordOfLight): when you end your turn with Claíomh Solais' effect active (current, or inherited by
/// Kusanagi), deal 2 (3 from Solais level 3) × Amount damage per Light to ALL enemies; Light is not consumed.
/// Runs at BeforeSideTurnEndEarly (game PlatingPower timing), i.e. before the behavior's end-of-turn charge.
/// </summary>
public sealed class SolaisSwordOfLightPower : MagicSwordsmanPower
{
    public const int DamagePerLight = 2;
    public const int DamagePerLightHigh = 3;
    public const int HighFromLevel = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SolaisLightPower>()];

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.IsDead || Owner.Player is not { } player) return;
        if (!SolaisLight.TryGetActiveEffect(player, out _)) return;
        var light = SolaisLight.Get(player);
        if (light <= 0 || Owner.CombatState is not { } combatState) return;

        var perLight = SwordCombat.LevelOf(player, SwordId.ClaiomhSolais) >= HighFromLevel
            ? DamagePerLightHigh
            : DamagePerLight;
        var enemies = combatState.HittableEnemies.ToList();
        if (enemies.Count == 0) return;
        Flash();
        await CreatureCmd.Damage(choiceContext, enemies, light * perLight * Amount, ValueProp.Unpowered, Owner, null);
    }
}

/// <summary>
/// 눈부심 (SolaisDazzle, card SolaisIrresistible): until your next turn, whenever an enemy that is not Vulnerable
/// attacks you (hit, blocked or not), spend 1 Light automatically and apply Amount Vulnerable to it. No Light -> no
/// effect. One dose per attacker while it stays Vulnerable. Trigger: game FlameBarrierPower pattern
/// (AfterDamageReceived); removal like it (after the opposing side's turn ends).
/// </summary>
public sealed class SolaisDazzlePower : MagicSwordsmanPower
{
    public const int VulnerablePerTrigger = 2;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<SolaisLightPower>(), HoverTipFactory.FromPower<VulnerablePower>()];

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!DurandalBehavior.IsEnemyAttackOn(Owner, target, props, dealer) || !Owner.IsAlive) return;
        if (dealer is not { IsAlive: true } attacker || attacker.HasPower<VulnerablePower>()) return;
        if (Owner.Player is not { } player) return;
        if (!await SolaisLightSpend.TrySpend(choiceContext, player, 1, null)) return;
        Flash();
        await PowerCmd.Apply<VulnerablePower>(choiceContext, attacker, Amount, Owner, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side != side) await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 스렝의 일격 (SolaisSreng, card SolaisSrengsBlow): until your next turn, whenever an enemy attack deals unblocked
/// damage to you, gain 1 Light. Amount = triggers left (each trigger uses 1). Removal like FlameBarrierPower.
/// </summary>
public sealed class SolaisSrengPower : MagicSwordsmanPower
{
    public const int MaxTriggers = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SolaisLightPower>()];

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!DurandalBehavior.IsEnemyAttackOn(Owner, target, props, dealer) || !Owner.IsAlive) return;
        if (result.UnblockedDamage <= 0 || Owner.Player is not { } player) return;
        Flash();
        await SolaisLight.Gain(choiceContext, player, 1, null);
        if (Amount <= 1) await PowerCmd.Remove(this);
        else await PowerCmd.ModifyAmount(choiceContext, this, -1, null, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side != side) await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 왕위 복귀 (SolaisKingship, card SolaisKingshipRestored): while the owner has at least <see cref="LightNeeded"/>
/// Light, the owner's powered attacks deal Amount more damage per hit. Nothing is spent. Pure (preview-safe).
/// </summary>
public sealed class SolaisKingshipPower : MagicSwordsmanPower
{
    public const int LightNeeded = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SolaisLightPower>()];

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (dealer != Owner || !props.IsPoweredAttack() || Owner.Player is not { } player) return 0m;
        return SolaisLight.Get(player) >= LightNeeded ? Amount : 0m;
    }
}

/// <summary>
/// 핀디아스의 새벽 (SolaisDawn, card SolaisFromFindias): at the start of your next turn, gain Amount Light, then this
/// power is removed. Timing: game EnergyNextTurnPower / SolaisMagTuiredPower pattern (AfterEnergyReset), i.e. after
/// the previous turn's end-of-turn Light rule already ran.
/// </summary>
public sealed class SolaisDawnPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SolaisLightPower>()];

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player || Owner.IsDead) return;
        Flash();
        await SolaisLight.Gain(new ThrowingPlayerChoiceContext(), player, Amount, null);
        await PowerCmd.Remove(this);
    }
}

/// <summary>
/// 빛을 머금은 칼집 (SolaisLitSheath, card SolaisLitSheath): whenever the owner gains Block from a card (cardSource
/// set, amount &gt; 0), gain 1 Light; at most Amount times per turn. Unpowered Block from powers (네 개의 보물, 은팔...)
/// has no card source and does not count, so no power chain can loop. Hook: game AfterBlockGained
/// (JuggernautPower pattern, ThrowingPlayerChoiceContext). Per-turn counter: internal data, cleared at the start of
/// the owner's turn (OutbreakPower pattern).
/// </summary>
public sealed class SolaisLitSheathPower : MagicSwordsmanPower
{
    public const int CapPerCopy = 2;

    private sealed class Data
    {
        public int TriggersThisTurn;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SolaisLightPower>()];

    protected override object InitInternalData() => new Data();

    public override async Task AfterBlockGained(Creature creature, decimal amount, ValueProp props,
        CardModel? cardSource)
    {
        if (creature != Owner || amount <= 0 || cardSource == null || Owner.IsDead) return;
        if (Owner.Player is not { } player || cardSource.Owner != player) return;
        var data = GetInternalData<Data>();
        if (data.TriggersThisTurn >= Amount) return;
        data.TriggersThisTurn++;
        Flash();
        await SolaisLight.Gain(new ThrowingPlayerChoiceContext(), player, 1, null);
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side) GetInternalData<Data>().TriggersThisTurn = 0;
        return Task.CompletedTask;
    }
}
