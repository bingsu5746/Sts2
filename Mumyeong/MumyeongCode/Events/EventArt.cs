using Godot;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Visuals;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Mumyeong.MumyeongCode.Events;

/// <summary>
/// Event backgrounds and the figure cinematics of the 황천 영사실 / stage-up events (user request 2026-10-24: a background
/// for every scene, a figure-themed background and a dedicated animation when the contract is made).
///
/// Background slots (1000x760 png, docs/mumyeong-art-slots.md):
///   images/events/theater_hall.png            the projection room (start pages)
///   images/events/scenes/&lt;scene&gt;.png         one per theater scene (lower-case key, e.g. bridge.png)
///   images/events/figures/&lt;figure&gt;.png       one per figure (descent, contract, card pick, stage-ups)
/// A missing file falls back to the hall, then to the game's own event art.
/// The portrait is swapped with NEventLayout.SetPortrait; the cinematics are nodes anchored to the portrait
/// (NEventLayout.AddVfxAnchoredToPortrait), so they scale with it. Presentation only, local player only.
/// </summary>
public static class EventArt
{
    private const string Dir = $"{MainFile.ResPath}/images/events";
    private const string VfxPrefix = "MumyeongVfx";

    public static string Hall => $"{Dir}/theater_hall.png";
    public static string Scene(string scene) => $"{Dir}/scenes/{scene.ToLowerInvariant()}.png";
    public static string Figure(FigureId figure) => $"{Dir}/figures/{SpiritVisuals.FileKey(figure)}.png";

    /// <summary>The path if it exists, else the hall, else the game's grave event art.</summary>
    public static string Resolve(string path)
    {
        if (ResourceLoader.Exists(path)) return path;
        return ResourceLoader.Exists(Hall) ? Hall : ImageHelper.GetImagePath("events/grave_of_the_forgotten.png");
    }

    private static TextureRect? Portrait()
    {
        var layout = NEventRoom.Instance?.Layout;
        if (layout == null || !GodotObject.IsInstanceValid(layout)) return null;
        return layout.GetNodeOrNull<TextureRect>("%Portrait");
    }

    /// <summary>Swaps the event background (and clears our cinematic nodes).</summary>
    public static void Show(Player? owner, string path)
    {
        if (owner == null || !LocalContext.IsMe(owner)) return;
        try
        {
            var layout = NEventRoom.Instance?.Layout;
            if (layout == null || Portrait() is not { } portrait) return;
            ClearVfx(portrait);
            layout.SetPortrait(GD.Load<Texture2D>(Resolve(path)));
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[EventArt] show {path}: {e.Message}");
        }
    }

    private static void ClearVfx(TextureRect portrait)
    {
        foreach (var child in portrait.GetChildren())
            if (child.Name.ToString().StartsWith(VfxPrefix, StringComparison.Ordinal))
                child.QueueFree();
    }

    // ------------------------------------------------------------------ cinematics

