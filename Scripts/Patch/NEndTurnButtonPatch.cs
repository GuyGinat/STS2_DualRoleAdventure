using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NEndTurnButton), "CallReleaseLogic")]
internal static class NEndTurnButtonPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NEndTurnButton __instance)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return true;
        }

        CombatState? combatState = AccessTools.Field(typeof(NEndTurnButton), "_combatState")?.GetValue(__instance) as CombatState;
        if (combatState == null)
        {
            return true;
        }

        bool handled = LocalMultiControlRuntime.TryManualEndTurnAutoCloseAllPlayers();
        if (handled)
        {
            LocalMultiControlLogger.Info("回合结束点击已按“全员无牌可出”规则处理，跳过原始结束逻辑。");
            return false;
        }

        Player? me = LocalContext.GetMe(combatState);
        if (me != null)
        {
            LocalMultiControlRuntime.RecordManualEndTurnIntent(me.NetId, "end-turn-button");
        }

        if (me != null && CombatManager.Instance.IsPlayerReadyToEndTurn(me))
        {
            LocalMultiControlLogger.Info($"忽略结束回合回退点击: player={me.NetId}");
            return false;
        }

        return true;
    }
}
