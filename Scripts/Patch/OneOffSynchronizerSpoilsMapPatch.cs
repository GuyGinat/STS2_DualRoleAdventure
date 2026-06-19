using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(OneOffSynchronizer), nameof(OneOffSynchronizer.DoLocalTreasureRoomRewards))]
internal static class OneOffSynchronizerSpoilsMapPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref Task<int> __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        __result = ResolveSpoilsMapForAllLocalPlayersAsync(__result);
    }

    private static async Task<int> ResolveSpoilsMapForAllLocalPlayersAsync(Task<int> originalTask)
    {
        int totalGold = await originalTask;

        try
        {
            Player? localPlayer = LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState());
            if (localPlayer?.RunState?.Players == null || localPlayer.RunState.Players.Count <= 1)
            {
                return totalGold;
            }

            IRunState runState = localPlayer.RunState;
            if (!runState.CurrentMapCoord.HasValue)
            {
                return totalGold;
            }

            MapPoint? currentPoint = runState.Map.GetPoint(runState.CurrentMapCoord.Value);
            if (currentPoint == null || !currentPoint.Quests.Any((quest) => quest is SpoilsMap))
            {
                return totalGold;
            }

            List<Player> playersWithSpoilsMap = runState.Players
                .Where((player) => player.Deck.Cards.OfType<SpoilsMap>().Any((map) => map.SpoilsActIndex == runState.CurrentActIndex))
                .ToList();
            if (playersWithSpoilsMap.Count == 0)
            {
                return totalGold;
            }

            int syncedCount = 0;
            foreach (Player player in playersWithSpoilsMap)
            {
                SpoilsMap? spoilsMap = player.Deck.Cards
                    .OfType<SpoilsMap>()
                    .FirstOrDefault((map) => map.SpoilsActIndex == runState.CurrentActIndex);
                if (spoilsMap == null)
                {
                    continue;
                }

                int gainedGold = await spoilsMap.OnQuestComplete();
                totalGold += gainedGold;
                syncedCount++;
                LocalMultiControlLogger.Info($"宝箱房间触发藏宝图结算: player={player.NetId}, gold={gainedGold}");
            }

            LocalMultiControlLogger.Info($"宝箱房间藏宝图批处理完成: processed={syncedCount}, players={runState.Players.Count}");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"宝箱房间藏宝图批处理失败: {exception.Message}");
        }

        return totalGold;
    }
}
