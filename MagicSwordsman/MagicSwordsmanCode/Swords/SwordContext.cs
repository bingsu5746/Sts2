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
    /// The level that applies to a card's numbers: the sword's level for sword cards, 0 for shared (colourless) cards
    /// and anything else. Shared cards still carry the current sword's effect, but upgrading a sword never makes them
    /// stronger (user decision 2026-10-09, replacing content doc §6 "공용 카드는 검 효과를 운반" for the level part).
    /// </summary>
    public int LevelFor(MegaCrit.Sts2.Core.Models.CardModel? card) =>
        card is MagicSwordsman.MagicSwordsmanCode.Cards.MagicSwordCard { Sword: not null } ? Level : 0;

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

    /// <summary>
    /// True while the game is only PREVIEWING numbers of a sword card in hand whose sword is not current yet
    /// (the card would switch to this sword when played). Behaviors normally do not need to care: the same
    /// Modify* result is used for the preview and for the real play. Never change state when this is true.
    /// </summary>
    public bool IsPreview { get; init; }

    /// <summary>
    /// The sword that was current before <see cref="Sword"/> became current (Kusanagi inherits from it).
    /// Normally <c>Combat.Previous</c>; during a preview it is the sword that is current right now.
    /// </summary>
    public SwordId? PreviousSword { get; init; }

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
        IsPresent = IsPresent, Combat = Combat, IsPreview = IsPreview, PreviousSword = PreviousSword
    };

    public override string ToString() =>
        $"SwordContext({Sword} lv{Level}{(IsInherited ? " inherited" : "")}{(IsCurrent ? " current" : "")}{(IsPreview ? " preview" : "")})";
}
