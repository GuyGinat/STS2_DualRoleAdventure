using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NMultiplayerPlayerIntentHandler), nameof(NMultiplayerPlayerIntentHandler.Create))]
internal static class NMultiplayerPlayerIntentHandlerPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Player player, ref NMultiplayerPlayerIntentHandler? __result)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return true;
        }

        // 风险点：远端意图UI会订阅 InputSynchronizer/Targeting 事件，
        // 本地双人切人时容易与 NMouseCardPlay、远端目标线节点生命周期冲突，
        // 表现为频繁 ObjectDisposedException 与出牌队列异常。
        // 当前选择直接禁用该UI，后续如需恢复必须先完成稳定性回归。
        __result = null;
        LocalMultiControlLogger.Info($"本地双人模式已禁用远端意图UI: player={player.NetId}");
        return false;
    }
}
