using HarmonyLib;
using Mumyeong.MumyeongCode.Relics;
using Mumyeong.MumyeongCode.Stages;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Mumyeong.MumyeongCode.Events;

/// <summary>
/// Forces the mod's events (pattern and verified game flow: 엔시페르 TombResonanceTrigger).
///  1. Run start: when the starting Ancient's (Neow's) PROCEED would open the map, the 황천 영사실
///     (<see cref="TheaterEvent"/>) opens first while a 코어 still has no figure. A run started without Neow gets it
///     right after the first act is entered.
///  2. Stage-ups: when a stage-up is ready (<see cref="StageRules.NextReadyStage"/>), <see cref="StageUpEvent"/> opens
///     right after a new act is entered and, as a safety net, at the PROCEED of any event (? rooms, the act's Ancient).
/// The event room is pushed on top with RunManager.EnterRoomWithoutExitingCurrentRoom; its own PROCEED opens the map.
///
/// Save/reload (from the decompiled game, untested in game): pushing an event room does not save. With Neow, the last
/// save is the one made when Neow finished (EventRoom saves itself as pre-finished), so a reload during the theater
/// lands in Neow's finished room and its PROCEED (patch 1) opens the theater again. Without Neow there is no map point
/// yet and the load (RunManager.LoadIntoLatestMapCoord) only opens the map, so <see cref="LoadPatch"/> reopens it.
/// A stage-up event lost to a reload is offered again by the next ? event, rest site or act change (the 코어 data
/// still says it is ready).
/// UNTESTED IN GAME: transition timing and multiplayer.
/// </summary>
internal static class EventTriggers
{
    private static IRunState? _justEntered;

    private static IRunState? CurrentRunState() =>
        AccessTools.Property(typeof(RunManager), "State")?.GetValue(RunManager.Instance) as IRunState;

    private static bool NeedsTheater(IRunState? runState) =>
        runState != null && runState.Players.Any(p =>
            p.GetRelic<SoulCore>() is { Figure: null } core && core.GetRunCounter(TheaterEvent.DoneKey) == 0);

    private static bool NeedsStageUp(IRunState? runState) =>
        runState != null && runState.Players.Any(p =>
            p.GetRelic<SoulCore>() is { } core && StageRules.NextReadyStage(core) != null);

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
            if (runState == null || runState.CurrentActIndex != actIndex) return;
            if (actIndex == 0 && !runState.ExtraFields.StartedWithNeow && NeedsTheater(runState))
            {
                MainFile.Logger.Info("[Mumyeong] run without Neow: opening the theater");
                await RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(
                    new EventRoom(ModelDb.Event<TheaterEvent>()), fadeToBlack: true);
                return;
            }

            if (actIndex > 0 && NeedsStageUp(runState))
            {
                MainFile.Logger.Info($"[Mumyeong] act {actIndex}: stage-up ready");
                await RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(
                    new EventRoom(ModelDb.Event<StageUpEvent>()), fadeToBlack: true);
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"[Mumyeong] act event failed: {e}");
        }
    }

    /// <summary>Reload of a run that has no map point yet (no Neow) while the theater was still owed.</summary>
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.LoadIntoLatestMapCoord))]
    internal static class LoadPatch
    {
        [HarmonyPostfix]
        private static void Chain(ref Task __result)
        {
            __result = AfterLoad(__result);
        }
    }

    private static async Task AfterLoad(Task original)
    {
        await original;
        try
        {
            var runState = CurrentRunState();
            if (runState is not RunState { CurrentActIndex: 0, VisitedMapCoords.Count: 0 } || !NeedsTheater(runState)) return;
            MainFile.Logger.Info("[Mumyeong] reload before the first map point: opening the theater");
            await RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(
                new EventRoom(ModelDb.Event<TheaterEvent>()), fadeToBlack: true);
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"[Mumyeong] load event failed: {e}");
        }
    }

    [HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
    internal static class ProceedPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref Task __result)
        {
            try
            {
                var runState = CurrentRunState();
                if (NeedsTheater(runState))
                {
                    MainFile.Logger.Info("[Mumyeong] opening the theater");
                    __result = RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(
                        new EventRoom(ModelDb.Event<TheaterEvent>()), fadeToBlack: true);
                    return false;
                }

                if (NeedsStageUp(runState))
                {
                    MainFile.Logger.Info("[Mumyeong] opening a stage-up");
                    __result = RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(
                        new EventRoom(ModelDb.Event<StageUpEvent>()), fadeToBlack: true);
                    return false;
                }
            }
            catch (Exception e)
            {
                MainFile.Logger.Error($"[Mumyeong] proceed hook failed: {e}");
            }

            return true;
        }
    }
}
