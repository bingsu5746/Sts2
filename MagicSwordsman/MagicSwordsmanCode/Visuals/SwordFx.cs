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
/// (≤ 0.6 s), local (no full-screen flashes) and frees itself. Every entry point swallows its errors.
///
///  - Summon (<see cref="Summon"/>): also sets the sword's starting pose (the layout tween in SwordVisuals then carries it
///    to its slot), so e.g. Durandal really descends and Skofnung really condenses.
///  - Switch (<see cref="Switch"/>): at the current-sword slot, a ring + a smaller accent of the sword's summon.
///  - Impact (<see cref="Impact"/>): on the enemy, when our hit lands (Mangeomchong.AfterDamageReceived).
/// tools/fx_preview.gd mirrors these effects in GDScript for headless renders — keep the two in step.
/// UNVERIFIED in game: sizes against the combat camera scaling, z-order against enemy sprites.
/// </summary>
public static class SwordFx
{
    // ------------------------------------------------------------------ entry points

    /// <summary>
    /// A sword appears for the first time this combat. <paramref name="sword"/> node was just created; its layout tween
    /// (to <paramref name="slot"/>) starts on the next frame and reads the pose set here as its start values.
    /// </summary>
    internal static void Summon(Node2D rigRoot, Node2D swordNode, SwordId sword, Vector2 slot)
    {
        try
        {
            Sfx.Summon(sword);
            var col = SwordVisuals.ColorOf(sword);
            var fx = Root(rigRoot, slot, 0.9, 2);
            var back = Root(rigRoot, slot, 0.9, -1);
            switch (sword)
            {
                case SwordId.Tyrfing: // bursts out of embers, cooling from red-hot
                    Pose(swordNode, slot + new Vector2(0, 30), 0.2f, 0f, new Color(1.8f, 0.9f, 0.5f, 0.4f));
                    Flare(back, Vector2.Zero, col, 260, 0.45);
                    Burst(fx, Vector2.Zero, Ramp(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.3f, 0.08f)), "fx_spark", 40, 0.55,
                        150, 420, new Vector2(0, -320), 180, 0.25f, 0.55f, true, radius: 20, alignY: true);
                    Burst(back, new Vector2(0, -10), Ramp(new Color(0.22f, 0.12f, 0.1f, 0.5f)), "fx_smoke", 8, 0.6, 30, 90,
                        new Vector2(0, -120), 70, 0.5f, 0.9f, false, dir: -90, radius: 30);
                    Ring(back, new Vector2(0, 95), new Color(1f, 0.45f, 0.15f), 30, 170, 0.4, squash: 0.35f);
                    break;

                case SwordId.Skofnung: // condenses out of ghost mist
                    Pose(swordNode, slot, 1.6f, 0f, new Color(0.7f, 0.9f, 1f, 0f));
                    Burst(back, Vector2.Zero, Ramp(new Color(0.75f, 0.9f, 1f, 0.55f)), "fx_smoke", 18, 0.45, 0, 10, Vector2.Zero,
                        180, 0.5f, 0.9f, true, circle: 170, radial: -1400);
                    Burst(fx, Vector2.Zero, Ramp(new Color(0.85f, 0.95f, 1f, 0.8f)), "fx_glow", 14, 0.4, 0, 0, Vector2.Zero,
                        180, 0.08f, 0.16f, true, circle: 130, radial: -1100);
                    Flare(back, Vector2.Zero, col, 190, 0.35, delay: 0.25);
                    break;

                case SwordId.Durandal: // descends inside a pillar of light
                    Pose(swordNode, slot + new Vector2(0, -340), 1.15f, 0f, new Color(1.3f, 1.25f, 1.1f, 0.3f));
                    Pillar(back, new Vector2(0, -120), new Color(1f, 0.92f, 0.65f), 560, 110, 0.55);
                    Burst(fx, new Vector2(0, -300), Ramp(new Color(1f, 0.95f, 0.75f)), "fx_glow", 16, 0.5, 20, 60,
                        new Vector2(0, 420), 25, 0.06f, 0.12f, true, dir: 90, rect: new Vector2(45, 30));
                    Ring(back, new Vector2(0, 100), new Color(1f, 0.9f, 0.6f), 40, 160, 0.4, delay: 0.25, squash: 0.3f);
                    Flare(fx, Vector2.Zero, col, 200, 0.3, delay: 0.27);
                    break;

                case SwordId.Kusanagi: // spins in on a swirl of wind and grass
                    Pose(swordNode, slot + new Vector2(-60, 40), 0.5f, -Mathf.Tau, Colors.White);
                    Burst(fx, Vector2.Zero, Ramp(new Color(0.5f, 0.85f, 0.45f), new Color(0.75f, 0.95f, 0.5f)), "fx_leaf", 22, 0.55,
                        60, 90, Vector2.Zero, 180, 0.5f, 0.9f, false, circle: 95, radial: -60, tangential: 700, spin: 360);
                    Arc(back, Vector2.Zero, 115, 200, 520, new Color(0.6f, 0.95f, 0.65f, 0.75f), 9, 0.45);
                    Arc(back, new Vector2(10, -20), 80, 30, 330, new Color(0.75f, 1f, 0.75f, 0.6f), 6, 0.4, delay: 0.06);
                    break;

                case SwordId.Onimaru: // cut out of the air by a single ink slash
                    Pose(swordNode, slot + new Vector2(-25, 25), 1.15f, 0f, new Color(0.45f, 0.35f, 0.55f, 0f));
                    Stroke(back, new Vector2(-170, 110), -0.62f, new Color(0.1f, 0.05f, 0.14f, 0.95f), 400, 90, 0.55, false);
                    Stroke(fx, new Vector2(-160, 100), -0.62f, new Color(0.65f, 0.45f, 0.95f, 0.55f), 380, 26, 0.4, true, delay: 0.03);
                    Burst(back, new Vector2(60, -40), Ramp(new Color(0.12f, 0.06f, 0.16f, 0.9f)), "fx_ink", 10, 0.5, 120, 300,
                        new Vector2(0, 500), 50, 0.1f, 0.25f, false, dir: -40);
                    break;

                case SwordId.Dainsleif: // drips down, blood running off the blade
                    Pose(swordNode, slot + new Vector2(0, -140), new Vector2(0.5f, 1.4f), 0f, new Color(0.9f, 0.3f, 0.35f, 0.3f));
                    Flare(back, Vector2.Zero, new Color(0.6f, 0.08f, 0.14f), 170, 0.5);
                    Burst(fx, new Vector2(0, 10), Ramp(new Color(0.55f, 0.03f, 0.08f, 0.95f)), "fx_drop", 12, 0.5, 20, 80,
                        new Vector2(0, 900), 10, 0.3f, 0.5f, false, dir: 90, rect: new Vector2(8, 70), alignY: true, delay: 0.12);
                    Blot(back, new Vector2(0, 105), new Color(0.4f, 0.02f, 0.06f, 0.85f), 30, 120, 0.55, delay: 0.2);
                    break;

                case SwordId.ClaiomhSolais: // a flash of light (local; never covers the screen)
                    Pose(swordNode, slot, 1.4f, 0f, new Color(2f, 2f, 1.6f, 0f));
                    Flare(back, Vector2.Zero, new Color(1f, 0.97f, 0.8f), 300, 0.35);
                    Rays(fx, Vector2.Zero, new Color(1f, 0.95f, 0.7f), 10, 230, 0.45);
                    Burst(fx, Vector2.Zero, Ramp(new Color(1f, 1f, 0.85f)), "fx_glow", 16, 0.5, 80, 220, Vector2.Zero, 180, 0.05f, 0.1f, true);
                    Ring(back, Vector2.Zero, new Color(1f, 0.95f, 0.7f), 30, 190, 0.4);
                    break;

                case SwordId.Caladbolg: // arrives along a rainbow arc
                    Pose(swordNode, slot + new Vector2(-270, 60), 0.6f, -1.2f, Colors.White);
                    Arc(back, new Vector2(-135, 40), 135, 180, 360, Colors.White, 16, 0.6, rainbow: true);
                    Burst(fx, Vector2.Zero, Ramp(new Color(0.6f, 1f, 1f), new Color(0.9f, 0.7f, 1f)), "fx_glow", 18, 0.5, 60, 200,
                        Vector2.Zero, 180, 0.05f, 0.1f, true, delay: 0.25);
                    Ring(back, Vector2.Zero, col, 30, 160, 0.35, delay: 0.25);
                    break;

                case SwordId.Gram: // shards fly together and the blade is forged whole
                    Pose(swordNode, slot, 1f, 0f, new Color(1.5f, 1.3f, 0.8f, 0f));
                    Converge(fx, Vector2.Zero, new Color(1f, 0.85f, 0.5f), 7, 160, 0.22);
                    Flare(back, Vector2.Zero, col, 230, 0.35, delay: 0.22);
                    Ring(back, Vector2.Zero, col, 30, 170, 0.35, delay: 0.22);
                    Burst(fx, Vector2.Zero, Ramp(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.5f, 0.15f)), "fx_spark", 30, 0.45,
                        200, 450, new Vector2(0, 600), 180, 0.2f, 0.45f, true, alignY: true, delay: 0.22);
                    break;

                case SwordId.Ganjiang or SwordId.Moye: // a red and a blue flare circle in and fuse
                    Pose(swordNode, slot, 0.5f, 0f, new Color(1, 1, 1, 0));
                    Twin(fx, Vector2.Zero, 130, 0.3, sword == SwordId.Moye);
                    Flare(back, Vector2.Zero, new Color(0.85f, 0.6f, 0.95f), 220, 0.3, delay: 0.26);
                    Ring(back, Vector2.Zero, SwordVisuals.ColorOf(SwordId.Ganjiang), 30, 150, 0.3, delay: 0.26);
                    Ring(back, Vector2.Zero, SwordVisuals.ColorOf(SwordId.Moye), 20, 120, 0.3, delay: 0.29);
                    break;

                default:
                    Flare(back, Vector2.Zero, col, 220, 0.4);
                    Ring(back, Vector2.Zero, col, 30, 160, 0.4);
                    break;
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] summon {sword}: {e.Message}");
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
            PlayImpact(fx, sword);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordFx] impact: {e.Message}");
        }
    }

    /// <summary>The impact itself, centred on <paramref name="fx"/> (screen pixels).</summary>
    internal static void PlayImpact(Node2D fx, SwordId? sword)
    {
        var rot = (float)GD.RandRange(-0.5, 0.5);
        var col = sword is { } s ? SwordVisuals.ColorOf(s) : new Color(0.85f, 0.65f, 1f);
        switch (sword)
        {
            case SwordId.Tyrfing: // a burst of fire
                Flare(fx, Vector2.Zero, col, 230, 0.4);
                Burst(fx, Vector2.Zero, Ramp(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.3f, 0.08f)), "fx_spark", 30, 0.5,
                    200, 480, new Vector2(0, -250), 180, 0.25f, 0.5f, true, alignY: true);
                Ring(fx, Vector2.Zero, new Color(1f, 0.45f, 0.15f), 30, 170, 0.35);
                break;
            case SwordId.Skofnung: // a pale ghost cut that leaves frost-mist
                Stroke(fx, new Vector2(-190, 0).Rotated(rot - 0.5f), rot - 0.5f, new Color(0.7f, 0.9f, 1f, 0.8f), 380, 50, 0.35, true);
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
                    Slash(fx, Vector2.Zero, rot + k * Mathf.Tau / 3, new Color(0.55f, 0.95f, 0.6f, 0.6f), 0.75f, 0.32, delay: k * 0.04);
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
                Stroke(fx, new Vector2(-180, 0).Rotated(rot + 0.4f), rot + 0.4f, new Color(0.75f, 0.12f, 0.18f, 0.85f), 360, 40, 0.35, true);
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
                Stroke(fx, new Vector2(-170, 0).Rotated(0.55f), 0.55f, SwordVisuals.ColorOf(SwordId.Ganjiang), 340, 36, 0.35, true);
                Stroke(fx, new Vector2(-170, 0).Rotated(-0.55f), -0.55f, SwordVisuals.ColorOf(SwordId.Moye), 340, 36, 0.35, true, delay: 0.05);
                Flare(fx, Vector2.Zero, new Color(0.85f, 0.6f, 0.95f), 170, 0.3, delay: 0.05);
                break;
            default: // no current sword: a plain violet cut
                Slash(fx, Vector2.Zero, rot, new Color(col, 0.85f), 1f, 0.3);
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
        var n = new Node2D { Name = "SwordFx", Position = pos, ZIndex = z };
        parent.AddChild(n);
        // the "back" layer: same z as the idle swords (-1, known to render in front of the room), first in draw order
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
}
