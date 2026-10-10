using Godot;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Presentation only: each sword's own summon, switch-in and hit effects (user request 2026-10-09 "검마다 다른 연출").
/// Built from plain Godot 2D nodes (Sprite2D, CpuParticles2D, Line2D, tweens) and the white textures in
/// images/vfx/fx (tools/gen_fx_textures.py), tinted with <see cref="SwordVisuals.ColorOf"/>. Every effect is short
/// (≤ 0.6 s; the quiet summon ≈ 0.9 s), local (no full-screen flashes) and frees itself. Every entry point swallows
/// its errors.
///
///  - Summon (<see cref="Summon"/>): also sets the sword's starting pose (the layout tween in SwordVisuals then carries it
///    to its slot), so e.g. Durandal really descends and Skofnung really condenses.
///  - Switch (<see cref="Switch"/>): at the current-sword slot, a ring + a smaller accent of the sword's summon.
///  - Impact (<see cref="Impact"/>): on the enemy, when our hit lands (Mangeomchong.AfterDamageReceived).
/// tools/fx_preview.gd mirrors these effects in GDScript for headless renders — keep the two in step.
/// UNVERIFIED in game: sizes against the combat camera scaling, z-order against enemy sprites.
/// </summary>
public static partial class SwordFx
{
    // ------------------------------------------------------------------ entry points

    /// <summary>
    /// A sword appears for the first time this combat. <paramref name="sword"/> node was just created; its layout tween
    /// (to <paramref name="slot"/>, 0.85 s, SwordVisuals.Layout) starts on the next frame and reads the pose set here.
    /// Restrained and classical (user request 2026-10-10 "좀 더 멋지게... 초등학생이 좋아할 느낌 말고 좀 고풍스럽게"):
    /// the blade rises slowly out of nothing while a thin brush circle (ensō) draws itself behind it, the sword's own
    /// sigil surfaces faintly and a soft light stands under it; a few motes drift — embers, leaves, ash, frost or ink
    /// depending on the sword. Muted colours, no bursts, no spinning, about 0.9 s.
    /// </summary>
    internal static void Summon(Node2D rigRoot, Node2D swordNode, SwordId sword, Vector2 slot)
    {
        try
        {
            Sfx.Summon(sword);
            var modes = SummonModesOf(sword);
            switch (modes[SummonRng.Next(modes.Length)])
            {
                case SummonMode.Descend: SummonDescend(rigRoot, swordNode, sword, slot); break;
                case SummonMode.Gather: SummonGather(rigRoot, swordNode, sword, slot); break;
                case SummonMode.Ripple: SummonRipple(rigRoot, swordNode, sword, slot); break;
                case SummonMode.Tomb: SummonTomb(rigRoot, swordNode, sword, slot); break;
                default: SummonRise(rigRoot, swordNode, sword, slot); break;
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] summon {sword}: {e.Message}");
        }
    }

    /// <summary>
    /// The ways a sword can arrive (user request 2026-10-10 "검들 소환 모션도 좀 더 늘려줘"), all quiet and classical:
    /// Rise — out of a brush circle (ensō), the original; Descend — from high above, point down, turning upright as it
    /// settles onto a faint ring; Gather — motes (sparks, ash, leaves, frost, shards...) drift together and the blade
    /// condenses out of them; Ripple — drawn out of a ripple in the air, from small to full size; Tomb — rising out of
    /// the tomb sigil (images/vfx/magic_circle.png) behind Ensifer and gliding to its place.
    /// </summary>
    private enum SummonMode { Rise, Descend, Gather, Ripple, Tomb }

    private static readonly Random SummonRng = new();

    private static SummonMode[] SummonModesOf(SwordId sword) => sword switch
    {
        SwordId.Gram => [SummonMode.Rise, SummonMode.Gather, SummonMode.Descend],          // shards reforging
        SwordId.Tyrfing => [SummonMode.Rise, SummonMode.Gather, SummonMode.Tomb],          // ash kindling
        SwordId.Dainsleif => [SummonMode.Rise, SummonMode.Tomb, SummonMode.Ripple],
        SwordId.Skofnung => [SummonMode.Rise, SummonMode.Gather, SummonMode.Ripple],       // frost mist
        SwordId.Durandal => [SummonMode.Rise, SummonMode.Descend, SummonMode.Gather],
        SwordId.ClaiomhSolais => [SummonMode.Rise, SummonMode.Descend, SummonMode.Gather],
        SwordId.Caladbolg => [SummonMode.Rise, SummonMode.Ripple, SummonMode.Descend],
        SwordId.Kusanagi => [SummonMode.Rise, SummonMode.Gather, SummonMode.Ripple],       // leaves on the wind
        SwordId.Onimaru => [SummonMode.Rise, SummonMode.Tomb, SummonMode.Ripple],
        SwordId.Ganjiang or SwordId.Moye => [SummonMode.Rise, SummonMode.Gather, SummonMode.Descend],
        _ => [SummonMode.Rise, SummonMode.Gather],
    };

