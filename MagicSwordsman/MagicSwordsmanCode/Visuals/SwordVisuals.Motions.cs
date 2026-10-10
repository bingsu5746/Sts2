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
///  - <see cref="Guard"/>: gaining Block / a fully blocked hit — the free swords form a guard on the attacker's side whose
///    shape depends on how many there are (one: a lone parry, or a heavy blade planted point-down like a wall; two: an
///    X; three: a fan; four or more: a slowly turning ring of blades), with a flourish of the lead sword's own
///    (SwordFx.GuardFx). Wind-up, a fast snap, one impact frame, a wobble, a soft return (≈ 0.7 s).
///  - <see cref="OnimaruStrike"/>: Onimaru's own attacks (【명령】, auto attacks, kind cards) fly the katana to the
///    target and back. Those attacks deal Unpowered damage without the game's "Attack" trigger, so the generic
///    <see cref="Strike"/> never saw them (OnimaruAttack.Perform calls this directly).
///  - <see cref="DeathBetrayal"/>: the death — his swords (and spectral copies, six in all) turn on him, he parries two
///    cuts, then the blades drive into him one at a time and stay in his body (sprites that follow the body) while he
///    drops onto his knees and stays kneeling upright, head bowed (body clip "Dead_Swords", tools/gen_combat_scene.py;
///    the timings below match its keys). ≈ 2.8 s.
/// Presentation only; every entry point swallows its errors.
/// UNVERIFIED in game: positions against very large enemies, the pierce points after the body art is replaced.
/// </summary>
public static partial class SwordVisuals
{
    // ------------------------------------------------------------------ guard: formations by count, flourish by sword

    private static readonly Dictionary<ulong, ulong> LastGuard = new();
    private static readonly Dictionary<Node2D, ulong> GuardAt = new();

    /// <summary>How the guarding blades stand (user request 2026-10-10 "검마다 다르게, 검의 개수마다 다르게").</summary>
    private enum GuardShape
    {
        Solo,  // one light blade: spins once and stops on a diagonal block
        Plant, // one heavy blade: drops point-first into the ground before him like a wall
        Cross, // two: the X
        Fan,   // three: a fan of blades opening upward from one point
        Ring,  // four or more: a ring of blades in front of him, points outward, turning slowly
    }

    private static bool HeavyBlade(SwordId s) =>
        s is SwordId.Gram or SwordId.Durandal or SwordId.Tyrfing or SwordId.Caladbolg or SwordId.Dainsleif;

    /// <summary>
    /// Block gained (<paramref name="hit"/> false) or a hit fully blocked (true). <paramref name="attacker"/> decides the
    /// side (enemies stand to the right, so that is the default). Only one guard per 0.45 s.
    /// </summary>
    public static void Guard(Player player, Creature? attacker, bool hit)
    {
        try { GuardInner(player, attacker, hit); }
        catch (Exception e) { MainFile.Logger.Warn($"[SwordVisuals] guard failed: {e.Message}"); }
    }

    // Timing (s, from the call). The blades wind up (anticipation), snap into the formation fast (contrast), meet the
    // blow in one frame (all of them arrive together: Clash + the lead sword's flourish, SwordFx.GuardFx), ride the
    // blow with a decaying wobble, then unwind home on a soft overshooting arc (follow-through). ≈ 0.7 s in all.
    private const double GuardWindHit = 0.05, GuardWindBlock = 0.08;   // anticipation
    private const double GuardSnapHit = 0.10, GuardSnapBlock = 0.12;   // into the formation (blade 0)
    private const double GuardPush = 0.07;                              // gaining Block: the push forward that meets the blow
    private const double GuardHold = 0.26, GuardHome = 0.3;

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
            .Take(6).ToList();
        if (free.Count == 0) return;
        var lead = free[0].Key;
        // the twin blades stand together: Moye right after Ganjiang (or the reverse) when both are out
        var twin = lead switch { SwordId.Ganjiang => SwordId.Moye, SwordId.Moye => SwordId.Ganjiang, _ => (SwordId?)null };
        var ti = twin is { } tw0 ? free.FindIndex(kv => kv.Key == tw0) : -1;
        if (ti > 1) { var x = free[ti]; free.RemoveAt(ti); free.Insert(1, x); }
        // Onimaru guards alone: one precise parry, whatever else is out
        if (lead == SwordId.Onimaru) free = free.Take(1).ToList();
        LastGuard[player.NetId] = now;

