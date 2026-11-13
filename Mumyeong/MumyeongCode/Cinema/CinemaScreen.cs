using System.Runtime.CompilerServices;
using Godot;
using Mumyeong.MumyeongCode.Events;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Visuals;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace Mumyeong.MumyeongCode.Cinema;

/// <summary>What the cinema shows on a page (set by the event before each SetEventState).</summary>
public enum CinemaFx
{
    /// <summary>Just the background.</summary>
    None,
    /// <summary>Opening: black, two heartbeats, then the projector light brings the hall up.</summary>
    Blackout,
    /// <summary>The three shadows stand up in the hall, each with its epithet.</summary>
    Shadows,
    /// <summary>A scene and its cuts: the three shadows watch from the top, resonance pips under each.</summary>
    Cut,
    /// <summary>The winning shadow walks into the screen and becomes the spirit.</summary>
    Rise,
    /// <summary>The spirit stays on screen (figure pages after the rise).</summary>
    Hold,
    /// <summary>Stage-up: the spirit flickers in like an old reel.</summary>
    Memory,
    /// <summary>End of the theater: the house lights come up.</summary>
    LightsOn,
}

public sealed record Shot(
    string Background,
    CinemaFx Fx = CinemaFx.None,
    IReadOnlyList<FigureId>? Figures = null,
    FigureId? Figure = null,
    IReadOnlyList<FigureId>? Picks = null,
    double TextDelay = 0.4);

/// <summary>
/// Our own full-screen presentation of an event (user request 2026-11-13: "원래 있던 이벤트 화면들 전혀 안 쓰고").
/// The event model stays the game's (its pages, options, multiplayer sync and saving are unchanged): this screen hides
/// the game's event layout, draws the page itself — full-screen background, film grain and letterbox, the event's
/// description as large centred text, its options as film-frame buttons, and the figure cinematics — and a click goes
/// through NEventRoom.OptionButtonClicked, the same call the game's own option buttons make. When the event is
/// finished, the game's own PROCEED option is used.
/// Added as the last child of NEventRoom, so the game's overlays (card choice, deck view, top bar) stay above it.
/// Presentation only and local player only; every failure is logged and swallowed (the hidden game layout is shown
/// again if the screen cannot be built).
/// </summary>
public static class CinemaScreen
{
    private const string NodeName = "MumyeongCinema";

    private sealed class State
    {
        public required EventModel Event;
        public required Player Owner;
        public Shot Shot = new(EventArt.Hall);
        public Shot? Applied;
        public Control? Root;
        public TextureRect? Bg;
        public TextureRect? BgNext;
        public ColorRect? Dim;
        public Node2D? Fx;
        public Control? Overlay; // labels above the fx (epithets, pips)
        public VBoxContainer? Panel;
        public RichTextLabel? Body;
        public VBoxContainer? Options;
        public ColorRect? Flash;
        public Tween? TypeTween;
        public bool Locked;
        public readonly Dictionary<FigureId, Sprite2D> Shadows = new();
        public readonly Dictionary<FigureId, Label> Tags = new();
        public readonly Dictionary<FigureId, Label> Pips = new();
        public Sprite2D? Spirit;
        public Sprite2D? Glow;
        public CpuParticles2D? Motes;
        public int Attempts;
        public double Clock;
    }

    private static readonly ConditionalWeakTable<EventModel, State> States = new();

    // ------------------------------------------------------------------ API (called from the event models)

    /// <summary>Sets what this page looks like. Safe to call from game logic on every client: only the local player's
    /// own event is drawn.</summary>
    public static void Direct(EventModel ev, Player? owner, Shot shot)
    {
        try
        {
            if (owner == null || !LocalContext.IsMe(owner)) return;
            var st = States.GetValue(ev, e => new State { Event = e, Owner = owner });
            st.Shot = shot;
            if (st.Root != null && GodotObject.IsInstanceValid(st.Root)) ApplyShot(st);
            else EnsureAttached(st);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[Cinema] direct: {e.Message}");
        }
    }

