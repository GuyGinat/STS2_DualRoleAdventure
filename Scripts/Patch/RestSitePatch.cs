using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Generate))]
internal static class RestSiteOptionPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref List<RestSiteOption> __result)
    {
        // 需求调整：休息区保留原多人联机选项，不再删减。
    }
}

[HarmonyPatch(typeof(HealRestSiteOption), nameof(HealRestSiteOption.OnSelect))]
internal static class HealRestSiteOptionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(HealRestSiteOption __instance, ref Task<bool> __result)
    {
        // 需求调整：休息区回血按角色独立结算，不再拦截为全体恢复。
        return true;
    }
}

[HarmonyPatch(typeof(RestSiteSynchronizer), nameof(RestSiteSynchronizer.ChooseLocalOption))]
internal static class RestSiteSynchronizerChooseLocalOptionPatch
{
    [HarmonyPostfix]
    private static void Postfix(RestSiteSynchronizer __instance, int index, ref Task<bool> __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        __result = WrapChooseLocalOptionAsync(__instance, index, __result);
    }

    private static async Task<bool> WrapChooseLocalOptionAsync(RestSiteSynchronizer synchronizer, int optionIndex, Task<bool> originalTask)
    {
        ulong? localPlayerId = LocalContext.NetId;
        if (!localPlayerId.HasValue)
        {
            localPlayerId = ReadLocalPlayerIdFromSynchronizer(synchronizer);
        }

        List<RestSiteOption> sourceOptionsSnapshot = new List<RestSiteOption>();
        if (localPlayerId.HasValue)
        {
            sourceOptionsSnapshot = synchronizer.GetOptionsForPlayer(localPlayerId.Value).ToList();
        }

        bool success = await originalTask;
        if (!localPlayerId.HasValue)
        {
            LocalMultiControlLogger.Warn($"休息区升级切换失败：无法识别当前本地角色，optionIndex={optionIndex}");
            return success;
        }

        if (!success)
        {
            LocalMultiControlLogger.Warn(
                $"休息区选项执行失败，不触发自动切人: player={localPlayerId.Value}, optionIndex={optionIndex}, snapshot={DescribeOptions(sourceOptionsSnapshot)}");
            return success;
        }

        if (TryFindNextSelectablePlayer(synchronizer, localPlayerId.Value, out ulong nextPlayerId))
        {
            LocalMultiControlLogger.Info(
                $"休息区选择成功，已排队切换到下一位待选角色（不代选）: {localPlayerId.Value} -> {nextPlayerId}, optionIndex={optionIndex}, snapshot={DescribeOptions(sourceOptionsSnapshot)}");
            Callable.From(delegate
            {
                RestSiteAutoSwitchUtil.SwitchToPlayerAndEnsureOptions(nextPlayerId, "rest-site-next-player-choice");
            }).CallDeferred();
        }
        else
        {
            LocalMultiControlLogger.Info(
                $"休息区选择完成：所有可选角色都已选择。player={localPlayerId.Value}, optionIndex={optionIndex}");
            Callable.From(delegate
            {
                RestSiteAutoSwitchUtil.ShowAllPlayersSelectedNotice();
            }).CallDeferred();
        }

        return success;
    }

    private static bool TryFindNextSelectablePlayer(RestSiteSynchronizer synchronizer, ulong currentPlayerId, out ulong nextPlayerId)
    {
        IReadOnlyList<ulong> orderedPlayerIds = LocalMultiControlRuntime.SessionState.OrderedPlayerIds;
        if (orderedPlayerIds.Count < 2)
        {
            nextPlayerId = 0;
            return false;
        }

        int currentIndex = IndexOfPlayer(orderedPlayerIds, currentPlayerId);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        for (int step = 1; step < orderedPlayerIds.Count; step++)
        {
            int index = (currentIndex + step) % orderedPlayerIds.Count;
            ulong candidatePlayerId = orderedPlayerIds[index];
            if (candidatePlayerId == currentPlayerId)
            {
                continue;
            }

            IReadOnlyList<RestSiteOption> candidateOptions = synchronizer.GetOptionsForPlayer(candidatePlayerId);
            if (candidateOptions.Count > 0)
            {
                nextPlayerId = candidatePlayerId;
                return true;
            }
        }

        nextPlayerId = 0;
        return false;
    }

