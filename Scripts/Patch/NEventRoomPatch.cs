using System.Linq;
using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NEventRoom), "RefreshEventState")]
internal static class NEventRoomPatch
{
    [HarmonyPostfix]
    private static void Postfix(NEventRoom __instance, EventModel eventModel)
    {
        if (LocalSelfCoopContext.UseSingleEventFlow)
        {
            return;
        }

        if (TryAutoSwitchToNextPendingEvent(eventModel))
        {
            return;
        }

        if (!LocalSelfCoopContext.ShouldQueueEventAutoSwitchAfterEventState(eventModel))
        {
            return;
        }

        if (NOverlayStack.Instance?.ScreenCount > 0)
        {
            LocalMultiControlLogger.Info("事件流程已完成，等待奖励/选择弹窗关闭后自动切换角色。");
            return;
        }

        Callable.From(delegate
        {
            if (RunManager.Instance.IsInProgress)
            {
                LocalMultiControlRuntime.TryRunPendingEventAutoSwitch("event-auto-next");
            }
        }).CallDeferred();
    }

    private static bool TryAutoSwitchToNextPendingEvent(EventModel eventModel)
    {
        if (RunManager.Instance.EventSynchronizer.IsShared)
        {
            return false;
        }

        if (eventModel.Owner == null || !eventModel.IsFinished)
        {
            return false;
        }

        EventModel? pendingEvent = RunManager.Instance.EventSynchronizer.Events.FirstOrDefault((candidate) =>
            candidate.Owner != null &&
            candidate.Owner.NetId != eventModel.Owner.NetId &&
            !candidate.IsFinished);
        if (pendingEvent?.Owner == null)
        {
            return false;
        }

        if (NOverlayStack.Instance?.ScreenCount > 0)
        {
            LocalMultiControlLogger.Info($"事件已完成，等待弹窗关闭后自动切换到下一位: {eventModel.Owner.NetId} -> {pendingEvent.Owner.NetId}");
            return false;
        }

        Callable.From(delegate
        {
            if (!RunManager.Instance.IsInProgress)
            {
                return;
            }

            LocalMultiControlLogger.Info($"事件自动切换到下一位待选角色: {eventModel.Owner.NetId} -> {pendingEvent.Owner.NetId}");
            LocalMultiControlRuntime.SwitchControlledPlayerTo(pendingEvent.Owner.NetId, "event-finished-next-player");
        }).CallDeferred();
        return true;
    }
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.OptionButtonClicked))]
internal static class NEventRoomOptionButtonPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NEventRoom __instance, EventOption option)
    {
        // 已在实机验证：该拦截用于保证涅奥/非共享事件必须双角色都完成后才可 Proceed。
        // 这里是开局主流程稳定点，后续若需调整请先做日志回归，避免回归到“仅一人可选”。
        if (!LocalSelfCoopContext.IsEnabled || !option.IsProceed || !RunManager.Instance.IsInProgress)
        {
            return true;
        }

        if (LocalSelfCoopContext.UseSingleEventFlow)
        {
            return true;
        }

        if (RunManager.Instance.EventSynchronizer.IsShared)
        {
            return true;
        }

        EventModel? currentEvent = AccessTools.Field(typeof(NEventRoom), "_event")?.GetValue(__instance) as EventModel;
        if (currentEvent?.Owner == null || !currentEvent.IsFinished)
        {
            return true;
        }

        EventModel? pendingEvent = RunManager.Instance.EventSynchronizer.Events.FirstOrDefault((eventModel) =>
            eventModel.Owner != null &&
            eventModel.Owner.NetId != currentEvent.Owner.NetId &&
            !eventModel.IsFinished);
        if (pendingEvent?.Owner == null)
        {
            return true;
        }

        LocalMultiControlLogger.Info($"检测到另一名角色尚未完成事件，拦截 Proceed 并切换到 player={pendingEvent.Owner.NetId}");
        LocalMultiControlRuntime.SwitchControlledPlayerTo(pendingEvent.Owner.NetId, "event-proceed-next-player");
        return false;
    }
}
