using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Runs;

namespace MagicSwordsman.MagicSwordsmanCode.RestSite;

/// <summary>
/// An event-style choice page (picture on the left, story text and option buttons on the right) for the sword forge,
/// instead of the game's "choose a card" screen (user request 2026-10-09: "카드 선택처럼 말고 고유 이벤트로").
/// The choice is synced like the game's own selection screens (CardSelectCmd.FromChooseACardScreen): the local player
/// picks on screen and the index is sent with PlayerChoiceSynchronizer; other clients wait for it.
/// Returns the chosen option index, or -1 for the back button.
/// UNVERIFIED in game: look at 1080p and other resolutions; multiplayer.
/// </summary>
public static class EventChoiceScreen
{
    public sealed record Option(string Label, string Description, bool Enabled = true);

    private static readonly Color PanelBg = new(0.06f, 0.055f, 0.08f, 0.97f);
    private static readonly Color Text = new(0.93f, 0.9f, 0.84f);
    private static readonly Color Muted = new(0.7f, 0.68f, 0.72f);

    public static async Task<int> Choose(Player player, PlayerChoiceContext context, string title, string body,
        string? imagePath, Color accent, IReadOnlyList<Option> options, string? backLabel)
    {
        var sync = RunManager.Instance.PlayerChoiceSynchronizer;
        var choiceId = sync.ReserveChoiceId(player);
        await context.SignalPlayerChoiceBegun(PlayerChoiceOptions.None);
        int index;
        if (LocalContext.IsMe(player) && RunManager.Instance.NetService.Type != MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Replay)
        {
            index = await ShowLocal(title, body, imagePath, accent, options, backLabel);
            sync.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndex(index));
        }
        else
        {
            index = (await sync.WaitForRemoteChoice(player, choiceId)).AsIndex();
        }
        await context.SignalPlayerChoiceEnded();
        return index;
    }

    private static Task<int> ShowLocal(string title, string body, string? imagePath, Color accent,
        IReadOnlyList<Option> options, string? backLabel)
    {
        var done = new TaskCompletionSource<int>();
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            done.SetResult(options.Count > 0 ? 0 : -1);
            return done.Task;
        }

        var layer = new CanvasLayer { Layer = 115, Name = "MagicSwordsmanEventChoice" };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.75f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        // Layout (2026-10-10 fix, "마검 강화 누른 건데 UI가 저렇게 뜨네"): the old option button set its minimum height in
        // Ready from its labels, measured before they had a width, so autowrap broke every character onto its own line:
        // one option grew taller than the screen, pushed the panel off screen and the picture (centred in the art
        // panel) with it. Now the panel has a fixed size that fits the viewport, every option is a PanelContainer that
        // takes its height from its wrapped text, the options sit in a ScrollContainer (the panel can never grow), and
        // the picture fills the art panel.
        var view = tree.Root.GetVisibleRect().Size;
        // canvas_items stretch: the visible rect is at least the 1920x1080 base; guard against an unsized viewport
        if (view.X < 1000f || view.Y < 700f) view = new Vector2(1920f, 1080f);
        var panelSize = new Vector2(Math.Min(1440f, view.X - 60f), Math.Min(820f, view.Y - 60f));
        var panel = new PanelContainer { CustomMinimumSize = panelSize, MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Box(PanelBg, new Color(accent, 0.85f), 3, 16, 26));
        center.AddChild(panel);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 30);
        panel.AddChild(row);

        // ---- picture (fills the art panel, aspect kept)
        var art = new PanelContainer
        {
            CustomMinimumSize = new Vector2(Math.Min(520f, panelSize.X * 0.36f), 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        art.AddThemeStyleboxOverride("panel", Box(new Color(accent.Darkened(0.88f), 1f), new Color(accent, 0.6f), 2, 10, 18,
            new Color(accent, 0.25f), 36));
        var tex = imagePath != null && ResourceLoader.Exists(imagePath) ? GD.Load<Texture2D>(imagePath) : null;
        if (tex == null && imagePath != null) MainFile.Logger.Warn($"[EventChoiceScreen] missing picture {imagePath}");
        if (tex != null)
        {
            art.AddChild(new TextureRect
            {
                Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }
        row.AddChild(art);

        // ---- title, story, options (scrolling), back
        var col = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        col.AddThemeConstantOverride("separation", 14);
        row.AddChild(col);
        col.AddChild(Rich($"[b]{Esc(title)}[/b]", 40, accent.Lightened(0.35f), true));
        if (!string.IsNullOrWhiteSpace(body)) col.AddChild(Rich(Bb(body), 23, Text, false));

        var rule = new ColorRect
        {
            Color = new Color(accent, 0.35f), CustomMinimumSize = new Vector2(0, 2), MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        col.AddChild(rule);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        col.AddChild(scroll);
        var list = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        list.AddThemeConstantOverride("separation", 10);
        var gutter = new MarginContainer // room for the scroll bar
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        gutter.AddThemeConstantOverride("margin_right", 16);
        gutter.AddChild(list);
        scroll.AddChild(gutter);

        var closing = false;
        void Finish(int i)
        {
            if (closing) return;
            closing = true;
            var t = root.CreateTween();
            t.TweenProperty(root, "modulate:a", 0f, 0.15);
            t.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(layer)) layer.QueueFree();
                done.TrySetResult(i);
            }));
        }

        for (var i = 0; i < options.Count; i++)
        {
            var idx = i;
            list.AddChild(OptionButton(options[i], accent, () => Finish(idx)));
        }

        if (backLabel != null)
        {
            var back = new Button
            {
                Text = backLabel, CustomMinimumSize = new Vector2(220, 52), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            };
            StyleButton(back, new Color(0.25f, 0.42f, 0.48f), 24);
            back.Pressed += () => Finish(-1);
            col.AddChild(back);
        }

        tree.Root.AddChild(layer);
        root.Modulate = new Color(1, 1, 1, 0);
        root.CreateTween().TweenProperty(root, "modulate:a", 1f, 0.2);
        return done.Task;
    }

    /// <summary>
    /// One option: a PanelContainer whose height comes from its wrapped text. A flat Button below the text fills it and
    /// takes the clicks (and draws the normal / hover / pressed / disabled frames); the labels on top ignore the mouse.
    /// </summary>
    private static Control OptionButton(Option o, Color accent, Action onPress)
    {
        var holder = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        holder.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        var b = new Button
        {
            Disabled = !o.Enabled, FocusMode = Control.FocusModeEnum.All,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        StyleButton(b, accent, 0);
        b.Pressed += onPress;
        holder.AddChild(b);

        var pad = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        pad.AddThemeConstantOverride("margin_left", 22);
        pad.AddThemeConstantOverride("margin_right", 22);
        pad.AddThemeConstantOverride("margin_top", 12);
        pad.AddThemeConstantOverride("margin_bottom", 12);
        holder.AddChild(pad);

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 4);
        pad.AddChild(box);
        box.AddChild(Rich($"[b]{Esc(o.Label)}[/b]", 25, o.Enabled ? new Color(1f, 0.92f, 0.7f) : Muted, true));
        if (!string.IsNullOrWhiteSpace(o.Description))
            box.AddChild(Rich(Bb(o.Description), 19, o.Enabled ? Text : Muted, false));
        return holder;
    }

    private static void StyleButton(Button b, Color accent, int fontSize)
    {
        b.AddThemeStyleboxOverride("normal", Box(new Color(0.11f, 0.1f, 0.14f, 0.95f), new Color(accent, 0.45f), 2, 10, 0));
        b.AddThemeStyleboxOverride("hover", Box(new Color(0.18f, 0.15f, 0.22f, 0.98f), new Color(accent, 0.95f), 2, 10, 0,
            new Color(accent, 0.3f), 14));
        b.AddThemeStyleboxOverride("pressed", Box(new Color(0.24f, 0.2f, 0.3f, 1f), accent, 2, 10, 0));
        b.AddThemeStyleboxOverride("disabled", Box(new Color(0.08f, 0.08f, 0.09f, 0.9f), new Color(0.3f, 0.3f, 0.3f, 0.5f), 2, 10, 0));
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        if (fontSize > 0)
        {
            b.AddThemeFontSizeOverride("font_size", fontSize);
            if (GameFont(true) is { } f) b.AddThemeFontOverride("font", f);
            b.AddThemeColorOverride("font_color", Text);
        }
    }

    private static RichTextLabel Rich(string bbcode, int size, Color color, bool bold)
    {
        var l = new RichTextLabel
        {
            BbcodeEnabled = true, Text = bbcode, FitContent = true, ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        l.AddThemeFontSizeOverride("normal_font_size", size);
        l.AddThemeFontSizeOverride("bold_font_size", size);
        l.AddThemeColorOverride("default_color", color);
        if (GameFont(false) is { } f) l.AddThemeFontOverride("normal_font", f);
        if (GameFont(true) is { } fb) l.AddThemeFontOverride("bold_font", fb);
        l.AddThemeColorOverride("font_outline_color", Colors.Black);
        l.AddThemeConstantOverride("outline_size", bold ? 5 : 3);
        return l;
    }

    private static StyleBoxFlat Box(Color bg, Color border, int bw, int radius, int margin, Color? shadow = null, int shadowSize = 0)
    {
        var s = new StyleBoxFlat
        {
            BgColor = bg, BorderColor = border, BorderWidthLeft = bw, BorderWidthRight = bw, BorderWidthTop = bw,
            BorderWidthBottom = bw, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
        };
        if (shadow is { } sc) { s.ShadowColor = sc; s.ShadowSize = shadowSize; }
        return s;
    }

    private static Font? GameFont(bool bold)
    {
        try
        {
            var lang = LocManager.Instance?.Language ?? "eng";
            return FontManager.GetSubstituteFont(lang, bold ? FontType.Bold : FontType.Regular);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Esc(string s) => s.Replace("[", "[lb]");

    /// <summary>The game's [gold] markup → plain BBCode colour; other game tags are dropped.</summary>
    private static string Bb(string s)
    {
        s = s.Replace("[gold]", "[color=#e8c56a]").Replace("[/gold]", "[/color]");
        return Regex.Replace(s, @"\[(?!/?(color|b|i)\b)[^\]]*\]", "");
    }
}
