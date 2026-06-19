using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Rewards;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain), new[] { typeof(RelicModel), typeof(Player), typeof(int) })]
internal static class RelicCmdObtainPatch
{
    private static readonly HashSet<RelicModel> NonSharedChainRelics = new(ReferenceEqualityComparer.Instance);

    internal static bool TryConsumeNonSharedChainRelic(RelicModel relic)
    {
        return NonSharedChainRelics.Remove(relic);
    }

    [HarmonyPrefix]
    private static void Prefix()
    {
        GoldMirrorSuppressionContext.EnterSuppression();
    }

    [HarmonyPostfix]
    private static void Postfix(Player player, ref Task<RelicModel> __result)
    {
        __result = GoldMirrorSuppressionContext.ExitSuppressionWhenCompleteAsync(MirrorObtainForOtherLocalPlayersAsync(player, __result));
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception)
    {
        if (__exception != null)
        {
            GoldMirrorSuppressionContext.ExitSuppressionOnce();
        }

        return __exception;
    }

    private static async Task<RelicModel> MirrorObtainForOtherLocalPlayersAsync(Player player, Task<RelicModel> originalTask)
    {
        RelicModel obtainedRelic = await originalTask;
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return obtainedRelic;
        }

        if (player.RunState?.Players == null || player.RunState.Players.Count <= 1)
        {
            return obtainedRelic;
        }

        // 汇总奖励流程中，每个角色已独立生成奖励，不需要镜像
        if (CombatRewardMergeContext.IsActive)
        {
            LocalMultiControlLogger.Info($"汇总奖励流程中跳过遗物镜像: relic={obtainedRelic.Id.Entry}, owner={player.NetId}");
            return obtainedRelic;
        }

        bool isCombatRewardContext = player.RunState.CurrentRoom is CombatRoom && !CombatManager.Instance.IsInProgress;
        bool isCrystalSphereContext = CrystalSphereMirrorRuntime.IsInCrystalSphereEventContext(player);
        if (!isCombatRewardContext && !isCrystalSphereContext)
        {
            return obtainedRelic;
        }

        if (PaelsWingPatch.TryConsumePendingOwner(player.NetId))
        {
            LocalMultiControlLogger.Info($"佩尔之翼献祭产出的遗物不做共享镜像: relic={obtainedRelic.Id.Entry}, owner={player.NetId}");
            return obtainedRelic;
        }

        bool skipChainMirror = ShouldSkipChainMirror(obtainedRelic);
        if (skipChainMirror)
        {
            NonSharedChainRelics.Add(obtainedRelic);
            LocalMultiControlLogger.Info($"链式遗物按特判处理，不做共享镜像: relic={obtainedRelic.Id.Entry}, owner={player.NetId}");
            return obtainedRelic;
        }

        foreach (Player otherPlayer in player.RunState.Players.Where((candidate) => candidate.NetId != player.NetId))
        {
            if (!obtainedRelic.IsStackable && otherPlayer.GetRelicById(obtainedRelic.Id) != null)
            {
                continue;
            }

            try
            {
                RelicModel mirroredRelic = RelicModel.FromSerializable(obtainedRelic.ToSerializable());
                otherPlayer.AddRelicInternal(mirroredRelic);
                await mirroredRelic.AfterObtained();
                LocalMultiControlLogger.Info($"本地多控共享遗物同步: {obtainedRelic.Id.Entry}, {player.NetId} -> {otherPlayer.NetId}");
            }
            catch (Exception exception)
            {
                LocalMultiControlLogger.Warn($"共享遗物同步失败(获得): target={otherPlayer.NetId}, error={exception.Message}");
            }
        }

        return obtainedRelic;
    }

    private static bool ShouldSkipChainMirror(RelicModel relic)
    {
        if (relic.IsWax)
        {
            return true;
        }

        string relicId = relic.Id.Entry;
        return relicId == "LARGE_CAPSULE" || relicId == "TOY_BOX";
    }
}

[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Remove))]
internal static class RelicCmdRemovePatch
{
    [HarmonyPostfix]
    private static void Postfix(RelicModel relic, ref Task __result)
    {
        __result = MirrorRemoveForOtherLocalPlayersAsync(relic, __result);
    }

    private static async Task MirrorRemoveForOtherLocalPlayersAsync(RelicModel removedRelic, Task originalTask)
    {
        await originalTask;
        if (RelicCmdObtainPatch.TryConsumeNonSharedChainRelic(removedRelic))
        {
            return;
        }

        if (!LocalSelfCoopContext.IsEnabled || removedRelic.Owner?.RunState == null)
        {
            return;
        }

        // 汇总奖励流程中跳过镜像
        if (CombatRewardMergeContext.IsActive)
        {
            return;
        }

        bool isCombatRewardContext = removedRelic.Owner.RunState.CurrentRoom is CombatRoom && !CombatManager.Instance.IsInProgress;
        bool isCrystalSphereContext = CrystalSphereMirrorRuntime.IsInCrystalSphereEventContext(removedRelic.Owner);
        if (!isCombatRewardContext && !isCrystalSphereContext)
        {
            return;
        }

        IRunState runState = removedRelic.Owner.RunState;
        if (runState.Players.Count <= 1)
        {
            return;
        }

        foreach (Player otherPlayer in runState.Players.Where((candidate) => candidate.NetId != removedRelic.Owner.NetId))
        {
            RelicModel? mirroredRelic = otherPlayer.GetRelicById(removedRelic.Id);
            if (mirroredRelic == null)
            {
                continue;
            }

            try
            {
                otherPlayer.RemoveRelicInternal(mirroredRelic);
                await mirroredRelic.AfterRemoved();
                LocalMultiControlLogger.Info($"本地多控共享遗物同步移除: {removedRelic.Id.Entry}, owner={otherPlayer.NetId}");
            }
            catch (Exception exception)
            {
                LocalMultiControlLogger.Warn($"共享遗物同步失败(移除): target={otherPlayer.NetId}, error={exception.Message}");
            }
        }
    }
}
