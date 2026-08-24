using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Rewards;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace LocalMultiControl.Scripts.Patch;

/// <summary>
/// 战后奖励重构：为每个角色独立生成奖励（利用原版 RewardsSet 逻辑），
/// 然后将所有角色的奖励汇总到一个列表展示，每个奖励标识对应角色。
/// 这样遗物翻倍、上等好货因子、猎人狩猎等效果能正确按角色独立生效。
/// </summary>
// NOTE: since the Silken Tress fix, the PRIMARY merge point for post-combat rewards is
// CombatRoomOfferRewardsPatch (CombatRoom.OfferRoomEndRewards). This patch remains as a
// backstop for direct OfferForRoomEnd callers; TryMarkRoomMerged dedupes between the two.
[HarmonyPatch(typeof(RewardsCmd), nameof(RewardsCmd.OfferForRoomEnd))]
internal static class RewardsCmdPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Player player, AbstractRoom room, ref Task __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || room is not CombatRoom combatRoom || player.RunState.Players.Count <= 1)
        {
            return true;
        }

        if (!CombatRewardMergeContext.TryMarkRoomMerged(room))
        {
            LocalMultiControlLogger.Info($"检测到重复战后奖励调用，已忽略: player={player.NetId}, room={room.RoomType}");
            __result = Task.CompletedTask;
            return false;
        }

        __result = OfferMergedRewardsForAllPlayers(combatRoom);
        return false;
    }

    private static async Task OfferMergedRewardsForAllPlayers(CombatRoom combatRoom)
    {
        IRunState runState = combatRoom.CombatState.RunState;
        List<Player> allPlayers = runState.Players.ToList();
        if (allPlayers.Count == 0)
        {
            return;
        }

        // 标记进入汇总奖励流程，抑制遗物/药水/金币的镜像复制
        CombatRewardMergeContext.Enter();
        try
        {
            await OfferMergedRewardsCore(combatRoom, allPlayers);
        }
        finally
        {
            CombatRewardMergeContext.Exit();
        }
    }

    private static async Task OfferMergedRewardsCore(CombatRoom combatRoom, List<Player> allPlayers)
    {
        // 为每个角色独立生成奖励（不展示），收集到汇总列表
        List<Reward> mergedRewards = new();
        bool shouldGiveRewards = combatRoom.Encounter == null || combatRoom.Encounter.ShouldGiveRewards;

        foreach (Player player in allPlayers)
        {
            if (player.Creature?.IsDead == true)
            {
                continue;
            }

            RewardsSet perPlayerSet = shouldGiveRewards
                ? new RewardsSet(player).WithRewardsFromRoom(combatRoom)
                : new RewardsSet(player).EmptyForRoom(combatRoom);

            // 调用 GenerateWithoutOffering 触发 Populate + Hook.ModifyRewards
            await perPlayerSet.GenerateWithoutOffering();

            // 为每个奖励注册角色标签
            foreach (Reward reward in perPlayerSet.Rewards)
            {
                RewardPlayerLabelRegistry.Register(reward, player.NetId);
            }

            mergedRewards.AddRange(perPlayerSet.Rewards);
            LocalMultiControlLogger.Info(
                $"角色独立奖励已生成: player={player.NetId}, rewardCount={perPlayerSet.Rewards.Count}");
        }

        // 切换到第一个存活角色的控制上下文来展示奖励界面
        Player? displayPlayer = allPlayers.FirstOrDefault((p) => p.Creature?.IsDead != true) ?? allPlayers[0];
        LocalMultiControlRuntime.SwitchControlledPlayerTo(displayPlayer.NetId, "merged-rewards-offer");
        RewardsSet displaySet = new RewardsSet(displayPlayer).WithCustomRewards(mergedRewards);

        if (TestMode.IsOn)
        {
            foreach (Reward reward in mergedRewards)
            {
                await reward.SelectUnsynchronized();
            }

            return;
        }

        bool isTerminal = true; // CombatRoom 的奖励界面始终是 terminal
        NRewardsScreen rewardScreen = NRewardsScreen.ShowScreen(displaySet, isTerminal, displayPlayer.RunState);
        await rewardScreen.ToSignal(rewardScreen, NRewardsScreen.SignalName.Completed);
    }
}
