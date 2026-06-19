using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Rewards;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Rewards;

namespace LocalMultiControl.Scripts.Patch;

/// <summary>
/// 在奖励按钮的描述文本前追加角色标签（如"[角色1] "）。
/// </summary>
[HarmonyPatch(typeof(NRewardButton), "Reload")]
internal static class NRewardButtonLabelPatch
{
    [HarmonyPostfix]
    private static void Postfix(NRewardButton __instance)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return;
        }

        Reward? reward = __instance.Reward;
        if (reward == null)
        {
            return;
        }

        if (!RewardPlayerLabelRegistry.TryGetLabel(reward, out string? label) || label == null)
        {
            return;
        }

        object? labelNode = AccessTools.Field(typeof(NRewardButton), "_label")?.GetValue(__instance);
        if (labelNode == null)
        {
            return;
        }

        string? currentText = AccessTools.Property(labelNode.GetType(), "Text")?.GetValue(labelNode) as string;
        if (currentText == null)
        {
            return;
        }

        string prefixedText = $"[{label}] {currentText}";
        AccessTools.Property(labelNode.GetType(), "Text")?.SetValue(labelNode, prefixedText);
    }
}

[HarmonyPatch(typeof(NRewardButton), "GetReward")]
internal static class NRewardButtonMergedRewardSelectPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NRewardButton __instance, ref Task __result)
    {
        Reward? reward = __instance.Reward;
        if (!LocalSelfCoopContext.IsEnabled
            || !LocalSelfCoopContext.UseSingleAdventureMode
            || reward == null
            || !RewardPlayerLabelRegistry.TryGetLabel(reward, out _))
        {
            return true;
        }

        __result = SelectMergedRewardAsync(__instance, reward);
        return false;
    }

    private static async Task SelectMergedRewardAsync(NRewardButton button, Reward reward)
    {
        button.Disable();
        if (await reward.SelectUnsynchronized())
        {
            button.EmitSignal(NRewardButton.SignalName.RewardClaimed, button);
            return;
        }

        button.Enable();
        button.TryGrabFocus();
        button.EmitSignal(NRewardButton.SignalName.RewardSkipped, button);
    }
}
