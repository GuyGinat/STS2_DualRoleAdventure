using System.Runtime.CompilerServices;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Rewards;

namespace LocalMultiControl.Scripts.Rewards;

/// <summary>
/// 记录每个 Reward 实例对应的角色标签（如"角色1"）。
/// 在战利品汇总时写入，在 NRewardButton 展示时读取。
/// </summary>
internal static class RewardPlayerLabelRegistry
{
    private static readonly ConditionalWeakTable<Reward, string> Labels = new();

    internal static void Register(Reward reward, ulong playerNetId)
    {
        string slotLabel = LocalSelfCoopContext.GetSlotLabel(playerNetId);
        string label = LocalModText.RoleSlot(slotLabel);
        Labels.AddOrUpdate(reward, label);
    }

    internal static bool TryGetLabel(Reward reward, out string? label)
    {
        return Labels.TryGetValue(reward, out label);
    }
}
