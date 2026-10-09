using Godot;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Presentation only: "검이 완성되었다" card shown when a sword first reaches level 5 in a run (<see cref="SwordMastery"/>).
/// Same look as <see cref="SwordAcquiredPopup"/> (sword art on the left, text on the right, framed in the sword's
/// color); click or press a key to close. Never blocks game logic and swallows every failure.
/// Texts: events table, MAGICSWORDSMAN-SWORD_MASTERY.POPUP_* and MAGICSWORDSMAN-SWORD_LORE.&lt;SWORD&gt;.{NAME,STAGE5,MASTERY}.
/// UNVERIFIED in game: layout at non-1080p resolutions; how it stacks with the card-added preview animation.
/// </summary>
public static class SwordMasteryPopup
{
    private static readonly Color Panel = new(0.07f, 0.075f, 0.095f, 0.97f);
    private static readonly Color Muted = new(0.72f, 0.72f, 0.76f);
    private static readonly Color Gold = new(0.98f, 0.82f, 0.4f);

    public static void Show(SwordId sword, CardModel legendCard)
    {
        try { ShowInner(SwordRegistry.GroupLeader(sword), legendCard); }
        catch (Exception e) { MainFile.Logger.Warn($"[SwordMasteryPopup] failed: {e.Message}"); }
    }

    private static void ShowInner(SwordId sword, CardModel legendCard)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        var accent = SwordVisuals.ColorOf(sword == SwordId.Ganjiang ? SwordId.Moye : sword);

        var layer = new CanvasLayer { Layer = 121, Name = "MagicSwordMasteryPopup" };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.72f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(1000, 0) };
        panel.AddThemeStyleboxOverride("panel", Box(Panel, Gold, 3, 14, 28, new Color(accent, 0.45f), 28));
        center.AddChild(panel);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 28);
        panel.AddChild(row);

        // ---- art: the sword itself (the pair: its crossed-swords picture)
        var artPath = ArtPath(sword);
        if (artPath != null)
        {
            var isSprite = artPath.Contains("/images/swords/");
            var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            frame.AddThemeStyleboxOverride("panel",
                Box(new Color(accent.Darkened(0.85f), 1f), new Color(Gold, 0.8f), 2, 8, 0, new Color(accent, 0.3f), 40));
            frame.AddChild(new TextureRect
            {
                Texture = GD.Load<Texture2D>(artPath), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = isSprite
                    ? TextureRect.StretchModeEnum.KeepAspectCentered
                    : TextureRect.StretchModeEnum.KeepAspectCovered,
                CustomMinimumSize = isSprite ? new Vector2(280, 440) : new Vector2(400, 304),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            row.AddChild(frame);
        }

        // ---- text
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 8);
        row.AddChild(col);

        col.AddChild(MakeLabel(Text(SwordMastery.Text("POPUP_HEADER")), 18, Gold));
        col.AddChild(MakeLabel(SwordLore.NameText(sword), 46, Colors.White, bold: true));
        var level = SwordMastery.Text("POPUP_LEVEL");
        level.Add("Level", SwordRegistry.MaxLevel);
        col.AddChild(MakeLabel($"{Text(level)} — {Text(SwordLore.Line(sword, "STAGE5"))}", 22, accent));
        col.AddChild(MakeLabel(Text(SwordLore.Line(sword, "MASTERY")), 19, new Color(0.85f, 0.82f, 0.75f)));
        col.AddChild(new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore });
        col.AddChild(MakeLabel(Text(SwordMastery.Text("POPUP_REWARD")), 17, Gold, bold: true));
        col.AddChild(MakeLabel(SafeTitle(legendCard), 30, Colors.White, bold: true));
        col.AddChild(MakeLabel(Text(SwordMastery.Text("POPUP_REWARD_NOTE")), 18, Muted));
        var hint = MakeLabel(Text(SwordLore.Generic("POPUP_CONTINUE")), 16, new Color(Muted, 0.7f));
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        col.AddChild(hint);

        tree.Root.AddChild(layer);

        root.Modulate = new Color(1, 1, 1, 0);
        root.CreateTween().TweenProperty(root, "modulate:a", 1f, 0.3);

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
            // ignore the click that caused the upgrade itself
            if (Time.GetTicksMsec() - openedAt < 400) return;
            if (ev is InputEventMouseButton { Pressed: true } or InputEventKey { Pressed: true }) Close();
        };
    }

    private static StyleBoxFlat Box(Color bg, Color border, int borderWidth, int radius, int margin, Color shadow,
        int shadowSize) => new()
    {
        BgColor = bg, BorderColor = border, BorderWidthLeft = borderWidth, BorderWidthRight = borderWidth,
        BorderWidthTop = borderWidth, BorderWidthBottom = borderWidth, CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ShadowColor = shadow, ShadowSize = shadowSize, ContentMarginLeft = margin, ContentMarginRight = margin,
        ContentMarginTop = margin, ContentMarginBottom = margin,
    };

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
        try
        {
            var lang = LocManager.Instance?.Language ?? "eng";
            var font = FontManager.GetSubstituteFont(lang, bold ? FontType.Bold : FontType.Regular)
                       ?? NGame.Instance?.GetThemeDefaultFont();
            if (font != null) label.AddThemeFontOverride("font", font);
        }
        catch (Exception)
        {
            // default theme font
        }

        return label;
    }

    private static string SafeTitle(CardModel card)
    {
        try { return card.Title; }
        catch (Exception) { return card.Id.Entry; }
    }

    private static string Text(LocString loc)
    {
        try { return loc.GetFormattedText(); }
        catch (Exception) { return ""; }
    }

    private static string? ArtPath(SwordId sword)
    {
        if (sword == SwordId.Ganjiang)
        {
            var pair = $"{MainFile.ResPath}/images/card_portraits/big/ganjiang_moye_pair.jpg";
            return ResourceLoader.Exists(pair) ? pair : null;
        }

        var sprite = $"{MainFile.ResPath}/images/swords/{sword.ToString().ToLowerInvariant()}.png";
        return ResourceLoader.Exists(sprite) ? sprite : null;
    }
}
