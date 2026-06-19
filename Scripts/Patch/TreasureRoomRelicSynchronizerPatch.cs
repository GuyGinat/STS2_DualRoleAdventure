using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.OnPicked))]
internal static class TreasureRoomRelicSynchronizerPatch
{
    internal sealed class OverflowCopyPlan
    {
        public Player PrimaryPlayer { get; init; } = null!;

        public List<Player> Followers { get; } = new();
    }

    private static readonly Dictionary<TreasureRoomRelicSynchronizer, OverflowCopyPlan> OverflowPlans = new();
    private static readonly HashSet<TreasureRoomRelicSynchronizer> SkipAutoSwitchOnce = new();

    [HarmonyPostfix]
    private static void Postfix(TreasureRoomRelicSynchronizer __instance, Player player)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        if (SkipAutoSwitchOnce.Remove(__instance))
        {
            return;
        }

        TryAutoSwitchToNextUnpickedPlayer(__instance, player.NetId, "treasure-picked");
    }

    [HarmonyPrefix]
    private static bool Prefix(TreasureRoomRelicSynchronizer __instance, Player player, int? index)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return true;
        }

        if (!TryGetOverflowPlan(__instance, out OverflowCopyPlan? plan) || plan == null)
        {
            return true;
        }

        if (!index.HasValue)
        {
            return true;
        }

        try
        {
            List<TreasureRoomRelicSynchronizer.PlayerVote>? votes =
                AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_votes")?.GetValue(__instance)
                    as List<TreasureRoomRelicSynchronizer.PlayerVote>;
            IPlayerCollection? players = AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_playerCollection")?.GetValue(__instance) as IPlayerCollection;
            IReadOnlyList<RelicModel>? currentRelics = __instance.CurrentRelics;
            if (votes == null || players == null || currentRelics == null || currentRelics.Count == 0)
            {
                return false;
            }

            int selectedIndex = index.Value;
            if (selectedIndex < 0 || selectedIndex >= currentRelics.Count)
            {
                LocalMultiControlLogger.Warn($"宝箱投票索引越界，已忽略: index={selectedIndex}, relicCount={currentRelics.Count}");
                return false;
            }

            int sharedCount = Math.Min(votes.Count, players.Players.Count);
            for (int i = 0; i < sharedCount; i++)
            {
                votes[i].index = selectedIndex;
                votes[i].voteReceived = true;
            }

            InvokeVotesChanged(__instance);

            RelicModel selectedRelic = currentRelics[selectedIndex];
            List<RelicPickingResult> results = BuildOverflowResults(plan, player, selectedRelic);

            InvokeRelicsAwarded(__instance, results);
            AccessTools.Method(typeof(TreasureRoomRelicSynchronizer), "EndRelicVoting")?.Invoke(__instance, null);
            SkipAutoSwitchOnce.Add(__instance);
            RemoveOverflowPlan(__instance);
            LocalMultiControlLogger.Info(
                $"宝箱5人以上快速结算：player={player.NetId}, relic={selectedRelic.Id.Entry}，已按结算结果发放并结束房间。");
            return false;
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"宝箱溢出投票接管失败，回退原流程: {exception.Message}");
            RemoveOverflowPlan(__instance);
            return true;
        }
    }

    private static List<RelicPickingResult> BuildOverflowResults(OverflowCopyPlan plan, Player sourcePlayer, RelicModel selectedRelic)
    {
        List<Player> orderedPlayers = new();
        HashSet<ulong> seenPlayerIds = new();

        void AddPlayer(Player candidate)
        {
            if (seenPlayerIds.Add(candidate.NetId))
            {
                orderedPlayers.Add(candidate);
            }
        }

        AddPlayer(sourcePlayer);
        AddPlayer(plan.PrimaryPlayer);
        foreach (Player follower in plan.Followers)
        {
            AddPlayer(follower);
        }

        List<RelicPickingResult> results = new(orderedPlayers.Count);
        foreach (Player participant in orderedPlayers)
        {
            results.Add(new RelicPickingResult
            {
                type = RelicPickingResultType.OnlyOnePlayerVoted,
                relic = selectedRelic,
                player = participant
            });
        }

        return results;
    }

    private static void InvokeVotesChanged(TreasureRoomRelicSynchronizer synchronizer)
    {
        Action? callback = AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "VotesChanged")?.GetValue(synchronizer) as Action;
        callback?.Invoke();
    }

    private static void InvokeRelicsAwarded(TreasureRoomRelicSynchronizer synchronizer, List<RelicPickingResult> results)
    {
        Action<List<RelicPickingResult>>? callback =
            AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "RelicsAwarded")?.GetValue(synchronizer) as Action<List<RelicPickingResult>>;
        callback?.Invoke(results);
    }

    internal static void SetOverflowPlan(TreasureRoomRelicSynchronizer synchronizer, OverflowCopyPlan plan)
    {
        OverflowPlans[synchronizer] = plan;
    }

    private static bool TryGetOverflowPlan(TreasureRoomRelicSynchronizer synchronizer, out OverflowCopyPlan? plan)
    {
        return OverflowPlans.TryGetValue(synchronizer, out plan);
    }

    internal static void RemoveOverflowPlan(TreasureRoomRelicSynchronizer synchronizer)
    {
        OverflowPlans.Remove(synchronizer);
    }

    internal static bool TryAutoSwitchToNextUnpickedPlayer(TreasureRoomRelicSynchronizer synchronizer, ulong currentPlayerId, string source)
    {
        try
        {
            List<TreasureRoomRelicSynchronizer.PlayerVote>? votes =
                AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_votes")?.GetValue(synchronizer)
                    as List<TreasureRoomRelicSynchronizer.PlayerVote>;
            IPlayerCollection? players = AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_playerCollection")?.GetValue(synchronizer) as IPlayerCollection;
            if (votes == null || players == null)
            {
                return false;
            }

            int sharedCount = Math.Min(votes.Count, players.Players.Count);
            if (sharedCount < 2)
            {
                return false;
            }

            int currentSlot = players.Players.Take(sharedCount).ToList().FindIndex((candidate) => candidate.NetId == currentPlayerId);
            if (currentSlot < 0)
            {
                return false;
            }

            if (!votes[currentSlot].voteReceived)
            {
                return false;
            }

            int nextSlot = -1;
            for (int step = 1; step < sharedCount; step++)
            {
                int slot = (currentSlot + step) % sharedCount;
                if (!votes[slot].voteReceived)
                {
                    nextSlot = slot;
                    break;
                }
            }

            if (nextSlot < 0)
            {
                return false;
            }

            ulong nextPlayerId = players.Players[nextSlot].NetId;
            Callable.From(delegate
            {
                LocalMultiControlRuntime.SwitchControlledPlayerTo(nextPlayerId, source);
            }).CallDeferred();

            LocalMultiControlLogger.Info($"宝箱选择完成后自动切换到下一位未选角色: {currentPlayerId} -> {nextPlayerId}");
            return true;
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"宝箱自动切换未选角色失败: {exception.Message}");
            return false;
        }
    }
}