    /// <summary>Rise: the blade rises slowly out of nothing while an ensō draws itself behind it (the original summon).</summary>
    private static void SummonRise(Node2D rigRoot, Node2D swordNode, SwordId sword, Vector2 slot)
    {
        {
            var col = SwordVisuals.ColorOf(sword);
            var soft = Muted(col);
            var fx = Root(rigRoot, slot, 1.4, 2);
            var back = Root(rigRoot, slot, 1.4, -1);
            // the blade rises the last few dozen pixels into its place, fading in (the layout tween moves it)
            Pose(swordNode, slot + new Vector2(0, 46), swordNode.Scale.X, 0f, new Color(soft.Lightened(0.3f), 0f));

            Shaft(back, new Vector2(0, -10), soft.Lightened(0.25f), 330, 70, 0.28f, 0.95);
            Sigil(back, Vector2.Zero, sword, soft, 0.32f, 0.95);
            switch (sword)
            {
                case SwordId.Onimaru: // ink: a dark ensō and one level stroke of the brush
                    Enso(back, Vector2.Zero, 92, new Color(0.13f, 0.08f, 0.18f, 0.85f), 7, 0.9, additive: false);
                    Stroke(back, new Vector2(-110, 66), 0f, new Color(0.14f, 0.09f, 0.2f, 0.5f), 220, 9, 0.9, false, delay: 0.25);
                    Motes(fx, Vector2.Zero, new Color(0.2f, 0.14f, 0.26f, 0.7f), "fx_ink", 6, 0.9, new Vector2(0, 30), 0.06f, 0.12f, false,
                        rect: new Vector2(70, 60), delay: 0.2);
                    break;
                case SwordId.Durandal: // a stronger standing light, pale motes settling down through it
                    Shaft(back, new Vector2(0, -60), new Color(1f, 0.95f, 0.8f), 520, 90, 0.35f, 0.95);
                    Enso(back, Vector2.Zero, 96, new Color(soft, 0.55f), 4, 0.9);
                    Motes(fx, new Vector2(0, -150), new Color(1f, 0.96f, 0.85f, 0.7f), "fx_glow", 9, 1.0, new Vector2(0, 40), 0.04f, 0.08f, true,
                        rect: new Vector2(30, 40));
                    break;
                case SwordId.Tyrfing: // embers cooling to ash
                    Enso(back, Vector2.Zero, 92, new Color(soft, 0.55f), 4, 0.9);
                    Motes(fx, new Vector2(0, -40), FadeRamp(new Color(1f, 0.6f, 0.3f, 0.75f), new Color(0.45f, 0.4f, 0.38f, 0.5f)), "fx_glow", 10, 1.0,
                        new Vector2(0, -25), 0.04f, 0.08f, true, rect: new Vector2(45, 80));
                    break;
                case SwordId.Kusanagi: // a few leaves carried past on a slow wind
                    Enso(back, Vector2.Zero, 92, new Color(soft, 0.55f), 4, 0.9);
                    Motes(fx, new Vector2(-70, -40), new Color(0.55f, 0.75f, 0.5f, 0.85f), "fx_leaf", 5, 1.1, new Vector2(40, 18), 0.3f, 0.45f, false,
                        rect: new Vector2(30, 70), spin: 60);
                    break;
                case SwordId.Skofnung: // frost mist gathering about the blade
                    Enso(back, Vector2.Zero, 92, new Color(soft, 0.5f), 4, 0.9);
                    Motes(back, Vector2.Zero, new Color(0.8f, 0.9f, 1f, 0.22f), "fx_smoke", 6, 1.0, Vector2.Zero, 0.4f, 0.6f, true,
                        circle: 80, radial: -40);
                    break;
                case SwordId.Dainsleif: // a dark red circle; a few slow drops fall from the blade
                    Enso(back, Vector2.Zero, 92, new Color(0.5f, 0.1f, 0.14f, 0.7f), 5, 0.9, additive: false);
                    Motes(fx, new Vector2(0, 40), new Color(0.5f, 0.05f, 0.1f, 0.75f), "fx_drop", 4, 0.9, new Vector2(0, 160), 0.18f, 0.26f, false,
                        rect: new Vector2(6, 30), delay: 0.35);
                    break;
                case SwordId.ClaiomhSolais: // warm light, motes rising like dust in a sunbeam
                    Shaft(back, new Vector2(0, -40), new Color(1f, 0.97f, 0.85f), 420, 80, 0.3f, 0.95);
                    Enso(back, Vector2.Zero, 96, new Color(soft, 0.5f), 4, 0.9);
                    Motes(fx, new Vector2(0, -20), new Color(1f, 1f, 0.9f, 0.7f), "fx_glow", 9, 1.0, new Vector2(0, -30), 0.04f, 0.07f, true,
                        rect: new Vector2(40, 90));
                    break;
                case SwordId.Gram: // a few gold sparks rising, as from a cooling forge
                    Enso(back, Vector2.Zero, 94, new Color(soft, 0.55f), 4, 0.9);
                    Motes(fx, new Vector2(0, 40), new Color(1f, 0.85f, 0.5f, 0.75f), "fx_glow", 8, 1.0, new Vector2(0, -45), 0.04f, 0.07f, true,
                        rect: new Vector2(30, 60));
                    break;
                case SwordId.Ganjiang or SwordId.Moye: // two circles, bronze and silver, drawn against each other
                    Enso(back, Vector2.Zero, 96, new Color(Muted(SwordVisuals.ColorOf(SwordId.Ganjiang)), 0.55f), 4, 0.9);
                    Enso(back, Vector2.Zero, 84, new Color(Muted(SwordVisuals.ColorOf(SwordId.Moye)), 0.55f), 4, 0.9, fromDeg: 30, delay: 0.08);
                    break;
                default: // Caladbolg and anything else
                    Enso(back, Vector2.Zero, 94, new Color(soft, 0.55f), 4, 0.9);
                    Motes(fx, Vector2.Zero, new Color(soft.Lightened(0.3f), 0.6f), "fx_glow", 7, 1.0, new Vector2(0, -20), 0.04f, 0.07f, true,
                        rect: new Vector2(40, 80));
                    break;
            }
        }
    }

    /// <summary>The motes a sword is made of when it gathers / settles: texture, colour ramp, additive, spin.</summary>
    private static (string Tex, Gradient Ramp, bool Add, float Spin, float SMin, float SMax) MotesOf(SwordId sword)
    {
        var soft = Muted(SwordVisuals.ColorOf(sword));
        return sword switch
        {
            SwordId.Gram => ("fx_glow", FadeRamp(new Color(1f, 0.85f, 0.5f, 0.8f), new Color(1f, 0.75f, 0.35f, 0.7f)), true, 0f, 0.04f, 0.08f),
            // grey ash that kindles to embers as it gathers
            SwordId.Tyrfing => ("fx_glow", FadeRamp(new Color(0.5f, 0.46f, 0.44f, 0.6f), new Color(1f, 0.55f, 0.25f, 0.85f)), true, 0f, 0.04f, 0.08f),
            SwordId.Skofnung => ("fx_smoke", FadeRamp(new Color(0.8f, 0.9f, 1f, 0.2f), new Color(0.85f, 0.95f, 1f, 0.28f)), true, 30f, 0.3f, 0.5f),
            SwordId.Kusanagi => ("fx_leaf", FadeRamp(new Color(0.55f, 0.75f, 0.5f, 0.85f), new Color(0.6f, 0.82f, 0.55f, 0.8f)), false, 200f, 0.25f, 0.4f),
            SwordId.Dainsleif => ("fx_drop", FadeRamp(new Color(0.5f, 0.05f, 0.1f, 0.75f), new Color(0.45f, 0.04f, 0.08f, 0.7f)), false, 0f, 0.14f, 0.22f),
            SwordId.Onimaru => ("fx_ink", FadeRamp(new Color(0.2f, 0.14f, 0.26f, 0.7f), new Color(0.15f, 0.1f, 0.2f, 0.7f)), false, 40f, 0.06f, 0.12f),
            SwordId.Durandal or SwordId.ClaiomhSolais =>
                ("fx_glow", FadeRamp(new Color(1f, 0.97f, 0.85f, 0.75f), new Color(1f, 0.95f, 0.8f, 0.7f)), true, 0f, 0.04f, 0.08f),
            _ => ("fx_glow", FadeRamp(new Color(soft.Lightened(0.3f), 0.7f), new Color(soft.Lightened(0.2f), 0.6f)), true, 0f, 0.04f, 0.07f),
        };
    }

    /// <summary>
    /// Descend: from high above, point down, it comes down slowly and turns upright as it settles; a soft column of light
    /// marks its way and a faint ring opens flat under it when it arrives.
    /// </summary>
    private static void SummonDescend(Node2D rigRoot, Node2D swordNode, SwordId sword, Vector2 slot)
    {
        var soft = Muted(SwordVisuals.ColorOf(sword));
        var fx = Root(rigRoot, slot, 1.5, 2);
        var back = Root(rigRoot, slot, 1.5, -1);
        // the layout tween (0.85 s, sine) carries it down; its rotation tween turns it from point-down to upright
        Pose(swordNode, slot + new Vector2(0, -300), swordNode.Scale.X, Mathf.Pi, new Color(soft.Lightened(0.3f), 0f));
        var light = sword is SwordId.Durandal or SwordId.ClaiomhSolais ? new Color(1f, 0.95f, 0.8f) : soft.Lightened(0.25f);
        Shaft(back, new Vector2(0, -170), light, 560, 60, sword is SwordId.Durandal or SwordId.ClaiomhSolais ? 0.3f : 0.2f, 1.1);
        var m = MotesOf(sword);
        Motes(fx, new Vector2(0, -260), m.Ramp, m.Tex, 7, 1.1, new Vector2(0, 55), m.SMin, m.SMax, m.Add, rect: new Vector2(26, 60), spin: m.Spin);
        Ring(back, new Vector2(0, 92), new Color(soft * 0.75f, 1f), 20, 120, 0.6, delay: 0.62, squash: 0.28f);
        Enso(back, Vector2.Zero, 84, new Color(soft, 0.4f), 3, 0.8, delay: 0.45);
    }

