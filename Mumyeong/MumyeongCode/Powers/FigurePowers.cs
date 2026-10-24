using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Mumyeong.MumyeongCode.Powers;

// All powers of 무명 (spec docs/mumyeong-spec.md §4.3, §6). Hook patterns are copied from 엔시페르 powers that build:
// turn start = AfterPlayerTurnStart, end of turn = BeforeSideTurnEndEarly (game PlatingPower timing), card played =
// AfterCardPlayed, enemy attack on the owner = BeforeDamageReceived (game ThornsPower), max energy = ModifyMaxEnergy.

internal static class PowerHelpers
{
    public static bool IsMine(PowerModel power, CardPlay cardPlay) =>
        cardPlay.Card.Owner?.Creature == power.Owner && power.Owner.IsAlive;

    public static bool IsEnemyAttackOn(Creature owner, Creature? target, ValueProp props, Creature? dealer) =>
        target == owner && dealer != null && dealer != owner && dealer.Side != owner.Side && props.IsPoweredAttack();

    public static async Task DamageAllEnemies(PlayerChoiceContext choiceContext, Creature owner, decimal amount)
    {
        if (amount <= 0 || owner.CombatState is not { } combat) return;
        var enemies = combat.HittableEnemies.ToList();
        if (enemies.Count == 0) return;
        await CreatureCmd.Damage(choiceContext, enemies, amount, ValueProp.Unpowered, owner, null);
    }

    public static async Task DamageRandomEnemy(PlayerChoiceContext choiceContext, Creature owner, decimal amount)
    {
        if (amount <= 0 || owner.CombatState is not { } combat || owner.Player is not { } player) return;
        var enemies = combat.HittableEnemies;
        if (enemies.Count == 0) return;
        var target = player.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target == null) return;
        await CreatureCmd.Damage(choiceContext, target, amount, ValueProp.Unpowered, owner, null);
    }
}

// ============================================================================ shared

/// <summary>객석의 갈채: at the start of your turn, gain Amount Block.</summary>
public sealed class ApplausePower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || !Owner.IsAlive) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}

/// <summary>한 (한 계약, spec §4.3): grows when you play a card that is not your figure's. Display only; the 코어 acts.</summary>
public sealed class GrudgePower : MumyeongPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

// ============================================================================ 관우

/// <summary>위진화하: at the start of your turn, gain Amount Vigor.</summary>
public sealed class GuanyuAwePower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<VigorPower>()];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || !Owner.IsAlive) return;
        Flash();
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner, Amount, Owner, null);
    }
}

/// <summary>무신: whenever you play an Attack, gain Amount Vigor.</summary>
public sealed class GuanyuWarGodPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<VigorPower>()];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!PowerHelpers.IsMine(this, cardPlay) || cardPlay.Card.Type != CardType.Attack) return;
        Flash();
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner, Amount, Owner, null);
    }
}

/// <summary>의절천추: whenever you play a Skill, gain Amount Vigor.</summary>
public sealed class GuanyuRighteousPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<VigorPower>()];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!PowerHelpers.IsMine(this, cardPlay) || cardPlay.Card.Type != CardType.Skill) return;
        Flash();
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner, Amount, Owner, null);
    }
}

// ============================================================================ 잔 다르크

/// <summary>신념: at the end of your turn, gain Block equal to Faith. Faith does not decay.</summary>
public sealed class JoanFaithPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || !Owner.IsAlive || Amount <= 0) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}

/// <summary>흔들리지 않는 깃발: at the start of your turn, gain Amount Faith.</summary>
public sealed class JoanBannerPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<JoanFaithPower>()];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || !Owner.IsAlive) return;
        Flash();
        await PowerCmd.Apply<JoanFaithPower>(choiceContext, Owner, Amount, Owner, null);
    }
}

/// <summary>화형대: whenever you lose HP, gain Amount Faith.</summary>
public sealed class JoanPyrePower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<JoanFaithPower>()];

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || !Owner.IsAlive || result.UnblockedDamage <= 0) return;
        Flash();
        await PowerCmd.Apply<JoanFaithPower>(choiceContext, Owner, Amount, Owner, null);
    }
}

/// <summary>심판: at the end of your turn, deal damage equal to your Faith to ALL enemies (× Amount).</summary>
public sealed class JoanJudgmentPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<JoanFaithPower>()];

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || !Owner.IsAlive) return;
        var faith = Owner.GetPowerAmount<JoanFaithPower>();
        if (faith <= 0) return;
        Flash();
        await PowerHelpers.DamageAllEnemies(choiceContext, Owner, faith * Amount);
    }
}

// ============================================================================ 레오니다스

/// <summary>방진 (팔랑크스): Block is not removed at the start of your turn.</summary>
public sealed class LeonidasPhalanxPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override bool ShouldClearBlock(Creature creature) => creature != Owner;
}

