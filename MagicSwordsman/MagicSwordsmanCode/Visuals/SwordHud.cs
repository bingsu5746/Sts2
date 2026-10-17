using Godot;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Events;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// User feedback 2026-10-17 "이 검이 뭐하는 검인지나 검의 고유 능력(피해 고정으로 감소) 이런 게 잘 보이지 않고": a compact
/// stack of tags under Ensifer's health bar, local player only.
///   Row 1 — the CURRENT sword: its icon + name + its effect in a few words with live numbers
///           (<see cref="SwordEffectText.Short"/>, e.g. "뒤랑달 · 받는 공격 피해 -3", "클라이브 솔라시 · 빛 3 (턴 종료 +1)").
///           Hovering it shows the full text: role, effect, cost, level and a one-line reminder of how swords switch.
///           Hidden while there is no current sword.
///   Row 2 — Onimaru's stance while Onimaru is out (<see cref="OnimaruStanceHud"/>, the tag of 2026-10-15).
///
/// The stack is a child of the creature's NCreatureStateDisplay (the node NCreature finds as %HealthBar, which holds
/// the health bar and the power row), so it shows, hides and slides with the health bar. It is placed under the
/// health bar and, when the power row below the bar has icons, under the lowest icon, so it never covers a power.
/// A 150 ms poll keeps text (level changes, Light, Kusanagi's inherited sword...), visibility and position current;
/// <see cref="Refresh"/> (called from SwordVisuals.Sync, which runs on every switch / summon / stance change) updates
/// it at once. Presentation only; every failure is swallowed.
/// Texts: events table MAGICSWORDSMAN-SWORD_HUD.*, MAGICSWORDSMAN-SWORD_FX.SHORT_* (loc_fragments/clarity.*).
/// UNVERIFIED in game: exact offsets under the bar, tag width with long names at 1280x720.
/// </summary>
public static class SwordHud
{
    private const string NodeName = "MagicSwordsHud";
    private const string LocTable = "events";

    public static void Refresh(Player player)
    {
        try { RefreshInner(player); }
        catch (Exception e) { MainFile.Logger.Warn($"[SwordHud] {e.Message}"); }
    }

    private static void RefreshInner(Player player)
    {
        if (!LocalContext.IsMe(player)) return;
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        if (creatureNode == null) return;
        var display = creatureNode.GetNodeOrNull<Control>("%HealthBar");
        if (display == null) return;
        var stack = display.GetNodeOrNull<VBoxContainer>(NodeName) ?? Create(display, player);
        Update(stack, player, display);
    }