    private static int IndexOfPlayer(IReadOnlyList<ulong> orderedPlayerIds, ulong playerId)
    {
        for (int i = 0; i < orderedPlayerIds.Count; i++)
        {
            if (orderedPlayerIds[i] == playerId)
            {
                return i;
            }
        }

        return -1;
    }

    private static ulong? ReadLocalPlayerIdFromSynchronizer(RestSiteSynchronizer synchronizer)
    {
        object? fieldValue = AccessTools.Field(typeof(RestSiteSynchronizer), "_localPlayerId")?.GetValue(synchronizer);
        if (fieldValue is ulong fieldPlayerId)
        {
            return fieldPlayerId;
        }

        return null;
    }

    private static string DescribeOptions(IReadOnlyList<RestSiteOption> options)
    {
        if (options.Count == 0)
        {
            return "[]";
        }

        return "[" + string.Join(", ", options.Select((option, index) => $"{index}:{option.GetType().Name}")) + "]";
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "AfterSelectingOptionAsync")]
internal static class NRestSiteRoomAfterSelectingOptionPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref Task __result)
    {
        // 选择后的自动切人由 RestSiteSynchronizerChooseLocalOptionPatch 统一处理。
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "OnPlayerChangedHoveredRestSiteOption")]
internal static class NRestSiteRoomHoverGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NRestSiteRoom __instance, ulong playerId)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return true;
        }

        IRunState? runState = AccessTools.Field(typeof(NRestSiteRoom), "_runState")?.GetValue(__instance) as IRunState;
        if (runState == null || runState.Players.Count <= 1)
        {
            return false;
        }

        NRestSiteCharacter? character = __instance.Characters.FirstOrDefault((candidate) => candidate.Player.NetId == playerId);
        if (character == null)
        {
            return false;
        }

        int? hoveredOptionIndex = RunManager.Instance.RestSiteSynchronizer.GetHoveredOptionIndex(playerId);
        RestSiteOption? option = null;
        if (hoveredOptionIndex.HasValue)
        {
            IReadOnlyList<RestSiteOption> options = RunManager.Instance.RestSiteSynchronizer.GetOptionsForPlayer(playerId);
            if (hoveredOptionIndex.Value >= 0 && hoveredOptionIndex.Value < options.Count)
            {
                option = options[hoveredOptionIndex.Value];
            }
            else
            {
                LocalMultiControlLogger.Warn($"休息区悬停索引越界，已忽略: player={playerId}, index={hoveredOptionIndex.Value}, options={options.Count}");
            }
        }

        character.ShowHoveredRestSiteOption(option);
        return false;
    }
}

