using System.Collections.Generic;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Relics;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(PaelsWing), nameof(PaelsWing.OnSacrifice))]
internal static class PaelsWingPatch
{
    private static readonly object PendingOwnerLock = new();
    private static readonly HashSet<ulong> PendingOwnerNetIds = new();

    [HarmonyPrefix]
    private static void Prefix(PaelsWing __instance)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        Player? owner = __instance.Owner;
        if (owner == null)
        {
            return;
        }

        int sacrificesNeeded = __instance.DynamicVars["Sacrifices"].IntValue;
        if (sacrificesNeeded <= 0)
        {
            return;
        }

        bool willGainRelic = (__instance.RewardsSacrificed + 1) % sacrificesNeeded == 0;
        if (!willGainRelic)
        {
            return;
        }

        lock (PendingOwnerLock)
        {
            PendingOwnerNetIds.Add(owner.NetId);
        }

        LocalMultiControlLogger.Info($"佩尔之翼献祭命中遗物阈值，已登记为非共享: owner={owner.NetId}");
    }

    internal static bool TryConsumePendingOwner(ulong ownerNetId)
    {
        lock (PendingOwnerLock)
        {
            return PendingOwnerNetIds.Remove(ownerNetId);
        }
    }
}
