using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Powers;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Players;

namespace MagicSwordsman.MagicSwordsmanCode.Cards.GanjiangMoye;

/// <summary>
/// 【짝】 (Ganjiang/Moye pair bonus) rules, content doc §0.3:
///  - a Ganjiang card triggers it when the current sword right before it was Moye, and vice versa;
///  - twin-sword cards (쌍검) always trigger it, and the NEXT Ganjiang/Moye card after a twin card triggers it
///    whichever side it is ("primed");
///  - 암수 한 쌍 (TwinMatedPair): for the rest of this turn every Ganjiang/Moye card triggers it.
/// Every trigger also feeds 막야의 투신 (<see cref="MoyesSacrificePower"/>).
/// Per-combat bookkeeping lives in SwordCombatState counters on SwordId.Ganjiang (reset every combat).
/// </summary>
public static class PairRules
{
    private const string PrimedKey = "pair_primed";
    private const string AlwaysTurnKey = "pair_always_turn";

    /// <summary>Whether a card with this side/twin flag would trigger 【짝】 if played now (pure; for glow/preview).</summary>
    public static bool WouldTrigger(Player player, SwordId? side, bool isTwin, SwordId? swordBeforePlay)
    {
        if (isTwin) return true;
        var state = SwordCombat.Get(player);
        if (state == null) return false;
        if (state.GetCounter(SwordId.Ganjiang, PrimedKey) > 0) return true;
        var always = state.GetCounter(SwordId.Ganjiang, AlwaysTurnKey);
        if (always > 0 && always == TurnNumber(player)) return true;
        return side switch
        {
            SwordId.Ganjiang => swordBeforePlay == SwordId.Moye,
            SwordId.Moye => swordBeforePlay == SwordId.Ganjiang,
            _ => false,
        };
    }

    /// <summary>
    /// Resolves 【짝】 for a Ganjiang/Moye card being played: returns whether it triggers, consumes / sets the
    /// twin "primed" flag and notifies 막야의 투신. Call exactly once per play, from the card's effect.
    /// </summary>
    public static async Task<bool> Resolve(Player player, SwordId? side, bool isTwin, SwordId? swordBeforePlay)
    {
        var triggers = WouldTrigger(player, side, isTwin, swordBeforePlay);
        var state = SwordCombat.Get(player);
        state?.SetCounter(SwordId.Ganjiang, PrimedKey, isTwin ? 1 : 0);

        if (triggers)
        {
            var power = player.Creature.GetPower<MoyesSacrificePower>();
            if (power != null) await power.OnPairTriggered();
        }

        return triggers;
    }

    /// <summary>암수 한 쌍: every Ganjiang/Moye card triggers 【짝】 for the rest of this turn.</summary>
    public static void MakeAlwaysThisTurn(Player player)
    {
        SwordCombat.Get(player)?.SetCounter(SwordId.Ganjiang, AlwaysTurnKey, TurnNumber(player));
    }

    private static int TurnNumber(Player player) => player.PlayerCombatState?.TurnNumber ?? 0;
}
