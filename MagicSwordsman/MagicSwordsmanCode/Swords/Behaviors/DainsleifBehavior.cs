using System.Runtime.CompilerServices;
using MagicSwordsman.MagicSwordsmanCode.Cards.Dainsleif;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Curses;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

/// <summary>
/// 다인슬레이프. Spec §7 [확정]:
///  - Current effect: played cards that would go to the discard pile go to a RANDOM position of the draw pile
///    instead (content doc 0.4 #17). Exhausted / power cards are unchanged. Spec §9 [확정] (2026-10-03, replaces the
///    old 12-per-turn cap): only cards that cost 1 or more return (<see cref="DainsleifReturn.CanReturn"/>), so 0-cost
///    cards cannot loop forever; 0-cost cards are discarded normally.
///    Kusanagi inherits: only the first such card each turn.
///  - Cost: cannot gain block while Dainsleif is current (ModifyBlockMultiplicative -> 0, like the game's
///    NoBlockPower). Not inherited by Kusanagi.
///
/// Timing note (verified in CardModel.OnPlayWrapper): the game decides the result pile BEFORE the card's OnPlay,
/// i.e. before a sword card switches the current sword. The decision therefore uses the sword that will be current
/// AFTER the card's own switch (<see cref="SwordCombat.EffectiveSwordFor"/>): a Dainsleif card played while another
/// sword is current returns (handled by <see cref="DainsleifCard"/>), and another sword's card played while
/// Dainsleif is current is discarded normally (it switches away first).
/// </summary>
public sealed class DainsleifBehavior : SwordBehavior
{
    public override SwordId Id => SwordId.Dainsleif;

    public override IEnumerable<CardModel> StarterCards =>
        [ModelDb.Card<DainsleifLegacy>(), ModelDb.Card<DainsleifUnsheathe>()];

    /// <summary>화해할 수 없음 (content doc §3.1).</summary>
    public override CardModel? FailureCurse => ModelDb.Card<IrreconcilableCurse>();

    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(SwordContext ctx,
        CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
    {
        if (card.Owner != ctx.Player) return (pileType, position);

        // A sword card that switches to ANOTHER sword resolves under that sword -> not returned by Dainsleif.
        var (effective, preview) = SwordCombat.EffectiveSwordFor(ctx.Player, card);
        if (preview && effective != null) return (pileType, position);

        return DainsleifReturn.Decide(ctx.Player, card, resources, pileType, position, ctx.IsInherited);
    }

    public override decimal ModifyBlockMultiplicative(SwordContext ctx, Creature target, decimal block,
        ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (ctx.IsInherited || target != ctx.Creature) return 1m; // the cost is not inherited by Kusanagi
        return 0m;
    }
}

/// <summary>
/// Shared Dainsleif return logic (current sword effect, Dainsleif cards switching in, Kusanagi inheritance,
/// 햐드닝아비그 / 빗나가지 않는 칼 powers). Per-turn numbers live in the Dainsleif counters of SwordCombatState.
/// </summary>
public static class DainsleifReturn
{
    /// <summary>
    /// Spec §9 [확정]: only cards that cost 1 or more return (loop guard; replaces the old 12-per-turn cap).
    /// Printed cost is used (a 1-cost card made free this turn still counts as a 1-cost card). X-cost cards return
    /// only when at least 1 energy was spent on them.
    /// </summary>
    public static bool CanReturn(CardModel card, ResourceInfo resources) =>
        card.EnergyCost.CostsX ? resources.EnergySpent >= 1 : card.EnergyCost.Canonical >= 1;

    private const string TurnKey = "return_turn";
    private const string CountKey = "returned";
    private const string InheritedKey = "returned_inherited";
    private const string TopKey = "returned_top";

    /// <summary>Card -> turn number in which Dainsleif decided to return it (consumed by 빗나가지 않는 칼).</summary>
    private static readonly ConditionalWeakTable<CardModel, StrongBox<int>> PendingReturn = new();

    /// <summary>Cards Dainsleif returned to the draw pile this turn (for 회그니 왕).</summary>
    public static int ReturnedThisTurn(Player player)
    {
        var state = SwordCombat.Get(player);
        if (state == null) return 0;
        var turn = player.PlayerCombatState?.TurnNumber ?? 0;
        return state.GetCounter(SwordId.Dainsleif, TurnKey) == turn ? state.GetCounter(SwordId.Dainsleif, CountKey) : 0;
    }

    /// <summary>
    /// Called only from ModifyCardPlayResultPileTypeAndPosition (real card plays only — the game calls that hook
    /// exclusively from CardModel.OnPlayWrapper), so counting here is safe.
    /// </summary>
    public static (PileType, CardPilePosition) Decide(Player player, CardModel card, ResourceInfo resources,
        PileType pileType, CardPilePosition position, bool inherited)
    {
        if (pileType != PileType.Discard) return (pileType, position);
        PendingReturn.Remove(card); // a new play: forget any older mark
        var state = SwordCombat.Get(player);
        if (state == null) return (pileType, position);

        var turn = player.PlayerCombatState?.TurnNumber ?? 0;
        if (state.GetCounter(SwordId.Dainsleif, TurnKey) != turn)
        {
            state.SetCounter(SwordId.Dainsleif, TurnKey, turn);
            state.SetCounter(SwordId.Dainsleif, CountKey, 0);
            state.SetCounter(SwordId.Dainsleif, InheritedKey, 0);
            state.SetCounter(SwordId.Dainsleif, TopKey, 0);
        }

        if (!CanReturn(card, resources)) return (pileType, position);
        if (inherited)
        {
            // Kusanagi inheritance [확정]: only the first card each turn.
            if (state.GetCounter(SwordId.Dainsleif, InheritedKey) > 0) return (pileType, position);
            state.SetCounter(SwordId.Dainsleif, InheritedKey, 1);
        }

        state.AddCounter(SwordId.Dainsleif, CountKey, 1);

        var newPosition = CardPilePosition.Random;
        // 햐드닝아비그: the first 2 returned cards each turn go on TOP (not cards that cost 0 — loop guard).
        var hj = player.Creature.GetPower<DainsleifHjadningavigPower>();
        if (hj != null && resources.EnergySpent > 0 && card.EnergyCost.Canonical > 0 &&
            state.GetCounter(SwordId.Dainsleif, TopKey) < DainsleifHjadningavigPower.TopCardsPerTurn)
        {
            state.AddCounter(SwordId.Dainsleif, TopKey, 1);
            newPosition = CardPilePosition.Top;
        }

        PendingReturn.AddOrUpdate(card, new StrongBox<int>(turn));
        return (PileType.Draw, newPosition);
    }

    /// <summary>True once for a card Dainsleif just sent back to the draw pile (빗나가지 않는 칼 trigger).</summary>
    public static bool ConsumeReturned(CardModel card)
    {
        if (!PendingReturn.TryGetValue(card, out var turn)) return false;
        PendingReturn.Remove(card);
        return turn.Value == (card.Owner?.PlayerCombatState?.TurnNumber ?? -1);
    }
}