[HarmonyPatch(typeof(NRestSiteButton), "SelectOption")]
internal static class NRestSiteButtonSelectGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(RestSiteOption option, ref Task __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return true;
        }

        NRestSiteRoom? room = NRestSiteRoom.Instance;
        if (room == null)
        {
            return true;
        }

        if (FindOptionIndex(room.Options, option) >= 0)
        {
            return true;
        }

        RestSiteUiRefreshUtil.TryRefresh("button-option-mismatch");
        room = NRestSiteRoom.Instance;
        if (room != null && FindOptionIndex(room.Options, option) >= 0)
        {
            return true;
        }

        RunManager.Instance.RestSiteSynchronizer.LocalOptionHovered(null);
        LocalMultiControlLogger.Warn("休息区按钮与当前选项列表不一致，已拒绝本次点击并刷新。");
        __result = Task.CompletedTask;
        return false;
    }

    private static int FindOptionIndex(IReadOnlyList<RestSiteOption> options, RestSiteOption target)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == target)
            {
                return i;
            }
        }

        return -1;
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), nameof(NRestSiteRoom._Ready))]
internal static class NRestSiteRoomReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix(NRestSiteRoom __instance)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        Callable.From(delegate
        {
            EnsurePrimaryPlayerOptionsVisible(__instance, attempt: 0, loadingSettledFramesLeft: 2, switchedToPrimary: false);
        }).CallDeferred();
    }

    private static void EnsurePrimaryPlayerOptionsVisible(
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
            Callable.From(delegate
            {
                EnsurePrimaryPlayerOptionsVisible(room, attempt, loadingSettledFramesLeft: 2, switchedToPrimary);
            }).CallDeferred();
            return;
        }

        if (loadingSettledFramesLeft > 0)
        {
            Callable.From(delegate
            {
                EnsurePrimaryPlayerOptionsVisible(room, attempt, loadingSettledFramesLeft - 1, switchedToPrimary);
            }).CallDeferred();
            return;
        }

        if (!switchedToPrimary)
        {
            LocalMultiControlRuntime.SwitchControlledPlayerTo(LocalSelfCoopContext.PrimaryPlayerId, $"rest-site-enter-primary-{attempt}");
            switchedToPrimary = true;
        }

        RestSiteUiRefreshUtil.TryRefresh($"rest-site-enter-primary-{attempt}");

        int optionCount = room.Options.Count;
        int localOptionCount = RunManager.Instance.RestSiteSynchronizer.GetLocalOptions().Count;
        if (optionCount > 0 || localOptionCount > 0 || attempt >= 6)
        {
            LocalMultiControlLogger.Info(
                $"休息区进入后选项检查: attempt={attempt}, options={optionCount}, localOptions={localOptionCount}, switchedToPrimary={switchedToPrimary}");
            return;
        }

        Callable.From(delegate
        {
            EnsurePrimaryPlayerOptionsVisible(room, attempt + 1, loadingSettledFramesLeft: 1, switchedToPrimary);
        }).CallDeferred();
    }
}

internal static class RestSiteAutoSwitchUtil
{
    private const int MaxRefreshAttempts = 5;

    internal static void SwitchToPlayerAndEnsureOptions(ulong targetPlayerId, string source)
    {
        EnsureOptionsAfterSwitch(targetPlayerId, source, attempt: 0, switched: false);
    }

    internal static void ShowAllPlayersSelectedNotice()
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        NGame.Instance?.AddChildSafely(NFullscreenTextVfx.Create(LocalModText.RestSiteAllChosen));
        LocalMultiControlLogger.Info("休息区提示文本已弹出：所有可选角色都已选择。");
    }

    private static void EnsureOptionsAfterSwitch(ulong targetPlayerId, string source, int attempt, bool switched)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode || !RunManager.Instance.IsInProgress)
        {
            return;
        }

        NRestSiteRoom? room = NRestSiteRoom.Instance;
        if (room == null)
        {
            return;
        }

        if (!switched)
        {
            LocalMultiControlRuntime.SwitchControlledPlayerTo(targetPlayerId, source);
            switched = true;
        }

        RestSiteUiRefreshUtil.TryRefresh($"{source}-attempt-{attempt}");

        int targetOptionCount = RunManager.Instance.RestSiteSynchronizer.GetOptionsForPlayer(targetPlayerId).Count;
        int localOptionCount = RunManager.Instance.RestSiteSynchronizer.GetLocalOptions().Count;
        if (targetOptionCount > 0 && localOptionCount > 0)
        {
            LocalMultiControlLogger.Info(
                $"休息区已自动切换到下一位待选角色并刷新成功: target={targetPlayerId}, attempt={attempt}, targetOptions={targetOptionCount}, localOptions={localOptionCount}");
            return;
        }

        if (attempt >= MaxRefreshAttempts)
        {
            LocalMultiControlLogger.Warn(
                $"休息区自动切换后仍未恢复选项显示: target={targetPlayerId}, attempts={attempt + 1}, targetOptions={targetOptionCount}, localOptions={localOptionCount}");
            return;
        }

        Callable.From(delegate
        {
            EnsureOptionsAfterSwitch(targetPlayerId, source, attempt + 1, switched);
        }).CallDeferred();
    }
}

