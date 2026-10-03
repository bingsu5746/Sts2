using MagicSwordsman.MagicSwordsmanCode.Swords;

namespace MagicSwordsman.MagicSwordsmanCode.Combat;

/// <summary>
/// Per-player, per-combat sword state. Created at combat start (Mangeomchong.BeforeCombatStart) and
/// discarded after combat. Not saved: the game restarts a combat from its beginning when a run is loaded.
/// </summary>
public sealed class SwordCombatState
{
    /// <summary>Spec §3: no current sword at combat start.</summary>
    public SwordId? Current { get; internal set; }

    /// <summary>The sword that was current before <see cref="Current"/> (Kusanagi inherits from this one).</summary>
    public SwordId? Previous { get; internal set; }

    /// <summary>Swords floating around the player: summoned this combat, or emerged on their own (Onimaru).</summary>
    public HashSet<SwordId> Present { get; } = new();

    /// <summary>Swords summoned out of Mangeomchong this combat (summon = once per sword per combat).</summary>
    public HashSet<SwordId> Summoned { get; } = new();

    /// <summary>Mangeomchong's "first summon of the combat: Block 3 + draw 1" already used.</summary>
    public bool FirstSummonBonusUsed { get; internal set; }

    /// <summary>Times another sword switched INTO Kusanagi this combat (spec: max 2 per combat).</summary>
    public int KusanagiSwitchIns { get; internal set; }

    /// <summary>Total number of current-sword changes this combat.</summary>
    public int SwitchCount { get; internal set; }

    /// <summary>Player turn number (PlayerCombatState.TurnNumber) of the most recent switch (0 = none yet).</summary>
    public int LastSwitchTurn { get; internal set; }

    /// <summary>Number of current-sword changes during <see cref="LastSwitchTurn"/>.</summary>
    public int SwitchesInLastSwitchTurn { get; internal set; }

    /// <summary>Current-sword changes during the given player turn (see SwordCombat.SwitchesThisTurn).</summary>
    public int SwitchesInTurn(int turn) => turn > 0 && turn == LastSwitchTurn ? SwitchesInLastSwitchTurn : 0;

    /// <summary>Swords sent back to Mangeomchong for the rest of this combat (Kusanagi exhausted).</summary>
    public HashSet<SwordId> ReturnedToVault { get; } = new();

    private readonly Dictionary<string, int> _counters = new();

    /// <summary>Free-form per-sword counters, e.g. Claiomh Solais "light", Skofnung wound bookkeeping.</summary>
    public int GetCounter(SwordId sword, string key) => _counters.GetValueOrDefault(Key(sword, key));

    public void SetCounter(SwordId sword, string key, int value) => _counters[Key(sword, key)] = value;

    public int AddCounter(SwordId sword, string key, int delta)
    {
        var v = GetCounter(sword, key) + delta;
        SetCounter(sword, key, v);
        return v;
    }

    private static string Key(SwordId sword, string key) => $"{(int)sword}:{key}";
}
