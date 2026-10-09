using Godot;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
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
/// UNVERIFIED in game: node offsets relative to the creature node, z-ordering against the creature body.
/// </summary>
public static class SwordVisuals
{
    private sealed class Rig
    {
        public required Node2D Root;
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

    // Layout (사용자 스케치 2026-10-04): swords float upright around the character — left, above the head, right.
    // The current sword always takes the right slot (in front, toward the enemy). The character only gestures.
    private static readonly Vector2 SpawnPos = new(0, -220);      // swords appear from / vanish into the character
    private static readonly Vector2 CurrentPos = new(150, -200);  // right of the character
    private static readonly Vector2[] IdleSlots =
    {
        new(-150, -200),  // left of the character
        new(0, -400),     // above the head
        new(-110, -360),  // extra slots (sword cap raised by relics)
        new(110, -360),
    };

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

    /// <summary>Combat over: forget the rig (its nodes die with the combat room).</summary>
    public static void Clear(Player player)
    {
        Rigs.Remove(player);
        MotionDirector.Clear(player);
    }

    // ------------------------------------------------------------------ internals

    private static void SyncInner(Player player)
    {
        var state = SwordCombat.Get(player);
        if (state == null) return;
        var rig = GetRig(player, create: state.Present.Count > 0 || state.ReturnedToVault.Count > 0);
        if (rig == null) return;

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
        foreach (var sword in state.Present.Where(s => !rig.Swords.ContainsKey(s)))
        {
            var node = CreateSword(sword, player);
            rig.Root.AddChild(node);
            node.Position = SpawnPos;
            node.Scale = new Vector2(0.3f, 0.3f);
            rig.Swords[sword] = node;
        }

        rig.Current = state.Current;
        Layout(rig);
        ApplyPalmCircles(player, state.Current);
    }

