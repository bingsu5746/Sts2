using Godot;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Presentation only (no game state): the swords floating around the Magic Swordsman and Mangeomchong standing behind
/// them. Everything here is derived from <see cref="SwordCombatState"/> via <see cref="Sync"/>, so it can be called
/// from anywhere, any number of times, and never changes gameplay. Every entry point swallows exceptions — a visual
/// bug must never break a combat.
///
/// Layout (player faces right, user sketch 2026-10-04): upright swords left of / above / right of the character; the
/// current sword takes the right slot. New swords fly out of the character; a sword that leaves shrinks back into it.
/// No tomb object (removed by user decision). Art: drop <c>images/swords/&lt;sword id lowercase&gt;.png</c>
/// (blade pointing up) to replace the drawn placeholder shapes. TODO(art): real art, VFX on summon.
/// Draw order (bug report 2026-10-10 "현재 장착 검이 아닌 애들도 주위에 떠돌아다녀야 하는데 표현이 안 되고 있어"): the
/// rig sits under the creature node, which lives in NCombatRoom's SceneContainer (z_index -10, together with the
/// room background). The non-current swords used to get z_index -1, i.e. absolute -11: drawn BELOW the background,
/// so they were never visible. Now no sword node has a negative z: the rig is moved to be the creature's first child
/// (before NCreature.Visuals, like the game's own NSovereignBladeVfx does to go behind the Regent) so z 0 draws
/// behind the body but in front of the room, z 1 in front of the body, z 2 for a sword in flight.
/// UNVERIFIED in game: node offsets relative to the creature node.
/// </summary>
public static partial class SwordVisuals
{
    private sealed class Rig
    {
        public required Node2D Root;
        public required Player Player;
        /// <summary>The death choreography took the swords over: nothing else moves them any more.</summary>
        public bool Dead;
        public readonly Dictionary<SwordId, Node2D> Swords = new();
        /// <summary>Where each sword rests (set by Layout): strikes start from here, never from the summon point.</summary>
        public readonly Dictionary<SwordId, Vector2> Slots = new();
        /// <summary>The running layout tween of each sword (killed when a strike takes over the sword).</summary>
        public readonly Dictionary<SwordId, Tween> Moves = new();
        /// <summary>
        /// Which idle slot each non-current sword holds. Kept from one layout to the next so a switch only moves the two
        /// swords that trade places (bug report 2026-10-15 "환검 쓰면 검들의 위치가 이상하게 꼬이는 버그").
        /// </summary>
        public readonly Dictionary<SwordId, int> SlotIndex = new();
        public SwordId? Current;
    }

    private static readonly Dictionary<Player, Rig> Rigs = new();

    /// <summary>On-screen height (px, before the rig's per-sword scale) of a sword drawn from images/swords art.</summary>
    private const float SwordSpriteHeight = 190f;

    // Layout (사용자 스케치 2026-10-04, revised 2026-10-10): swords float upright around the character — left, behind his
    // body (partly hidden by it), right.
    // The current sword always takes the right slot (in front, toward the enemy). The character only gestures.
    // Creature space: feet at y 0, head top about y -390, body about x -128..128 (scenes/magic_swordsman_combat.tscn).
    private static readonly Vector2 SpawnPos = new(0, -220);      // swords appear from / vanish into the character
    private static readonly Vector2 CurrentPos = new(160, -215);  // right of the character, in front of him
    private static readonly Vector2[] IdleSlots =
    {
        new(-190, -235),  // left of the character (clear of his left arm)
        new(-70, -290),   // behind his body, partly hidden by it (feedback 2026-10-10: not above the head)
        new(55, -305),    // behind his body on the other side (sword cap raised by relics)
        new(-205, -420),  // left and higher
        // more swords out at once (환검 / summon cards can bring out every owned sword): an outer ring, so no two
        // swords ever share a slot (slot 4 used to wrap onto slot 0 and draw two swords on top of each other)
        new(-320, -320),
        new(190, -440),
        new(-330, -470),
        new(-75, -480),
        new(-300, -175),
        new(300, -360),
    };