    /// <summary>Gather: motes drift together from all round and the blade condenses out of them, a little late.</summary>
    private static void SummonGather(Node2D rigRoot, Node2D swordNode, SwordId sword, Vector2 slot)
    {
        var soft = Muted(SwordVisuals.ColorOf(sword));
        var fx = Root(rigRoot, slot, 1.5, 2);
        var back = Root(rigRoot, slot, 1.5, -1);
        Pose(swordNode, slot, swordNode.Scale.X, 0f, new Color(soft.Lightened(0.3f), 0f));
        // the blade itself stays unseen until the motes have mostly gathered (the holder's layout fade still runs)
        if (swordNode.GetNodeOrNull<Node2D>("Blade") is { } blade)
        {
            blade.Modulate = new Color(1, 1, 1, 0);
            var t = blade.CreateTween();
            t.TweenInterval(0.38);
            t.TweenProperty(blade, "modulate:a", 1f, 0.45).SetTrans(Tween.TransitionType.Sine);
        }
        var m = MotesOf(sword);
        Gathering(fx, m.Ramp, m.Tex, sword is SwordId.Skofnung ? 10 : 18, 150, 0.6, m.Add, m.Spin, m.SMin, m.SMax,
            swirl: sword is SwordId.Kusanagi or SwordId.Skofnung ? 260f : 0f);
        switch (sword)
        {
            case SwordId.Gram: // the shards of the broken blade come together (reforged)
                Converge(fx, Vector2.Zero, Muted(SwordVisuals.ColorOf(sword)).Lightened(0.2f), 7, 170, 0.45);
                break;
            case SwordId.Ganjiang or SwordId.Moye: // a bronze and a silver light winding in to each other
                Twin(fx, Vector2.Zero, 120, 0.5, sword == SwordId.Moye);
                break;
        }
        Sigil(back, Vector2.Zero, sword, soft, 0.28f, 1.0);
        Flare(back, Vector2.Zero, soft.Lightened(0.2f), 150, 0.5, delay: 0.35);
    }

    /// <summary>Ripple: rings spread through the air as through still water and the blade is drawn out of them.</summary>
    private static void SummonRipple(Node2D rigRoot, Node2D swordNode, SwordId sword, Vector2 slot)
    {
        var soft = Muted(SwordVisuals.ColorOf(sword));
        var back = Root(rigRoot, slot, 1.5, -1);
        var fx = Root(rigRoot, slot, 1.5, 2);
        // the layout tween grows it to its size: from small and far to here
        Pose(swordNode, slot + new Vector2(0, 12), swordNode.Scale.X * 0.35f, 0f, new Color(soft.Lightened(0.3f), 0f));
        var ring = sword switch
        {
            SwordId.Onimaru => new Color(0.35f, 0.25f, 0.45f),
            SwordId.Dainsleif => new Color(0.5f, 0.12f, 0.16f),
            _ => soft * 0.45f,
        };
        for (var i = 0; i < 3; i++)
            Ring(back, Vector2.Zero, new Color(ring, 1f), 16, 120 + i * 18, 0.8, delay: i * 0.15, squash: 0.92f);
        if (sword == SwordId.Caladbolg)
            Arc(back, Vector2.Zero, 70, -90, 270, Colors.White, 5, 0.9, rainbow: true, delay: 0.1, grow: 1.5f);
        if (sword == SwordId.Onimaru)
            Enso(back, Vector2.Zero, 88, new Color(0.13f, 0.08f, 0.18f, 0.8f), 6, 0.9, additive: false);
        Flare(back, Vector2.Zero, soft.Lightened(0.15f), 170, 0.7);
        var m = MotesOf(sword);
        Motes(fx, Vector2.Zero, m.Ramp, m.Tex, 5, 0.9, new Vector2(0, -10), m.SMin, m.SMax, m.Add, circle: 60, spin: m.Spin, delay: 0.3);
    }

    /// <summary>Behind Ensifer: the tomb sigil turns faintly where the swords sleep.</summary>
    private static readonly Vector2 TombPos = new(0, -250);

