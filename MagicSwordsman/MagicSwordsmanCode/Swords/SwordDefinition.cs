namespace MagicSwordsman.MagicSwordsmanCode.Swords;

/// <summary>
/// Static, framework-owned facts about a sword (slot cost, pairing, special rules).
/// Content (cards, curses, numbers) lives in the sword's <see cref="SwordBehavior"/> instead,
/// so content agents never need to edit this file.
/// </summary>
public sealed class SwordDefinition
{
    public required SwordId Id { get; init; }

    /// <summary>How many Mangeomchong slots this sword occupies. Ganjiang 1 + Moye 1 = the pair's 2 slots.</summary>
    public int SlotCost { get; init; } = 1;

    /// <summary>
    /// Swords that are always acquired / lost together with this one (Ganjiang &lt;-&gt; Moye).
    /// </summary>
    public IReadOnlyList<SwordId> Partners { get; init; } = [];

    /// <summary>
    /// The sword whose saved upgrade level this sword uses. Moye shares Ganjiang's level so the pair is
    /// upgraded as one unit at the rest site ("검 단위 강화"). Defaults to the sword itself.
    /// </summary>
    public SwordId? LevelOwnerOverride { get; init; }

    public SwordId LevelOwner => LevelOwnerOverride ?? Id;

    /// <summary>Spec §4: Gram never leaves on a shatter — it returns to level 0 ("부서진 그람").</summary>
    public bool CanBeLost { get; init; } = true;

    /// <summary>Spec §3: Onimaru comes out on its own at combat start. That is NOT a summon (no Mangeomchong bonus).</summary>
    public bool EmergesAtCombatStart { get; init; }

    /// <summary>
    /// Whether this sword can be offered by acquisition events / random rolls.
    /// False for the starting sword (Gram) and for Moye (it comes with Ganjiang).
    /// </summary>
    public bool OfferedByAcquisition { get; init; } = true;

    /// <summary>True for the sword that represents a pair at the rest site / in acquisition offers.</summary>
    public bool IsPairLeader => Partners.Count > 0 && LevelOwner == Id;
}
