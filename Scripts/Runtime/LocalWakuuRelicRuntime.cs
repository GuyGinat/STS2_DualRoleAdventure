using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LocalMultiControl.Scripts.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;
using Godot;

namespace LocalMultiControl.Scripts.Runtime;

internal static class LocalWakuuRelicRuntime
{
    private const int MaxCardsToPlay = 13;
    private const long WatchdogRestartCooldownMs = 300L;

    private static readonly Dictionary<string, long> _watchdogLastRunAt = new();
    private static readonly HashSet<string> _watchdogInFlight = new();
    private static readonly SemaphoreSlim SelectorScopeGate = new(1, 1);
    private static readonly FieldInfo? SelectorStackField =
        typeof(CardSelectCmd).GetField("_selectorStack", BindingFlags.NonPublic | BindingFlags.Static);
    private static int _selectorScopeInFlight;

    public readonly struct SelectorStackSnapshot
    {
        public SelectorStackSnapshot(int count, string topType, bool allVakuuSelectors)
        {
            Count = count;
            TopType = topType;
            AllVakuuSelectors = allVakuuSelectors;
        }

        public int Count { get; }

        public string TopType { get; }

        public bool AllVakuuSelectors { get; }
    }

    public static LocalWakuuStarterRelic? TryGetWakuuRelic(Player player)
    {
        return player.GetRelicById(ModelDb.GetId<LocalWakuuStarterRelic>()) as LocalWakuuStarterRelic;
    }

    public static bool HasWakuuRelic(Player player)
    {
        return TryGetWakuuRelic(player) != null;
    }

