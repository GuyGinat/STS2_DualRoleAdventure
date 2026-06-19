using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(CardReward), "OnSelect")]
internal static class CardRewardPatch
{
    private struct SenderState
    {
        internal bool IsPatched;
        internal ulong? PreviousContextNetId;
        internal ulong PreviousSenderId;
    }

    [HarmonyPrefix]
    private static void Prefix(CardReward __instance, ref SenderState __state)
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

        LocalContext.NetId = __instance.Player.NetId;
        loopback.SetCurrentSenderId(__instance.Player.NetId);
        LocalMultiControlLogger.Info($"卡牌奖励切换到奖励归属角色: player={__instance.Player.NetId}");
    }

    [HarmonyPostfix]
    private static void Postfix(SenderState __state)
    {
        if (!__state.IsPatched)
        {
            return;
        }

        if (RunManager.Instance.NetService is LocalLoopbackHostGameService loopback && loopback.NetId != __state.PreviousSenderId)
        {
            loopback.SetCurrentSenderId(__state.PreviousSenderId);
        }

        LocalContext.NetId = __state.PreviousContextNetId;
    }
}
