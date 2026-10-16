using Godot;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// The death to the Architect (user request 2026-10-16 "3막까지 다 깨고 아키텍트한테 죽을 때도 마검들로 아키텍트한테
/// 저항하다가 아키텍트가 마검의 조종을 뺏고 죽이는 모션으로 해줘").
///
/// How the game ends a won run (decompiled sts2.dll): after the act 3 boss, RunManager.EnterNextAct enters an EventRoom
/// with the event TheArchitect (combat layout, encounter TheArchitectEventEncounter = one Architect monster, 9999 HP,
/// does nothing). The event plays the dialogue, then on PROCEED (TheArchitect.WinRun) the player's attack animation
/// and/or the Architect's ("Attack" + vfx_attack_lightning on the player) per the dialogue's EndAttackers, then signals
/// the act change; EnterNextAct sees the victory room (AbstractRoom.IsVictoryRoom) and calls RunManager.WinRun:
/// OnEnded(isVictory: true), then GuaranteeKillAllPlayers -> CreatureCmd.Kill(player, force: true) — the ordinary death
/// path, so NCreature.StartDeathAnim fires "Dead" and MotionDirector redirects it here. Every won run ends this way;
/// there is no fight with the Architect.
///
/// Choreography (body clip "Dead_Architect", tools/gen_combat_scene.py; keep the times in step), 4.3 s:
///  0-0.2 the six blades (his swords on screen, then copies of the other owned ones) gather in front of him, points on
///  the Architect; three waves (0.36, 0.56, 0.76 s), two blades each, draw back, strike and are thrown back off an
///  unseen wall in front of it (SwordFx.Repel); 1.02 they are caught where they tumble, 1.05 the Architect takes them
///  (SwordFx.Seize: a thread from it to each blade), they shudder and lose their colour (1.05-1.3 s,
///  <see cref="SeizeMaterial"/>) and slowly turn their points on him (1.3-1.5 s). From 1.5 s the betrayal of the plain
///  death (<see cref="DeathBetrayal"/>) runs unchanged, with colourless blades.
/// The colour they take: the game gives the Architect's speech bubble VfxColor.DarkGray (TheArchitect.ShowSpeechBubble);
/// its sprite art is not in the decompiled code, so a cold dark grey stands for "the Architect's colour".
/// UNVERIFIED in game: the Architect's size / where its body is (the aim is NCreature.VfxSpawnPosition, the wall the
/// near edge of its hitbox).
/// </summary>
public static partial class SwordVisuals
{
    /// <summary>Seconds of fighting the Architect before the betrayal starts (Dead_Architect: ARCH_O).</summary>
    private const double ArchitectPrelude = 1.5;

    /// <summary>The blades' tint once taken (see the class summary) and the colour of their effects from then on.</summary>
    private static readonly Color SeizedTint = new(0.6f, 0.62f, 0.68f);
    private static readonly Color SeizedColor = new(0.72f, 0.74f, 0.8f);

    /// <summary>Where the Architect is, in rig space: what the blades aim at, and the wall that throws them back.</summary>
    private sealed record ArchitectTarget(Vector2 Aim, float WallX);

    /// <summary>When each wave strikes (s); blade i belongs to wave i / 2.</summary>
    private static readonly float[] ArchitectWaves = { 0.36f, 0.56f, 0.76f };

    /// <summary>Where the six blades gather in front of him (rig space, he faces right).</summary>
    private static readonly Vector2[] ResistSlots =
    {
        new(200, -330), new(175, -190), new(270, -440), new(255, -255), new(330, -350), new(150, -470),
    };

