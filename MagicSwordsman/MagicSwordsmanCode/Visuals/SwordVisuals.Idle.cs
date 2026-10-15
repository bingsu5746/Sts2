using Godot;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Idle specials (user request 2026-10-15 "검들이 부유하다가 뭐 혼자 빙글빙글 돌던가 움직이던지 하는 특수 애니메이션 여러
/// 개"): every 6–12 s one floating sword, picked at random, does something on its own and settles back into its slot.
/// Generic: a full spin, a flourish twirl, a swoop around Ensifer and back, a bow toward the enemy, a sheathing slide
/// (sinks and dims, then is drawn up with a glint), two swords crossing briefly. Personal: Kusanagi spins up a wind of
/// leaves, Onimaru flashes a quick draw, Dainsleif twitches restlessly and sheds a drop, Durandal salutes in a little
/// light, Skofnung blinks away as a ghost and glides back, Tyrfing flares and smoulders. A sword's own special is picked
/// 45 % of the time when it has one.
/// Never while any sword is flying, guarding or in a 【조합】 choreography, never while Ensifer plays anything but idle,
/// never in the first seconds after a summon. The special's tween is the sword's rig.Moves entry, so a strike, guard or
/// union takes the sword over cleanly; while it runs <see cref="InFlight"/> is true, so a re-layout leaves it alone and
/// its own end hands the sword back (Layout only: it). Silent. Presentation only: every entry point swallows its errors.
/// The headless preview mirror (cv.gd, outside the repo; scenarios idle_*) ports these.
/// </summary>
public static partial class SwordVisuals
{
    private const string IdleUntilMeta = "ms_idle_until";
    private static readonly Random IdleRng = new();

    private enum IdleSpecial { Spin, Flourish, Swoop, Bow, Sheathe, Cross, WindSpin, QuickDraw, Twitch, Salute, GhostBlink, Smoulder }

    private static readonly IdleSpecial[] GenericSpecials =
        [IdleSpecial.Spin, IdleSpecial.Flourish, IdleSpecial.Swoop, IdleSpecial.Bow, IdleSpecial.Sheathe, IdleSpecial.Cross];

    private static IdleSpecial? PersonalSpecial(SwordId sword) => sword switch
    {
        SwordId.Kusanagi => IdleSpecial.WindSpin,
        SwordId.Onimaru => IdleSpecial.QuickDraw,
        SwordId.Dainsleif => IdleSpecial.Twitch,
        SwordId.Durandal => IdleSpecial.Salute,
        SwordId.Skofnung => IdleSpecial.GhostBlink,
        SwordId.Tyrfing => IdleSpecial.Smoulder,
        _ => null,
    };

    private static bool IsIdling(Node2D node) =>
        node.HasMeta(IdleUntilMeta) && Time.GetTicksMsec() < (ulong)node.GetMeta(IdleUntilMeta);

    /// <summary>Called once per rig (GetRig): the timer that now and then lets a sword do something on its own.</summary>
    private static void StartIdleSpecials(Rig rig)
    {
        var timer = new Godot.Timer { WaitTime = 7.0, Autostart = true, Name = "IdleSpecials" };
        timer.Timeout += () =>
        {
            try
            {
                timer.WaitTime = 6.0 + IdleRng.NextDouble() * 6.0;
                TryIdleSpecial(rig);
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[SwordVisuals] idle special: {e.Message}");
            }
        };
        rig.Root.AddChild(timer);
    }

    private static void TryIdleSpecial(Rig rig, IdleSpecial? force = null, SwordId? forceSword = null)
    {
        if (rig.Dead || !GodotObject.IsInstanceValid(rig.Root)) return;
        var clip = CurrentClip(rig.Player);
        if (clip != null && clip != "idle" && clip != "" && !clip.StartsWith("Idle_")) return; // he is doing something
        var now = Time.GetTicksMsec();
        var valid = rig.Swords.Where(kv => GodotObject.IsInstanceValid(kv.Value)).ToList();
        if (valid.Any(kv => InFlight(kv.Value) || IsUnionBusy(kv.Value))) return; // a flight, guard or union is on
        var free = valid.Where(kv =>
                !(kv.Value.HasMeta(BornMeta) && now - (ulong)kv.Value.GetMeta(BornMeta) < 2500) &&
                !(kv.Key == SwordId.Onimaru && OnimaruKindOrIai(rig) == OnimaruKind.Ranbu) &&
                rig.Slots.ContainsKey(kv.Key))
            .Select(kv => kv.Key).ToList();
        if (forceSword is { } fs) free = free.Where(s => s == fs).ToList();
        if (free.Count == 0) return;
        var sword = free[IdleRng.Next(free.Count)];
        var special = force ?? (PersonalSpecial(sword) is { } own && IdleRng.NextDouble() < 0.45
            ? own : GenericSpecials[IdleRng.Next(GenericSpecials.Length)]);
        SwordId? partner = null;
        if (special == IdleSpecial.Cross)
        {
            var others = free.Where(s => s != sword).ToList();
            if (others.Count == 0) special = IdleSpecial.Spin;
            else partner = others[IdleRng.Next(others.Count)];
        }
        PlayIdleSpecial(rig, sword, special, partner);
    }

    private static OnimaruKind OnimaruKindOrIai(Rig rig)
    {
        try { return OnimaruAttack.GetKind(rig.Player); }
        catch (Exception) { return OnimaruKind.Iai; }
    }

    /// <summary>Starts <paramref name="special"/> on <paramref name="sword"/> (and <paramref name="partner"/> for Cross).</summary>
    private static void PlayIdleSpecial(Rig rig, SwordId sword, IdleSpecial special, SwordId? partner = null)
    {
        var node = rig.Swords[sword];
        var home = rig.Slots[sword];
        if (rig.Moves.TryGetValue(sword, out var old) && GodotObject.IsInstanceValid(old)) old.Kill();
        // start exactly at rest (the layout tween may still have been settling)
        node.Position = home;
        var r0 = node.Rotation;
        var tw = node.CreateTween();
        rig.Moves[sword] = tw;
        var col = ColorOf(sword);
        var dur = BuildIdle(rig, node, sword, special, tw, home, r0, col, partner);
        node.SetMeta(IdleUntilMeta, Time.GetTicksMsec() + (ulong)(dur * 1000) + 150);
        tw.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            node.SetMeta(IdleUntilMeta, 0UL);
            Layout(rig, only: sword);
        }));
    }

    private const Tween.TransitionType Sine = Tween.TransitionType.Sine;
    private const Tween.EaseType InOut = Tween.EaseType.InOut;

    /// <summary>Builds the special on <paramref name="tw"/>; returns its length (s).</summary>
    private static double BuildIdle(Rig rig, Node2D node, SwordId sword, IdleSpecial special, Tween tw, Vector2 home, float r0,
        Color col, SwordId? partner)
    {
        var root = rig.Root;
        switch (special)
        {
            case IdleSpecial.Spin: // rises a little, one full turn, settles
                tw.TweenProperty(node, "position", home + new Vector2(0, -16), 0.3).SetTrans(Sine).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(node, "rotation", r0 + Mathf.Tau, 0.85).SetTrans(Tween.TransitionType.Cubic).SetEase(InOut);
                tw.TweenProperty(node, "position", home, 0.35).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenCallback(Callable.From(() => node.Rotation = r0));
                return 1.5;

            case IdleSpecial.Flourish: // cocks back, twirls twice as it springs up, overshoots and settles
                tw.TweenProperty(node, "rotation", r0 - 0.45f, 0.22).SetTrans(Sine).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(node, "rotation", r0 + Mathf.Tau * 2f + 0.12f, 0.75).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "position", home + new Vector2(10, -28), 0.4).SetTrans(Sine).SetEase(Tween.EaseType.Out);
                tw.TweenCallback(Callable.From(() => node.Rotation = r0 + 0.12f));
                tw.TweenProperty(node, "rotation", r0, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "position", home, 0.4).SetTrans(Sine).SetEase(InOut);
                tw.TweenCallback(Callable.From(() => SwordFx.Glint(root, node.Position + new Vector2(0, -80), col, 90f)));
                return 1.7;

            case IdleSpecial.Swoop: // a loop around Ensifer (in front of him on the near side) and back to its place
            {
                var c = SpawnPos + new Vector2(0, -20);
                var off = home - c;
                var th0 = Mathf.Atan2(off.Y / 0.42f, off.X);
                var r = Mathf.Max(80f, new Vector2(off.X, off.Y / 0.42f).Length());
                Vector2 At(float u)
                {
                    var th = th0 + Mathf.Tau * u;
                    var swell = Mathf.Sin(u * Mathf.Pi); // swings wider mid-loop
                    var rr = r + (240f - r) * swell;
                    return c + new Vector2(Mathf.Cos(th) * rr, Mathf.Sin(th) * rr * 0.42f) + new Vector2(0, -30f * swell);
                }
                var z0 = node.ZIndex;
                tw.TweenMethod(Callable.From<float>(u =>
                {
                    node.Position = At(u);
                    var ahead = At(Mathf.Min(1f, u + 0.02f)) - At(Mathf.Max(0f, u - 0.02f));
                    // blade along the flight in the middle of the loop, upright again at both ends
                    var along = Mathf.Atan2(ahead.Y, ahead.X) + Mathf.Pi / 2;
                    var k = Mathf.SmoothStep(0f, 0.2f, u) * (1f - Mathf.SmoothStep(0.8f, 1f, u));
                    node.Rotation = Mathf.LerpAngle(r0, along, k);
                    node.ZIndex = Mathf.Sin(th0 + Mathf.Tau * u) > 0.1f ? ZFront : z0;
                }), 0f, 1f, 1.8).SetTrans(Sine).SetEase(InOut);
                tw.TweenCallback(Callable.From(() => { node.ZIndex = z0; node.Rotation = r0; }));
                Trail(node, 1.6, col);
                return 1.8;
            }

            case IdleSpecial.Bow: // tips toward the enemy and dips, holds, rises
                tw.TweenProperty(node, "rotation", r0 + 0.8f, 0.45).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "position", home + new Vector2(8, 18), 0.45).SetTrans(Sine).SetEase(InOut);
                tw.TweenInterval(0.4);
                tw.TweenProperty(node, "rotation", r0, 0.5).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "position", home, 0.5).SetTrans(Sine).SetEase(InOut);
                return 1.4;

            case IdleSpecial.Sheathe: // sinks into an unseen scabbard and dims, then is drawn up with a glint
            {
                var a0 = node.Modulate.A;
                tw.TweenProperty(node, "position", home + new Vector2(0, 52), 0.55).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "modulate:a", a0 * 0.25f, 0.55);
                tw.TweenInterval(0.35);
                tw.TweenProperty(node, "position", home + new Vector2(0, -14), 0.16).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "modulate:a", a0, 0.12);
                tw.Parallel().TweenCallback(Callable.From(() => SwordFx.Glint(root, home + new Vector2(0, -100), col, 140f)));
                tw.TweenProperty(node, "position", home, 0.35).SetTrans(Sine).SetEase(InOut);
                return 1.45;
            }

            case IdleSpecial.Cross when partner is { } p && rig.Swords.TryGetValue(p, out var other) && GodotObject.IsInstanceValid(other):
            {
                // both meet halfway and cross into an X for a moment, a soft glint where they touch
                var otherHome = rig.Slots[p];
                var mid = (home + otherHome) / 2 + new Vector2(0, -36);
                var leftFirst = home.X <= otherHome.X;
                var mine = mid + new Vector2(leftFirst ? -16 : 16, 0);
                var theirs = mid + new Vector2(leftFirst ? 16 : -16, 0);
                tw.TweenProperty(node, "position", mine, 0.45).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "rotation", leftFirst ? 0.55f : -0.55f, 0.45).SetTrans(Sine).SetEase(InOut);
                tw.TweenCallback(Callable.From(() => SwordFx.Glint(root, mid + new Vector2(0, -20), col.Lerp(ColorOf(p), 0.5f), 120f)));
                tw.TweenInterval(0.35);
                if (rig.Moves.TryGetValue(p, out var om) && GodotObject.IsInstanceValid(om)) om.Kill();
                other.Position = otherHome;
                var ot = other.CreateTween();
                rig.Moves[p] = ot;
                other.SetMeta(IdleUntilMeta, Time.GetTicksMsec() + 950);
                var z = other.ZIndex;
                other.ZIndex = node.ZIndex + 1 > ZFlying ? ZFlying : Math.Max(node.ZIndex, ZFront);
                ot.TweenProperty(other, "position", theirs, 0.45).SetTrans(Sine).SetEase(InOut);
                ot.Parallel().TweenProperty(other, "rotation", leftFirst ? -0.55f : 0.55f, 0.45).SetTrans(Sine).SetEase(InOut);
                ot.TweenInterval(0.35);
                ot.TweenCallback(Callable.From(() =>
                {
                    if (!GodotObject.IsInstanceValid(other)) return;
                    other.ZIndex = z;
                    other.SetMeta(IdleUntilMeta, 0UL);
                    Layout(rig, only: p);
                }));
                return 0.8;
            }

            case IdleSpecial.WindSpin: // Kusanagi: a turn and a half on a gust, leaves whirling round it
                tw.TweenCallback(Callable.From(() => SwordFx.IdleWind(root, home)));
                tw.TweenProperty(node, "rotation", r0 + Mathf.Tau * 1.5f, 0.9).SetTrans(Tween.TransitionType.Quart).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "position", home + new Vector2(-14, -22), 0.45).SetTrans(Sine).SetEase(Tween.EaseType.Out);
                tw.TweenCallback(Callable.From(() => node.Rotation = r0 + Mathf.Pi));
                tw.TweenProperty(node, "rotation", r0 + Mathf.Tau, 0.45).SetTrans(Sine).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "position", home, 0.45).SetTrans(Sine).SetEase(InOut);
                tw.TweenCallback(Callable.From(() => node.Rotation = r0));
                return 1.4;

            case IdleSpecial.QuickDraw: // Onimaru: a breath of stillness, then one flash of a draw toward the enemy
                tw.TweenProperty(node, "rotation", r0 - 0.35f, 0.35).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "position", home + new Vector2(-10, 6), 0.35).SetTrans(Sine).SetEase(InOut);
                tw.TweenInterval(0.2);
                tw.TweenProperty(node, "rotation", Mathf.Pi / 2, 0.07).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "position", home + new Vector2(46, -24), 0.07).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                tw.TweenCallback(Callable.From(() => SwordFx.IdleInkFlash(root, home + new Vector2(90, -24))));
                tw.TweenInterval(0.35);
                tw.TweenProperty(node, "rotation", r0, 0.4).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "position", home, 0.4).SetTrans(Sine).SetEase(InOut);
                return 1.4;

            case IdleSpecial.Twitch: // Dainsleif: restless jerks, as if it wanted to be drawn; one drop falls
            {
                float[] jerks = [0.16f, -0.1f, 0.22f, -0.18f, 0.08f, -0.24f, 0.12f];
                for (var i = 0; i < jerks.Length; i++)
                {
                    var j = jerks[i] * (0.8f + (float)IdleRng.NextDouble() * 0.4f);
                    tw.TweenProperty(node, "rotation", r0 + j, 0.05 + 0.03 * (i % 3)).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
                    tw.Parallel().TweenProperty(node, "position", home + new Vector2(j * 22f, -Math.Abs(j) * 12f), 0.05).SetTrans(Tween.TransitionType.Expo);
                    if (i == 3) tw.TweenInterval(0.25);
                }
                tw.TweenCallback(Callable.From(() => SwordFx.IdleDrop(root, home + new Vector2(0, 90))));
                tw.TweenProperty(node, "rotation", r0, 0.3).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "position", home, 0.3).SetTrans(Sine).SetEase(InOut);
                return 1.1;
            }

            case IdleSpecial.Salute: // Durandal: rises upright in a little light, dips its point to the enemy, returns
                tw.TweenProperty(node, "position", home + new Vector2(0, -34), 0.45).SetTrans(Sine).SetEase(Tween.EaseType.Out);
                tw.Parallel().TweenProperty(node, "rotation", r0, 0.3);
                tw.TweenCallback(Callable.From(() => SwordFx.IdleHalo(root, home + new Vector2(0, -34))));
                tw.TweenInterval(0.35);
                tw.TweenProperty(node, "rotation", r0 + 0.6f, 0.35).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "position", home + new Vector2(12, -18), 0.35).SetTrans(Sine).SetEase(InOut);
                tw.TweenInterval(0.25);
                tw.TweenProperty(node, "rotation", r0, 0.45).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "position", home, 0.5).SetTrans(Sine).SetEase(InOut);
                return 1.9;

            case IdleSpecial.GhostBlink: // Skofnung: fades out, reappears a little away as a pale ghost, glides home
            {
                var a0 = node.Modulate.A;
                var away = home + new Vector2(-58, -34);
                tw.TweenProperty(node, "modulate:a", 0f, 0.25);
                tw.TweenCallback(Callable.From(() => { node.Position = away; node.Rotation = r0 - 0.2f; }));
                tw.TweenProperty(node, "modulate", new Color(0.75f, 0.9f, 1f, a0 * 0.8f), 0.3);
                tw.TweenInterval(0.25);
                tw.TweenProperty(node, "position", home, 0.7).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "rotation", r0, 0.7).SetTrans(Sine).SetEase(InOut);
                tw.Parallel().TweenProperty(node, "modulate", new Color(node.Modulate, a0), 0.7);
                Trail(node, 1.4, new Color(0.7f, 0.85f, 1f));
                return 1.5;
            }

            case IdleSpecial.Smoulder: // Tyrfing: shivers with heat, flares and sheds embers
                tw.TweenCallback(Callable.From(() => SwordFx.IdleEmbers(root, home)));
                for (var i = 0; i < 6; i++)
                    tw.TweenProperty(node, "position", home + new Vector2(i % 2 == 0 ? 3 : -3, -i * 2), 0.06);
                tw.TweenProperty(node, "modulate", new Color(1.5f, 1.15f, 0.85f, node.Modulate.A), 0.2);
                tw.TweenProperty(node, "modulate", node.Modulate, 0.6).SetTrans(Sine);
                tw.Parallel().TweenProperty(node, "position", home, 0.6).SetTrans(Sine).SetEase(InOut);
                return 1.2;

            default:
                return BuildIdle(rig, node, sword, IdleSpecial.Spin, tw, home, r0, col, null);
        }
    }
}

