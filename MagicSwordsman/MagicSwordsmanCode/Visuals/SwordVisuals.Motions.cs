using Godot;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Sword choreographies besides the plain strike (user requests 2026-10-10):
///  - <see cref="Guard"/>: gaining Block / a fully blocked hit — two swords cross into an X in front of Ensifer on the
///    attacker's side, push forward to meet the blow (a small clash spark) and go back to their slots (≤ 0.6 s).
///  - <see cref="OnimaruStrike"/>: Onimaru's own attacks (【명령】, auto attacks, kind cards) fly the katana to the
///    target and back. Those attacks deal Unpowered damage without the game's "Attack" trigger, so the generic
///    <see cref="Strike"/> never saw them (OnimaruAttack.Perform calls this directly).
///  - <see cref="DeathBetrayal"/>: the death — his swords turn on him, he fends off two cuts, three blades pierce him
///    and stay in his body (sprites parented to the body) while he sinks to his knees (body clip "Dead_Swords",
///    tools/gen_combat_scene.py; the timings below match its keys). ≤ 2.4 s.
/// Presentation only; every entry point swallows its errors.
/// UNVERIFIED in game: positions against very large enemies, the pierce points after the body art is replaced.
/// </summary>
public static partial class SwordVisuals
{
    // ------------------------------------------------------------------ guard: an X of two blades

    private static readonly Dictionary<ulong, ulong> LastGuard = new();
    private static readonly Dictionary<Node2D, ulong> GuardAt = new();

    /// <summary>
    /// Block gained (<paramref name="hit"/> false) or a hit fully blocked (true). <paramref name="attacker"/> decides the
    /// side (enemies stand to the right, so that is the default). Only one guard per 0.45 s.
    /// </summary>
    public static void Guard(Player player, Creature? attacker, bool hit)
    {
        try { GuardInner(player, attacker, hit); }
        catch (Exception e) { MainFile.Logger.Warn($"[SwordVisuals] guard failed: {e.Message}"); }
    }

