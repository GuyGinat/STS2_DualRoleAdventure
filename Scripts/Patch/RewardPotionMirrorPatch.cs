using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Rewards;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(RewardSynchronizer), nameof(RewardSynchronizer.SyncLocalObtainedPotion))]
internal static class RewardPotionMirrorPatch
{
    private static readonly AsyncLocal<bool> IsMirroring = new();

    [HarmonyPostfix]
    private static void Postfix(RewardSynchronizer __instance, PotionModel potion)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode || IsMirroring.Value)
        {
            return;
        }

        // 汇总奖励流程中，每个角色已独立生成药水奖励，不需要镜像
        if (CombatRewardMergeContext.IsActive)
        {
            return;
        }

        Player? sourcePlayer = ResolveSourcePlayer(__instance);
        if (sourcePlayer == null)
        {
            return;
        }

        bool isCombatRewardContext = sourcePlayer.RunState.CurrentRoom is CombatRoom && !CombatManager.Instance.IsInProgress;
        bool isCrystalSphereContext = CrystalSphereMirrorRuntime.IsInCrystalSphereEventContext(sourcePlayer);
        if (!isCombatRewardContext && !isCrystalSphereContext)
        {
            return;
        }

        TaskHelper.RunSafely(MirrorPotionToOtherPlayersAsync(sourcePlayer, potion));
    }

    private static Player? ResolveSourcePlayer(RewardSynchronizer synchronizer)
    {
        ulong localPlayerId = AccessTools.Field(typeof(RewardSynchronizer), "_localPlayerId")?.GetValue(synchronizer) as ulong? ?? 0UL;
        if (localPlayerId == 0UL)
        {
            return null;
        }

        IPlayerCollection? playerCollection = AccessTools.Field(typeof(RewardSynchronizer), "_playerCollection")
            ?.GetValue(synchronizer) as IPlayerCollection;
        return playerCollection?.GetPlayer(localPlayerId);
    }

    private static async Task MirrorPotionToOtherPlayersAsync(Player sourcePlayer, PotionModel potion)
    {
        IsMirroring.Value = true;
        try
        {
            foreach (Player otherPlayer in sourcePlayer.RunState.Players.Where((candidate) => candidate.NetId != sourcePlayer.NetId))
            {
                PotionModel mirroredPotion = PotionModel.FromSerializable(potion.ToSerializable(-1));
                PotionProcureResult result = await PotionCmd.TryToProcure(mirroredPotion, otherPlayer);
                LocalMultiControlLogger.Info(
                    $"战利品药水同步: source={sourcePlayer.NetId}, target={otherPlayer.NetId}, potion={mirroredPotion.Id.Entry}, success={result.success}");
            }
        }
        finally
        {
            IsMirroring.Value = false;
        }
    }
}
