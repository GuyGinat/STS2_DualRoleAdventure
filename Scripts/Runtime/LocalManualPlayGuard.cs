using System.Threading;
using Godot;

namespace LocalMultiControl.Scripts.Runtime;

internal static class LocalManualPlayGuard
{
    private static int _depth;
    private static ulong _holdUntilMs;

    public static bool IsActive
    {
        get
        {
            if (Volatile.Read(ref _depth) > 0)
            {
                return true;
            }

            return Time.GetTicksMsec() <= Volatile.Read(ref _holdUntilMs);
        }
    }

    public static void Enter(string source)
    {
        int nextDepth = Interlocked.Increment(ref _depth);
        if (nextDepth == 1)
        {
            LocalMultiControlLogger.Info($"进入手动出牌临界区: source={source}");
        }
    }

    public static void Exit(string source)
    {
        int nextDepth = Interlocked.Decrement(ref _depth);
        if (nextDepth <= 0)
        {
            Volatile.Write(ref _depth, 0);
            // 目标选择结束到动作真正入队之间存在一个很窄的异步窗口，这里补一个短暂保护期。
            Volatile.Write(ref _holdUntilMs, Time.GetTicksMsec() + 180UL);
            LocalMultiControlLogger.Info($"退出手动出牌临界区: source={source}");
        }
    }
}
