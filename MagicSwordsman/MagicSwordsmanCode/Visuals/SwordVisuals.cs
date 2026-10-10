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
    };

    private const int ZBack = 0, ZFront = 1, ZFlying = 2;

    // ------------------------------------------------------------------ entry points

    /// <summary>Reconciles the floating swords with the combat state (present swords, current sword).</summary>
    public static void Sync(Player player)
    {
        try { SyncInner(player); }
        catch (Exception e) { MainFile.Logger.Warn($"[SwordVisuals] Sync failed: {e.Message}"); }
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
                return node.ToGlobal(new Vector2(20, -SwordSpriteHeight * 0.5f));
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
            node.QueueFree();
            rig.Swords[sw] = fresh;
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
    /// Sends every sword to its resting place: the current sword to the front-right slot, the others to the idle
    /// slots around him (behind the body), Onimaru to the pose of its stance (<see cref="OnimaruPose"/>).
    /// <paramref name="fresh"/>: swords summoned this very sync rise slowly to their place (SwordFx.Summon).
    /// <paramref name="only"/>: a choreography (strike, guard, union) handing one sword back — the others are left
    /// alone so a sword still in flight is not yanked home.
    /// </summary>
    private static void Layout(Rig rig, ICollection<SwordId>? fresh = null, SwordId? only = null)
    {
        if (rig.Dead) return;
        var slot = 0;
        foreach (var (sword, node) in rig.Swords.OrderBy(kv => (int)kv.Key))
        {
            if (!GodotObject.IsInstanceValid(node)) continue;
            var isCurrent = rig.Current == sword;
            var stance = sword == SwordId.Onimaru ? OnimaruPose(rig.Player, isCurrent) : null;
            var pos = stance?.Pos ?? (isCurrent ? CurrentPos : IdleSlots[slot++ % IdleSlots.Length]);
            rig.Slots[sword] = pos;
            if (only != null && only != sword) continue;
            // a full re-layout (Sync) leaves a sword in the middle of a choreography alone: its own end calls
            // Layout(only: it), which reads the slot computed here
            if (only == null && (IsUnionBusy(node) || InFlight(node))) continue;
            if (rig.Moves.TryGetValue(sword, out var old) && GodotObject.IsInstanceValid(old)) old.Kill();
            var born = fresh?.Contains(sword) == true;
            var dur = born ? 0.85 : 0.4;
            var t = node.CreateTween().SetParallel();
            rig.Moves[sword] = t;
            if (born)
                t.TweenProperty(node, "position", pos, dur).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            else
                t.TweenProperty(node, "position", pos, dur).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            var scale = stance?.Scale ?? (isCurrent ? 1.15f : 0.85f);
            t.TweenProperty(node, "scale", new Vector2(scale, scale), dur * 0.8);
            t.TweenProperty(node, "rotation", stance?.Rot ?? 0f, dur * 0.8).SetTrans(Tween.TransitionType.Sine);
            t.TweenProperty(node, "modulate", isCurrent ? Colors.White : new Color(0.82f, 0.82f, 0.88f, 0.95f), dur * 0.8);
            node.ZIndex = stance?.Z ?? (isCurrent ? ZFront : ZBack);
            if (stance?.Orbit == true) t.Chain().TweenCallback(Callable.From(() => StartRanbuOrbit(rig, sword, node)));
        }
    }

    /// <summary>
    /// Onimaru's resting pose by stance (user request 2026-10-10 "오니마루는 자세마다 소환되어서 있는 모션이나 위치, 각도
    /// 등등을 좀 바꿔줘"): 호위 stands guard in front of him, 거합 rests at his hip like a sheathed blade, 퇴마 is raised
    /// high point up, 베기 is held level pointing at the enemy, 난무 circles him quickly (<see cref="StartRanbuOrbit"/>).
    /// Art points up, so rotation is the angle of the tip from "up", clockwise. A stance change re-runs Layout
    /// (OnimaruAttack.SetKind calls Sync), which tweens the katana from the old pose to the new one.
    /// </summary>
    private static (Vector2 Pos, float Rot, float Scale, int Z, bool Orbit)? OnimaruPose(Player player, bool isCurrent)
    {
        OnimaruKind kind;
        try { kind = OnimaruAttack.GetKind(player); }
        catch (Exception) { kind = OnimaruKind.Iai; }
        var k = isCurrent ? 1.12f : 1f;
        return kind switch
        {
            OnimaruKind.Goei => (new Vector2(70, -225), 0f, 1.1f * k, ZFront, false),
            OnimaruKind.Taima => (new Vector2(70, -575), 0f, 1f * k, ZBack, false),
            OnimaruKind.Giri => (new Vector2(215, -350), Mathf.Pi / 2, 0.95f * k, ZFront, false),
            OnimaruKind.Ranbu => (RanbuAt(0f), RanbuRot(0f), 0.85f * k, ZFront, true),
            _ => (new Vector2(30, -165), -1.95f, 0.9f * k, ZFront, false), // Iai: hilt forward, blade back and down
        };
    }

    private static readonly Vector2 RanbuCenter = new(0, -255);
    private static Vector2 RanbuAt(float th) => RanbuCenter + new Vector2(Mathf.Cos(th) * 215f, Mathf.Sin(th) * 70f);
    // blade along the direction of travel
    private static float RanbuRot(float th) => PointAt(RanbuAt(th), RanbuAt(th + 0.1f));

    /// <summary>난무: Onimaru circles him fast, passing in front of and behind the body.</summary>
    private static void StartRanbuOrbit(Rig rig, SwordId sword, Node2D node)
    {
        if (rig.Dead || !GodotObject.IsInstanceValid(node)) return;
        var t = node.CreateTween().SetLoops();
        rig.Moves[sword] = t;
        t.TweenMethod(Callable.From<float>(th =>
        {
            node.Position = RanbuAt(th);
            node.Rotation = RanbuRot(th);
            node.ZIndex = Mathf.Sin(th) > 0 ? ZFront : ZBack; // lower half of the ellipse = nearer the camera
        }), 0f, Mathf.Tau, 1.3);
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
        var size = new Vector2(40, 120);
        if (ResourceLoader.Exists(texPath))
        {
            var tex = GD.Load<Texture2D>(texPath);
            // Art is high-res (about 1000 px tall); every sword is shown at the same on-screen height.
            var scale = SwordSpriteHeight / Math.Max(1f, tex.GetHeight());

            // faint violet aura behind the blade (user request 2026-10-08), breathing slowly
            var auraPath = $"{MainFile.ResPath}/images/swords/aura/{id}.png";
            if (ResourceLoader.Exists(auraPath))
            {
                var aura = new Sprite2D
                {
                    Texture = GD.Load<Texture2D>(auraPath), Scale = new Vector2(scale, scale),
                    Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
                    Modulate = new Color(AuraColor, 0.22f), Name = "Aura",
                };
                blade.AddChild(aura);
                // stronger, larger aura the higher the sword's level (user request 2026-10-09)
                var lv = Math.Clamp(SwordCombat.LevelOf(player, sword), 0, 5);
                float lo = 0.08f + 0.05f * lv, hi = 0.18f + 0.07f * lv;
                aura.Scale *= 1f + 0.03f * lv;
                var breathe = aura.CreateTween().SetLoops();
                breathe.TweenProperty(aura, "modulate:a", hi, lv >= 5 ? 0.9 : 1.8).SetTrans(Tween.TransitionType.Sine);
                breathe.TweenProperty(aura, "modulate:a", lo, lv >= 5 ? 0.9 : 1.8).SetTrans(Tween.TransitionType.Sine);
            }

            var level = SwordCombat.LevelOf(player, sword);
            holder.SetMeta("level", level);
            AddBlade(blade, sword, level, tex, scale);
            size = new Vector2(tex.GetWidth(), tex.GetHeight()) * scale;
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

        // hover box exactly over the drawn sword (moves with the bob): name, level and effect in the game's tooltip
        var hover = new Control
        {
            Name = "Hover", Size = size, Position = -size / 2, MouseFilter = Control.MouseFilterEnum.Pass,
        };
        hover.MouseEntered += () => ShowTip(hover, sword, player);
        hover.MouseExited += () => HideTip(hover);
        blade.AddChild(hover);

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

    /// <summary>
    /// The blade itself, by level: dull and greyed at 0 brightening to full colour at 5, motes of light from level 3.
    /// Gram at level 0 is "부서진 그람": drawn as two shards with a gap (its stage name; Odin broke it).
    /// </summary>
    private static void AddBlade(Node2D blade, SwordId sword, int level, Texture2D tex, float scale)
    {
        level = Math.Clamp(level, 0, 5);
        var t = level / 5f;
        var tint = new Color(0.62f + 0.38f * t, 0.6f + 0.4f * t, 0.66f + 0.34f * t);
        if (sword == SwordId.Gram && level == 0)
        {
            int w = tex.GetWidth(), h = tex.GetHeight();
            var top = new Sprite2D
            {
                Texture = tex, RegionEnabled = true, RegionRect = new Rect2(0, 0, w, h * 0.47f), Scale = new Vector2(scale, scale),
                Position = new Vector2(10, -h * 0.29f * scale), Rotation = 0.22f, Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            };
            var bottom = new Sprite2D
            {
                Texture = tex, RegionEnabled = true, RegionRect = new Rect2(0, h * 0.53f, w, h * 0.47f), Scale = new Vector2(scale, scale),
                Position = new Vector2(0, h * 0.27f * scale), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            };
            blade.AddChild(bottom);
            blade.AddChild(top);
            return;
        }

        blade.AddChild(new Sprite2D { Texture = tex, TextureFilter = CanvasItem.TextureFilterEnum.Linear, Scale = new Vector2(scale, scale), Modulate = tint });
        if (level < 3) return;
        var motes = new CpuParticles2D
        {
            Amount = 4 + level * 3, Lifetime = 1.8, Preprocess = 1.8,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(tex.GetWidth() * scale * 0.3f, tex.GetHeight() * scale * 0.45f),
            Gravity = new Vector2(0, -18), InitialVelocityMin = 2, InitialVelocityMax = 10,
            ScaleAmountMin = 0.035f, ScaleAmountMax = 0.07f, Color = ColorOf(sword).Lightened(0.3f),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        var core = $"{MainFile.ResPath}/images/vfx/hand_core.png";
        if (ResourceLoader.Exists(core)) motes.Texture = GD.Load<Texture2D>(core);
        blade.AddChild(motes);
    }

    private static float PointAt(Vector2 from, Vector2 to)
    {
        var d = to - from;
        return Mathf.Atan2(d.Y, d.X) + Mathf.Pi / 2; // art points up
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) =>
        (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;

    /// <summary>Fading copies of the sword left behind while it flies.</summary>
    private static void Trail(Node2D node, double duration, Color col)
    {
        if (node.GetParent() is not Node2D parent) return;
        var timer = new Godot.Timer { WaitTime = 0.025, Autostart = true };
        var elapsed = 0.0;
        timer.Timeout += () =>
        {
            elapsed += timer.WaitTime;
            if (elapsed > duration || !GodotObject.IsInstanceValid(node)) { timer.QueueFree(); return; }
            if (BladeCopy(node) is not { } ghost) return;
            ghost.ZIndex = Math.Max(ZBack, node.ZIndex - 1);
            ghost.Modulate = new Color(col.Lightened(0.3f), 0.45f);
            ghost.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            parent.AddChild(ghost);
            var fade = ghost.CreateTween();
            fade.TweenProperty(ghost, "modulate:a", 0f, 0.22);
            fade.TweenCallback(Callable.From(ghost.QueueFree));
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
            var desc = $"{Text(Events.SwordLore.StageName(sword, level))} ({level})\n{Text(Events.SwordLore.Line(sword, "EFFECT"))}";
            var affinity = Dialogue.SwordAffinity.TipLine(player, sword);
            if (affinity.Length > 0) desc += "\n" + affinity;
            desc += Cards.Union.UnionCatalog.SwordTipLine(player, sword); // 【조합】 cards of this sword
            MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet.CreateAndShow(owner,
                new MegaCrit.Sts2.Core.HoverTips.HoverTip(Events.SwordLore.Name(sword), desc));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordVisuals] tooltip: {e.Message}");
        }
    }

    private static void HideTip(Control owner)
    {
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

    /// <summary>A strike (until it ends) / Onimaru attack (0.45 s) or a guard (0.6 s) is moving this sword right now.</summary>
    private static bool InFlight(Node2D node) =>
        IsStriking(node) || (LastStrike.TryGetValue(node, out var t0) && Time.GetTicksMsec() - t0 < 450) ||
        (GuardAt.TryGetValue(node, out var g0) && Time.GetTicksMsec() - g0 < 800);

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