        var side = 1f;
        var room = NCombatRoom.Instance;
        if (attacker != null && room?.GetCreatureNode(attacker) is { } enemy && room.GetCreatureNode(player.Creature) is { } me)
            side = enemy.GlobalPosition.X >= me.GlobalPosition.X ? 1f : -1f;

        var n = free.Count;
        var shape = n switch
        {
            1 => HeavyBlade(lead) ? GuardShape.Plant : GuardShape.Solo,
            2 => GuardShape.Cross,
            3 => GuardShape.Fan,
            _ => GuardShape.Ring,
        };

        var wind = hit ? GuardWindHit : GuardWindBlock;
        var snap = hit ? GuardSnapHit : GuardSnapBlock;
        // the blow lands when the blades arrive (blocked hit) or at the end of their push forward (gaining Block); a planted
        // blade takes it the moment it bites into the ground
        var impactAt = wind + snap + (hit || shape == GuardShape.Plant ? 0 : GuardPush);
        var impactPos = GuardImpactPoint(shape, side);
        var leadCol = ColorOf(lead);
        var fxClock = rig.Root.CreateTween();
        fxClock.TweenInterval(impactAt);
        fxClock.TweenCallback(Callable.From(() =>
        {
            SwordFx.GuardFx(rig.Root, impactPos, lead, hit, side);
            if (shape == GuardShape.Plant) SwordFx.GroundBite(rig.Root, GuardPose(shape, 0, 1, side, 0).Pos + new Vector2(0, 100), leadCol);
            if (hit) Shake(lead == SwordId.Gram ? MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Weak
                : MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.VeryWeak);
            if (lead == SwordId.Skofnung)
                foreach (var (_, nd) in free)
                    if (GodotObject.IsInstanceValid(nd)) GuardGhosts(nd, side);
        }));

