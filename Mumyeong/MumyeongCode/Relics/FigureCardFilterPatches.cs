using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;

namespace Mumyeong.MumyeongCode.Relics;

/// <summary>
/// Card rewards and the shop are filtered by <see cref="SoulCore"/> hooks. Two generators have no hook (transform,
/// random cards generated in combat); they get the same rule here. If filtering would leave nothing, the original list
/// is kept (the factory throws on an empty list). Copied from 엔시페르 OwnedSwordCardFilterPatches.
/// </summary>
[HarmonyPatch]
internal static class FigureCardFilterPatches
{
    private static IEnumerable<CardModel> Filter(Player? player, IEnumerable<CardModel> cards)
    {
        var core = player?.GetRelic<SoulCore>();
        if (core == null) return cards;
        var all = cards.ToList();
        var allowed = all.Where(core.IsCardAllowed).ToList();
        return allowed.Count > 0 ? allowed : all;
    }

    [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.GetDefaultTransformationOptions))]
    [HarmonyPostfix]
    private static void Transform(CardModel original, ref IEnumerable<CardModel> __result)
    {
        try { __result = Filter(original.Owner, __result); }
        catch (Exception e) { MainFile.Logger.Warn($"[FigureCardFilter] transform: {e.Message}"); }
    }

    [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.GetForCombat))]
    [HarmonyPrefix]
    private static void ForCombat(Player player, ref IEnumerable<CardModel> cards)
    {
        try { cards = Filter(player, cards); }
        catch (Exception e) { MainFile.Logger.Warn($"[FigureCardFilter] combat: {e.Message}"); }
    }

    [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.GetDistinctForCombat))]
    [HarmonyPrefix]
    private static void DistinctForCombat(Player player, ref IEnumerable<CardModel> cards)
    {
        try { cards = Filter(player, cards); }
        catch (Exception e) { MainFile.Logger.Warn($"[FigureCardFilter] combat: {e.Message}"); }
    }
}
