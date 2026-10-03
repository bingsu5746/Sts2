using HarmonyLib;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace MagicSwordsman.MagicSwordsmanCode.Events;

/// <summary>
/// Forces <see cref="TombResonanceEvent"/> right after an act boss (spec §5 [확정] "보스 후 확정 이벤트").
///
/// How the game moves on after a boss (verified in the decompiled source): the boss reward screen ->
/// ActChangeSynchronizer.MoveToNextAct (on every client once all players are ready) -> RunManager.EnterNextAct ->
/// RunManager.EnterAct(index) -> new act map (MapRoom) -> Hook.AfterActEntered(runState). The game has no hook that
/// inserts a room there, so two small Harmony patches are used:
///   1. postfix on Hook.AfterActEntered: remember the run state of the act that was just entered;
///   2. postfix on RunManager.EnterAct: after the whole act transition finished, push the event room on top of the
///      new act's map with RunManager.EnterRoomWithoutExitingCurrentRoom (the same call events use to start an
///      event combat). When the event ends, its PROCEED button opens the map (NEventRoom.Proceed) and the player
///      walks to the act's Ancient node as usual (entering a map point exits all stacked rooms).
/// Only act indices 1 and 2 (= after the act 1 / act 2 boss), only when a player owns 만검총 and has not finished
/// this act's event yet (run counter <see cref="TombResonanceEvent.DoneKey"/>).
///
/// UNTESTED IN GAME (no game run available): the transition timing and multiplayer behaviour. Known gap: if the game
/// is saved and reloaded while the event is open, the load puts the player on the act map without the event (the
/// run counter stays 0, but no later trigger re-offers it). TODO: re-offer on load / first map node if that matters.
/// </summary>
internal static class TombResonanceTrigger
{
    private static IRunState? _justEntered;

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterActEntered))]
    internal static class AfterActEnteredPatch
    {
        [HarmonyPostfix]
        private static void Remember(IRunState runState) => _justEntered = runState;
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterAct))]
    internal static class EnterActPatch
    {
        [HarmonyPostfix]
        private static void Chain(int currentActIndex, ref Task __result)
        {
            __result = AfterEnterAct(__result, currentActIndex);
        }
    }

    private static async Task AfterEnterAct(Task original, int actIndex)
    {
        await original;
        var runState = _justEntered;
        _justEntered = null;
        try
        {
            if (!ShouldStart(runState, actIndex)) return;
            MainFile.Logger.Info($"[TombResonance] starting the sword event after the act {actIndex} boss");
            await RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(
                new EventRoom(ModelDb.Event<TombResonanceEvent>()), fadeToBlack: true);
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"[TombResonance] could not start the event: {e}");
        }
    }

    private static bool ShouldStart(IRunState? runState, int actIndex)
    {
        if (runState == null || actIndex is not (1 or 2)) return false;
        if (runState.CurrentActIndex != actIndex) return false;
        return runState.Players.Any(p =>
            p.GetRelic<Mangeomchong>() is { } tomb &&
            tomb.GetRunCounter(TombResonanceEvent.DoneKey(actIndex)) == 0);
    }
}
