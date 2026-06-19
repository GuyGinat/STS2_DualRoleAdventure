using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))]
internal static class NMerchantInventoryPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref MerchantInventory inventory)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState?.CurrentRoom is not MerchantRoom merchantRoom)
        {
            return;
        }

        ulong currentPlayerId = LocalContext.NetId ?? LocalSelfCoopContext.PrimaryPlayerId;
        Player? currentPlayer = runState.GetPlayer(currentPlayerId) ?? LocalContext.GetMe(runState);
        if (currentPlayer == null)
        {
            return;
        }

        inventory = LocalMerchantInventoryRuntime.GetOrCreateInventory(merchantRoom, currentPlayer);
        LocalMerchantInventoryRuntime.BindInventoryToRoom(merchantRoom, currentPlayer, inventory);
        LocalMultiControlLogger.Info($"商店库存绑定到当前角色: player={currentPlayer.NetId}");
    }
}
