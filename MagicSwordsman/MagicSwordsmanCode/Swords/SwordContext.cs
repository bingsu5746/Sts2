using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace MagicSwordsman.MagicSwordsmanCode.Swords;

/// <summary>
/// Everything a <see cref="SwordBehavior"/> needs to know when one of its hooks runs.
/// Created by the framework; behaviors should treat it as read-only.
/// </summary>
public sealed class SwordContext
{
    public required Player Player { get; init; }

    /// <summary>The sword whose behavior is running (for an inherited effect: the ORIGINAL sword, e.g. Gram).</summary>
    public required SwordId Sword { get; init; }

    /// <summary>The upgrade level (0..5) of <see cref="Sword"/>.</summary>
    public required int Level { get; init; }

    /// <summary>
    /// True when Kusanagi is running this behavior as an inherited effect (spec: 50%, rounded down, min 1,
    /// costs/penalties are NOT inherited). Use <see cref="Scale(int)"/> for numbers and skip penalties.
    /// </summary>
    public bool IsInherited { get; init; }

    /// <summary>True when <see cref="Sword"/> is the current sword (always true for current-effect hooks).</summary>
    public bool IsCurrent { get; init; }

    /// <summary>True when the sword is floating around (summoned this combat, or emerged on its own like Onimaru).</summary>
    public bool IsPresent { get; init; }

    /// <summary>Per-combat sword state for this player (null outside combat).</summary>
    public SwordCombatState? Combat { get; init; }

    public Creature Creature => Player.Creature;

    public Mangeomchong? Relic => Player.GetRelic<Mangeomchong>();

    /// <summary>The player's turn number in this combat (1 on the first turn, 0 outside combat).</summary>
    public int TurnNumber => Player.PlayerCombatState?.TurnNumber ?? 0;

    /// <summary>
    /// Scales a full-strength number for inheritance: full value normally; when inherited, half rounded down
    /// with a minimum of 1 (but 0 stays 0) — spec §7 "50%(버림, 최소 1)".
    /// </summary>
    public int Scale(int full)
    {
        if (!IsInherited) return full;
        if (full == 0) return 0;
        var half = full / 2;
        return full > 0 ? Math.Max(1, half) : Math.Min(-1, half);
    }

    public decimal Scale(decimal full) => Scale((int)full);

    /// <summary>Per-combat counter namespaced to this sword (e.g. "light", "wounds_applied").</summary>
    public int GetCounter(string key) => Combat?.GetCounter(Sword, key) ?? 0;

    public void SetCounter(string key, int value) => Combat?.SetCounter(Sword, key, value);

    public int AddCounter(string key, int delta) => Combat?.AddCounter(Sword, key, delta) ?? 0;

    /// <summary>A copy of this context flagged as an inherited (Kusanagi) invocation.</summary>
    public SwordContext AsInherited() => new()
    {
        Player = Player, Sword = Sword, Level = Level, IsInherited = true, IsCurrent = IsCurrent,
        IsPresent = IsPresent, Combat = Combat
    };

    public override string ToString() =>
        $"SwordContext({Sword} lv{Level}{(IsInherited ? " inherited" : "")}{(IsCurrent ? " current" : "")})";
}
