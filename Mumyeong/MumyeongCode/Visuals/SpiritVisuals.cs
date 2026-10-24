using Godot;
using Mumyeong.MumyeongCode.Figures;
using Mumyeong.MumyeongCode.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Mumyeong.MumyeongCode.Visuals;

/// <summary>
/// The guardian spirit behind the reaper (user request 2026-10-24): once the 코어 holds a figure, that figure's
/// spirit art (images/character/spirit_&lt;key&gt;.png, rest site: spirit_&lt;key&gt;_rest.png) is put on the "Spirit"
/// sprite of the character scenes (RuntimeScenes). It grows clearer with the soul stage. Presentation only:
/// nothing here touches game state.
/// </summary>
public static class SpiritVisuals
{
    /// <summary>How clearly the spirit shows at each stage (빙의 → 강림).</summary>
    public static float AlphaOf(SoulStage stage) => stage switch
    {
        SoulStage.Possession => 0.5f,
        SoulStage.Synchronization => 0.62f,
        SoulStage.Focus => 0.74f,
        SoulStage.Descent => 0.88f,
        _ => 0.5f,
    };

    public static string FileKey(FigureId figure) => figure.ToString().ToLowerInvariant();

    public static Texture2D? Texture(FigureId figure, bool resting)
    {
        var path = $"{MainFile.ResPath}/images/character/spirit_{FileKey(figure)}{(resting ? "_rest" : "")}.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    /// <summary>Combat: the "Spirit" sprite under the player's creature visuals. Null when it is not on screen.</summary>
    public static Sprite2D? CombatSpirit(Player player)
    {
        var visuals = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals;
        return visuals?.FindChild("Spirit", true, false) as Sprite2D;
    }

    /// <summary>The reaper's "Body" sprite in combat (for speech anchors).</summary>
    public static Sprite2D? CombatBody(Player player)
    {
        var visuals = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals;
        return visuals?.FindChild("Body", true, false) as Sprite2D;
    }

    public static NRestSiteCharacter? RestCharacter(Player player)
    {
        var character = NRestSiteRoom.Instance?.GetCharacterForPlayer(player);
        return character != null && GodotObject.IsInstanceValid(character) && character.IsVisibleInTree()
            ? character
            : null;
    }

    public static Node2D? MerchantFigure()
    {
        var merchant = NMerchantRoom.Instance;
        if (merchant == null || !GodotObject.IsInstanceValid(merchant) || !merchant.IsVisibleInTree()) return null;
        if (merchant.PlayerVisuals.Count == 0) return null;
        var figure = merchant.PlayerVisuals[0];
        return GodotObject.IsInstanceValid(figure) ? figure : null;
    }

    /// <summary>Shows (or hides) the spirit on whichever of the player's figures is on screen now.</summary>
    public static void Refresh(Player player)
    {
        try
        {
            var core = player.GetRelic<SoulCore>();
            var figure = core?.Figure;
            var alpha = core == null ? 0f : AlphaOf(core.CurrentStage);

            if (CombatSpirit(player) is { } combat) Apply(combat, figure, false, alpha);
            if (RestCharacter(player)?.FindChild("Spirit", true, false) is Sprite2D rest) Apply(rest, figure, true, alpha);
            // the shop only shows the local player's figure first; others have no spirit node to find there
            if (MerchantFigure()?.FindChild("Spirit", true, false) is Sprite2D shop &&
                MegaCrit.Sts2.Core.Context.LocalContext.IsMe(player))
                Apply(shop, figure, false, alpha);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SpiritVisuals] refresh: {e.Message}");
        }
    }

    /// <summary>Refreshes now and again shortly after (room nodes are built after the room-entered hook).</summary>
    public static void RefreshSoon(Player player)
    {
        Refresh(player);
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        foreach (var delay in new[] { 0.3, 1.0, 2.5 })
            tree.CreateTimer(delay).Timeout += () => Refresh(player);
    }

    /// <summary>The spirit flares up for a moment (a figure card was played, a stage was reached).</summary>
    public static void Pulse(Player player, float strength = 1f)
    {
        try
        {
            if (CombatSpirit(player) is not { Visible: true } sprite) return;
            var baseAlpha = sprite.GetMeta("base_alpha", sprite.Modulate.A).AsSingle();
            var t = sprite.CreateTween();
            t.TweenProperty(sprite, "modulate", new Color(1.25f, 1.3f, 1.4f, Math.Min(1f, baseAlpha + 0.3f * strength)), 0.12);
            t.TweenProperty(sprite, "modulate", new Color(1, 1, 1, baseAlpha), 0.6);
        }
        catch (Exception)
        {
            // presentation only
        }
    }

    private static void Apply(Sprite2D sprite, FigureId? figure, bool resting, float alpha)
    {
        if (!GodotObject.IsInstanceValid(sprite)) return;
        if (figure is not { } f || Texture(f, resting) is not { } tex)
        {
            sprite.Visible = false;
            return;
        }

        var fresh = sprite.Texture != tex || !sprite.Visible;
        sprite.Texture = tex;
        sprite.Visible = true;
        sprite.SetMeta("base_alpha", alpha);
        sprite.SetMeta("figure", (int)f);
        if (!fresh)
        {
            sprite.Modulate = new Color(1, 1, 1, alpha);
            return;
        }

        // the spirit gathers out of the smoke
        sprite.Modulate = new Color(1, 1, 1, 0);
        sprite.CreateTween().TweenProperty(sprite, "modulate", new Color(1, 1, 1, alpha), 1.2)
            .SetTrans(Tween.TransitionType.Sine);
    }
}