    /// <summary>Tomb: the tomb sigil surfaces behind him and the blade rises out of it, then glides to its place.</summary>
    private static void SummonTomb(Node2D rigRoot, Node2D swordNode, SwordId sword, Vector2 slot)
    {
        var soft = Muted(SwordVisuals.ColorOf(sword));
        var tomb = Root(rigRoot, TombPos, 1.5, -1);
        var fx = Root(rigRoot, slot, 1.5, 2);
        Pose(swordNode, TombPos + new Vector2(0, 40), swordNode.Scale.X * 0.75f, 0f, new Color(soft.Lightened(0.3f), 0f));
        // behind his body while it rises out of the sigil; then back to the z the layout gave it
        var z = swordNode.ZIndex;
        swordNode.ZIndex = 0;
        var zt = swordNode.CreateTween();
        zt.TweenInterval(0.4);
        zt.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(swordNode) && swordNode.ZIndex == 0) swordNode.ZIndex = z; }));
        var circleTex = T("magic_circle");
        if (circleTex != null)
        {
            var ink = sword is SwordId.Onimaru or SwordId.Dainsleif;
            var c = new Sprite2D
            {
                Texture = circleTex, Modulate = new Color(ink ? new Color(0.45f, 0.3f, 0.55f) : soft, 0f),
                Material = AddMat, Scale = Vector2.One * 300f / Math.Max(1, circleTex.GetWidth()),
            };
            tomb.AddChild(c);
            var t = c.CreateTween();
            t.TweenProperty(c, "modulate:a", 0.32f, 0.35).SetTrans(Tween.TransitionType.Sine);
            t.Parallel().TweenProperty(c, "rotation", 0.5f, 1.3).SetTrans(Tween.TransitionType.Sine);
            t.TweenProperty(c, "modulate:a", 0f, 0.8).SetTrans(Tween.TransitionType.Sine);
        }
        Sigil(tomb, Vector2.Zero, sword, soft, 0.35f, 1.0);
        Enso(tomb, Vector2.Zero, 120, sword == SwordId.Onimaru ? new Color(0.13f, 0.08f, 0.18f, 0.8f) : new Color(soft, 0.5f), 4, 1.0,
            additive: sword != SwordId.Onimaru);
        var m = MotesOf(sword);
        Motes(fx, Vector2.Zero, m.Ramp, m.Tex, 6, 0.9, new Vector2(0, -12), m.SMin, m.SMax, m.Add, rect: new Vector2(30, 70), spin: m.Spin, delay: 0.5);
    }

    /// <summary>Motes released on a ring round <paramref name="p"/> that drift in to its centre over <paramref name="dur"/>.</summary>
    private static void Gathering(Node2D p, Gradient ramp, string tex, int amount, float radius, double dur, bool additive,
        float spin, float sMin, float sMax, float swirl = 0f)
    {
        var accel = 2f * radius / (float)(dur * dur); // from rest at the ring to the centre in one lifetime
        var c = new CpuParticles2D
        {
            Amount = amount, Lifetime = dur, OneShot = true, Explosiveness = 0.55f, Randomness = 0.15f, Texture = T(tex),
            Spread = 180, Gravity = Vector2.Zero, InitialVelocityMin = 0, InitialVelocityMax = 4, ScaleAmountMin = sMin,
            ScaleAmountMax = sMax, ColorRamp = ramp, RadialAccelMin = -accel * 0.95f, RadialAccelMax = -accel,
            TangentialAccelMin = swirl * 0.8f, TangentialAccelMax = swirl, AngleMin = -180, AngleMax = 180,
            AngularVelocityMin = -spin, AngularVelocityMax = spin, EmissionShape = CpuParticles2D.EmissionShapeEnum.SphereSurface,
            EmissionSphereRadius = radius,
        };
        if (additive) c.Material = AddMat;
        p.AddChild(c);
        c.Emitting = true;
    }

    /// <summary>A short bright glint across <paramref name="pos"/> (a drawn cut, a flash of light).</summary>
    internal static void Glint(Node2D parent, Vector2 pos, Color col, float size)
    {
        try
        {
            var fx = Root(parent, pos, 0.5, 3);
            Flare(fx, Vector2.Zero, new Color(1f, 0.98f, 0.92f), size * 0.55f, 0.25);
            Beam(fx, Vector2.Zero, Mathf.Pi / 2 + (float)GD.RandRange(-0.15, 0.15), col.Lightened(0.3f), size * 1.6f, 12, 0.3);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] glint: {e.Message}");
        }
    }

    /// <summary>A sword colour taken towards old paper grey: the summons stay quiet.</summary>
    private static Color Muted(Color c) => c.Lerp(new Color(0.78f, 0.75f, 0.72f), 0.35f);

    /// <summary>
    /// Two blades meet (guard, a parried cut): a short bright spark and a few sparks thrown off. <paramref name="power"/>
    /// scales it (a blocked hit ≈ 1.4: the impact frame of the guard); from 1.2 up a thin white streak flashes across the
    /// contact at <paramref name="angle"/> (radians, the line of the blades).
    /// </summary>
    internal static void Clash(Node2D parent, Vector2 pos, Color col, float power = 1f, float angle = -0.6f)
    {
        try
        {
            Sfx.Guard();
            var fx = Root(parent, pos, 0.6, 3);
            Flare(fx, Vector2.Zero, new Color(1f, 0.97f, 0.9f), 120 * power, 0.22);
            Flare(fx, Vector2.Zero, col, 80 * power, 0.3);
            if (power >= 1.2f)
            {
                Beam(fx, Vector2.Zero, angle, new Color(1f, 0.98f, 0.92f), 150 * power, 10, 0.16);
                Ring(fx, Vector2.Zero, col.Lightened(0.35f), 12, 70 * power, 0.22);
            }
            Burst(fx, Vector2.Zero, Ramp(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.7f, 0.4f)), "fx_spark", (int)(12 * power), 0.3,
                160, 360 * power, new Vector2(0, 500), 180, 0.12f, 0.22f, true, alignY: true);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] clash: {e.Message}");
        }
    }

    /// <summary>
    /// A blade goes into Ensifer (death): a dull dark-red spatter thrown out along the blade's travel
    /// (<paramref name="dir"/>), a brief pale streak along it and a dark flash of the sword's colour, so every one of the
    /// blows reads on its own. No bright light.
    /// </summary>
    internal static void Pierce(Node2D parent, Vector2 pos, Vector2? dir = null, Color? col = null)
    {
        try
        {
            Sfx.Impact(null);
            var fx = Root(parent, pos, 0.8, 3);
            var d = dir ?? new Vector2(0.5f, -0.87f);
            var deg = Mathf.RadToDeg(d.Angle());
            Burst(fx, Vector2.Zero, Ramp(new Color(0.45f, 0.03f, 0.07f, 0.9f)), "fx_drop", 10, 0.45, 80, 220, new Vector2(0, 700), 45,
                0.14f, 0.24f, false, dir: dir == null ? -60 : deg, alignY: true);
            Blot(fx, Vector2.Zero, new Color(0.32f, 0.02f, 0.06f, 0.75f), 8, 34, 0.45, squash: 1f);
            if (dir == null) return;
            Beam(fx, -d * 40, d.Angle() + Mathf.Pi / 2, new Color(0.95f, 0.9f, 0.92f, 0.8f), 140, 7, 0.14);
            if (col is { } c) Flare(fx, Vector2.Zero, c.Darkened(0.2f), 90, 0.25);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] pierce: {e.Message}");
        }
    }

    /// <summary><paramref name="sword"/>'s impact on <paramref name="target"/> (Onimaru's attacks: not the current sword).</summary>
    internal static void ImpactWith(Creature target, SwordId sword)
    {
        try
        {
            var room = NCombatRoom.Instance;
            var node = room?.GetCreatureNode(target);
            if (room == null || node == null) return;
            Sfx.Impact(sword);
            var fx = new Node2D { Name = "MagicSwordImpact", ZIndex = 5 };
            room.CombatVfxContainer.AddChild(fx);
            fx.GlobalPosition = node.VfxSpawnPosition;
            FreeAfter(fx, 0.8);
            PlayImpact(fx, sword);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] impact {sword}: {e.Message}");
        }
    }

    /// <summary>The current sword changed to <paramref name="sword"/> (which is moving to <paramref name="pos"/>).</summary>
    internal static void Switch(Node2D rigRoot, SwordId sword, Vector2 pos)
    {
        try
        {
            Sfx.Switch(sword);
            var col = SwordVisuals.ColorOf(sword);
            var fx = Root(rigRoot, pos, 0.8, 2);
            var back = Root(rigRoot, pos, 0.8, -1);
            const double d = 0.12; // the sword is still flying in
            Ring(back, Vector2.Zero, col, 25, 125, 0.35, delay: d);
            Flare(back, Vector2.Zero, col, 150, 0.3, delay: d);
            switch (sword)
            {
                case SwordId.Tyrfing:
                    Burst(fx, Vector2.Zero, Ramp(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.3f, 0.08f)), "fx_spark", 16, 0.45,
                        120, 300, new Vector2(0, -300), 180, 0.2f, 0.4f, true, alignY: true, delay: d);
                    break;
                case SwordId.Skofnung:
                    Burst(back, Vector2.Zero, Ramp(new Color(0.75f, 0.9f, 1f, 0.45f)), "fx_smoke", 8, 0.45, 60, 120, Vector2.Zero,
                        180, 0.4f, 0.7f, true, delay: d);
                    break;
                case SwordId.Durandal:
                    Pillar(back, new Vector2(0, -80), new Color(1f, 0.92f, 0.65f), 340, 70, 0.4, delay: d - 0.05);
                    break;
                case SwordId.Kusanagi:
                    Burst(fx, Vector2.Zero, Ramp(new Color(0.5f, 0.85f, 0.45f)), "fx_leaf", 10, 0.45, 60, 90, Vector2.Zero, 180,
                        0.4f, 0.7f, false, circle: 70, tangential: 600, spin: 360, delay: d);
                    Arc(back, Vector2.Zero, 90, 160, 460, new Color(0.6f, 0.95f, 0.65f, 0.7f), 7, 0.35, delay: d);
                    break;
                case SwordId.Onimaru:
                    Stroke(back, new Vector2(-110, 70), -0.62f, new Color(0.1f, 0.05f, 0.14f, 0.9f), 240, 55, 0.4, false, delay: d - 0.06);
                    break;
                case SwordId.Dainsleif:
                    Burst(fx, new Vector2(0, 10), Ramp(new Color(0.55f, 0.03f, 0.08f, 0.95f)), "fx_drop", 6, 0.45, 20, 60,
                        new Vector2(0, 900), 10, 0.25f, 0.4f, false, dir: 90, rect: new Vector2(8, 60), alignY: true, delay: d);
                    break;
                case SwordId.ClaiomhSolais:
                    Rays(fx, Vector2.Zero, new Color(1f, 0.95f, 0.7f), 6, 160, 0.35, delay: d);
                    break;
                case SwordId.Caladbolg:
                    Arc(back, Vector2.Zero, 100, -90, 270, Colors.White, 8, 0.4, rainbow: true, delay: d);
                    break;
                case SwordId.Gram:
                    Burst(fx, Vector2.Zero, Ramp(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.5f, 0.15f)), "fx_spark", 14, 0.4,
                        150, 350, new Vector2(0, 600), 180, 0.2f, 0.35f, true, alignY: true, delay: d);
                    break;
                case SwordId.Ganjiang or SwordId.Moye:
                    var other = sword == SwordId.Moye ? SwordId.Ganjiang : SwordId.Moye;
                    Flare(fx, new Vector2(-45, 10), SwordVisuals.ColorOf(SwordId.Ganjiang), 100, 0.35, delay: d);
                    Flare(fx, new Vector2(45, -10), SwordVisuals.ColorOf(SwordId.Moye), 100, 0.35, delay: d);
                    Ring(back, Vector2.Zero, SwordVisuals.ColorOf(other), 20, 95, 0.35, delay: d + 0.05);
                    break;
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] switch {sword}: {e.Message}");
        }
    }

    private static readonly Dictionary<ulong, ulong> LastImpact = new();

    /// <summary>Our hit landed on <paramref name="target"/>: the current sword's impact on the enemy.</summary>
    public static void Impact(Player player, Creature target)
    {
        try
        {
            var room = NCombatRoom.Instance;
            var node = room?.GetCreatureNode(target);
            if (room == null || node == null) return;
            var now = Time.GetTicksMsec();
            if (LastImpact.TryGetValue(player.NetId, out var t0) && now - t0 < 60) return;
            LastImpact[player.NetId] = now;
            var sword = SwordCombat.CurrentSword(player);
            Sfx.Impact(sword);
            var container = room.CombatVfxContainer;
            var fx = new Node2D { Name = "MagicSwordImpact", ZIndex = 5 };
            container.AddChild(fx);
            fx.GlobalPosition = node.VfxSpawnPosition;
            FreeAfter(fx, 0.8);
            var accent = SwordVisuals.StrikeAccent(player); // the cut of the strike that just landed (SwordVisuals.Strikes.cs)
            PlayImpact(fx, sword, accent?.Cut, accent?.Heavy ?? false);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] impact: {e.Message}");
        }
    }

    /// <summary>
    /// The impact itself, centred on <paramref name="fx"/> (screen pixels). <paramref name="cut"/>: direction of the
    /// sword's cut (the strokes follow it, so successive hits of a flurry leave differently angled marks);
    /// <paramref name="heavy"/>: a heavy variant (slam, cleave, judgment) adds a ring across the ground.
    /// </summary>
    internal static void PlayImpact(Node2D fx, SwordId? sword, float? cut = null, bool heavy = false)
    {
        var rot = (float)GD.RandRange(-0.5, 0.5);
        var col = sword is { } s ? SwordVisuals.ColorOf(s) : new Color(0.85f, 0.65f, 1f);
        if (heavy)
        {
            Ring(fx, new Vector2(0, 120), col, 40, 300, 0.5, squash: 0.28f);
            Flare(fx, Vector2.Zero, new Color(1f, 0.97f, 0.9f), 260, 0.3);
        }
        // a cut's stroke grows along the cut; without one, a random slant as before
        float Along(float fallback) => cut ?? fallback;
        switch (sword)
        {
            case SwordId.Tyrfing: // a burst of fire
                Flare(fx, Vector2.Zero, col, 230, 0.4);
                Burst(fx, Vector2.Zero, Ramp(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.3f, 0.08f)), "fx_spark", 30, 0.5,
                    200, 480, new Vector2(0, -250), 180, 0.25f, 0.5f, true, alignY: true);
                Ring(fx, Vector2.Zero, new Color(1f, 0.45f, 0.15f), 30, 170, 0.35);
                break;
            case SwordId.Skofnung: // a pale ghost cut that leaves frost-mist
                Stroke(fx, new Vector2(-190, 0).Rotated(Along(rot - 0.5f)), Along(rot - 0.5f), new Color(0.7f, 0.9f, 1f, 0.8f), 380, 50, 0.35, true);
                Burst(fx, Vector2.Zero, Ramp(new Color(0.75f, 0.9f, 1f, 0.22f)), "fx_smoke", 6, 0.5, 60, 140, Vector2.Zero, 180, 0.5f, 0.8f, true);
                Burst(fx, Vector2.Zero, Ramp(new Color(0.85f, 0.95f, 1f)), "fx_shard", 10, 0.4, 150, 320, new Vector2(0, 400), 180, 0.25f, 0.45f, true, alignY: true);
                break;
            case SwordId.Durandal: // a cross of light
                Beam(fx, Vector2.Zero, 0, new Color(1f, 0.95f, 0.75f), 330, 26, 0.4);
                Beam(fx, Vector2.Zero, Mathf.Pi / 2, new Color(1f, 0.95f, 0.75f), 230, 22, 0.4, delay: 0.03);
                Flare(fx, Vector2.Zero, new Color(1f, 0.92f, 0.65f), 200, 0.35);
                break;
            case SwordId.Kusanagi: // three wind cuts and a scatter of grass
                for (var k = 0; k < 3; k++)
                    Slash(fx, Vector2.Zero, Along(rot) + k * Mathf.Tau / 3, new Color(0.55f, 0.95f, 0.6f, 0.6f), 0.75f, 0.32, delay: k * 0.04);
                Burst(fx, Vector2.Zero, Ramp(new Color(0.5f, 0.85f, 0.45f)), "fx_leaf", 14, 0.5, 150, 320, new Vector2(0, 300), 180,
                    0.4f, 0.7f, false, spin: 540);
                break;
            case SwordId.Onimaru: // an ink cross
                Stroke(fx, new Vector2(-170, 0).Rotated(0.7f), 0.7f, new Color(0.1f, 0.05f, 0.14f, 0.92f), 340, 60, 0.45, false);
                Stroke(fx, new Vector2(-170, 0).Rotated(-0.7f), -0.7f, new Color(0.1f, 0.05f, 0.14f, 0.92f), 340, 60, 0.45, false, delay: 0.06);
                Stroke(fx, new Vector2(-160, 0).Rotated(0.7f), 0.7f, new Color(0.65f, 0.45f, 0.95f, 0.5f), 320, 16, 0.3, true);
                Burst(fx, Vector2.Zero, Ramp(new Color(0.12f, 0.06f, 0.16f, 0.9f)), "fx_ink", 8, 0.45, 150, 320, new Vector2(0, 500), 180, 0.1f, 0.22f, false);
                break;
            case SwordId.Dainsleif: // a blood-red cut and spatter
                Stroke(fx, new Vector2(-180, 0).Rotated(Along(rot + 0.4f)), Along(rot + 0.4f), new Color(0.75f, 0.12f, 0.18f, 0.85f), 360, 40, 0.35, true);
                Blot(fx, new Vector2(10, 10), new Color(0.45f, 0.02f, 0.07f, 0.85f), 30, 130, 0.5, squash: 1f);
                Burst(fx, Vector2.Zero, Ramp(new Color(0.55f, 0.03f, 0.08f, 0.95f)), "fx_drop", 14, 0.5, 150, 380, new Vector2(0, 900), 180,
                    0.25f, 0.45f, false, alignY: true);
                break;
            case SwordId.ClaiomhSolais: // a starburst
                Rays(fx, Vector2.Zero, new Color(1f, 0.95f, 0.7f), 8, 220, 0.35);
                Flare(fx, Vector2.Zero, new Color(1f, 0.97f, 0.8f), 200, 0.3);
                break;
            case SwordId.Caladbolg: // a rainbow ring opening out
                Arc(fx, Vector2.Zero, 70, -90, 270, Colors.White, 12, 0.4, rainbow: true, grow: 2.2f);
                Burst(fx, Vector2.Zero, Ramp(new Color(0.6f, 1f, 1f), new Color(0.9f, 0.7f, 1f)), "fx_glow", 16, 0.45, 120, 300, Vector2.Zero, 180, 0.05f, 0.1f, true);
                break;
            case SwordId.Gram: // a heavy, golden shockwave
                Ring(fx, Vector2.Zero, col, 40, 230, 0.4);
                Ring(fx, Vector2.Zero, new Color(1f, 0.7f, 0.3f), 20, 150, 0.35, delay: 0.05);
                Flare(fx, Vector2.Zero, col, 220, 0.35);
                Burst(fx, Vector2.Zero, Ramp(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.5f, 0.15f)), "fx_spark", 24, 0.45,
                    250, 520, new Vector2(0, 700), 180, 0.25f, 0.5f, true, alignY: true);
                break;
            case SwordId.Ganjiang or SwordId.Moye: // two crossing cuts, red and blue
                var g = Along(0f);
                Stroke(fx, new Vector2(-170, 0).Rotated(g + 0.55f), g + 0.55f, SwordVisuals.ColorOf(SwordId.Ganjiang), 340, 36, 0.35, true);
                Stroke(fx, new Vector2(-170, 0).Rotated(g - 0.55f), g - 0.55f, SwordVisuals.ColorOf(SwordId.Moye), 340, 36, 0.35, true, delay: 0.05);
                Flare(fx, Vector2.Zero, new Color(0.85f, 0.6f, 0.95f), 170, 0.3, delay: 0.05);
                break;
            default: // no current sword: a plain violet cut
                Slash(fx, Vector2.Zero, Along(rot), new Color(col, 0.85f), 1f, 0.3);
                Flare(fx, Vector2.Zero, col, 170, 0.3);
                break;
        }
    }

    // ------------------------------------------------------------------ building blocks

    private static readonly Dictionary<string, Texture2D?> Tex = new();
    private static CanvasItemMaterial? _add;

    private static CanvasItemMaterial AddMat => _add ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    private static Texture2D? T(string name)
    {
        if (Tex.TryGetValue(name, out var t)) return t;
        t = null;
        // images/vfx/fx/<name>.png (tools/gen_fx_textures.py), else images/vfx/<name>.png (e.g. slash)
        foreach (var path in new[] { $"{MainFile.ResPath}/images/vfx/fx/{name}.png", $"{MainFile.ResPath}/images/vfx/{name}.png" })
        {
            if (!ResourceLoader.Exists(path)) continue;
            t = GD.Load<Texture2D>(path);
            break;
        }
        Tex[name] = t;
        return t;
    }

    /// <summary>An effect holder at <paramref name="pos"/> (parent space) that frees itself after <paramref name="life"/> s.</summary>
    private static Node2D Root(Node2D parent, Vector2 pos, double life, int z)
    {
        // the "back" layer: z 0 and first in the rig's draw order, i.e. behind the swords and (the rig being the creature's
        // first child) behind the body. Never a negative z: that drew it below the room background (SwordVisuals).
        var n = new Node2D { Name = "SwordFx", Position = pos, ZIndex = Math.Max(0, z) };
        parent.AddChild(n);
        if (z < 0) parent.MoveChild(n, 0);
        FreeAfter(n, life);
        return n;
    }

    private static void FreeAfter(Node node, double seconds)
    {
        var t = node.CreateTween();
        t.TweenInterval(seconds);
        t.TweenCallback(Callable.From(node.QueueFree));
    }

    private static void Pose(Node2D node, Vector2 pos, float scale, float rot, Color modulate) =>
        Pose(node, pos, new Vector2(scale, scale), rot, modulate);

    private static void Pose(Node2D node, Vector2 pos, Vector2 scale, float rot, Color modulate)
    {
        node.Position = pos;
        node.Scale = scale;
        node.Rotation = rot;
        node.Modulate = modulate;
    }

    private static Sprite2D Sprite(Node2D parent, string tex, Color col, bool additive, Vector2 pos)
    {
        var s = new Sprite2D { Texture = T(tex), Position = pos, Modulate = col };
        if (additive) s.Material = AddMat;
        parent.AddChild(s);
        return s;
    }

    private static Tween Delayed(Node node, double delay)
    {
        var t = node.CreateTween();
        if (delay > 0) t.TweenInterval(delay);
        return t;
    }

    /// <summary>Soft glow that blooms to <paramref name="size"/> px and fades.</summary>
    private static void Flare(Node2D p, Vector2 pos, Color col, float size, double dur, double delay = 0)
    {
        var s = Sprite(p, "fx_glow", new Color(col, 0f), true, pos);
        var k = size / 128f;
        s.Scale = Vector2.One * k * 0.3f;
        var t = Delayed(s, delay);
        t.TweenProperty(s, "modulate:a", 0.9f, dur * 0.2);
        t.Parallel().TweenProperty(s, "scale", Vector2.One * k, dur * 0.35).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenProperty(s, "modulate:a", 0f, dur * 0.65).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
    }

    /// <summary>A thin ring expanding from r0 to r1 px (squash &lt; 1 lays it flat on the ground).</summary>
    private static void Ring(Node2D p, Vector2 pos, Color col, float r0, float r1, double dur, double delay = 0, float squash = 1f)
    {
        var s = Sprite(p, "fx_ring", new Color(col, 0f), true, pos);
        float k0 = r0 / 110f, k1 = r1 / 110f;
        s.Scale = new Vector2(k0, k0 * squash);
        var t = Delayed(s, delay);
        t.TweenProperty(s, "modulate:a", 0.85f, 0.03);
        t.TweenProperty(s, "scale", new Vector2(k1, k1 * squash), dur).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.Parallel().TweenProperty(s, "modulate:a", 0f, dur).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
    }

    private static Gradient Ramp(Color a) => Ramp(a, a);

    /// <summary>Particle colour over life: a, then b, then transparent.</summary>
    private static Gradient Ramp(Color a, Color b)
    {
        var g = new Gradient();
        g.SetColor(0, a);
        g.SetColor(1, new Color(b, 0f));
        g.AddPoint(0.55f, b);
        return g;
    }

    /// <summary>
    /// One-shot particle burst. dir/spread in degrees; circle = emit on a ring of that radius, rect = emit in a box;
    /// radial/tangential = acceleration towards (negative) / around the centre; spin = degrees per second.
    /// </summary>
    private static void Burst(Node2D p, Vector2 pos, Gradient ramp, string tex, int amount, double life, float vMin, float vMax,
        Vector2 gravity, float spread, float sMin, float sMax, bool additive, float dir = -90, float radius = 0, float circle = 0,
        Vector2? rect = null, float radial = 0, float tangential = 0, float spin = 0, bool alignY = false, double delay = 0)
    {
        var c = new CpuParticles2D
        {
            Position = pos, Amount = amount, Lifetime = life, OneShot = true, Explosiveness = 0.85f, Randomness = 0.3f,
            Texture = T(tex), Direction = Vector2.Right.Rotated(Mathf.DegToRad(dir)), Spread = spread, Gravity = gravity,
            InitialVelocityMin = vMin, InitialVelocityMax = vMax, ScaleAmountMin = sMin, ScaleAmountMax = sMax,
            ColorRamp = ramp, RadialAccelMin = radial, RadialAccelMax = radial, TangentialAccelMin = tangential * 0.8f,
            TangentialAccelMax = tangential, DampingMin = 20, DampingMax = 60, AngleMin = -180, AngleMax = 180,
            AngularVelocityMin = -spin, AngularVelocityMax = spin, Emitting = false,
        };
        c.SetParticleFlag(CpuParticles2D.ParticleFlags.AlignYToVelocity, alignY);
        if (circle > 0) { c.EmissionShape = CpuParticles2D.EmissionShapeEnum.SphereSurface; c.EmissionSphereRadius = circle; }
        else if (rect is { } r) { c.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle; c.EmissionRectExtents = r; }
        else if (radius > 0) { c.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere; c.EmissionSphereRadius = radius; }
        if (additive) c.Material = AddMat;
        p.AddChild(c);
        if (delay <= 0) c.Emitting = true;
        else Delayed(c, delay).TweenCallback(Callable.From(() => c.Emitting = true));
    }

    /// <summary>A brush stroke growing from <paramref name="from"/> along <paramref name="rot"/>, then fading.</summary>
    private static void Stroke(Node2D p, Vector2 from, float rot, Color col, float length, float thickness, double dur, bool additive,
        double delay = 0)
    {
        var s = Sprite(p, "fx_brush", new Color(col, 0f), additive, from);
        s.Centered = false;
        s.Offset = new Vector2(0, -96); // texture is 1024 x 192: grow from its left end
        s.Rotation = rot;
        var target = new Vector2(length / 1024f, thickness / 192f * 1.6f);
        s.Scale = new Vector2(0.01f, target.Y);
        var t = Delayed(s, delay);
        t.TweenProperty(s, "modulate:a", col.A, 0.02);
        t.Parallel().TweenProperty(s, "scale:x", target.X, dur * 0.22).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        t.TweenInterval(dur * 0.25);
        t.TweenProperty(s, "modulate:a", 0f, dur * 0.53).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        t.Parallel().TweenProperty(s, "scale:y", target.Y * 0.5f, dur * 0.53);
    }

    /// <summary>A vertical shaft of light (Durandal).</summary>
    private static void Pillar(Node2D p, Vector2 pos, Color col, float height, float width, double dur, double delay = 0)
    {
        var s = Sprite(p, "fx_pillar", new Color(col, 0f), true, pos);
        var k = new Vector2(width / 128f, height / 512f);
        s.Scale = new Vector2(k.X * 0.2f, k.Y);
        var t = Delayed(s, Math.Max(0, delay));
        t.TweenProperty(s, "modulate:a", 0.9f, dur * 0.15);
        t.Parallel().TweenProperty(s, "scale:x", k.X, dur * 0.2).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenInterval(dur * 0.3);
        t.TweenProperty(s, "modulate:a", 0f, dur * 0.5);
        t.Parallel().TweenProperty(s, "scale:x", k.X * 0.15f, dur * 0.5).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
    }

    /// <summary>A long thin beam (a stretched spark) through <paramref name="pos"/> at angle <paramref name="rot"/>.</summary>
    private static void Beam(Node2D p, Vector2 pos, float rot, Color col, float length, float width, double dur, double delay = 0)
    {
        var s = Sprite(p, "fx_spark", new Color(col, 0f), true, pos);
        s.Rotation = rot;
        var k = new Vector2(width / 16f, length / 64f);
        s.Scale = new Vector2(k.X, k.Y * 0.2f);
        var t = Delayed(s, delay);
        t.TweenProperty(s, "modulate:a", 1f, 0.03);
        t.Parallel().TweenProperty(s, "scale:y", k.Y, dur * 0.3).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        t.TweenProperty(s, "modulate:a", 0f, dur * 0.7);
        t.Parallel().TweenProperty(s, "scale:x", k.X * 0.3f, dur * 0.7);
    }

    /// <summary>Rays of light radiating from <paramref name="pos"/>, turning slightly.</summary>
    private static void Rays(Node2D p, Vector2 pos, Color col, int n, float length, double dur, double delay = 0)
    {
        var holder = new Node2D { Position = pos, Rotation = (float)GD.RandRange(0, Mathf.Tau) };
        p.AddChild(holder);
        for (var i = 0; i < n; i++)
        {
            var a = Mathf.Tau * i / n;
            var len = length * (i % 2 == 0 ? 1f : 0.6f);
            var s = Sprite(holder, "fx_spark", new Color(col, 0f), true, Vector2.Up.Rotated(a) * len * 0.5f);
            s.Rotation = a;
            var k = new Vector2(0.9f, len / 64f);
            s.Scale = new Vector2(k.X, 0.1f);
            var t = Delayed(s, delay);
            t.TweenProperty(s, "modulate:a", 0.9f, 0.03);
            t.Parallel().TweenProperty(s, "scale:y", k.Y, dur * 0.35).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            t.TweenProperty(s, "modulate:a", 0f, dur * 0.65);
        }
        var spin = Delayed(holder, delay);
        spin.TweenProperty(holder, "rotation", holder.Rotation + 0.35f, dur + 0.05);
    }

    /// <summary>A curved cut (fx_crescent: the shape of images/vfx/slash.png in white), swept and faded.</summary>
    private static void Slash(Node2D p, Vector2 pos, float rot, Color col, float scale, double dur, double delay = 0)
    {
        var s = Sprite(p, "fx_crescent", new Color(col, 0f), true, pos);
        s.Rotation = rot - 0.4f;
        s.Scale = new Vector2(scale * 0.6f, scale * 0.6f);
        var t = Delayed(s, delay);
        t.TweenProperty(s, "modulate:a", col.A, 0.03);
        t.TweenProperty(s, "rotation", rot + 0.25f, dur).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.Parallel().TweenProperty(s, "scale", new Vector2(scale, scale), dur * 0.6);
        t.Parallel().TweenProperty(s, "modulate:a", 0f, dur).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
    }

    /// <summary>A splat spreading out (blood / ink) — normal blending, it is matter rather than light.</summary>
    private static void Blot(Node2D p, Vector2 pos, Color col, float r0, float r1, double dur, double delay = 0, float squash = 0.32f)
    {
        var holder = new Node2D { Position = pos, Scale = new Vector2(1, squash) }; // squash lays it on the ground
        p.AddChild(holder);
        var s = Sprite(holder, "fx_ink", new Color(col, 0f), false, Vector2.Zero);
        s.Rotation = (float)GD.RandRange(0, Mathf.Tau);
        s.Scale = Vector2.One * r0 / 48f;
        var t = Delayed(s, delay);
        t.TweenProperty(s, "modulate:a", col.A, 0.05);
        t.Parallel().TweenProperty(s, "scale", Vector2.One * r1 / 48f, dur * 0.4).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenProperty(s, "modulate:a", 0f, dur * 0.6);
    }

    /// <summary>An arc of a circle drawn progressively (degrees, 0 = right, 90 = down), then faded.</summary>
    private static void Arc(Node2D p, Vector2 center, float radius, float fromDeg, float toDeg, Color col, float width, double dur,
        bool rainbow = false, double delay = 0, float grow = 1f)
    {
        var line = new Line2D
        {
            Position = center, Width = width, DefaultColor = col, Material = AddMat, Modulate = new Color(1, 1, 1, 0),
            JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
            Antialiased = true,
        };
        var wc = new Curve();
        wc.AddPoint(new Vector2(0, 0.2f));
        wc.AddPoint(new Vector2(0.5f, 1f));
        wc.AddPoint(new Vector2(1, 0.4f));
        line.WidthCurve = wc;
        if (rainbow)
        {
            var g = new Gradient();
            Color[] cs =
            [
                new(1f, 0.35f, 0.35f), new(1f, 0.65f, 0.3f), new(1f, 0.95f, 0.4f), new(0.45f, 1f, 0.5f),
                new(0.4f, 0.85f, 1f), new(0.5f, 0.55f, 1f), new(0.8f, 0.5f, 1f),
            ];
            g.SetColor(0, cs[0]);
            g.SetColor(1, cs[^1]);
            for (var i = 1; i < cs.Length - 1; i++) g.AddPoint(i / (float)(cs.Length - 1), cs[i]);
            line.Gradient = g;
        }
        p.AddChild(line);
        const int seg = 28;
        line.Points = [Vector2.Zero, Vector2.Zero];
        void Draw(float k)
        {
            var n = Math.Max(2, (int)(seg * k) + 1);
            var pts = new Vector2[n];
            for (var i = 0; i < n; i++)
            {
                var a = Mathf.DegToRad(Mathf.Lerp(fromDeg, toDeg, k * i / (n - 1)));
                pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            line.Points = pts;
        }
        var t = Delayed(line, delay);
        t.TweenProperty(line, "modulate:a", 0.9f, 0.04);
        t.Parallel().TweenMethod(Callable.From<float>(Draw), 0.05f, 1f, dur * 0.5).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        if (grow != 1f) t.Parallel().TweenProperty(line, "scale", Vector2.One * grow, dur).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenProperty(line, "modulate:a", 0f, dur * 0.5);
    }

    /// <summary>Blade shards flying in from a ring to the centre (Gram reforged).</summary>
    private static void Converge(Node2D p, Vector2 pos, Color col, int n, float radius, double dur)
    {
        var a0 = (float)GD.RandRange(0, Mathf.Tau);
        for (var i = 0; i < n; i++)
        {
            var a = a0 + Mathf.Tau * i / n + (float)GD.RandRange(-0.25, 0.25);
            var start = pos + Vector2.Right.Rotated(a) * radius * (float)GD.RandRange(0.8, 1.15);
            var s = Sprite(p, "fx_shard", new Color(col, 0f), true, start);
            s.Rotation = a + Mathf.Pi; // tip points at the centre
            s.Scale = Vector2.One * (float)GD.RandRange(0.5, 0.8);
            var t = s.CreateTween();
            t.TweenProperty(s, "modulate:a", 1f, 0.05);
            t.Parallel().TweenProperty(s, "position", pos, dur).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            t.TweenProperty(s, "modulate:a", 0f, 0.06);
        }
    }

    /// <summary>A red and a blue flare spiralling in to the centre (Ganjiang · Moye).</summary>
    private static void Twin(Node2D p, Vector2 pos, float radius, double dur, bool moyeFirst)
    {
        for (var i = 0; i < 2; i++)
        {
            var sw = (i == 0) == moyeFirst ? SwordId.Moye : SwordId.Ganjiang;
            var col = SwordVisuals.ColorOf(sw);
            var phase = i * Mathf.Pi;
            var s = Sprite(p, "fx_glow", new Color(col, 0.95f), true, pos + Vector2.Right.Rotated(phase) * radius);
            s.Scale = Vector2.One * 0.7f;
            var trail = new CpuParticles2D
            {
                Amount = 24, Lifetime = 0.25, LocalCoords = false, Texture = T("fx_glow"), ScaleAmountMin = 0.25f,
                ScaleAmountMax = 0.4f, ColorRamp = Ramp(new Color(col, 0.7f)), Gravity = Vector2.Zero, InitialVelocityMax = 10,
                Material = AddMat,
            };
            s.AddChild(trail);
            var sp = s;
            var t = s.CreateTween();
            t.TweenMethod(Callable.From<float>(k =>
            {
                var a = phase + k * Mathf.Pi * 1.5f;
                sp.Position = pos + Vector2.Right.Rotated(a) * radius * (1 - k);
            }), 0f, 1f, dur).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            t.TweenCallback(Callable.From(() => trail.Emitting = false));
            t.TweenProperty(s, "modulate:a", 0f, 0.15);
        }
    }

    // ------------------------------------------------------------------ quiet summon pieces

    /// <summary>Particle colour over life that also fades IN (motes drift into sight instead of popping).</summary>
    private static Gradient FadeRamp(Color a, Color b)
    {
        var g = new Gradient();
        g.SetColor(0, new Color(a, 0f));
        g.SetColor(1, new Color(b, 0f));
        g.AddPoint(0.25f, a);
        g.AddPoint(0.65f, b);
        return g;
    }

    private static void Motes(Node2D p, Vector2 pos, Color col, string tex, int amount, double life, Vector2 gravity, float sMin,
        float sMax, bool additive, Vector2? rect = null, float circle = 0, float radial = 0, float spin = 0, double delay = 0) =>
        Motes(p, pos, FadeRamp(col, col), tex, amount, life, gravity, sMin, sMax, additive, rect, circle, radial, spin, delay);

    /// <summary>A few slow drifting particles, released gradually (low explosiveness).</summary>
    private static void Motes(Node2D p, Vector2 pos, Gradient ramp, string tex, int amount, double life, Vector2 gravity, float sMin,
        float sMax, bool additive, Vector2? rect = null, float circle = 0, float radial = 0, float spin = 0, double delay = 0)
    {
        var c = new CpuParticles2D
        {
            Position = pos, Amount = amount, Lifetime = life, OneShot = true, Explosiveness = 0.3f, Randomness = 0.4f,
            Texture = T(tex), Direction = Vector2.Up, Spread = 180, Gravity = gravity, InitialVelocityMin = 4,
            InitialVelocityMax = 16, ScaleAmountMin = sMin, ScaleAmountMax = sMax, ColorRamp = ramp, DampingMin = 4,
            DampingMax = 10, AngleMin = -180, AngleMax = 180, AngularVelocityMin = -spin, AngularVelocityMax = spin,
            RadialAccelMin = radial, RadialAccelMax = radial, Emitting = false,
        };
        if (circle > 0) { c.EmissionShape = CpuParticles2D.EmissionShapeEnum.SphereSurface; c.EmissionSphereRadius = circle; }
        else if (rect is { } r) { c.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle; c.EmissionRectExtents = r; }
        if (additive) c.Material = AddMat;
        p.AddChild(c);
        if (delay <= 0) c.Emitting = true;
        else Delayed(c, delay).TweenCallback(Callable.From(() => c.Emitting = true));
    }

    /// <summary>
    /// An ensō: one circle of the brush drawn in a single unhurried stroke (thick where the brush lands, thinning to a dry
    /// tail), held a moment, then faded. Normal blending reads as ink, additive as light.
    /// </summary>
    private static void Enso(Node2D p, Vector2 center, float radius, Color col, float width, double dur, bool additive = true,
        float fromDeg = 200, double delay = 0)
    {
        var line = new Line2D
        {
            Position = center, Width = width, DefaultColor = col, Modulate = new Color(1, 1, 1, 0),
            JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
            Antialiased = true,
        };
        if (additive) line.Material = AddMat;
        var wc = new Curve();
        wc.AddPoint(new Vector2(0, 0.7f));
        wc.AddPoint(new Vector2(0.12f, 1f));
        wc.AddPoint(new Vector2(0.75f, 0.7f));
        wc.AddPoint(new Vector2(1, 0.15f));
        line.WidthCurve = wc;
        p.AddChild(line);
        line.Points = [Vector2.Zero, Vector2.Zero];
        const float sweep = 325f;
        void Draw(float k)
        {
            var n = Math.Max(2, (int)(40 * k) + 1);
            var pts = new Vector2[n];
            for (var i = 0; i < n; i++)
            {
                var a = Mathf.DegToRad(fromDeg + sweep * k * i / (n - 1));
                // a hand-drawn circle is never quite round
                var r = radius * (1f + 0.035f * Mathf.Sin(a * 2f + 0.7f));
                pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            line.Points = pts;
        }
        var t = Delayed(line, delay);
        t.TweenProperty(line, "modulate:a", 1f, dur * 0.15);
        t.Parallel().TweenMethod(Callable.From<float>(Draw), 0.02f, 1f, dur * 0.55).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        t.TweenInterval(dur * 0.15);
        t.TweenProperty(line, "modulate:a", 0f, dur * 0.45).SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>A soft column of light standing under the sword: rises in slowly, never flashes.</summary>
    private static void Shaft(Node2D p, Vector2 pos, Color col, float height, float width, float alpha, double dur)
    {
        var s = Sprite(p, "fx_pillar", new Color(col, 0f), true, pos);
        var k = new Vector2(width / 128f, height / 512f);
        s.Scale = new Vector2(k.X * 0.6f, k.Y);
        var t = s.CreateTween();
        t.TweenProperty(s, "modulate:a", alpha, dur * 0.4).SetTrans(Tween.TransitionType.Sine);
        t.Parallel().TweenProperty(s, "scale:x", k.X, dur * 0.5).SetTrans(Tween.TransitionType.Sine);
        t.TweenProperty(s, "modulate:a", 0f, dur * 0.6).SetTrans(Tween.TransitionType.Sine);
        t.Parallel().TweenProperty(s, "scale:x", k.X * 0.5f, dur * 0.6);
    }

    /// <summary>The sword's own sigil (images/vfx/glyph_&lt;sword&gt;.png) surfacing faintly behind it, turning a little.</summary>
    private static void Sigil(Node2D p, Vector2 pos, SwordId sword, Color col, float alpha, double dur)
    {
        var path = $"{MainFile.ResPath}/images/vfx/glyph_{sword.ToString().ToLowerInvariant()}.png";
        if (!ResourceLoader.Exists(path)) return;
        var tex = GD.Load<Texture2D>(path);
        var s = new Sprite2D { Texture = tex, Position = pos, Modulate = new Color(col, 0f), Material = AddMat };
        var k = 150f / Math.Max(1, tex.GetWidth());
        s.Scale = Vector2.One * k * 0.96f;
        p.AddChild(s);
        var t = s.CreateTween();
        t.TweenProperty(s, "modulate:a", alpha, dur * 0.4).SetTrans(Tween.TransitionType.Sine);
        t.Parallel().TweenProperty(s, "scale", Vector2.One * k, dur).SetTrans(Tween.TransitionType.Sine);
        t.Parallel().TweenProperty(s, "rotation", 0.14f, dur).SetTrans(Tween.TransitionType.Sine);
        t.TweenProperty(s, "modulate:a", 0f, dur * 0.5).SetTrans(Tween.TransitionType.Sine);
    }
}