    private static VBoxContainer Create(Control display, Player player)
    {
        var stack = new VBoxContainer { Name = NodeName, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        stack.AddThemeConstantOverride("separation", 4);
        stack.AddChild(CreateCurrentTag(player));
        stack.AddChild(OnimaruStanceHud.CreateTag());
        display.AddChild(stack);

        var timer = new Godot.Timer { WaitTime = 0.15, Autostart = true };
        timer.Timeout += () =>
        {
            try
            {
                if (GodotObject.IsInstanceValid(stack) && GodotObject.IsInstanceValid(display)) Update(stack, player, display);
            }
            catch (Exception) { /* presentation only */ }
        };
        stack.AddChild(timer);
        return stack;
    }

    private static void Update(VBoxContainer stack, Player player, Control display)
    {
        var changed = false;
        if (stack.GetNodeOrNull<PanelContainer>("CurrentSword") is { } cur) changed |= UpdateCurrentTag(cur, player);
        if (stack.GetNodeOrNull<PanelContainer>("OnimaruStance") is { } oni) changed |= OnimaruStanceHud.UpdateTag(oni, player);

        var any = stack.GetChildren().OfType<Control>().Any(c => c.Visible);
        if (!any)
        {
            stack.Visible = false;
            return;
        }

        if (changed) stack.ResetSize();

        // under the health bar, and under the power row when it has icons
        var bar = display.GetNodeOrNull<Control>("%HealthBar");
        var powers = display.GetNodeOrNull<Control>("%PowerContainer");
        var barRect = bar?.GetGlobalRect() ?? display.GetGlobalRect();
        var bottom = barRect.End.Y;
        if (powers != null && powers.IsVisibleInTree() && powers.GetChildren().OfType<CanvasItem>().Any(c => c.Visible))
            bottom = Math.Max(bottom, ChildrenBottom(powers));
        stack.GlobalPosition = new Vector2(barRect.Position.X, bottom + 4f);
        stack.Visible = true;
    }

    // ------------------------------------------------------------------ row 1: current sword

    private static PanelContainer CreateCurrentTag(Player player)
    {
        // Pass (not Stop): the tag gets mouse enter / exit for its tooltip without swallowing anything else
        var tag = new PanelContainer { Name = "CurrentSword", MouseFilter = Control.MouseFilterEnum.Pass, Visible = false };
        tag.AddThemeStyleboxOverride("panel", TagStyle(new Color(0.8f, 0.8f, 0.85f, 0.85f)));
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        tag.AddChild(row);
        row.AddChild(MakeIcon());
        row.AddChild(MakeLabel("Name", Colors.White));
        row.AddChild(MakeLabel("Effect", new Color(0.95f, 0.95f, 0.95f), bold: false));

        tag.MouseEntered += () => ShowTip(tag, player);
        tag.MouseExited += () => HideTip(tag);
        tag.TreeExiting += () => HideTip(tag);
        return tag;
    }

    /// <summary>Fills the current-sword row. Returns true when its content or visibility changed.</summary>
    private static bool UpdateCurrentTag(PanelContainer tag, Player player)
    {
        var sword = !player.Creature.IsDead ? SwordCombat.CurrentSword(player) : null;
        if (sword is not { } s)
        {
            if (!tag.Visible) return false;
            tag.Visible = false;
            tag.RemoveMeta("key");
            HideTip(tag);
            return true;
        }

        var level = SwordCombat.LevelOf(player, s);
        var name = SwordRegistry.DisplayName(s);
        var effect = SwordEffectText.Short(s, player, level);
        var key = $"{s}|{level}|{effect}";
        if (tag.Visible && tag.HasMeta("key") && (string)tag.GetMeta("key") == key) return false;

        var swordChanged = !tag.HasMeta("sword") || (string)tag.GetMeta("sword") != s.ToString();
        tag.SetMeta("key", key);
        tag.SetMeta("sword", s.ToString());

        var accent = SwordVisuals.ColorOf(s);
        if (swordChanged)
        {
            tag.AddThemeStyleboxOverride("panel", TagStyle(new Color(accent, 0.9f)));
            if (tag.FindChild("Icon", true, false) is TextureRect icon)
            {
                var path = System.IO.Path.Join(MainFile.ResPath, "images", "powers", $"current_sword_{s.ToString().ToLowerInvariant()}.png");
                icon.Texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            }
        }

        if (tag.FindChild("Name", true, false) is Label nameLabel)
        {
            nameLabel.Text = name;
            nameLabel.AddThemeColorOverride("font_color", accent.Lightened(0.35f));
        }

        if (tag.FindChild("Effect", true, false) is Label effectLabel)
        {
            effectLabel.Text = effect.Length > 0 ? "· " + effect : "";
            effectLabel.Visible = effect.Length > 0;
        }

        tag.Visible = true;
        if (swordChanged) Pulse(tag);
        else if (Tips.ContainsKey(tag)) ShowTip(tag, player); // live numbers in an open tooltip
        return true;
    }

    private static readonly Dictionary<Control, bool> Tips = new();

    private static void ShowTip(PanelContainer tag, Player player)
    {
        try
        {
            if (!tag.Visible || SwordCombat.CurrentSword(player) is not { } s) return;
            var level = SwordCombat.LevelOf(player, s);
            var title = new LocString(LocTable, "MAGICSWORDSMAN-SWORD_HUD.TIP_TITLE");
            title.Add("Sword", SwordRegistry.DisplayName(s));

            var lines = new List<string>();
            var role = SwordRole.Rich(s);
            if (role.Length > 0) lines.Add(role);
            lines.Add(SwordEffectText.Full(s, player, level));
            var levelText = new LocString(LocTable, "MAGICSWORDSMAN-SWORD_VIEWER.LEVEL");
            levelText.Add("Level", level);
            levelText.Add("Max", SwordRegistry.MaxLevel);
            lines.Add($"{Text(SwordLore.StageName(s, level))} — {Text(levelText)}");
            lines.Add(Text(new LocString(LocTable, "MAGICSWORDSMAN-SWORD_HUD.TIP_HINT")));

            NHoverTipSet.Remove(tag);
            NHoverTipSet.CreateAndShow(tag, new HoverTip(title, string.Join("\n", lines)), HoverTipAlignment.Right);
            Tips[tag] = true;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordHud] tooltip: {e.Message}");
        }
    }

    private static void HideTip(Control tag)
    {
        if (!Tips.Remove(tag)) return;
        try { NHoverTipSet.Remove(tag); }
        catch (Exception) { /* presentation only */ }
    }

    // ------------------------------------------------------------------ shared look (also used by OnimaruStanceHud)

    internal static StyleBoxFlat TagStyle(Color border) => new()
    {
        BgColor = new Color(0.05f, 0.04f, 0.08f, 0.78f), BorderColor = border,
        BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        ContentMarginLeft = 6, ContentMarginRight = 10, ContentMarginTop = 2, ContentMarginBottom = 2,
    };

    internal static TextureRect MakeIcon() => new()
    {
        Name = "Icon", CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    internal static Label MakeLabel(string name, Color color, bool bold = true)
    {
        var label = new Label { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", bold ? 20 : 18);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 6);
        if (GameFont(bold) is { } font) label.AddThemeFontOverride("font", font);
        return label;
    }

    /// <summary>A short scale pulse so a change is noticed.</summary>
    internal static void Pulse(Control c)
    {
        c.PivotOffset = c.Size / 2f;
        var t = c.CreateTween();
        t.TweenProperty(c, "scale", new Vector2(1.12f, 1.12f), 0.08);
        t.TweenProperty(c, "scale", Vector2.One, 0.16);
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

    private static string Text(LocString loc)
    {
        try { return loc.GetFormattedText(); }
        catch (Exception) { return ""; }
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
