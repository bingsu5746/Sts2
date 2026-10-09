using MagicSwordsman.MagicSwordsmanCode.Cards.Kusanagi;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Powers;

/// <summary>
/// 헌상 (card KusanagiOffering): whenever a status/curse card of the owner is exhausted, gain Amount Block.
/// Same hook and Unpowered block as the game's FeelNoPainPower, restricted to status/curse cards.
/// </summary>
public sealed class KusanagiOfferingPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.Block)];

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner?.Creature != Owner || !KusanagiPurify.IsStatusOrCurse(card)) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}

/// <summary>
/// 이 빠진 도쓰카 (card KusanagiChippedTotsuka), removed at the end of the owner's turn: while Kusanagi is the
/// effective sword, the inherited sword's additive damage/block bonus applies at full strength. Implemented by
/// adding the missing half on top of CurrentSwordPower / Mangeomchong (full minus inherited value), only when both
/// values point the same way, so a penalty that only exists at full strength (e.g. Gram under Balmung) never leaks in.
/// </summary>
public sealed class KusanagiChippedTotsukaPower : MagicSwordsmanPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private Player? OwnerPlayer => IsMutable ? Owner?.Player : null;

    /// <summary>The inherited effects that apply to numbers from <paramref name="cardSource"/>, with a full-strength context.</summary>
    private IEnumerable<(SwordBehavior Behavior, SwordContext Inherited, SwordContext Full)> Inherited(CardModel? cardSource)
    {
        var player = OwnerPlayer;
        if (player == null) yield break;
        var (sword, preview) = SwordCombat.EffectiveSwordFor(player, cardSource);
        if (sword != SwordId.Kusanagi) yield break;
        foreach (var (b, ctx) in SwordCombat.CurrentEffects(player, SwordId.Kusanagi, preview))
        {
            if (!ctx.IsInherited) continue;
            yield return (b, ctx, new SwordContext
            {
                Player = ctx.Player, Sword = ctx.Sword, Level = ctx.Level, IsInherited = false,
                IsCurrent = ctx.IsCurrent, IsPresent = ctx.IsPresent, Combat = ctx.Combat,
                IsPreview = ctx.IsPreview, PreviousSword = ctx.PreviousSword,
            });
        }
    }

    /// <summary>Extra amount that turns <paramref name="half"/> into <paramref name="full"/> (0 if they disagree).</summary>
    private static decimal Missing(decimal half, decimal full)
    {
        if (half == 0m || full == 0m || Math.Sign(half) != Math.Sign(full)) return 0m;
        return Math.Abs(full) > Math.Abs(half) ? full - half : 0m;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer,
        CardModel? cardSource)
    {
        decimal sum = 0m;
        foreach (var (b, half, full) in Inherited(cardSource))
            sum += Missing(b.ModifyDamageAdditive(half, target, amount, props, dealer, cardSource),
                b.ModifyDamageAdditive(full, target, amount, props, dealer, cardSource));
        return sum;
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource,
        CardPlay? cardPlay)
    {
        decimal sum = 0m;
        foreach (var (b, half, full) in Inherited(cardSource))
            sum += Missing(b.ModifyBlockAdditive(half, target, block, props, cardSource, cardPlay),
                b.ModifyBlockAdditive(full, target, block, props, cardSource, cardPlay));
        return sum;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}
