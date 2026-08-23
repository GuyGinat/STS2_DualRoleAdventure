using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace LocalMultiControl.Scripts.Patch;

/// <summary>
/// When one local character readies for the enemy turn, mirror that readiness onto every other local character so
/// the game's own all-players-ready check passes.
///
/// v0.111.0: CombatManager moved its per-combat ready sets into an internal <c>CombatTurnState</c> object
/// (<c>_turnState</c>) guarded by <c>ReadyLock</c>, and the player→enemy transition is now driven by a
/// <c>BeginEnemyTurnSignalSource</c> TaskCompletionSource awaited by the turn loop. Because the type is internal,
/// everything below goes through reflection. We no longer invoke <c>AfterAllPlayersReadyToBeginEnemyTurn</c> directly
/// — under the new turn loop that would run the transition twice; completing the signal is the correct (idempotent) way.
/// </summary>
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetReadyToBeginEnemyTurn))]
internal static class CombatManagerReadyEnemyTurnPatch
{
    private static readonly FieldInfo? TurnStateField = AccessTools.Field(typeof(CombatManager), "_turnState");
    private static PropertyInfo? _readyLockProperty;
    private static PropertyInfo? _readySetProperty;
    private static PropertyInfo? _signalSourceProperty;
    private static PropertyInfo? _isInProgressProperty;
    private static bool _membersResolved;
    private static bool _missingLogged;

    [HarmonyPrefix]
    private static void Prefix(CombatManager __instance, Player player)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        CombatState? state = __instance.DebugOnlyGetState();
        if (state == null || state.CurrentSide != CombatSide.Player || state.Players.Count < 2)
        {
            return;
        }

        object? turnState = GetTurnState(__instance, out HashSet<Player>? readySet, out Lock? readyLock);
        if (turnState == null || readySet == null)
        {
            return;
        }

        List<Player> pendingPlayers;
        using (EnterScope(readyLock))
        {
            pendingPlayers = state.Players
                .Where((candidate) => candidate.NetId != player.NetId)
                .Where((candidate) => !readySet.Contains(candidate))
                .ToList();
            foreach (Player pendingPlayer in pendingPlayers)
            {
                readySet.Add(pendingPlayer);
            }
        }

        if (pendingPlayers.Count > 0)
        {
            LocalMultiControlLogger.Info(
                $"本地多控自动补齐敌方回合就绪: trigger={player.NetId}, mirrored={string.Join(",", pendingPlayers.Select((candidate) => candidate.NetId))}");
        }
    }

    [HarmonyPostfix]
    private static void Postfix(CombatManager __instance, Func<Task>? actionDuringEnemyTurn)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        CombatState? state = __instance.DebugOnlyGetState();
        if (state == null || state.CurrentSide != CombatSide.Player || __instance.EndingPlayerTurnPhaseTwo)
        {
            return;
        }

        object? turnState = GetTurnState(__instance, out HashSet<Player>? readySet, out Lock? readyLock);
        if (turnState == null || readySet == null)
        {
            return;
        }

        TaskCompletionSource<Func<Task>?>? signalSource;
        bool allReady;
        using (EnterScope(readyLock))
        {
            allReady = readySet.Count >= state.Players.Count;
            signalSource = _signalSourceProperty?.GetValue(turnState) as TaskCompletionSource<Func<Task>?>;
        }

        // The game already completed the signal on the normal path; TrySetResult is a no-op then.
        if (allReady && signalSource != null && signalSource.TrySetResult(actionDuringEnemyTurn))
        {
            LocalMultiControlLogger.Info("检测到敌方回合未推进，触发本地兜底推进。");
        }
    }

    private static object? GetTurnState(CombatManager combatManager, out HashSet<Player>? readySet, out Lock? readyLock)
    {
        readySet = null;
        readyLock = null;

        object? turnState = TurnStateField?.GetValue(combatManager);
        if (turnState == null)
        {
            return null;
        }

        if (!_membersResolved)
        {
            Type turnStateType = turnState.GetType();
            _readyLockProperty = AccessTools.Property(turnStateType, "ReadyLock");
            _readySetProperty = AccessTools.Property(turnStateType, "PlayersReadyToBeginEnemyTurn");
            _signalSourceProperty = AccessTools.Property(turnStateType, "BeginEnemyTurnSignalSource");
            _isInProgressProperty = AccessTools.Property(turnStateType, "IsInProgress");
            _membersResolved = true;
        }

        if (_readySetProperty == null || _signalSourceProperty == null)
        {
            if (!_missingLogged)
            {
                _missingLogged = true;
                LocalMultiControlLogger.Warn("CombatTurnState members not found (PlayersReadyToBeginEnemyTurn/BeginEnemyTurnSignalSource); enemy-turn auto-ready disabled.");
            }

            return null;
        }

        if (_isInProgressProperty?.GetValue(turnState) is false)
        {
            return null;
        }

        readySet = _readySetProperty.GetValue(turnState) as HashSet<Player>;
        readyLock = _readyLockProperty?.GetValue(turnState) as Lock;
        return turnState;
    }

    private static IDisposable EnterScope(Lock? readyLock)
    {
        return new LockHolder(readyLock);
    }

    private sealed class LockHolder : IDisposable
    {
        private readonly Lock? _lock;

        internal LockHolder(Lock? readyLock)
        {
            _lock = readyLock;
            _lock?.Enter();
        }

        public void Dispose()
        {
            _lock?.Exit();
        }
    }
}
