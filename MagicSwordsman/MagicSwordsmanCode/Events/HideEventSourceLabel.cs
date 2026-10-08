using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace MagicSwordsman.MagicSwordsmanCode.Events;

/// <summary>
/// BaseLib labels events from mods with the mod id in the bottom-left corner ("MagicSwordsman"). Our events are the
/// character's own story pages, so hide that label for them (user request 2026-10-08). Runs after BaseLib's postfix.
/// Other mods' events keep the label.
/// </summary>
[HarmonyPatch(typeof(NEventLayout), "SetEvent")]
internal static class HideEventSourceLabel
{
    private const string BaseLibLabelName = "BaseLibModSourceLabel";

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Hide(NEventLayout __instance, EventModel eventModel)
    {
        try
        {
            if (eventModel.GetType().Namespace?.StartsWith("MagicSwordsman") != true) return;
            __instance.GetNodeOrNull<Control>(BaseLibLabelName)?.QueueFree();
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"[HideEventSourceLabel] {e.Message}");
        }
    }
}