        for (var i = 0; i < n; i++)
        {
            var (sword, node) = free[i];
            if (!rig.Slots.TryGetValue(sword, out var home)) home = node.Position;
            if (rig.Moves.TryGetValue(sword, out var mv) && GodotObject.IsInstanceValid(mv)) mv.Kill();
            GuardAt[node] = now; // in flight: a Sync during the guard leaves it alone (a strike may still take it over)
            var homeZ = rig.Current == sword ? ZFront : node.ZIndex;
            var homeScale = node.Scale;
            var lag = Math.Min(i * 0.02, 0.06);
            var snapI = Math.Max(0.05, snap - lag); // every blade arrives on the same frame
            var target = GuardPose(shape, i, n, side, 0);
            var scale = Vector2.One * GuardScale(shape, rig.Current == sword);

            var startPos = node.Position;
            var startRot = node.Rotation;
            // anticipation: drawn back away from the blow and cocked the other way
            var windPos = shape == GuardShape.Plant
                ? startPos + new Vector2(side * 20f, -70f)
                : startPos + new Vector2(-side * 22f, 12f);
            var windRot = shape == GuardShape.Plant ? Mathf.Pi + side * 0.5f : startRot - side * 0.3f;
            var ctrl = shape == GuardShape.Plant
                ? new Vector2(target.Pos.X, windPos.Y - 60f)
                : (windPos + target.Pos) * 0.5f + new Vector2(side * 30f, -70f);
            var spin = shape == GuardShape.Solo ? -side * Mathf.Tau : 0f; // the lone blade twirls once on its way in

            var tw = node.CreateTween();
            rig.Moves[sword] = tw;
            if (lag > 0) tw.TweenInterval(lag);
            tw.TweenProperty(node, "position", windPos, wind).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(node, "rotation", windRot, wind).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(node, "modulate", Colors.White, wind);
            tw.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(node)) node.ZIndex = target.Z; }));
            tw.TweenMethod(Callable.From<float>(u =>
            {
                if (!GodotObject.IsInstanceValid(node)) return;
                node.Position = Bezier(windPos, ctrl, target.Pos, u);
                node.Rotation = Mathf.LerpAngle(windRot, target.Rot, u) + spin * (1 - u);
            }), 0f, 1f, snapI)
                .SetTrans(shape == GuardShape.Plant ? Tween.TransitionType.Quad : Tween.TransitionType.Cubic)
                .SetEase(shape == GuardShape.Plant ? Tween.EaseType.In : Tween.EaseType.Out);
            tw.Parallel().TweenProperty(node, "scale", scale, snapI);

            // the blow: knocked back with a decaying wobble (hit), or a push forward that meets it (gaining Block)
            var idx = i;
            var push = !hit && shape != GuardShape.Plant;
            var hold = GuardHold + (push ? GuardPush : 0);
            var sign = i % 2 == 0 ? 1f : -1f;
            tw.TweenMethod(Callable.From<float>(t =>
            {
                if (!GodotObject.IsInstanceValid(node)) return;
                var (off, kick) = GuardRide(shape, hit, t);
                var p = GuardPose(shape, idx, n, side, GuardRingTurn(shape, hit, t, hold) * side);
                node.Position = p.Pos + new Vector2(side * off, Math.Abs(off) * 0.12f);
                node.Rotation = p.Rot + side * kick * sign;
            }), 0f, (float)hold, hold);

            // follow-through: home on an arc that dips below the straight line, overshooting a little before it settles
            tw.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(node)) node.ZIndex = homeZ; }));
            var endPose = GuardPose(shape, i, n, side, GuardRingTurn(shape, hit, (float)hold, hold) * side);
            var homeCtrl = (endPose.Pos + home) * 0.5f + new Vector2(-side * 20f, 50f);
            var endRot = endPose.Rot;
            tw.TweenMethod(Callable.From<float>(u =>
            {
                if (!GodotObject.IsInstanceValid(node)) return;
                node.Position = Bezier(endPose.Pos, homeCtrl, home, u);
                node.Rotation = Mathf.LerpAngle(endRot, 0f, Mathf.Clamp(u, 0f, 1f));
            }), 0f, 1f, GuardHome).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(node, "scale", homeScale, GuardHome);
            var s = sword;
            tw.TweenCallback(Callable.From(() => Layout(rig, only: s)));
        }
    }

    /// <summary>Where blade <paramref name="i"/> of <paramref name="n"/> stands in the formation (rig space).</summary>
    private static (Vector2 Pos, float Rot, int Z) GuardPose(GuardShape shape, int i, int n, float side, float turn)
    {
        var c = new Vector2(side * 150f, -268f); // in front of his chest, on the attacker's side
        switch (shape)
        {
            case GuardShape.Plant:
                // point down, bitten into the ground, the hilt leaning toward the enemy
                return (new Vector2(side * 178f, -98f), Mathf.Pi + side * 0.12f, ZFlying);
            case GuardShape.Solo:
                return (c + new Vector2(side * 14f, -8f), side * 0.62f, ZFlying);
            case GuardShape.Cross:
                return i == 0
                    ? (c + new Vector2(side * 10f, -6f), side * 0.58f, ZFlying)
                    : (c + new Vector2(-side * 6f, 6f), -side * 0.52f, ZFront);
            case GuardShape.Fan:
            {
                var a = side * (i == 0 ? 0f : i == 1 ? -0.55f : 0.55f) + side * 0.1f;
                var pivot = new Vector2(side * 150f, -150f);
                return (pivot + Vector2.Up.Rotated(a) * 102f, a, i == 0 ? ZFlying : ZFront);
            }
            default:
            {
                var center = new Vector2(side * 168f, -272f);
                var th = -Mathf.Pi / 2 + Mathf.Tau * i / n + turn;
                var pos = center + new Vector2(Mathf.Cos(th) * 80f, Mathf.Sin(th) * 128f);
                return (pos, PointAt(center, pos), ZFlying);
            }
        }
    }

    private static float GuardScale(GuardShape shape, bool current) => shape switch
    {
        GuardShape.Plant => 1.2f,
        GuardShape.Solo => 1.15f,
        GuardShape.Cross => current ? 1.15f : 1.05f,
        GuardShape.Fan => current ? 1.05f : 0.95f,
        _ => current ? 0.95f : 0.85f,
    };

    private static Vector2 GuardImpactPoint(GuardShape shape, float side) => shape switch
    {
        GuardShape.Plant => new Vector2(side * 200f, -175f),
        GuardShape.Solo => new Vector2(side * 192f, -300f),
        GuardShape.Cross => new Vector2(side * 172f, -282f),
        GuardShape.Fan => new Vector2(side * 185f, -250f),
        _ => new Vector2(side * 200f, -272f),
    };

    /// <summary>A pulse that rises to 1 at <paramref name="tau"/> and dies away: (t/τ)·e^(1 − t/τ).</summary>
    private static float Pulse(float t, float tau) => t <= 0 ? 0 : t / tau * Mathf.Exp(1 - t / tau);

    /// <summary>
    /// The blades riding the blow, <paramref name="t"/> s after they arrive: (offset toward the enemy in px, extra tilt).
    /// A blocked hit knocks them back and they wobble to rest; gaining Block they push forward to meet it. A planted
    /// blade does not slide, it only quivers.
    /// </summary>
    private static (float Off, float Kick) GuardRide(GuardShape shape, bool hit, float t)
    {
        var amp = shape switch { GuardShape.Plant => 5f, GuardShape.Solo => 30f, GuardShape.Cross => 26f, GuardShape.Fan => 22f, _ => 16f };
        var tilt = shape == GuardShape.Plant ? 0.12f : 0.18f;
        if (hit || shape == GuardShape.Plant)
        {
            var w = Pulse(t, 0.05f) * Mathf.Cos(Math.Max(0, t - 0.05f) * 22f);
            return (-amp * w, -tilt * w);
        }
        var push = Pulse(t, (float)GuardPush);
        var quiver = Pulse(t - (float)GuardPush, 0.04f) * Mathf.Cos(Math.Max(0, t - (float)GuardPush) * 24f);
        return (amp * push - amp * 0.35f * quiver, tilt * 0.5f * push - tilt * 0.6f * quiver);
    }

    /// <summary>The ring turns slowly while it holds (and is jolted round by a blocked hit); other shapes do not turn.</summary>
    private static float GuardRingTurn(GuardShape shape, bool hit, float t, double hold)
    {
        if (shape != GuardShape.Ring) return 0f;
        var turn = 0.45f * Mathf.SmoothStep(0, 1, t / (float)hold);
        return hit ? turn + 0.3f * Pulse(t, 0.05f) : turn;
    }

    /// <summary>Skofnung: two pale copies of the blade linger behind it and fade (its ghostly afterimages).</summary>
    private static void GuardGhosts(Node2D node, float side)
    {
        if (node.GetParent() is not Node2D parent || node.GetNodeOrNull<Node2D>("Blade") is not { } blade) return;
        for (var k = 1; k <= 2; k++)
        {
            var ghost = new Node2D
            {
                Position = node.Position + new Vector2(-side * 20f * k, 6f * k), Rotation = node.Rotation - side * 0.08f * k,
                Scale = node.Scale, ZIndex = ZFront, Modulate = new Color(0.75f, 0.9f, 1f, 0.42f / k),
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            };
            foreach (var child in blade.GetChildren())
                if (child is Sprite2D sp && sp.Name != "Aura")
                    ghost.AddChild(new Sprite2D
                    {
                        Texture = sp.Texture, Scale = sp.Scale, Position = sp.Position, Rotation = sp.Rotation,
                        RegionEnabled = sp.RegionEnabled, RegionRect = sp.RegionRect, UseParentMaterial = true,
                    });
            parent.AddChild(ghost);
            var fade = ghost.CreateTween();
            fade.TweenProperty(ghost, "position", ghost.Position + new Vector2(-side * 26f * k, 0), 0.4)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            fade.Parallel().TweenProperty(ghost, "modulate:a", 0f, 0.4);
            fade.TweenCallback(Callable.From(ghost.QueueFree));
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
    /// Ensifer dies (MotionDirector, when the game starts the "Dead" clip). User request 2026-10-10 "죽을 때 ... 내가 보내준
    /// 사진처럼 쓰러졌으면 좋겠고, 검들이 좀 더 잘 보이게": six blades — the swords on screen, then spectral copies of the
    /// other swords he owns (or of the present ones) — circle him with their points turned on him. He parries two of them
    /// (0.6 s, 0.92 s); then they come one at a time, each one aiming, drawing back and driving in (1.12, 1.34, 1.56 s);
    /// his knees give and he drops onto them, and the last three go in while he kneels (2.0, 2.18, 2.36 s). He ends
    /// kneeling upright, head bowed, arms hanging, the blades standing out of him at every angle — through the chest,
    /// through the back, into the side and the shoulders (body clip "Dead_Swords", tools/gen_combat_scene.py; the times
    /// match its keys). Swords beyond six fall point-down into the ground. Nothing moves the swords afterwards
    /// (<see cref="Rig.Dead"/>). ≈ 2.8 s.
    /// </summary>
    public static void DeathBetrayal(Player player)
    {
        try { DeathInner(player); }
        catch (Exception e) { MainFile.Logger.Warn($"[SwordVisuals] death choreography failed: {e.Message}"); }
    }

    /// <summary>The animated body node ("Visuals" inside the character scene); its local origin is the body centre.</summary>
    private static Node2D? BodyNode(Player player) =>
        NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals?.GetNodeOrNull<Node2D>("Visuals");

    /// <summary>How a blade sits in him once it is in.</summary>
    private enum StuckKind
    {
        Front,            // in front of the body, the buried part cut away
        Behind,           // behind the body: the body hides the buried part
        ThroughFromFront, // went in at the front: hilt side in front, the point comes out behind him
        ThroughFromBack,  // went in at the back: hilt side behind him, the point comes out of his front
    }

    /// <summary>
    /// Where a blade goes in, in the body node's space (scenes/magic_swordsman_combat.tscn: origin = body centre, 200 px
    /// above the feet; head about y -190..-134, shoulders about y -128, chest gem about (-8, -114), he faces right, so his
    /// back is on the left), the direction it travels (= where its point goes), how it sits, and how much of its length
    /// passes the entry point. In order of the blows.
    /// </summary>
    private static readonly (Vector2 Point, Vector2 Dir, StuckKind Kind, float Depth)[] PierceSpots =
    {
        (new Vector2(2, -100), new Vector2(-0.86f, 0.5f), StuckKind.ThroughFromFront, 0.5f),  // chest, from the upper right
        (new Vector2(-44, -104), new Vector2(0.7f, 0.71f), StuckKind.ThroughFromBack, 0.55f), // back, point out of his belly
        (new Vector2(8, -40), new Vector2(-0.97f, -0.2f), StuckKind.Front, 0.22f),            // right side, level
        (new Vector2(6, -124), new Vector2(-0.4f, 0.92f), StuckKind.Front, 0.24f),            // right shoulder, from above
        (new Vector2(-36, -126), new Vector2(0.22f, 0.97f), StuckKind.Behind, 0.32f),         // left shoulder, from above behind
        (new Vector2(-48, -66), new Vector2(0.98f, 0.12f), StuckKind.Behind, 0.32f),          // small of the back, from behind
    };

    /// <summary>When each blow lands (s); the order of <see cref="PierceSpots"/>. Body clip Dead_Swords jolts on these.</summary>
    private static readonly float[] PierceAt = { 1.12f, 1.34f, 1.56f, 2.0f, 2.18f, 2.36f };

    /// <summary>The two parried cuts: (time, where the blades meet in rig space, where the parried blade is thrown).</summary>
    private static readonly (float At, Vector2 Clash, Vector2 Thrown)[] Parries =
    {
        (0.6f, new Vector2(105, -300), new Vector2(330, -430)),
        (0.92f, new Vector2(-112, -318), new Vector2(-340, -470)),
    };

    private const int DeathBlades = 6;
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
        var blades = present.Take(DeathBlades).ToList();
        var extra = present.Skip(DeathBlades).ToList();
        if (blades.Count < DeathBlades)
        {
            // spectral copies: first the owned swords that are not out, then the present ones again, then any owned
            var owned = player.GetRelic<Mangeomchong>()?.OwnedSwords ?? [];
            var pool = owned.Where(s => !rig.Swords.ContainsKey(s)).Concat(present.Select(p => p.Sword)).Concat(owned).ToList();
            for (var k = 0; blades.Count < DeathBlades && pool.Count > 0 && k < 64; k++)
            {
                var s = pool[k % pool.Count];
                var ghost = CreateSword(s, player, spectral: true);
                rig.Root.AddChild(ghost);
                ghost.Position = SpawnPos;
                ghost.Scale = Vector2.One;
                ghost.Modulate = new Color(0.8f, 0.82f, 1f, 0f);
                blades.Add((s, ghost));
            }
        }
        if (blades.Count == 0) return; // no swords at all: the body clip alone

        var body = BodyNode(player);
        // roles: blade 0 (the current sword) strikes first, through the chest; blades 1 and 2 are the parried cuts
        // and strike third and fourth; the rest fill the other blows in order
        int[] spotOf = { 0, 2, 3, 1, 4, 5 };
        for (var i = 0; i < blades.Count; i++)
            DeathBlade(rig, body, blades[i].Sword, blades[i].Node, i, blades.Count, spotOf[i]);
        foreach (var (_, node) in extra) FallIntoGround(node);
    }

    /// <summary>One blade of the death: circles him, maybe gets parried, then aims, draws back and drives in.</summary>
    private static void DeathBlade(Rig rig, Node2D? body, SwordId sword, Node2D node, int index, int count, int spot)
    {
        var col = ColorOf(sword);
        var spectral = node.Modulate.A < 0.01f;
        var look = spectral ? new Color(0.86f, 0.88f, 1f, 0.9f) : Colors.White;
        var scale = Vector2.One * (spectral ? 1.22f : 1.3f); // larger than in the layout: they must read in the corpse pose
        var th0 = -Mathf.Pi / 2 + Mathf.Tau * index / Math.Max(3, count);
        Vector2 Orbit(float th) => DeathCenter + new Vector2(Mathf.Cos(th) * 270f, Mathf.Sin(th) * 130f);
        const float speed = 2.2f; // radians per second
        node.ZIndex = ZFlying;
        var tw = node.CreateTween();
        var clock = 0.0;
        void Until(double t)
        {
            if (t > clock + 1e-4) tw.TweenInterval(t - clock);
            clock = Math.Max(clock, t);
        }

        // they leave their places and take up the circle, points turned on him
        tw.TweenProperty(node, "position", Orbit(th0), 0.18).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", PointAt(Orbit(th0), DeathCenter), 0.18);
        tw.Parallel().TweenProperty(node, "modulate", look, 0.18);
        tw.Parallel().TweenProperty(node, "scale", scale, 0.18);
        clock = 0.18;

        var hitAt = PierceAt[spot];
        var parry = index is 1 or 2 ? Parries[index - 1] : ((float At, Vector2 Clash, Vector2 Thrown)?)null;
        var leave = parry is { } pr ? pr.At - 0.12f : hitAt - 0.34f;
        var endTh = th0 + speed * (leave - 0.18f);
        tw.TweenMethod(Callable.From<float>(th =>
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            node.Position = Orbit(th);
            node.Rotation = PointAt(node.Position, DeathCenter);
            node.ZIndex = Mathf.Sin(th) > 0 ? ZFlying : ZBack; // passes behind him on the far side
        }), th0, endTh, leave - 0.18f);
        clock = leave;
        tw.TweenCallback(Callable.From(() => node.ZIndex = ZFlying));
        var from = Orbit(endTh);

        if (parry is { } p)
        {
            // a cut at him: he parries (body clip) and the blade is thrown off, turning over
            tw.TweenProperty(node, "rotation", PointAt(from, p.Clash), 0.03);
            tw.TweenProperty(node, "position", p.Clash, 0.09).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            clock += 0.12;
            tw.TweenCallback(Callable.From(() => SwordFx.Clash(rig.Root, p.Clash, col, 1.3f)));
            tw.TweenProperty(node, "position", p.Thrown, 0.26).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(node, "rotation", PointAt(p.Thrown, DeathCenter) - Mathf.Tau, 0.26);
            clock += 0.26;
            from = p.Thrown;
        }

        Until(hitAt - 0.34f);
        DriveIn(tw, rig, body, node, from, PierceSpots[spot], spectral, col, spot == PierceSpots.Length - 1);
    }

    /// <summary>
    /// One blow, readable on its own (0.34 s): the blade swings round to line up with its entry point (0.22 s), draws back
    /// a little (0.06 s), drives in (0.06 s) and is replaced by a copy stuck in the body.
    /// </summary>
    private static void DriveIn(Tween tw, Rig rig, Node2D? body, Node2D node, Vector2 from,
        (Vector2 Point, Vector2 Dir, StuckKind Kind, float Depth) spot, bool spectral, Color col, bool last)
    {
        var dir = spot.Dir.Normalized();
        var length = SwordSpriteHeight * node.Scale.Y;
        Vector2 Contact() => body != null && GodotObject.IsInstanceValid(body)
            ? rig.Root.ToLocal(body.ToGlobal(spot.Point))
            : DeathCenter + spot.Point + new Vector2(0, 60);
        // holder centre when the point is Depth * length past the entry point
        Vector2 Target() => Contact() + dir * (spot.Depth * length - length * 0.5f);
        Vector2 Launch(float back) => Target() - dir * back;
        var aimRot = PointAt(Vector2.Zero, dir);
        var startRot = 0f;
        tw.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(node)) startRot = node.Rotation; }));
        tw.TweenMethod(Callable.From<float>(u =>
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            node.Position = from.Lerp(Launch(230f), u);
            node.Rotation = Mathf.LerpAngle(startRot, aimRot, u);
        }), 0f, 1f, 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenMethod(Callable.From<float>(u =>
        {
            if (GodotObject.IsInstanceValid(node)) node.Position = Launch(230f + 35f * u);
        }), 0f, 1f, 0.06).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tw.TweenMethod(Callable.From<float>(u =>
        {
            if (GodotObject.IsInstanceValid(node)) node.Position = Launch(265f * (1 - u));
        }), 0f, 1f, 0.06).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(() =>
        {
            try
            {
                SwordFx.Pierce(rig.Root, Contact(), dir, col);
                Shake(last ? MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Weak
                    : MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.VeryWeak);
                if (body == null || !GodotObject.IsInstanceValid(body)) return;
                if (StuckSword(rig.Root, node, body, spot, spectral) != null) node.Visible = false;
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[SwordVisuals] stuck sword: {e.Message}");
            }
        }));
    }

    /// <summary>
    /// A copy of the sword stuck in the body. It lives in the rig (so it does not take on the body's hurt / death tint)
    /// and follows the body through a RemoteTransform2D on the body node, so it goes down with him. Pieces: in front of
    /// the body only what is outside him is drawn (texture region); behind the body (z 0, first in the rig = behind the
    /// body sprites) the whole blade is drawn and the body itself hides what is inside him. A blade run through him is
    /// both: the hilt half on its entry side, the point on the other.
    /// </summary>
    private static Node2D? StuckSword(Node2D rigRoot, Node2D flying, Node2D body,
        (Vector2 Point, Vector2 Dir, StuckKind Kind, float Depth) spot, bool spectral)
    {
        var sprite = flying.GetNodeOrNull<Node2D>("Blade")?.GetChildren().OfType<Sprite2D>()
            .FirstOrDefault(s => s.Name != "Aura" && s.Texture != null && !s.RegionEnabled);
        if (sprite?.Texture is not { } tex) return null;
        float w = tex.GetWidth(), h = tex.GetHeight();
        var depth = spot.Depth;
        var look = spectral ? new Color(0.86f, 0.88f, 1f, 0.9f) : Colors.White;
        var anchor = new RemoteTransform2D
        {
            Name = "StuckSwordAnchor", Position = spot.Point, Rotation = PointAt(Vector2.Zero, spot.Dir.Normalized()), UpdateScale = false,
        };
        body.AddChild(anchor);
        anchor.ForceUpdateCache();
        Node2D? first = null;

        // art points up: texture y 0 = point, y h = pommel; the holder's origin is the entry point
        void Piece(bool behind, float from, float to)
        {
            var stuck = new Node2D { Name = "StuckSword", Scale = flying.Scale, ZIndex = behind ? ZBack : ZFlying, Modulate = look };
            stuck.AddChild(new Sprite2D
            {
                Texture = tex, Centered = false, TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                RegionEnabled = true, RegionRect = new Rect2(0, h * from, w, h * (to - from)),
                Offset = new Vector2(-w / 2f, h * (from - depth)), Scale = sprite.Scale, Modulate = sprite.Modulate,
            });
            rigRoot.AddChild(stuck);
            if (behind) rigRoot.MoveChild(stuck, 0);
            stuck.GlobalPosition = anchor.GlobalPosition;
            stuck.GlobalRotation = anchor.GlobalRotation;
            if (first == null)
            {
                first = stuck;
                anchor.RemotePath = anchor.GetPathTo(stuck);
            }
            else
            {
                // a second piece follows the first (same transform)
                var follow = new RemoteTransform2D { Name = "StuckSwordAnchor2", UpdateScale = false };
                first.AddChild(follow);
                follow.RemotePath = follow.GetPathTo(stuck);
            }
            // dimmed only a little with the body once he is down: the blades must stay readable
            var dim = stuck.CreateTween();
            dim.TweenInterval(Math.Max(0.0, 2.5 - (Time.GetTicksMsec() - _deathStart) / 1000.0));
            dim.TweenProperty(stuck, "modulate", look * new Color(0.9f, 0.88f, 0.94f), 0.3);
        }

        switch (spot.Kind)
        {
            case StuckKind.Front: Piece(false, depth, 1f); break;
            case StuckKind.Behind: Piece(true, 0f, 1f); break;
            case StuckKind.ThroughFromFront: Piece(false, depth, 1f); Piece(true, 0f, depth); break;
            case StuckKind.ThroughFromBack: Piece(true, 0f, 1f); Piece(false, 0f, Math.Min(0.2f, depth)); break;
        }
        return first;
    }

    /// <summary>A sword beyond the six that pierce him: drops point-down into the ground beside him and stays.</summary>
    private static void FallIntoGround(Node2D node)
    {
        var x = node.Position.X < 0 ? -170f - 50f * GD.Randf() : 160f + 50f * GD.Randf();
        var tw = node.CreateTween();
        tw.TweenInterval(2.3);
        tw.TweenProperty(node, "rotation", Mathf.Pi + (GD.Randf() - 0.5f) * 0.3f, 0.15);
        tw.TweenProperty(node, "position", new Vector2(x, -80), 0.18).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.TweenProperty(node, "modulate", new Color(0.7f, 0.68f, 0.76f), 0.4);
    }
}