    /// <summary>The contract: the spirit rushes out of the screen into you, a flash, the heart beats. Local only; the
    /// caller changes game state before awaiting this.</summary>
    public static async Task PlayMerge(EventModel ev, Player? owner, FigureId figure)
    {
        if (owner == null || !LocalContext.IsMe(owner) || !States.TryGetValue(ev, out var st)) return;
        try
        {
            if (st.Root == null || !GodotObject.IsInstanceValid(st.Root)) return;
            st.Locked = true;
            ClearOptions(st);
            if (st.Spirit is { } spirit && GodotObject.IsInstanceValid(spirit))
            {
                spirit.SetMeta("float_stop", true);
                var t = spirit.CreateTween();
                t.TweenProperty(spirit, "position", spirit.Position + new Vector2(0, 18), 0.4).SetTrans(Tween.TransitionType.Sine);
                t.TweenProperty(spirit, "scale", spirit.Scale * 3.2f, 0.6).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
                t.Parallel().TweenProperty(spirit, "modulate", new Color(1.8f, 1.8f, 2f, 0f), 0.6)
                    .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            }

            if (st.Flash is { } flash)
            {
                var f = flash.CreateTween();
                f.TweenInterval(0.85);
                f.TweenProperty(flash, "color", new Color(1, 1, 1, 0.95f), 0.1);
                f.TweenProperty(flash, "color", Tint(figure, 0.35f), 0.45);
                f.TweenProperty(flash, "color", new Color(0, 0, 0, 0), 1.0);
            }

            await CinemaUi.Wait(0.95);
            Shake(st, 18f, 0.5);
            await CinemaUi.Wait(0.6);
            Heartbeat(st, 2);
            await CinemaUi.Wait(1.2);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[Cinema] merge: {e.Message}");
        }
        finally
        {
            st.Locked = false;
        }
    }

    // ------------------------------------------------------------------ attach

    private static void EnsureAttached(State st)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        var room = NEventRoom.Instance;
        var layout = room?.Layout;
        var ours = st.Owner.RunState.CurrentRoom is EventRoom { CanonicalEvent: { } canonical } &&
                   canonical.GetType() == st.Event.GetType();
        if (room == null || layout == null || !ours || !room.IsInsideTree())
        {
            if (++st.Attempts > 100) return; // ~10 s: give up, the game's own layout stays visible
            tree.CreateTimer(0.1).Timeout += () => EnsureAttached(st);
            return;
        }

