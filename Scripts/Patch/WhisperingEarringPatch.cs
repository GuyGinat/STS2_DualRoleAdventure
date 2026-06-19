using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Relics;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(WhisperingEarring), nameof(WhisperingEarring.AfterAutoPrePlayPhaseEnteredLate))]
internal static class WhisperingEarringPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        // 保持原版低语耳环行为，避免与本地多控的瓦库专用遗物混用导致持续接管。
        return true;
    }
}