public static partial class SwordFx
{
    // ------------------------------------------------------------------ idle special accents (SwordVisuals.Idle.cs)

    /// <summary>Kusanagi's idle wind: a few leaves whirled round the blade.</summary>
    internal static void IdleWind(Node2D rigRoot, Vector2 pos)
    {
        try
        {
            var fx = Root(rigRoot, pos, 1.4, 2);
            Burst(fx, Vector2.Zero, Ramp(new Color(0.55f, 0.85f, 0.5f, 0.9f)), "fx_leaf", 9, 1.0, 40, 80, Vector2.Zero, 180,
                0.3f, 0.5f, false, circle: 60, tangential: 420, radial: -30, spin: 300);
            Arc(fx, Vector2.Zero, 70, 150, 470, new Color(0.6f, 0.95f, 0.65f, 0.5f), 5, 0.7);
        }
        catch (Exception) { /* presentation only */ }
    }

    /// <summary>Onimaru's idle draw: a thin cut of light and a wisp of ink where the point stops.</summary>
    internal static void IdleInkFlash(Node2D rigRoot, Vector2 pos)
    {
        try
        {
            var fx = Root(rigRoot, pos, 0.8, 2);
            Beam(fx, Vector2.Zero, Mathf.Pi / 2, new Color(0.85f, 0.75f, 1f), 200, 8, 0.25);
            Stroke(fx, new Vector2(-110, 6), 0f, new Color(0.12f, 0.07f, 0.16f, 0.6f), 200, 14, 0.5, false);
        }
        catch (Exception) { /* presentation only */ }
    }

