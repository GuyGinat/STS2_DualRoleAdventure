using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NRelicInventory), nameof(NRelicInventory.AnimateRelic))]
internal static class NRelicInventoryPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NRelicInventory __instance, RelicModel relic, Godot.Vector2? startPosition = null, Godot.Vector2? startScale = null)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return true;
        }

        if (!LocalContext.IsMine(relic))
        {
            return false;
        }

        List<NRelicInventoryHolder>? relicNodes = AccessTools.Field(typeof(NRelicInventory), "_relicNodes")?.GetValue(__instance) as List<NRelicInventoryHolder>;
        NRelicInventoryHolder? holder = relicNodes?.FirstOrDefault((node) => node.Relic.Model == relic);
        if (holder == null)
        {
            LocalMultiControlLogger.Warn($"跳过遗物动画：当前视图不存在遗物 {relic.Id.Entry}");
            return false;
        }

        TaskHelper.RunSafely(holder.PlayNewlyAcquiredAnimation(startPosition, startScale));
        return false;
    }

    internal static bool TryRebuildRelicInventoryToPrimaryPlayer(NRelicInventory relicInventory, IRunState runState)
    {
        return TryRebuildRelicInventoryToPlayer(relicInventory, runState, LocalSelfCoopContext.PrimaryPlayerId);
    }

    internal static bool TryRebuildRelicInventoryToPlayer(NRelicInventory relicInventory, IRunState runState, ulong playerId)
    {
        MegaCrit.Sts2.Core.Entities.Players.Player? targetPlayer = runState.GetPlayer(playerId);
        if (targetPlayer == null)
        {
            return false;
        }

        AccessTools.Method(typeof(NRelicInventory), "DisconnectPlayerEvents")?.Invoke(relicInventory, null);
        AccessTools.Field(typeof(NRelicInventory), "_player")?.SetValue(relicInventory, targetPlayer);
        AccessTools.Method(typeof(NRelicInventory), "ConnectPlayerEvents")?.Invoke(relicInventory, null);

        List<NRelicInventoryHolder>? relicNodes = AccessTools.Field(typeof(NRelicInventory), "_relicNodes")?.GetValue(relicInventory) as List<NRelicInventoryHolder>;
        if (relicNodes == null)
        {
            return false;
        }

        foreach (NRelicInventoryHolder holder in relicNodes.ToList())
        {
            holder.GetParent()?.RemoveChild(holder);
            holder.QueueFreeSafely();
        }

        relicNodes.Clear();
        System.Reflection.MethodInfo? addMethod = AccessTools.Method(typeof(NRelicInventory), "Add");
        if (addMethod == null)
        {
            return false;
        }

        foreach (RelicModel relic in targetPlayer.Relics)
        {
            addMethod.Invoke(relicInventory, new object[] { relic, true, -1 });
        }

        LocalMultiControlLogger.Info($"遗物栏已重建到目标玩家: player={targetPlayer.NetId}, count={targetPlayer.Relics.Count}");
        return true;
    }
}
