using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// First-time guidance (user feedback 2026-10-17 "처음에 캐릭터를 하는 사람들은 이해가 잘 안 갈 것 같아"): at the start of
/// the FIRST combat of an Ensifer run, a short non-blocking hint card at the top of the screen explains the core rule
/// (a sword card summons its sword and makes it the current sword; the current sword gives a lasting effect; where to
/// look). Click it to close; "다시 보지 않기" turns it off for good; it also fades out by itself and goes away with the
/// combat room. At most once per run.
///
/// Why not the game's own tutorial popups: the game's FTUEs (MegaCrit.Sts2.Core.Nodes.Ftue, e.g. NPowerCardFtue) are
/// blocking modal scenes with fixed loc keys (NModalContainer + NHotkeyManager blocking screen), so they cannot carry
/// this text without patching their scenes. This hint reuses only the game's tutorial SWITCH: it is not shown when the
/// player turned tutorials off (SaveManager.Progress.EnableFtues — the flag the game's SeenFtue checks first).
///
/// Persistence (local, never in the run or profile save, so nothing synced in multiplayer changes):
/// user://magicswordsman_hints.cfg — [runs] &lt;run seed&gt;=true for runs that already showed it (last 40 kept) and
/// [hint] never=true. Local player only. Presentation only; every failure is swallowed.
/// Texts: events table MAGICSWORDSMAN-SWORD_HINT.* (loc_fragments/clarity.&lt;lang&gt;.events.json).
/// UNVERIFIED in game: placement under the top bar at non-1080p resolutions.
/// </summary>
public static class SwordRulesHint
{
    private const string CfgPath = "user://magicswordsman_hints.cfg";
    private const string Prefix = "MAGICSWORDSMAN-SWORD_HINT";
    private const int KeepRuns = 40;
    private const double AutoCloseSeconds = 40;
    private static readonly HashSet<string> ShownThisSession = new();

    /// <summary>Called at the start of every player turn of the owner (Mangeomchong); acts only on turn 1.</summary>
    public static void OnTurnStart(Player player)
    {
        try
        {
            if (!LocalContext.IsMe(player) || player.PlayerCombatState is not { TurnNumber: 1 }) return;
            if (SaveManager.Instance?.Progress is { EnableFtues: false }) return;
            var seed = Regex.Replace(player.RunState.Rng.StringSeed ?? "", "[^A-Za-z0-9]", "_");
            if (seed.Length == 0) seed = "unknown";
            if (!ShownThisSession.Add(seed)) return;

            var cfg = new ConfigFile();
            // Missing file = empty config. Any other load error: leave the file alone (saving would overwrite it).
            var err = cfg.Load(CfgPath);
            if (err != Error.Ok && err != Error.FileNotFound)
            {
                MainFile.Logger.Warn($"[SwordRulesHint] could not read {CfgPath}: {err}");
                return;
            }
            if ((bool)cfg.GetValue("hint", "never", false)) return;
            if (cfg.HasSectionKey("runs", seed)) return;
            cfg.SetValue("runs", seed, true);
            var keys = cfg.HasSection("runs") ? cfg.GetSectionKeys("runs") : [];
            for (var i = 0; i < keys.Length - KeepRuns; i++) cfg.EraseSectionKey("runs", keys[i]);
            cfg.Save(CfgPath);

            Show();
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordRulesHint] {e.Message}");
        }
    }

    private static void NeverAgain()
    {
        try
        {
            var cfg = new ConfigFile();
            var err = cfg.Load(CfgPath);
            if (err != Error.Ok && err != Error.FileNotFound)
            {
                MainFile.Logger.Warn($"[SwordRulesHint] could not read {CfgPath}: {err}");
                return;
            }
            cfg.SetValue("hint", "never", true);
            cfg.Save(CfgPath);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordRulesHint] save: {e.Message}");
        }
    }

    private static void Show()
    {
        var room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room)) return;
        var accent = new Color(0.86f, 0.72f, 0.38f);

        // child of the combat room: freed with it. The full-screen root ignores the mouse, so only the card itself
        // takes clicks and the fight goes on underneath (non-blocking).
        var layer = new CanvasLayer { Layer = 110, Name = "MagicSwordRulesHint" };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(700, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.075f, 0.095f, 0.95f), BorderColor = accent, BorderWidthLeft = 2,
            BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2, CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = 16,
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 14, ContentMarginBottom = 12,
        });
        // top centre, below the top bar and the relic row: a full-width row that centres the card whatever its size
        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        top.OffsetTop = 170;
        root.AddChild(top);
        top.AddChild(panel);

        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 6);
        panel.AddChild(col);
        col.AddChild(MakeLabel(Text("HEADER"), 18, accent, bold: true));
        col.AddChild(MakeLabel(Text("TITLE"), 26, Colors.White, bold: true));
        col.AddChild(MakeLabel(Text("BODY"), 19, new Color(0.92f, 0.92f, 0.94f)));

        var footer = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        footer.AddThemeConstantOverride("separation", 18);
        col.AddChild(footer);
        footer.AddChild(MakeLabel(Text("CLOSE"), 16, new Color(0.72f, 0.72f, 0.76f, 0.85f)));
        var never = new Button { Text = Text("NEVER"), Flat = true, FocusMode = Control.FocusModeEnum.None };
        never.AddThemeFontSizeOverride("font_size", 16);
        never.AddThemeColorOverride("font_color", accent);
        never.AddThemeColorOverride("font_hover_color", accent.Lightened(0.3f));
        if (GameFont(false) is { } f) never.AddThemeFontOverride("font", f);
        footer.AddChild(never);

        room.AddChild(layer);

        root.Modulate = new Color(1, 1, 1, 0);
        root.CreateTween().TweenProperty(root, "modulate:a", 1f, 0.3);

        var closing = false;
        void Close()
        {
            if (closing || !GodotObject.IsInstanceValid(layer)) return;
            closing = true;
            var o = root.CreateTween();
            o.TweenProperty(root, "modulate:a", 0f, 0.25);
            o.TweenCallback(Callable.From(layer.QueueFree));
        }

        never.Pressed += () =>
        {
            NeverAgain();
            Close();
        };
        panel.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true }) Close();
        };
        var timer = new Godot.Timer { WaitTime = AutoCloseSeconds, OneShot = true, Autostart = true };
        timer.Timeout += Close;
        layer.AddChild(timer);
    }

    private static string Text(string part)
    {
        try { return new LocString("events", $"{Prefix}.{part}").GetFormattedText(); }
        catch (Exception) { return ""; }
    }

    private static Label MakeLabel(string text, int size, Color color, bool bold = false)
    {
        var label = new Label
        {
            Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0, 0),
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
            return FontManager.GetSubstituteFont(lang, bold ? FontType.Bold : FontType.Regular) ?? NGame.Instance?.GetThemeDefaultFont();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
