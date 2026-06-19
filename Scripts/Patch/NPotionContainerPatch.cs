using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NPotionContainer), nameof(NPotionContainer.AnimatePotion))]
internal static class NPotionContainerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NPotionContainer), nameof(NPotionContainer.Initialize))]
    private static void PostfixInitialize(NPotionContainer __instance, IRunState runState)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        try
        {
            ulong targetPlayerId = LocalContext.NetId ?? LocalSelfCoopContext.PrimaryPlayerId;
            if (!TryBindPotionContainerToPlayer(__instance, runState, targetPlayerId))
            {
                LocalMultiControlLogger.Warn($"药水栏初始化失败：未找到目标玩家 {targetPlayerId}。");
            }
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"重建药水栏失败: {exception.Message}");
        }
    }

    [HarmonyPrefix]
    private static bool PrefixAnimatePotion(NPotionContainer __instance, PotionModel potion, Godot.Vector2? startPosition = null)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return true;
        }

        List<NPotionHolder>? holders = AccessTools.Field(typeof(NPotionContainer), "_holders")?.GetValue(__instance) as List<NPotionHolder>;
        NPotionHolder? holder = holders?.FirstOrDefault((node) => node.Potion != null && node.Potion.Model == potion);
        if (holder?.Potion == null)
        {
            LocalMultiControlLogger.Warn($"跳过药水动画：当前视图不存在药水 {potion.Id.Entry}");
            return false;
        }

        TaskHelper.RunSafely(holder.Potion.PlayNewlyAcquiredAnimation(startPosition));
        return false;
    }

    internal static bool TryBindPotionContainerToPrimaryPlayer(NPotionContainer potionContainer, IRunState runState)
    {
        return TryBindPotionContainerToPlayer(potionContainer, runState, LocalSelfCoopContext.PrimaryPlayerId);
    }

    internal static bool TryBindPotionContainerToPlayer(NPotionContainer potionContainer, IRunState runState, ulong playerId)
    {
        MegaCrit.Sts2.Core.Entities.Players.Player? targetPlayer = runState.GetPlayer(playerId);
        if (targetPlayer == null)
        {
            return false;
        }

        AccessTools.Method(typeof(NPotionContainer), "DisconnectPlayerEvents")?.Invoke(potionContainer, null);
        AccessTools.Field(typeof(NPotionContainer), "_player")?.SetValue(potionContainer, targetPlayer);
        AccessTools.Method(typeof(NPotionContainer), "ConnectPlayerEvents")?.Invoke(potionContainer, null);

        List<NPotionHolder>? holders = AccessTools.Field(typeof(NPotionContainer), "_holders")?.GetValue(potionContainer) as List<NPotionHolder>;
        if (holders == null)
        {
            return false;
        }

        foreach (NPotionHolder holder in holders.ToList())
        {
            holder.GetParent()?.RemoveChild(holder);
            holder.QueueFreeSafely();
        }

        holders.Clear();
        AccessTools.Field(typeof(NPotionContainer), "_focusedHolder")?.SetValue(potionContainer, null);

        AccessTools.Method(typeof(NPotionContainer), "GrowPotionHolders")?.Invoke(potionContainer, new object[] { targetPlayer.MaxPotionCount });
        foreach (PotionModel ownedPotion in targetPlayer.Potions)
        {
            AccessTools.Method(typeof(NPotionContainer), "Add")?.Invoke(potionContainer, new object[] { ownedPotion, true });
        }

        LocalMultiControlLogger.Info($"药水栏已重建到目标玩家: player={targetPlayer.NetId}, slotCount={targetPlayer.MaxPotionCount}");
        return true;
    }
}
