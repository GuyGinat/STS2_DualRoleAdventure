using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Runtime;

internal static class LocalControlSwitchGuard
{
    public static bool TrySwitchNext(string source)
    {
        if (!CanSwitchNow(source))
        {
            return false;
        }

        LocalMultiControlRuntime.SwitchNextControlledPlayer(source);
        return true;
    }

    public static bool TrySwitchPrevious(string source)
    {
        if (!CanSwitchNow(source))
        {
            return false;
        }

        LocalMultiControlRuntime.SwitchPreviousControlledPlayer(source);
        return true;
    }

    public static bool TrySwitchTo(ulong playerId, string source)
    {
        if (!CanSwitchNow(source))
        {
            return false;
        }

        LocalMultiControlRuntime.SwitchControlledPlayerTo(playerId, source);
        return true;
    }

    private static bool CanSwitchNow(string source)
    {
        if (!LocalSelfCoopContext.IsEnabled || !RunManager.Instance.IsInProgress)
        {
            return false;
        }

        if (!CombatManager.Instance.IsInProgress)
        {
            return true;
        }

        NCombatUi? combatUi = NCombatRoom.Instance?.Ui;
        if (combatUi == null)
        {
            return false;
        }

        if (combatUi.Hand.InCardPlay || combatUi.Hand.IsInCardSelection || (NTargetManager.Instance?.IsInSelection ?? false))
        {
            LocalMultiControlLogger.Info($"忽略切人请求({source})：当前有进行中的出牌/选牌流程。");
            return false;
        }

        if (RunManager.Instance.ActionQueueSynchronizer.CombatState != ActionSynchronizerCombatState.PlayPhase)
        {
            LocalMultiControlLogger.Info(
                $"忽略切人请求({source})：战斗同步阶段={RunManager.Instance.ActionQueueSynchronizer.CombatState}。");
            return false;
        }

        return true;
    }
}
