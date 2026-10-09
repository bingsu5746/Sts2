using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>
/// Base class of every 간장·막야 card.
///  - <see cref="Side"/>: Ganjiang (attack side) or Moye (defense side). Twin-sword cards (쌍검, <see cref="IsTwin"/>)
///    use Sword = Ganjiang (FRAMEWORK.md §5: the pair is always owned together, so the reward filter is right).
///  - Twin cards: the first use also summons Moye, the current sword afterwards is Ganjiang, and the card gets
///    Moye's block effect on top of Ganjiang's damage effect (added by this class to its own block).
///  - 【짝】: resolved once per play before <see cref="OnPairCardPlay"/> (rules in <see cref="PairRules"/>), evaluated with the current sword captured
///    right before the card was played (the switch to this card's sword happens inside OnPlay).
///  - Level scaling: <see cref="LevelDamageBonus"/> per hit for own powered attacks (exact preview through the
///    damage hook), WithDamagePerLevel / WithBlockPerLevel for linear numbers.
/// </summary>
public abstract class GanjiangMoyeCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : MagicSwordCard(cost, type, rarity, target)
{
    private SwordId? _swordBeforePlay;

    /// <summary>Ganjiang or Moye. Twin cards: Ganjiang.</summary>
    protected abstract SwordId Side { get; }

    /// <summary>쌍검: counts as both swords.</summary>
    protected virtual bool IsTwin => false;

    /// <summary>Whether this card has a 【짝】 effect (gold glow when it would trigger).</summary>
    protected virtual bool HasPairEffect => true;

    public sealed override SwordId? Sword => IsTwin ? SwordId.Ganjiang : Side;

    /// <summary>Extra damage per hit of this card's own powered attacks at the given pair level.</summary>
    protected virtual int LevelDamageBonus(int level) => 0;

    /// <summary>Extra damage for this card's own powered attacks, other than level scaling. Must be pure.</summary>
    protected virtual int ExtraDamage(Creature? target) => 0;

    /// <summary>The current sword right before this card was played.</summary>
    protected SwordId? SwordBeforePlay => _swordBeforePlay;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this) && IsMutable && Owner != null)
            _swordBeforePlay = SwordCombat.CurrentSword(Owner);
        return base.BeforeCardPlayed(cardPlay);
    }

    private bool _pairActive;

    /// <summary>
    /// Runs the shared part of every Ganjiang/Moye card: twin cards summon Moye too, then 【짝】 is resolved once
    /// (only for cards with a pair effect, and always for twin cards), then the card's own effect runs.
    /// </summary>
    protected sealed override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await SummonTwinPartner(choiceContext);
        var pair = (HasPairEffect || IsTwin) && await PairRules.Resolve(Owner, Side, IsTwin, _swordBeforePlay);
        if (!HasPairEffect && !IsTwin) PairRules.ConsumePrimed(Owner);
        _pairActive = pair;
        try
        {
            await OnPairCardPlay(choiceContext, cardPlay, pair);
        }
        finally
        {
            _pairActive = false;
        }
    }

    /// <summary>The card's own effect. <paramref name="pair"/> = 【짝】 triggered for this play.</summary>
    protected abstract Task OnPairCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, bool pair);

    /// <summary>
    /// True while this play's 【짝】 is active, or (for the hand preview) when it would trigger if played now.
    /// Pure — safe to use from damage/block hooks.
    /// </summary>
    protected bool PairBonusApplies
    {
        get
        {
            if (_pairActive) return true;
            if (!HasPairEffect || !IsMutable || Owner?.PlayerCombatState == null) return false;
            if (Pile?.Type != PileType.Hand) return false;
            return PairRules.WouldTrigger(Owner, Side, IsTwin, SwordCombat.CurrentSword(Owner));
        }
    }

    /// <summary>Twin cards: summons Moye too on first use (Ganjiang is already summoned/current at this point).</summary>
    private async Task SummonTwinPartner(PlayerChoiceContext choiceContext)
    {
        if (!IsTwin || !IsMutable || Owner?.PlayerCombatState == null) return;
        if (!SwordCombat.IsPresent(Owner, SwordId.Moye))
            await SwordCombat.Summon(Owner, SwordId.Moye, choiceContext);
    }

    protected override bool ShouldGlowGoldInternal
    {
        get
        {
            if (!HasPairEffect || !IsMutable || Owner?.PlayerCombatState == null) return false;
            return PairRules.WouldTrigger(Owner, Side, IsTwin, SwordCombat.CurrentSword(Owner));
        }
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        var bonus = base.ModifyDamageAdditive(target, amount, props, dealer, cardSource);
        if (!ReferenceEquals(cardSource, this) || !props.IsPoweredAttack()) return bonus;
        return bonus + LevelDamageBonus(SwordLevel) + ExtraDamage(target);
    }

    /// <summary>Twin cards carry Moye's block effect on their own block (Ganjiang is the current sword).</summary>
    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        var bonus = base.ModifyBlockAdditive(target, block, props, cardSource, cardPlay);
        if (!IsTwin || !ReferenceEquals(cardSource, this) || !IsMutable || Owner == null) return bonus;
        if (!props.IsPoweredCardOrMonsterMoveBlock()) return bonus;
        // A twin card always switches to Ganjiang before its effect, so Moye's own current effect never applies to
        // it (in hand, the preview uses Ganjiang too: SwordCombat.EffectiveSwordFor) -> no double counting.
        return bonus + MoyeBehavior.BlockBonus(SwordLevel);
    }
}
