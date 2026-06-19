using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(RunLobby), nameof(RunLobby.AbandonRun))]
internal static class RunLobbyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(RunLobby __instance)
    {
        bool shouldHandleLocally = LocalSelfCoopContext.IsEnabled || RunManager.Instance.NetService is LocalLoopbackHostGameService;
        if (!shouldHandleLocally)
        {
            return true;
        }

        LocalMultiControlLogger.Info("本地多控接管 RunLobby.AbandonRun，直接执行整局放弃流程。");
        ((IRunLobbyListener)RunManager.Instance).RunAbandoned();
        return false;
    }
}