    private static void GuardInner(Player player, Creature? attacker, bool hit)
    {
        var rig = GetRig(player, create: false);
        if (rig == null || rig.Dead || rig.Swords.Count == 0) return;
        var now = Time.GetTicksMsec();
        if (LastGuard.TryGetValue(player.NetId, out var t0) && now - t0 < 450) return;

        var free = rig.Swords
            .Where(kv => GodotObject.IsInstanceValid(kv.Value) && !IsUnionBusy(kv.Value) &&
                         !(LastStrike.TryGetValue(kv.Value, out var s0) && now - s0 < 500))
            .OrderByDescending(kv => kv.Key == rig.Current).ThenBy(kv => (int)kv.Key)
            .Take(2).ToList();
        if (free.Count == 0) return;
        LastGuard[player.NetId] = now;

        var side = 1f;
        var room = NCombatRoom.Instance;
        if (attacker != null && room?.GetCreatureNode(attacker) is { } enemy && room.GetCreatureNode(player.Creature) is { } me)
            side = enemy.GlobalPosition.X >= me.GlobalPosition.X ? 1f : -1f;

        var cross = new Vector2(side * 185f, -265f);       // in front of his chest, on the attacker's side
        var meet = cross + new Vector2(side * 48f, -4f);   // pushed forward to meet the blow
        var arrive = hit ? 0.09 : 0.14;

        for (var i = 0; i < 2; i++)
        {
            var real = i < free.Count;
            var sword = free[Math.Min(i, free.Count - 1)].Key;
            Node2D node;
            Vector2 home;
            if (real)
            {
                node = free[i].Value;
                if (!rig.Slots.TryGetValue(sword, out home)) home = node.Position;
                if (rig.Moves.TryGetValue(sword, out var mv) && GodotObject.IsInstanceValid(mv)) mv.Kill();
            }
            else
            {
                // only one sword out: its spectral twin makes the other half of the X
                node = CreateSword(sword, player, spectral: true);
                rig.Root.AddChild(node);
                home = free[0].Value.Position;
                node.Position = home;
                node.Scale = free[0].Value.Scale;
                node.Modulate = new Color(0.75f, 0.85f, 1f, 0f);
            }

            GuardAt[node] = now; // in flight: a Sync during the guard leaves it alone (a strike may still take it over)
            var homeZ = real && rig.Current != sword ? node.ZIndex : ZFront;
            node.ZIndex = Math.Max(homeZ, ZBack); // passes behind him on the way if it rests behind him
            var rot = (i == 0 ? 0.62f : -0.62f) * side;
            var look = real ? Colors.White : new Color(0.75f, 0.85f, 1f, 0.6f);
            var tw = node.CreateTween();
            if (real) rig.Moves[sword] = tw;
            tw.TweenProperty(node, "position", cross, arrive).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(node, "rotation", rot, arrive);
            tw.Parallel().TweenProperty(node, "scale", Vector2.One * 1.05f, arrive);
            tw.Parallel().TweenProperty(node, "modulate", look, arrive);
            tw.TweenCallback(Callable.From(() => node.ZIndex = ZFlying));
            tw.TweenProperty(node, "position", meet, 0.08).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            if (i == 0)
            {
                var col = ColorOf(sword);
                var root = rig.Root;
                tw.TweenCallback(Callable.From(() => SwordFx.Clash(root, meet + new Vector2(side * 14f, -10f), col)));
            }
            tw.TweenProperty(node, "position", cross + new Vector2(side * 14f, 0), 0.1).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tw.TweenInterval(0.06);
            tw.TweenCallback(Callable.From(() => node.ZIndex = homeZ));
            if (real)
            {
                tw.TweenProperty(node, "position", home, 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", 0f, 0.22);
                tw.TweenCallback(Callable.From(() => Layout(rig, only: sword)));
            }
            else
            {
                tw.TweenProperty(node, "position", home, 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "modulate:a", 0f, 0.22);
                tw.TweenCallback(Callable.From(node.QueueFree));
            }
        }
    }

    // ------------------------------------------------------------------ Onimaru's own attacks

    /// <summary>Where a sword aims at <paramref name="target"/> (its chest), in rig space; null without a node.</summary>
    private static Vector2? AimAt(Rig rig, Creature target)
    {
        var node = NCombatRoom.Instance?.GetCreatureNode(target);
        return node == null ? null : rig.Root.ToLocal(node.GlobalPosition + new Vector2(0, -130));
    }

    /// <summary>
    /// Onimaru attacks <paramref name="targets"/> with one attack of <paramref name="kind"/> (called by
    /// OnimaruAttack.Perform right before the damage). 거합: a drawing cut straight through; 퇴마: from above, downward;
    /// 베기: one level sweep through every enemy; 난무: called once per hit, each call cuts from wherever the katana is.
    /// The damage lands immediately, so the outward flight is very short (0.06–0.1 s); the way back is unhurried.
    /// </summary>
    /// <returns>false when nothing was played (no Onimaru on screen, or a 【조합】 choreography is moving it)</returns>
    public static bool OnimaruStrike(Player player, OnimaruKind kind, IReadOnlyList<Creature> targets)
    {
        try { return OnimaruStrikeInner(player, kind, targets); }
        catch (Exception e) { MainFile.Logger.Warn($"[SwordVisuals] Onimaru strike failed: {e.Message}"); return false; }
    }

    private static bool OnimaruStrikeInner(Player player, OnimaruKind kind, IReadOnlyList<Creature> targets)
    {
        if (kind == OnimaruKind.Goei) return false; // a guard, not an attack (the Block hook crosses the swords)
        var rig = GetRig(player, create: false);
        if (rig == null || rig.Dead) return false;
        if (!rig.Swords.TryGetValue(SwordId.Onimaru, out var node) || !GodotObject.IsInstanceValid(node) || IsUnionBusy(node)) return false;
        if (!rig.Slots.TryGetValue(SwordId.Onimaru, out var home)) home = node.Position;
        var aims = targets.Select(t => AimAt(rig, t)).OfType<Vector2>().ToList();
        if (aims.Count == 0) aims.Add(home + new Vector2(520, 40));

        if (rig.Moves.TryGetValue(SwordId.Onimaru, out var mv) && GodotObject.IsInstanceValid(mv)) mv.Kill();
        var from = node.Position; // 난무 chains: the next cut starts wherever the last one left the katana
        node.ZIndex = ZFlying;
        node.Modulate = Colors.White;
        LastStrike[node] = Time.GetTicksMsec();
        var tw = node.CreateTween();
        rig.Moves[SwordId.Onimaru] = tw;
        var col = ColorOf(SwordId.Onimaru);
        Trail(node, 0.5, col);
        Sfx.Whoosh(SwordId.Onimaru);
        var hitTargets = targets.ToList();
        void Impact() { foreach (var t in hitTargets) SwordFx.ImpactWith(t, SwordId.Onimaru); }

        Vector2 end;
        var aim = aims[0];
        switch (kind)
        {
            case OnimaruKind.Giri: // held level, one sweep through all of them
            {
                var y = aims.Average(a => a.Y);
                var start = new Vector2(aims.Min(a => a.X) - 200f, y - 20f);
                end = new Vector2(aims.Max(a => a.X) + 200f, y + 10f);
                tw.TweenProperty(node, "rotation", Mathf.Pi / 2, 0.04);
                tw.Parallel().TweenProperty(node, "position", start, 0.06).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                tw.TweenCallback(Callable.From(Impact));
                tw.TweenProperty(node, "position", end, 0.13).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
                break;
            }
            case OnimaruKind.Taima: // from high above, a downward cut
            {
                var above = aim + new Vector2(-70f, -250f);
                end = aim + new Vector2(70f, 110f);
                tw.TweenProperty(node, "position", above, 0.06).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", PointAt(above, end), 0.06);
                tw.TweenProperty(node, "position", end, 0.07).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
                tw.TweenCallback(Callable.From(Impact));
                break;
            }
            default: // 거합 / 난무: straight through the target
            {
                var dir = (aim - from).Normalized();
                if (dir == Vector2.Zero) dir = Vector2.Right;
                end = aim + dir * (kind == OnimaruKind.Ranbu ? 110f : 140f);
                tw.TweenProperty(node, "rotation", PointAt(from, aim), 0.03);
                tw.TweenProperty(node, "position", end, kind == OnimaruKind.Ranbu ? 0.06 : 0.07)
                    .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
                tw.TweenCallback(Callable.From(Impact));
                break;
            }
        }

        tw.TweenInterval(kind == OnimaruKind.Ranbu ? 0.14 : 0.1);
        var mid = (end + home) / 2 + new Vector2(0, -170f);
        tw.TweenMethod(Callable.From<float>(u => node.Position = Bezier(end, mid, home, u)), 0f, 1f, 0.34)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.Parallel().TweenProperty(node, "rotation", 0f, 0.34);
        tw.TweenCallback(Callable.From(() => Layout(rig, only: SwordId.Onimaru)));
        return true;
    }

    // ------------------------------------------------------------------ death: his own swords turn on him

    /// <summary>
    /// Ensifer dies (MotionDirector, when the game starts the "Dead" clip). The present swords — or, if fewer than three,
    /// spectral copies of the other swords he owns / of the present ones — circle him, two cut at him (he fends off the
    /// first, the second gets him from behind), then three pierce him and stay in his body. Swords beyond three fall
    /// point-down into the ground. Nothing moves the swords afterwards (<see cref="Rig.Dead"/>).
    /// </summary>
    public static void DeathBetrayal(Player player)
    {
        try { DeathInner(player); }
        catch (Exception e) { MainFile.Logger.Warn($"[SwordVisuals] death choreography failed: {e.Message}"); }
    }

    /// <summary>The animated body node ("Visuals" inside the character scene); its local origin is the body centre.</summary>
    private static Node2D? BodyNode(Player player) =>
        NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals?.GetNodeOrNull<Node2D>("Visuals");

    // Pierce points in the body node's space (scenes/magic_swordsman_combat.tscn: origin = body centre, 200 px above
    // the feet; chest gem at about (-8, -114)) and the direction each blade travels when it goes in.
    private static readonly (Vector2 Point, Vector2 Dir, bool Behind)[] PierceSpots =
    {
        (new Vector2(4, -28), new Vector2(-0.95f, -0.3f), false),   // A: belly, from the right (the enemy side)
        (new Vector2(-30, -95), new Vector2(0.65f, 0.76f), true),   // B: shoulder blade, from above behind him
        (new Vector2(-6, -108), new Vector2(-0.85f, 0.52f), false), // C: chest, from the upper right
    };

    private static readonly Vector2 DeathCenter = new(0, -260);
    private static ulong _deathStart;

    private static void DeathInner(Player player)
    {
        var rig = GetRig(player, create: true);
        if (rig == null || rig.Dead) return;
        rig.Dead = true;
        _deathStart = Time.GetTicksMsec();
        foreach (var m in rig.Moves.Values)
            if (GodotObject.IsInstanceValid(m)) m.Kill();
        rig.Moves.Clear();

        var present = rig.Swords
            .Where(kv => GodotObject.IsInstanceValid(kv.Value))
            .OrderByDescending(kv => kv.Key == rig.Current).ThenBy(kv => (int)kv.Key)
            .Select(kv => (Sword: kv.Key, Node: kv.Value)).ToList();
        var blades = present.Take(3).ToList();
        var extra = present.Skip(3).ToList();
        if (blades.Count < 3)
        {
            var pool = (player.GetRelic<Mangeomchong>()?.OwnedSwords ?? []).Where(s => !rig.Swords.ContainsKey(s))
                .Concat(present.Select(p => p.Sword)).Concat(player.GetRelic<Mangeomchong>()?.OwnedSwords ?? [])
                .ToList();
            foreach (var s in pool)
            {
                if (blades.Count >= 3) break;
                var ghost = CreateSword(s, player, spectral: true);
                rig.Root.AddChild(ghost);
                ghost.Position = SpawnPos;
                ghost.Scale = Vector2.One * 0.95f;
                ghost.Modulate = new Color(0.8f, 0.82f, 1f, 0f);
                blades.Add((s, ghost));
            }
        }
        if (blades.Count == 0) return; // no swords at all: the body clip alone

        var body = BodyNode(player);
        for (var i = 0; i < blades.Count; i++)
            DeathBlade(rig, body, blades[i].Sword, blades[i].Node, i, blades.Count);
        foreach (var (_, node) in extra) FallIntoGround(node);
    }

    /// <summary>
    /// One blade of the death. Roles by index: 0 = cut, parried at 0.6 s, pierces the belly at 1.6 s; 1 = cuts him from
    /// behind at 1.0 s, pierces the shoulder blade at 1.8 s; 2 = pierces the chest at 1.4 s.
    /// </summary>
    private static void DeathBlade(Rig rig, Node2D? body, SwordId sword, Node2D node, int role, int count)
    {
        var col = ColorOf(sword);
        var spectral = node.Modulate.A < 0.01f;
        var look = spectral ? new Color(0.8f, 0.82f, 1f, 0.75f) : Colors.White;
        var th0 = -Mathf.Pi / 2 + Mathf.Tau * role / Math.Max(3, count);
        Vector2 Orbit(float th) => DeathCenter + new Vector2(Mathf.Cos(th) * 240f, Mathf.Sin(th) * 120f);
        node.ZIndex = ZFlying;
        var tw = node.CreateTween();

        // they leave their places and circle him, tips turned on him (0 - 0.15 - leave)
        var startTh = th0;
        tw.TweenProperty(node, "position", Orbit(startTh), 0.15).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", PointAt(Orbit(startTh), DeathCenter), 0.15);
        tw.Parallel().TweenProperty(node, "modulate", look, 0.15);
        tw.Parallel().TweenProperty(node, "scale", Vector2.One * 1.05f, 0.15);
        var leave = role switch { 0 => 0.5f, 1 => 0.88f, _ => 1.27f };
        const float speed = 2.6f; // radians per second
        var endTh = startTh + speed * (leave - 0.15f);
        tw.TweenMethod(Callable.From<float>(th =>
        {
            node.Position = Orbit(th);
            node.Rotation = PointAt(node.Position, DeathCenter);
            node.ZIndex = Mathf.Sin(th) > 0 ? ZFlying : ZBack; // passes behind him on the far side
        }), startTh, endTh, leave - 0.15f);
        var circled = Orbit(endTh);
        tw.TweenCallback(Callable.From(() => node.ZIndex = ZFlying));

        switch (role)
        {
            case 0:
            {
                // a cut at him from the front: he parries (body clip, 0.6 s) and it is thrown back
                var clash = new Vector2(105, -300);
                tw.TweenProperty(node, "rotation", PointAt(circled, DeathCenter), 0.03);
                tw.TweenProperty(node, "position", clash, 0.07).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
                tw.TweenCallback(Callable.From(() => SwordFx.Clash(rig.Root, clash + new Vector2(-18, 0), col)));
                var thrown = new Vector2(310, -400);
                tw.TweenProperty(node, "position", thrown, 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", PointAt(thrown, DeathCenter) - Mathf.Tau, 0.22);
                tw.TweenInterval(1.47 - 0.82);
                Pierce(tw, rig, body, node, thrown, PierceSpots[0], 0.13, spectral);
                break;
            }
            case 1:
            {
                // gets him from behind (body clip: stagger at 1.0 s), then pulls back
                var cut = new Vector2(-75, -285);
                tw.TweenProperty(node, "rotation", PointAt(circled, cut + new Vector2(60, 20)), 0.03);
                tw.TweenProperty(node, "position", cut, 0.08).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
                tw.TweenCallback(Callable.From(() => SwordFx.Pierce(rig.Root, cut + new Vector2(20, 5))));
                var back = new Vector2(-300, -440);
                tw.TweenProperty(node, "position", back, 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", PointAt(back, DeathCenter), 0.2);
                tw.TweenInterval(1.67 - 1.19);
                Pierce(tw, rig, body, node, back, PierceSpots[1], 0.13, spectral);
                break;
            }
            default:
                Pierce(tw, rig, body, node, circled, PierceSpots[2], 0.13, spectral);
                break;
        }
    }

    /// <summary>The blade dives from <paramref name="from"/> into the body and is replaced by a sprite stuck in it.</summary>
    private static void Pierce(Tween tw, Rig rig, Node2D? body, Node2D node, Vector2 from,
        (Vector2 Point, Vector2 Dir, bool Behind) spot, double dur, bool spectral)
    {
        var dir = spot.Dir.Normalized();
        var half = SwordSpriteHeight * 0.5f * 1.05f;
        const float depth = 0.3f; // the part of the blade that disappears into him (fraction of its length)
        Vector2 Contact() => body != null && GodotObject.IsInstanceValid(body)
            ? rig.Root.ToLocal(body.ToGlobal(spot.Point))
            : DeathCenter + spot.Point + new Vector2(0, 60);
        // holder centre when the tip is depth*length past the contact point
        Vector2 Target() => Contact() - dir * (half - SwordSpriteHeight * 1.05f * depth);
        tw.TweenProperty(node, "rotation", PointAt(Vector2.Zero, dir), 0.04);
        tw.TweenMethod(Callable.From<float>(u => node.Position = from.Lerp(Target(), u * u)), 0f, 1f, dur);
        tw.TweenCallback(Callable.From(() =>
        {
            try
            {
                SwordFx.Pierce(rig.Root, Contact());
                Shake(MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.VeryWeak);
                if (body == null || !GodotObject.IsInstanceValid(body)) return;
                var stuck = StuckSword(rig.Root, node, body, spot.Point, dir, spot.Behind, depth, spectral);
                if (stuck == null) return;
                node.Visible = false;
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[SwordVisuals] stuck sword: {e.Message}");
            }
        }));
    }

    /// <summary>
    /// A copy of the sword stuck in the body. It lives in the rig (so it does not take on the body's hurt / death tint)
    /// and follows the body through a RemoteTransform2D on the body node, so it goes down with him. In front of the body
    /// the tip part of the texture is cut away (it is inside him); behind the body (z 0, first in the rig = behind the
    /// body sprites) the whole blade is drawn and the body itself hides the tip.
    /// </summary>
    private static Node2D? StuckSword(Node2D rigRoot, Node2D flying, Node2D body, Vector2 point, Vector2 dir, bool behind,
        float depth, bool spectral)
    {
        var sprite = flying.GetNodeOrNull<Node2D>("Blade")?.GetChildren().OfType<Sprite2D>()
            .FirstOrDefault(s => s.Name != "Aura" && s.Texture != null && !s.RegionEnabled);
        if (sprite?.Texture is not { } tex) return null;
        float w = tex.GetWidth(), h = tex.GetHeight();
        var stuck = new Node2D
        {
            Name = "StuckSword", Scale = flying.Scale, ZIndex = behind ? ZBack : ZFlying,
            Modulate = spectral ? new Color(0.85f, 0.87f, 1f, 0.85f) : Colors.White,
        };
        var cut = behind ? 0f : depth;
        stuck.AddChild(new Sprite2D
        {
            Texture = tex, Centered = false, TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            RegionEnabled = true, RegionRect = new Rect2(0, h * cut, w, h * (1 - cut)),
            Offset = new Vector2(-w / 2f, behind ? -h * depth : 0f),
            Scale = sprite.Scale, Modulate = sprite.Modulate,
        });
        rigRoot.AddChild(stuck);
        if (behind) rigRoot.MoveChild(stuck, 0);
        var anchor = new RemoteTransform2D
        {
            Name = "StuckSwordAnchor", Position = point, Rotation = PointAt(Vector2.Zero, dir), UpdateScale = false,
        };
        body.AddChild(anchor);
        anchor.RemotePath = anchor.GetPathTo(stuck);
        anchor.ForceUpdateCache();
        stuck.GlobalPosition = anchor.GlobalPosition;
        stuck.GlobalRotation = anchor.GlobalRotation;
        // dimmed with the body once he has gone down
        var dim = stuck.CreateTween();
        dim.TweenInterval(Math.Max(0.0, 2.1 - (Time.GetTicksMsec() - _deathStart) / 1000.0));
        dim.TweenProperty(stuck, "modulate", stuck.Modulate * new Color(0.78f, 0.76f, 0.84f), 0.3);
        return stuck;
    }

    /// <summary>A sword beyond the three that pierce him: drops point-down into the ground beside him and stays.</summary>
    private static void FallIntoGround(Node2D node)
    {
        var x = node.Position.X < 0 ? -150f - 40f * GD.Randf() : 140f + 40f * GD.Randf();
        var tw = node.CreateTween();
        tw.TweenInterval(1.85);
        tw.TweenProperty(node, "rotation", Mathf.Pi + (GD.Randf() - 0.5f) * 0.3f, 0.15);
        tw.TweenProperty(node, "position", new Vector2(x, -70), 0.18).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.TweenProperty(node, "modulate", new Color(0.6f, 0.58f, 0.66f), 0.4);
    }
}
