using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Rooms;

namespace LocalMultiControl.Scripts.Rewards;

/// <summary>
/// 标记当前是否处于战利品汇总流程中。
/// 当处于汇总流程时，遗物/药水/金币的镜像复制应被抑制，
/// 因为每个角色已经独立生成了自己的奖励。
/// 使用静态计数器而非 AsyncLocal，确保在 UI 回调中也能正确读取。
/// </summary>
internal static class CombatRewardMergeContext
{
    private sealed class MergeMarker
    {
    }

    private static int _depth;
    private static readonly ConditionalWeakTable<AbstractRoom, MergeMarker> MergedRooms = new();

    internal static bool IsActive => _depth > 0;

    internal static bool TryMarkRoomMerged(AbstractRoom room)
    {
        lock (MergedRooms)
        {
            if (MergedRooms.TryGetValue(room, out _))
            {
                return false;
            }

            MergedRooms.Add(room, new MergeMarker());
            return true;
        }
    }

    internal static void Enter()
    {
        _depth++;
    }

    internal static void Exit()
    {
        if (_depth > 0)
        {
            _depth--;
        }
    }
}
