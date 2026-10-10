using Godot;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>What kind of attack card sent the sword (MotionDirector): picks the fitting strike variant more often.</summary>
public enum StrikeHint
{
    Auto,
    Single,
    /// <summary>An all-enemies attack: sweeps, cleaves and volleys through every enemy.</summary>
    All,
    /// <summary>A cost 2+ attack: the slams and cleaves.</summary>
    Heavy,
}

/// <summary>
/// The sword strikes (user request 2026-10-10 "검들 모션을 좀 더 다양화 시켜줘"): every sword has three strike variants,
/// picked at random but weighted by the card (an all-enemies card prefers the sweep, a cost 2+ card the heavy one).
/// Successive hits of one attack (multi-hit cards; hits within ~1.3 s) never repeat the previous variant and alternate
/// the side they come from, so a flurry reads as different cuts instead of one cut replayed.
/// Timing: the game deals the damage 0.15 s after the Attack trigger, so every variant reaches the target ("contact")
/// 0.11–0.15 s after it starts; the unhurried part (follow-through, return) comes after. A strike sent for a hit that
/// already landed (<see cref="Strike"/> with afterHit) runs its approach 1.6x faster until contact.
/// Variants (index: heavy / all-enemies preference noted):
///  Gram: overhead slam (heavy) / reforged spinning cleave (all) / thrust through.
///  Tyrfing: burning lunge (heavy) / curse-dragging low slash (all) / triple ember cuts.
///  Dainsleif: blood-spray thrust / returns and strikes again / spiral bore (heavy).
///  Skofnung: ghost split (afterimages strike from several angles) / phase-through cut / twelve-ghost flurry (all).
///  Durandal: falling judgment (heavy) / halo sweep (all) / guarding counter-thrust.
///  Claíomh Solais: beam of light (heavy) / blinding flash slash / draw-cut streak (all).
///  Caladbolg: giant sweep (all) / hill-cleaving overhead (heavy) / bounce between enemies.
///  Kusanagi: wind boomerang / grass-mowing sweep (all) / serpent zigzag.
///  Onimaru (generic attacks only; its stance attacks are OnimaruStrike): blink cut / sheath-draw iai flash / dance.
///  Ganjiang · Moye: twin crossing cuts / alternating stabs / spiral pair (heavy) — the partner sword joins when it is
///  out, otherwise a phantom of it in its colour.
/// The headless preview mirror (cv.gd, outside the repo; scenarios st_<sword>_<n>, combo) ports these 1:1.
/// Presentation only: every entry point swallows its errors.
/// UNVERIFIED in game: the spacing of multi-hit damage (hits closer than <see cref="StrikeLockMs"/> share one flight).
/// </summary>
public static partial class SwordVisuals
{
    private static readonly Random StrikeRng = new();

    /// <summary>A second strike call within this many ms of the last one is the same hit (Attack trigger + its damage).</summary>
    private const ulong StrikeLockMs = 380;
    private const ulong ComboWindowMs = 1300;

    private const string StrikeUntilMeta = "ms_strike_until";
    private const string ComboMeta = "ms_combo";
    private const string ComboAtMeta = "ms_combo_at";
    private const string LastVariantMeta = "ms_strike_var";
    private const string CutMeta = "ms_strike_cut";
    private const string HeavyMeta = "ms_strike_heavy";

    /// <summary>One strike being built: the sword's tween plus the planned position/rotation at the end of each step.</summary>
    private sealed class StrikeCtx
    {
        public required Rig Rig;
        public required Tween Tw;
        public required Node2D Node;
        public required SwordId Sword;
        public required Vector2 Home;
        public required Vector2 Aim;
        public required List<Vector2> Aims;
        public Vector2 At;
        public float Rot;
        /// <summary>+1 / -1: which side of the line of attack the offsets go (alternates on successive hits).</summary>
        public float Side = 1f;
        public int Combo;
        public bool Heavy;
        public float Cut;
        public Vector2 Dir => (Aim - At).LengthSquared() < 1f ? Vector2.Right : (Aim - At).Normalized();
        /// <summary>Perpendicular to the line of attack, on <see cref="Side"/>.</summary>
        public Vector2 Perp => new Vector2(-Dir.Y, Dir.X) * Side;
        public float MinX => Aims.Min(a => a.X);
        public float MaxX => Aims.Max(a => a.X);
        public Vector2 Center => Aims.Aggregate(Vector2.Zero, (s, v) => s + v) / Aims.Count;
    }

    /// <summary>A strike's tween is still running (it marks its end itself; expires on its own if the tween was killed).</summary>
    private static bool IsStriking(Node2D node) =>
        node.HasMeta(StrikeUntilMeta) && Time.GetTicksMsec() < (ulong)node.GetMeta(StrikeUntilMeta);

    /// <summary>
    /// The current sword (else any present one) flies to <paramref name="target"/> and back in one of its variants
    /// (user requests 2026-10-08 "공격하면 소환된 칼이 직접 날아가서 공격", 2026-10-10 "검들 모션을 좀 더 다양화").
    /// <paramref name="hint"/>: the card's kind (MotionDirector). <paramref name="afterHit"/>: the damage already landed
    /// (a later hit of a multi-hit attack), so the approach is quicker. A call within <see cref="StrikeLockMs"/> of the
    /// previous one is the same hit and is ignored; a call while the sword is still flying back chains from where it is.
    /// </summary>
    public static void Strike(Player player, Creature? target, StrikeHint hint = StrikeHint.Auto, bool afterHit = false)
    {
        try { StrikeInner(player, target, hint, afterHit); }
        catch (Exception e) { MainFile.Logger.Warn($"[SwordVisuals] Strike failed: {e.Message}"); }
    }

