using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(CrystalSphere), "PaymentPlan")]
internal static class CrystalSpherePaymentPlanPatch
{
    private static readonly AsyncLocal<bool> IsMirroringDebt = new();

    [HarmonyPostfix]
    private static void Postfix(CrystalSphere __instance, ref Task __result)
    {
        __result = MirrorDebtToOtherPlayersAsync(__instance, __result);
    }

    private static async Task MirrorDebtToOtherPlayersAsync(CrystalSphere eventModel, Task originalTask)
    {
        await originalTask;

        if (IsMirroringDebt.Value || !CrystalSphereMirrorRuntime.IsInCrystalSphereEventContext(eventModel.Owner))
        {
            return;
        }

        if (eventModel.Owner == null)
        {
            return;
        }

        IsMirroringDebt.Value = true;
        try
        {
            MegaCrit.Sts2.Core.Entities.Players.Player owner = eventModel.Owner;
            System.Collections.Generic.List<MegaCrit.Sts2.Core.Entities.Players.Player> otherPlayers = CrystalSphereMirrorRuntime.GetOtherPlayers(owner);
            foreach (MegaCrit.Sts2.Core.Entities.Players.Player otherPlayer in otherPlayers)
            {
                await CardPileCmd.AddCurseToDeck<Debt>(otherPlayer);
            }

            LocalMultiControlLogger.Info(
                $"水晶球事件债务卡已同步加入其余角色: owner={owner.NetId}, mirrored={string.Join(",", otherPlayers.Select((player) => player.NetId))}");
        }
        finally
        {
            IsMirroringDebt.Value = false;
        }
    }
}
