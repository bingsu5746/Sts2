using Godot;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// 【조합】 card choreographies (user request 2026-10-09: "검 조합 카드 사용 시 카드별로 고유 모션"). Both swords of the
/// card leave their slots and move together in a card-specific pattern (<see cref="UnionMotionKind"/>); the blades
/// land <c>impact</c> seconds after the start, which the attack cards use as their attack animation delay.
/// Presentation only, every failure is swallowed. While a sword is in a choreography the generic per-hit
/// <see cref="Strike"/> leaves it alone. UNVERIFIED in game: exact timing against the damage numbers, positions with
/// very large / very small enemies.
/// </summary>
public static partial class SwordVisuals
{
    private static readonly Dictionary<Node2D, ulong> UnionBusyUntil = new();

    /// <summary>A union choreography is moving this sword right now.</summary>
    private static bool IsUnionBusy(Node2D node) =>
        UnionBusyUntil.TryGetValue(node, out var until) && Time.GetTicksMsec() < until;

    private sealed record UnionBlade(SwordId Sword, Node2D Node, Vector2 Home, Tween Tw);

    /// <summary>Plays one union choreography (see <see cref="UnionMotion"/>). Never throws.</summary>
    internal static void PlayUnion(Player player, UnionMotionKind kind, SwordId primary, SwordId partner,
        Creature? target, float impact)
    {
        try
        {
            PlayUnionInner(player, kind, primary, partner, target, Math.Max(0.15f, impact));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordVisuals] union {kind} failed: {e.Message}");
        }
    }

    private static void PlayUnionInner(Player player, UnionMotionKind kind, SwordId primary, SwordId partner,
        Creature? target, float t)
    {
        var rig = GetRig(player, create: false);
        if (rig == null) return;
        var primaryGroup = SwordRegistry.WithPartners(primary);
        var a = ClaimBlades(rig, primaryGroup);
        var b = ClaimBlades(rig, SwordRegistry.WithPartners(partner).Where(s => !primaryGroup.Contains(s)));
        if (a.Count == 0 && b.Count == 0) return;

        var aim = UnionAim(player, rig, target);
        var center = SpawnPos + new Vector2(0, 30); // Ensifer's chest

        switch (kind)
        {
            case UnionMotionKind.Coil:
                foreach (var x in b) CoilAround(x, aim, t);
                foreach (var x in a) RiseAndSlam(x, aim, t, 0);
                break;
            case UnionMotionKind.Sequence:
                foreach (var x in b) RiseAndSlam(x, aim, t, 0);
                foreach (var x in a) Dash(x, aim, t + 0.1f, glow: false);
                break;
            case UnionMotionKind.Cross:
                foreach (var x in a) CrossThrough(x, aim + new Vector2(-210, -190), aim + new Vector2(210, 190), t);
                foreach (var x in b) CrossThrough(x, aim + new Vector2(-210, 190), aim + new Vector2(210, -190), t);
                break;
            case UnionMotionKind.Pincer:
                foreach (var x in a) FrontStabs(x, aim, t);
                foreach (var x in b) GhostBehind(x, aim, t);
                break;
            case UnionMotionKind.Orbit:
            {
                var all = a.Concat(b).ToList();
                for (var i = 0; i < all.Count; i++)
                {
                    var strikeAt = i == 0 ? t : t + 0.18f;
                    OrbitTarget(all[i], aim, t, Mathf.Tau * i / all.Count, strikeAt);
                }
                break;
            }
            case UnionMotionKind.Ward:
                foreach (var x in a) RingAround(x, center, t + 0.25f);
                foreach (var x in b) DoubleBlink(x, aim, t);
                break;
            case UnionMotionKind.Drop:
                foreach (var x in a) FallFromSky(x, aim + new Vector2(-70, 0), t, 2.4f);
                foreach (var x in b) FallFromSky(x, aim + new Vector2(70, 0), t, 1.5f);
                if (a.Concat(b).FirstOrDefault()?.Node.GetTree() is { } tree)
                    tree.CreateTimer(t).Timeout += () => Shake(ShakeStrength.Medium);
                break;
            case UnionMotionKind.BeamArc:
                foreach (var x in b) BeamFromAbove(x, aim, t);
                foreach (var x in a) RainbowSweep(x, aim, t);
                break;
            case UnionMotionKind.Halo:
            {
                var all = a.Concat(b).ToList();
                for (var i = 0; i < all.Count; i++) HaloAround(all[i], center, Mathf.Tau * i / all.Count);
                break;
            }
            case UnionMotionKind.Forge:
            {
                var anvil = new Vector2(0, -380);
                for (var i = 0; i < b.Count; i++) AnvilPiece(b[i], anvil, i == 0 ? -1 : 1);
                foreach (var x in a) Hammer(x, anvil);
                break;
            }
            case UnionMotionKind.Guard:
            {
                var front = CurrentPos + new Vector2(10, 0);
                foreach (var x in a) GuardStand(x, front, 0f, 1.35f);
                for (var i = 0; i < b.Count; i++) GuardStand(b[i], front + new Vector2(i == 0 ? -28 : 28, 12), i == 0 ? -0.6f : 0.6f, 1.15f);
                break;
            }
            case UnionMotionKind.FireRing:
                foreach (var x in b) RingAround(x, center, t + 0.3f);
                foreach (var x in a) Dash(x, aim, t, glow: true);
                break;
        }

        var busy = Time.GetTicksMsec() + (ulong)((t + 0.9f) * 1000);
        foreach (var x in a.Concat(b))
        {
            UnionBusyUntil[x.Node] = busy;
            GoHome(x);
            x.Tw.TweenCallback(Callable.From(() => Layout(rig)));
        }
    }

    // ------------------------------------------------------------------ setup

    /// <summary>Takes the present swords of a group over from the layout (as Strike does) and gives each a fresh tween.</summary>
    private static List<UnionBlade> ClaimBlades(Rig rig, IEnumerable<SwordId> swords)
    {
        var list = new List<UnionBlade>();
        foreach (var s in swords)
        {
            if (!rig.Swords.TryGetValue(s, out var node) || !GodotObject.IsInstanceValid(node)) continue;
            if (!rig.Slots.TryGetValue(s, out var home)) home = node.Position;
            if (rig.Moves.TryGetValue(s, out var move) && GodotObject.IsInstanceValid(move)) move.Kill();
            node.Position = home;
            node.Rotation = 0;
            node.Scale = Vector2.One * 1.15f;
            node.Modulate = Colors.White;
            var tw = node.CreateTween();
            rig.Moves[s] = tw;
            list.Add(new UnionBlade(s, node, home, tw));
        }

        return list;
    }

    /// <summary>The target's chest; without a target the middle of the enemies (or a point in front of Ensifer).</summary>
    private static Vector2 UnionAim(Player player, Rig rig, Creature? target)
    {
        var room = NCombatRoom.Instance;
        if (target != null && room?.GetCreatureNode(target) is { } node)
            return rig.Root.ToLocal(node.GlobalPosition + new Vector2(0, -130));
        var enemies = player.Creature.CombatState?.HittableEnemies
            .Select(e => room?.GetCreatureNode(e))
            .Where(n => n != null)
            .Select(n => rig.Root.ToLocal(n!.GlobalPosition + new Vector2(0, -130)))
            .ToList();
        if (enemies is { Count: > 0 }) return enemies.Aggregate(Vector2.Zero, (s, v) => s + v) / enemies.Count;
        return new Vector2(650, -200);
    }

    private static void GoHome(UnionBlade x)
    {
        x.Tw.TweenProperty(x.Node, "position", x.Home, 0.32).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        x.Tw.Parallel().TweenProperty(x.Node, "rotation", 0f, 0.32);
        x.Tw.Parallel().TweenProperty(x.Node, "scale", Vector2.One * 1.15f, 0.32);
        x.Tw.Parallel().TweenProperty(x.Node, "modulate", Colors.White, 0.32);
    }

    private static void Shake(ShakeStrength strength)
    {
        try { NGame.Instance?.ScreenShake(strength, ShakeDuration.Short, 90f); }
        catch (Exception) { /* presentation only */ }
    }

    private static Color Glow(SwordId sword, float boost) =>
        new(1f + (ColorOf(sword).R - 0.5f) * boost, 1f + (ColorOf(sword).G - 0.5f) * boost, 1f + (ColorOf(sword).B - 0.5f) * boost);

    // ------------------------------------------------------------------ pieces

    /// <summary>Kusanagi's coil: spirals in around the target like the serpent and cuts through its middle.</summary>
    private static void CoilAround(UnionBlade x, Vector2 aim, float t)
    {
        var (node, tw) = (x.Node, x.Tw);
        Trail(node, t + 0.2, ColorOf(x.Sword));
        var entry = aim + new Vector2(-200, 0);
        tw.TweenProperty(node, "position", entry, 0.12).SetTrans(Tween.TransitionType.Quad);
        tw.TweenMethod(Callable.From<float>(u =>
        {
            var th = Mathf.Pi + Mathf.Tau * 1.5f * u;
            var r = Mathf.Lerp(200f, 50f, u);
            node.Position = aim + new Vector2(Mathf.Cos(th) * r, Mathf.Sin(th) * r * 0.55f);
            node.Rotation = th;
        }), 0f, 1f, Math.Max(0.05, t - 0.18));
        tw.TweenProperty(node, "position", aim + new Vector2(80, 10), 0.06).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        tw.TweenInterval(0.08);
    }

    /// <summary>Gram's style: rises high above the target and slams down on it at <paramref name="t"/>.</summary>
    private static void RiseAndSlam(UnionBlade x, Vector2 aim, float t, float dx)
    {
        var (node, tw) = (x.Node, x.Tw);
        Trail(node, t + 0.15, ColorOf(x.Sword));
        var up = aim + new Vector2(dx, -380);
        tw.TweenProperty(node, "position", up, Math.Max(0.05, t - 0.08)).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", Mathf.Pi, Math.Max(0.05, t - 0.08));
        tw.TweenProperty(node, "position", aim + new Vector2(dx, -20), 0.08).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.Parallel().TweenProperty(node, "scale", Vector2.One * 1.6f, 0.08);
        tw.TweenInterval(0.12);
    }

    /// <summary>A straight dash through the target landing at <paramref name="t"/> (glow: burns brighter first).</summary>
    private static void Dash(UnionBlade x, Vector2 aim, float t, bool glow)
    {
        var (node, tw) = (x.Node, x.Tw);
        var dir = (aim - x.Home).Normalized();
        var windUp = glow ? Math.Max(0.08, t - 0.1) : 0.08;
        tw.TweenProperty(node, "rotation", PointAt(x.Home, aim), 0.08);
        if (glow) tw.Parallel().TweenProperty(node, "modulate", Glow(x.Sword, 1.4f), windUp);
        tw.TweenInterval(Math.Max(0.0, t - 0.1 - windUp)); // the dash below starts at t - 0.1 and lands at t
        tw.TweenCallback(Callable.From(() => Trail(node, 0.35, ColorOf(x.Sword))));
        tw.TweenProperty(node, "position", aim + dir * 60, 0.1).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.TweenInterval(0.08);
    }

    /// <summary>Lines up on one corner and cuts straight through the target to the opposite corner.</summary>
    private static void CrossThrough(UnionBlade x, Vector2 from, Vector2 to, float t)
    {
        var (node, tw) = (x.Node, x.Tw);
        Trail(node, t + 0.25, ColorOf(x.Sword));
        tw.TweenProperty(node, "position", from, Math.Max(0.05, t - 0.1)).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", PointAt(from, to), Math.Max(0.05, t - 0.1));
        tw.TweenProperty(node, "position", to, 0.2); // passes the target halfway, at t
        tw.TweenInterval(0.06);
    }

    /// <summary>Dainsleif in front: one deep thrust at <paramref name="t"/>, then two quick stabs.</summary>
    private static void FrontStabs(UnionBlade x, Vector2 aim, float t)
    {
        var (node, tw) = (x.Node, x.Tw);
        var dir = (aim - x.Home).Normalized();
        var front = aim - dir * 220;
        Trail(node, t + 0.35, ColorOf(x.Sword));
        tw.TweenProperty(node, "position", front, Math.Max(0.05, t - 0.08)).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", PointAt(front, aim), Math.Max(0.05, t - 0.08));
        tw.TweenProperty(node, "position", aim - dir * 30, 0.08).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        for (var k = 0; k < 2; k++)
        {
            tw.TweenProperty(node, "position", aim - dir * 110, 0.05);
            tw.TweenProperty(node, "position", aim - dir * 25, 0.05);
        }
    }

    /// <summary>Skofnung as a ghost: vanishes, appears behind the target and pierces it from behind at <paramref name="t"/>.</summary>
    private static void GhostBehind(UnionBlade x, Vector2 aim, float t)
    {
        var (node, tw) = (x.Node, x.Tw);
        var dir = (aim - x.Home).Normalized();
        var behind = aim + dir * 240;
        tw.TweenProperty(node, "modulate", new Color(0.7f, 0.9f, 1f, 0f), 0.1);
        tw.TweenCallback(Callable.From(() => { node.Position = behind; node.Rotation = PointAt(behind, aim); }));
        tw.TweenInterval(Math.Max(0.0, t - 0.28));
        tw.TweenProperty(node, "modulate", new Color(0.8f, 0.95f, 1f, 0.9f), 0.08);
        tw.TweenProperty(node, "position", aim + dir * 30, 0.1).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.TweenProperty(node, "modulate:a", 0f, 0.15);
        tw.TweenCallback(Callable.From(() => { node.Position = x.Home; node.Rotation = 0; }));
    }

    /// <summary>Circles the target (phase <paramref name="phase"/>) like a spirit, then cuts in at <paramref name="strikeAt"/>.</summary>
    private static void OrbitTarget(UnionBlade x, Vector2 aim, float t, float phase, float strikeAt)
    {
        var (node, tw) = (x.Node, x.Tw);
        const float r = 190f;
        Vector2 At(float th) => aim + new Vector2(Mathf.Cos(th) * r, Mathf.Sin(th) * r * 0.6f);
        Trail(node, strikeAt + 0.15, ColorOf(x.Sword));
        tw.TweenProperty(node, "position", At(phase), 0.12).SetTrans(Tween.TransitionType.Quad);
        tw.Parallel().TweenProperty(node, "modulate", new Color(1f, 1f, 1f, 0.75f), 0.12);
        tw.TweenMethod(Callable.From<float>(u =>
        {
            var th = phase + Mathf.Tau * u;
            node.Position = At(th);
            node.Rotation = th + Mathf.Pi;
        }), 0f, 1f, Math.Max(0.05, t - 0.12));
        tw.TweenInterval(Math.Max(0.0, strikeAt - t));
        tw.TweenProperty(node, "modulate", Colors.White, 0.03);
        tw.TweenProperty(node, "position", aim, 0.06).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        tw.TweenProperty(node, "position", aim + (aim - At(phase)).Normalized() * 120, 0.08);
    }

    /// <summary>A circle of cuts around Ensifer (Kusanagi cutting the burning grass / a warding ring).</summary>
    private static void RingAround(UnionBlade x, Vector2 center, float duration)
    {
        var (node, tw) = (x.Node, x.Tw);
        Vector2 At(float th) => center + new Vector2(Mathf.Cos(th) * 175f, Mathf.Sin(th) * 115f);
        Trail(node, duration + 0.15, ColorOf(x.Sword));
        tw.TweenProperty(node, "position", At(0), 0.1);
        tw.TweenMethod(Callable.From<float>(u =>
        {
            var th = Mathf.Tau * u;
            node.Position = At(th);
            node.Rotation = th + Mathf.Pi; // blade along the circle
        }), 0f, 1f, Math.Max(0.1, duration - 0.1));
    }

    /// <summary>Onimaru's blink, twice: gone and through the target at <paramref name="t"/> and again 0.2 s later.</summary>
    private static void DoubleBlink(UnionBlade x, Vector2 aim, float t)
    {
        var (node, tw) = (x.Node, x.Tw);
        var dir = (aim - x.Home).Normalized();
        tw.TweenProperty(node, "rotation", PointAt(x.Home, aim), 0.08);
        tw.TweenInterval(Math.Max(0.0, t - 0.08));
        tw.TweenCallback(Callable.From(() => node.Position = aim + dir * 110));
        tw.TweenInterval(0.2);
        tw.TweenCallback(Callable.From(() => { node.Position = aim - dir * 140 + new Vector2(0, -60); node.Rotation = PointAt(node.Position, aim) + Mathf.Pi; }));
        tw.TweenInterval(0.15);
        tw.TweenProperty(node, "modulate:a", 0f, 0.08);
        tw.TweenCallback(Callable.From(() => { node.Position = x.Home; node.Rotation = 0; }));
    }

    /// <summary>Rises far above the target (growing to <paramref name="size"/>) and falls point-first at <paramref name="t"/>.</summary>
    private static void FallFromSky(UnionBlade x, Vector2 spot, float t, float size)
    {
        var (node, tw) = (x.Node, x.Tw);
        var up = spot + new Vector2(0, -430);
        Trail(node, t + 0.2, ColorOf(x.Sword));
        tw.TweenProperty(node, "position", up, Math.Max(0.05, t - 0.1)).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", Mathf.Pi, Math.Max(0.05, t - 0.1));
        tw.Parallel().TweenProperty(node, "scale", Vector2.One * size, Math.Max(0.05, t - 0.1));
        tw.TweenProperty(node, "position", spot + new Vector2(0, -30), 0.1).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.TweenInterval(0.18);
    }

    /// <summary>Claíomh Solais above Ensifer's head: stretches into a shaft of light and shoots down at <paramref name="t"/>.</summary>
    private static void BeamFromAbove(UnionBlade x, Vector2 aim, float t)
    {
        var (node, tw) = (x.Node, x.Tw);
        var above = new Vector2(40, -470);
        tw.TweenProperty(node, "position", above, 0.15).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", PointAt(above, aim), 0.15);
        tw.TweenProperty(node, "scale", new Vector2(0.8f, 2.6f), Math.Max(0.05, t - 0.21));
        tw.Parallel().TweenProperty(node, "modulate", new Color(1.7f, 1.7f, 1.35f), Math.Max(0.05, t - 0.21));
        tw.TweenCallback(Callable.From(() => Trail(node, 0.3, ColorOf(x.Sword))));
        tw.TweenProperty(node, "position", aim, 0.06).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        tw.TweenProperty(node, "scale", Vector2.One * 1.15f, 0.12);
    }

    /// <summary>Caladbolg grows to rainbow size and sweeps an arc across the enemies, crossing the target at <paramref name="t"/>.</summary>
    private static void RainbowSweep(UnionBlade x, Vector2 aim, float t)
    {
        var (node, tw) = (x.Node, x.Tw);
        var start = aim + new Vector2(-320, -260);
        var top = aim + new Vector2(0, -440);
        var end = aim + new Vector2(320, -40);
        tw.TweenProperty(node, "position", start, Math.Max(0.05, t - 0.11)).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "scale", Vector2.One * 2.4f, Math.Max(0.05, t - 0.11));
        tw.Parallel().TweenProperty(node, "rotation", -0.9f, Math.Max(0.05, t - 0.11));
        tw.TweenCallback(Callable.From(() => Trail(node, 0.35, ColorOf(x.Sword))));
        tw.TweenMethod(Callable.From<float>(u =>
        {
            node.Position = Bezier(start, top, end, u);
            node.Rotation = Mathf.Lerp(-0.9f, 2.4f, u);
        }), 0f, 1f, 0.22);
        tw.TweenInterval(0.08);
    }

    /// <summary>Glowing circles around Ensifer (no target).</summary>
    private static void HaloAround(UnionBlade x, Vector2 center, float phase)
    {
        var (node, tw) = (x.Node, x.Tw);
        Vector2 At(float th) => center + new Vector2(Mathf.Cos(th) * 190f, Mathf.Sin(th) * 120f);
        Trail(node, 1.0, ColorOf(x.Sword));
        tw.TweenProperty(node, "position", At(phase), 0.15).SetTrans(Tween.TransitionType.Quad);
        tw.Parallel().TweenProperty(node, "modulate", Glow(x.Sword, 1.2f), 0.15);
        tw.TweenMethod(Callable.From<float>(u =>
        {
            var th = phase + Mathf.Tau * u;
            node.Position = At(th);
            node.Rotation = 0.25f * Mathf.Sin(th);
        }), 0f, 1f, 0.8);
    }

    /// <summary>One half of the anvil: lies flat above Ensifer's head (side -1 left / +1 right).</summary>
    private static void AnvilPiece(UnionBlade x, Vector2 anvil, int side)
    {
        var (node, tw) = (x.Node, x.Tw);
        tw.TweenProperty(node, "position", anvil + new Vector2(60 * side, 34), 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", Mathf.Pi / 2 * side, 0.2);
        tw.TweenInterval(0.75);
    }

    /// <summary>Gram as the hammer: three blows on the anvil, each with a flash.</summary>
    private static void Hammer(UnionBlade x, Vector2 anvil)
    {
        var (node, tw) = (x.Node, x.Tw);
        var raised = anvil + new Vector2(0, -170);
        tw.TweenProperty(node, "position", raised, 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", Mathf.Pi, 0.2);
        for (var k = 0; k < 3; k++)
        {
            tw.TweenProperty(node, "position", anvil + new Vector2(0, -30), 0.08).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.TweenProperty(node, "modulate", new Color(2f, 1.7f, 1.3f), 0.03);
            tw.TweenProperty(node, "position", raised, 0.12).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(node, "modulate", Colors.White, 0.12);
        }
    }

    /// <summary>Plants in front of Ensifer as a shield, then fades half out of sight (hidden), then comes back.</summary>
    private static void GuardStand(UnionBlade x, Vector2 spot, float rotation, float size)
    {
        var (node, tw) = (x.Node, x.Tw);
        tw.TweenProperty(node, "position", spot, 0.2).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", rotation, 0.2);
        tw.Parallel().TweenProperty(node, "scale", Vector2.One * size, 0.2);
        tw.TweenInterval(0.25);
        tw.TweenProperty(node, "modulate", new Color(0.8f, 0.8f, 0.9f, 0.3f), 0.25);
        tw.TweenInterval(0.2);
        tw.TweenProperty(node, "modulate", Colors.White, 0.2);
    }
}
