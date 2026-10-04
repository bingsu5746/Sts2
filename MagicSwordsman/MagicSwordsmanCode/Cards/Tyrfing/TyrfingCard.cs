using MagicSwordsman.MagicSwordsmanCode.Curses;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.Tyrfing;

/// <summary>
/// Base of every 티르빙 card. Spec §7 [확정] cost "세 번의 악행": once Tyrfing is at level <see cref="TyrfingCurses.CurseStartLevel"/>+, the first 3 times Tyrfing cards are used in the
/// whole run, each use adds one permanent curse to the deck (after the card's effect). Implement
/// <see cref="OnTyrfingPlay"/> instead of OnCardPlay.
/// </summary>
public abstract class TyrfingCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : MagicSwordCard(cost, type, rarity, target)
{
    public override SwordId? Sword => SwordId.Tyrfing;

    protected sealed override async Task OnCardPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await OnTyrfingPlay(choiceContext, cardPlay);
        // One "use" per card play, not per extra repetition of the same play (PlayIndex > 0).
        if (cardPlay.IsFirstInSeries) await TyrfingCurses.OnTyrfingCardUsed(Owner);
    }

    /// <summary>The card's own effect (Tyrfing is already current here, unless it could not switch).</summary>
    protected abstract Task OnTyrfingPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay);

    /// <summary>Extra damage per hit of THIS card's powered attacks (also used by the preview: keep it pure).</summary>
    protected virtual decimal BonusDamage(Creature? target) => 0m;

    /// <summary>Damage multiplier of THIS card's powered attacks (preview-safe).</summary>
    protected virtual decimal DamageMultiplier(Creature? target) => 1m;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        var bonus = base.ModifyDamageAdditive(target, amount, props, dealer, cardSource);
        if (ReferenceEquals(cardSource, this) && props.IsPoweredAttack()) bonus += BonusDamage(target);
        return bonus;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        var mult = base.ModifyDamageMultiplicative(target, amount, props, dealer, cardSource);
        if (ReferenceEquals(cardSource, this) && props.IsPoweredAttack()) mult *= DamageMultiplier(target);
        return mult;
    }

    /// <summary>Curse cards in the owner's hand, draw pile and discard pile.</summary>
    protected int CursesInPiles()
    {
        if (!IsMutable || Owner?.PlayerCombatState == null) return 0;
        return PileType.Hand.GetPile(Owner).Cards
            .Concat(PileType.Draw.GetPile(Owner).Cards)
            .Concat(PileType.Discard.GetPile(Owner).Cards)
            .Count(c => c.Type == CardType.Curse);
    }
}

/// <summary>
/// Tyrfing's run curses ("세 번의 악행", content doc §3.2): in the order of the source —
/// 스바프를라미의 최후 → 햐르마르의 죽음 → 앙간튀르의 죽음. The count is the saved Mangeomchong run counter
/// <see cref="RunCounterKey"/>, so it survives save/load and never exceeds 3.
/// </summary>
public static class TyrfingCurses
{
    public const string RunCounterKey = "tyrfing.curses";
    public const int MaxCurses = 3;

    /// <summary>[임시, 사용자 결정 2026-10-04] curses only start once Tyrfing has been upgraded to this level.
    /// Uses below this level do not count.</summary>
    public const int CurseStartLevel = 3;

    public static CardModel? CurseFor(int index) => index switch
    {
        0 => ModelDb.Card<SvafrlamisEnd>(),
        1 => ModelDb.Card<HjalmarsDeath>(),
        2 => ModelDb.Card<AngantyrsDeath>(),
        _ => null,
    };

    public static async Task OnTyrfingCardUsed(Player player)
    {
        var relic = player.GetRelic<Mangeomchong>();
        if (relic == null) return;
        if (relic.GetLevel(SwordId.Tyrfing) < CurseStartLevel) return;
        var given = relic.GetRunCounter(RunCounterKey);
        if (given >= MaxCurses || CurseFor(given) is not { } curse) return;
        relic.SetRunCounter(RunCounterKey, given + 1);
        // Permanent: added to the DECK (Mangeomchong.AddCurse -> CardPileCmd.AddCursesToDeck), not to this combat.
        await relic.AddCurse(curse);
    }
}
