using System;
using System.Reflection;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.SetReady))]
internal static class StartRunLobbySetReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix(StartRunLobby __instance, bool ready)
    {
        if (!ready || __instance.NetService is not LocalLoopbackHostGameService)
        {
            return;
        }

        bool hasChange = false;
        for (int i = 0; i < __instance.Players.Count; i++)
        {
            StartRunLobbyPlayer player = __instance.Players[i];
            if (!player.isReady)
            {
                player.isReady = true;
                __instance.Players[i] = player;
                __instance.LobbyListener.PlayerChanged(player, false);
                hasChange = true;
            }
        }

        if (hasChange)
        {
            LocalMultiControlLogger.Info("本地双人模式自动就绪：已将全部玩家标记为 ready。");
        }

        bool beginningRun = AccessTools.Field(typeof(StartRunLobby), "_isBeginningRun")?.GetValue(__instance) as bool? ?? false;
        if (!beginningRun)
        {
            MethodInfo? beginRunIfReadyMethod =
                AccessTools.Method(typeof(StartRunLobby), "BeginRunForAllPlayersIfAllReady")
                ?? AccessTools.Method(typeof(StartRunLobby), "BeginRunIfAllPlayersReady");
            beginRunIfReadyMethod?.Invoke(__instance, Array.Empty<object>());
        }
    }
}
