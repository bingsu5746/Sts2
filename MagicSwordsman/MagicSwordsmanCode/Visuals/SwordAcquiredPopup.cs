using System.Text.RegularExpressions;
using Godot;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Presentation only: a short "new sword" card shown the first time a sword is acquired in a run
/// (user request 2026-10-08). Art on the left (the sword token's card art), name / tagline / effect / cost / card style
/// on the right, framed in the sword's color. Click anywhere (or press a key) to close. Never blocks game logic:
/// it is a plain overlay added to the scene root, and every failure is swallowed.
/// Texts: events table, MAGICSWORDSMAN-SWORD_LORE.&lt;SWORD&gt;.{NAME,TAGLINE,EFFECT,COST,STYLE} and .POPUP_*.
/// UNVERIFIED in game: layout at non-1080p resolutions, font substitution for Korean.
/// </summary>
public static class SwordAcquiredPopup
{
    private static readonly Color Panel = new(0.07f, 0.075f, 0.095f, 0.97f);
    private static readonly Color Muted = new(0.72f, 0.72f, 0.76f);

    public static void Show(SwordId sword)
    {
        try { ShowInner(SwordRegistry.GroupLeader(sword)); }
        catch (Exception e) { MainFile.Logger.Warn($"[SwordAcquiredPopup] failed: {e.Message}"); }
    }

    private static void ShowInner(SwordId sword)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        var accent = SwordVisuals.ColorOf(sword == SwordId.Ganjiang ? SwordId.Moye : sword);