    public static async Task ExecuteBeforePlayPhaseStartAsync(
        LocalWakuuStarterRelic relic,
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (!LocalSelfCoopContext.IsEnabled || player != relic.Owner)
        {
            return;
        }

        ICombatState? combatState = player.Creature.CombatState;
        if (combatState == null || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        CardModel? firstPlayableCard = PileType.Hand.GetPile(relic.Owner).Cards.FirstOrDefault((candidate) => candidate.CanPlay());
        if (firstPlayableCard == null)
        {
            return;
        }

        EnsureWakuuPerspective(player, "before-play-phase");
        relic.Flash();

        bool reachedPlayLimit;
        int cardsPlayed;
        bool gateEntered = false;
        ulong enterTick = Time.GetTicksMsec();
        ulong gateWaitStartTick = Time.GetTicksMsec();
        LocalMultiControlLogger.Info(
            $"瓦库选择器闸门等待: player={player.NetId}, round={combatState.RoundNumber}, source={choiceContext.GetType().Name}, inFlight={Volatile.Read(ref _selectorScopeInFlight)}");
        await SelectorScopeGate.WaitAsync();
        gateEntered = true;
        int inFlight = Interlocked.Increment(ref _selectorScopeInFlight);
        ulong gateWaitMs = Time.GetTicksMsec() - gateWaitStartTick;
        SelectorStackSnapshot gateEnterSnapshot = SnapshotSelectorStack();
        LocalMultiControlLogger.Info(
            $"瓦库选择器闸门已进入: player={player.NetId}, round={combatState.RoundNumber}, waitMs={gateWaitMs}, inFlight={inFlight}, selectorStackCount={gateEnterSnapshot.Count}, selectorStackTop={gateEnterSnapshot.TopType}");
        try
        {
            using (CardSelectCmd.PushSelector(new VakuuCardSelector()))
            {
                SelectorStackSnapshot pushSnapshot = SnapshotSelectorStack();
                LocalMultiControlLogger.Info(
                    $"瓦库选择器作用域进入: player={player.NetId}, round={combatState.RoundNumber}, selectorStackCount={pushSnapshot.Count}, selectorStackTop={pushSnapshot.TopType}");
                for (cardsPlayed = 0; cardsPlayed < MaxCardsToPlay; cardsPlayed++)
                {
                    if (TryGetAutoplayUnsafeReason(combatState, out string unsafeReason))
                    {
                        LocalMultiControlRuntime.RecordFlowBlockSignal(
                            "autoplay_skipped_due_to_phase",
                            unsafeReason,
                            player.NetId,
                            "wakuu-autoplay-loop",
                            combatState.RoundNumber,
                            dedupePerRoundPlayer: true);
                        LocalMultiControlLogger.Warn(
                            $"瓦库自动出牌已熔断跳过本次执行: player={player.NetId}, round={combatState.RoundNumber}, reason={unsafeReason}, played={cardsPlayed}");
                        break;
                    }

                    if (CombatManager.Instance.IsOverOrEnding)
                    {
                        break;
                    }

                    CardModel? card = cardsPlayed == 0
                        ? firstPlayableCard
                        : PileType.Hand.GetPile(relic.Owner).Cards.FirstOrDefault((candidate) => candidate.CanPlay());
                    if (card == null)
                    {
                        break;
                    }

                    Creature? target = ResolveTarget(card, combatState, relic.Owner);
                    await card.SpendResources();
                    await CardCmd.AutoPlay(choiceContext, card, target, AutoPlayType.Default, skipXCapture: true);
                }

                reachedPlayLimit = cardsPlayed >= MaxCardsToPlay;
                SelectorStackSnapshot popSnapshot = SnapshotSelectorStack();
                LocalMultiControlLogger.Info(
                    $"瓦库选择器作用域退出: player={player.NetId}, round={combatState.RoundNumber}, cardsPlayed={cardsPlayed}, reachedLimit={reachedPlayLimit}, selectorStackCount={popSnapshot.Count}, selectorStackTop={popSnapshot.TopType}");
            }
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn(
                $"瓦库选择器作用域异常退出: player={player.NetId}, round={combatState.RoundNumber}, error={exception.Message}");
            throw;
        }
        finally
        {
            if (gateEntered)
            {
                int remainInFlight = Interlocked.Decrement(ref _selectorScopeInFlight);
                SelectorScopeGate.Release();
                ulong elapsedMs = Time.GetTicksMsec() - enterTick;
                SelectorStackSnapshot releaseSnapshot = SnapshotSelectorStack();
                LocalMultiControlLogger.Info(
                    $"瓦库选择器闸门已释放: player={player.NetId}, round={combatState.RoundNumber}, elapsedMs={elapsedMs}, inFlight={remainInFlight}, selectorStackCount={releaseSnapshot.Count}, selectorStackTop={releaseSnapshot.TopType}");
                ProbeAndRecoverSelectorStack($"wakuu-selector-finally-{player.NetId}-{combatState.RoundNumber}", allowRecover: true);
            }
        }

        if (cardsPlayed <= 0)
        {
            return;
        }

        LocString line = reachedPlayLimit
            ? new LocString("relics", "WHISPERING_EARRING.warning")
            : new LocString("relics", "WHISPERING_EARRING.approval");
        TalkCmd.Play(line, relic.Owner.Creature, VfxColor.Purple);
    }

    public static bool TryScheduleWatchdog(Player player, string source)
    {
        return TryScheduleWatchdog(player, source, out _);
    }

    public static bool TryScheduleWatchdog(Player player, string source, out string reason)
    {
        reason = "unknown";
        if (Volatile.Read(ref _selectorScopeInFlight) > 0)
        {
            reason = "selector-scope-busy";
            return false;
        }

        if (LocalManualPlayGuard.IsActive)
        {
            reason = "manual-play-active";
            return false;
        }

        ICombatState? combatState = player.Creature.CombatState;
        if (combatState == null || combatState.CurrentSide != CombatSide.Player || CombatManager.Instance.IsOverOrEnding)
        {
            reason = "invalid-combat-state";
            return false;
        }

        LocalWakuuStarterRelic? relic = TryGetWakuuRelic(player);
        if (relic == null)
        {
            reason = "no-wakuu-relic";
            return false;
        }

        bool hasPlayableCards = PileType.Hand.GetPile(player).Cards.Any((card) => card.CanPlay());
        if (!hasPlayableCards)
        {
            reason = "no-playable-cards";
            return false;
        }

        string key = $"{combatState.RoundNumber}:{player.NetId}";
        long nowMs = (long)Time.GetTicksMsec();
        if (_watchdogInFlight.Contains(key))
        {
            reason = "watchdog-in-flight";
            return false;
        }

        if (_watchdogLastRunAt.TryGetValue(key, out long lastRunMs)
            && nowMs - lastRunMs < WatchdogRestartCooldownMs)
        {
            reason = "watchdog-cooldown";
            return false;
        }

        _watchdogLastRunAt[key] = nowMs;
        _watchdogInFlight.Add(key);
        TaskHelper.RunSafely(RunWatchdogAsync(key, relic, player, combatState, source));
        reason = "scheduled";
        return true;
    }

    private static async Task RunWatchdogAsync(
        string key,
        LocalWakuuStarterRelic relic,
        Player player,
        ICombatState combatState,
        string source)
    {
        ulong? previousNetId = LocalContext.NetId;
        ulong previousSenderId = LocalSelfCoopContext.NetService?.NetId ?? 0UL;
        bool hasNetService = LocalSelfCoopContext.NetService != null;
        try
        {
            if (LocalManualPlayGuard.IsActive)
            {
                return;
            }

            if (!RunManager.Instance.IsInProgress || !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding)
            {
                return;
            }

            if (combatState.CurrentSide != CombatSide.Player)
            {
                return;
            }

            if (RunManager.Instance.ActionQueueSynchronizer.CombatState != ActionSynchronizerCombatState.PlayPhase)
            {
                return;
            }

            if (player.Creature.CombatState != combatState || !HasWakuuRelic(player))
            {
                return;
            }

            if (!PileType.Hand.GetPile(player).Cards.Any((card) => card.CanPlay()))
            {
                return;
            }

            EnsureWakuuPerspective(player, source);
            LocalContext.NetId = player.NetId;
            LocalSelfCoopContext.NetService?.SetCurrentSenderId(player.NetId);

            HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(
                relic,
                player.NetId,
                combatState,
                GameActionType.CombatPlayPhaseOnly);
            Task action = ExecuteBeforePlayPhaseStartAsync(relic, choiceContext, player);
            await choiceContext.AssignTaskAndWaitForPauseOrCompletion(action);
            await action;
            LocalMultiControlLogger.Info($"瓦库看门狗已重启自动出牌: player={player.NetId}, source={source}");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"瓦库看门狗重启失败: player={player.NetId}, source={source}, error={exception.Message}");
        }
        finally
        {
            _watchdogInFlight.Remove(key);
            LocalContext.NetId = previousNetId;
            if (hasNetService)
            {
                LocalSelfCoopContext.NetService?.SetCurrentSenderId(previousSenderId);
            }

            Callable.From(delegate
            {
                LocalMultiControlRuntime.RequestAutoSwitchToNonWakuuOncePerRound($"wakuu-watchdog-{source}");
            }).CallDeferred();
            ProbeAndRecoverSelectorStack($"wakuu-watchdog-finally-{player.NetId}-{combatState.RoundNumber}-{source}", allowRecover: true);
        }
    }

