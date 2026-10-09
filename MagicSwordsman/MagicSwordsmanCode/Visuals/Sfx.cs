using Godot;
using MagicSwordsman.MagicSwordsmanCode.Swords;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Presentation only: Ensifer's own sound effects (user request 2026-10-09 "고유 사운드"). The clips in
/// <c>audio/sfx/*.wav</c> are synthesised by tools/gen_sfx.py (no recorded material). They play through plain Godot
/// AudioStreamPlayers on the game's "SFX" bus — the bus whose volume the game's SFX slider sets
/// (NSfxVolumeSlider -> NDebugAudioManager.SetSfxAudioVolume) — so the player's SFX volume applies. Volumes are kept
/// modest (about -9 dB). Every call swallows its errors and a missing clip is silent.
/// UNVERIFIED in game: the SFX bus exists in the shipped bus layout (falls back to Master), loudness against the
/// game's FMOD sounds.
/// </summary>
public static class Sfx
{
    private static readonly Dictionary<string, AudioStream?> Cache = new();
    private static readonly Dictionary<string, ulong> LastPlayed = new();
    private static readonly StringName SfxBus = new("SFX");
    private static readonly StringName MasterBus = new("Master");
    private static int _playing;

    /// <summary>Plays audio/sfx/&lt;name&gt;.wav once. Repeats of the same clip within 45 ms are dropped.</summary>
    public static void Play(string name, float volumeDb = -9f, float pitch = 1f)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null) return;
            var now = Time.GetTicksMsec();
            if (LastPlayed.TryGetValue(name, out var last) && now - last < 45) return;
            if (_playing >= 12) return; // never pile up (multi-hit attacks)
            var stream = Load(name);
            if (stream == null) return;
            LastPlayed[name] = now;

            var player = new AudioStreamPlayer
            {
                Stream = stream, VolumeDb = volumeDb, PitchScale = Math.Clamp(pitch, 0.5f, 2f),
                Bus = AudioServer.GetBusIndex(SfxBus) >= 0 ? SfxBus : MasterBus, Name = "MagicSwordsSfx",
            };
            _playing++;
            player.Finished += () =>
            {
                _playing = Math.Max(0, _playing - 1);
                player.QueueFree();
            };
            tree.Root.AddChild(player);
            player.Play();
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[Sfx] {name}: {e.Message}");
        }
    }

    /// <summary>A sword-coloured pitch for the shared clips (switch / whoosh / impact): heavy swords lower, light ones higher.</summary>
    public static float PitchOf(SwordId sword) => sword switch
    {
        SwordId.Gram => 0.86f,
        SwordId.Caladbolg => 0.9f,
        SwordId.Durandal => 0.94f,
        SwordId.Tyrfing => 0.97f,
        SwordId.Dainsleif => 0.88f,
        SwordId.Onimaru => 0.92f,
        SwordId.Ganjiang => 1.0f,
        SwordId.Kusanagi => 1.08f,
        SwordId.Moye => 1.12f,
        SwordId.Skofnung => 1.15f,
        SwordId.ClaiomhSolais => 1.2f,
        _ => 1f,
    };

    /// <summary>The sword's own summon sound (audio/sfx/summon_&lt;sword&gt;.wav).</summary>
    public static void Summon(SwordId sword) => Play("summon_" + sword.ToString().ToLowerInvariant(), -8f);

    public static void Switch(SwordId sword) => Play("switch", -11f, PitchOf(sword));

    public static void Whoosh(SwordId sword) => Play("whoosh", -12f, PitchOf(sword) * (0.95f + 0.1f * Random.Shared.NextSingle()));

    public static void Impact(SwordId? sword) =>
        Play("impact", -11f, (sword is { } s ? PitchOf(s) : 1f) * (0.94f + 0.12f * Random.Shared.NextSingle()));

    /// <summary>The palm magic circle of a skill / power cast.</summary>
    public static void Circle() => Play("circle", -14f, 0.96f + 0.08f * Random.Shared.NextSingle());

    public static void BossKill() => Play("bosskill", -7f);

    private static AudioStream? Load(string name)
    {
        if (Cache.TryGetValue(name, out var s)) return s;
        var path = $"{MainFile.ResPath}/audio/sfx/{name}.wav";
        s = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
        if (s == null) MainFile.Logger.Warn($"[Sfx] missing {path}");
        Cache[name] = s;
        return s;
    }
}
