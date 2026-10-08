using HarmonyLib;
using MagicSwordsman.MagicSwordsmanCode.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
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

    /// <summary>
    /// Run start (사용자 결정 2026-10-04): when the starting Ancient event ends and its PROCEED would open the map,
    /// open the start sword pick (TombResonanceEvent in act 0) first. Same for acts 2 and 3 when the pick pushed at
    /// the act start was lost (see <see cref="ShouldStartAtRunStart"/>). Its own PROCEED then finds the pick done and
    /// opens the map normally. UNTESTED IN GAME.
    /// </summary>
    [HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
    internal static class StartAfterAncientPatch
    {
        private static readonly System.Reflection.PropertyInfo? StateProp = AccessTools.Property(typeof(RunManager), "State");

        [HarmonyPrefix]
        private static bool Prefix(ref Task __result)
        {
            try
            {
                var runState = StateProp?.GetValue(RunManager.Instance) as IRunState;
                if (!ShouldStartAtRunStart(runState)) return true;
                MainFile.Logger.Info("[TombResonance] starting the run-start sword pick");
                __result = RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(
                    new EventRoom(ModelDb.Event<TombResonanceEvent>()), fadeToBlack: true);
                return false;
            }
            catch (Exception e)
            {
                MainFile.Logger.Error($"[TombResonance] run-start pick failed: {e}");
                return true;
            }
        }
    }

    /// <summary>
    /// Also the safety net for acts 2 and 3 (bug report 2026-10-08): the pick pushed by <see cref="AfterEnterAct"/>
    /// sits on top of the act map, and clicking the Ancient node before it shows up exits every stacked room, so the
    /// pick was lost for good. Now any event PROCEED in an act whose pick is still not done (normally the Ancient's)
    /// opens it.
    /// </summary>
    private static bool ShouldStartAtRunStart(IRunState? runState)
    {
        if (runState == null || runState.CurrentActIndex is not (0 or 1 or 2)) return false;
        var act = runState.CurrentActIndex;
        return runState.Players.Any(p =>
            p.GetRelic<Mangeomchong>() is { } tomb &&
            tomb.GetRunCounter(TombResonanceEvent.DoneKey(act)) == 0);
    }

    private static bool ShouldStart(IRunState? runState, int actIndex)
    {
        if (runState == null || actIndex is not (0 or 1 or 2)) return false;
        if (runState.CurrentActIndex != actIndex) return false;
        // Run start with Neow: the starting Ancient room is open now; StartAfterAncientPatch starts the pick instead.
        if (actIndex == 0 && runState.ExtraFields.StartedWithNeow) return false;
        return runState.Players.Any(p =>
            p.GetRelic<Mangeomchong>() is { } tomb &&
            tomb.GetRunCounter(TombResonanceEvent.DoneKey(actIndex)) == 0);
    }
}
