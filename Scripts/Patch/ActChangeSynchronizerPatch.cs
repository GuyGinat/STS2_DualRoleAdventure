using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(ActChangeSynchronizer), nameof(ActChangeSynchronizer.SetLocalPlayerReady))]
internal static class ActChangeSynchronizerPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActChangeSynchronizer __instance)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        if (RunManager.Instance.NetService is not LocalLoopbackHostGameService)
        {
            return;
        }

        RunState? runState = AccessTools.Field(typeof(ActChangeSynchronizer), "_runState")?.GetValue(__instance) as RunState;
        if (runState == null || runState.Players.Count < 2)
        {
            return;
        }

        ulong? localNetId = LocalContext.NetId;
        if (!localNetId.HasValue)
        {
            return;
        }

        List<bool>? readyPlayers = AccessTools.Field(typeof(ActChangeSynchronizer), "_readyPlayers")?.GetValue(__instance) as List<bool>;
        List<Player> pendingPlayers = runState.Players
            .Where((player) => player.NetId != localNetId.Value)
            .Where((player) =>
            {
                if (readyPlayers == null)
                {
                    return true;
                }

                int slot = runState.GetPlayerSlotIndex(player);
                return slot < 0 || slot >= readyPlayers.Count || !readyPlayers[slot];
            })
            .ToList();
        if (pendingPlayers.Count == 0)
        {
            return;
        }

        foreach (Player mirroredPlayer in pendingPlayers)
        {
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new VoteToMoveToNextActAction(mirroredPlayer));
        }

        LocalMultiControlLogger.Info(
            $"本地多控自动补齐下一幕就绪: local={localNetId.Value}, mirrored={string.Join(",", pendingPlayers.Select((player) => player.NetId))}");
    }
}