    /// <summary>
    /// 강림: the shadow of the figure walks into the screen — a black silhouette rises out of the bottom, then turns into
    /// the pale spirit, with a glow and drifting motes. Stays on the portrait (floating) until the background changes.
    /// </summary>
    public static void PlayRise(Player? owner, FigureId figure)
    {
        if (owner == null || !LocalContext.IsMe(owner)) return;
        try
        {
            if (Portrait() is not { } portrait) return;
            ClearVfx(portrait);
            var size = portrait.Size;
            var root = new Node2D { Name = $"{VfxPrefix}_Rise", Position = new Vector2(size.X / 2, size.Y * 0.55f) };
            portrait.AddChildSafely(root);

            var glow = new Sprite2D
            {
                Name = "Glow", Texture = RadialTexture(256), Scale = Vector2.One * (size.Y / 256f) * 1.4f,
                Modulate = Tint(figure, 0f), Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            };
            root.AddChild(glow);
            root.AddChild(Motes(size, figure));

            if (SpiritVisuals.Texture(figure, false) is { } tex)
            {
                var spirit = new Sprite2D { Name = "Spirit", Texture = tex };
                var scale = size.Y * 0.95f / tex.GetHeight();
                spirit.Scale = Vector2.One * scale;
                spirit.Position = new Vector2(0, size.Y * 0.25f);
                spirit.Modulate = new Color(0, 0, 0, 0);
                root.AddChild(spirit);

                var t = spirit.CreateTween();
                // a shadow rises...
                t.TweenProperty(spirit, "modulate", new Color(0.02f, 0.02f, 0.04f, 0.95f), 0.9);
                t.Parallel().TweenProperty(spirit, "position", new Vector2(0, -size.Y * 0.02f), 1.6)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
                // ...and the name lights it
                t.TweenProperty(spirit, "modulate", new Color(1, 1, 1, 0.9f), 1.1);
                t.Parallel().TweenProperty(glow, "modulate", Tint(figure, 0.55f), 1.1);
                t.TweenCallback(Callable.From(() => Float(spirit)));
            }
            else
            {
                glow.CreateTween().TweenProperty(glow, "modulate", Tint(figure, 0.55f), 1.2);
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[EventArt] rise {figure}: {e.Message}");
        }
    }

    /// <summary>Contract: the spirit rushes out of the screen into the viewer, and a flash of light.</summary>
    public static async Task PlayMerge(Player? owner, FigureId figure)
    {
        if (owner == null || !LocalContext.IsMe(owner)) return;
        try
        {
            if (Portrait() is not { } portrait) return;
            var root = portrait.GetNodeOrNull<Node2D>($"{VfxPrefix}_Rise");
            if (root == null)
            {
                PlayRise(owner, figure);
                await Wait(2.4);
                root = portrait.GetNodeOrNull<Node2D>($"{VfxPrefix}_Rise");
                if (root == null) return;
            }

            var flash = new ColorRect
            {
                Name = $"{VfxPrefix}_Flash", Color = new Color(1, 1, 1, 0), Size = portrait.Size,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            portrait.AddChildSafely(flash);

            if (root.GetNodeOrNull<Sprite2D>("Spirit") is { } spirit)
            {
                spirit.SetMeta("float_stop", true);
                var t = spirit.CreateTween();
                t.TweenProperty(spirit, "position", spirit.Position + new Vector2(0, 14), 0.35)
                    .SetTrans(Tween.TransitionType.Sine); // a breath in
                t.TweenProperty(spirit, "scale", spirit.Scale * 2.6f, 0.55)
                    .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
                t.Parallel().TweenProperty(spirit, "modulate", new Color(1.6f, 1.6f, 1.8f, 0f), 0.55)
                    .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            }

            var f = flash.CreateTween();
            f.TweenInterval(0.75);
            f.TweenProperty(flash, "color", new Color(1, 1, 1, 0.92f), 0.12);
            f.TweenProperty(flash, "color", Tint(figure, 0.25f), 0.5);
            f.TweenProperty(flash, "color", new Color(1, 1, 1, 0f), 0.9);
            await Wait(2.0);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[EventArt] merge {figure}: {e.Message}");
        }
    }

    // ------------------------------------------------------------------ helpers

    private static void Float(Sprite2D spirit)
    {
        if (!GodotObject.IsInstanceValid(spirit) || spirit.HasMeta("float_stop")) return;
        var y = spirit.Position.Y;
        var t = spirit.CreateTween().SetLoops();
        t.TweenProperty(spirit, "position:y", y - 8f, 2.2).SetTrans(Tween.TransitionType.Sine);
        t.TweenProperty(spirit, "position:y", y + 4f, 2.2).SetTrans(Tween.TransitionType.Sine);
    }

    private static Color Tint(FigureId figure, float alpha)
    {
        var c = FigureRegistry.ColorOf(figure).Lerp(new Color(0.75f, 0.88f, 1f), 0.5f);
        return new Color(c.R, c.G, c.B, alpha);
    }

    private static GradientTexture2D RadialTexture(int size)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1, 1, 1, 1));
        gradient.SetColor(1, new Color(1, 1, 1, 0));
        return new GradientTexture2D
        {
            Gradient = gradient, Width = size, Height = size, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f),
        };
    }

    private static CpuParticles2D Motes(Vector2 size, FigureId figure)
    {
        return new CpuParticles2D
        {
            Name = "Motes", Texture = RadialTexture(16), Amount = 40, Lifetime = 4.0, Preprocess = 2.0,
            Position = new Vector2(0, size.Y * 0.4f), EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(size.X * 0.35f, 10f), Direction = new Vector2(0, -1), Spread = 12f,
            Gravity = Vector2.Zero, InitialVelocityMin = 20f, InitialVelocityMax = 55f, ScaleAmountMin = 0.4f,
            ScaleAmountMax = 1.2f, Color = Tint(figure, 0.7f),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
    }

    private static async Task Wait(double seconds)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }
}
