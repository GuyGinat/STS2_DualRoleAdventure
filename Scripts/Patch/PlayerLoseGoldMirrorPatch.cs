using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.LoseGold))]
internal static class PlayerLoseGoldMirrorPatch
{
    private static readonly AsyncLocal<bool> IsMirroring = new();

    [HarmonyPostfix]
    private static void Postfix(decimal amount, Player player, GoldLossType goldLossType, ref Task __result)
    {
        if (!CrystalSphereMirrorRuntime.IsInCrystalSphereEventContext(player))
        {
            return;
        }

        if (amount <= 0m || IsMirroring.Value)
        {
            return;
        }

        __result = MirrorGoldLossToOtherPlayersAsync(amount, player, goldLossType, __result);
    }

    private static async Task MirrorGoldLossToOtherPlayersAsync(
        decimal amount,
        Player sourcePlayer,
        GoldLossType goldLossType,
        Task originalTask)
    {
        await originalTask;

        IsMirroring.Value = true;
        try
        {
            System.Collections.Generic.List<Player> otherPlayers = CrystalSphereMirrorRuntime.GetOtherPlayers(sourcePlayer);
            foreach (Player otherPlayer in otherPlayers)
            {
                await PlayerCmd.LoseGold(amount, otherPlayer, goldLossType);
            }

            LocalMultiControlLogger.Info(
                $"水晶球事件金币消耗已镜像到其余角色: amount={amount}, owner={sourcePlayer.NetId}, mirrored={string.Join(",", otherPlayers.Select((player) => player.NetId))}");
        }
        finally
        {
            IsMirroring.Value = false;
        }
    }
}
