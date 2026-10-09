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
                var breathe = aura.CreateTween().SetLoops();
                breathe.TweenProperty(aura, "modulate:a", 0.38f, 1.8).SetTrans(Tween.TransitionType.Sine);
                breathe.TweenProperty(aura, "modulate:a", 0.2f, 1.8).SetTrans(Tween.TransitionType.Sine);
            }

            blade.AddChild(new Sprite2D { Texture = tex, TextureFilter = CanvasItem.TextureFilterEnum.Linear, Scale = new Vector2(scale, scale) });
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
    /// The current sword (else any present one) flies to <paramref name="target"/> and back (user request 2026-10-08:
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
            else aim = node.Position + new Vector2(520, 40); // no single target: thrust toward the enemy side

            // a sword summoned by this very card is still flying out of Ensifer: put it on its slot first so the strike
            // starts from there (bug report 2026-10-09: it looked like a swing from his hand)
            if (!rig.Slots.TryGetValue(sword, out var home)) home = node.Position;
            if (rig.Moves.TryGetValue(sword, out var move) && GodotObject.IsInstanceValid(move)) move.Kill();
            node.Position = home;
            node.Scale = Vector2.One * 1.15f;
            node.Modulate = Colors.White;
            var dir = aim - home;
            var angle = Mathf.Atan2(dir.Y, dir.X) + Mathf.Pi / 2; // art points up
            // stop a little short so the blade tip, not the hilt, meets the target
            var hit = aim - dir.Normalized() * 40f;

            var tw = node.CreateTween();
            tw.TweenProperty(node, "rotation", angle, 0.06).SetTrans(Tween.TransitionType.Quad);
            tw.TweenProperty(node, "position", hit, 0.11).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.TweenInterval(0.06);
            tw.TweenProperty(node, "position", home, 0.28).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(node, "rotation", 0f, 0.28);
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
