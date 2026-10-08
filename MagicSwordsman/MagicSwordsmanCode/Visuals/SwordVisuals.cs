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
    /// Plays one of the character's own AnimationPlayer clips (e.g. "Summon", "Block") through BaseLib's handler.
    /// The game only triggers Attack / Cast / Hit / Dead; these extra motions are fired from our own hooks.
    /// Presentation only; silently does nothing if the clip or the creature node is missing.
    /// </summary>
    public static void PlayMotion(Player player, string clip)
    {
        try
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
            if (node == null) return;
            var anim = FindChild<AnimationPlayer>(node.Visuals);
            if (anim == null || !anim.HasAnimation(clip)) return;
            anim.Stop();
            anim.Play(clip);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordVisuals] PlayMotion {clip} failed: {e.Message}");
        }
    }

    /// <summary>Combat over: forget the rig (its nodes die with the combat room).</summary>
    public static void Clear(Player player) => Rigs.Remove(player);

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
            var node = CreateSword(sword);
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
            var t = node.CreateTween().SetParallel();
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
        HookIdleReturn(creatureNode);
        return rig;
    }

    /// <summary>
    /// BaseLib plays our AnimationPlayer clips by name but does not go back to "idle" after one-shot clips
    /// (Attack / Cast / Hit). Queue idle when a non-looping clip finishes. Dead stays on its last frame.
    /// </summary>
    private static void HookIdleReturn(NCreature creatureNode)
    {
        var player = FindChild<AnimationPlayer>(creatureNode.Visuals);
        if (player == null || player.HasMeta("ms_idle_hook") || !player.HasAnimation("idle")) return;
        player.SetMeta("ms_idle_hook", true);
        player.AnimationFinished += name =>
        {
            if (name != "idle" && name != "Dead" && GodotObject.IsInstanceValid(player)) player.Play("idle");
        };
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

    private static Node2D CreateSword(SwordId sword)
    {
        var holder = new Node2D { Name = $"Sword_{sword}" };
        Node2D blade;
        var texPath = $"{MainFile.ResPath}/images/swords/{sword.ToString().ToLowerInvariant()}.png";
        if (ResourceLoader.Exists(texPath))
        {
            var tex = GD.Load<Texture2D>(texPath);
            var sprite = new Sprite2D { Texture = tex, TextureFilter = CanvasItem.TextureFilterEnum.Linear };
            // Art is high-res (about 1000 px tall); show every sword at the same on-screen height as the placeholder.
            var scale = SwordSpriteHeight / Math.Max(1f, tex.GetHeight());
            sprite.Scale = new Vector2(scale, scale);
            blade = sprite;
        }
        else
        {
            blade = new Node2D();
            var color = ColorOf(sword);
            blade.AddChild(new Polygon2D
            {
                Color = color,
                Polygon = new[] { new Vector2(0, -70), new Vector2(7, -55), new Vector2(6, 18), new Vector2(-6, 18), new Vector2(-7, -55) },
            });
            blade.AddChild(new Polygon2D
            {
                Color = color.Darkened(0.45f),
                Polygon = new[] { new Vector2(-18, 18), new Vector2(18, 18), new Vector2(18, 25), new Vector2(-18, 25) },
            });
            blade.AddChild(new Polygon2D
            {
                Color = new Color(0.25f, 0.18f, 0.12f),
                Polygon = new[] { new Vector2(-4, 25), new Vector2(4, 25), new Vector2(4, 48), new Vector2(-4, 48) },
            });
        }

        holder.AddChild(blade);

        // gentle bob, offset per sword so they don't move in lockstep
        var period = 1.1 + (int)sword % 4 * 0.15;
        var bob = blade.CreateTween().SetLoops();
        bob.TweenProperty(blade, "position:y", -8f, period).SetTrans(Tween.TransitionType.Sine);
        bob.TweenProperty(blade, "position:y", 0f, period).SetTrans(Tween.TransitionType.Sine);
        return holder;
    }

    internal static Color ColorOf(SwordId sword) => sword switch
    {
        SwordId.Gram => new Color(0.95f, 0.78f, 0.30f),
        SwordId.Ganjiang => new Color(0.45f, 0.55f, 0.75f),
        SwordId.Moye => new Color(0.90f, 0.75f, 0.82f),
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
