using Godot;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Extensions;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// User request 2026-10-15 "내 체력바 아래에 오니마루 현재 어떤 자세인지 알 수 있게": while Onimaru is out in combat, a
/// small tag under Ensifer's health bar shows its stance (the stance power's icon + its name, e.g. "거합 자세").
///
/// The tag is a child of the creature's NCreatureStateDisplay (the node NCreature finds as %HealthBar, which holds the
/// health bar and the power row), so it shows, hides and slides with the health bar. It sits under the health bar and,
/// when the power row below the bar has icons, under that row, so it never covers a power. A 150 ms poll keeps
/// stance, visibility and position current; <see cref="Refresh"/> (called from SwordVisuals.Sync, which runs on every
/// stance change) updates it at once. Local player only. Presentation only; every failure is swallowed.
/// Names: powers table MAGICSWORDSMAN-ONIMARU_&lt;KIND&gt;_POWER.title; icons images/powers/onimaru_&lt;kind&gt;_power.png.
/// UNVERIFIED in game: exact offsets under the bar.
/// </summary>
public static class OnimaruStanceHud
{
    private const string NodeName = "MagicSwordsOnimaruStance";

    public static void Refresh(Player player)
    {
        try { RefreshInner(player); }
        catch (Exception e) { MainFile.Logger.Warn($"[OnimaruStanceHud] {e.Message}"); }
    }

    private static void RefreshInner(Player player)
    {
        if (!LocalContext.IsMe(player)) return;
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        if (creatureNode == null) return;
        var display = creatureNode.GetNodeOrNull<Control>("%HealthBar");
        if (display == null) return;
        var hud = display.GetNodeOrNull<PanelContainer>(NodeName) ?? Create(display, player);
        Update(hud, player, display);
    }

    private static PanelContainer Create(Control display, Player player)
    {
        var hud = new PanelContainer { Name = NodeName, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        hud.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.04f, 0.08f, 0.78f), BorderColor = new Color(0.62f, 0.45f, 0.85f, 0.85f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 6, ContentMarginRight = 10, ContentMarginTop = 2, ContentMarginBottom = 2,
        });
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        hud.AddChild(row);
        row.AddChild(new TextureRect
        {
            Name = "Icon", CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        var label = new Label { Name = "Name", MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_color", new Color(0.93f, 0.88f, 1f));
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 6);
        if (GameFont() is { } font) label.AddThemeFontOverride("font", font);
        row.AddChild(label);
        display.AddChild(hud);

        var timer = new Godot.Timer { WaitTime = 0.15, Autostart = true };
        timer.Timeout += () =>
        {
            try
            {
                if (GodotObject.IsInstanceValid(hud) && GodotObject.IsInstanceValid(display)) Update(hud, player, display);
            }
            catch (Exception) { /* presentation only */ }
        };
        hud.AddChild(timer);
        return hud;
    }

    private static void Update(PanelContainer hud, Player player, Control display)
    {
        var state = SwordCombat.Get(player);
        var show = state != null && state.Present.Contains(SwordId.Onimaru) && !player.Creature.IsDead;
        if (!show)
        {
            hud.Visible = false;
            return;
        }

        var kind = OnimaruAttack.GetKind(player);
        var key = kind.ToString();
        if (!hud.HasMeta("kind") || (string)hud.GetMeta("kind") != key)
        {
            hud.SetMeta("kind", key);
            var id = KindId(kind);
            if (hud.FindChild("Icon", true, false) is TextureRect icon)
            {
                var path = $"onimaru_{id}_power.png".PowerImagePath();
                icon.Texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            }

            if (hud.FindChild("Name", true, false) is Label label)
            {
                var loc = LocString.GetIfExists("powers", $"MAGICSWORDSMAN-ONIMARU_{id.ToUpperInvariant()}_POWER.title");
                string text;
                try { text = loc?.GetFormattedText() ?? kind.ToString(); }
                catch (Exception) { text = kind.ToString(); }
                label.Text = text;
            }

            hud.ResetSize();
            // a short pulse so a stance change is noticed
            hud.PivotOffset = hud.Size / 2f;
            var t = hud.CreateTween();
            t.TweenProperty(hud, "scale", new Vector2(1.12f, 1.12f), 0.08);
            t.TweenProperty(hud, "scale", Vector2.One, 0.16);
        }

        // under the health bar, and under the power row when it has icons
        var bar = display.GetNodeOrNull<Control>("%HealthBar");
        var powers = display.GetNodeOrNull<Control>("%PowerContainer");
        var barRect = bar?.GetGlobalRect() ?? display.GetGlobalRect();
        var bottom = barRect.End.Y;
        if (powers != null && powers.IsVisibleInTree() && powers.GetChildren().OfType<CanvasItem>().Any(c => c.Visible))
            bottom = Math.Max(bottom, ChildrenBottom(powers));
        hud.GlobalPosition = new Vector2(barRect.Position.X, bottom + 4f);
        hud.Visible = true;
    }

    /// <summary>Lowest edge of the visible children (the container's own rect may not shrink to its icons).</summary>
    private static float ChildrenBottom(Control container)
    {
        var bottom = container.GetGlobalRect().Position.Y;
        foreach (var child in container.GetChildren())
            if (child is Control { Visible: true } c)
                bottom = Math.Max(bottom, c.GetGlobalRect().End.Y);
        return bottom;
    }

    private static string KindId(OnimaruKind kind) => kind switch
    {
        OnimaruKind.Ranbu => "ranbu",
        OnimaruKind.Giri => "giri",
        OnimaruKind.Taima => "taima",
        OnimaruKind.Goei => "goei",
        _ => "iai",
    };

    private static Font? GameFont()
    {
        try
        {
            var lang = LocManager.Instance?.Language ?? "eng";
            return FontManager.GetSubstituteFont(lang, FontType.Bold) ?? NGame.Instance?.GetThemeDefaultFont();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
