using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Rewards;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;

namespace LocalMultiControl.Scripts.Patch;

internal static class GoldMirrorSuppressionContext
{
    private static readonly AsyncLocal<int> SuppressDepth = new();

    internal static bool ShouldSuppressGoldMirror => SuppressDepth.Value > 0;

    internal static void EnterSuppression()
    {
        SuppressDepth.Value++;
    }

    internal static async Task<T> ExitSuppressionWhenCompleteAsync<T>(Task<T> task)
    {
        try
        {
            return await task;
        }
        finally
        {
            ExitSuppressionOnce();
        }
    }

    internal static void ExitSuppressionOnce()
    {
        if (SuppressDepth.Value > 0)
        {
            SuppressDepth.Value--;
        }
    }
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainGold))]
internal static class PlayerGainGoldMirrorPatch
{
    private static readonly AsyncLocal<bool> IsMirroring = new();

    [HarmonyPostfix]
    private static void Postfix(decimal amount, Player player, bool wasStolenBack, ref Task __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        if (amount <= 0m || IsMirroring.Value)
        {
            return;
        }

        if (GoldMirrorSuppressionContext.ShouldSuppressGoldMirror)
        {
            LocalMultiControlLogger.Info($"遗物流程金币跳过镜像: amount={amount}, owner={player.NetId}");
            return;
        }

        // 汇总奖励流程中，每个角色已独立生成金币奖励，不需要镜像
        if (CombatRewardMergeContext.IsActive)
        {
            return;
        }

        bool isCombatRewardContext = player.RunState.CurrentRoom is CombatRoom && !CombatManager.Instance.IsInProgress;
        bool isCrystalSphereContext = CrystalSphereMirrorRuntime.IsInCrystalSphereEventContext(player);
        if (!isCombatRewardContext && !isCrystalSphereContext)
        {
            return;
        }

        __result = MirrorGoldToOtherPlayersAsync(amount, player, wasStolenBack, __result);
    }

    private static async Task MirrorGoldToOtherPlayersAsync(decimal amount, Player sourcePlayer, bool wasStolenBack, Task originalTask)
    {
        await originalTask;

        var otherPlayers = sourcePlayer.RunState.Players.Where((candidate) => candidate.NetId != sourcePlayer.NetId).ToList();
        if (otherPlayers.Count == 0)
        {
            return;
        }

        IsMirroring.Value = true;
        try
        {
            foreach (Player otherPlayer in otherPlayers)
            {
                await PlayerCmd.GainGold(amount, otherPlayer, wasStolenBack);
            }

            LocalMultiControlLogger.Info(
                $"事件/流程金币已同步到其余角色: amount={amount}, owner={sourcePlayer.NetId}, mirrored={string.Join(",", otherPlayers.Select((player) => player.NetId))}");
        }
        finally
        {
            IsMirroring.Value = false;
        }
    }
}
