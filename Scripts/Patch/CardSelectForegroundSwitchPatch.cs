using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

internal static class CardSelectForegroundSwitchPatch
{
    private static bool TryGetRejectReason(out string reason)
    {
        reason = string.Empty;
        if (!RunManager.Instance.IsInProgress || !CombatManager.Instance.IsInProgress)
        {
            reason = "combat-not-in-progress";
            return true;
        }

        NCombatUi? combatUi = NCombatRoom.Instance?.Ui;
        if (combatUi == null)
        {
            reason = "combat-ui-null";
            return true;
        }

        NPlayerHand hand = combatUi.Hand;
        if (hand.InCardPlay)
        {
            reason = "hand-in-card-play";
            return true;
        }

        if (hand.IsInCardSelection)
        {
            reason = "hand-in-card-selection";
            return true;
        }

        if (NTargetManager.Instance?.IsInSelection ?? false)
        {
            reason = "target-selecting";
            return true;
        }

        ActionSynchronizerCombatState syncState = RunManager.Instance.ActionQueueSynchronizer.CombatState;
        if (syncState != ActionSynchronizerCombatState.PlayPhase)
        {
            reason = $"sync-{syncState}";
            return true;
        }

        return false;
    }

    private static void EnsureForegroundForCombatChoice(Player player, string source)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        if (!LocalSelfCoopContext.LocalPlayerIds.Contains(player.NetId))
        {
            return;
        }

        ulong currentPlayerId = LocalMultiControlRuntime.SessionState.CurrentControlledPlayerId
            ?? LocalContext.NetId
            ?? LocalSelfCoopContext.PrimaryPlayerId;
        if (currentPlayerId == player.NetId)
        {
            return;
        }

        if (TryGetRejectReason(out string reason))
        {
            LocalMultiControlRuntime.RecordFlowBlockSignal(
                "foreground_switch_rejected_due_to_state",
                reason,
                player.NetId,
                source,
                round: -1);
            LocalMultiControlLogger.Info(
                $"战斗选牌切前台已跳过: source={source}, target={player.NetId}, reason={reason}");
            return;
        }

        LocalMultiControlLogger.Info(
            $"检测到战斗选牌请求来自后台角色，准备延迟切换前台进行手选: source={source}, current={currentPlayerId}, target={player.NetId}");
        Callable.From(delegate
        {
            if (TryGetRejectReason(out string deferredReason))
            {
                LocalMultiControlRuntime.RecordFlowBlockSignal(
                    "foreground_switch_rejected_due_to_state",
                    $"{deferredReason}-deferred",
                    player.NetId,
                    source,
                    round: -1);
                LocalMultiControlLogger.Info(
                    $"战斗选牌延迟切前台已取消: source={source}, target={player.NetId}, reason={deferredReason}");
                return;
            }

            LocalMultiControlRuntime.SwitchControlledPlayerTo(player.NetId, $"combat-choice-{source}");
        }).CallDeferred();
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromSimpleGrid))]
    [HarmonyPrefix]
    private static void FromSimpleGridPrefix(Player player)
    {
        EnsureForegroundForCombatChoice(player, "FromSimpleGrid");
    }

    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHand))]
    [HarmonyPrefix]
    private static void FromHandPrefix(Player player)
    {
        EnsureForegroundForCombatChoice(player, "FromHand");
    }
}
