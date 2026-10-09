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

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1440, 760), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Box(PanelBg, new Color(accent, 0.85f), 3, 16, 26));
        center.AddChild(panel);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 30);
        panel.AddChild(row);

        // ---- picture
        var art = new PanelContainer { CustomMinimumSize = new Vector2(520, 700), MouseFilter = Control.MouseFilterEnum.Ignore };
        art.AddThemeStyleboxOverride("panel", Box(new Color(accent.Darkened(0.88f), 1f), new Color(accent, 0.6f), 2, 10, 0,
            new Color(accent, 0.25f), 36));
        if (imagePath != null && ResourceLoader.Exists(imagePath))
        {
            art.AddChild(new TextureRect
            {
                Texture = GD.Load<Texture2D>(imagePath), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }
        row.AddChild(art);

        // ---- text + options
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(820, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 12);
        row.AddChild(col);
        col.AddChild(Rich($"[b]{Esc(title)}[/b]", 40, accent.Lightened(0.35f), true));
        var story = Rich(Bb(body), 23, Text, false);
        story.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(story);

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
            col.AddChild(OptionButton(options[i], accent, () => Finish(idx)));
        }

        if (backLabel != null)
        {
            var back = new Button { Text = backLabel, CustomMinimumSize = new Vector2(220, 52), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
            StyleButton(back, new Color(0.25f, 0.42f, 0.48f), 24);
            back.Pressed += () => Finish(-1);
            col.AddChild(back);
        }

        tree.Root.AddChild(layer);
        root.Modulate = new Color(1, 1, 1, 0);
        root.CreateTween().TweenProperty(root, "modulate:a", 1f, 0.2);
        return done.Task;
    }

    private static Control OptionButton(Option o, Color accent, Action onPress)
    {
        var b = new Button { CustomMinimumSize = new Vector2(820, 84), Disabled = !o.Enabled, ClipText = true };
        StyleButton(b, accent, 0);
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        box.OffsetLeft = 18; box.OffsetRight = -18; box.OffsetTop = 8; box.OffsetBottom = -8;
        box.AddThemeConstantOverride("separation", 2);
        box.AddChild(Rich($"[b]{Esc(o.Label)}[/b]", 25, o.Enabled ? new Color(1f, 0.92f, 0.7f) : Muted, true));
        if (!string.IsNullOrWhiteSpace(o.Description)) box.AddChild(Rich(Bb(o.Description), 19, o.Enabled ? Text : Muted, false));
        b.AddChild(box);
        b.Pressed += onPress;
        // grow to the text
        b.Ready += () => b.CustomMinimumSize = new Vector2(820, Math.Max(84, box.GetCombinedMinimumSize().Y + 16));
        return b;
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