    private static void StrikeInner(Player player, Creature? target, StrikeHint hint, bool afterHit)
    {
        var rig = GetRig(player, create: false);
        if (rig == null || rig.Dead || rig.Swords.Count == 0) return;
        var sword = rig.Current is { } c0 && rig.Swords.ContainsKey(c0) ? c0 : rig.Swords.Keys.First();
        var node = rig.Swords[sword];
        if (!GodotObject.IsInstanceValid(node) || IsUnionBusy(node)) return; // a 【조합】 choreography is moving it
        var now = Time.GetTicksMsec();
        if (LastStrike.TryGetValue(node, out var t0) && now - t0 < StrikeLockMs) return;
        LastStrike[node] = now;

        var combo = node.HasMeta(ComboAtMeta) && now - (ulong)node.GetMeta(ComboAtMeta) < ComboWindowMs
            ? (int)node.GetMeta(ComboMeta) + 1 : 0;
        node.SetMeta(ComboMeta, combo);
        node.SetMeta(ComboAtMeta, now);

        var aims = new List<Vector2>();
        var room = NCombatRoom.Instance;
        if (target != null && room?.GetCreatureNode(target) is { } tn)
            aims.Add(rig.Root.ToLocal(tn.GlobalPosition + new Vector2(0, -130)));
        else
            aims.AddRange((player.Creature.CombatState?.HittableEnemies ?? Enumerable.Empty<Creature>())
                .Select(e => room?.GetCreatureNode(e)).Where(n => n != null)
                .Select(n => rig.Root.ToLocal(n!.GlobalPosition + new Vector2(0, -130))));
        if (!rig.Slots.TryGetValue(sword, out var home)) home = node.Position;
        if (aims.Count == 0) aims.Add(home + new Vector2(520, 40)); // no enemy node: a cut in front of him
        aims.Sort((a, b) => a.X.CompareTo(b.X));

        // a sword summoned by this very card is still flying out of Ensifer: put it on its slot first so the strike starts
        // from there (bug report 2026-10-09); a sword still flying back from the previous hit chains from where it is
        var chain = IsStriking(node);
        if (rig.Moves.TryGetValue(sword, out var move) && GodotObject.IsInstanceValid(move)) move.Kill();
        if (!chain)
        {
            node.Position = home;
            node.Rotation = 0f;
            node.Scale = Vector2.One * 1.15f;
        }
        node.Modulate = Colors.White;
        node.ZIndex = ZFlying;
        node.SetMeta(StrikeUntilMeta, now + 1800);

        var tw = node.CreateTween();
        rig.Moves[sword] = tw; // a later strike / layout takes over cleanly instead of fighting this tween
        if (afterHit) tw.SetSpeedScale(1.6f);
        var ctx = new StrikeCtx
        {
            Rig = rig, Tw = tw, Node = node, Sword = sword, Home = home, Aims = aims,
            Aim = aims[0], At = node.Position, Rot = node.Rotation,
            Combo = combo,
        };
        // successive hits alternate sides; a first hit picks one at random
        ctx.Side = combo > 0 ? (combo % 2 == 0 ? 1f : -1f) : (StrikeRng.NextDouble() < 0.5 ? 1f : -1f);

        var variants = VariantsOf(sword);
        var last = node.HasMeta(LastVariantMeta) ? (int)node.GetMeta(LastVariantMeta) : -1;
        var pick = ChooseVariant(variants.Fns.Length, variants.Heavy, variants.All, hint, combo, last);
        node.SetMeta(LastVariantMeta, pick);
        Trail(node, sword == SwordId.ClaiomhSolais ? 0.35 : 0.55, ColorOf(sword));
        variants.Fns[pick](ctx);
        node.SetMeta(CutMeta, ctx.Cut);
        node.SetMeta(HeavyMeta, ctx.Heavy);
        Sfx.Whoosh(sword);
        tw.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            node.SetMeta(StrikeUntilMeta, 0UL);
            Layout(rig, only: sword);
        }));
    }

    /// <summary>
    /// The cut direction (radians, screen space) and weight of <paramref name="player"/>'s last strike if it was within
    /// 0.8 s (SwordFx.Impact lines its strokes up with the cut); null otherwise.
    /// </summary>
    internal static (float Cut, bool Heavy)? StrikeAccent(Player player)
    {
        try
        {
            var rig = GetRig(player, create: false);
            if (rig == null) return null;
            var sword = rig.Current is { } c && rig.Swords.ContainsKey(c) ? c : rig.Swords.Keys.FirstOrDefault();
            if (!rig.Swords.TryGetValue(sword, out var node) || !GodotObject.IsInstanceValid(node)) return null;
            if (!LastStrike.TryGetValue(node, out var t0) || Time.GetTicksMsec() - t0 > 800) return null;
            if (!node.HasMeta(CutMeta)) return null;
            return ((float)node.GetMeta(CutMeta), node.HasMeta(HeavyMeta) && (bool)node.GetMeta(HeavyMeta));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int ChooseVariant(int n, int heavy, int all, StrikeHint hint, int combo, int last)
    {
        if (combo > 0 && last >= 0 && n > 1) // a later hit of the same attack: never the same cut twice in a row
            return (last + 1 + StrikeRng.Next(n - 1)) % n;
        if (hint == StrikeHint.Heavy && heavy >= 0 && StrikeRng.NextDouble() < 0.75) return heavy;
        if (hint == StrikeHint.All && all >= 0 && StrikeRng.NextDouble() < 0.8) return all;
        var r = StrikeRng.Next(n);
        if (r == last && StrikeRng.NextDouble() < 0.6) r = (r + 1) % n;
        return r;
    }

    private static (Action<StrikeCtx>[] Fns, int Heavy, int All) VariantsOf(SwordId sword) => sword switch
    {
        SwordId.Gram => (A(GramSlam, GramCleave, GramThrust), 0, 1),
        SwordId.Tyrfing => (A(TyrfingLunge, TyrfingDrag, TyrfingEmbers), 0, 1),
        SwordId.Dainsleif => (A(DainsleifThrust, DainsleifTwice, DainsleifBore), 2, -1),
        SwordId.Skofnung => (A(SkofnungSplit, SkofnungPhase, SkofnungTwelve), -1, 2),
        SwordId.Durandal => (A(DurandalJudgment, DurandalHalo, DurandalCounter), 0, 1),
        SwordId.ClaiomhSolais => (A(ClaiomhBeam, ClaiomhFlash, ClaiomhStreak), 0, 2),
        SwordId.Caladbolg => (A(CaladbolgSweep, CaladbolgCleave, CaladbolgBounce), 1, 0),
        SwordId.Kusanagi => (A(KusanagiBoomerang, KusanagiMow, KusanagiSerpent), -1, 1),
        SwordId.Onimaru => (A(OnimaruBlink, OnimaruIai, OnimaruDance), 1, 2),
        SwordId.Ganjiang or SwordId.Moye => (A(TwinCross, TwinAlternate, TwinSpiral), 2, 0),
        _ => (A(TyrfingLunge, GramThrust, KusanagiSerpent), -1, -1),
    };

    private static Action<StrikeCtx>[] A(params Action<StrikeCtx>[] f) => f;

    // ------------------------------------------------------------------ building blocks (they keep At / Rot up to date)

    private static float Shortest(float from, float to) => from + Mathf.Wrap(to - from, -Mathf.Pi, Mathf.Pi);

    /// <summary>Turns the tip toward <paramref name="toward"/> (the short way round).</summary>
    private static void Face(StrikeCtx c, Vector2 toward, double dur, bool parallel = false)
    {
        var r = Shortest(c.Rot, PointAt(c.At, toward));
        (parallel ? c.Tw.Parallel() : c.Tw).TweenProperty(c.Node, "rotation", r, dur).SetTrans(Tween.TransitionType.Sine);
        c.Rot = r;
    }

    private static void Turn(StrikeCtx c, float rot, double dur, bool parallel = false)
    {
        (parallel ? c.Tw.Parallel() : c.Tw).TweenProperty(c.Node, "rotation", rot, dur).SetTrans(Tween.TransitionType.Sine);
        c.Rot = rot;
    }

    /// <summary>Moves straight to <paramref name="pos"/>; <paramref name="face"/>: the tip turns along the way meanwhile.</summary>
    private static void Go(StrikeCtx c, Vector2 pos, double dur, Tween.TransitionType trans = Tween.TransitionType.Quad,
        Tween.EaseType ease = Tween.EaseType.In, bool face = false)
    {
        var from = c.At;
        c.Tw.TweenProperty(c.Node, "position", pos, dur).SetTrans(trans).SetEase(ease);
        c.At = pos;
        if (face)
        {
            var r = Shortest(c.Rot, PointAt(from, pos));
            c.Tw.Parallel().TweenProperty(c.Node, "rotation", r, dur * 0.5).SetTrans(Tween.TransitionType.Sine);
            c.Rot = r;
        }
    }

    /// <summary>Follows <paramref name="f"/> (u 0..1); <paramref name="face"/>: the tip points along the path.</summary>
    private static void Path(StrikeCtx c, Func<float, Vector2> f, double dur, bool face = true,
        Tween.TransitionType trans = Tween.TransitionType.Linear, Tween.EaseType ease = Tween.EaseType.InOut, float spin = 0f)
    {
        var node = c.Node;
        var r0 = c.Rot;
        c.Tw.TweenMethod(Callable.From<float>(u =>
        {
            node.Position = f(u);
            if (face) node.Rotation = PointAt(f(Math.Max(0f, u - 0.02f)), f(Math.Min(1f, u + 0.02f)) + new Vector2(0.001f, 0));
            else if (spin != 0f) node.Rotation = r0 + spin * u;
        }), 0f, 1f, dur).SetTrans(trans).SetEase(ease);
        c.At = f(1f);
        c.Rot = face ? PointAt(f(0.98f), f(1f) + new Vector2(0.001f, 0)) : spin != 0f ? r0 + spin : r0;
    }

    /// <summary>Jumps (unseen) to <paramref name="pos"/> pointing <paramref name="rot"/>.</summary>
    private static void Blink(StrikeCtx c, Vector2 pos, float rot)
    {
        var node = c.Node;
        c.Tw.TweenCallback(Callable.From(() => { node.Position = pos; node.Rotation = rot; }));
        c.At = pos;
        c.Rot = rot;
    }

    private static void Fade(StrikeCtx c, float a, double dur, bool parallel = false) =>
        (parallel ? c.Tw.Parallel() : c.Tw).TweenProperty(c.Node, "modulate:a", a, dur);

    private static void Tint(StrikeCtx c, Color col, double dur, bool parallel = false) =>
        (parallel ? c.Tw.Parallel() : c.Tw).TweenProperty(c.Node, "modulate", col, dur);

    private static void Size(StrikeCtx c, Vector2 s, double dur, bool parallel = true) =>
        (parallel ? c.Tw.Parallel() : c.Tw).TweenProperty(c.Node, "scale", s, dur);

    private static void Hold(StrikeCtx c, double dur) => c.Tw.TweenInterval(dur);

    /// <summary>The blade meets the target here: restores normal speed and records the cut direction for the impact.</summary>
    private static void Contact(StrikeCtx c, Vector2 cut)
    {
        c.Cut = cut.Angle();
        var tw = c.Tw;
        tw.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(tw)) tw.SetSpeedScale(1f); }));
    }

    /// <summary>
    /// Home again, unhurried: straight, or on an arc lifted by <paramref name="lift"/> px; scale, colour and rotation
    /// settle on the way (<paramref name="spin"/> adds whole turns).
    /// </summary>
    private static void Return(StrikeCtx c, double dur, float lift = 0f, float spin = 0f)
    {
        var node = c.Node;
        var from = c.At;
        var home = c.Home;
        c.Tw.TweenCallback(Callable.From(() => node.Rotation = Mathf.Wrap(node.Rotation, -Mathf.Pi, Mathf.Pi)));
        if (lift == 0f)
            c.Tw.TweenProperty(node, "position", home, dur).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        else
        {
            var mid = (from + home) / 2 + new Vector2(0, -lift);
            c.Tw.TweenMethod(Callable.From<float>(u => node.Position = Bezier(from, mid, home, u)), 0f, 1f, dur)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }
        c.Tw.Parallel().TweenProperty(node, "rotation", spin != 0f ? spin : 0f, dur).SetTrans(Tween.TransitionType.Sine);
        c.Tw.Parallel().TweenProperty(node, "scale", Vector2.One * 1.15f, dur);
        c.Tw.Parallel().TweenProperty(node, "modulate", Colors.White, dur * 0.8);
        if (spin != 0f) c.Tw.TweenCallback(Callable.From(() => node.Rotation = 0f));
        c.At = home;
        c.Rot = 0f;
    }

    /// <summary>Vanishes where it is and fades back in at home (ghost / light swords).</summary>
    private static void ReturnByFading(StrikeCtx c, double outDur = 0.1, double inDur = 0.25)
    {
        var node = c.Node;
        var home = c.Home;
        c.Tw.TweenProperty(node, "modulate:a", 0f, outDur);
        c.Tw.TweenCallback(Callable.From(() => { node.Position = home; node.Rotation = 0f; node.Scale = Vector2.One * 1.15f; }));
        c.Tw.TweenProperty(node, "modulate", Colors.White, inDur).From(new Color(1, 1, 1, 0));
        c.At = home;
        c.Rot = 0f;
    }

    /// <summary>A copy of the blade's sprites (no aura, no hover box, no bob).</summary>
    private static Node2D? BladeCopy(Node2D node)
    {
        if (node.GetNodeOrNull<Node2D>("Blade") is not { } blade) return null;
        var ghost = new Node2D { Position = node.Position, Rotation = node.Rotation, Scale = node.Scale };
        foreach (var child in blade.GetChildren())
            if (child is Sprite2D sp && sp.Name != "Aura")
                ghost.AddChild(new Sprite2D
                {
                    Texture = sp.Texture, Scale = sp.Scale, Position = sp.Position, Rotation = sp.Rotation,
                    RegionEnabled = sp.RegionEnabled, RegionRect = sp.RegionRect, UseParentMaterial = true,
                    TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                });
        return ghost;
    }

    /// <summary>
    /// A spectral copy of the sword (in <paramref name="tint"/>, additive) that appears at <paramref name="from"/> after
    /// <paramref name="delay"/>, cuts to <paramref name="to"/> in <paramref name="dur"/> and fades. Skofnung's ghosts,
    /// the missing twin of Ganjiang / Moye.
    /// </summary>
    private static void Phantom(StrikeCtx c, Vector2 from, Vector2 to, double delay, double dur, Color tint, float alpha = 0.75f)
    {
        if (BladeCopy(c.Node) is not { } g) return;
        g.Position = from;
        g.Rotation = PointAt(from, to);
        g.Scale = Vector2.One * 1.1f;
        g.ZIndex = ZFlying;
        g.Modulate = new Color(tint, 0f);
        g.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        c.Rig.Root.AddChild(g);
        var t = g.CreateTween();
        if (delay > 0) t.TweenInterval(delay);
        t.TweenProperty(g, "modulate:a", alpha, 0.03);
        t.TweenProperty(g, "position", to, dur).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        t.TweenProperty(g, "modulate:a", 0f, 0.16);
        t.TweenCallback(Callable.From(g.QueueFree));
    }

    /// <summary>
    /// Ganjiang's / Moye's partner joins the strike when it is out and idle: a context of its own (own tween, own
    /// return). Null when the partner is not on screen — the caller then sends a phantom of it.
    /// </summary>
    private static StrikeCtx? Partner(StrikeCtx c)
    {
        var other = c.Sword == SwordId.Moye ? SwordId.Ganjiang : c.Sword == SwordId.Ganjiang ? SwordId.Moye : (SwordId?)null;
        if (other is not { } o || !c.Rig.Swords.TryGetValue(o, out var pn) || !GodotObject.IsInstanceValid(pn)) return null;
        if (IsUnionBusy(pn) || InFlight(pn) || IsStriking(pn)) return null;
        if (c.Rig.Moves.TryGetValue(o, out var mv) && GodotObject.IsInstanceValid(mv)) mv.Kill();
        if (!c.Rig.Slots.TryGetValue(o, out var home)) home = pn.Position;
        pn.ZIndex = ZFlying;
        pn.Modulate = Colors.White;
        pn.SetMeta(StrikeUntilMeta, Time.GetTicksMsec() + 1800);
        LastStrike[pn] = Time.GetTicksMsec();
        var tw = pn.CreateTween();
        c.Rig.Moves[o] = tw;
        Trail(pn, 0.5, ColorOf(o));
        var rig = c.Rig;
        var pc = new StrikeCtx
        {
            Rig = rig, Tw = tw, Node = pn, Sword = o, Home = home, Aim = c.Aim, Aims = c.Aims, At = pn.Position, Rot = pn.Rotation,
            Side = -c.Side, Combo = c.Combo,
        };
        return pc;
    }

    private static void FinishPartner(StrikeCtx pc)
    {
        var node = pc.Node;
        var rig = pc.Rig;
        var sword = pc.Sword;
        pc.Tw.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            node.SetMeta(StrikeUntilMeta, 0UL);
            Layout(rig, only: sword);
        }));
    }

    private static Color PartnerColor(SwordId sword) => ColorOf(sword == SwordId.Moye ? SwordId.Ganjiang : SwordId.Moye);

    // ------------------------------------------------------------------ Gram

    /// <summary>Rises, then slams down onto the target (heavy).</summary>
    private static void GramSlam(StrikeCtx c)
    {
        var a = c.Aim; var hit = a - c.Dir * 40f;
        var up = new Vector2((c.At.X + a.X) / 2 - c.Side * 40f, Math.Min(c.At.Y, a.Y) - 270f);
        c.Heavy = true;
        Go(c, up, 0.07, Tween.TransitionType.Quad, Tween.EaseType.Out);
        Face(c, hit, 0.07, parallel: true);
        Go(c, hit, 0.07, Tween.TransitionType.Quad, Tween.EaseType.In);
        Size(c, Vector2.One * 1.5f, 0.07);
        Contact(c, hit - up);
        Hold(c, 0.08);
        Return(c, 0.38, lift: 60f, spin: Mathf.Tau * c.Side);
    }

    /// <summary>Reforged spinning cleave: turning end over end, it cleaves through every enemy in one pass.</summary>
    private static void GramCleave(StrikeCtx c)
    {
        var y = c.Aims.Average(v => v.Y);
        var start = new Vector2(c.MinX - 170f, y - 60f * c.Side);
        var end = new Vector2(c.MaxX + 170f, y + 40f * c.Side);
        var first = new Vector2(c.MinX, y);
        Go(c, start, 0.06, Tween.TransitionType.Quad, Tween.EaseType.Out);
        Size(c, Vector2.One * 1.55f, 0.06);
        Turn(c, c.Rot + Mathf.Tau * 0.5f * c.Side, 0.06, parallel: true);
        // spinning blade: one whole turn per ~0.12 s, contact with the first enemy at 0.12 s
        var s0 = start; var r0 = c.Rot;
        var node = c.Node;
        var span = Math.Max(1f, end.X - s0.X);
        var firstU = Mathf.Clamp((first.X - s0.X) / span, 0.1f, 0.6f);
        c.Tw.TweenMethod(Callable.From<float>(u =>
        {
            node.Position = s0.Lerp(end, u) + new Vector2(0, -Mathf.Sin(u * Mathf.Pi) * 35f);
            node.Rotation = r0 + Mathf.Tau * 2.2f * u * c.Side;
        }), 0f, firstU, 0.06);
        Contact(c, end - start);
        c.Tw.TweenMethod(Callable.From<float>(u =>
        {
            node.Position = s0.Lerp(end, u) + new Vector2(0, -Mathf.Sin(u * Mathf.Pi) * 35f);
            node.Rotation = r0 + Mathf.Tau * 2.2f * u * c.Side;
        }), firstU, 1f, 0.06 + 0.1 * (1 - firstU));
        c.At = end;
        c.Rot = r0 + Mathf.Tau * 2.2f * c.Side;
        Hold(c, 0.05);
        Return(c, 0.36, lift: 120f);
    }

    /// <summary>A short draw back, then a straight thrust clean through the target.</summary>
    private static void GramThrust(StrikeCtx c)
    {
        var a = c.Aim + c.Perp * 14f; var d = c.Dir;
        Go(c, c.At - d * 45f, 0.04, Tween.TransitionType.Sine, Tween.EaseType.Out);
        Face(c, a, 0.04, parallel: true);
        Go(c, a + d * 120f, 0.08, Tween.TransitionType.Expo, Tween.EaseType.In);
        Size(c, new Vector2(1.2f, 1.45f), 0.08);
        Contact(c, d);
        Hold(c, 0.07);
        Return(c, 0.36, lift: 150f);
    }

    // ------------------------------------------------------------------ Tyrfing

    /// <summary>A straight burning lunge: it glows hot on the way in.</summary>
    private static void TyrfingLunge(StrikeCtx c)
    {
        var hit = c.Aim - c.Dir * 30f;
        c.Heavy = true;
        Face(c, hit, 0.04);
        Tint(c, new Color(1.6f, 1.15f, 0.8f), 0.05, parallel: true);
        Go(c, hit, 0.09, Tween.TransitionType.Quad, Tween.EaseType.In);
        Size(c, Vector2.One * 1.35f, 0.09);
        Contact(c, c.Dir);
        Hold(c, 0.06);
        Return(c, 0.3);
    }

    /// <summary>The cursed blade drags low along the ground, then rips upward through the enemies.</summary>
    private static void TyrfingDrag(StrikeCtx c)
    {
        var low = new Vector2(c.MinX - 230f, c.Aims.Average(v => v.Y) + 120f);
        var drag = new Vector2(c.MaxX + 30f, low.Y - 10f);
        var rip = drag + new Vector2(130f, -230f);
        Go(c, low, 0.06, Tween.TransitionType.Quad, Tween.EaseType.Out);
        Turn(c, Shortest(c.Rot, 2.0f), 0.06, parallel: true); // tip forward and a little down: it drags
        Go(c, drag, 0.08, Tween.TransitionType.Quad, Tween.EaseType.In);
        Tint(c, new Color(1.5f, 0.9f, 0.7f), 0.08, parallel: true);
        Contact(c, drag - low);
        Go(c, rip, 0.09, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        Turn(c, Shortest(c.Rot, 0.5f), 0.09, parallel: true);
        Hold(c, 0.04);
        Return(c, 0.34, lift: 80f);
    }

    /// <summary>Three ember cuts in a Z across the target.</summary>
    private static void TyrfingEmbers(StrikeCtx c)
    {
        var a = c.Aim; var s = c.Side;
        var p1 = a + new Vector2(-95f, -95f * s);
        var p2 = a + new Vector2(85f, 70f * s);
        var p3 = a + new Vector2(-85f, 35f * s);
        var p4 = a + new Vector2(95f, -65f * s);
        Go(c, p1, 0.06, Tween.TransitionType.Quad, Tween.EaseType.Out, face: true);
        Face(c, p2, 0.02);
        Go(c, p2, 0.045, Tween.TransitionType.Quad, Tween.EaseType.In);
        Contact(c, p2 - p1);
        Face(c, p3, 0.02);
        Go(c, p3, 0.05, Tween.TransitionType.Quad, Tween.EaseType.In);
        Face(c, p4, 0.02);
        Go(c, p4, 0.05, Tween.TransitionType.Quad, Tween.EaseType.In);
        Hold(c, 0.05);
        Return(c, 0.32, lift: 90f);
    }

    // ------------------------------------------------------------------ Dainsleif

    /// <summary>A thrust that drinks: it pulls back, drives in deep and flushes red.</summary>
    private static void DainsleifThrust(StrikeCtx c)
    {
        var a = c.Aim + c.Perp * 10f; var d = c.Dir;
        Go(c, c.At - d * 50f, 0.04, Tween.TransitionType.Sine, Tween.EaseType.Out);
        Face(c, a, 0.04, parallel: true);
        Go(c, a + d * 25f, 0.08, Tween.TransitionType.Expo, Tween.EaseType.In);
        Contact(c, d);
        Tint(c, new Color(1.5f, 0.7f, 0.75f), 0.06);
        Go(c, a - d * 40f, 0.08, Tween.TransitionType.Sine, Tween.EaseType.Out);
        Return(c, 0.3);
    }

    /// <summary>Strikes, draws out to the side and comes back for a second bite from another angle.</summary>
    private static void DainsleifTwice(StrikeCtx c)
    {
        var a = c.Aim; var d = c.Dir; var p = c.Perp;
        var hit = a - d * 30f;
        Face(c, hit, 0.04);
        Go(c, hit, 0.09, Tween.TransitionType.Quad, Tween.EaseType.In);
        Contact(c, d);
        var out1 = a - d * 170f + p * 150f;
        Go(c, out1, 0.1, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        Face(c, a, 0.1, parallel: true);
        Go(c, a + (a - out1).Normalized() * 40f, 0.07, Tween.TransitionType.Expo, Tween.EaseType.In);
        Tint(c, new Color(1.5f, 0.7f, 0.75f), 0.07, parallel: true);
        Hold(c, 0.05);
        Return(c, 0.3);
    }

    /// <summary>Spiral bore: the blade turns about its own length (corkscrew) as it bores in (heavy).</summary>
    private static void DainsleifBore(StrikeCtx c)
    {
        var from = c.At; var a = c.Aim; var d = c.Dir; var p = c.Perp;
        var hit = a - d * 20f;
        var node = c.Node;
        c.Heavy = true;
        Face(c, hit, 0.03);
        var rot = c.Rot;
        c.Tw.TweenMethod(Callable.From<float>(u =>
        {
            node.Position = from.Lerp(hit, u) + p * Mathf.Sin(u * Mathf.Pi * 3f) * 34f * (1 - u);
            node.Rotation = rot;
            node.Scale = new Vector2(1.25f * Math.Max(0.25f, Mathf.Abs(Mathf.Cos(u * Mathf.Pi * 5f))), 1.3f);
        }), 0f, 1f, 0.11).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        c.At = hit;
        Contact(c, d);
        var deep = hit + d * 45f;
        c.Tw.TweenMethod(Callable.From<float>(u =>
        {
            node.Position = hit.Lerp(deep, u);
            node.Scale = new Vector2(1.25f * Math.Max(0.25f, Mathf.Abs(Mathf.Cos(u * Mathf.Pi * 4f))), 1.3f);
        }), 0f, 1f, 0.12);
        c.At = deep;
        Tint(c, new Color(1.5f, 0.7f, 0.75f), 0.12, parallel: true);
        Return(c, 0.32, lift: 70f);
    }

    // ------------------------------------------------------------------ Skofnung

    private static readonly Color GhostTint = new(0.7f, 0.9f, 1f);

    /// <summary>Ghost split: three afterimages strike from different angles, then the sword itself cuts through.</summary>
    private static void SkofnungSplit(StrikeCtx c)
    {
        var a = c.Aim; var d = c.Dir; var p = c.Perp;
        // afterimages converge first, so the real cut lands on the hit
        Phantom(c, a - d * 190f - p * 120f, a + d * 30f + p * 20f, 0.02, 0.08, GhostTint);
        Phantom(c, a + new Vector2(30f, -230f), a + new Vector2(-10f, 20f), 0.04, 0.08, GhostTint);
        Phantom(c, a + d * 200f + p * 90f, a - d * 20f, 0.06, 0.08, GhostTint, 0.6f);
        var beside = a - d * 160f + p * 70f;
        Tint(c, new Color(0.7f, 0.9f, 1f, 0f), 0.05);
        Blink(c, beside, PointAt(beside, a));
        Tint(c, new Color(0.8f, 0.95f, 1f, 0.95f), 0.03);
        Go(c, a + (a - beside).Normalized() * 70f, 0.05, Tween.TransitionType.Expo, Tween.EaseType.In);
        Contact(c, a - beside);
        Hold(c, 0.05);
        ReturnByFading(c, 0.12, 0.25);
    }

    /// <summary>Phase-through: translucent, it passes straight through the target and fades out beyond.</summary>
    private static void SkofnungPhase(StrikeCtx c)
    {
        var a = c.Aim; var d = c.Dir;
        Face(c, a, 0.03);
        Tint(c, new Color(0.75f, 0.92f, 1f, 0.55f), 0.04, parallel: true);
        Go(c, a - d * 10f, 0.1, Tween.TransitionType.Quad, Tween.EaseType.In);
        Contact(c, d);
        Go(c, a + d * 260f, 0.12, Tween.TransitionType.Sine, Tween.EaseType.Out);
        ReturnByFading(c, 0.12, 0.28);
    }

    /// <summary>Twelve ghosts stab in from a ring around the enemies; the sword itself waits, translucent.</summary>
    private static void SkofnungTwelve(StrikeCtx c)
    {
        var a0 = (float)StrikeRng.NextDouble() * Mathf.Tau;
        for (var i = 0; i < 12; i++)
        {
            var t = c.Aims[i % c.Aims.Count];
            var ang = a0 + Mathf.Tau * i / 12f;
            var from = t + new Vector2(Mathf.Cos(ang) * 230f, Mathf.Sin(ang) * 170f);
            Phantom(c, from, t + (t - from).Normalized() * 30f, 0.012 * i, 0.07, GhostTint, 0.55f);
        }
        var a = c.Aim;
        var over = a + new Vector2(-40f, -260f);
        Tint(c, new Color(0.7f, 0.9f, 1f, 0f), 0.05);
        Blink(c, over, PointAt(over, a));
        Tint(c, new Color(0.8f, 0.95f, 1f, 0.9f), 0.04);
        Go(c, a + new Vector2(20f, 40f), 0.05, Tween.TransitionType.Expo, Tween.EaseType.In);
        Contact(c, a - over);
        Hold(c, 0.12);
        ReturnByFading(c, 0.12, 0.25);
    }

    // ------------------------------------------------------------------ Durandal

    /// <summary>Falling judgment: vanishes, appears high above the target point down and drops (heavy).</summary>
    private static void DurandalJudgment(StrikeCtx c)
    {
        var a = c.Aim;
        var above = a + new Vector2(0, -320f);
        c.Heavy = true;
        Fade(c, 0f, 0.04);
        Blink(c, above, Mathf.Pi);
        Fade(c, 1f, 0.03);
        Go(c, a + new Vector2(0, -30f), 0.07, Tween.TransitionType.Quad, Tween.EaseType.In);
        Size(c, Vector2.One * 1.5f, 0.07);
        Contact(c, Vector2.Down);
        Hold(c, 0.12);
        ReturnByFading(c, 0.1, 0.22);
    }

    /// <summary>Halo sweep: lying level, it circles the enemies once like a halo of light.</summary>
    private static void DurandalHalo(StrikeCtx c)
    {
        var center = c.Center + new Vector2(0, -20f);
        var rx = Math.Max(190f, (c.MaxX - c.MinX) / 2 + 150f);
        const float ry = 70f;
        var s = c.Side;
        Vector2 At(float th) => center + new Vector2(Mathf.Cos(th) * rx, Mathf.Sin(th) * ry);
        var th0 = Mathf.Pi; // start on the near (left) side
        var start = At(th0);
        Go(c, start, 0.06, Tween.TransitionType.Quad, Tween.EaseType.Out);
        Turn(c, Shortest(c.Rot, s > 0 ? Mathf.Pi / 2 : -Mathf.Pi / 2), 0.06, parallel: true);
        Tint(c, new Color(1.35f, 1.3f, 1.1f), 0.06, parallel: true);
        var node = c.Node;
        void Orbit(float u)
        {
            var th = th0 - s * Mathf.Tau * u;
            node.Position = At(th);
            node.ZIndex = Mathf.Sin(th) > 0 ? ZFlying : ZBack + 1;
            node.Rotation = PointAt(At(th), At(th - s * 0.1f));
        }
        c.Tw.TweenMethod(Callable.From<float>(Orbit), 0f, 0.3f, 0.07);
        Contact(c, new Vector2(1, 0));
        c.Tw.TweenMethod(Callable.From<float>(Orbit), 0.3f, 1f, 0.17);
        c.Tw.TweenCallback(Callable.From(() => node.ZIndex = ZFlying));
        c.At = start;
        c.Rot = PointAt(At(th0), At(th0 - s * 0.1f));
        Return(c, 0.34, lift: 90f);
    }

    /// <summary>Guarding counter-thrust: first it swings up into a guard before him, then thrusts from the guard.</summary>
    private static void DurandalCounter(StrikeCtx c)
    {
        var guard = c.Home + new Vector2(70f, 10f);
        Go(c, guard, 0.05, Tween.TransitionType.Sine, Tween.EaseType.Out);
        Turn(c, -0.45f, 0.05, parallel: true);
        var a = c.Aim; var d = (a - guard).Normalized();
        Face(c, a, 0.025);
        Go(c, a - d * 20f, 0.07, Tween.TransitionType.Expo, Tween.EaseType.In);
        Size(c, new Vector2(1.2f, 1.4f), 0.07);
        Contact(c, d);
        Hold(c, 0.07);
        Return(c, 0.32);
    }

    // ------------------------------------------------------------------ Claíomh Solais

    /// <summary>A beam of light: it stretches into a long bright shaft as it shoots.</summary>
    private static void ClaiomhBeam(StrikeCtx c)
    {
        var hit = c.Aim - c.Dir * 40f;
        c.Heavy = true;
        Face(c, hit, 0.03);
        Size(c, new Vector2(0.8f, 2.6f), 0.05, parallel: false);
        Tint(c, new Color(1.6f, 1.6f, 1.3f), 0.05, parallel: true);
        Go(c, hit, 0.06, Tween.TransitionType.Expo, Tween.EaseType.In);
        Contact(c, c.Dir);
        Size(c, Vector2.One * 1.15f, 0.12, parallel: false);
        Tint(c, Colors.White, 0.12, parallel: true);
        Return(c, 0.3);
    }

    /// <summary>Blinding flash: it flares where it floats, is gone, and the cut is already across the target.</summary>
    private static void ClaiomhFlash(StrikeCtx c)
    {
        var a = c.Aim; var d = c.Dir; var p = c.Perp;
        Tint(c, new Color(2f, 2f, 1.6f), 0.06);
        Size(c, Vector2.One * 1.3f, 0.06);
        Fade(c, 0f, 0.025);
        var s0 = a - d * 120f - p * 90f;
        var s1 = a + d * 130f + p * 90f;
        Blink(c, s0, PointAt(s0, s1));
        var rig = c.Rig.Root;
        c.Tw.TweenCallback(Callable.From(() => SwordFx.Glint(rig, a, ColorOf(SwordId.ClaiomhSolais), 260f)));
        Tint(c, new Color(1.8f, 1.8f, 1.5f, 1f), 0.015);
        Go(c, s1, 0.04, Tween.TransitionType.Expo, Tween.EaseType.Out);
        Contact(c, s1 - s0);
        Hold(c, 0.1);
        ReturnByFading(c, 0.12, 0.25);
    }

    /// <summary>Draw-cut streak: level, stretched into a streak, it crosses every enemy in one drawn cut.</summary>
    private static void ClaiomhStreak(StrikeCtx c)
    {
        var y = c.Aims.Average(v => v.Y) + 10f * c.Side;
        var start = new Vector2(c.MinX - 240f, y);
        var end = new Vector2(c.MaxX + 240f, y);
        Go(c, start, 0.06, Tween.TransitionType.Quad, Tween.EaseType.Out);
        Turn(c, Shortest(c.Rot, Mathf.Pi / 2), 0.06, parallel: true);
        Size(c, new Vector2(0.85f, 2.3f), 0.06);
        Tint(c, new Color(1.7f, 1.7f, 1.4f), 0.06, parallel: true);
        var firstU = Mathf.Clamp((c.MinX - start.X) / Math.Max(1f, end.X - start.X), 0.1f, 0.5f);
        var node = c.Node;
        c.Tw.TweenMethod(Callable.From<float>(u => node.Position = start.Lerp(end, u)), 0f, firstU, 0.06);
        Contact(c, Vector2.Right);
        c.Tw.TweenMethod(Callable.From<float>(u => node.Position = start.Lerp(end, u)), firstU, 1f, 0.07);
        c.At = end;
        Size(c, Vector2.One * 1.15f, 0.1, parallel: false);
        ReturnByFading(c, 0.1, 0.25);
    }

    // ------------------------------------------------------------------ Caladbolg

    /// <summary>Giant sweep: it grows huge and sweeps across the enemies.</summary>
    private static void CaladbolgSweep(StrikeCtx c)
    {
        var a = c.Aims.Count > 1 ? c.Center : c.Aim;
        var span = (c.MaxX - c.MinX) / 2;
        var a1 = a + new Vector2(-60f - span, -220f);
        Size(c, Vector2.One * 2.4f, 0.07, parallel: false);
        c.Tw.Parallel().TweenProperty(c.Node, "position", a1, 0.07).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        c.At = a1;
        Turn(c, Shortest(c.Rot, -0.6f), 0.07, parallel: true);
        var end = a + new Vector2(40f + span, -80f);
        c.Tw.TweenProperty(c.Node, "rotation", c.Rot + 2.8f, 0.07).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        c.Tw.Parallel().TweenProperty(c.Node, "position", end, 0.07);
        c.At = end;
        c.Rot += 2.8f;
        Contact(c, new Vector2(1, 0.4f));
        Hold(c, 0.08);
        Return(c, 0.32);
    }

    /// <summary>Hill-cleaving overhead: grown huge, raised high, it comes down over the top onto the target (heavy).</summary>
    private static void CaladbolgCleave(StrikeCtx c)
    {
        var a = c.Aim;
        var high = a + new Vector2(-150f, -380f);
        c.Heavy = true;
        Go(c, high, 0.07, Tween.TransitionType.Quad, Tween.EaseType.Out);
        Size(c, Vector2.One * 2.6f, 0.07);
        Turn(c, Shortest(c.Rot, -0.35f), 0.07, parallel: true);
        var down = a + new Vector2(60f, -40f);
        c.Tw.TweenProperty(c.Node, "position", down, 0.07).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        c.Tw.Parallel().TweenProperty(c.Node, "rotation", 2.5f, 0.07).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        c.At = down; c.Rot = 2.5f;
        Contact(c, down - high);
        Hold(c, 0.12);
        Return(c, 0.36, lift: 60f);
    }

    /// <summary>Bounces from enemy to enemy, spinning; a single target is hit, glanced off and hit again.</summary>
    private static void CaladbolgBounce(StrikeCtx c)
    {
        var pts = new List<Vector2>(c.Aims);
        if (pts.Count == 1) pts.Add(c.Aim + new Vector2(10f, 0)); // up and back down onto the same enemy
        if (c.Side < 0 && pts.Count > 2) pts.Reverse();
        var prev = c.At;
        var turn = Mathf.Tau * c.Side;
        for (var i = 0; i < pts.Count; i++)
        {
            var to = pts[i];
            var lift = i == 0 ? 60f : 170f;
            var from = prev;
            var mid = (from + to) / 2 + new Vector2(0, -lift);
            Path(c, u => Bezier(from, mid, to, u), i == 0 ? 0.12 : 0.13, face: false, spin: turn,
                trans: Tween.TransitionType.Sine, ease: Tween.EaseType.In);
            if (i == 0) Contact(c, to - from);
            prev = to;
        }
        Return(c, 0.34, lift: 120f);
    }

    // ------------------------------------------------------------------ Kusanagi

    /// <summary>Wind boomerang: spinning out on one curve and home on the other.</summary>
    private static void KusanagiBoomerang(StrikeCtx c)
    {
        var from = c.At; var a = c.Aim; var hit = a - c.Dir * 40f; var p = c.Perp;
        var mid = (from + a) / 2 + p * 160f;
        Path(c, u => Bezier(from, mid, hit, u), 0.13, face: false, spin: Mathf.Tau * 2 * c.Side);
        Contact(c, hit - mid);
        var home = c.Home;
        var back = (home + a) / 2 - p * 160f;
        Path(c, u => Bezier(hit, back, home, u), 0.4, face: false, spin: Mathf.Tau * 3 * c.Side);
        c.Tw.TweenCallback(Callable.From(() => c.Node.Rotation = 0f));
        c.Rot = 0f;
        Return(c, 0.08);
    }

    /// <summary>Grass-mowing sweep: low and level, a wide scything arc through every enemy.</summary>
    private static void KusanagiMow(StrikeCtx c)
    {
        var y = c.Aims.Average(v => v.Y) + 70f;
        var start = new Vector2(c.MinX - 220f, y - 70f);
        var low = new Vector2((c.MinX + c.MaxX) / 2, y + 60f);
        var end = new Vector2(c.MaxX + 220f, y - 90f);
        Go(c, start, 0.05, Tween.TransitionType.Quad, Tween.EaseType.Out);
        Turn(c, Shortest(c.Rot, 2.3f), 0.05, parallel: true);
        var firstU = Mathf.Clamp((c.MinX - start.X) / Math.Max(1f, end.X - start.X), 0.15f, 0.5f);
        var node = c.Node;
        void Mow(float u)
        {
            node.Position = Bezier(start, low, end, u);
            node.Rotation = 2.3f - 1.6f * u; // the blade turns through the sweep like a scythe
        }
        c.Tw.TweenMethod(Callable.From<float>(Mow), 0f, firstU, 0.07);
        Contact(c, end - start);
        c.Tw.TweenMethod(Callable.From<float>(Mow), firstU, 1f, 0.12);
        c.At = end; c.Rot = 0.7f;
        Return(c, 0.34, lift: 100f);
    }

    /// <summary>Serpent zigzag: it weaves in on a snaking path, the tip always leading.</summary>
    private static void KusanagiSerpent(StrikeCtx c)
    {
        var from = c.At; var a = c.Aim; var d = c.Dir; var p = c.Perp;
        var hit = a - d * 30f;
        Path(c, u => from.Lerp(hit, u) + p * Mathf.Sin(u * Mathf.Pi * 2.5f) * 75f * (1 - u * 0.75f), 0.13,
            trans: Tween.TransitionType.Sine, ease: Tween.EaseType.In);
        Contact(c, d);
        Go(c, hit + d * 50f - p * 30f, 0.06, Tween.TransitionType.Sine, Tween.EaseType.Out);
        var start = c.At; var home = c.Home;
        Path(c, u => start.Lerp(home, u) - p * Mathf.Sin(u * Mathf.Pi * 2f) * 50f, 0.36,
            trans: Tween.TransitionType.Sine, ease: Tween.EaseType.Out);
        Return(c, 0.08);
    }

    // ------------------------------------------------------------------ Onimaru (generic attacks)

    /// <summary>Blink cut: gone, then already cutting through from beside the target.</summary>
    private static void OnimaruBlink(StrikeCtx c)
    {
        var a = c.Aim; var d = c.Dir; var p = c.Perp;
        var s0 = a - d * 130f + p * 60f;
        var s1 = a + d * 150f - p * 50f;
        Fade(c, 0f, 0.035);
        Blink(c, s0, PointAt(s0, s1));
        Fade(c, 1f, 0.02);
        Go(c, s1, 0.06, Tween.TransitionType.Expo, Tween.EaseType.In);
        Contact(c, s1 - s0);
        Hold(c, 0.1);
        ReturnByFading(c, 0.1, 0.24);
    }

    /// <summary>Sheath-draw iai flash: it sinks to his hip as into a scabbard, then one drawn flash through the target.</summary>
    private static void OnimaruIai(StrikeCtx c)
    {
        var hip = c.Home + new Vector2(-40f, 70f);
        Go(c, hip, 0.05, Tween.TransitionType.Sine, Tween.EaseType.Out);
        Turn(c, Shortest(c.Rot, -1.95f), 0.05, parallel: true);
        var a = c.Aim; var d = (a - hip).Normalized();
        var end = a + d * 170f;
        Face(c, end, 0.015);
        Go(c, end, 0.07, Tween.TransitionType.Expo, Tween.EaseType.In);
        var rig = c.Rig.Root;
        c.Tw.TweenCallback(Callable.From(() => SwordFx.Glint(rig, a, new Color(0.75f, 0.6f, 1f), 200f)));
        Contact(c, d);
        Hold(c, 0.1);
        Return(c, 0.34, lift: 170f);
    }

    /// <summary>Dance: it circles the target in two cuts, the edge always leading.</summary>
    private static void OnimaruDance(StrikeCtx c)
    {
        var a = c.Aims.Count > 1 ? c.Center : c.Aim;
        var rx = Math.Max(130f, (c.MaxX - c.MinX) / 2 + 110f);
        var s = c.Side;
        Vector2 At(float th) => a + new Vector2(Mathf.Cos(th) * rx, Mathf.Sin(th) * 90f);
        var th0 = Mathf.Pi + 0.4f * s;
        var start = At(th0);
        Go(c, start, 0.06, Tween.TransitionType.Quad, Tween.EaseType.Out, face: true);
        Path(c, u => At(th0 + s * Mathf.Tau * 0.55f * u), 0.07, trans: Tween.TransitionType.Sine, ease: Tween.EaseType.In);
        Contact(c, new Vector2(0, s));
        var th1 = th0 + s * Mathf.Tau * 0.55f;
        Path(c, u => At(th1 + s * Mathf.Tau * 0.6f * u), 0.13);
        Hold(c, 0.04);
        Return(c, 0.32, lift: 110f);
    }

    // ------------------------------------------------------------------ Ganjiang · Moye

    /// <summary>Twin crossing cuts: the two blades cut the target along both diagonals, an X.</summary>
    private static void TwinCross(StrikeCtx c)
    {
        var a = c.Aim; var s = c.Side;
        var p0 = a + new Vector2(-90f, -110f * s); var p1 = a + new Vector2(90f, 95f * s);
        var q0 = a + new Vector2(90f, -110f * s); var q1 = a + new Vector2(-90f, 95f * s);
        Go(c, p0, 0.07, Tween.TransitionType.Quad, Tween.EaseType.Out, face: true);
        Face(c, p1, 0.02);
        Go(c, p1, 0.05, Tween.TransitionType.Expo, Tween.EaseType.In);
        Contact(c, p1 - p0);
        Hold(c, 0.06);
        Return(c, 0.3);
        if (Partner(c) is { } pc)
        {
            pc.Tw.TweenInterval(0.02);
            Go(pc, q0, 0.07, Tween.TransitionType.Quad, Tween.EaseType.Out, face: true);
            Face(pc, q1, 0.02);
            Go(pc, q1, 0.05, Tween.TransitionType.Expo, Tween.EaseType.In);
            Hold(pc, 0.06);
            Return(pc, 0.32);
            FinishPartner(pc);
        }
        else Phantom(c, q0, q1, 0.07, 0.06, PartnerColor(c.Sword));
    }

    /// <summary>Alternating stabs: one blade, then the other, then the first again — each from its own angle.</summary>
    private static void TwinAlternate(StrikeCtx c)
    {
        var a = c.Aim; var p = c.Perp;
        var hit = a - c.Dir * 30f;
        Face(c, hit + p * 25f, 0.03);
        Go(c, hit + p * 25f, 0.09, Tween.TransitionType.Quad, Tween.EaseType.In);
        Contact(c, c.Dir);
        var back1 = a - c.Dir * 170f + p * 90f;
        Go(c, back1, 0.1, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        Face(c, a, 0.06, parallel: true);
        Go(c, a + (a - back1).Normalized() * 30f, 0.06, Tween.TransitionType.Expo, Tween.EaseType.In);
        Hold(c, 0.05);
        Return(c, 0.3);
        var from2 = a - c.Dir * 200f - p * 110f;
        if (Partner(c) is { } pc)
        {
            pc.Tw.TweenInterval(0.08);
            Face(pc, a, 0.03);
            Go(pc, a - (a - pc.At).Normalized() * 20f - p * 20f, 0.09, Tween.TransitionType.Quad, Tween.EaseType.In);
            Hold(pc, 0.06);
            Return(pc, 0.32);
            FinishPartner(pc);
        }
        else Phantom(c, from2, a - p * 15f, 0.15, 0.06, PartnerColor(c.Sword));
    }

    /// <summary>Spiral pair: the two blades corkscrew round each other on the way in and strike together (heavy).</summary>
    private static void TwinSpiral(StrikeCtx c)
    {
        var a = c.Aim; var d = c.Dir; var p = new Vector2(-d.Y, d.X);
        var from = c.At; var hit = a - d * 35f;
        c.Heavy = true;
        Face(c, hit, 0.03);
        Path(c, u => from.Lerp(hit, u) + p * Mathf.Sin(u * Mathf.Tau * 1.25f) * 70f * (1 - u), 0.11,
            trans: Tween.TransitionType.Sine, ease: Tween.EaseType.In);
        Contact(c, d);
        Go(c, hit + d * 40f, 0.06, Tween.TransitionType.Sine, Tween.EaseType.Out);
        Hold(c, 0.05);
        Return(c, 0.32, lift: 90f);
        if (Partner(c) is { } pc)
        {
            var pf = pc.At;
            Path(pc, u => pf.Lerp(hit + p * 12f, u) - p * Mathf.Sin(u * Mathf.Tau * 1.25f) * 70f * (1 - u), 0.14,
                trans: Tween.TransitionType.Sine, ease: Tween.EaseType.In);
            Go(pc, hit + p * 12f + d * 40f, 0.06, Tween.TransitionType.Sine, Tween.EaseType.Out);
            Hold(pc, 0.05);
            Return(pc, 0.34, lift: 60f);
            FinishPartner(pc);
        }
        else
        {
            // a phantom of the twin winds round the other way
            Phantom(c, from - p * 70f, hit + p * 10f, 0.02, 0.11, PartnerColor(c.Sword), 0.6f);
        }
    }
}