    public static void ProbeAndRecoverSelectorStack(string source, bool allowRecover)
    {
        SelectorStackSnapshot snapshot = SnapshotSelectorStack();
        LocalMultiControlLogger.Info(
            $"瓦库选择器栈探针: source={source}, selectorStackCount={snapshot.Count}, selectorStackTop={snapshot.TopType}, allVakuu={snapshot.AllVakuuSelectors}, inFlight={Volatile.Read(ref _selectorScopeInFlight)}");

        if (!allowRecover || snapshot.Count <= 0)
        {
            return;
        }

        if (Volatile.Read(ref _selectorScopeInFlight) > 0)
        {
            return;
        }

        if (!snapshot.AllVakuuSelectors)
        {
            return;
        }

        if (TryClearSelectorStack(out int clearedCount))
        {
            LocalMultiControlLogger.Warn(
                $"检测到瓦库选择器栈残留，已执行自恢复清理: source={source}, clearedCount={clearedCount}, selectorStackTop={snapshot.TopType}");
        }
    }

    public static SelectorStackSnapshot SnapshotSelectorStack()
    {
        object? rawStack = SelectorStackField?.GetValue(null);
        if (rawStack == null)
        {
            return new SelectorStackSnapshot(0, "null", allVakuuSelectors: false);
        }

        Type stackType = rawStack.GetType();
        int count = (int?)stackType.GetProperty("Count")?.GetValue(rawStack) ?? 0;
        object? top = count > 0 ? stackType.GetMethod("Peek")?.Invoke(rawStack, null) : null;
        string topType = top?.GetType().Name ?? "none";

        bool allVakuuSelectors = count > 0;
        if (rawStack is IEnumerable enumerable)
        {
            foreach (object? selector in enumerable)
            {
                if (selector is not VakuuCardSelector)
                {
                    allVakuuSelectors = false;
                    break;
                }
            }
        }
        else
        {
            allVakuuSelectors = false;
        }

        return new SelectorStackSnapshot(count, topType, allVakuuSelectors);
    }

