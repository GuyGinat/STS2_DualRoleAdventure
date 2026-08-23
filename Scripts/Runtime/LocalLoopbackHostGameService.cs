using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync;
using MegaCrit.Sts2.Core.Multiplayer.Quality;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Runtime;

internal sealed class LocalLoopbackHostGameService : INetHostGameService
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    private readonly List<Action> _bufferedDispatches = new();

    private readonly List<NetClientData> _connectedPeers = new();

    private ulong _currentSenderId;

    private bool _isBufferingMessages;

    public LocalLoopbackHostGameService(ulong hostPlayerId)
    {
        _currentSenderId = hostPlayerId;
        IsConnected = true;
        LocalMultiControlLogger.Info($"创建本地回环网络服务，初始 sender={_currentSenderId}");
    }

    public ulong NetId => _currentSenderId;

    public bool IsConnected { get; private set; }

    public bool IsGameLoading { get; private set; }

    public NetGameType Type => NetGameType.Host;

    public PlatformType Platform => PlatformType.None;

    public IReadOnlyList<NetClientData> ConnectedPeers => _connectedPeers;

    public NetHost? NetHost => null;

    // v0.111.0: every loopback peer is this same game instance, so the local
    // version info is the correct answer for all of them. The game's lobbies
    // dereference GetVersionInfoForPeer(...).Value unguarded, so never return null
    // for a connected peer.
    public PeerVersionInfo LocalVersion => PeerVersionInfo.LocalDefault();

    public event Action<NetErrorInfo>? Disconnected;

    public event Action<ulong>? ClientConnected;

    public event Action<ulong, NetErrorInfo>? ClientDisconnected;

    // v0.111.0: handshake failures cannot happen on the loopback; never raised.
#pragma warning disable CS0067
    public event Action<ulong, NetErrorInfo>? ClientConnectionFailed;
