using Godot;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MagicSwordsman.MagicSwordsmanCode.Visuals;

/// <summary>
/// Hover areas and tooltips of the floating swords, and the point a sword's speech bubble starts from.
///
/// Bug 2026-10-15 "검 설명 중 하나가 왼쪽 위에 표시되는 버그": the tooltip was opened with
/// NHoverTipSet.CreateAndShow(owner, tip) and the default HoverTipAlignment.None. With None the game's SetAlignment does
/// nothing at all (decompiled NHoverTipSet.SetAlignment: every placement is inside `if (alignment != 0)`), so the tip
/// set stayed at its own origin = the top-left corner of the screen. Game nodes that pass None always place the set
/// themselves afterwards (NSovereignBladeVfx: SetGlobalPosition(hitbox right edge)); we now do the same in
/// <see cref="PlaceTip"/>: right of the sword's drawn box (left of it near the right screen edge), clamped to the
/// screen, and re-placed every tick while shown so it follows the sword.
///
/// Bug 2026-10-15 "엔시페르 몸에 마우스를 댔는데 마검에 대한 설명이 뜨는 버그": the old hover box was the whole texture
/// rectangle (transparent padding included), two idle slots sit behind his body, and Onimaru's 거합/호위 poses overlap
/// it; whenever the creature hitbox did not take the mouse (it is set to Ignore while he is not interactable) the
/// sword's box did. Now:
///   - the hover area is a stack of small boxes fitted to the opaque pixels of the art (<see cref="SpriteBounds.Bands"/>),
///     children of the blade so they move, bob and rotate with it;
///   - the tip shows only while the mouse is over one of those boxes AND outside Ensifer's own hitbox rectangle
///     (whatever its mouse filter), so his body always wins;
///   - while the sword flies (strike, guard, union, death, a layout move, or simply moving fast) or a card is being
///     played / targeted, the boxes ignore the mouse entirely and no tip is shown.
/// Polled by a 50 ms timer per sword (no mouse-enter / exit signals), so a tip never sticks after the sword moved away.
/// UNVERIFIED in game: exact tip placement at non-1080p resolutions.
/// </summary>
public static partial class SwordVisuals
{
    private const string HoverNodeName = "Hover";

    private sealed class HoverState
    {
        public required SwordId Sword;
        public required Player Player;
        public required Node2D Holder;
        public required Control Box;
        public Vector2 LastPos;
        public bool Hovered;
        public bool InputOn = true;
    }

    /// <summary>
    /// Builds the hover boxes of a sword under its blade node (<paramref name="tex"/> null = placeholder polygon) and
    /// starts the hover poll. Called by CreateSword for non-spectral swords.
    /// </summary>
    private static void AttachHover(Node2D holder, Node2D blade, SwordId sword, Player player, Texture2D? tex, float scale)
    {
        try
        {
            var box = new Control { Name = HoverNodeName, MouseFilter = Control.MouseFilterEnum.Ignore, Size = Vector2.Zero };
            var rects = tex != null
                ? SpriteBounds.Bands(tex).Select(r => new Rect2(r.Position * scale, r.Size * scale)).ToArray()
                : [new Rect2(-8, -72, 16, 122)];
            foreach (var r in rects)
            {
                // a little slack on very thin bands so the blade stays easy to point at
                var grow = new Vector2(Math.Max(0, 14 - r.Size.X) / 2f, 0);
                box.AddChild(new Control
                {
                    Position = r.Position - grow, Size = r.Size + grow * 2, MouseFilter = Control.MouseFilterEnum.Pass,
                });
            }

            blade.AddChild(box);
            var st = new HoverState { Sword = sword, Player = player, Holder = holder, Box = box };
            var timer = new Godot.Timer { WaitTime = 0.05, Autostart = true, Name = "HoverPoll" };
            timer.Timeout += () => HoverTick(st);
            box.AddChild(timer);
            box.TreeExiting += () => HideTip(box);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[SwordVisuals] hover {sword}: {e.Message}");
        }
    }

