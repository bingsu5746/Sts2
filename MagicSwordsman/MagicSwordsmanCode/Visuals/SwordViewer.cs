using System.Text.RegularExpressions;
using Godot;
using HarmonyLib;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// User request 2026-10-15 "만검총 유물을 누르면 마검 골랐을 때 뜨는 그 검 설명 창을 볼 수 있게": clicking 만검총 in the top
/// bar opens a viewer of the owned swords in the style of <see cref="SwordAcquiredPopup"/> (same panel, art frame and
/// fonts): art, name, stage name and level, the "현재 검일 때" effect, card style, 친밀도 and 【조합】 lines. Arrows / the
/// left-right keys / the mouse wheel page through the swords (간장·막야 = one page); a click outside the arrows, Esc or
/// the back key closes it.
///
/// Hook: the game opens its relic inspect screen from the private NRelicInventory.OnRelicClicked(RelicModel) (connected
/// to each NRelicInventoryHolder's Released signal). A Harmony prefix opens this viewer instead for 만검총 when at least
/// one sword is owned; any other relic (or an empty tomb) keeps the game's inspect screen. While open, the viewer
/// registers itself with NHotkeyManager as a blocking screen (like NInspectRelicScreen), so game hotkeys do nothing.
/// Presentation only; every failure is swallowed (the click then falls back to the inspect screen).
/// UNVERIFIED in game: layout at non-1080p resolutions; controller navigation.
/// </summary>
public static class SwordViewer
{
    private const string LocTable = "events";
    private const string LocPrefix = "MAGICSWORDSMAN-SWORD_VIEWER";
    private static readonly Color Panel = new(0.07f, 0.075f, 0.095f, 0.97f);
    private static readonly Color Muted = new(0.72f, 0.72f, 0.76f);

    private static CanvasLayer? _open;

    [HarmonyPatch(typeof(NRelicInventory), "OnRelicClicked")]
    internal static class RelicClickPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(RelicModel model)
        {
            try
            {
                if (model is Mangeomchong { IsMutable: true } tomb && tomb.OwnedSwords.Count > 0 && Open(tomb))
                    return false;
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"[SwordViewer] click: {e.Message}");
            }

