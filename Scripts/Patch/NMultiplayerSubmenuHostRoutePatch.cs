using System;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;

namespace LocalMultiControl.Scripts.Patch;

/// <summary>
/// Since game v0.109.0, pressing Host on a profile with zero completed runs
/// (SaveManager.Progress.NumberOfRuns == 0) skips the host submenu and immediately
/// hosts a Standard online game, which makes the injected Local Multi-Control card
/// unreachable. Route those profiles through the host submenu instead; its Standard
/// card offers the same one-click hosting the shortcut provided.
/// </summary>
[HarmonyPatch(typeof(NMultiplayerSubmenu), "OnHostPressed")]
internal static class NMultiplayerSubmenuHostRoutePatch
{
    [HarmonyPrefix]
    private static bool Prefix(NMultiplayerSubmenu __instance)
    {
        try
        {
            if (SaveManager.Instance.Progress.NumberOfRuns > 0)
            {
                return true;
            }

            if (AccessTools.Field(typeof(NSubmenu), "_stack")?.GetValue(__instance) is not NSubmenuStack stack)
            {
                LocalMultiControlLogger.Warn("Host reroute: NSubmenuStack not found, falling back to vanilla direct-host flow.");
                return true;
            }

            stack.PushSubmenuType<NMultiplayerHostSubmenu>();
            LocalMultiControlLogger.Info("Fresh-profile direct-host shortcut intercepted: opened host submenu so the Local Multi-Control entry stays reachable.");
            return false;
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"Host reroute failed: {exception.Message}; using vanilla flow.");
            return true;
        }
    }
}
