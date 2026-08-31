using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Rewards;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace LocalMultiControl.Scripts.Patch;

/// <summary>
/// Merged post-combat rewards, moved up to the room level.
///
/// The vanilla flow (<c>CombatRoom.OfferRoomEndRewards</c>) first GENERATES a RewardsSet for every player —
/// which runs all card-reward modify hooks — and only then calls <c>RewardsSet.Offer</c> per set. The mod's
/// previous merge point was the Offer patch, which discarded those already-generated sets and regenerated
/// per player. That double generation burned one-shot generation-time relics on the invisible first pass
/// (Silken Tress's Glam never appeared: its <c>IsUsed</c> was consumed by the discarded set), advanced the
/// reward RNG twice, and ran <c>Hook.BeforeCombatRewardOffered</c> only on sets nobody ever saw.
///
/// This patch replaces OfferRoomEndRewards itself: each player's rewards are generated exactly once (via the
/// vanilla <c>RewardsCmd.GenerateForRoomEnd</c>), <c>BeforeCombatRewardOffered</c> fires on the sets that are
/// actually shown, and the merged screen displays those same Reward instances. The Offer/OfferForRoomEnd merge
/// patches remain only as backstops for other callers; <c>TryMarkRoomMerged</c> keeps them from re-running.
/// </summary>
[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.OfferRoomEndRewards))]
internal static class CombatRoomOfferRewardsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(CombatRoom __instance, ref Task __result)
    {
        if (!LocalSelfCoopContext.IsEnabled
            || !LocalSelfCoopContext.UseSingleAdventureMode
            || __instance.CombatState.RunState.Players.Count <= 1)
        {
            return true;
        }

        if (!CombatRewardMergeContext.TryMarkRoomMerged(__instance))
        {
            LocalMultiControlLogger.Info("Duplicate room-end reward call ignored (already merged for this room).");
            __result = Task.CompletedTask;
            return false;
        }

        __result = OfferMergedRoomEndRewards(__instance);
        return false;
    }

    private static async Task OfferMergedRoomEndRewards(CombatRoom combatRoom)
    {
        IRunState runState = combatRoom.CombatState.RunState;
        List<Player> allPlayers = runState.Players.ToList();
        if (allPlayers.Count == 0)
        {
            return;
        }

        // Suppress relic/potion/gold mirroring while each character receives their own independent rewards.
        CombatRewardMergeContext.Enter();
        try
        {
            // Generate every player's set exactly once, through the same command vanilla uses.
            List<RewardsSet> generatedSets = new();
            foreach (Player player in allPlayers)
            {
                if (player.Creature?.IsDead == true)
                {
                    continue;
                }

                RewardsSet perPlayerSet = await RewardsCmd.GenerateForRoomEnd(player, combatRoom);
                generatedSets.Add(perPlayerSet);
                LocalMultiControlLogger.Info(
                    $"Per-character rewards generated (room end): player={player.NetId}, rewardCount={perPlayerSet.Rewards.Count}");
            }

            AddExtraCrossCharacterCardRewards(combatRoom, allPlayers, generatedSets);

            // Mirror vanilla: the before-offered hook runs on the sets that will actually be shown.
            List<Reward> mergedRewards = new();
            foreach (RewardsSet perPlayerSet in generatedSets)
            {
                await Hook.BeforeCombatRewardOffered(perPlayerSet, runState, combatRoom);
                foreach (Reward reward in perPlayerSet.Rewards)
                {
                    RewardPlayerLabelRegistry.Register(reward, perPlayerSet.Player.NetId);
                }

                mergedRewards.AddRange(perPlayerSet.Rewards);
            }

            Player displayPlayer = allPlayers.FirstOrDefault((p) => p.Creature?.IsDead != true) ?? allPlayers[0];
            LocalMultiControlRuntime.SwitchControlledPlayerTo(displayPlayer.NetId, "merged-rewards-offer-from-combatroom");
            RewardsSet displaySet = new RewardsSet(displayPlayer).WithCustomRewards(mergedRewards);

            if (TestMode.IsOn)
            {
                foreach (Reward reward in mergedRewards)
                {
                    await reward.SelectUnsynchronized();
                }

                return;
            }

            NRewardsScreen rewardScreen = NRewardsScreen.ShowScreen(displaySet, isTerminal: true, displayPlayer.RunState);
            await rewardScreen.ToSignal(rewardScreen, NRewardsScreen.SignalName.Completed);
        }
        finally
        {
            CombatRewardMergeContext.Exit();
        }
    }

    /// <summary>
    /// Optional (settings key "extraCrossCharacterCardReward", default off): each character's
    /// post-combat rewards gain one extra pick-1-of-3 card group drawn from the OTHER
    /// characters' card pools — the original author's unfinished v1.30 design. The extra
    /// CardReward is created for its receiving player directly (the old
    /// combatRoom.AddExtraReward(otherPlayer, ...) approach parked the reward under the
    /// wrong player's ExtraRewards key, so it never appeared).
    /// </summary>
    private static void AddExtraCrossCharacterCardRewards(CombatRoom combatRoom, List<Player> allPlayers, List<RewardsSet> generatedSets)
    {
        if (!LocalModSettings.GetBool("extraCrossCharacterCardReward", defaultValue: false))
        {
            return;
        }

        foreach (RewardsSet perPlayerSet in generatedSets)
        {
            Player player = perPlayerSet.Player;
            List<CardPoolModel> otherPools = allPlayers
                .Where((candidate) => candidate.NetId != player.NetId && candidate.Creature?.IsDead != true)
                .Select((candidate) => candidate.Character.CardPool)
                .Distinct()
                .ToList();
            if (otherPools.Count == 0)
            {
                continue;
            }

            try
            {
                CardCreationOptions options = CardCreationOptions
                    .ForRoom(player, combatRoom.RoomType)
                    .WithCardPools(otherPools);
                CardReward extraReward = new(options, 3, player);
                extraReward.Populate();
                perPlayerSet.Rewards.Add(extraReward);
                LocalMultiControlLogger.Info(
                    $"Extra cross-character card group added: player={player.NetId}, pools={otherPools.Count}");
            }
            catch (Exception exception)
            {
                LocalMultiControlLogger.Warn($"Failed to add extra cross-character card group: player={player.NetId}, error={exception.Message}");
            }
        }
    }
}
