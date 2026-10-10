using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Dialogue;

/// <summary>
/// 친밀도 (affinity) between Ensifer and each sword he keeps (user request 2026-10-09). Run-level, saved.
///
/// Storage: Mangeomchong's saved <see cref="Mangeomchong.RunCounters"/> (key "affinity.&lt;SwordId int&gt;"), the same
/// [SavedProperty] that already carries Tyrfing's curse count, so no new saved field is needed. Affinity survives
/// losing and regaining a sword (the tomb remembers).
///
/// Gains (all deterministic from game events, so every multiplayer client computes the same value):
///  - a sword card played in combat: +1, at most <see cref="CardGainCapPerCombat"/> per sword per combat;
///  - combat won while the sword was out (summoned, or emerged like Onimaru): +3 (elite / boss +5);
///  - the sword's level goes up (forge, events): +4 (both blades of Ganjiang/Moye).
/// Tiers: 0 경계 (0+) / 1 익숙 (12+) / 2 신뢰 (35+) / 3 각별 (70+).
/// </summary>
public static class SwordAffinity
{
    public const string LocTable = "events";
    public const string LocPrefix = "MAGICSWORDSMAN-SWORD_TALK";

    private const string CounterPrefix = "affinity.";
    private const string CardGainCounter = "affinity_card_gain";

    public const int CardGainCapPerCombat = 3;
    public const int VictoryGain = 3;
    public const int BigVictoryGain = 5;
    public const int UpgradeGain = 4;

    /// <summary>Minimum points for tier 0..3.</summary>
    public static readonly int[] TierThresholds = [0, 12, 35, 70];

    public static int TierCount => TierThresholds.Length;

    public static int Points(Player player, SwordId sword) =>
        player.GetRelic<Mangeomchong>()?.GetRunCounter(CounterPrefix + (int)sword) ?? 0;

    public static int TierOf(int points)
    {
        var tier = 0;
        for (var i = 0; i < TierThresholds.Length; i++)
            if (points >= TierThresholds[i]) tier = i;
        return tier;
    }

    public static int Tier(Player player, SwordId sword) => TierOf(Points(player, sword));

    /// <summary>Adds points; returns the tier before the change and after it.</summary>
    public static (int Before, int After) Add(Player player, SwordId sword, int amount)
    {
        var relic = player.GetRelic<Mangeomchong>();
        if (relic == null || amount == 0) return (0, 0);
        var before = relic.GetRunCounter(CounterPrefix + (int)sword);
        var after = Math.Max(0, before + amount);
        relic.SetRunCounter(CounterPrefix + (int)sword, after);
        return (TierOf(before), TierOf(after));
    }

    // ------------------------------------------------------------------ gains

    /// <summary>Called from SwordCombat.OnSwordCardPlayed (every client).</summary>
    public static void OnCardPlayed(Player player, SwordId sword)
    {
        try
        {
            var state = SwordCombat.Get(player);
            if (state == null) return;
            if (state.GetCounter(sword, CardGainCounter) >= CardGainCapPerCombat) return;
            state.AddCounter(sword, CardGainCounter, 1);
            Gain(player, sword, 1);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordAffinity] card gain failed: {e.Message}");
        }
    }

    /// <summary>Combat won: every sword that was out gains. Returns the swords that gained.</summary>
    public static List<SwordId> OnVictory(Player player, IReadOnlyCollection<SwordId> swordsOut, CombatRoom room)
    {
        var relic = player.GetRelic<Mangeomchong>();
        if (relic == null) return [];
        var gain = room.RoomType is RoomType.Elite or RoomType.Boss ? BigVictoryGain : VictoryGain;
        var gained = new List<SwordId>();
        foreach (var sword in swordsOut.Where(relic.Owns))
        {
            Gain(player, sword, gain);
            gained.Add(sword);
        }

        return gained;
    }

    /// <summary>The sword's (pair's) level went up.</summary>
    public static void OnUpgraded(Player player, SwordId sword)
    {
        foreach (var s in SwordRegistry.WithPartners(sword)) Gain(player, s, UpgradeGain);
    }

    /// <summary>
    /// An affinity gain from play (cards, victories, upgrades). Affinity only changes what the swords SAY: no item,
    /// card or rule reads it for damage, block or any other game effect (user decision 2026-10-10).
    /// </summary>
    public static (int Before, int After) Gain(Player player, SwordId sword, int amount) => Add(player, sword, amount);

    // ------------------------------------------------------------------ text

    public static LocString TierName(int tier) => new(LocTable, $"{LocPrefix}.TIER.T{Math.Clamp(tier, 0, 3)}");

    /// <summary>"친밀도: 신뢰" for the floating sword's hover tip ("" when unavailable).</summary>
    public static string TipLine(Player player, SwordId sword)
    {
        try
        {
            var label = new LocString(LocTable, $"{LocPrefix}.AFFINITY_LABEL").GetFormattedText();
            return $"{label}: {TierName(Tier(player, sword)).GetFormattedText()}";
        }
        catch (Exception)
        {
            return "";
        }
    }
}
