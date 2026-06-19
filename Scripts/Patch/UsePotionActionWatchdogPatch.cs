using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.GameActions;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(UsePotionAction), "ExecuteAction")]
internal static class UsePotionActionWatchdogPatch
{
    private const int WarnThresholdMs = 2000;

    private static int _watchIdSeed;

    [HarmonyPostfix]
    private static void Postfix(UsePotionAction __instance, Task __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        int watchId = Interlocked.Increment(ref _watchIdSeed);
        _ = WatchAsync(__instance, __result, watchId);
    }

    private static async Task WatchAsync(UsePotionAction action, Task executionTask, int watchId)
    {
        await Task.Delay(WarnThresholdMs);
        if (!executionTask.IsCompleted)
        {
            LocalMultiControlLogger.Warn($"药水动作等待选择超过{WarnThresholdMs}ms，可能导致队列阻塞: watchId={watchId}, action={action}");
        }

        try
        {
            await executionTask;
        }
        catch
        {
        }
    }
}
