using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;

namespace MagicSwordsman.MagicSwordsmanCode.Relics;

/// <summary>
/// Card rewards and the shop are filtered through hooks on Mangeomchong (spec §2: only common cards + cards of owned
/// swords). Two generators have no hook and drew from the whole character pool, so a sword you do not own could
/// show up (bug report 2026-10-08: transform events):
///  - CardFactory.GetDefaultTransformationOptions (Transform / NewLeaf / events that transform at random);
///  - CardFactory.GetForCombat / GetDistinctForCombat (random cards generated in combat).
/// Same rule as <see cref="Mangeomchong.IsCardAllowed"/>; if filtering would leave nothing, the original list is kept
/// (the factory throws on an empty list).
/// </summary>
[HarmonyPatch]
internal static class OwnedSwordCardFilterPatches
{
    private static IEnumerable<CardModel> Filter(Player? player, IEnumerable<CardModel> cards)
    {
        var relic = player?.GetRelic<Mangeomchong>();
        if (relic == null) return cards;
        var all = cards.ToList();
        var allowed = all.Where(relic.IsCardAllowed).ToList();
        return allowed.Count > 0 ? allowed : all;
    }

    [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.GetDefaultTransformationOptions))]
    [HarmonyPostfix]
    private static void Transform(CardModel original, ref IEnumerable<CardModel> __result)
    {
        try { __result = Filter(original.Owner, __result); }
        catch (Exception e) { MainFile.Logger.Warn($"[OwnedSwordCardFilter] transform: {e.Message}"); }
    }

    [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.GetForCombat))]
    [HarmonyPrefix]
    private static void ForCombat(Player player, ref IEnumerable<CardModel> cards)
    {
        try { cards = Filter(player, cards); }
        catch (Exception e) { MainFile.Logger.Warn($"[OwnedSwordCardFilter] combat: {e.Message}"); }
    }

    [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.GetDistinctForCombat))]
    [HarmonyPrefix]
    private static void DistinctForCombat(Player player, ref IEnumerable<CardModel> cards)
    {
        try { cards = Filter(player, cards); }
        catch (Exception e) { MainFile.Logger.Warn($"[OwnedSwordCardFilter] combat: {e.Message}"); }
    }
}