        try
        {
            Build(st, room);
            layout.Visible = false;
            st.Event.StateChanged += _ => Callable.From(() => Render(st)).CallDeferred();
            ApplyShot(st);
            Render(st);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[Cinema] build failed, using the game's layout: {e.Message}");
            layout.Visible = true;
            st.Root?.QueueFree();
            st.Root = null;
        }
    }

    private static void Build(State st, NEventRoom room)
    {
        room.GetNodeOrNull(NodeName)?.QueueFree();
        var root = CinemaUi.Full(new Control { Name = NodeName, MouseFilter = Control.MouseFilterEnum.Stop });
        room.AddChild(root);
        st.Root = root;

        root.AddChild(CinemaUi.Full(new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore }));
        st.Bg = CinemaUi.Full(CinemaUi.Cover(null));
        st.BgNext = CinemaUi.Full(CinemaUi.Cover(null));
        st.BgNext.Modulate = new Color(1, 1, 1, 0);
        root.AddChild(st.Bg);
        root.AddChild(st.BgNext);
        st.Dim = CinemaUi.Full(new ColorRect { Color = new Color(0, 0, 0, 0.25f), MouseFilter = Control.MouseFilterEnum.Ignore });
        root.AddChild(st.Dim);

        st.Fx = new Node2D { Name = "Fx" };
        root.AddChild(st.Fx);
        st.Overlay = CinemaUi.Full(new Control { MouseFilter = Control.MouseFilterEnum.Ignore });
        root.AddChild(st.Overlay);

        // bottom shade for the text, letterbox bars, grain
        var shade = new TextureRect
        {
            Texture = CinemaUi.Fade(new Color(0, 0, 0, 0.88f)), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = 0.45f, AnchorBottom = 1,
        };
        root.AddChild(shade);
        var top = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore, AnchorRight = 1, OffsetBottom = 64 };
        var bottom = new ColorRect
        {
            Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore, AnchorRight = 1, AnchorTop = 1,
            AnchorBottom = 1, OffsetTop = -64,
        };
        root.AddChild(top);
        root.AddChild(bottom);
        var grain = new TextureRect
        {
            Texture = new NoiseTexture2D
            {
                Width = 256, Height = 256, Seamless = true,
                Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Value, Frequency = 0.9f },
            },
            StretchMode = TextureRect.StretchModeEnum.Tile, MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0.07f), AnchorRight = 1, AnchorBottom = 1, OffsetLeft = -256, OffsetTop = -256,
        };
        root.AddChild(grain);
        var scratch = new ColorRect
        {
            Color = new Color(1, 1, 1, 0.0f), MouseFilter = Control.MouseFilterEnum.Ignore, AnchorBottom = 1,
            Size = new Vector2(2, 0),
        };
        root.AddChild(scratch);

        // text and choices, growing upward from above the bottom bar
        st.Panel = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = -460, OffsetRight = 460,
            OffsetBottom = -96, GrowVertical = Control.GrowDirection.Begin, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        st.Panel.AddThemeConstantOverride("separation", 22);
        root.AddChild(st.Panel);
        st.Body = CinemaUi.Rich(30);
        st.Body.CustomMinimumSize = new Vector2(920, 0);
        st.Panel.AddChild(st.Body);
        st.Options = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        st.Options.AddThemeConstantOverride("separation", 10);
        st.Options.Alignment = BoxContainer.AlignmentMode.Center;
        st.Panel.AddChild(st.Options);

        st.Flash = CinemaUi.Full(new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
        root.AddChild(st.Flash);

        // click while the text is still typing: show it all at once
        root.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && st.TypeTween is { } tw &&
                tw.IsValid() && tw.IsRunning())
            {
                tw.Kill();
                FinishTyping(st);
            }
        };

        // grain jitter, projector flicker, the odd scratch on the film
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            var rng = new Random();
            Action tick = () =>
            {
                if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return;
                st.Clock += root.GetProcessDeltaTime();
                grain.Position = new Vector2(-rng.Next(256), -rng.Next(256));
                if (st.Bg is { } bg) bg.SelfModulate = new Color(1, 1, 1) * (0.94f + (float)rng.NextDouble() * 0.06f);
                if (rng.NextDouble() < 0.01)
                {
                    scratch.Position = new Vector2((float)rng.NextDouble() * root.Size.X, 0);
                    scratch.Color = new Color(1, 1, 1, 0.18f);
                }
                else if (scratch.Color.A > 0)
                {
                    scratch.Color = new Color(1, 1, 1, Math.Max(0, scratch.Color.A - 0.03f));
                }
            };
            tree.ProcessFrame += tick;
            root.TreeExiting += () => tree.ProcessFrame -= tick;
        }
    }

    // ------------------------------------------------------------------ pages

    private static void ApplyShot(State st)
    {
        if (st.Root == null || !GodotObject.IsInstanceValid(st.Root)) return;
        var shot = st.Shot;
        var first = st.Applied == null;
        var sameBg = st.Applied?.Background == shot.Background;
        st.Applied = shot;
        if (!sameBg) SetBackground(st, shot.Background, first && shot.Fx == CinemaFx.Blackout ? 2.2 : 0.8,
            shot.Fx == CinemaFx.Blackout ? 1.8 : 0);

        switch (shot.Fx)
        {
            case CinemaFx.Blackout:
                Heartbeat(st, 2);
                break;
            case CinemaFx.Shadows when shot.Figures != null:
                LayoutShadows(st, shot.Figures, standing: true);
                break;
            case CinemaFx.Cut when shot.Figures != null:
                LayoutShadows(st, shot.Figures, standing: false);
                UpdatePips(st, shot.Figures, shot.Picks ?? []);
                break;
            case CinemaFx.Rise when shot.Figure is { } f:
                Rise(st, f);
                break;
            case CinemaFx.Hold when shot.Figure is { } f:
                if (st.Spirit == null || !GodotObject.IsInstanceValid(st.Spirit)) ShowSpirit(st, f, 0.85f, fadeIn: 0.6);
                break;
            case CinemaFx.Memory when shot.Figure is { } f:
                ClearFigures(st);
                ShowSpirit(st, f, 0.72f, fadeIn: 1.6, flicker: true);
                break;
            case CinemaFx.LightsOn:
                ClearFigures(st);
                if (st.Dim is { } dim) dim.CreateTween().TweenProperty(dim, "color", new Color(0, 0, 0, 0), 1.6);
                break;
            default:
                break;
        }
    }

    private static void SetBackground(State st, string path, double fade, double delay)
    {
        if (st.Bg == null || st.BgNext == null) return;
        var tex = GD.Load<Texture2D>(EventArt.Resolve(path));
        if (st.Bg.Texture == null)
        {
            st.Bg.Texture = tex;
            st.Bg.Modulate = new Color(1, 1, 1, 0);
            var t0 = st.Bg.CreateTween();
            t0.TweenInterval(delay);
            t0.TweenProperty(st.Bg, "modulate:a", 1f, fade);
            return;
        }

        // reel change: through black
        st.BgNext.Texture = tex;
        var t = st.BgNext.CreateTween();
        t.TweenInterval(delay);
        t.TweenProperty(st.Bg, "modulate:a", 0f, fade * 0.5);
        t.TweenCallback(Callable.From(() =>
        {
            if (st.Bg == null || st.BgNext == null) return;
            st.Bg.Texture = tex;
        }));
        t.TweenProperty(st.Bg, "modulate:a", 1f, fade * 0.5);
    }

    /// <summary>Description and options of the event's current state.</summary>
    private static void Render(State st)
    {
        try
        {
            if (st.Root == null || !GodotObject.IsInstanceValid(st.Root) || st.Body == null || st.Options == null) return;
            if (st.Locked) return; // a cinematic is playing; the next state change renders
            var ev = st.Event;
            var text = ev.Description?.GetFormattedText() ?? "";
            CinemaUi.SetRich(st.Body, text);
            ClearOptions(st);

            var options = new List<(string Title, string Sub, Action Choose)>();
            if (ev.IsFinished)
            {
                options.Add((CinemaUi.Loc("MUMYEONG-CINEMA.CONTINUE").GetFormattedText(), "", () => Proceed(st)));
            }
            else
            {
                var list = ev.CurrentOptions;
                for (var i = 0; i < list.Count; i++)
                {
                    var option = list[i];
                    var index = i;
                    var sub = option.Description?.GetFormattedText() ?? "";
                    if (sub is "..." or " ") sub = "";
                    options.Add((option.Title?.GetFormattedText() ?? "...", sub, () => Choose(st, option, index)));
                }
            }

            foreach (var (title, sub, choose) in options)
            {
                var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                box.AddThemeConstantOverride("separation", 2);
                var button = CinemaUi.ChoiceButton(CinemaUi.Plain(title), options.Count > 1 ? 24 : 26);
                button.Pressed += () =>
                {
                    if (st.Locked) return;
                    st.Locked = true;
                    foreach (var b in st.Options.GetChildren().OfType<VBoxContainer>().SelectMany(v => v.GetChildren().OfType<Button>()))
                        b.Disabled = b != button;
                    var t = button.CreateTween();
                    t.TweenProperty(button, "modulate", new Color(1.4f, 1.3f, 1.0f), 0.08);
                    t.TweenProperty(button, "modulate", Colors.White, 0.25);
                    t.TweenCallback(Callable.From(() =>
                    {
                        st.Locked = false;
                        choose();
                    }));
                };
                box.AddChild(button);
                if (CinemaUi.Plain(sub).Trim().Length > 0)
                    box.AddChild(CinemaUi.Label(CinemaUi.Plain(sub), 17, CinemaUi.Muted));
                st.Options.AddChild(box);
            }

            // typewriter, then the choices fade in
            st.Body.VisibleRatio = 0;
            st.Options.Modulate = new Color(1, 1, 1, 0);
            var plainLength = CinemaUi.Plain(text).Length;
            var tween = st.Body.CreateTween();
            tween.TweenInterval(st.Applied?.TextDelay ?? 0.3);
            tween.TweenProperty(st.Body, "visible_ratio", 1f, Math.Clamp(plainLength * 0.03, 0.3, 2.8));
            tween.TweenCallback(Callable.From(() => FinishTyping(st)));
            st.TypeTween = tween;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[Cinema] render: {e.Message}");
        }
    }

    private static void FinishTyping(State st)
    {
        if (st.Body == null || st.Options == null) return;
        st.Body.VisibleRatio = 1;
        st.Options.CreateTween().TweenProperty(st.Options, "modulate:a", 1f, 0.35);
        st.Options.GetChildren().OfType<VBoxContainer>().FirstOrDefault()?.GetChildren().OfType<Button>()
            .FirstOrDefault()?.GrabFocus();
    }

    private static void ClearOptions(State st)
    {
        if (st.Options == null) return;
        foreach (var child in st.Options.GetChildren())
        {
            st.Options.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static void Choose(State st, MegaCrit.Sts2.Core.Events.EventOption option, int index)
    {
        try
        {
            ClearOptions(st);
            NEventRoom.Instance?.OptionButtonClicked(option, index);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[Cinema] choose {index}: {e.Message}");
        }
    }

    /// <summary>The finished event: the game's own PROCEED option (its button is built on the hidden layout).</summary>
    private static void Proceed(State st)
    {
        try
        {
            var room = NEventRoom.Instance;
            var proceed = room?.Layout?.OptionButtons.FirstOrDefault(b => b.Option.IsProceed);
            if (room != null && proceed != null)
            {
                ClearOptions(st);
                room.OptionButtonClicked(proceed.Option, 0);
                return;
            }

            TaskHelper.RunSafely(NEventRoom.Proceed());
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[Cinema] proceed: {e.Message}");
        }
    }

    // ------------------------------------------------------------------ figures

    private static Vector2 ViewSize(State st) => st.Root?.Size is { X: > 0 } s ? s : new Vector2(1920, 1080);

    private static Sprite2D MakeShadow(State st, FigureId figure)
    {
        if (st.Shadows.TryGetValue(figure, out var s) && GodotObject.IsInstanceValid(s)) return s;
        s = new Sprite2D { Texture = SpiritVisuals.Texture(figure, false), Modulate = new Color(0.02f, 0.02f, 0.03f, 0f) };
        st.Fx!.AddChild(s);
        st.Shadows[figure] = s;
        var tag = CinemaUi.Label($"「{FigureRegistry.Shadow(figure)}」", 22, CinemaUi.Gold, bold: true);
        tag.Size = new Vector2(360, 40);
        tag.Modulate = new Color(1, 1, 1, 0);
        st.Overlay!.AddChild(tag);
        st.Tags[figure] = tag;
        var pips = CinemaUi.Label("", 22, CinemaUi.Gold);
        pips.Size = new Vector2(360, 30);
        pips.Modulate = new Color(1, 1, 1, 0);
        st.Overlay.AddChild(pips);
        st.Pips[figure] = pips;
        return s;
    }

    /// <summary>Standing in the hall (Shadows) or small along the top, watching (Cut).</summary>
    private static void LayoutShadows(State st, IReadOnlyList<FigureId> figures, bool standing)
    {
        var size = ViewSize(st);
        for (var i = 0; i < figures.Count; i++)
        {
            var f = figures[i];
            var s = MakeShadow(st, f);
            if (s.Texture == null) continue;
            var h = size.Y * (standing ? 0.52f : 0.26f);
            var scale = h / s.Texture.GetHeight();
            var x = size.X * (standing ? 0.27f + 0.23f * i : 0.30f + 0.20f * i);
            var y = standing ? size.Y * 0.40f : size.Y * 0.20f;
            var first = s.Modulate.A < 0.01f;
            if (first)
            {
                s.Position = new Vector2(x, y + 30);
                s.Scale = Vector2.One * scale;
            }

            var t = s.CreateTween().SetParallel();
            var delay = first && standing ? 0.6 * i + 0.3 : 0;
            t.TweenProperty(s, "position", new Vector2(x, y), 0.9).SetDelay(delay).SetTrans(Tween.TransitionType.Sine);
            t.TweenProperty(s, "scale", Vector2.One * scale, 0.9).SetDelay(delay).SetTrans(Tween.TransitionType.Sine);
            t.TweenProperty(s, "modulate", new Color(0.02f, 0.02f, 0.03f, standing ? 0.95f : 0.8f), 0.9).SetDelay(delay);

            var tag = st.Tags[f];
            var tagY = standing ? y - h * 0.62f : y + h * 0.42f;
            tag.Position = new Vector2(x - 180, tagY);
            tag.AddThemeFontSizeOverride("font_size", standing ? 24 : 18);
            tag.CreateTween().TweenProperty(tag, "modulate:a", 1f, 0.6).SetDelay(delay + 0.5);
            var pips = st.Pips[f];
            pips.Position = new Vector2(x - 180, tagY + 28);
            pips.CreateTween().TweenProperty(pips, "modulate:a", standing ? 0f : 1f, 0.4);
        }
    }

    private static void UpdatePips(State st, IReadOnlyList<FigureId> figures, IReadOnlyList<FigureId> picks)
    {
        foreach (var f in figures)
        {
            if (!st.Pips.TryGetValue(f, out var label)) continue;
            var n = picks.Count(p => p == f);
            label.Text = string.Concat(Enumerable.Range(0, 3).Select(i => i < n ? "●" : "○"));
        }

        // the shadow chosen last steps closer and catches a little light
        if (picks.Count > 0 && st.Shadows.TryGetValue(picks[^1], out var s) && GodotObject.IsInstanceValid(s))
        {
            var t = s.CreateTween();
            t.TweenProperty(s, "scale", s.Scale * 1.12f, 0.25).SetTrans(Tween.TransitionType.Back);
            t.TweenProperty(s, "scale", s.Scale * 1.05f, 0.35);
            var n = picks.Count(p => p == picks[^1]);
            s.CreateTween().TweenProperty(s, "modulate", new Color(0.05f + 0.08f * n, 0.06f + 0.09f * n, 0.09f + 0.12f * n, 0.9f), 0.5);
        }
    }

    private static void Rise(State st, FigureId winner)
    {
        var size = ViewSize(st);
        foreach (var (f, s) in st.Shadows.ToList())
        {
            if (f == winner || !GodotObject.IsInstanceValid(s)) continue;
            s.CreateTween().TweenProperty(s, "modulate:a", 0f, 0.8);
        }
        foreach (var (f, tag) in st.Tags.ToList())
            tag.CreateTween().TweenProperty(tag, "modulate:a", 0f, 0.5);
        foreach (var (f, pips) in st.Pips.ToList())
            pips.CreateTween().TweenProperty(pips, "modulate:a", 0f, 0.5);

        var shadow = MakeShadow(st, winner);
        if (shadow.Texture == null) return;
        var scale = size.Y * 0.9f / shadow.Texture.GetHeight();
        var center = new Vector2(size.X * 0.5f, size.Y * 0.42f);
        var t = shadow.CreateTween();
        t.TweenProperty(shadow, "modulate", new Color(0.02f, 0.02f, 0.03f, 0.97f), 0.5);
        t.Parallel().TweenProperty(shadow, "position", center, 1.6).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        t.Parallel().TweenProperty(shadow, "scale", Vector2.One * scale, 1.6).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        t.TweenInterval(0.3);
        t.TweenCallback(Callable.From(() =>
        {
            // the name lights the shadow: it becomes the spirit
            if (!GodotObject.IsInstanceValid(shadow)) return;
            st.Shadows.Remove(winner);
            st.Spirit = shadow;
            AddGlow(st, winner, center, size);
            var light = shadow.CreateTween();
            light.TweenProperty(shadow, "modulate", new Color(1, 1, 1, 0.9f), 1.2);
            light.TweenCallback(Callable.From(() => Float(shadow)));
        }));
    }

    private static void ShowSpirit(State st, FigureId figure, float alpha, double fadeIn, bool flicker = false)
    {
        var size = ViewSize(st);
        if (SpiritVisuals.Texture(figure, false) is not { } tex || st.Fx == null) return;
        if (st.Spirit != null && GodotObject.IsInstanceValid(st.Spirit)) st.Spirit.QueueFree();
        var center = new Vector2(size.X * 0.5f, size.Y * 0.42f);
        var spirit = new Sprite2D
        {
            Texture = tex, Position = center + new Vector2(0, 40), Scale = Vector2.One * (size.Y * 0.9f / tex.GetHeight()),
            Modulate = new Color(1, 1, 1, 0),
        };
        st.Fx.AddChild(spirit);
        st.Spirit = spirit;
        AddGlow(st, figure, center, size);
        var t = spirit.CreateTween();
        if (flicker)
        {
            for (var i = 0; i < 4; i++)
            {
                t.TweenProperty(spirit, "modulate:a", alpha * (0.3f + 0.15f * i), 0.12);
                t.TweenProperty(spirit, "modulate:a", alpha * 0.1f, 0.1);
            }
        }
        t.TweenProperty(spirit, "modulate:a", alpha, fadeIn);
        t.Parallel().TweenProperty(spirit, "position", center, fadeIn).SetTrans(Tween.TransitionType.Sine);
        t.TweenCallback(Callable.From(() => Float(spirit)));
    }

    private static void AddGlow(State st, FigureId figure, Vector2 center, Vector2 size)
    {
        if (st.Fx == null) return;
        if (st.Glow != null && GodotObject.IsInstanceValid(st.Glow)) st.Glow.QueueFree();
        var glow = new Sprite2D
        {
            Texture = CinemaUi.Radial(), Position = center, Scale = Vector2.One * (size.Y / 256f) * 1.5f,
            Modulate = Tint(figure, 0f), Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        st.Fx.AddChild(glow);
        st.Fx.MoveChild(glow, 0);
        st.Glow = glow;
        glow.CreateTween().TweenProperty(glow, "modulate", Tint(figure, 0.5f), 1.2);
        var motes = new CpuParticles2D
        {
            Texture = CinemaUi.Radial(16), Amount = 50, Lifetime = 5.0, Preprocess = 2.5,
            Position = new Vector2(center.X, size.Y * 0.95f), EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(size.X * 0.3f, 10f), Direction = new Vector2(0, -1), Spread = 12f,
            Gravity = Vector2.Zero, InitialVelocityMin = 25f, InitialVelocityMax = 70f, ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.4f, Color = Tint(figure, 0.75f),
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        if (st.Motes != null && GodotObject.IsInstanceValid(st.Motes)) st.Motes.QueueFree();
        st.Fx.AddChild(motes);
        st.Motes = motes;
    }

    private static void ClearFigures(State st)
    {
        foreach (var s in st.Shadows.Values) if (GodotObject.IsInstanceValid(s)) s.QueueFree();
        foreach (var l in st.Tags.Values) if (GodotObject.IsInstanceValid(l)) l.QueueFree();
        foreach (var l in st.Pips.Values) if (GodotObject.IsInstanceValid(l)) l.QueueFree();
        st.Shadows.Clear();
        st.Tags.Clear();
        st.Pips.Clear();
        if (st.Spirit != null && GodotObject.IsInstanceValid(st.Spirit))
            st.Spirit.CreateTween().TweenProperty(st.Spirit, "modulate:a", 0f, 0.8);
        if (st.Glow != null && GodotObject.IsInstanceValid(st.Glow))
            st.Glow.CreateTween().TweenProperty(st.Glow, "modulate:a", 0f, 0.8);
        if (st.Motes != null && GodotObject.IsInstanceValid(st.Motes)) st.Motes.Emitting = false;
        st.Spirit = null;
        st.Glow = null;
        st.Motes = null;
    }

    private static void Float(Sprite2D s)
    {
        if (!GodotObject.IsInstanceValid(s) || s.HasMeta("float_stop")) return;
        var y = s.Position.Y;
        var t = s.CreateTween().SetLoops();
        t.TweenProperty(s, "position:y", y - 10f, 2.4).SetTrans(Tween.TransitionType.Sine);
        t.TweenProperty(s, "position:y", y + 4f, 2.4).SetTrans(Tween.TransitionType.Sine);
    }

    // ------------------------------------------------------------------ screen effects

    private static void Heartbeat(State st, int beats)
    {
        if (st.Flash is not { } flash) return;
        var t = flash.CreateTween();
        for (var i = 0; i < beats; i++)
        {
            t.TweenProperty(flash, "color", new Color(0.55f, 0.05f, 0.05f, 0.28f), 0.08);
            t.TweenProperty(flash, "color", new Color(0, 0, 0, 0), 0.25);
            t.TweenProperty(flash, "color", new Color(0.55f, 0.05f, 0.05f, 0.18f), 0.08);
            t.TweenProperty(flash, "color", new Color(0, 0, 0, 0), 0.5);
        }
    }

    private static void Shake(State st, float strength, double seconds)
    {
        if (st.Root is not { } root) return;
        var rng = new Random();
        var t = root.CreateTween();
        var steps = (int)(seconds / 0.04);
        for (var i = 0; i < steps; i++)
        {
            var k = strength * (1f - (float)i / steps);
            t.TweenProperty(root, "position", new Vector2((float)(rng.NextDouble() * 2 - 1) * k, (float)(rng.NextDouble() * 2 - 1) * k), 0.04);
        }
        t.TweenProperty(root, "position", Vector2.Zero, 0.05);
    }

    private static Color Tint(FigureId figure, float alpha)
    {
        var c = FigureRegistry.ColorOf(figure).Lerp(new Color(0.75f, 0.88f, 1f), 0.5f);
        return new Color(c.R, c.G, c.B, alpha);
    }
}
