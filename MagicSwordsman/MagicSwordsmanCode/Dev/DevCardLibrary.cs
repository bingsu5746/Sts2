using HarmonyLib;
using MagicSwordsman.MagicSwordsmanCode.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace MagicSwordsman.MagicSwordsmanCode.Dev;

/// <summary>
/// 개발용 (사용자 요청 2026-10-04): 카드 도감에서 엔시페르(마검사) 카드를 잠금·미발견 상태와 상관없이 전부 보이게 한다.
/// The game hides cards that are locked or not yet discovered (NCardLibraryGrid.GetCardVisibility checks the unlock
/// state and SaveManager.Progress.DiscoveredCards). This postfix forces Visible for this mod's cards only.
/// Set <see cref="Enabled"/> to false (or delete this file) before release.
/// </summary>
[HarmonyPatch(typeof(NCardLibraryGrid), "GetCardVisibility")]
internal static class DevCardLibrary
{
    public static readonly bool Enabled = true;

    [HarmonyPostfix]
    private static void ShowModCards(CardModel card, ref ModelVisibility __result)
    {
        if (!Enabled) return;
        if (card is MagicSwordCard || card.GetType().Namespace?.StartsWith("MagicSwordsman") == true)
            __result = ModelVisibility.Visible;
    }
}
