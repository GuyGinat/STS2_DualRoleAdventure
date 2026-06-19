using System.Collections.Generic;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(LavaRock), nameof(LavaRock.TryModifyRewards))]
internal static class LavaRockPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        LavaRock __instance,
        Player player,
        List<Reward> rewards,
        AbstractRoom? room,
        ref bool __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return true;
        }

        if (player.RunState == null)
        {
            __result = false;
            return false;
        }

        if (room == null || room.RoomType != RoomType.Boss)
        {
            __result = false;
            return false;
        }

        if (player.RunState.CurrentActIndex != 0)
        {
            __result = false;
            return false;
        }

        LavaRock? lavaRock = player.GetRelic<LavaRock>();
        if (lavaRock == null || lavaRock.HasTriggered)
        {
            __result = false;
            return false;
        }

        int extraRelicCount = lavaRock.DynamicVars["Relics"].IntValue;
        for (int i = 0; i < extraRelicCount; i++)
        {
            rewards.Add(new RelicReward(player));
        }

        lavaRock.Flash();
        lavaRock.HasTriggered = true;
        lavaRock.Status = RelicStatus.Disabled;

        __result = true;
        LocalMultiControlLogger.Info(
            $"本地多控已按角色熔岩石触发首领额外遗物: rewardPlayer={player.NetId}, added={extraRelicCount}");
        return false;
    }
}
