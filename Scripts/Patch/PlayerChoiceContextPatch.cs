using System;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(PlayerChoiceSynchronizer), nameof(PlayerChoiceSynchronizer.SyncLocalChoice))]
internal static class PlayerChoiceSynchronizerPatch
{
    private struct SenderState
    {
        internal bool IsPatched;
        internal ulong? PreviousContextNetId;
        internal ulong PreviousSenderId;
    }

    [HarmonyPrefix]
    private static void Prefix(Player player, ref SenderState __state)
    {
        __state = default;
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        if (RunManager.Instance.NetService is not LocalLoopbackHostGameService loopback)
        {
            return;
        }

        __state.IsPatched = true;
        __state.PreviousContextNetId = LocalContext.NetId;
        __state.PreviousSenderId = loopback.NetId;

        LocalContext.NetId = player.NetId;
        loopback.SetCurrentSenderId(player.NetId);
    }

    [HarmonyPostfix]
    private static void Postfix(ref SenderState __state)
    {
        RestoreSenderState(ref __state, "postfix");
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, ref SenderState __state)
    {
        RestoreSenderState(ref __state, "finalizer");
        return __exception;
    }

    private static void RestoreSenderState(ref SenderState state, string source)
    {
        if (!state.IsPatched)
        {
            return;
        }

        if (RunManager.Instance.NetService is LocalLoopbackHostGameService loopback && loopback.NetId != state.PreviousSenderId)
        {
            loopback.SetCurrentSenderId(state.PreviousSenderId);
        }

        LocalContext.NetId = state.PreviousContextNetId;
        state.IsPatched = false;
        LocalMultiControlLogger.Info($"PlayerChoice sender/context 已恢复: source={source}");
    }
}

[HarmonyPatch(typeof(GameActionPlayerChoiceContext), nameof(GameActionPlayerChoiceContext.SignalPlayerChoiceEnded))]
internal static class GameActionPlayerChoiceContextPatch
{
    [HarmonyPrefix]
    private static bool Prefix(GameActionPlayerChoiceContext __instance, ref Task __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return true;
        }

        __result = ResumeInLocalSelfCoopAsync(__instance);
        return false;
    }

    private static async Task ResumeInLocalSelfCoopAsync(GameActionPlayerChoiceContext context)
    {
        RunManager.Instance.ActionQueueSynchronizer.RequestResumeActionAfterPlayerChoice(context.Action);
        await context.Action.WaitForActionToResumeExecutingAfterPlayerChoice();
    }
}

[HarmonyPatch(typeof(HookPlayerChoiceContext), nameof(HookPlayerChoiceContext.SignalPlayerChoiceEnded))]
internal static class HookPlayerChoiceContextPatch
{
    [HarmonyPrefix]
    private static bool Prefix(HookPlayerChoiceContext __instance, ref Task __result)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return true;
        }

        __result = ResumeInLocalSelfCoopAsync(__instance);
        return false;
    }

    private static async Task ResumeInLocalSelfCoopAsync(HookPlayerChoiceContext context)
    {
        if (context.GameAction != null)
        {
            RunManager.Instance.ActionQueueSynchronizer.RequestResumeActionAfterPlayerChoice(context.GameAction);
            await context.GameAction.WaitForActionToResumeExecutingAfterPlayerChoice();
        }
    }
}