    private static void Layout(Rig rig)
    {
        var slot = 0;
        foreach (var (sword, node) in rig.Swords.OrderBy(kv => (int)kv.Key))
        {
            var isCurrent = rig.Current == sword;
            var pos = isCurrent ? CurrentPos : IdleSlots[slot++ % IdleSlots.Length];
            rig.Slots[sword] = pos;
            if (rig.Moves.TryGetValue(sword, out var old) && GodotObject.IsInstanceValid(old)) old.Kill();
            var t = node.CreateTween().SetParallel();
            rig.Moves[sword] = t;
            t.TweenProperty(node, "position", pos, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            t.TweenProperty(node, "scale", isCurrent ? new Vector2(1.15f, 1.15f) : new Vector2(0.8f, 0.8f), 0.3);
            t.TweenProperty(node, "rotation", 0f, 0.3);
            t.TweenProperty(node, "modulate", isCurrent ? Colors.White : new Color(0.75f, 0.75f, 0.8f, 0.9f), 0.3);
            node.ZIndex = isCurrent ? 1 : -1;
        }
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

        rig = new Rig { Root = root };
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

    private static Node2D CreateSword(SwordId sword, Player player)
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

        // hover box exactly over the drawn sword (moves with the bob): name, level and effect in the game's tooltip
        var hover = new Control
        {
            Name = "Hover", Size = size, Position = -size / 2, MouseFilter = Control.MouseFilterEnum.Pass,
        };
        hover.MouseEntered += () => ShowTip(hover, sword, player);
        hover.MouseExited += () => HideTip(hover);
        blade.AddChild(hover);

        holder.AddChild(blade);

        // float: slow bob + a slight sway, offset per sword so they never move in lockstep
        var period = 1.4 + (int)sword % 4 * 0.2;
        var bob = blade.CreateTween().SetLoops();
        bob.TweenProperty(blade, "position:y", -12f, period).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        bob.TweenProperty(blade, "position:y", 0f, period).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        var sway = blade.CreateTween().SetLoops();
        sway.TweenProperty(blade, "rotation", 0.05f, period * 1.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        sway.TweenProperty(blade, "rotation", -0.05f, period * 1.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        return holder;
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

    /// <summary>
    /// Each sword flies its own way (user request 2026-10-09). All flights reach the target within ~0.15 s (the game
    /// deals the damage 0.15 s after the Attack trigger) and leave fading afterimages in the sword's colour.
    /// </summary>
    private static void StrikeStyle(Tween tw, Node2D node, SwordId sword, Vector2 home, Vector2 aim)
    {
        var col = ColorOf(sword);
        var dir = (aim - home).Normalized();
        var hit = aim - dir * 40f;
        var side = new Vector2(-dir.Y, dir.X);
        Trail(node, sword == SwordId.ClaiomhSolais ? 0.35 : 0.5, col);
        switch (sword)
        {
            case SwordId.Gram: // rises, then slams down onto the target
            {
                var up = new Vector2((home.X + aim.X) / 2, Math.Min(home.Y, aim.Y) - 260);
                tw.TweenProperty(node, "position", up, 0.07).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", PointAt(up, hit), 0.07);
                tw.TweenProperty(node, "position", hit, 0.07).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                tw.Parallel().TweenProperty(node, "scale", Vector2.One * 1.45f, 0.07);
                tw.TweenInterval(0.08);
                tw.TweenProperty(node, "position", home, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", Mathf.Tau, 0.35);
                tw.Parallel().TweenProperty(node, "scale", Vector2.One * 1.15f, 0.35);
                tw.TweenCallback(Callable.From(() => node.Rotation = 0));
                break;
            }
            case SwordId.Ganjiang or SwordId.Moye: // two quick crossing cuts
            {
                var a1 = hit + side * 70; var a2 = hit - side * 70;
                tw.TweenProperty(node, "rotation", PointAt(home, a1), 0.04);
                tw.TweenProperty(node, "position", a1, 0.06);
                tw.TweenProperty(node, "position", a2, 0.06).SetTrans(Tween.TransitionType.Sine);
                tw.Parallel().TweenProperty(node, "rotation", PointAt(a1, a2), 0.06);
                tw.TweenProperty(node, "position", hit, 0.05);
                tw.TweenProperty(node, "position", home, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", 0f, 0.3);
                break;
            }
            case SwordId.Kusanagi: // spinning boomerang along a curve
            {
                var mid = (home + aim) / 2 + side * 160;
                tw.TweenMethod(Callable.From<float>(t => node.Position = Bezier(home, mid, hit, t)), 0f, 1f, 0.14);
                tw.Parallel().TweenProperty(node, "rotation", Mathf.Tau * 2, 0.14);
                var back = (home + aim) / 2 - side * 160;
                tw.TweenMethod(Callable.From<float>(t => node.Position = Bezier(hit, back, home, t)), 0f, 1f, 0.4);
                tw.Parallel().TweenProperty(node, "rotation", Mathf.Tau * 4, 0.4);
                tw.TweenCallback(Callable.From(() => node.Rotation = 0));
                break;
            }
            case SwordId.Dainsleif: // three rapid stabs
            {
                tw.TweenProperty(node, "rotation", PointAt(home, hit), 0.04);
                for (var k = 0; k < 3; k++)
                {
                    tw.TweenProperty(node, "position", hit + side * (k - 1) * 30, 0.045).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                    tw.TweenProperty(node, "position", hit - dir * 90, 0.04);
                }
                tw.TweenProperty(node, "position", home, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", 0f, 0.3);
                break;
            }
            case SwordId.Durandal: // vanishes, appears above the target and drops straight down
            {
                var above = aim + new Vector2(0, -300);
                tw.TweenProperty(node, "modulate:a", 0f, 0.04);
                tw.TweenCallback(Callable.From(() => { node.Position = above; node.Rotation = Mathf.Pi; }));
                tw.TweenProperty(node, "modulate:a", 1f, 0.03);
                tw.TweenProperty(node, "position", aim + new Vector2(0, -30), 0.07).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                tw.Parallel().TweenProperty(node, "scale", Vector2.One * 1.5f, 0.07);
                tw.TweenInterval(0.12);
                tw.TweenProperty(node, "modulate:a", 0f, 0.1);
                tw.TweenCallback(Callable.From(() => { node.Position = home; node.Rotation = 0; node.Scale = Vector2.One * 1.15f; }));
                tw.TweenProperty(node, "modulate:a", 1f, 0.2);
                break;
            }
            case SwordId.Skofnung: // a ghost: fades out, reappears beside the target, cuts, fades home
            {
                var beside = aim - dir * 160 + side * 60;
                tw.TweenProperty(node, "modulate", new Color(0.7f, 0.9f, 1f, 0f), 0.05);
                tw.TweenCallback(Callable.From(() => { node.Position = beside; node.Rotation = PointAt(beside, hit); }));
                tw.TweenProperty(node, "modulate", new Color(0.8f, 0.95f, 1f, 0.9f), 0.04);
                tw.TweenProperty(node, "position", hit + dir * 60, 0.06);
                tw.TweenProperty(node, "modulate:a", 0f, 0.12);
                tw.TweenCallback(Callable.From(() => { node.Position = home; node.Rotation = 0; }));
                tw.TweenProperty(node, "modulate", Colors.White, 0.25);
                break;
            }
            case SwordId.Onimaru: // blink: gone and through the target in one frame, a flash line left behind
            {
                tw.TweenProperty(node, "rotation", PointAt(home, hit), 0.03);
                tw.TweenInterval(0.08);
                tw.TweenCallback(Callable.From(() => node.Position = hit + dir * 120));
                tw.TweenInterval(0.15);
                tw.TweenProperty(node, "modulate:a", 0f, 0.08);
                tw.TweenCallback(Callable.From(() => { node.Position = home; node.Rotation = 0; }));
                tw.TweenProperty(node, "modulate:a", 1f, 0.15);
                break;
            }
            case SwordId.ClaiomhSolais: // a beam of light: stretches into a long shaft as it shoots
            {
                tw.TweenProperty(node, "rotation", PointAt(home, hit), 0.03);
                tw.TweenProperty(node, "scale", new Vector2(0.8f, 2.6f), 0.05);
                tw.Parallel().TweenProperty(node, "modulate", new Color(1.6f, 1.6f, 1.3f), 0.05);
                tw.TweenProperty(node, "position", hit, 0.06).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
                tw.TweenProperty(node, "scale", Vector2.One * 1.15f, 0.12);
                tw.Parallel().TweenProperty(node, "modulate", Colors.White, 0.12);
                tw.TweenProperty(node, "position", home, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", 0f, 0.3);
                break;
            }
            case SwordId.Caladbolg: // grows huge and sweeps across the target
            {
                var a1 = aim + new Vector2(-60, -220);
                tw.TweenProperty(node, "scale", Vector2.One * 2.4f, 0.07);
                tw.Parallel().TweenProperty(node, "position", a1, 0.07);
                tw.Parallel().TweenProperty(node, "rotation", -0.6f, 0.07);
                tw.TweenProperty(node, "rotation", 2.2f, 0.09).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                tw.Parallel().TweenProperty(node, "position", aim + new Vector2(40, -80), 0.09);
                tw.TweenInterval(0.08);
                tw.TweenProperty(node, "scale", Vector2.One * 1.15f, 0.3);
                tw.Parallel().TweenProperty(node, "position", home, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", 0f, 0.3);
                break;
            }
            default: // Tyrfing and anything else: a straight burning dash
            {
                tw.TweenProperty(node, "rotation", PointAt(home, hit), 0.05).SetTrans(Tween.TransitionType.Quad);
                tw.TweenProperty(node, "position", hit, 0.09).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                tw.TweenInterval(0.06);
                tw.TweenProperty(node, "position", home, 0.28).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", 0f, 0.28);
                break;
            }
        }
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
            if (node.GetNodeOrNull<Node2D>("Blade") is not { } blade) return;
            var ghost = new Node2D { Position = node.Position, Rotation = node.Rotation, Scale = node.Scale, ZIndex = node.ZIndex - 1 };
            foreach (var child in blade.GetChildren())
                if (child is Sprite2D sp && sp.Name != "Aura")
                    ghost.AddChild(new Sprite2D { Texture = sp.Texture, Scale = sp.Scale, Position = sp.Position, Rotation = sp.Rotation,
                        RegionEnabled = sp.RegionEnabled, RegionRect = sp.RegionRect });
            ghost.Modulate = new Color(col.Lightened(0.3f), 0.45f);
            ghost.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            foreach (var c in ghost.GetChildren()) if (c is CanvasItem ci) ci.UseParentMaterial = true;
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

    /// <summary>
    /// The current sword (else any present one) flies to <paramref name="target"/> and back in its own style
    /// (<see cref="StrikeStyle"/>; user request 2026-10-08:
    /// "공격하면 소환된 칼이 직접 날아가서 공격"). The game deals the damage 0.15 s after the Attack trigger, so the
    /// flight reaches the target on the hit. A second call within the same flight is ignored.
    /// </summary>
    public static void Strike(Player player, MegaCrit.Sts2.Core.Entities.Creatures.Creature? target)
    {
        try
        {
            var rig = GetRig(player, create: false);
            if (rig == null || rig.Swords.Count == 0) return;
            var sword = rig.Current is { } c && rig.Swords.ContainsKey(c) ? c : rig.Swords.Keys.First();
            var node = rig.Swords[sword];
            var now = Time.GetTicksMsec();
            if (LastStrike.TryGetValue(node, out var t0) && now - t0 < 420) return;
            LastStrike[node] = now;
            var room = NCombatRoom.Instance;
            var targetNode = target != null ? room?.GetCreatureNode(target) : null;
            Vector2 aim;
            if (targetNode != null) aim = rig.Root.ToLocal(targetNode.GlobalPosition + new Vector2(0, -130));
            else aim = (rig.Slots.TryGetValue(sword, out var hs) ? hs : node.Position) + new Vector2(520, 40); // no single target

            // a sword summoned by this very card is still flying out of Ensifer: put it on its slot first so the strike
            // starts from there (bug report 2026-10-09: it looked like a swing from his hand)
            if (!rig.Slots.TryGetValue(sword, out var home)) home = node.Position;
            if (rig.Moves.TryGetValue(sword, out var move) && GodotObject.IsInstanceValid(move)) move.Kill();
            node.Position = home;
            node.Scale = Vector2.One * 1.15f;
            node.Modulate = Colors.White;

            var tw = node.CreateTween();
            rig.Moves[sword] = tw; // a later strike / layout takes over cleanly instead of fighting this tween
            StrikeStyle(tw, node, sword, home, aim);
            tw.TweenCallback(Callable.From(() => Layout(rig)));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordVisuals] Strike failed: {e.Message}");
        }
    }

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
