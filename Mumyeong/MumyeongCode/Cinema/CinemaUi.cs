using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;

namespace Mumyeong.MumyeongCode.Cinema;

/// <summary>
/// Small UI helpers for our own screens (the theater cinema, the character-select info panel). Plain Godot nodes only:
/// a mod cannot register its own node classes here, so behaviour comes from signals, tweens and SceneTree.ProcessFrame.
/// Fonts: the game's substitute font for the current language (same approach as 엔시페르's SwordAcquiredPopup), so
/// Korean renders.
/// </summary>
public static class CinemaUi
{
    public static readonly Color Gold = new("efc851");
    public static readonly Color Ink = new(0.04f, 0.04f, 0.06f);
    public static readonly Color Paper = new(0.93f, 0.92f, 0.88f);
    public static readonly Color Muted = new(0.70f, 0.70f, 0.74f);

    public static Font? GameFont(bool bold = false)
    {
        try
        {
            var lang = LocManager.Instance?.Language ?? "eng";
            return FontManager.GetSubstituteFont(lang, bold ? FontType.Bold : FontType.Regular)
                   ?? NGame.Instance?.GetThemeDefaultFont();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Our localization uses the game's colour tags ([gold] ...); a plain RichTextLabel needs real BBCode.</summary>
    public static string ToBbcode(string text)
    {
        text = Regex.Replace(text, @"\[(/?)(gold|aqua|red|green|purple|blue)\]", m =>
        {
            if (m.Groups[1].Value == "/") return "[/color]";
            return m.Groups[2].Value switch
            {
                "gold" => "[color=#efc851]",
                "aqua" => "[color=#7fd8e0]",
                "red" => "[color=#ff7a6b]",
                "green" => "[color=#8fdc8a]",
                "purple" => "[color=#c79bff]",
                _ => "[color=#8fb6ff]",
            };
        });
        text = text.Replace("[sine]", "[wave amp=20 freq=3]").Replace("[/sine]", "[/wave]");
        return text;
    }

    public static string Plain(string text) => Regex.Replace(text, @"\[/?[^\]]+\]", "");

    public static RichTextLabel Rich(int size, bool center = true)
    {
        var label = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        if (GameFont() is { } f) label.AddThemeFontOverride("normal_font", f);
        if (GameFont(true) is { } b) label.AddThemeFontOverride("bold_font", b);
        label.AddThemeFontSizeOverride("normal_font_size", size);
        label.AddThemeFontSizeOverride("bold_font_size", size);
        label.AddThemeColorOverride("default_color", Paper);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", Math.Max(4, size / 5));
        label.SetMeta("center", center);
        return label;
    }

    public static void SetRich(RichTextLabel label, string text)
    {
        var bb = ToBbcode(text);
        label.Text = label.GetMeta("center", true).AsBool() ? $"[center]{bb}[/center]" : bb;
    }

    public static Label Label(string text, int size, Color color, bool bold = false)
    {
        var label = new Label
        {
            Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        if (GameFont(bold) is { } f) label.AddThemeFontOverride("font", f);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", Math.Max(3, size / 6));
        return label;
    }

    /// <summary>A film-frame style choice button: dark glass, thin gold edge, brighter on hover.</summary>
    public static Button ChoiceButton(string text, int size = 24, float width = 760f)
    {
        var button = new Button
        {
            Text = text, CustomMinimumSize = new Vector2(width, 64), FocusMode = Control.FocusModeEnum.All,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        if (GameFont() is { } f) button.AddThemeFontOverride("font", f);
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeColorOverride("font_color", Paper);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Gold);
        button.AddThemeColorOverride("font_focus_color", Colors.White);
        button.AddThemeColorOverride("font_outline_color", Colors.Black);
        button.AddThemeConstantOverride("outline_size", 4);
        button.AddThemeStyleboxOverride("normal", Box(new Color(0.03f, 0.03f, 0.05f, 0.78f), new Color(Gold, 0.35f)));
        button.AddThemeStyleboxOverride("hover", Box(new Color(0.10f, 0.09f, 0.07f, 0.9f), Gold));
        button.AddThemeStyleboxOverride("focus", Box(new Color(0.10f, 0.09f, 0.07f, 0.9f), Gold));
        button.AddThemeStyleboxOverride("pressed", Box(new Color(0.20f, 0.16f, 0.08f, 0.95f), Gold));
        button.AddThemeStyleboxOverride("disabled", Box(new Color(0.03f, 0.03f, 0.05f, 0.5f), new Color(Gold, 0.15f)));
        return button;
    }

    public static StyleBoxFlat Box(Color bg, Color border, int borderWidth = 2, int radius = 6)
    {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(radius);
        box.ContentMarginLeft = 28;
        box.ContentMarginRight = 28;
        box.ContentMarginTop = 12;
        box.ContentMarginBottom = 12;
        return box;
    }

    public static GradientTexture2D Radial(int size = 256)
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

    /// <summary>Vertical gradient from transparent (top) to <paramref name="bottom"/>.</summary>
    public static GradientTexture2D Fade(Color bottom)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(bottom, 0f));
        gradient.SetColor(1, bottom);
        return new GradientTexture2D
        {
            Gradient = gradient, Width = 4, Height = 256, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1),
        };
    }

    public static TextureRect Cover(Texture2D? texture) => new()
    {
        Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    public static T Full<T>(T control) where T : Control
    {
        control.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return control;
    }

    public static async Task Wait(double seconds)
    {
        if (seconds <= 0 || Engine.GetMainLoop() is not SceneTree tree) return;
        await tree.ToSignal(tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }

    public static LocString Loc(string key) => new("events", key);
}
