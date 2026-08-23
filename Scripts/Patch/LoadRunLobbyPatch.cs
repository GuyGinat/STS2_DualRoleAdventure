using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(LoadRunLobby), nameof(LoadRunLobby.SetReady))]
internal static class LoadRunLobbyPatch
{
    [HarmonyPostfix]
    private static void Postfix(LoadRunLobby __instance, bool ready)
    {
        if (__instance.NetService is not LocalLoopbackHostGameService || !LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        List<ulong> localPlayerIdsInRun = __instance.Run.Players
            .Select((player) => player.NetId)
            .Where((id) => LocalSelfCoopContext.LocalPlayerIds.Contains(id))
            .Distinct()
            .ToList();
        if (localPlayerIdsInRun.Count <= 1)
        {
            return;
        }

        // v0.111.0: LoadRunLobby no longer keeps ConnectedPlayerIds/_readyPlayers sets;
        // membership and readiness both live in the public Players list of
        // LoadRunLobbyPlayer structs, so we mirror the host's ready state onto
        // every other local character there.
        List<LoadRunLobbyPlayer> players = __instance.Players;
        ulong localHostId = __instance.NetService.NetId;
        bool isModded = __instance.NetService.LocalVersion.IsModded();

        foreach (ulong playerId in localPlayerIdsInRun)
        {
            if (playerId == localHostId)
            {
                continue;
            }

            int index = players.FindIndex((player) => player.id == playerId);
            if (index < 0)
            {
                LoadRunLobbyPlayer newPlayer = new LoadRunLobbyPlayer
                {
                    id = playerId,
                    isModded = isModded,
                    isReady = ready,
                };
                players.Add(newPlayer);
                __instance.LobbyListener.PlayerConnected(newPlayer);
                if (ready)
                {
                    __instance.LobbyListener.PlayerReadyChanged(playerId);
                }

                continue;
            }

            LoadRunLobbyPlayer existing = players[index];
            if (existing.isReady == ready)
            {
                continue;
            }

            existing.isReady = ready;
            players[index] = existing;
            __instance.LobbyListener.PlayerReadyChanged(playerId);
        }

        if (ready)
        {
            InvokeBeginRunIfAllPlayersReady(__instance);
            LocalMultiControlLogger.Info($"本地多控读档自动就绪: players={string.Join(",", localPlayerIdsInRun)}");
        }
    }

    private static void InvokeBeginRunIfAllPlayersReady(LoadRunLobby lobby)
    {
        if (AccessTools.Method(typeof(LoadRunLobby), "BeginRunForAllPlayersIfAllReady") is { } beginRunNew)
        {
            beginRunNew.Invoke(lobby, new object[] { });
            return;
        }

        if (AccessTools.Method(typeof(LoadRunLobby), "BeginRunIfAllPlayersReady") is { } beginRunLegacy)
        {
            beginRunLegacy.Invoke(lobby, new object[] { });
            return;
        }

        LocalMultiControlLogger.Warn("读档自动开局失败：未找到 BeginRunIfAllPlayersReady/BeginRunForAllPlayersIfAllReady。");
    }
}
