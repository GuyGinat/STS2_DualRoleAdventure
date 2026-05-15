using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.Offer))]
internal static class RewardsSetPatch
{
    [HarmonyPrefix]
    private static bool Prefix(RewardsSet __instance, ref Task __result)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return true;
        }

        __result = OfferLocalSelfCoop(__instance);
        return false;
    }

    private static async Task OfferLocalSelfCoop(RewardsSet rewardsSet)
    {
        if (rewardsSet.Player.Creature.IsDead)
        {
            return;
        }

        await rewardsSet.GenerateWithoutOffering();
        bool isTerminal = rewardsSet.Room is CombatRoom;
        bool allowEmptyRewards = (bool)(AccessTools.Field(typeof(RewardsSet), "_allowEmptyRewards")?.GetValue(rewardsSet) ?? false);
        if (rewardsSet.Rewards.Count <= 0 && !isTerminal && !allowEmptyRewards)
        {
            return;
        }

        if (!rewardsSet.Rewards.All((reward) => reward.IsPopulated) && rewardsSet.Rewards.Any((reward) => reward.IsPopulated))
        {
            Log.Warn("Some rewards are populated and others are not when calling RewardsCmd.Offer! This might lead to hooks getting called twice");
        }

        LocalMultiControlRuntime.SwitchControlledPlayerTo(rewardsSet.Player.NetId, "rewards-offer");
        LocalMultiControlLogger.Info($"打开奖励界面: player={rewardsSet.Player.NetId}, count={rewardsSet.Rewards.Count}");
        Task rewardsSetTask = RunManager.Instance.RewardsSetSynchronizer.BeginRewardsSet(rewardsSet);

        if (TestMode.IsOn)
        {
            foreach (Reward reward in rewardsSet.Rewards)
            {
                await RunManager.Instance.RewardsSetSynchronizer.SelectLocalReward(reward);
            }

            await rewardsSetTask;
            return;
        }

        NRewardsScreen.ShowScreen(rewardsSet, isTerminal, rewardsSet.Player.RunState);
        await rewardsSetTask;
    }
}