    /// <summary>Idle slot <paramref name="i"/>; past the table, a small step off the wrapped slot (never exactly on it).</summary>
    private static Vector2 IdleSlot(int i) =>
        IdleSlots[i % IdleSlots.Length] + new Vector2(-24f, -20f) * (i / IdleSlots.Length);

    private const int ZBack = 0, ZFront = 1, ZFlying = 2;

    // ------------------------------------------------------------------ entry points

    /// <summary>Reconciles the floating swords with the combat state (present swords, current sword).</summary>
    public static void Sync(Player player)
    {
        try { SyncInner(player); }
        catch (Exception e) { MainFile.Logger.Warn($"[SwordVisuals] Sync failed: {e.Message}"); }
        OnimaruStanceHud.Refresh(player); // stance tag under the health bar (never throws)
    }

    /// <summary>Removed by user decision (2026-10-04): no tomb object / door animation. Kept as a no-op for callers.</summary>
    public static void RattleTomb(Player player)
    {
    }

    /// <summary>
    /// Plays one of the character's own AnimationPlayer clips (e.g. "Summon_Gram", "Block_Cross"); MotionDirector
    /// decides which. Falls back to the base clip ("Summon_X" -> "Summon") if a variant is missing.
    /// <paramref name="onlyIfIdle"/>: skip it while another clip (an attack, a cast...) is playing.
    /// Presentation only; silently does nothing if the clip or the creature node is missing.
    /// </summary>
    public static void PlayMotion(Player player, string clip, bool onlyIfIdle = false)
    {
        try
        {
            var anim = MainAnimationPlayer(player);
            if (anim == null) return;
            if (!anim.HasAnimation(clip) && clip.Contains('_')) clip = clip[..clip.IndexOf('_')];
            if (!anim.HasAnimation(clip)) return;
            if (onlyIfIdle && anim.IsPlaying() && anim.CurrentAnimation != "idle") return;
            if (anim.CurrentAnimation == clip) anim.Stop(); // restart the same clip; otherwise cross-fade into it
            anim.Play(clip, 0.12);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordVisuals] PlayMotion {clip} failed: {e.Message}");
        }
    }

    /// <summary>Name of the clip the character is playing ("" when none); null outside combat. Never throws.</summary>
    public static string? CurrentClip(Player player)
    {
        try { return MainAnimationPlayer(player)?.CurrentAnimation.ToString(); }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// The character scene's main AnimationPlayer (a direct child named "AnimationPlayer"; the scene also has a
    /// "HandSpin" player that only spins the palm circles). Hooks MotionDirector on first use.
    /// </summary>
    private static AnimationPlayer? MainAnimationPlayer(Player player)
    {
        var node = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        var visuals = node?.Visuals;
        if (visuals == null) return null;
        var anim = visuals.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") ?? FindChild<AnimationPlayer>(visuals);
        if (anim != null) MotionDirector.Hook(player, anim);
        return anim;
    }

    /// <summary>
    /// Global point a sword's speech bubble should start from (Dialogue/SwordTalk.cs): just above the floating blade,
    /// or the "above the head" slot when the sword is not out. Null outside combat.
    /// </summary>
    public static Vector2? SpeechAnchor(Player player, SwordId sword)
    {
        try
        {
            if (Rigs.TryGetValue(player, out var rig) && GodotObject.IsInstanceValid(rig.Root) &&
                rig.Swords.TryGetValue(sword, out var node) && GodotObject.IsInstanceValid(node))
                return SpeechPointOf(node) ?? node.ToGlobal(new Vector2(0, -SwordSpriteHeight * 0.5f));
            var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
            return creatureNode == null ? null : creatureNode.GetGlobalTransform() * IdleSlots[1];
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Combat over: forget the rig (its nodes die with the combat room).</summary>
    public static void Clear(Player player)
    {
        Rigs.Remove(player);
        MotionDirector.Clear(player);
        LastGuard.Remove(player.NetId);
        // per-node bookkeeping of nodes that died with the combat room
        foreach (var dict in new[] { LastStrike, GuardAt, UnionBusyUntil })
            foreach (var dead in dict.Keys.Where(n => !GodotObject.IsInstanceValid(n)).ToList())
                dict.Remove(dead);
    }

    // ------------------------------------------------------------------ internals

    private static void SyncInner(Player player)
    {
        var state = SwordCombat.Get(player);
        if (state == null) return;
        var rig = GetRig(player, create: state.Present.Count > 0 || state.ReturnedToVault.Count > 0);
        if (rig == null || rig.Dead) return;

        // swords that left -> shrink back into the character
        foreach (var gone in rig.Swords.Keys.Where(s => !state.Present.Contains(s)).ToList())
        {
            var node = rig.Swords[gone];
            rig.Swords.Remove(gone);
            rig.Slots.Remove(gone);
            rig.SlotIndex.Remove(gone);
            // whatever was moving it (layout, guard, strike) would fight the shrink and leave it hanging mid-air
            if (rig.Moves.Remove(gone, out var mv) && GodotObject.IsInstanceValid(mv)) mv.Kill();
            if (!GodotObject.IsInstanceValid(node)) continue;
            var t = node.CreateTween().SetParallel();
            t.TweenProperty(node, "position", SpawnPos, 0.4).SetTrans(Tween.TransitionType.Quad);
            t.TweenProperty(node, "modulate:a", 0f, 0.4);
            t.Chain().TweenCallback(Callable.From(node.QueueFree));
        }

        // level changed mid-combat (Hone, Temper...) -> redraw that sword in place
        foreach (var (sw, node) in rig.Swords.ToList())
        {
            if (!node.HasMeta("level") || (int)node.GetMeta("level") == SwordCombat.LevelOf(player, sw)) continue;
            var fresh = CreateSword(sw, player);
            rig.Root.AddChild(fresh);
            fresh.Position = node.Position;
            fresh.Scale = node.Scale;
            fresh.ZIndex = node.ZIndex;
            fresh.Modulate = node.Modulate;
            var up = (int)fresh.GetMeta("level", 0) > (int)node.GetMeta("level");
            node.QueueFree();
            rig.Swords[sw] = fresh;
            if (up) SwordFx.LevelUp(rig.Root, fresh.Position, sw, (int)fresh.GetMeta("level", 0));
        }

        // new swords -> fly out of the character
        var appeared = new List<SwordId>();
        foreach (var sword in state.Present.Where(s => !rig.Swords.ContainsKey(s)))
        {
            var node = CreateSword(sword, player);
            rig.Root.AddChild(node);
            node.Position = SpawnPos;
            node.Scale = new Vector2(0.85f, 0.85f);
            node.Modulate = new Color(1, 1, 1, 0); // SwordFx.Summon sets the real starting pose
            rig.Swords[sword] = node;
            appeared.Add(sword);
        }

        var previous = rig.Current;
        rig.Current = state.Current;
        AssignSlots(rig, previous);
        Layout(rig, appeared);
        ApplyPalmCircles(player, state.Current);
        PlaySyncFx(rig, appeared, previous);
    }

    /// <summary>
    /// Per-sword summon / switch effects and sounds (SwordFx). Runs right after Layout: the summon effect sets each new
    /// sword's starting pose, which the layout tween (started next frame) then carries to its slot.
    /// </summary>
    private static void PlaySyncFx(Rig rig, List<SwordId> fresh, SwordId? previous)
    {
        foreach (var sword in fresh)
        {
            var node = rig.Swords[sword];
            node.SetMeta(BornMeta, Time.GetTicksMsec());
            SwordFx.Summon(rig.Root, node, sword, rig.Slots.TryGetValue(sword, out var slot) ? slot : node.Position);
        }
        // a switch to a sword that is appearing right now is already covered by its summon effect
        if (rig.Current is not { } current || current == previous || !rig.Swords.TryGetValue(current, out var cur)) return;
        if (cur.HasMeta(BornMeta) && Time.GetTicksMsec() - (ulong)cur.GetMeta(BornMeta) < 700) return;
        SwordFx.Switch(rig.Root, current, CurrentPos);
    }

    private const string BornMeta = "ms_fx_born";

    /// <summary>
    /// Keeps each idle sword in the slot it already holds (bug report 2026-10-15 "환검 쓰면 검들의 위치가 이상하게 꼬이는
    /// 버그"). The old layout handed out the idle slots in sword order every time, so one switch re-seated every sword:
    /// several flew across each other at once, and with five or more idle swords two of them shared slot 0. Now the new
    /// current sword leaves its slot, the previous current sword takes exactly that slot (the two trade places), and a
    /// newly summoned sword takes the first free slot.
    /// </summary>
    private static void AssignSlots(Rig rig, SwordId? previous)
    {
        foreach (var s in rig.SlotIndex.Keys.Where(s => !rig.Swords.ContainsKey(s)).ToList()) rig.SlotIndex.Remove(s);
        int? vacated = null;
        if (rig.Current is { } cur && rig.SlotIndex.Remove(cur, out var v)) vacated = v;
        if (previous is { } prev && prev != rig.Current && rig.Swords.ContainsKey(prev) && !rig.SlotIndex.ContainsKey(prev) &&
            vacated is { } vac && !rig.SlotIndex.ContainsValue(vac))
            rig.SlotIndex[prev] = vac;
        FillSlots(rig);
    }

    /// <summary>Every non-current sword without a slot takes the lowest free one (in sword order).</summary>
    private static void FillSlots(Rig rig)
    {
        if (rig.Current is { } cur) rig.SlotIndex.Remove(cur);
        foreach (var sword in rig.Swords.Keys.OrderBy(k => (int)k))
        {
            if (sword == rig.Current || rig.SlotIndex.ContainsKey(sword)) continue;
            var i = 0;
            while (rig.SlotIndex.ContainsValue(i)) i++;
            rig.SlotIndex[sword] = i;
        }
    }

    /// <summary>
    /// Sends every sword to its resting place: the current sword to the front-right slot, the others to the idle slot
    /// they hold (<see cref="AssignSlots"/>). Onimaru floats in a slot like every other sword; its stance only tilts it
    /// a little (<see cref="OnimaruTilt"/>).
    /// <paramref name="fresh"/>: swords summoned this very sync rise slowly to their place (SwordFx.Summon).
    /// <paramref name="only"/>: a choreography (strike, guard, union) handing one sword back — the others are left
    /// alone so a sword still in flight is not yanked home. Handing back also ends that sword's "in flight" marks, so the
    /// next full layout moves it again.
    /// </summary>
    private static void Layout(Rig rig, ICollection<SwordId>? fresh = null, SwordId? only = null)
    {
        if (rig.Dead) return;
        FillSlots(rig);
        var now = Time.GetTicksMsec();
        foreach (var (sword, node) in rig.Swords.OrderBy(kv => (int)kv.Key))
        {
            if (!GodotObject.IsInstanceValid(node)) continue;
            var isCurrent = rig.Current == sword;
            var pos = isCurrent ? CurrentPos : IdleSlot(rig.SlotIndex.TryGetValue(sword, out var si) ? si : 0);
            var tilt = sword == SwordId.Onimaru ? OnimaruTilt(rig.Player) : 0f;
            rig.Slots[sword] = pos;
            if (only != null && only != sword) continue;
            if (only != null)
            {
                GuardAt.Remove(node);
                if (node.HasMeta(StrikeUntilMeta)) node.SetMeta(StrikeUntilMeta, 0UL);
            }
            // a full re-layout (Sync) leaves a sword in the middle of a choreography alone: its own end calls
            // Layout(only: it), which reads the slot computed here
            if (only == null && (IsUnionBusy(node) || InFlight(node))) continue;
            if (rig.Moves.TryGetValue(sword, out var old) && GodotObject.IsInstanceValid(old)) old.Kill();
            // summoned a moment ago and still rising (e.g. 환검 summoning a sword and switching to it right away): keep
            // the slow summon glide for the rest of the way instead of snapping across in 0.4 s
            var isFresh = fresh?.Contains(sword) == true;
            var age = node.HasMeta(BornMeta) ? now - (ulong)node.GetMeta(BornMeta) : ulong.MaxValue;
            var born = isFresh || age < 700;
            var dur = isFresh ? 0.85 : born ? Math.Max(0.45, 0.85 - age / 1000.0) : 0.4;
            var t = node.CreateTween().SetParallel();
            rig.Moves[sword] = t;
            if (born)
                t.TweenProperty(node, "position", pos, dur).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            else
                t.TweenProperty(node, "position", pos, dur).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            var scale = isCurrent ? 1.15f : 0.85f;
            t.TweenProperty(node, "scale", new Vector2(scale, scale), dur * 0.8);
            t.TweenProperty(node, "rotation", tilt, dur * 0.8).SetTrans(Tween.TransitionType.Sine);
            t.TweenProperty(node, "modulate", isCurrent ? Colors.White : new Color(0.82f, 0.82f, 0.88f, 0.95f), dur * 0.8);
            node.ZIndex = isCurrent ? ZFront : ZBack;
        }
    }

    /// <summary>
    /// Onimaru's stance shows only as a slight lean of the floating katana (user request 2026-10-15 "오니마루도 다른 검들처럼
    /// 부유하고 있게 해줘. 허리 쪽에 있게 하지 말고": no more hip / overhead / level / orbiting poses — it floats in a slot and
    /// bobs like the others). 거합 leans back like a blade about to be drawn, 베기 leans its point toward the enemy, 난무 a
    /// restless slant, 퇴마 stands straight, 호위 barely tilts back. Art points up, so this is the angle of the tip from
    /// "up", clockwise. A stance change re-runs Layout (OnimaruAttack.SetKind calls Sync), which eases it to the new lean.
    /// </summary>
    private static float OnimaruTilt(Player player)
    {
        OnimaruKind kind;
        try { kind = OnimaruAttack.GetKind(player); }
        catch (Exception) { kind = OnimaruKind.Iai; }
        return kind switch
        {
            OnimaruKind.Goei => -0.05f,
            OnimaruKind.Taima => 0f,
            OnimaruKind.Giri => 0.2f,
            OnimaruKind.Ranbu => 0.11f,
            _ => -0.15f, // Iai
        };
    }

    private static Rig? GetRig(Player player, bool create)
    {
        if (Rigs.TryGetValue(player, out var rig) && GodotObject.IsInstanceValid(rig.Root)) return rig;
        Rigs.Remove(player);
        if (!create) return null;

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        if (creatureNode == null) return null;

        var root = new Node2D { Name = "MagicSwordsRig" };
        creatureNode.AddChild(root);
        creatureNode.MoveChild(root, 0); // before NCreature.Visuals: z 0 = behind the body, never behind the room

        rig = new Rig { Root = root, Player = player };
        Rigs[player] = rig;
        MainAnimationPlayer(player);
        StartIdleSpecials(rig); // now and then a sword does something on its own (SwordVisuals.Idle.cs)
        return rig;
    }

    private static T? FindChild<T>(Node? node) where T : Node
    {
        if (node == null) return null;
        if (node is T found) return found;
        foreach (var child in node.GetChildren())
        {
            var r = FindChild<T>(child);
            if (r != null) return r;
        }
        return null;
    }

    // ------------------------------------------------------------------ placeholder art

    private static readonly Color AuraColor = new(0.62f, 0.4f, 1f);

    /// <param name="spectral">a temporary copy (guard / death choreography): no tooltip box, no idle float</param>
    private static Node2D CreateSword(SwordId sword, Player player, bool spectral = false)
    {
        var holder = new Node2D { Name = $"Sword_{sword}" };
        var blade = new Node2D { Name = "Blade" };
        var id = sword.ToString().ToLowerInvariant();
        var texPath = $"{MainFile.ResPath}/images/swords/{id}.png";
        Texture2D? bladeTex = null;
        var bladeScale = 1f;
        if (ResourceLoader.Exists(texPath))
        {
            var tex = GD.Load<Texture2D>(texPath);
            // Art is high-res (about 1000 px tall); every sword is shown at the same on-screen height.
            var scale = SwordSpriteHeight / Math.Max(1f, tex.GetHeight());
            bladeTex = tex;
            bladeScale = scale;

            // level look, aura, the broken Gram, Claíomh Solais' Light (SwordVisuals.Level.cs)
            var level = Math.Clamp(SwordCombat.LevelOf(player, sword), 0, 5);
            holder.SetMeta("level", level);
            BuildSwordArt(blade, sword, level, tex, scale, player);
        }
        else
        {
            blade.AddChild(new Polygon2D
            {
                Color = ColorOf(sword),
                Polygon = new[] { new Vector2(0, -70), new Vector2(7, -55), new Vector2(6, 48), new Vector2(-6, 48), new Vector2(-7, -55) },
            });
        }

        if (spectral)
        {
            holder.AddChild(blade);
            return holder;
        }

        // hover boxes fitted to the drawn sword (move with the bob): name, level and effect in the game's tooltip
        // (SwordVisuals.Hover.cs)
        AttachHover(holder, blade, sword, player, bladeTex, bladeScale);

        holder.AddChild(blade);

        StartIdleFloat(blade, sword);
        return holder;
    }

    /// <summary>
    /// How each sword hangs in the air (user request 2026-10-10): the heavy ones (Gram, Durandal, Caladbolg) bob slowly
    /// and barely sway; the light ones (Kusanagi, Skofnung, Claíomh Solais, Ganjiang, Moye) bob quicker, sway more and
    /// flutter (a fast, tiny skew, like a leaf in a draught). Period (s per half bob), bob height, sway, drift, flutter.
    /// </summary>
    private static (double Period, float Bob, float Sway, float Drift, float Flutter) FloatOf(SwordId sword) => sword switch
    {
        SwordId.Gram => (2.0, 9f, 0.03f, 5f, 0f),
        SwordId.Durandal => (2.15, 10f, 0.025f, 4f, 0f),
        SwordId.Caladbolg => (2.4, 8f, 0.02f, 4f, 0f),
        SwordId.Tyrfing => (1.6, 12f, 0.05f, 7f, 0f),
        SwordId.Dainsleif => (1.75, 11f, 0.045f, 6f, 0f),
        SwordId.Onimaru => (1.5, 10f, 0.04f, 6f, 0f),
        SwordId.Kusanagi => (1.1, 14f, 0.08f, 10f, 0.035f),
        SwordId.Skofnung => (1.3, 16f, 0.06f, 9f, 0.025f),
        SwordId.ClaiomhSolais => (1.2, 13f, 0.06f, 8f, 0.02f),
        SwordId.Ganjiang => (1.25, 12f, 0.07f, 8f, 0.02f),
        SwordId.Moye => (1.15, 12f, 0.07f, 8f, 0.025f),
        _ => (1.5, 12f, 0.05f, 7f, 0f),
    };

    /// <summary>Slow bob + a slight sway + a sideways drift on a third rhythm (+ the light swords' flutter).</summary>
    private static void StartIdleFloat(Node2D blade, SwordId sword)
    {
        var (period, bobH, swayA, driftA, flutter) = FloatOf(sword);
        const Tween.TransitionType sine = Tween.TransitionType.Sine;
        const Tween.EaseType io = Tween.EaseType.InOut;
        var bob = blade.CreateTween().SetLoops();
        bob.TweenProperty(blade, "position:y", -bobH, period).SetTrans(sine).SetEase(io);
        bob.TweenProperty(blade, "position:y", 0f, period).SetTrans(sine).SetEase(io);
        var sway = blade.CreateTween().SetLoops();
        sway.TweenProperty(blade, "rotation", swayA, period * 1.3).SetTrans(sine).SetEase(io);
        sway.TweenProperty(blade, "rotation", -swayA, period * 1.3).SetTrans(sine).SetEase(io);
        // so the swords seem to hang in the air rather than on rails
        var drift = blade.CreateTween().SetLoops();
        drift.TweenProperty(blade, "position:x", driftA, period * 1.9).SetTrans(sine).SetEase(io);
        drift.TweenProperty(blade, "position:x", -driftA, period * 1.9).SetTrans(sine).SetEase(io);
        if (flutter <= 0f) return;
        var flut = blade.CreateTween().SetLoops();
        flut.TweenProperty(blade, "skew", flutter, 0.21).SetTrans(sine).SetEase(io);
        flut.TweenProperty(blade, "skew", -flutter * 0.6f, 0.17).SetTrans(sine).SetEase(io);
        flut.TweenProperty(blade, "skew", flutter * 0.4f, 0.26).SetTrans(sine).SetEase(io);
        flut.TweenProperty(blade, "skew", -flutter, 0.19).SetTrans(sine).SetEase(io);
    }

    private static float PointAt(Vector2 from, Vector2 to)
    {
        var d = to - from;
        return Mathf.Atan2(d.Y, d.X) + Mathf.Pi / 2; // art points up
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) =>
        (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;

    /// <summary>
    /// Fading copies of the sword left behind while it flies. Scales with the sword's level (user request 2026-10-15):
    /// at 0 a few faint, greyish, short-lived copies; at 5 a dense, bright, longer trail shedding sparks.
    /// </summary>
    private static void Trail(Node2D node, double duration, Color col)
    {
        if (node.GetParent() is not Node2D parent) return;
        var level = node.HasMeta("level") ? Math.Clamp((int)node.GetMeta("level"), 0, 5) : 2;
        var every = 0.035 - 0.003 * level;           // 0.035 s .. 0.02 s between copies
        var alpha = 0.22f + 0.08f * level;           // 0.22 .. 0.62
        var life = 0.14 + 0.035 * level;             // 0.14 s .. 0.315 s
        var tint = level >= 3 ? col.Lightened(0.3f + 0.05f * (level - 3)) : col.Lerp(new Color(0.7f, 0.7f, 0.75f), 0.4f - 0.15f * level);
        var timer = new Godot.Timer { WaitTime = every, Autostart = true };
        var elapsed = 0.0;
        var n = 0;
        timer.Timeout += () =>
        {
            elapsed += timer.WaitTime;
            if (elapsed > duration || !GodotObject.IsInstanceValid(node)) { timer.QueueFree(); return; }
            if (BladeCopy(node) is not { } ghost) return;
            ghost.ZIndex = Math.Max(ZBack, node.ZIndex - 1);
            ghost.Modulate = new Color(tint, alpha);
            ghost.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            parent.AddChild(ghost);
            var fade = ghost.CreateTween();
            fade.TweenProperty(ghost, "modulate:a", 0f, life);
            fade.TweenCallback(Callable.From(ghost.QueueFree));
            // level 4+: sparks shed along the trail (every other copy)
            if (level >= 4 && n++ % 2 == 0) SwordFx.TrailSpark(parent, node.Position, col, level);
        };
        parent.AddChild(timer);
    }

    private static readonly Color PalmDefault = new(0.85f, 0.65f, 1f);

    /// <summary>
    /// Ensifer's palm circles take the current sword's colour and centre glyph (images/vfx/glyph_&lt;sword&gt;.png,
    /// tools/gen_hand_glyphs.py). The clips only animate their size and alpha (HandL/HandR); colour lives on the
    /// HandX/Tint node below them, so the two never fight.
    /// </summary>
    private static void ApplyPalmCircles(Player player, SwordId? current)
    {
        var visuals = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals;
        if (visuals == null) return;
        var color = current is { } c ? ColorOf(c).Lightened(0.2f) : PalmDefault;
        Texture2D? glyph = null;
        if (current is { } cs)
        {
            var path = $"{MainFile.ResPath}/images/vfx/glyph_{cs.ToString().ToLowerInvariant()}.png";
            if (ResourceLoader.Exists(path)) glyph = GD.Load<Texture2D>(path);
        }
        foreach (var hand in new[] { "Visuals/Rig/UpperL/ForeL/HandL/Tint", "Visuals/Rig/UpperR/ForeR/HandR/Tint" })
        {
            if (visuals.GetNodeOrNull<Node2D>(hand) is not { } tint) continue;
            tint.Modulate = color;
            if (glyph != null && tint.GetNodeOrNull<Sprite2D>("GlyphTilt/Glyph") is { } g) g.Texture = glyph;
        }
    }

    private static void ShowTip(Control owner, SwordId sword, Player player)
    {
        try
        {
            var level = SwordCombat.LevelOf(player, sword);
            var desc = $"{Text(Events.SwordLore.StageName(sword, level))} ({level})\n{Swords.SwordEffectText.Effect(sword, player, level)}";
            var affinity = Dialogue.SwordAffinity.TipLine(player, sword);
            if (affinity.Length > 0) desc += "\n" + affinity;
            desc += Cards.Union.UnionCatalog.SwordTipLine(player, sword); // 【조합】 cards of this sword
            MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet.Remove(owner);
            // alignment None leaves the set at the screen's top-left: PlaceTip puts it beside the sword
            PlaceTip(owner, MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet.CreateAndShow(owner,
                new MegaCrit.Sts2.Core.HoverTips.HoverTip(Events.SwordLore.Name(sword), desc)));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordVisuals] tooltip: {e.Message}");
        }
    }

    private static void HideTip(Control owner)
    {
        Tips.Remove(owner);
        try { MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet.Remove(owner); }
        catch (Exception) { /* presentation only */ }
    }

    private static string Text(MegaCrit.Sts2.Core.Localization.LocString loc)
    {
        try { return loc.GetFormattedText(); }
        catch (Exception) { return ""; }
    }

    // ------------------------------------------------------------------ strike: the sword itself flies at the target

    private static readonly Dictionary<Node2D, ulong> LastStrike = new();

    /// <summary>
    /// A strike, an Onimaru attack, a guard, a guard break or an idle special (SwordVisuals.Idle.cs) is moving this sword right now. Each choreography ends by
    /// handing the sword back (Layout(only)), which clears its mark; the time limits are only a safety net for a
    /// choreography whose tween was killed. (These used to be fixed windows — 0.45 s after a strike, 0.8 s after a guard
    /// — that did not match the real lengths, so a Sync could skip a sword that had already gone home, or run while it
    /// was still on its way, and leave it in its old slot after a switch.)
    /// </summary>
    private static bool InFlight(Node2D node) =>
        IsStriking(node) || IsIdling(node) || (GuardAt.TryGetValue(node, out var g0) && Time.GetTicksMsec() < g0);

    // Strike(): SwordVisuals.Strikes.cs

    internal static Color ColorOf(SwordId sword) => sword switch
    {
        SwordId.Gram => new Color(0.95f, 0.78f, 0.30f),
        SwordId.Ganjiang => new Color(0.85f, 0.38f, 0.26f), // bronze blade, red tassels (images/swords/ganjiang.png)
        SwordId.Moye => new Color(0.55f, 0.78f, 0.98f),     // silver blade, blue inlay (images/swords/moye.png)
        SwordId.Kusanagi => new Color(0.45f, 0.80f, 0.55f),
        SwordId.Tyrfing => new Color(1.00f, 0.55f, 0.20f),
        SwordId.Dainsleif => new Color(0.75f, 0.15f, 0.20f),
        SwordId.Durandal => new Color(0.98f, 0.95f, 0.85f),
        SwordId.Skofnung => new Color(0.65f, 0.85f, 1.00f),
        SwordId.Onimaru => new Color(0.45f, 0.30f, 0.60f),
        SwordId.ClaiomhSolais => new Color(1.00f, 1.00f, 0.75f),
        SwordId.Caladbolg => new Color(0.40f, 0.90f, 0.95f),
        _ => Colors.White,
    };
}
