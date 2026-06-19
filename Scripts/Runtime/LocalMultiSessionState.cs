using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Runtime;

internal sealed class LocalMultiSessionState
{
    private readonly List<ulong> _orderedPlayerIds = new();

    private int _activeIndex;

    public bool IsInitialized { get; private set; }

    public IReadOnlyList<ulong> OrderedPlayerIds => _orderedPlayerIds;

    public ulong? CurrentControlledPlayerId
    {
        get
        {
            if (!IsInitialized || _orderedPlayerIds.Count == 0)
            {
                return null;
            }

            return _orderedPlayerIds[_activeIndex];
        }
    }

    public void InitializeFromRunState(RunState runState)
    {
        Reset("准备初始化新会话");

        if (runState.Players.Count < 2)
        {
            LocalMultiControlLogger.Info($"本次运行玩家数={runState.Players.Count}，玩家不足2时不启用本地多控会话。");
            return;
        }

        if (LocalSelfCoopContext.IsEnabled)
        {
            foreach (ulong localPlayerId in LocalSelfCoopContext.LocalPlayerIds)
            {
                Player? player = runState.GetPlayer(localPlayerId);
                if (player != null && !_orderedPlayerIds.Contains(player.NetId))
                {
                    _orderedPlayerIds.Add(player.NetId);
                }
            }
        }

        if (_orderedPlayerIds.Count == 0)
        {
            foreach (Player player in runState.Players)
            {
                if (!_orderedPlayerIds.Contains(player.NetId))
                {
                    _orderedPlayerIds.Add(player.NetId);
                }
            }
        }

        _activeIndex = 0;
        IsInitialized = true;
        LocalMultiControlLogger.Info(
            $"本地多控会话已初始化，玩家列表: {string.Join(",", _orderedPlayerIds)}，当前操控玩家: {_orderedPlayerIds[_activeIndex]}");
    }

    public void Reset(string reason)
    {
        if (IsInitialized || _orderedPlayerIds.Count > 0)
        {
            LocalMultiControlLogger.Info($"重置本地多控会话，原因: {reason}");
        }

        IsInitialized = false;
        _orderedPlayerIds.Clear();
        _activeIndex = 0;
    }

    public bool SwitchNextPlayer()
    {
        if (!CanSwitch("切换到下一位"))
        {
            return false;
        }

        int previousIndex = _activeIndex;
        _activeIndex = (_activeIndex + 1) % _orderedPlayerIds.Count;
        LocalMultiControlLogger.Info($"切换操控角色(下一位): {_orderedPlayerIds[previousIndex]} -> {_orderedPlayerIds[_activeIndex]}");
        return true;
    }

    public bool SwitchPreviousPlayer()
    {
        if (!CanSwitch("切换到上一位"))
        {
            return false;
        }

        int previousIndex = _activeIndex;
        _activeIndex = (_activeIndex - 1 + _orderedPlayerIds.Count) % _orderedPlayerIds.Count;
        LocalMultiControlLogger.Info($"切换操控角色(上一位): {_orderedPlayerIds[previousIndex]} -> {_orderedPlayerIds[_activeIndex]}");
        return true;
    }

    public bool TrySetCurrentPlayer(ulong playerId)
    {
        if (!IsInitialized)
        {
            return false;
        }

        int index = _orderedPlayerIds.IndexOf(playerId);
        if (index < 0)
        {
            LocalMultiControlLogger.Warn($"尝试设置当前操控角色失败：玩家 {playerId} 不在会话中。");
            return false;
        }

        if (_activeIndex == index)
        {
            return true;
        }

        ulong previousPlayerId = _orderedPlayerIds[_activeIndex];
        _activeIndex = index;
        LocalMultiControlLogger.Info($"切换操控角色(指定): {previousPlayerId} -> {_orderedPlayerIds[_activeIndex]}");
        return true;
    }

    private bool CanSwitch(string actionName)
    {
        if (!IsInitialized)
        {
            LocalMultiControlLogger.Warn($"忽略{actionName}，原因: 会话未初始化。");
            return false;
        }

        if (_orderedPlayerIds.Count < 2)
        {
            LocalMultiControlLogger.Warn($"忽略{actionName}，原因: 玩家数量不足2。");
            return false;
        }

        return true;
    }
}
