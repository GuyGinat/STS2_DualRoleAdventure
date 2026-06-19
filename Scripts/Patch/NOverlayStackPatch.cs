using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NOverlayStack), nameof(NOverlayStack.Remove))]
internal static class NOverlayStackPatch
{
    [HarmonyPostfix]
    private static void Postfix(NOverlayStack __instance)
    {
        if (!LocalSelfCoopContext.IsEnabled || __instance.ScreenCount > 0)
        {
            return;
        }

        if (LocalSelfCoopContext.UseSingleEventFlow)
        {
            return;
        }

        if (!RunManager.Instance.IsInProgress)
        {
            return;
        }

        Callable.From(delegate
        {
            LocalMultiControlRuntime.TryRunPendingEventAutoSwitch("event-auto-next");
        }).CallDeferred();
    }
}