    /// <summary>Dainsleif's idle twitch: one dark drop falling from the blade.</summary>
    internal static void IdleDrop(Node2D rigRoot, Vector2 pos)
    {
        try
        {
            var fx = Root(rigRoot, pos, 1.0, 2);
            Burst(fx, Vector2.Zero, Ramp(new Color(0.5f, 0.04f, 0.09f, 0.9f)), "fx_drop", 1, 0.8, 5, 10, new Vector2(0, 500), 5,
                0.2f, 0.24f, false, dir: 90, alignY: true);
        }
        catch (Exception) { /* presentation only */ }
    }

    /// <summary>Durandal's salute: a soft column and a bloom of pale light behind it.</summary>
    internal static void IdleHalo(Node2D rigRoot, Vector2 pos)
    {
        try
        {
            var back = Root(rigRoot, pos, 1.3, -1);
            Shaft(back, new Vector2(0, -30), new Color(1f, 0.95f, 0.8f), 380, 70, 0.28f, 1.1);
            Flare(back, new Vector2(0, -40), new Color(1f, 0.95f, 0.8f), 170, 0.7);
        }
        catch (Exception) { /* presentation only */ }
    }

    /// <summary>Tyrfing smoulders: embers rising off the blade.</summary>
    internal static void IdleEmbers(Node2D rigRoot, Vector2 pos)
    {
        try
        {
            var fx = Root(rigRoot, pos, 1.4, 2);
            Motes(fx, new Vector2(0, -10), FadeRamp(new Color(1f, 0.65f, 0.3f, 0.9f), new Color(0.5f, 0.42f, 0.4f, 0.5f)), "fx_glow", 12, 1.1,
                new Vector2(0, -50), 0.04f, 0.08f, true, rect: new Vector2(20, 80));
            Flare(fx, Vector2.Zero, new Color(1f, 0.55f, 0.2f), 150, 0.5, delay: 0.35);
        }
        catch (Exception) { /* presentation only */ }
    }
}