    private static bool TryClearSelectorStack(out int clearedCount)
    {
        clearedCount = 0;
        object? rawStack = SelectorStackField?.GetValue(null);
        if (rawStack == null)
        {
            return false;
        }

        Type stackType = rawStack.GetType();
        int count = (int?)stackType.GetProperty("Count")?.GetValue(rawStack) ?? 0;
        if (count <= 0)
        {
            return false;
        }

        stackType.GetMethod("Clear")?.Invoke(rawStack, null);
        clearedCount = count;
        return true;
    }

    private static Creature? ResolveTarget(CardModel card, ICombatState combatState, Player owner)
    {
        return card.TargetType switch
        {
            TargetType.AnyEnemy => combatState.HittableEnemies.FirstOrDefault(),
            TargetType.AnyAlly => owner.RunState.Rng.CombatTargets.NextItem(
                combatState.Allies.Where((creature) => creature != null && creature.IsAlive && creature.IsPlayer && creature != owner.Creature)),
            TargetType.AnyPlayer => owner.Creature,
            _ => null
        };
    }

    private static void EnsureWakuuPerspective(Player player, string source)
    {
        ulong currentControlledPlayerId = LocalMultiControlRuntime.SessionState.CurrentControlledPlayerId
            ?? LocalContext.NetId
            ?? player.NetId;
        if (currentControlledPlayerId == player.NetId)
        {
            return;
        }

        LocalMultiControlLogger.Info($"瓦库自动操作前切换视角: {currentControlledPlayerId} -> {player.NetId}, source={source}");
        LocalMultiControlRuntime.SwitchControlledPlayerTo(player.NetId, $"wakuu-{source}");
    }

    private static bool TryGetAutoplayUnsafeReason(ICombatState combatState, out string reason)
    {
        reason = string.Empty;
        if (!RunManager.Instance.IsInProgress || !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding)
        {
            reason = "combat-not-in-progress";
            return true;
        }

        if (RunManager.Instance.ActionQueueSynchronizer.CombatState != ActionSynchronizerCombatState.PlayPhase)
        {
            reason = $"sync-{RunManager.Instance.ActionQueueSynchronizer.CombatState}";
            return true;
        }

        if (combatState.CurrentSide != CombatSide.Player)
        {
            reason = $"side-{combatState.CurrentSide}";
            return true;
        }

        NCombatUi? combatUi = NCombatRoom.Instance?.Ui;
        if (combatUi == null)
        {
            reason = "combat-ui-null";
            return true;
        }

        NPlayerHand hand = combatUi.Hand;
        if (hand.InCardPlay)
        {
            reason = "hand-in-card-play";
            return true;
        }

        if (hand.IsInCardSelection)
        {
            reason = "hand-in-card-selection";
            return true;
        }

        if (NTargetManager.Instance?.IsInSelection ?? false)
        {
            reason = "target-selecting";
            return true;
        }

        return false;
    }
}
