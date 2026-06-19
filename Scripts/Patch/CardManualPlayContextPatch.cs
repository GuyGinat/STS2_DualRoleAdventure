using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(CardModel), "EnqueueManualPlay", new[] { typeof(Creature) })]
internal static class CardManualPlayContextPatch
{
    private readonly struct ManualPlayPatchState
    {
        public ManualPlayPatchState(bool guardEntered)
        {
            GuardEntered = guardEntered;
        }

        public bool GuardEntered { get; }
    }

    [HarmonyPrefix]
    private static void Prefix(CardModel __instance, ref ManualPlayPatchState __state)
    {
        __state = new ManualPlayPatchState(guardEntered: false);
        if (!LocalSelfCoopContext.IsEnabled || !RunManager.Instance.IsInProgress)
        {
            return;
        }

        __state = new ManualPlayPatchState(guardEntered: true);
        LocalManualPlayGuard.Enter("CardModel.EnqueueManualPlay");

        Player? owner = __instance.Owner;
        if (owner == null)
        {
            return;
        }

        LocalMultiControlRuntime.AlignContextForActionOwner(owner.NetId, "card-enqueue-manual-play");
    }

    [HarmonyFinalizer]
    private static System.Exception? Finalizer(System.Exception? __exception, ManualPlayPatchState __state)
    {
        if (__state.GuardEntered)
        {
            LocalManualPlayGuard.Exit("CardModel.EnqueueManualPlay");
        }

        return __exception;
    }
}
