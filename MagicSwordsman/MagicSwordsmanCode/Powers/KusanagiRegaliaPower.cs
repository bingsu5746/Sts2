using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 삼종신기 (KusanagiThreeRegalia), content doc §2.3:
///  - when you switch FROM Kusanagi to another sword, the effect Kusanagi was inheriting stays (at the inherited,
///    half strength) until the end of that turn — two sword effects at once for one turn;
///  - whenever you switch INTO Kusanagi from another sword, draw Amount cards.
/// The lingering effect is run here exactly like CurrentSwordPower runs an inherited effect (same behavior hooks,
/// ctx.IsInherited = true). It is skipped while Kusanagi itself is current (it inherits normally then) and when the
/// lingering sword is the one in effect anyway (no double effect of the same sword).
/// </summary>
public sealed class KusanagiRegaliaPower : MagicSwordsmanPower, ISwordListener
{
    private SwordId? _inheritSource;
    private SwordId? _lingering;
    private int _lingerTurn = -1;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private Player? OwnerPlayer => IsMutable ? Owner?.Player : null;

    private int TurnNumber => OwnerPlayer?.PlayerCombatState?.TurnNumber ?? 0;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // The card that applied this power usually switched to Kusanagi just before: remember its inherit source.
        if (OwnerPlayer is { } p && SwordCombat.Get(p) is { Current: SwordId.Kusanagi } state)
            _inheritSource = state.Previous;
        return Task.CompletedTask;
    }

    public async Task AfterSwordSwitched(Player player, SwordId? from, SwordId to, PlayerChoiceContext choiceContext)
    {
        if (player != OwnerPlayer) return;

        if (from == SwordId.Kusanagi && _inheritSource is { } src && src != SwordId.Kusanagi &&
            SwordRegistry.Get(src).CanBeInherited)
        {
            _lingering = src;
            _lingerTurn = TurnNumber;
        }

        if (to == SwordId.Kusanagi)
        {
            _inheritSource = from;
            if (from != null)
            {
                Flash();
                await CardPileCmd.Draw(choiceContext, Amount, player);
            }
        }
    }

    /// <summary>The lingering inherited effect, if it applies to numbers from <paramref name="cardSource"/>.</summary>
    private IEnumerable<(SwordBehavior Behavior, SwordContext Ctx)> Lingering(CardModel? cardSource = null)
    {
        var player = OwnerPlayer;
        if (player == null || _lingering is not { } sword || _lingerTurn != TurnNumber) return [];
        var state = SwordCombat.Get(player);
        if (state == null) return [];
        var (effective, _) = SwordCombat.EffectiveSwordFor(player, cardSource);
        if (effective == SwordId.Kusanagi || effective == sword) return [];
        var baseCtx = SwordCombat.ContextFor(player, sword);
        return
        [
            (SwordRegistry.Get(sword), new SwordContext
            {
                Player = player, Sword = sword, Level = baseCtx.Level, IsInherited = true,
                IsCurrent = baseCtx.IsCurrent, IsPresent = baseCtx.IsPresent, Combat = state,
                PreviousSword = baseCtx.PreviousSword,
            })
        ];
    }

    // ------------------------------------------------------------------ forwarded hooks (mirror CurrentSwordPower)

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        decimal sum = 0m;
        foreach (var (b, ctx) in Lingering(cardSource)) sum += b.ModifyDamageAdditive(ctx, target, amount, props, dealer, cardSource);
        return sum;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        decimal mult = 1m;
        foreach (var (b, ctx) in Lingering(cardSource))
            mult *= b.ModifyDamageMultiplicative(ctx, target, amount, props, dealer, cardSource);
        return mult;
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        decimal sum = 0m;
        foreach (var (b, ctx) in Lingering(cardSource)) sum += b.ModifyBlockAdditive(ctx, target, block, props, cardSource, cardPlay);
        return sum;
    }

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        decimal mult = 1m;
        foreach (var (b, ctx) in Lingering(cardSource))
            mult *= b.ModifyBlockMultiplicative(ctx, target, block, props, cardSource, cardPlay);
        return mult;
    }

    public override int ModifyAttackHitCount(AttackCommand attack, int hitCount)
    {
        foreach (var (b, ctx) in Lingering()) hitCount = b.ModifyAttackHitCount(ctx, attack, hitCount);
        return hitCount;
    }

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card,
        bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
    {
        var result = (pileType, position);
        foreach (var (b, ctx) in Lingering(card))
            result = b.ModifyCardPlayResultPileTypeAndPosition(ctx, card, isAutoPlay, resources, result.pileType,
                result.position);
        return result;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        foreach (var (b, ctx) in Lingering().ToList()) await b.BeforeCardPlayed(ctx, cardPlay);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (var (b, ctx) in Lingering().ToList()) await b.AfterCardPlayed(ctx, choiceContext, cardPlay);
    }

    public override async Task BeforeAttack(AttackCommand command)
    {
        foreach (var (b, ctx) in Lingering().ToList()) await b.BeforeAttack(ctx, command);
    }

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        foreach (var (b, ctx) in Lingering().ToList()) await b.AfterAttack(ctx, choiceContext, command);
        // A lingering Tyrfing may have registered this attack at its start; unregistering is idempotent.
        TyrfingPierce.End(command);
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        foreach (var (b, ctx) in Lingering().ToList())
            await b.AfterDamageGiven(ctx, choiceContext, dealer, result, props, target, cardSource);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        foreach (var (b, ctx) in Lingering().ToList())
            await b.AfterDamageReceived(ctx, choiceContext, target, result, props, dealer, cardSource);
    }
}