#pragma warning restore CS0067

    public void SetCurrentSenderId(ulong playerId)
    {
        if (_currentSenderId == playerId)
        {
            return;
        }

        LocalMultiControlLogger.Info($"sender切换: {_currentSenderId} -> {playerId}");
        _currentSenderId = playerId;
    }

    public void SendMessage<T>(T message, ulong playerId) where T : INetMessage
    {
        AlignSenderWithLocalContext();
        LocalMultiControlLogger.Info($"本地回环定向发消息: {typeof(T).Name}, sender={_currentSenderId}, target={playerId}");
    }

    public void SendMessage<T>(T message) where T : INetMessage
    {
        AlignSenderWithLocalContext();
        if (message is not PeerInputMessage)
        {
            LocalMultiControlLogger.Info($"本地回环广播消息: {typeof(T).Name}, sender={_currentSenderId}");
        }

        TryDispatchSyntheticLocalPlayerSync(message);
    }

    public void RegisterMessageHandler<T>(MessageHandlerDelegate<T> messageHandlerDelegate) where T : INetMessage
    {
        Type messageType = typeof(T);
        if (!_handlers.TryGetValue(messageType, out List<Delegate>? handlers))
        {
            handlers = new List<Delegate>();
            _handlers[messageType] = handlers;
        }

        handlers.Add(messageHandlerDelegate);
    }

    public void UnregisterMessageHandler<T>(MessageHandlerDelegate<T> messageHandlerDelegate) where T : INetMessage
    {
        Type messageType = typeof(T);
        if (_handlers.TryGetValue(messageType, out List<Delegate>? handlers))
        {
            handlers.Remove(messageHandlerDelegate);
        }
    }

    public void DispatchLoopback<T>(T message, ulong senderId) where T : INetMessage
    {
        if (_isBufferingMessages && message.ShouldBuffer)
        {
            _bufferedDispatches.Add(() => DispatchLoopback(message, senderId));
            LocalMultiControlLogger.Info($"本地回环消息进入缓冲: {typeof(T).Name}, sender={senderId}, buffered={_bufferedDispatches.Count}");
            return;
        }

        Type messageType = typeof(T);
        if (!_handlers.TryGetValue(messageType, out List<Delegate>? handlers) || handlers.Count == 0)
        {
            LocalMultiControlLogger.Info($"本地回环消息分发: {messageType.Name}, sender={senderId}, handlers=0");
            return;
        }

        LocalMultiControlLogger.Info($"本地回环消息分发: {messageType.Name}, sender={senderId}, handlers={handlers.Count}");
        foreach (Delegate handler in handlers)
        {
            if (handler is MessageHandlerDelegate<T> typedHandler)
            {
                typedHandler(message, senderId);
            }
        }
    }

    public void Update()
    {
    }

    public void Disconnect(NetError reason, bool now = false)
    {
        if (!IsConnected)
        {
            return;
        }

        IsConnected = false;
        LocalMultiControlLogger.Info($"本地回环网络断开: reason={reason}, now={now}");
        Disconnected?.Invoke(new NetErrorInfo(reason, selfInitiated: true));
    }

    public ConnectionStats? GetStatsForPeer(ulong peerId)
    {
        return null;
    }

    public void SetGameLoading(bool isLoading)
    {
        IsGameLoading = isLoading;
        LocalMultiControlLogger.Info($"本地回环加载状态更新: {isLoading}");
    }

    public void SetBufferMessages(bool bufferMessages)
    {
        if (_isBufferingMessages == bufferMessages)
        {
            return;
        }

        _isBufferingMessages = bufferMessages;
        if (bufferMessages)
        {
            LocalMultiControlLogger.Info("本地回环开始缓冲消息");
            return;
        }

        List<Action> bufferedDispatches = new(_bufferedDispatches);
        _bufferedDispatches.Clear();
        LocalMultiControlLogger.Info($"本地回环释放缓冲消息: count={bufferedDispatches.Count}");
        foreach (Action dispatch in bufferedDispatches)
        {
            dispatch();
        }
    }

    public string? GetRawLobbyIdentifier()
    {
        return "local-self-coop";
    }

    public void DisconnectClient(ulong peerId, NetError reason, bool now = false)
    {
        LocalMultiControlLogger.Warn($"本地回环请求断开客户端被忽略: peer={peerId}, reason={reason}, now={now}");
        ClientDisconnected?.Invoke(peerId, new NetErrorInfo(reason, selfInitiated: true));
    }

    public PeerVersionInfo? GetVersionInfoForPeer(ulong peerId)
    {
        return LocalVersion;
    }

    public void SetPeerReadyForBroadcasting(ulong peerId)
    {
        LocalMultiControlLogger.Info($"本地回环设置广播就绪（占位）: peer={peerId}");
        ClientConnected?.Invoke(peerId);
    }

    private void TryDispatchSyntheticLocalPlayerSync<T>(T message) where T : INetMessage
    {
        if (_currentSenderId != LocalSelfCoopContext.PrimaryPlayerId)
        {
            return;
        }

        if (message is not SyncPlayerDataMessage)
        {
            return;
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null)
        {
            return;
        }

        int dispatchCount = 0;
        foreach (var player in runState.Players.Where((candidate) => candidate.NetId != LocalSelfCoopContext.PrimaryPlayerId))
        {
            SyncPlayerDataMessage syntheticMessage = new()
            {
                player = player.ToSerializable()
            };
            DispatchLoopback(syntheticMessage, player.NetId);
            dispatchCount++;
        }

        if (dispatchCount > 0)
        {
            LocalMultiControlLogger.Info($"本地回环已注入额外玩家同步消息: count={dispatchCount}");
        }
    }

    private void AlignSenderWithLocalContext()
    {
        if (LocalContext.NetId.HasValue && LocalContext.NetId.Value != _currentSenderId)
        {
            SetCurrentSenderId(LocalContext.NetId.Value);
        }
    }
}
