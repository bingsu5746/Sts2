using System.Reflection;
using MagicSwordsman.MagicSwordsmanCode.Cards.Tokens;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace MagicSwordsman.MagicSwordsmanCode.Swords;

/// <summary>
/// Central lookup: SwordId -> static <see cref="SwordDefinition"/> + the registered <see cref="SwordBehavior"/>.
///
/// Behaviors are registered automatically: every non-abstract subclass of <see cref="SwordBehavior"/> with a
/// public parameterless constructor in this assembly is instantiated once by <see cref="AutoRegister"/>
/// (called from MainFile.Initialize). A sword without a behavior falls back to
/// <see cref="PlaceholderSwordBehavior"/> (no effect), so the game never crashes on a missing sword.
/// </summary>
public static class SwordRegistry
{
    public const int MaxLevel = 5;          // spec §4: max 5 for every sword
    public const int BaseSlotCapacity = 3;  // spec §2: base 3 swords (Ganjiang+Moye = 2 slots)

    private static readonly Dictionary<SwordId, SwordDefinition> Definitions = new()
    {
        // Gram: an ordinary sword since 2026-10-09 — in every pool (no longer the fixed first sword) and, like the others,
        // lost by a forge shatter (its "back to level 0" protection was removed the same day).
        [SwordId.Gram] = new SwordDefinition { Id = SwordId.Gram },
        [SwordId.Ganjiang] = new SwordDefinition { Id = SwordId.Ganjiang, Partners = [SwordId.Moye] },
        [SwordId.Moye] = new SwordDefinition
        {
            Id = SwordId.Moye, Partners = [SwordId.Ganjiang], LevelOwnerOverride = SwordId.Ganjiang,
            OfferedByAcquisition = false
        },
        [SwordId.Kusanagi] = new SwordDefinition { Id = SwordId.Kusanagi },
        [SwordId.Tyrfing] = new SwordDefinition { Id = SwordId.Tyrfing },
        [SwordId.Dainsleif] = new SwordDefinition { Id = SwordId.Dainsleif },
        [SwordId.Durandal] = new SwordDefinition { Id = SwordId.Durandal },
        [SwordId.Skofnung] = new SwordDefinition { Id = SwordId.Skofnung },
        [SwordId.Onimaru] = new SwordDefinition { Id = SwordId.Onimaru, EmergesAtCombatStart = true },
        [SwordId.ClaiomhSolais] = new SwordDefinition { Id = SwordId.ClaiomhSolais },
        [SwordId.Caladbolg] = new SwordDefinition { Id = SwordId.Caladbolg },
    };

    private static readonly Dictionary<SwordId, SwordBehavior> Behaviors = new();

    public static IEnumerable<SwordId> AllSwords => Definitions.Keys.OrderBy(id => (int)id);

    public static SwordDefinition GetDefinition(SwordId id) => Definitions[id];

    /// <summary>The behavior for a sword (placeholder if none was registered).</summary>
    public static SwordBehavior Get(SwordId id)
    {
        if (Behaviors.TryGetValue(id, out var behavior)) return behavior;
        behavior = new PlaceholderSwordBehavior(id);
        Behaviors[id] = behavior;
        return behavior;
    }

    public static bool HasRealBehavior(SwordId id) =>
        Behaviors.TryGetValue(id, out var b) && b is not PlaceholderSwordBehavior;

    /// <summary>Manually register a behavior (replaces an existing one). Normally not needed: see AutoRegister.</summary>
    public static void Register(SwordBehavior behavior)
    {
        if (Behaviors.TryGetValue(behavior.Id, out var existing) && existing is not PlaceholderSwordBehavior &&
            existing.GetType() != behavior.GetType())
        {
            MainFile.Logger.Warn(
                $"SwordRegistry: {behavior.Id} already has behavior {existing.GetType().Name}; replacing with {behavior.GetType().Name}.");
        }

        Behaviors[behavior.Id] = behavior;
    }

    /// <summary>Instantiates and registers every concrete SwordBehavior subclass in the assembly.</summary>
    public static void AutoRegister(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(SwordBehavior).IsAssignableFrom(type)) continue;
            if (type == typeof(PlaceholderSwordBehavior)) continue;
            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                MainFile.Logger.Warn($"SwordRegistry: {type.Name} has no parameterless constructor; not registered.");
                continue;
            }

            try
            {
                Register((SwordBehavior)Activator.CreateInstance(type)!);
            }
            catch (Exception e)
            {
                MainFile.Logger.Error($"SwordRegistry: failed to create {type.Name}: {e}");
            }
        }

        foreach (var id in AllSwords)
        {
            if (!HasRealBehavior(id))
                MainFile.Logger.Info($"SwordRegistry: {id} has no behavior yet (placeholder in use).");
        }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>The sword plus its partners (Ganjiang -> [Ganjiang, Moye]).</summary>
    public static IReadOnlyList<SwordId> WithPartners(SwordId id)
    {
        var def = GetDefinition(id);
        if (def.Partners.Count == 0) return [id];
        return new[] { id }.Concat(def.Partners).Distinct().OrderBy(s => (int)s).ToList();
    }

    /// <summary>Total slots the sword and its partners occupy (Ganjiang -> 2).</summary>
    public static int GroupSlotCost(SwordId id) => WithPartners(id).Sum(s => GetDefinition(s).SlotCost);

    /// <summary>The sword that represents the group at the rest site / in offers (Moye -> Ganjiang).</summary>
    public static SwordId GroupLeader(SwordId id) => GetDefinition(id).LevelOwner;

    /// <summary>Localized display name (taken from the sword's selection token card title).</summary>
    public static string DisplayName(SwordId id)
    {
        try
        {
            return SwordTokenCard.CanonicalFor(id).Title;
        }
        catch (Exception)
        {
            return id.ToString();
        }
    }

    /// <summary>
    /// Random acquisition candidates for events (spec §5: "무작위 후보 3자루 중 1 선택").
    /// Excludes owned swords, swords that do not fit in the free slots, and non-offerable swords.
    /// Uses the given game Rng (never System.Random) so the result is seeded / multiplayer-safe.
    /// </summary>
    public static List<SwordId> RollAcquisitionCandidates(MegaCrit.Sts2.Core.Entities.Players.Player player, int count,
        Rng rng)
    {
        var relic = player.GetRelic<Relics.Mangeomchong>();
        var pool = AllSwords
            .Where(id => GetDefinition(id).OfferedByAcquisition)
            .Where(id => relic == null || relic.CanAcquire(id))
            .ToList();
        var result = new List<SwordId>();
        while (result.Count < count && pool.Count > 0)
        {
            var pick = pool[rng.NextInt(pool.Count)];
            pool.Remove(pick);
            result.Add(pick);
        }

        return result;
    }

    /// <summary>
    /// Swords that can be the random first sword of a run (사용자 결정 2026-10-09): every offerable sword, pairs as their
    /// leader (간장·막야 -> Ganjiang), i.e. all 10 swords. Sorted by id so every client builds the same list.
    /// </summary>
    public static List<SwordId> StartingSwordCandidates() =>
        AllSwords.Where(id => GetDefinition(id).OfferedByAcquisition).Select(GroupLeader).Distinct().ToList();

    /// <summary>Resolves a canonical card model from a type (for content that stores card types).</summary>
    public static CardModel CanonicalCard(Type cardType) => ModelDb.GetById<CardModel>(ModelDb.GetId(cardType));
}