    private static void HoverTick(HoverState st)
    {
        try
        {
            var box = st.Box;
            if (!GodotObject.IsInstanceValid(box) || !box.IsInsideTree()) return;
            var holder = st.Holder;
            var pos = holder.GlobalPosition;
            var moving = (pos - st.LastPos).LengthSquared() > 16f; // > 4 px in 50 ms
            st.LastPos = pos;

            var rigOk = Rigs.TryGetValue(st.Player, out var rig) && !rig.Dead && rig.Swords.TryGetValue(st.Sword, out var n) && n == holder;
            var busy = !rigOk || moving || holder.IsQueuedForDeletion() || !holder.IsVisibleInTree() ||
                       holder.Modulate.A < 0.6f || InFlight(holder) || IsUnionBusy(holder) || CardPlayInProgress();

            if (busy == st.InputOn)
            {
                st.InputOn = !busy;
                var filter = busy ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Pass;
                foreach (var child in box.GetChildren())
                    if (child is Control c) c.MouseFilter = filter;
            }

            var hovered = !busy && box.GetViewport().GuiGetHoveredControl() is { } hc && hc.GetParent() == box &&
                          !MouseOverBody(st.Player, box);
            if (hovered != st.Hovered)
            {
                st.Hovered = hovered;
                if (hovered) ShowTip(box, st.Sword, st.Player);
                else HideTip(box);
            }
            else if (hovered)
            {
                PlaceTip(box, null);
            }
        }
        catch (Exception)
        {
            // presentation only
        }
    }

    /// <summary>A card is being dragged / aimed, or the game is choosing a target: swords stay out of the way.</summary>
    private static bool CardPlayInProgress()
    {
        try
        {
            if (NCombatRoom.Instance?.Ui?.Hand is { } hand && hand.InCardPlay) return true;
            return NRun.Instance != null && NTargetManager.Instance is { } tm && tm.IsInSelection;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The mouse is inside Ensifer's own hitbox rectangle (his body always takes precedence over a sword).</summary>
    private static bool MouseOverBody(Player player, CanvasItem any)
    {
        var hitbox = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Hitbox;
        if (hitbox == null || !GodotObject.IsInstanceValid(hitbox)) return false;
        return hitbox.GetGlobalRect().HasPoint(any.GetGlobalMousePosition());
    }

    private static readonly Dictionary<Control, NHoverTipSet> Tips = new();

    /// <summary>
    /// Places the tooltip of <paramref name="owner"/> (a sword's hover box) beside the sword: to the right of its drawn
    /// box, or to the left when that would leave the screen; top aligned with the sword, kept on screen.
    /// <paramref name="fresh"/>: the set CreateAndShow just returned (null = re-place the one already shown).
    /// </summary>
    private static void PlaceTip(Control owner, NHoverTipSet? fresh)
    {
        try
        {
            if (fresh != null) Tips[owner] = fresh;
            if (!Tips.TryGetValue(owner, out var tip) || !GodotObject.IsInstanceValid(tip)) return;
            if (BoxOf(owner, live: true) is not { } sword) return;

            var view = NGame.Instance?.GetViewportRect().Size ?? owner.GetViewportRect().Size;
            var text = tip.GetChildCount() > 0 ? tip.GetChild(0) as Control : null;
            var size = text?.Size ?? new Vector2(360, 200);
            if (size.X < 1) size.X = 360;
            const float gap = 14f;
            var x = sword.End.X + gap;
            if (x + size.X > view.X - 8) x = sword.Position.X - gap - size.X;
            x = Mathf.Clamp(x, 8, Math.Max(8, view.X - size.X - 8));
            var y = Mathf.Clamp(sword.Position.Y, 8, Math.Max(8, view.Y - size.Y - 8));
            tip.GlobalPosition = new Vector2(x, y);
        }
        catch (Exception)
        {
            // presentation only
        }
    }

    /// <summary>
    /// Global box of the drawn sword from its hover boxes. <paramref name="live"/>: with the blade's current bob / sway
    /// (tooltips follow the moving art); otherwise the blade's resting place under the holder (speech bubbles stay put
    /// while the blade drifts a few px around it).
    /// </summary>
    private static Rect2? BoxOf(Control hoverBox, bool live)
    {
        Rect2? acc = null;
        var holderXf = (hoverBox.GetParent()?.GetParent() as Node2D)?.GetGlobalTransform();
        foreach (var child in hoverBox.GetChildren())
        {
            if (child is not Control c) continue;
            var local = new Rect2(c.Position, c.Size);
            var g = live || holderXf == null
                ? SpriteBounds.GlobalBox(hoverBox.GetGlobalTransform(), local)
                : SpriteBounds.GlobalBox(holderXf.Value, local); // blade at rest = identity under the holder
            acc = acc?.Merge(g) ?? g;
        }

        return acc;
    }

    /// <summary>
    /// Where a speech bubble's tail should touch this sword: just above the top of its drawn art, horizontally centred on
    /// it (rotation and scale included). Null when the node has no hover boxes (spectral copies, placeholders).
    /// </summary>
    internal static Vector2? SpeechPointOf(Node2D holder)
    {
        try
        {
            if (holder.GetNodeOrNull<Control>($"Blade/{HoverNodeName}") is not { } box) return null;
            if (BoxOf(box, live: false) is not { } r) return null;
            return new Vector2(r.Position.X + r.Size.X * 0.5f, r.Position.Y - 6f);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
