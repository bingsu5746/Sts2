using Godot;
using MagicSwordsman.MagicSwordsmanCode.Swords;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// The guard's impact frame, by the lead sword's personality (user request 2026-10-10 "방어 모션도 검마다 다르게 ...
/// 좀 더 멋지게"). Called by SwordVisuals.Guard at the moment the blades meet the blow; <c>pos</c> is the contact point
/// in rig space, <c>side</c> +1 when the enemy stands to the right. Restrained, classical, ≤ 0.5 s, local.
/// Rendered for review through a headless GDScript mirror of the choreography (not in the repo).
/// </summary>
public static partial class SwordFx
{
    internal static void GuardFx(Node2D rigRoot, Vector2 pos, SwordId lead, bool hit, float side)
    {
        try
        {
            var col = SwordVisuals.ColorOf(lead);
            // z 1: in front of the body, behind the blades in flight (z 2); "back" = behind the body
            var fx = Root(rigRoot, pos, 0.9, 3);
            var mid = Root(rigRoot, pos, 0.9, 1);
            Clash(rigRoot, pos, col, hit ? 1.45f : 1.2f, side * -0.55f);
            switch (lead)
            {
                case SwordId.Durandal: // a shield of holy light opens behind the blades
                    Flare(mid, new Vector2(-side * 20, 0), new Color(1f, 0.93f, 0.7f), 230, 0.4);
                    Ring(mid, new Vector2(-side * 20, 0), new Color(1f, 0.9f, 0.6f), 40, 150, 0.4, squash: 1.35f);
                    Pillar(mid, new Vector2(-side * 20, -40), new Color(1f, 0.95f, 0.75f), 360, 60, 0.45);
                    break;
                case SwordId.Gram: // a heavy block: a shower of sparks and a low golden shock
                    Burst(fx, Vector2.Zero, Ramp(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.45f, 0.12f)), "fx_spark", 26, 0.5, 220, 520,
                        new Vector2(0, 900), 70, 0.18f, 0.32f, true, dir: side > 0 ? -150 : -30, alignY: true);
                    Ring(mid, Vector2.Zero, col, 30, 140, 0.35);
                    break;
                case SwordId.Kusanagi: // the wind turns the blow aside: two swirling arcs and a few leaves
                    Arc(mid, Vector2.Zero, 95, side > 0 ? 200 : -20, side > 0 ? 470 : -290, new Color(0.65f, 0.95f, 0.7f, 0.75f), 6, 0.42);
                    Arc(mid, Vector2.Zero, 60, side > 0 ? 20 : 160, side > 0 ? 300 : -120, new Color(0.8f, 1f, 0.85f, 0.6f), 4, 0.38, delay: 0.04);
                    Burst(fx, Vector2.Zero, Ramp(new Color(0.5f, 0.85f, 0.45f)), "fx_leaf", 8, 0.5, 60, 110, Vector2.Zero, 180,
                        0.35f, 0.6f, false, circle: 60, tangential: side * 700, spin: 360);
                    break;
                case SwordId.Onimaru: // one exact parry: a single hairline of light and a touch of ink, nothing more
                    Beam(fx, Vector2.Zero, side * -0.9f, new Color(1f, 1f, 1f), 260, 6, 0.22);
                    Stroke(mid, new Vector2(-side * 70, 30), side > 0 ? -0.42f : Mathf.Pi + 0.42f, new Color(0.1f, 0.05f, 0.14f, 0.7f), 150, 18, 0.35, false);
                    break;
                case SwordId.Skofnung: // pale mist; the blades leave ghosts of themselves (SwordVisuals.GuardGhosts)
                    Burst(mid, Vector2.Zero, Ramp(new Color(0.75f, 0.9f, 1f, 0.3f)), "fx_smoke", 7, 0.55, 40, 110, Vector2.Zero, 180,
                        0.45f, 0.8f, true);
                    Ring(mid, Vector2.Zero, new Color(0.75f, 0.9f, 1f), 20, 110, 0.4);
                    break;
                case SwordId.Tyrfing: // embers knocked off the cursed blade drift up
                    Burst(fx, Vector2.Zero, Ramp(new Color(1f, 0.8f, 0.35f), new Color(1f, 0.3f, 0.06f)), "fx_spark", 16, 0.45, 120, 300,
                        new Vector2(0, 400), 120, 0.16f, 0.28f, true, alignY: true);
                    Motes(fx, Vector2.Zero, new Color(1f, 0.55f, 0.2f, 0.85f), "fx_glow", 10, 0.9, new Vector2(0, -90), 0.04f, 0.08f, true,
                        rect: new Vector2(50, 60));
                    break;
                case SwordId.Dainsleif: // a breath of blood mist
                    Burst(mid, Vector2.Zero, Ramp(new Color(0.5f, 0.04f, 0.09f, 0.55f), new Color(0.3f, 0.02f, 0.05f, 0.3f)), "fx_smoke", 8, 0.6,
                        50, 130, new Vector2(0, -20), 180, 0.45f, 0.85f, false);
                    Burst(fx, Vector2.Zero, Ramp(new Color(0.55f, 0.03f, 0.08f, 0.9f)), "fx_drop", 6, 0.45, 100, 220, new Vector2(0, 800), 90,
                        0.16f, 0.26f, false, dir: side > 0 ? -140 : -40, alignY: true);
                    break;
                case SwordId.ClaiomhSolais: // a flare of white light
                    Rays(fx, Vector2.Zero, new Color(1f, 0.97f, 0.78f), 8, 200, 0.35);
                    Flare(mid, Vector2.Zero, new Color(1f, 1f, 0.85f), 260, 0.35);
                    break;
                case SwordId.Caladbolg: // a rainbow arc sweeps across in front of him
                    Arc(mid, new Vector2(-side * 40, 10), 150, side > 0 ? -125 : -55, side > 0 ? 95 : -275, Colors.White, 9, 0.45, rainbow: true);
                    break;
                case SwordId.Ganjiang or SwordId.Moye: // the twin blades: a red and a blue cut cross over the block
                    Stroke(fx, new Vector2(-120, 0).Rotated(0.6f), 0.6f, SwordVisuals.ColorOf(SwordId.Ganjiang), 240, 26, 0.32, true);
                    Stroke(fx, new Vector2(-120, 0).Rotated(-0.6f), -0.6f, SwordVisuals.ColorOf(SwordId.Moye), 240, 26, 0.32, true, delay: 0.04);
                    break;
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] guard {lead}: {e.Message}");
        }
    }

    /// <summary>
    /// The guard breaks (SwordVisuals.GuardBreak, user request 2026-10-15): a hard white flash, jagged cracks of light
    /// splitting outward from the contact as if the formation were glass, shards of light thrown away from the blow
    /// (toward Ensifer: the blow carries through), a broken ring, and a breath of the lead sword's colour going out.
    /// <paramref name="fail"/>: no Block at all — the swords only got in the way: a smaller crack, fewer shards.
    /// </summary>
    internal static void GuardBreakFx(Node2D rigRoot, Vector2 pos, SwordId lead, float side, bool fail)
    {
        try
        {
            Sfx.Guard();
            var col = SwordVisuals.ColorOf(lead);
            var k = fail ? 0.7f : 1f;
            var fx = Root(rigRoot, pos, 1.0, 3);
            var mid = Root(rigRoot, pos, 1.0, 1);
            Flare(fx, Vector2.Zero, new Color(1f, 0.98f, 0.95f), 210 * k, 0.2);
            Flare(mid, Vector2.Zero, col, 170 * k, 0.4);
            // the cracks: a fan of jagged streaks, mostly back toward him and up/down, a short kinked tail on each
            var rng = new Random();
            var cracks = fail ? 4 : 7;
            for (var i = 0; i < cracks; i++)
            {
                var a = (side > 0 ? Mathf.Pi : 0f) + (i - (cracks - 1) / 2f) * (fail ? 0.55f : 0.42f) + (float)(rng.NextDouble() - 0.5) * 0.25f;
                var len = (110f + (float)rng.NextDouble() * 110f) * k;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var crackCol = new Color(1f, 0.97f, 1f).Lerp(col.Lightened(0.4f), 0.35f);
                Beam(fx, dir * len * 0.5f, a + Mathf.Pi / 2, crackCol, len, 5.5f, 0.3, delay: i * 0.008);
                var kink = a + ((i % 2 == 0) ? 0.6f : -0.6f);
                var kdir = new Vector2(Mathf.Cos(kink), Mathf.Sin(kink));
                Beam(fx, dir * len + kdir * len * 0.2f, kink + Mathf.Pi / 2, crackCol, len * 0.4f, 3.5f, 0.26, delay: 0.03 + i * 0.008);
            }
            Ring(mid, Vector2.Zero, col.Lightened(0.3f), 20, 170 * k, 0.32, squash: 1.15f);
            // shards of the broken guard, carried on by the blow
            Burst(fx, Vector2.Zero, Ramp(new Color(0.95f, 0.95f, 1f), col.Lightened(0.3f)), "fx_shard", fail ? 8 : 18, 0.6,
                220, 560 * k, new Vector2(0, 900), 70, 0.25f, 0.5f, true, dir: side > 0 ? 200 : -20, spin: 540, alignY: false);
            Burst(fx, Vector2.Zero, Ramp(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.7f, 0.4f)), "fx_spark", fail ? 10 : 22, 0.4,
                200, 480 * k, new Vector2(0, 700), 120, 0.12f, 0.24f, true, dir: side > 0 ? 190 : -10, alignY: true);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] guard break {lead}: {e.Message}");
        }
    }

    /// <summary>A planted blade: a flat ring of dust where its point bites into the ground.</summary>
    internal static void GroundBite(Node2D rigRoot, Vector2 pos, Color col)
    {
        try
        {
            var fx = Root(rigRoot, pos, 0.7, 1);
            Ring(fx, Vector2.Zero, col.Lerp(new Color(0.8f, 0.78f, 0.75f), 0.5f), 10, 90, 0.35, squash: 0.25f);
            Burst(fx, Vector2.Zero, Ramp(new Color(0.6f, 0.58f, 0.55f, 0.5f)), "fx_smoke", 5, 0.5, 40, 110, new Vector2(0, 60), 60,
                0.2f, 0.4f, false, dir: -90);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] ground bite: {e.Message}");
        }
    }
}
