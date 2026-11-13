using Godot;
using HarmonyLib;
using Mumyeong.MumyeongCode.Character;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Relics;
using Mumyeong.MumyeongCode.Stages;
using Mumyeong.MumyeongCode.Visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace Mumyeong.MumyeongCode.Cinema;

/// <summary>
/// Character select (user request 2026-10-24, like the 치이카와 mod): while 무명 is selected, a button at the top right
/// opens a short explanation of the character's system, a few pages with little text each. Numbers come from the code
/// constants. Presentation only; failures are logged and swallowed.
/// Texts: events table, MUMYEONG-INFO.*.
/// </summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class CharSelectInfo
{
    private const string ButtonName = "MumyeongInfoButton";
    private const string PanelName = "MumyeongInfoPanel";
    private const int PageCount = 5;

    private static void Postfix(NCharacterSelectScreen __instance, CharacterModel characterModel)
    {
        try
        {
            var mine = characterModel is MumyeongCharacter;
            var button = __instance.GetNodeOrNull<Button>(ButtonName);
            if (button == null)
            {
                if (!mine) return;
                button = CinemaUi.ChoiceButton(Text("BUTTON"), 22, 0);
                button.Name = ButtonName;
                button.CustomMinimumSize = new Vector2(0, 54);
                button.AnchorLeft = 1;
                button.AnchorRight = 1;
                button.OffsetLeft = -300;
                button.OffsetRight = -40;
                button.OffsetTop = 96;
                button.OffsetBottom = 150;
                button.Pressed += () => Open(0);
                __instance.AddChild(button);
            }

            button.Visible = mine;
            if (mine)
            {
                button.Modulate = new Color(1, 1, 1, 0);
                button.CreateTween().TweenProperty(button, "modulate:a", 1f, 0.4).SetDelay(0.3);
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[CharSelectInfo] {e.Message}");
        }
    }

    private static string Text(string key) => CinemaUi.Loc($"MUMYEONG-INFO.{key}").GetFormattedText();

    private static string Body(int page)
    {
        var text = CinemaUi.Loc($"MUMYEONG-INFO.BODY.{page}");
        switch (page)
        {
            case 2:
                text.Add("Strength", SoulCore.GrudgeStrength);
                text.Add("Limit", SoulCore.GrudgeLimit);
                text.Add("HpLoss", SoulCore.GrudgeHpLoss);
                text.Add("Sync", StageRules.SyncMasteryResonance);
                text.Add("Focus", StageRules.FocusMasteryResonance);
                break;
            case 3:
                text.Add("Sync", StageRules.SyncMastery);
                text.Add("Focus", StageRules.FocusMastery);
                text.Add("SyncAct", StageRules.SyncActIndex + 1);
                text.Add("FocusAct", StageRules.FocusActIndex + 1);
                text.Add("DescentAct", StageRules.DescentActIndex + 1);
                break;
        }

        var body = text.GetFormattedText();
        if (page == 4)
            body += "\n\n" + string.Join("\n", FigureRegistry.All.Select(f =>
                $"[gold]{FigureRegistry.Name(f)}[/gold]  「{FigureRegistry.Shadow(f)}」"));
        return body;
    }

    private static void Open(int page)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        tree.Root.GetNodeOrNull(PanelName)?.QueueFree();

        var layer = new CanvasLayer { Layer = 120, Name = PanelName };
        var root = CinemaUi.Full(new Control { MouseFilter = Control.MouseFilterEnum.Stop });
        layer.AddChild(root);
        var dim = CinemaUi.Full(new ColorRect { Color = new Color(0, 0, 0, 0.78f), MouseFilter = Control.MouseFilterEnum.Ignore });
        root.AddChild(dim);

        var center = CinemaUi.Full(new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore });
        root.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(980, 600), MouseFilter = Control.MouseFilterEnum.Stop };
        var box = CinemaUi.Box(new Color(0.05f, 0.05f, 0.07f, 0.97f), new Color(CinemaUi.Gold, 0.6f), 2, 10);
        box.ContentMarginLeft = box.ContentMarginRight = 56;
        box.ContentMarginTop = box.ContentMarginBottom = 40;
        panel.AddThemeStyleboxOverride("panel", box);
        center.AddChild(panel);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 36);
        panel.AddChild(row);

        // the reaper on page 0, a spirit on the figure page, the hall otherwise
        var art = new TextureRect
        {
            CustomMinimumSize = new Vector2(260, 420), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = page switch
            {
                4 => SpiritVisuals.Texture(FigureRegistry.All[0], false),
                _ => ResourceLoader.Exists($"{MainFile.ResPath}/images/character/reaper.png")
                    ? GD.Load<Texture2D>($"{MainFile.ResPath}/images/character/reaper.png")
                    : null,
            },
        };
        row.AddChild(art);

        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 18);
        row.AddChild(col);
        var title = CinemaUi.Label(Text($"TITLE.{page}"), 40, CinemaUi.Gold, bold: true);
        title.HorizontalAlignment = HorizontalAlignment.Left;
        col.AddChild(title);
        var body = CinemaUi.Rich(24, center: false);
        body.CustomMinimumSize = new Vector2(600, 0);
        CinemaUi.SetRich(body, Body(page));
        col.AddChild(body);
        col.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });

        var nav = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        nav.AddThemeConstantOverride("separation", 12);
        col.AddChild(nav);
        nav.AddChild(CinemaUi.Label($"{page + 1} / {PageCount}", 20, CinemaUi.Muted));
        if (page > 0) nav.AddChild(NavButton(Text("PREV"), () => Open(page - 1)));
        if (page + 1 < PageCount) nav.AddChild(NavButton(Text("NEXT"), () => Open(page + 1)));
        nav.AddChild(NavButton(Text("CLOSE"), () => layer.QueueFree()));

        root.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) layer.QueueFree();
        };
        root.Modulate = new Color(1, 1, 1, 0);
        tree.Root.AddChild(layer);
        root.CreateTween().TweenProperty(root, "modulate:a", 1f, 0.2);
    }

    private static Button NavButton(string text, Action onPressed)
    {
        var b = CinemaUi.ChoiceButton(text, 20, 120);
        b.CustomMinimumSize = new Vector2(120, 48);
        b.Pressed += onPressed;
        return b;
    }
}
