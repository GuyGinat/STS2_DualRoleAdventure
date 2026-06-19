using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NRemoteMouseCursorContainer), nameof(NRemoteMouseCursorContainer.GetCursorPosition))]
internal static class NRemoteMouseCursorContainerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NRemoteMouseCursorContainer), nameof(NRemoteMouseCursorContainer._Input))]
    private static bool PrefixInput()
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return true;
        }

        // 风险点：本地双人复用同进程时，远端鼠标输入同步会持续驱动 PeerInput 状态变更，
        // 在切人/节点销毁窗口中容易触发 NRemoteTargetingIndicator 的已释放对象访问。
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NRemoteMouseCursorContainer), "OnGuiFocusChanged")]
    private static bool PrefixOnGuiFocusChanged()
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return true;
        }

        // 风险点：焦点变化会触发 SyncLocalIsUsingController，进一步驱动远端意图刷新链路。
        // 本地双人下该链路无业务价值，且会放大战斗切换时的生命周期竞争。
        return false;
    }

    [HarmonyPrefix]
    private static bool Prefix(NRemoteMouseCursorContainer __instance, ulong playerId, ref Vector2 __result)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return true;
        }

        try
        {
            object? cursor = AccessTools.Method(typeof(NRemoteMouseCursorContainer), "GetCursor")?.Invoke(__instance, new object[] { playerId });
            if (cursor == null)
            {
                __result = Vector2.Zero;
                return false;
            }
        }
        catch
        {
            __result = Vector2.Zero;
            return false;
        }

        return true;
    }
}
