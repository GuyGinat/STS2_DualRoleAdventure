using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NInputManager), nameof(NInputManager._UnhandledInput))]
internal static class NInputManagerPatch
{
    private static ulong _lastBlockLogAtMs;

    [HarmonyPrefix]
    private static bool Prefix(InputEvent inputEvent)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return true;
        }

        bool intercepted = LocalGamepadAxisRouter.TryInterceptControllerInput(inputEvent);
        if (intercepted)
        {
            LocalMultiControlLogger.Info("[LT组合] 已拦截 NInputManager 控制器输入，阻止原 LT 逻辑。");
            return false;
        }

        if (LocalGamepadAxisRouter.ShouldBlockOriginalControllerInput(inputEvent))
        {
            ulong now = Time.GetTicksMsec();
            if (now - _lastBlockLogAtMs >= 500)
            {
                _lastBlockLogAtMs = now;
                LocalMultiControlLogger.Info("[LT组合] LT持有期间已全拦截控制器原始输入。");
            }

            return false;
        }

        return true;
    }
}