/// <summary>반격: whenever an enemy attacks you, deal Amount damage back (game ThornsPower timing).</summary>
public sealed class LeonidasCounterPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!PowerHelpers.IsEnemyAttackOn(Owner, target, props, dealer) || dealer is not { IsAlive: true }) return;
        Flash();
        await CreatureCmd.Damage(choiceContext, dealer, Amount, ValueProp.Unpowered | ValueProp.SkipHurtAnim, Owner, null);
    }
}

/// <summary>와서 가져가라: at the end of your turn, deal damage equal to half your Block to ALL enemies (× Amount).</summary>
public sealed class LeonidasMolonLabePower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || !Owner.IsAlive) return;
        var damage = Owner.Block / 2 * Amount;
        if (damage <= 0) return;
        Flash();
        await PowerHelpers.DamageAllEnemies(choiceContext, Owner, damage);
    }
}

/// <summary>테르모필레: whenever you play a Skill, deal Amount damage to a random enemy.</summary>
public sealed class LeonidasThermopylaePower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!PowerHelpers.IsMine(this, cardPlay) || cardPlay.Card.Type != CardType.Skill) return;
        Flash();
        await PowerHelpers.DamageRandomEnemy(choiceContext, Owner, Amount);
    }
}

// ============================================================================ 칭기즈 칸

/// <summary>기마 군단: at the start of your turn, deal Amount damage to ALL enemies, then Amount +1.</summary>
public sealed class GenghisHordePower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || !Owner.IsAlive) return;
        Flash();
        await PowerHelpers.DamageAllEnemies(choiceContext, Owner, Amount);
        await PowerCmd.ModifyAmount(choiceContext, this, 1m, null, null);
    }
}

/// <summary>정복: whenever an enemy dies, gain Amount energy and draw Amount card(s).</summary>
public sealed class GenghisConquestPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || !creature.IsEnemy || !Owner.IsAlive || Owner.Player is not { } player) return;
        Flash();
        await PlayerCmd.GainEnergy(Amount, player);
        await CardPileCmd.Draw(choiceContext, Amount, player);
    }
}

/// <summary>대몽골: whenever you play an Attack, deal Amount damage to ALL enemies.</summary>
public sealed class GenghisEmpirePower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!PowerHelpers.IsMine(this, cardPlay) || cardPlay.Card.Type != CardType.Attack) return;
        Flash();
        await PowerHelpers.DamageAllEnemies(choiceContext, Owner, Amount);
    }
}

// ============================================================================ 이순신

/// <summary>포격: at the end of your turn, deal Amount damage to ALL enemies. Does not decay.</summary>
public sealed class YiCannonPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || !Owner.IsAlive || Amount <= 0) return;
        Flash();
        await PowerHelpers.DamageAllEnemies(choiceContext, Owner, Amount);
    }
}

/// <summary>거북선: at the end of your turn, gain Amount Block.</summary>
public sealed class YiTurtleShipPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || !Owner.IsAlive) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}

/// <summary>학익진: at the end of your turn, deal Amount damage to ALL enemies for each unspent energy.</summary>
public sealed class YiCraneWingPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || !Owner.IsAlive) return;
        var energy = Owner.Player?.PlayerCombatState?.Energy ?? 0;
        if (energy <= 0) return;
        Flash();
        await PowerHelpers.DamageAllEnemies(choiceContext, Owner, energy * Amount);
    }
}

/// <summary>통제사: at the start of your turn, gain Amount 포격.</summary>
public sealed class YiCommanderPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<YiCannonPower>()];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || !Owner.IsAlive) return;
        Flash();
        await PowerCmd.Apply<YiCannonPower>(choiceContext, Owner, Amount, Owner, null);
    }
}

// ============================================================================ 나폴레옹

/// <summary>포병대: at the start of your turn, deal Amount damage to a random enemy.</summary>
public sealed class NapoleonArtilleryPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || !Owner.IsAlive) return;
        Flash();
        await PowerHelpers.DamageRandomEnemy(choiceContext, Owner, Amount);
    }
}

/// <summary>전격전: every 4th card you play in a turn gives Amount energy (per-turn counter: game OutbreakPower pattern).</summary>
public sealed class NapoleonBlitzPower : MumyeongPower
{
    public const int CardsPerTrigger = 4;

    private sealed class Data
    {
        public int Turn;
        public int Cards;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!PowerHelpers.IsMine(this, cardPlay) || Owner.Player is not { } player) return;
        var data = GetInternalData<Data>();
        var turn = player.PlayerCombatState?.TurnNumber ?? 0;
        if (data.Turn != turn)
        {
            data.Turn = turn;
            data.Cards = 0;
        }
        data.Cards++;
        if (data.Cards % CardsPerTrigger != 0) return;
        Flash();
        await PlayerCmd.GainEnergy(Amount, player);
    }
}

/// <summary>대관식: +Amount max energy (game relic pattern: ModifyMaxEnergy).</summary>
public sealed class NapoleonCoronationPower : MumyeongPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        player.Creature != Owner ? amount : amount + Amount;
}
