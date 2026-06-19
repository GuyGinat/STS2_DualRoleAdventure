using System;
using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NRestSiteRoom), nameof(NRestSiteRoom._Ready))]
internal static class NRestSiteRoomReadyGuardPatch
{
    private const int MaxRecoveryAttempts = 6;

    [HarmonyFinalizer]
    private static Exception? Finalizer(NRestSiteRoom __instance, Exception? __exception)
    {
        if (__exception is ArgumentOutOfRangeException)
        {
            int playerCount = ReadPlayerCount(__instance);
            int roomOptions = SafeCountRoomOptions(__instance);
            int localOptions = SafeCountLocalOptions();
            bool isLoading = LocalSelfCoopContext.NetService?.IsGameLoading ?? false;
            string controlledPlayer = LocalContext.NetId?.ToString() ?? "null";
            LocalMultiControlLogger.Warn(
                $"休息区初始化出现越界，已拦截并继续流程: error={__exception.Message}, players={playerCount}, controlled={controlledPlayer}, roomOptions={roomOptions}, localOptions={localOptions}, loading={isLoading}");

            if (playerCount > 4)
            {
                NGame.Instance?.AddChildSafely(NFullscreenTextVfx.Create(LocalModText.RestSiteFocusHint));
                LocalMultiControlLogger.Warn($"[待修复] 休息区5人以上首帧可能不显示选项，已弹出手动切换提示。players={playerCount}");
            }

            Callable.From(delegate
            {
                TryRecoverRestSiteAfterReadyOutOfRange(__instance, attempt: 0, loadingSettledFramesLeft: 2, switchedToPrimary: false);
            }).CallDeferred();

            return null;
        }

        return __exception;
    }

    private static void TryRecoverRestSiteAfterReadyOutOfRange(
        NRestSiteRoom room,
        int attempt,
        int loadingSettledFramesLeft,
        bool switchedToPrimary)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode || !RunManager.Instance.IsInProgress)
        {
            return;
        }

        if (NRestSiteRoom.Instance != room)
        {
            return;
        }

        bool isLoading = LocalSelfCoopContext.NetService?.IsGameLoading ?? false;
        if (isLoading)
        {
            if (attempt >= MaxRecoveryAttempts)
            {
                LocalMultiControlLogger.Warn(
                    $"休息区越界恢复失败：加载状态持续未结束，需手动切人。attempts={attempt + 1}, localOptions={SafeCountLocalOptions()}");
                return;
            }

            Callable.From(delegate
            {
                TryRecoverRestSiteAfterReadyOutOfRange(room, attempt + 1, loadingSettledFramesLeft: 2, switchedToPrimary);
            }).CallDeferred();
            return;
        }

        if (loadingSettledFramesLeft > 0)
        {
            Callable.From(delegate
            {
                TryRecoverRestSiteAfterReadyOutOfRange(room, attempt, loadingSettledFramesLeft - 1, switchedToPrimary);
            }).CallDeferred();
            return;
        }

        if (!switchedToPrimary)
        {
            LocalMultiControlRuntime.SwitchControlledPlayerTo(LocalSelfCoopContext.PrimaryPlayerId, $"rest-site-finalizer-recover-{attempt}");
            switchedToPrimary = true;
        }

        RestSiteUiRefreshUtil.TryRefresh($"rest-site-finalizer-recover-{attempt}");

        int roomOptions = SafeCountRoomOptions(room);
        int localOptions = SafeCountLocalOptions();
        if (localOptions > 0)
        {
            LocalMultiControlLogger.Info(
                $"休息区越界恢复成功：选项已可见。attempt={attempt}, roomOptions={roomOptions}, localOptions={localOptions}, controlled={LocalContext.NetId?.ToString() ?? "null"}");
            return;
        }

        if (attempt >= MaxRecoveryAttempts)
        {
            LocalMultiControlLogger.Warn(
                $"休息区越界恢复结束：仍未显示选项，请手动切人。attempts={attempt + 1}, roomOptions={roomOptions}, localOptions={localOptions}, controlled={LocalContext.NetId?.ToString() ?? "null"}");
            return;
        }

        Callable.From(delegate
        {
            TryRecoverRestSiteAfterReadyOutOfRange(room, attempt + 1, loadingSettledFramesLeft: 1, switchedToPrimary);
        }).CallDeferred();
    }

    private static int ReadPlayerCount(NRestSiteRoom room)
    {
        IRunState? runState = AccessTools.Field(typeof(NRestSiteRoom), "_runState")?.GetValue(room) as IRunState;
        return runState?.Players.Count ?? 0;
    }

    private static int SafeCountRoomOptions(NRestSiteRoom room)
    {
        try
        {
            return room.Options.Count;
        }
        catch
        {
            return -1;
        }
    }

    private static int SafeCountLocalOptions()
    {
        try
        {
            return RunManager.Instance.RestSiteSynchronizer.GetLocalOptions().Count;
        }
        catch
        {
            return -1;
        }
    }
}

[HarmonyPatch(typeof(NTreasureRoomRelicCollection), "get_DefaultFocusedControl")]
internal static class NTreasureRoomRelicCollectionFocusGuardPatch
{
    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception)
    {
        if (__exception is ArgumentOutOfRangeException)
        {
            LocalMultiControlLogger.Warn($"宝箱焦点控件越界，已拦截并跳过该帧聚焦: {__exception.Message}");
            return null;
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(NTreasureRoomRelicHolder), "OnFocus")]
internal static class NTreasureRoomRelicHolderFocusGuardPatch
{
    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception)
    {
        if (__exception is InvalidOperationException exception &&
            exception.Message.Contains("Model was accessed before it was set", StringComparison.Ordinal))
        {
            LocalMultiControlLogger.Warn("宝箱遗物焦点到达时模型尚未就绪，已跳过该帧 Focus。");
            return null;
        }

        return __exception;
    }
}
