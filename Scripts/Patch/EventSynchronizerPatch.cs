using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(EventSynchronizer), nameof(EventSynchronizer.ChooseLocalOption))]
internal static class EventSynchronizerPatch
{
    private static bool _isChoosingSharedEventOption;

    private struct SenderState
    {
        internal bool IsPatched;
        internal ulong? PreviousContextNetId;
        internal ulong PreviousSenderId;
    }

    [HarmonyPrefix]
    private static void Prefix(EventSynchronizer __instance, int index, ref SenderState __state)
    {
        __state = default;
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleEventFlow)
        {
            return;
        }

        if (!__instance.IsShared)
        {
            return;
        }

        INetGameService? netService = AccessTools.Field(typeof(EventSynchronizer), "_netService")?.GetValue(__instance) as INetGameService;
        if (netService is not LocalLoopbackHostGameService loopbackService)
        {
            return;
        }

        __state.IsPatched = true;
        __state.PreviousContextNetId = LocalContext.NetId;
        __state.PreviousSenderId = loopbackService.NetId;

        LocalContext.NetId = LocalSelfCoopContext.PrimaryPlayerId;
        loopbackService.SetCurrentSenderId(LocalSelfCoopContext.PrimaryPlayerId);
    }

    [HarmonyPostfix]
    private static void Postfix(EventSynchronizer __instance, int index, SenderState __state)
    {
        try
        {
            TryAutoProxyEventChoice(__instance, index);
        }
        finally
        {
            if (__state.IsPatched)
            {
                INetGameService? netService = AccessTools.Field(typeof(EventSynchronizer), "_netService")?.GetValue(__instance) as INetGameService;
                if (netService is LocalLoopbackHostGameService loopbackService && loopbackService.NetId != __state.PreviousSenderId)
                {
                    loopbackService.SetCurrentSenderId(__state.PreviousSenderId);
                }

                LocalContext.NetId = __state.PreviousContextNetId;
            }
        }
    }

    private static void TryAutoProxyEventChoice(EventSynchronizer synchronizer, int index)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        if (!synchronizer.IsShared)
        {
            ulong currentPlayerId =
                AccessTools.Field(typeof(EventSynchronizer), "_localPlayerId")?.GetValue(synchronizer) as ulong?
                ?? LocalMultiControlRuntime.SessionState.CurrentControlledPlayerId
                ?? 0UL;
            if (currentPlayerId != 0)
            {
                LocalSelfCoopContext.RequestEventAutoSwitchAfterChoice(currentPlayerId);
            }

            return;
        }

        try
        {
            INetGameService? netService = AccessTools.Field(typeof(EventSynchronizer), "_netService")?.GetValue(synchronizer) as INetGameService;
            if (netService is not LocalLoopbackHostGameService)
            {
                return;
            }

            IPlayerCollection? playerCollection = AccessTools.Field(typeof(EventSynchronizer), "_playerCollection")?.GetValue(synchronizer) as IPlayerCollection;
            List<uint?>? votes = AccessTools.Field(typeof(EventSynchronizer), "_playerVotes")?.GetValue(synchronizer) as List<uint?>;
            if (playerCollection == null || votes == null)
            {
                return;
            }

            int sharedCount = Math.Min(playerCollection.Players.Count, votes.Count);
            if (sharedCount < 2)
            {
                return;
            }

            // 若共享事件已结算并清空投票（例如最后一票触发了原版结算），这里直接跳过，
            // 避免再次写票导致重复触发并把控制上下文切乱。
            if (!votes.Take(sharedCount).Any((vote) => vote.HasValue))
            {
                return;
            }

            List<Player> players = playerCollection.Players.Take(sharedCount).ToList();
            ulong localPlayerId = LocalMultiControlRuntime.SessionState.CurrentControlledPlayerId
                ?? LocalContext.NetId
                ?? LocalSelfCoopContext.PrimaryPlayerId;

            int localSlot = players.FindIndex((player) => player.NetId == localPlayerId);
            if (localSlot < 0)
            {
                localSlot = players.FindIndex((player) => player.NetId == LocalSelfCoopContext.PrimaryPlayerId);
            }

            if (localSlot < 0)
            {
                return;
            }

            uint selectedOption = (uint)index;
            votes[localSlot] = selectedOption;
            int filledCount = 1;
            for (int i = 0; i < sharedCount; i++)
            {
                if (i == localSlot)
                {
                    continue;
                }

                if (!votes[i].HasValue || votes[i]!.Value != selectedOption)
                {
                    votes[i] = selectedOption;
                }

                filledCount++;
            }

            LocalMultiControlLogger.Info($"共享事件自动补齐投票: option={index}, filled={filledCount}/{sharedCount}");
            if (votes.Take(sharedCount).All((vote) => vote.HasValue) && netService.Type != NetGameType.Client)
            {
                TryChooseSharedEventOptionDeferred(synchronizer);
            }
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"共享事件自动补票失败: {exception.Message}");
        }
    }

    private static void TryChooseSharedEventOptionDeferred(EventSynchronizer synchronizer)
    {
        if (_isChoosingSharedEventOption)
        {
            LocalMultiControlLogger.Warn("共享事件结算已在进行中，跳过重复触发。");
            return;
        }

        _isChoosingSharedEventOption = true;
        Callable.From(delegate
        {
            try
            {
                AccessTools.Method(typeof(EventSynchronizer), "ChooseSharedEventOption")?.Invoke(synchronizer, Array.Empty<object>());
                LocalMultiControlLogger.Info("共享事件自动补票完成，已触发结算。");
            }
            catch (Exception exception)
            {
                LocalMultiControlLogger.Warn($"共享事件结算触发失败: {exception.Message}");
            }
            finally
            {
                _isChoosingSharedEventOption = false;
            }
        }).CallDeferred();
    }
}

