using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Random;

namespace MagicSwordsman.MagicSwordsmanCode.RestSite;

public enum ForgeMode
{
    Safe,        // 안전 강화: +1, a roll since 2026-10-15 (failure = no change, never shatters)
    GamblePlus2, // 도박 강화 +2
    GamblePlus3, // 도박 강화 +3
}

public enum ForgeOutcomeKind
{
    Success,
    FailureLevelDown,
    FailureCurse,
    FailureNothing, // level 0 and the sword has no curse card yet
    Shatter,        // sword lost (cards stored in Mangeomchong)
    ShatterReset,   // SwordDefinition.CanBeLost = false: back to level 0 instead of being lost (unused since 2026-10-09)
}

public readonly record struct ForgeOdds(int Success, int Failure, int Shatter);

public readonly record struct ForgeOutcome(ForgeOutcomeKind Kind, SwordId Sword, int OldLevel, int NewLevel);

/// <summary>
/// Implement on a relic to change "마검 강화" (content doc §7: 레긴의 모루 odds, 흑칠 칼집 shatter insurance).
/// Default no-ops. Queried from <c>player.Relics</c> by <see cref="SwordForge"/>.
/// </summary>
public interface ISwordForgeModifier
{
    /// <summary>Change the odds of one attempt (values are percent; keep the sum at 100).</summary>
    ForgeOdds ModifyForgeOdds(Player player, SwordId sword, ForgeMode mode, ForgeOdds odds) => odds;

    /// <summary>Called when a shatter was rolled. Return true to turn it into a normal failure (used up or not).</summary>
    Task<bool> TryPreventShatter(Player player, SwordId sword, ForgeMode mode) => Task.FromResult(false);
}

/// <summary>
/// Pure rules of the rest-site "마검 강화" (spec §4). UI lives in <see cref="SwordForgeRestSiteOption"/>.
/// Odds (user decision 2026-10-15): every mode gets harder as the sword's CURRENT level rises, no mode is certain
/// (안전 강화 is a roll too; its failure changes nothing) and reaching level 5 is never guaranteed.
/// Percent success / failure / shatter by current level:
///   안전 +1 : L0 95/5/0   L1 85/15/0  L2 75/25/0  L3 50/50/0  L4 25/75/0
///   도박 +2 : L0 60/37/3  L1 50/45/5  L2 40/53/7  L3 20/68/12
///   도박 +3 : L0 35/55/10 L1 25/63/12 L2 15/72/13
/// Gambles that would pass level 5 are not offered. A failed gamble = curse or level -1 (50:50; curse at level 0).
/// </summary>
public static class SwordForge
{
    public static int GainFor(ForgeMode mode) => mode switch
    {
        ForgeMode.Safe => 1,
        ForgeMode.GamblePlus2 => 2,
        ForgeMode.GamblePlus3 => 3,
        _ => 1,
    };

    private static readonly ForgeOdds[] SafeOdds =
        [new(95, 5, 0), new(85, 15, 0), new(75, 25, 0), new(50, 50, 0), new(25, 75, 0)];

    private static readonly ForgeOdds[] Gamble2Odds =
        [new(60, 37, 3), new(50, 45, 5), new(40, 53, 7), new(20, 68, 12)];

    private static readonly ForgeOdds[] Gamble3Odds =
        [new(35, 55, 10), new(25, 63, 12), new(15, 72, 13)];

    /// <summary>Base odds of <paramref name="mode"/> for a sword currently at <paramref name="currentLevel"/>.</summary>
    public static ForgeOdds OddsFor(ForgeMode mode, int currentLevel = 0)
    {
        var table = mode switch
        {
            ForgeMode.GamblePlus2 => Gamble2Odds,
            ForgeMode.GamblePlus3 => Gamble3Odds,
            _ => SafeOdds,
        };
        return table[Math.Clamp(currentLevel, 0, table.Length - 1)];
    }

    /// <summary>Odds for this player and sword (at its current level), after every relic's <see cref="ISwordForgeModifier"/>.</summary>
    public static ForgeOdds OddsFor(Player player, SwordId sword, ForgeMode mode)
    {
        var level = player.GetRelic<Mangeomchong>()?.GetLevel(SwordRegistry.GroupLeader(sword)) ?? 0;
        var odds = OddsFor(mode, level);
        foreach (var modifier in Modifiers(player))
            odds = modifier.ModifyForgeOdds(player, sword, mode, odds);
        return odds;
    }

    public static IEnumerable<ISwordForgeModifier> Modifiers(Player player) =>
        player.Relics.OfType<ISwordForgeModifier>().ToList();

