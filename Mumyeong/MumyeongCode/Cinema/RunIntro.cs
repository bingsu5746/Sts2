using Godot;

namespace Mumyeong.MumyeongCode.Cinema;

/// <summary>
/// The character's own opening (user request 2026-10-24, like the 치이카와 mod's: after the check button, the
/// character's animation, then Neow). Played once per run over the first room (Neow) by SoulCore: a film-leader
/// countdown, the projector light, the reaper's silhouette lit from behind, the name. About five seconds; a click
/// skips it. Presentation only (local player); failures are logged and swallowed.
/// The game's own character-select transition is a shader material we do not replace: a material that failed to load
/// there would break every later screen transition (NTransition keeps the failed material).
/// </summary>
public static class RunIntro
{
    private const string LayerName = "MumyeongRunIntro";

    public static void Play()
    {
        try { PlayInner(); }
        catch (Exception e) { MainFile.Logger.Warn($"[RunIntro] {e.Message}"); }
    }

    private static void PlayInner()
    {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root.GetNodeOrNull(LayerName) != null) return;
        var layer = new CanvasLayer { Layer = 110, Name = LayerName };
        var root = CinemaUi.Full(new Control { MouseFilter = Control.MouseFilterEnum.Stop });
        layer.AddChild(root);
        tree.Root.AddChild(layer);
        var size = root.GetViewportRect().Size;
        var c = size / 2;

        root.AddChild(CinemaUi.Full(new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore }));
        var hallPath = $"{MainFile.ResPath}/images/events/theater_hall.png";
        var hall = CinemaUi.Full(CinemaUi.Cover(ResourceLoader.Exists(hallPath) ? GD.Load<Texture2D>(hallPath) : null));
        hall.Modulate = new Color(1, 1, 1, 0);
        root.AddChild(hall);

        // film leader: ring, cross, a number
        var leader = new Node2D { Position = c };
        root.AddChild(leader);
        var ring = new Line2D { Width = 6, DefaultColor = new Color(0.85f, 0.85f, 0.8f, 0.9f) };
        for (var i = 0; i <= 64; i++)
        {
            var a = Mathf.Tau * i / 64;
            ring.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 170);
        }
        leader.AddChild(ring);
        leader.AddChild(new Line2D { Points = [new Vector2(-260, 0), new Vector2(260, 0)], Width = 3, DefaultColor = new Color(0.85f, 0.85f, 0.8f, 0.6f) });
        leader.AddChild(new Line2D { Points = [new Vector2(0, -260), new Vector2(0, 260)], Width = 3, DefaultColor = new Color(0.85f, 0.85f, 0.8f, 0.6f) });
        var sweep = new Polygon2D { Color = new Color(1, 1, 1, 0.12f) };
        var wedge = new List<Vector2> { Vector2.Zero };
        for (var i = 0; i <= 16; i++)
        {
            var a = -Mathf.Pi / 2 + Mathf.Pi / 2 * i / 16;
            wedge.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 168);
        }
        sweep.Polygon = wedge.ToArray();
        leader.AddChild(sweep);
        var number = CinemaUi.Label("3", 180, new Color(0.9f, 0.9f, 0.86f), bold: true);
        number.Size = new Vector2(400, 260);
        number.Position = c - new Vector2(200, 130);
        number.VerticalAlignment = VerticalAlignment.Center;
        root.AddChild(number);

        // the reaper, the name
        var reaperPath = $"{MainFile.ResPath}/images/character/reaper.png";
        var reaper = new Sprite2D
        {
            Texture = ResourceLoader.Exists(reaperPath) ? GD.Load<Texture2D>(reaperPath) : null,
            Position = new Vector2(size.X * 0.68f, size.Y * 0.56f), Scale = Vector2.One * (size.Y * 0.95f / 860f),
            Modulate = new Color(0, 0, 0, 0),
        };
        root.AddChild(reaper);
        var glow = new Sprite2D
        {
            Texture = CinemaUi.Radial(), Position = reaper.Position + new Vector2(-40, -120), Scale = Vector2.One * 5f,
            Modulate = new Color(0.55f, 0.75f, 0.95f, 0f), Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        root.AddChild(glow);
        root.MoveChild(glow, root.GetChildCount() - 2);
        var title = CinemaUi.Label(CinemaUi.Loc("MUMYEONG-INTRO.TITLE").GetFormattedText(), 110, CinemaUi.Gold, bold: true);
        title.Size = new Vector2(size.X * 0.5f, 160);
        title.Position = new Vector2(size.X * 0.08f, size.Y * 0.36f);
        title.HorizontalAlignment = HorizontalAlignment.Left;
        title.Modulate = new Color(1, 1, 1, 0);
        root.AddChild(title);
        var line = CinemaUi.Label(CinemaUi.Loc("MUMYEONG-INTRO.LINE").GetFormattedText(), 30, CinemaUi.Paper);
        line.Size = new Vector2(size.X * 0.5f, 60);
        line.Position = new Vector2(size.X * 0.08f + 6, size.Y * 0.36f + 160);
        line.HorizontalAlignment = HorizontalAlignment.Left;
        line.Modulate = new Color(1, 1, 1, 0);
        root.AddChild(line);
        var flash = CinemaUi.Full(new ColorRect { Color = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
        root.AddChild(flash);

        var t = root.CreateTween();
        // 3 · 2 · 1, the sweep turning once per number
        foreach (var n in new[] { "3", "2", "1" })
        {
            t.TweenCallback(Callable.From(() =>
            {
                number.Text = n;
                sweep.Rotation = 0;
                sweep.CreateTween().TweenProperty(sweep, "rotation", Mathf.Tau, 0.5);
            }));
            t.TweenInterval(0.5);
        }
        // the projector light
        t.TweenCallback(Callable.From(() =>
        {
            leader.Visible = false;
            number.Visible = false;
        }));
        t.TweenProperty(flash, "color", new Color(1, 1, 1, 0.85f), 0.06);
        t.TweenProperty(flash, "color", new Color(1, 1, 1, 0f), 0.5);
        t.Parallel().TweenProperty(hall, "modulate:a", 0.45f, 0.8);
        // a silhouette in the beam, then the cold light on him
        t.TweenProperty(reaper, "modulate", new Color(0.02f, 0.02f, 0.03f, 1f), 0.6);
        t.Parallel().TweenProperty(glow, "modulate:a", 0.45f, 1.0);
        t.TweenProperty(reaper, "modulate", Colors.White, 0.9);
        t.Parallel().TweenProperty(title, "modulate:a", 1f, 0.8);
        t.TweenProperty(line, "modulate:a", 1f, 0.6);
        t.TweenInterval(1.4);
        t.TweenProperty(root, "modulate:a", 0f, 0.8);
        t.TweenCallback(Callable.From(layer.QueueFree));

        root.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true } || !GodotObject.IsInstanceValid(root)) return;
            t.Kill();
            var skip = root.CreateTween();
            skip.TweenProperty(root, "modulate:a", 0f, 0.35);
            skip.TweenCallback(Callable.From(layer.QueueFree));
        };
    }
}