        var layer = new CanvasLayer { Layer = 120, Name = "MagicSwordAcquiredPopup" };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.72f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        // ---- framed panel
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Panel, BorderColor = accent, BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3,
            BorderWidthBottom = 3, CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14, ShadowColor = new Color(accent, 0.35f), ShadowSize = 24,
            ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 24, ContentMarginBottom = 24,
        });
        panel.CustomMinimumSize = new Vector2(1060, 0);
        // A full-screen CenterContainer keeps the panel centred whatever its final size (anchoring the panel itself at
        // the centre grew it towards the bottom-right and cut it off — bug report 2026-10-08).
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        center.AddChild(panel);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 28);
        panel.AddChild(row);

        // ---- art
        var artPath = ArtPath(sword);
        if (artPath != null)
        {
            var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = Colors.Black, BorderColor = new Color(accent, 0.8f), BorderWidthLeft = 2, BorderWidthRight = 2,
                BorderWidthTop = 2, BorderWidthBottom = 2, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            });
            var isSprite = artPath.Contains("/images/swords/");
            if (isSprite)
            {
                // the sword itself (same art as the floating combat sword) on a soft glow in its color
                frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    BgColor = new Color(accent.Darkened(0.85f), 1f), BorderColor = new Color(accent, 0.8f),
                    BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
                    CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8,
                    CornerRadiusBottomRight = 8, ShadowColor = new Color(accent, 0.25f), ShadowSize = 40,
                });
            }

            frame.AddChild(new TextureRect
            {
                Texture = GD.Load<Texture2D>(artPath), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = isSprite
                    ? TextureRect.StretchModeEnum.KeepAspectCentered
                    : TextureRect.StretchModeEnum.KeepAspectCovered,
                CustomMinimumSize = isSprite ? new Vector2(300, 460) : new Vector2(420, 320),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            row.AddChild(frame);
        }

        // ---- text column
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 8);
        row.AddChild(col);

        col.AddChild(MakeLabel(Lore("POPUP_HEADER"), 18, accent));
        col.AddChild(MakeLabel(SwordLore.NameText(sword), 46, Colors.White, bold: true));
        col.AddChild(MakeLabel(Text(SwordLore.Line(sword, "TAGLINE")), 20, Muted));
        var origin = MakeLabel(Text(SwordLore.Line(sword, "ORIGIN")), 18, new Color(0.85f, 0.82f, 0.75f));
        col.AddChild(origin);
        col.AddChild(new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore });
        AddEntry(col, Lore("POPUP_EFFECT"), Text(SwordLore.Line(sword, "EFFECT")), accent);
        AddEntry(col, Lore("POPUP_COST"), Text(SwordLore.Line(sword, "COST")), new Color(0.9f, 0.4f, 0.4f));
        AddEntry(col, Lore("POPUP_STYLE"), Text(SwordLore.Line(sword, "STYLE")), accent);
        AddEntry(col, Lore("POPUP_CARDS"), StarterCardNames(sword), accent);
        var hint = MakeLabel(Lore("POPUP_CONTINUE"), 16, new Color(Muted, 0.7f));
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        col.AddChild(hint);

        tree.Root.AddChild(layer);

        // appear: fade in (no scale tween: the container owns the panel's transform)
        root.Modulate = new Color(1, 1, 1, 0);
        var t = root.CreateTween();
        t.TweenProperty(root, "modulate:a", 1f, 0.25);

        var closing = false;
        void Close()
        {
            if (closing || !GodotObject.IsInstanceValid(layer)) return;
            closing = true;
            var o = root.CreateTween();
            o.TweenProperty(root, "modulate:a", 0f, 0.18);
            o.TweenCallback(Callable.From(layer.QueueFree));
        }

        var openedAt = Time.GetTicksMsec();
        root.GuiInput += ev =>
        {
            // ignore the click that triggered the acquisition itself
            if (Time.GetTicksMsec() - openedAt < 350) return;
            if (ev is InputEventMouseButton { Pressed: true } or InputEventKey { Pressed: true }) Close();
        };
    }

    private static void AddEntry(VBoxContainer col, string title, string body, Color titleColor)
    {
        if (string.IsNullOrWhiteSpace(body)) return;
        col.AddChild(MakeLabel(title, 17, titleColor, bold: true));
        col.AddChild(MakeLabel(body, 21, Colors.White));
    }

    private static Label MakeLabel(string text, int size, Color color, bool bold = false)
    {
        var label = new Label
        {
            Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", bold ? 6 : 3);
        var font = GameFont(bold);
        if (font != null) label.AddThemeFontOverride("font", font);
        return label;
    }

    private static Font? GameFont(bool bold)
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

    /// <summary>The cards granted on the first acquisition (SwordBehavior.StarterCards of the sword and its partner).</summary>
    private static string StarterCardNames(SwordId sword)
    {
        try
        {
            var names = SwordRegistry.WithPartners(sword)
                .SelectMany(s => SwordRegistry.Get(s).StarterCards)
                .Select(c => c.Title);
            return string.Join(" · ", names);
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static string Lore(string part) => Text(SwordLore.Generic(part));

    private static string Text(LocString loc)
    {
        try { return loc.GetFormattedText(); }
        catch (Exception) { return ""; }
    }

    /// <summary>The sword sprite if present, else the token's big card art (the pair uses 한 쌍).</summary>
    private static string? ArtPath(SwordId sword)
    {
        // Preferred: the sword itself (images/swords/<sword>.png, also used for the floating combat swords).
        // (간장·막야 come as a pair: their token art shows both swords crossed, so use that instead.)
        var spritePath = $"{MainFile.ResPath}/images/swords/{sword.ToString().ToLowerInvariant()}.png";
        if (sword != SwordId.Ganjiang && ResourceLoader.Exists(spritePath)) return spritePath;

        // the pair has its own crossed-swords picture; the Ganjiang token itself shows Ganjiang alone (swap screens)
        var id = sword == SwordId.Ganjiang
            ? "ganjiang_moye_pair"
            : Regex.Replace(sword.ToString(), "(?<!^)([A-Z])", "_$1").ToLowerInvariant() + "_token";
        foreach (var ext in new[] { ".png", ".jpg" })
        {
            var path = $"{MainFile.ResPath}/images/card_portraits/big/{id}{ext}";
            if (ResourceLoader.Exists(path)) return path;
        }

        return null;
    }
}
