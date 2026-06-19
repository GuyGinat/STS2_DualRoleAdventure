using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Platform;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(PlatformUtil), nameof(PlatformUtil.GetPlayerName))]
internal static class PlatformUtilGetPlayerNamePatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlatformType platformType, ulong playerId, ref string __result)
    {
        if (!LocalSelfCoopContext.TryGetSlotIndex(playerId, out int slotIndex))
        {
            return true;
        }

        __result = LocalModText.RoleSlot((slotIndex + 1).ToString());
        return false;
    }
}