    /// <summary>
    /// Ensifer dies in the Architect's room (MotionDirector: <c>"Dead"</c> while the run's current room is the victory
    /// room, or with an Architect among the enemies). <paramref name="architect"/> may be null (then any enemy node, or a
    /// point to his right).
    /// </summary>
    public static void DeathArchitect(Player player, Creature? architect)
    {
        try
        {
            var rig = GetRig(player, create: true);
            if (rig == null || rig.Dead) return;
            DeathInner(player, FindArchitect(rig, player, architect));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordVisuals] Architect death failed: {e.Message}");
        }
    }

    /// <summary>The Architect's creature in <paramref name="player"/>'s combat state, if there is one.</summary>
    internal static Creature? ArchitectOf(Player player) =>
        player.Creature.CombatState?.Enemies.FirstOrDefault(e => e.Monster is Architect);

    private static ArchitectTarget FindArchitect(Rig rig, Player player, Creature? architect)
    {
        var room = NCombatRoom.Instance;
        NCreature? node = architect != null ? room?.GetCreatureNode(architect) : null;
        node ??= room?.CreatureNodes.FirstOrDefault(n => n.Entity.Side == CombatSide.Enemy);
        if (node == null) return new ArchitectTarget(new Vector2(760, -300), 600);
        var aim = rig.Root.ToLocal(node.VfxSpawnPosition);
        var wall = aim.X - 150f;
        try
        {
            // the near edge of its hitbox, if that lies between him and its centre
            var edge = rig.Root.ToLocal(node.Hitbox.GetGlobalRect().Position).X;
            if (edge > 260f && edge < aim.X) wall = edge;
        }
        catch (Exception) { /* keep the default */ }
        return new ArchitectTarget(aim, Math.Max(wall, 380f));
    }

    /// <summary>
    /// The colourless look of the taken blades: a canvas_item shader that turns the sprite grey (lifted a little: the
    /// dark blades must still read against a dark room) and tints it
    /// <see cref="SeizedTint"/> by <c>amount</c> (0 = untouched). One per death; its amount rises 1.05-1.3 s.
    /// </summary>
    private static ShaderMaterial SeizeMaterial(Rig rig)
    {
        var mat = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = """
                    shader_type canvas_item;
                    uniform float amount = 0.0;
                    uniform vec4 tint : source_color = vec4(1.0);
                    void fragment() {
                        vec4 c = texture(TEXTURE, UV) * COLOR;
                        float l = dot(c.rgb, vec3(0.299, 0.587, 0.114));
                        COLOR = vec4(mix(c.rgb, vec3(0.22 + l) * tint.rgb * 1.25, amount), c.a);
                    }
                    """,
            },
        };
        mat.SetShaderParameter("amount", 0f);
        mat.SetShaderParameter("tint", SeizedTint);
        var tw = rig.Root.CreateTween();
        tw.TweenInterval(1.05);
        tw.TweenMethod(Callable.From<float>(a => mat.SetShaderParameter("amount", a)), 0f, 1f, 0.25)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        return mat;
    }

    /// <summary>
    /// The blade's own art (sprites without a material) takes the seize material; everything with one — the additive
    /// glows, aura, level light and sparks (SwordVisuals.Level.cs) — fades out.
    /// </summary>
    private static void Seize(Node2D node, Material seized)
    {
        foreach (var item in node.FindChildren("*", "CanvasItem", true, false).OfType<CanvasItem>())
        {
            if (item.Material == null) { if (item is Sprite2D or Polygon2D) item.Material = seized; }
            else item.CreateTween().TweenProperty(item, "modulate:a", 0f, 0.25);
        }
    }

    /// <summary>One blade's fight with the Architect, appended to <paramref name="tw"/>: 0 to <see cref="ArchitectPrelude"/> s.</summary>
    private static void ArchitectBlade(Tween tw, Rig rig, SwordId sword, Node2D node, int index, int count, ArchitectTarget arch,
        Material seized)
    {
        var col = ColorOf(sword);
        var slot = ResistSlots[index % ResistSlots.Length];
        var dir = (arch.Aim - slot).Normalized();
        if (dir == Vector2.Zero) dir = Vector2.Right;
        // the wall: where its line to the Architect meets the near edge (or 150 px short of the aim)
        var t = dir.X > 0.05f ? Math.Clamp((arch.WallX - slot.X) / dir.X, 60f, (arch.Aim - slot).Length()) : (arch.Aim - slot).Length() - 150f;
        var wallHit = slot + dir * t;
        var hitAt = (double)ArchitectWaves[Math.Min(index / 2, ArchitectWaves.Length - 1)];
        var down = index % 2 == 0 ? -1f : 1f;
        var thrown = wallHit - dir * (230f + 25f * (index % 3)) + new Vector2(0, 70f * down);
        var clock = 0.0;
        void Until(double at)
        {
            if (at > clock + 1e-4) tw.TweenInterval(at - clock);
            clock = Math.Max(clock, at);
        }

        // called up in front of him, points on the Architect
        tw.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(node)) node.ZIndex = ZFlying; }));
        tw.TweenProperty(node, "position", slot, 0.2).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", PointAt(slot, arch.Aim), 0.2);
        tw.Parallel().TweenProperty(node, "modulate", Colors.White, 0.2);
        tw.Parallel().TweenProperty(node, "scale", Vector2.One * 1.15f, 0.2);
        clock = 0.2;

        // the strike: draw back (0.07 s), drive at it (0.09 s) — and the wall throws it back, tumbling (0.26 s)
        Until(hitAt - 0.16);
        tw.TweenProperty(node, "position", slot - dir * 34f, 0.07).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tw.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            Trail(node, 0.14, col);
            Sfx.Whoosh(sword);
        }));
        tw.TweenProperty(node, "position", wallHit, 0.09).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        var lastWave = index / 2 >= ArchitectWaves.Length - 1 && index % 2 == 0;
        tw.TweenCallback(Callable.From(() =>
        {
            SwordFx.Repel(rig.Root, wallHit, col, dir);
            if (lastWave) Shake(MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.VeryWeak);
        }));
        tw.TweenProperty(node, "position", thrown, 0.26).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(node, "rotation", PointAt(slot, arch.Aim) - Mathf.Tau * down * 0.85f, 0.26)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        clock = hitAt + 0.26;

        // 1.05 taken: a thread from the Architect, a shudder, the colour drains (SeizeMaterial)
        Until(1.05);
        tw.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            SwordFx.Seize(rig.Root, arch.Aim, node.Position, SeizedColor, index * 0.025);
            Seize(node, seized);
        }));
        var held = thrown;
        var phase = index * 1.7f;
        tw.TweenMethod(Callable.From<float>(u =>
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            var k = (1f - u) * 5f;
            node.Position = held + new Vector2(Mathf.Sin(u * 46f + phase) * k, Mathf.Cos(u * 37f + phase) * k * 0.7f);
        }), 0f, 1f, 0.25);
        clock = 1.3;

        // 1.3-1.5 the points come round onto him, slowly
        var startRot = 0f;
        tw.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(node)) startRot = node.Rotation; }));
        tw.TweenMethod(Callable.From<float>(u =>
        {
            if (GodotObject.IsInstanceValid(node)) node.Rotation = Mathf.LerpAngle(startRot, PointAt(held, DeathCenter), u);
        }), 0f, 1f, ArchitectPrelude - 1.3).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }
}