[HarmonyPatch(typeof(EventSynchronizer), "ChooseOptionForEvent")]
internal static class EventSynchronizerChooseOptionForEventPatch
{
    private static readonly SemaphoreSlim SharedEventChoiceSemaphore = new(1, 1);

    [HarmonyPrefix]
    private static bool Prefix(EventSynchronizer __instance, Player player, int optionIndex)
    {
        if (!LocalSelfCoopContext.IsEnabled || !RunManager.Instance.IsInProgress || !__instance.IsShared)
        {
            return true;
        }

        if (RunManager.Instance.NetService is not LocalLoopbackHostGameService)
        {
            return true;
        }

        EventModel eventForPlayer = __instance.GetEventForPlayer(player);
        if (eventForPlayer.IsFinished)
        {
            throw new InvalidOperationException($"Option chosen for player {player} on {eventForPlayer}, but it is already finished!");
        }

        if (optionIndex < 0 || optionIndex >= eventForPlayer.CurrentOptions.Count)
        {
            throw new InvalidOperationException(
                $"Player {player.NetId} attempted to choose option index {optionIndex} in event {eventForPlayer.Id}, but there were only {eventForPlayer.CurrentOptions.Count} options available!");
        }

        EventOption eventOption = eventForPlayer.CurrentOptions[optionIndex];
        AccessTools.Method(typeof(EventSynchronizer), "SaveEventOptionToHistory")
            ?.Invoke(__instance, new object[] { player, eventOption });

        TaskHelper.RunSafely(ExecuteSharedOptionSeriallyAsync(player, eventOption));
        return false;
    }

    private static async Task ExecuteSharedOptionSeriallyAsync(Player player, EventOption eventOption)
    {
        await SharedEventChoiceSemaphore.WaitAsync();
        try
        {
            if (!RunManager.Instance.IsInProgress)
            {
                return;
            }

            LocalMultiControlRuntime.SwitchControlledPlayerTo(player.NetId, "event-shared-serial-execution");
            LocalMultiControlLogger.Info($"共享事件开始执行角色选项: player={player.NetId}, key={eventOption.TextKey}");
            await eventOption.Chosen();
            LocalMultiControlLogger.Info($"共享事件角色选项执行完成: player={player.NetId}, key={eventOption.TextKey}");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"共享事件角色选项执行失败: player={player.NetId}, key={eventOption.TextKey}, error={exception.Message}");
        }
        finally
        {
            SharedEventChoiceSemaphore.Release();
        }
    }
}
