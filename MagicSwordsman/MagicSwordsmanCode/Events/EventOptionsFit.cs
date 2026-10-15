using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace MagicSwordsman.MagicSwordsmanCode.Events;

/// <summary>
/// Keeps every option button of the mod's own event pages on screen (user report 2026-10-15: in act 3 the 만검총 event's
/// last row, "지나친다", was cut off at the bottom). The texts were shortened to one line per sword as the main fix
/// (SwordEventHelper.OptionDescription, TombResonanceEvent); this is the safety net for small windows / long pages:
/// after the game laid the buttons out, if the last one ends below the visible area the option list is tightened step by
/// step — smaller gaps between the buttons, then smaller button text — and, when its parent does not position it
/// (not a Container), the list is moved up into the free space above it.
/// Presentation only, never throws, other mods' and vanilla events are untouched.
/// UNVERIFIED in game (the event scene file is not in the decompiled source): check 1920x1080 and 1280x720.
/// </summary>
[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.AddOptions))]
internal static class EventOptionsFit
{
    private const float BottomMargin = 12f;
    private const int MinFontSize = 14;

    private static readonly AccessTools.FieldRef<NEventLayout, VBoxContainer> OptionsRef =
        AccessTools.FieldRefAccess<NEventLayout, VBoxContainer>("_optionsContainer");

    private static readonly AccessTools.FieldRef<NEventLayout, EventModel> EventRef =
        AccessTools.FieldRefAccess<NEventLayout, EventModel>("_event");

    [HarmonyPostfix]
    private static void Postfix(NEventLayout __instance)
    {
        try
        {
            if (EventRef(__instance)?.GetType().Namespace?.StartsWith("MagicSwordsman") != true) return;
            var container = OptionsRef(__instance);
            if (container == null || __instance.GetTree() is not { } tree) return;
            // wait for the buttons' _Ready and two layout passes
            var frames = 0;
            void Tick()
            {
                if (!GodotObject.IsInstanceValid(container) || !container.IsInsideTree()) return;
                if (++frames < 3)
                {
                    tree.CreateTimer(0.0).Timeout += Tick;
                    return;
                }

                Fit(container, 0);
            }

            tree.CreateTimer(0.0).Timeout += Tick;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[EventOptionsFit] {e.Message}");
        }
    }

    private static float Overflow(Control container)
    {
        var visible = container.GetViewportRect().Size.Y;
        var bottom = container.GetChildren().OfType<Control>().Where(c => c.Visible)
            .Select(c => c.GetGlobalRect().End.Y).DefaultIfEmpty(container.GetGlobalRect().End.Y).Max();
        return bottom - (visible - BottomMargin);
    }

    /// <summary>One tightening step per call, re-measured a frame later, up to 4 steps.</summary>
    private static void Fit(VBoxContainer container, int step)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(container) || !container.IsInsideTree()) return;
            var overflow = Overflow(container);
            if (overflow <= 0f || step > 4) return;

            switch (step)
            {
                case 0:
                    container.AddThemeConstantOverride("separation",
                        Math.Min(container.GetThemeConstant("separation"), 4));
                    break;
                case 1:
                case 2:
                    ShrinkText(container, 0.86f);
                    break;
                default:
                    if (container.GetParent() is Control parent and not Container)
                    {
                        var top = container.GetGlobalRect().Position.Y;
                        var up = Math.Min(overflow, Math.Max(0f, top - 8f));
                        if (up > 0f) container.Position -= new Vector2(0f, up);
                        MainFile.Logger.Info($"[EventOptionsFit] moved event options up by {up:0}px");
                    }
                    break;
            }

            MainFile.Logger.Info($"[EventOptionsFit] options overflowed by {overflow:0}px, step {step}");
            container.GetTree().CreateTimer(0.0).Timeout += () => Fit(container, step + 1);
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[EventOptionsFit] {e.Message}");
        }
    }

    private static void ShrinkText(Control container, float factor)
    {
        foreach (var button in container.GetChildren().OfType<NEventOptionButton>())
        {
            if (button.GetNodeOrNull<RichTextLabel>("%Text") is not { } label) continue;
            if (label is MegaRichTextLabel { AutoSizeEnabled: true } mega)
            {
                mega.MaxFontSize = Math.Max(MinFontSize, (int)(mega.MaxFontSize * factor));
                mega.MinFontSize = Math.Min(mega.MinFontSize, mega.MaxFontSize);
                mega.SetTextAutoSize(mega.Text + " ");
                mega.SetTextAutoSize(mega.Text.TrimEnd(' '));
                continue;
            }

            foreach (var name in new[] { "normal_font_size", "bold_font_size" })
            {
                var size = label.GetThemeFontSize(name);
                label.AddThemeFontSizeOverride(name, Math.Max(MinFontSize, (int)(size * factor)));
            }
        }
    }
}