    /// <summary>Spec: a gamble that would exceed the max level cannot be chosen.</summary>
    public static bool IsAllowed(ForgeMode mode, int currentLevel) =>
        currentLevel + GainFor(mode) <= SwordRegistry.MaxLevel;

    /// <summary>Owned swords that can still be upgraded (pair = one entry: its leader, e.g. Ganjiang).</summary>
    public static List<SwordId> UpgradeableSwords(Mangeomchong relic) =>
        relic.OwnedSwords
            .Select(SwordRegistry.GroupLeader)
            .Distinct()
            .Where(s => relic.GetLevel(s) < SwordRegistry.MaxLevel)
            .ToList();

    /// <summary>
    /// Rolls and applies one forge attempt. <paramref name="rng"/> must be a game Rng (seeded, multiplayer-safe).
    /// </summary>
    public static async Task<ForgeOutcome> Apply(Player player, SwordId sword, ForgeMode mode, Rng rng)
    {
        var relic = player.GetRelic<Mangeomchong>() ??
                    throw new InvalidOperationException("SwordForge requires Mangeomchong");
        sword = SwordRegistry.GroupLeader(sword);
        var oldLevel = relic.GetLevel(sword);
        var odds = OddsFor(player, sword, mode);

        var roll = rng.NextInt(100);
        if (roll < odds.Success)
        {
            var newLevel = Math.Min(SwordRegistry.MaxLevel, oldLevel + GainFor(mode));
            await relic.SetLevel(sword, newLevel);
            return new ForgeOutcome(ForgeOutcomeKind.Success, sword, oldLevel, newLevel);
        }

        if (roll < odds.Success + odds.Failure)
        {
            // 안전 강화 failing costs nothing: the level stays (FailureNothing result text "단계는 그대로다").
            if (mode == ForgeMode.Safe) return new ForgeOutcome(ForgeOutcomeKind.FailureNothing, sword, oldLevel, oldLevel);
            return await ApplyFailure(player, relic, sword, oldLevel, rng);
        }

        // shatter (a relic may turn it into a normal failure: 흑칠 칼집)
        foreach (var modifier in Modifiers(player))
        {
            if (await modifier.TryPreventShatter(player, sword, mode))
                return await ApplyFailure(player, relic, sword, oldLevel, rng);
        }

        if (!SwordRegistry.GetDefinition(sword).CanBeLost)
        {
            await relic.SetLevel(sword, 0);
            return new ForgeOutcome(ForgeOutcomeKind.ShatterReset, sword, oldLevel, 0);
        }

        await relic.LoseSword(sword);
        return new ForgeOutcome(ForgeOutcomeKind.Shatter, sword, oldLevel, 0);
    }

    /// <summary>
    /// Event reward "보유 검 하나 +1" (비문 / 공명 events): certain below level 4, but 4 -> 5 is never guaranteed (user
    /// decision 2026-10-15), so at level 4 it is a 안전 강화 roll (25%, failure = no change).
    /// </summary>
    public static async Task<ForgeOutcome> ApplyEventUpgrade(Player player, SwordId sword, Rng rng)
    {
        var relic = player.GetRelic<Mangeomchong>() ??
                    throw new InvalidOperationException("SwordForge requires Mangeomchong");
        sword = SwordRegistry.GroupLeader(sword);
        var oldLevel = relic.GetLevel(sword);
        if (oldLevel >= SwordRegistry.MaxLevel - 1) return await Apply(player, sword, ForgeMode.Safe, rng);
        await relic.SetLevel(sword, oldLevel + 1);
        return new ForgeOutcome(ForgeOutcomeKind.Success, sword, oldLevel, oldLevel + 1);
    }

    /// <summary>
    /// Failure: the sword's curse card OR level -1, 50:50 ([Claude] spec §4). At level 0: curse only.
    /// If the sword has no curse card registered yet, the level drops instead (nothing at level 0).
    /// </summary>
    private static async Task<ForgeOutcome> ApplyFailure(Player player, Mangeomchong relic, SwordId sword,
        int oldLevel, Rng rng)
    {
        var curse = SwordRegistry.Get(sword).FailureCurse;
        var wantCurse = oldLevel == 0 || rng.NextBool();

        if (wantCurse && curse != null)
        {
            await relic.AddCurse(curse);
            return new ForgeOutcome(ForgeOutcomeKind.FailureCurse, sword, oldLevel, oldLevel);
        }

        if (oldLevel > 0)
        {
            await relic.SetLevel(sword, oldLevel - 1);
            return new ForgeOutcome(ForgeOutcomeKind.FailureLevelDown, sword, oldLevel, oldLevel - 1);
        }

        return new ForgeOutcome(ForgeOutcomeKind.FailureNothing, sword, oldLevel, oldLevel);
    }
}