internal static class RestSiteUiRefreshUtil
{
    internal static bool TryRefresh(string source)
    {
        NRestSiteRoom? room = NRestSiteRoom.Instance;
        if (room == null)
        {
            return false;
        }

        try
        {
            RunManager.Instance.RestSiteSynchronizer.LocalOptionHovered(null);
            AccessTools.Field(typeof(NRestSiteRoom), "_lastFocused")?.SetValue(room, null);
            AccessTools.Method(typeof(NRestSiteRoom), "UpdateRestSiteOptions")?.Invoke(room, null);
            EnsureChoicesVisibleForLocalPlayer(room, source);
            LocalMultiControlLogger.Info($"休息区选项已刷新: source={source}, player={LocalContext.NetId?.ToString() ?? "null"}");
            return true;
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"休息区选项刷新失败: source={source}, error={exception.Message}");
            return false;
        }
    }

    internal static void EnsureChoicesVisibleForLocalPlayer(NRestSiteRoom room, string source)
    {
        try
        {
            int localOptionCount = RunManager.Instance.RestSiteSynchronizer.GetLocalOptions().Count;
            if (localOptionCount <= 0)
            {
                return;
            }

            Control? choicesScreen = AccessTools.Field(typeof(NRestSiteRoom), "_choicesScreen")?.GetValue(room) as Control;
            if (choicesScreen != null)
            {
                Color modulate = choicesScreen.Modulate;
                if (modulate.A < 0.99f)
                {
                    modulate.A = 1f;
                    choicesScreen.Modulate = modulate;
                }
            }

            AccessTools.Method(typeof(NRestSiteRoom), "EnableOptions")?.Invoke(room, null);
            AccessTools.Method(typeof(NRestSiteRoom), "AnimateDescriptionUp")?.Invoke(room, null);
            EnsureControllerFocus(room, source);
            LocalMultiControlLogger.Info($"休息区选项可见性已恢复: source={source}, options={localOptionCount}");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"恢复休息区选项可见性失败: source={source}, error={exception.Message}");
        }
    }

    private static void EnsureControllerFocus(NRestSiteRoom room, string source)
    {
        if (!(NControllerManager.Instance?.IsUsingController ?? false))
        {
            return;
        }

        try
        {
            AccessTools.Method(typeof(NRestSiteRoom), "UpdateNavigation")?.Invoke(room, null);
            Control? focusTarget = FindFirstFocusableRestSiteButton(room) ?? FindFirstFocusableControl(room);
            if (focusTarget == null)
            {
                LocalMultiControlLogger.Warn($"休息区手柄焦点恢复失败：未找到可聚焦控件。source={source}");
                return;
            }

            focusTarget.GrabFocus();
            LocalMultiControlLogger.Info($"休息区手柄焦点已恢复: source={source}, target={focusTarget.Name}");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"恢复休息区手柄焦点失败: source={source}, error={exception.Message}");
        }
    }

    private static Control? FindFirstFocusableRestSiteButton(Node root)
    {
        foreach (Node node in EnumerateDescendants(root))
        {
            if (node is NRestSiteButton button &&
                button.Visible &&
                button.FocusMode != Control.FocusModeEnum.None)
            {
                return button;
            }
        }

        return null;
    }

    private static Control? FindFirstFocusableControl(Node root)
    {
        foreach (Node node in EnumerateDescendants(root))
        {
            if (node is Control control &&
                control.Visible &&
                control.FocusMode != Control.FocusModeEnum.None)
            {
                return control;
            }
        }

        return null;
    }

    private static IEnumerable<Node> EnumerateDescendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (Node nested in EnumerateDescendants(child))
            {
                yield return nested;
            }
        }
    }
}
