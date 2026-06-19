using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(EntropyPower), nameof(EntropyPower.AfterPlayerTurnStart))]
internal static class EntropyPowerPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EntropyPower __instance, PlayerChoiceContext choiceContext, Player player, ref Task __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || !RunManager.Instance.IsInProgress)
        {
            return true;
        }

        if (player != __instance.Owner?.Player)
        {
            return true;
        }

        if (!LocalWakuuRelicRuntime.HasWakuuRelic(player))
        {
            return true;
        }

        LocalMultiControlLogger.Info($"熵已跳过瓦库角色: player={player.NetId}, amount={__instance.Amount}");
        __result = Task.CompletedTask;
        return false;
    }
}
