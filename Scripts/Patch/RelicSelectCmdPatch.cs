using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(RelicSelectCmd), "ShouldSelectLocalRelic")]
internal static class RelicSelectCmdPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Player player, ref bool __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return true;
        }

        if (RunManager.Instance.NetService is not LocalLoopbackHostGameService)
        {
            return true;
        }

        __result = true;
        LocalMultiControlLogger.Info($"本地双人模式下强制本地处理遗物选择: player={player.NetId}");
        return false;
    }
}
