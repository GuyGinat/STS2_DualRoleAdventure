using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Models;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.EnqueueManualUse))]
internal static class PotionManualUseTargetPatch
{
    [HarmonyPrefix]
    private static void Prefix(PotionModel __instance, ref Creature? target)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode || target == null)
        {
            return;
        }

        if (__instance.TargetType != TargetType.Self || target != __instance.Owner.Creature)
        {
            return;
        }

        ulong controlledPlayerId = LocalContext.NetId ?? __instance.Owner.NetId;
        Player? controlledPlayer = __instance.Owner.RunState.GetPlayer(controlledPlayerId);
        if (controlledPlayer == null || controlledPlayer.Creature == null || controlledPlayer == __instance.Owner)
        {
            return;
        }

        target = controlledPlayer.Creature;
        LocalMultiControlLogger.Info($"药水默认目标已跟随当前控制角色: potion={__instance.Id.Entry}, owner={__instance.Owner.NetId}, target={controlledPlayer.NetId}");
    }
}
