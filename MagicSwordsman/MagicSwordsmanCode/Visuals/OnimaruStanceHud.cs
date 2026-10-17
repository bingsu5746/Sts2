using Godot;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Extensions;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MagicSwordsman.MagicSwordsmanCode.Swords.Behaviors;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// User request 2026-10-15 "내 체력바 아래에 오니마루 현재 어떤 자세인지 알 수 있게": while Onimaru is out in combat, a
/// small tag shows its stance (the stance power's icon + its name, e.g. "거합 자세").
///
/// Since 2026-10-17 the tag is the second row of <see cref="SwordHud"/> (the stack under Ensifer's health bar, below
/// the current-sword tag), which owns the node, the poll and the placement; this class only builds and fills the row.
/// Names: powers table MAGICSWORDSMAN-ONIMARU_&lt;KIND&gt;_POWER.title; icons images/powers/onimaru_&lt;kind&gt;_power.png.
/// </summary>
public static class OnimaruStanceHud
{
    /// <summary>Kept for callers: refreshes the whole HUD stack (current sword + stance). Never throws.</summary>
    public static void Refresh(Player player) => SwordHud.Refresh(player);

    internal static PanelContainer CreateTag()
    {
        var hud = new PanelContainer { Name = "OnimaruStance", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        hud.AddThemeStyleboxOverride("panel", SwordHud.TagStyle(new Color(0.62f, 0.45f, 0.85f, 0.85f)));
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        hud.AddChild(row);
        row.AddChild(SwordHud.MakeIcon());
        row.AddChild(SwordHud.MakeLabel("Name", new Color(0.93f, 0.88f, 1f)));
        return hud;
    }

    /// <summary>Shows the stance while Onimaru is present. Returns true when the row content changed (the stack re-sizes).</summary>
    internal static bool UpdateTag(PanelContainer hud, Player player)
    {
        var state = SwordCombat.Get(player);
        var show = state != null && state.Present.Contains(SwordId.Onimaru) && !player.Creature.IsDead;
        if (!show)
        {
            var changed = hud.Visible;
            hud.Visible = false;
            hud.RemoveMeta("kind");
            return changed;
        }

        var kind = OnimaruAttack.GetKind(player);
        var key = kind.ToString();
        if (hud.Visible && hud.HasMeta("kind") && (string)hud.GetMeta("kind") == key) return false;

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

        hud.Visible = true;
        SwordHud.Pulse(hud); // a short pulse so a stance change is noticed
        return true;
    }

    private static string KindId(OnimaruKind kind) => kind switch
    {
        OnimaruKind.Ranbu => "ranbu",
        OnimaruKind.Giri => "giri",
        OnimaruKind.Taima => "taima",
        OnimaruKind.Goei => "goei",
        _ => "iai",
    };
}
