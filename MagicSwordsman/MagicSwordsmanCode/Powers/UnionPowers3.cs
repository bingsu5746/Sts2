using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

// Powers for the 【조합】 cards reworked / added on 2026-10-15 (user request #39).

/// <summary>
/// 시드의 빛 (【조합】 시드의 빛, Caladbolg + Claíomh Solais):
///  - whenever one of the owner's attacks hits 2+ distinct enemies, gain 1 Light (max <see cref="MaxLightPerTurn"/>
///    per turn; multi-target detection: CaladbolgSpread.DistinctTargetsHit, as 공중에서 커지는 칼);
///  - at BeforeSideTurnEndEarly (before ClaiomhSolaisBehavior's end-of-turn rule; SolaisSwordOfLightPower timing),
///    if Solais' effect is NOT active and Light &gt; 0 (the Light is about to fade as Solais' cost), deal
///    Amount × Light damage to ALL enemies. The fade itself still happens (the cost is never cancelled).
/// Amount = burst damage per Light (stacks add).
/// </summary>
public sealed class UnionSidheLightPower : MagicSwordsmanPower
{
    public const int MaxLightPerTurn = 2;
    public const int BaseBurst = 3;

    private int _turn = -1;
    private int _gainedThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<SolaisLightPower>()];

    private int TurnNumber => Owner.Player?.PlayerCombatState?.TurnNumber ?? 0;

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command.Attacker != Owner || Owner.IsDead || Owner.Player is not { } player) return;
        if (CaladbolgSpread.DistinctTargetsHit(command) < 2) return;
        if (_turn != TurnNumber)
        {
            _turn = TurnNumber;
            _gainedThisTurn = 0;
        }

        if (_gainedThisTurn >= MaxLightPerTurn) return;
        _gainedThisTurn++;
        Flash();
        await SolaisLight.Gain(choiceContext, player, 1, null);
    }

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.IsDead || Owner.Player is not { } player) return;
        if (SolaisLight.TryGetActiveEffect(player, out _)) return; // the Light will be charged, not lost
        var light = SolaisLight.Get(player);
        if (light <= 0 || Owner.CombatState is not { } combatState) return;
        var enemies = combatState.HittableEnemies.ToList();
        if (enemies.Count == 0) return;
        Flash();
        await CreatureCmd.Damage(choiceContext, enemies, light * Amount, ValueProp.Unpowered, Owner, null);
    }
}

/// <summary>
/// 난쟁이의 벼림 (【조합】 난쟁이의 솜씨, Tyrfing + Gram): card instances re-forged by that card deal more damage per hit
/// for the rest of the combat. Per-instance bonuses live in internal data (DainsleifDwarfForgedPower pattern);
/// Amount only counts how many times a card was forged.
/// </summary>
public sealed class UnionDwarvenEdgePower : MagicSwordsmanPower
{
    private sealed class Data
    {
        public readonly Dictionary<CardModel, int> Bonus = new();
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    /// <summary>Gives <paramref name="card"/> +<paramref name="bonus"/> damage per hit this combat.</summary>
    public static async Task Forge(PlayerChoiceContext choiceContext, Creature owner, CardModel card, int bonus,
        CardModel? source)
    {
        if (bonus <= 0) return;
        await PowerCmd.Apply<UnionDwarvenEdgePower>(choiceContext, owner, 1, owner, source);
        var power = owner.GetPower<UnionDwarvenEdgePower>();
        if (power == null) return;
        var data = power.GetInternalData<Data>().Bonus;
        data[card] = data.GetValueOrDefault(card) + bonus;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        if (cardSource == null || dealer != Owner || !props.IsPoweredAttack()) return 0m;
        return GetInternalData<Data>().Bonus.GetValueOrDefault(cardSource);
    }
}
