using HarmonyLib;
using MagicSwordsman.MagicSwordsmanCode.Combat;
using MagicSwordsman.MagicSwordsmanCode.Swords;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MagicSwordsman.MagicSwordsmanCode.Relics;

/// <summary>
/// 열린 만검총 — Ancient (Touch of Orobas) upgrade of 만검총 (content doc §9).
///  - Everything of Mangeomchong stays (saved swords/levels/stored cards, reward filter, rest-site 마검 강화):
///    it derives from <see cref="Mangeomchong"/>, so <c>player.GetRelic&lt;Mangeomchong&gt;()</c> finds it.
///  - First sword summon of each combat: Block 6 + draw 2 (Mangeomchong's 3 + 1, doubled like BurningBlood 6 ->
///    BlackBlood 12). The base class reads its Block/Cards vars, so only the numbers change here.
///  - Later summons give nothing (사용자 결정 2026-10-04: only the first summon of a combat has the bonus).
///  - No extra slot (slot expansions only come from 브란스톡 / 검총의 나무 패).
/// Replacement: Mangeomchong.GetUpgradeReplacement -> BaseLib StarterUpgradePatches -> TouchOfOrobas.AfterObtained ->
/// RelicCmd.Replace(old, new). RelicCmd.Replace does not move any state, so <see cref="ReplacePatch"/> copies the run
/// data from the old relic into the new one right before the swap (both are still mutable then).
/// </summary>
public sealed class OpenedMangeomchong : Mangeomchong
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Unpowered),
        new CardsVar(2),
        new StringVar("SwordList"),
    ];

    /// <summary>Copies all saved run data of the original 만검총 (called before RelicCmd.Replace swaps them).</summary>
    internal void CopyRunStateFrom(Mangeomchong source)
    {
        OwnedSwordIds = source.OwnedSwordIds.ToArray();
        SwordLevels = source.SwordLevels.ToArray();
        EverOwnedIds = source.EverOwnedIds.ToArray();
        ExtraSlots = source.ExtraSlots;
        StoredCards = source.StoredCards.ToList();
        RunCounters = source.RunCounters;
    }

    /// <summary>Moves the run data from 만검총 into 열린 만검총 when the Ancient replaces it.</summary>
    [HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Replace))]
    internal static class ReplacePatch
    {
        [HarmonyPrefix]
        private static void CopyState(RelicModel original, RelicModel replace)
        {
            if (original is not Mangeomchong source || replace is not OpenedMangeomchong target) return;
            if (ReferenceEquals(source, target)) return;
            try
            {
                target.CopyRunStateFrom(source);
                MainFile.Logger.Info($"[OpenedMangeomchong] copied run data: swords [{string.Join(",", source.OwnedSwords)}]");
            }
            catch (Exception e)
            {
                MainFile.Logger.Error($"[OpenedMangeomchong] failed to copy run data: {e}");
            }
        }
    }
}