[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.BeginRelicPicking))]
internal static class TreasureRoomRelicSynchronizerBeginPatch
{
    private const int MAX_MANUAL_RELIC_OPTIONS = 4;

    [HarmonyPostfix]
    private static void Postfix(TreasureRoomRelicSynchronizer __instance)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        try
        {
            List<TreasureRoomRelicSynchronizer.PlayerVote>? votes =
                AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_votes")?.GetValue(__instance)
                    as List<TreasureRoomRelicSynchronizer.PlayerVote>;
            IReadOnlyList<Player>? players = AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "_playerCollection")?.GetValue(__instance) is IPlayerCollection playerCollection
                ? playerCollection.Players
                : null;
            IReadOnlyList<RelicModel>? currentRelics = __instance.CurrentRelics;
            if (votes == null || players == null || votes.Count <= 1 || currentRelics == null || currentRelics.Count <= 1)
            {
                return;
            }

            int sharedCount = Math.Min(votes.Count, players.Count);
            if (sharedCount <= MAX_MANUAL_RELIC_OPTIONS)
            {
                for (int i = 0; i < sharedCount; i++)
                {
                    votes[i].index = null;
                    votes[i].voteReceived = false;
                }

                TreasureRoomRelicSynchronizerPatch.RemoveOverflowPlan(__instance);
                LocalMultiControlLogger.Info("宝箱已禁用自动代投，改为逐角色手动选择。");
                return;
            }

            Player primaryPlayer = players[0];
            TreasureRoomRelicSynchronizerPatch.OverflowCopyPlan plan = new()
            {
                PrimaryPlayer = primaryPlayer
            };

            for (int i = 0; i < sharedCount; i++)
            {
                votes[i].index = null;
                votes[i].voteReceived = false;
                if (i > 0)
                {
                    plan.Followers.Add(players[i]);
                }
            }

            TreasureRoomRelicSynchronizerPatch.SetOverflowPlan(__instance, plan);
            LocalMultiControlLogger.Info($"宝箱5人以上特判已启用：仅1号位参与事件，其余{plan.Followers.Count}人将直接复制1号位遗物。");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"禁用宝箱自动代投失败: {exception.Message}");
        }
    }
}
