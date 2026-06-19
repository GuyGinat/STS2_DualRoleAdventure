using System.Linq;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(CardSelectCmd), "ShouldSelectLocalCard")]
internal static class CardSelectCmdPatch
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

        if (!LocalSelfCoopContext.LocalPlayerIds.Contains(player.NetId))
        {
            return true;
        }

        // 本地多控下所有本地角色均按“本地手选”处理，避免事件删牌/变牌落入远端等待后随机或卡住。
        __result = RunManager.Instance.NetService.Type != NetGameType.Replay;
        return false;
    }

    [HarmonyPostfix]
    private static void Postfix(Player player, ref bool __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        if (RunManager.Instance.NetService is not LocalLoopbackHostGameService)
        {
            return;
        }

        if (!LocalSelfCoopContext.LocalPlayerIds.Contains(player.NetId))
        {
            return;
        }

        if (!__result)
        {
            LocalMultiControlLogger.Info($"牌组选牌已强制本地手选: player={player.NetId}");
        }

        __result = true;
    }
}