            return true;
        }
    }

    /// <summary>Opens the viewer for <paramref name="tomb"/>'s swords. False when it could not be shown.</summary>
    public static bool Open(Mangeomchong tomb)
    {
        try
        {
            if (_open != null && GodotObject.IsInstanceValid(_open)) return true;
            var player = tomb.Owner;
            if (player == null || !LocalContext.IsMe(player)) return false;
            var swords = tomb.OwnedSwords.Select(SwordRegistry.GroupLeader).Distinct().ToList();
            if (swords.Count == 0) return false;
            var start = SwordCombat.CurrentSword(player) is { } cur ? Math.Max(0, swords.IndexOf(SwordRegistry.GroupLeader(cur))) : 0;
            return Build(player, swords, start);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordViewer] open: {e.Message}");
            return false;
        }
    }

    private static bool Build(MegaCrit.Sts2.Core.Entities.Players.Player player, List<SwordId> swords, int index)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return false;

        var layer = new CanvasLayer { Layer = 120, Name = "MagicSwordViewer" };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop, FocusMode = Control.FocusModeEnum.All };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.72f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var outer = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        outer.AddThemeConstantOverride("separation", 18);
        center.AddChild(outer);

        var left = Arrow("◀");
        var right = Arrow("▶");
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(1060, 0) };
        outer.AddChild(left);
        outer.AddChild(panel);
        outer.AddChild(right);
        left.Visible = right.Visible = swords.Count > 1;

        tree.Root.AddChild(layer);
        _open = layer;

        void Show(int i)
        {
            index = (i % swords.Count + swords.Count) % swords.Count;
            foreach (var child in panel.GetChildren()) child.QueueFree();
            FillPage(panel, player, swords[index], index, swords.Count);
        }

        void Page(int delta)
        {
            if (swords.Count > 1 && GodotObject.IsInstanceValid(layer)) Show(index + delta);
        }

        var closing = false;
        Action? closeAction = null, prevAction = null, nextAction = null;
        void Close()
        {
            if (closing || !GodotObject.IsInstanceValid(layer)) return;
            closing = true;
            try
            {
                if (NHotkeyManager.Instance is { } hk)
                {
                    hk.RemoveHotkeyPressedBinding(MegaInput.cancel, closeAction!);
                    hk.RemoveHotkeyPressedBinding(MegaInput.pauseAndBack, closeAction!);
                    hk.RemoveHotkeyPressedBinding(MegaInput.left, prevAction!);
                    hk.RemoveHotkeyPressedBinding(MegaInput.right, nextAction!);
                    hk.RemoveBlockingScreen(layer);
                }
            }
            catch (Exception) { /* presentation only */ }

            if (_open == layer) _open = null;
            try
            {
                if (!root.IsInsideTree() || layer.IsQueuedForDeletion()) return;
                var o = root.CreateTween();
                o.TweenProperty(root, "modulate:a", 0f, 0.15);
                o.TweenCallback(Callable.From(layer.QueueFree));
            }
            catch (Exception)
            {
                layer.QueueFree();
            }
        }

        closeAction = Close;
        prevAction = () => Page(-1);
        nextAction = () => Page(1);
        left.Pressed += () => Page(-1);
        right.Pressed += () => Page(1);

        var hotkeys = false;
        try
        {
            if (NHotkeyManager.Instance is { } hk)
            {
                hk.AddBlockingScreen(layer);
                hk.PushHotkeyPressedBinding(MegaInput.cancel, closeAction);
                hk.PushHotkeyPressedBinding(MegaInput.pauseAndBack, closeAction);
                hk.PushHotkeyPressedBinding(MegaInput.left, prevAction);
                hk.PushHotkeyPressedBinding(MegaInput.right, nextAction);
                hotkeys = true;
            }
        }
        catch (Exception) { /* falls back to the focused root's key input below */ }

        layer.TreeExiting += () =>
        {
            if (_open == layer) _open = null;
            if (!closing) Close();
        };

        var openedAt = Time.GetTicksMsec();
        root.GuiInput += ev =>
        {
            if (Time.GetTicksMsec() - openedAt < 250) return; // the click that opened it
            switch (ev)
            {
                case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                    Page(-1);
                    break;
                case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                    Page(1);
                    break;
                case InputEventMouseButton { Pressed: true }:
                    Close();
                    break;
                case InputEventKey { Pressed: true, Echo: false } key when !hotkeys:
                    if (key.Keycode == Key.Left) Page(-1);
                    else if (key.Keycode == Key.Right) Page(1);
                    else Close();
                    break;
                default:
                    return;
            }

            root.AcceptEvent();
        };

        Show(index);
        root.Modulate = new Color(1, 1, 1, 0);
        root.CreateTween().TweenProperty(root, "modulate:a", 1f, 0.2);
        if (!hotkeys) root.CallDeferred(Control.MethodName.GrabFocus);
        return true;
    }

    private static Button Arrow(string glyph)
    {
        var b = new Button
        {
            Text = glyph, Flat = true, FocusMode = Control.FocusModeEnum.None, MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(64, 96), MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        b.AddThemeFontSizeOverride("font_size", 48);
        b.AddThemeColorOverride("font_color", new Color(0.98f, 0.82f, 0.4f));
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        b.AddThemeColorOverride("font_pressed_color", new Color(0.8f, 0.65f, 0.3f));
        b.AddThemeColorOverride("font_outline_color", Colors.Black);
        b.AddThemeConstantOverride("outline_size", 8);
        return b;
    }

    private static void FillPage(PanelContainer panel, MegaCrit.Sts2.Core.Entities.Players.Player player, SwordId sword,
        int index, int count)
    {
        var accent = SwordVisuals.ColorOf(sword == SwordId.Ganjiang ? SwordId.Moye : sword);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Panel, BorderColor = accent, BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3,
            BorderWidthBottom = 3, CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14, ShadowColor = new Color(accent, 0.35f), ShadowSize = 24,
            ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 24, ContentMarginBottom = 24,
        });

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 28);
        panel.AddChild(row);

        if (ArtPath(sword) is { } artPath)
        {
            var isSprite = artPath.Contains("/images/swords/");
            var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = isSprite ? new Color(accent.Darkened(0.85f), 1f) : Colors.Black,
                BorderColor = new Color(accent, 0.8f), BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2,
                BorderWidthBottom = 2, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8, ShadowColor = new Color(accent, isSprite ? 0.25f : 0f), ShadowSize = isSprite ? 40 : 0,
            });
            frame.AddChild(new TextureRect
            {
                Texture = isSprite ? SpriteBounds.Trimmed(GD.Load<Texture2D>(artPath)) : GD.Load<Texture2D>(artPath), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = isSprite ? TextureRect.StretchModeEnum.KeepAspectCentered : TextureRect.StretchModeEnum.KeepAspectCovered,
                CustomMinimumSize = isSprite ? new Vector2(300, 460) : new Vector2(420, 320),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            row.AddChild(frame);
        }

        var col = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 8);
        row.AddChild(col);

        var header = Loc("HEADER");
        if (count > 1) header += $"   {index + 1} / {count}";
        col.AddChild(MakeLabel(header, 18, accent));
        col.AddChild(MakeLabel(SwordLore.NameText(sword), 46, Colors.White, bold: true));
        // one-line role under the name (user feedback 2026-10-17)
        var role = SwordRole.Text(sword, pair: true);
        if (role.Length > 0) col.AddChild(MakeLabel(role, 24, accent.Lightened(0.3f), bold: true));

        var level = SwordCombat.LevelOf(player, sword);
        var levelText = new LocString(LocTable, $"{LocPrefix}.LEVEL");
        levelText.Add("Level", level);
        levelText.Add("Max", SwordRegistry.MaxLevel);
        var stage = $"{Text(SwordLore.StageName(sword, level))} — {Text(levelText)}";
        if (SwordCombat.CurrentSword(player) is { } cur && SwordRegistry.GroupLeader(cur) == sword)
            stage += $"   ({Loc("CURRENT")})";
        col.AddChild(MakeLabel(stage, 22, accent.Lightened(0.25f)));
        col.AddChild(MakeLabel(Text(SwordLore.Line(sword, "TAGLINE")), 20, Muted));
        col.AddChild(new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore });

        // the effect and cost with this sword's live numbers (SwordEffectText) instead of the old number-less summary
        AddEntry(col, Text(SwordLore.Generic("POPUP_EFFECT")), SwordEffectText.Effect(sword, player, level, withPartner: true), accent);
        AddEntry(col, Text(SwordLore.Generic("POPUP_COST")), SwordEffectText.Cost(sword, player, level), new Color(0.9f, 0.4f, 0.4f));
        AddEntry(col, Text(SwordLore.Generic("POPUP_STYLE")), Text(SwordLore.Line(sword, "STYLE")), accent);
        var extra = "";
        try { extra = Dialogue.SwordAffinity.TipLine(player, sword); } catch (Exception) { /* optional line */ }
        try { extra += Cards.Union.UnionCatalog.SwordTipLine(player, sword); } catch (Exception) { /* optional line */ }
        extra = StripBbCode(extra).Trim();
        if (extra.Length > 0) col.AddChild(MakeLabel(extra, 19, new Color(0.85f, 0.82f, 0.75f)));

        var hint = MakeLabel(Loc(count > 1 ? "HINT" : "HINT_SINGLE"), 16, new Color(Muted, 0.7f));
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        col.AddChild(hint);
    }

    private static void AddEntry(VBoxContainer col, string title, string body, Color titleColor)
    {
        if (string.IsNullOrWhiteSpace(body)) return;
        col.AddChild(MakeLabel(title, 17, titleColor, bold: true));
        col.AddChild(MakeLabel(StripBbCode(body), 21, Colors.White));
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
        if (GameFont(bold) is { } font) label.AddThemeFontOverride("font", font);
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

    private static string StripBbCode(string s) => Regex.Replace(s ?? "", "\\[/?[^\\]]+\\]", "");

    private static string Loc(string part) => Text(new LocString(LocTable, $"{LocPrefix}.{part}"));

    private static string Text(LocString loc)
    {
        try { return loc.GetFormattedText(); }
        catch (Exception) { return ""; }
    }

    /// <summary>Same art choice as SwordAcquiredPopup: the sword sprite, else the token's big card art.</summary>
    private static string? ArtPath(SwordId sword)
    {
        var spritePath = $"{MainFile.ResPath}/images/swords/{sword.ToString().ToLowerInvariant()}.png";
        if (sword != SwordId.Ganjiang && ResourceLoader.Exists(spritePath)) return spritePath;
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
