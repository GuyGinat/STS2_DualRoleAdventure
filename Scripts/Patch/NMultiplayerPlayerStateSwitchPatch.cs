using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NMultiplayerPlayerState), nameof(NMultiplayerPlayerState._Ready))]
internal static class NMultiplayerPlayerStateReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix(NMultiplayerPlayerState __instance)
    {
        LocalMultiplayerPlayerStateSwitchUi.Ensure(__instance);
    }
}

internal static class LocalMultiplayerPlayerStateSwitchUi
{
    private const string SwitchButtonName = "LocalSwitchPlayerButton";
    private const string RightClickMetaKey = "LocalSwitchPlayerRightClickBound";
    private const string TrackerName = "LocalSwitchPlayerTracker";

    private static readonly Vector2 SmallButtonSize = new(68f, 32f);
    private static readonly Vector2 FallbackOffset = new(248f, 8f);
    private static readonly Vector2 DefaultAnchorOffset = new(2f, 0f);
    private const float HpAnchorXOffset = 12f;

    public static void Ensure(NMultiplayerPlayerState state)
    {
        BindRightClick(state);
        EnsureSwitchButton(state);
        EnsureTracker(state);
        Refresh(state);
    }

    public static void Refresh(NMultiplayerPlayerState state)
    {
        LocalSimpleTextButton? button = state.GetNodeOrNull<LocalSimpleTextButton>(SwitchButtonName);
        if (button == null)
        {
            return;
        }

        bool shouldShow = LocalSelfCoopContext.IsEnabled && RunManager.Instance.IsInProgress;
        button.Visible = shouldShow;
        if (!shouldShow)
        {
            RestoreOriginalIdLabel(state);
            return;
        }

        button.ButtonText = string.Empty;
        Rect2 anchorRect = ResolveIdAnchorRect(state);
        HideOriginalIdLabel(state);

        button.Size = SmallButtonSize;
        button.CustomMinimumSize = SmallButtonSize;
        Vector2 targetPosition = ResolveSwitchButtonPosition(state, anchorRect);
        button.GlobalPosition = targetPosition;
        RefreshCombatTrackerState(state);
    }

    private static void EnsureSwitchButton(NMultiplayerPlayerState state)
    {
        if (state.GetNodeOrNull<LocalSimpleTextButton>(SwitchButtonName) != null)
        {
            return;
        }

        LocalSimpleTextButton button = new()
        {
            Name = SwitchButtonName,
            ButtonText = string.Empty,
            FocusMode = Control.FocusModeEnum.None,
            FontSize = 18,
            Size = SmallButtonSize,
            CustomMinimumSize = SmallButtonSize,
            ImageScale = Vector2.One * 1.5f,
            TopLevel = true,
            ZIndex = 100
        };

        button.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>((_) => TrySwitchToPlayer(state.Player, "player-state-button")));
        state.AddChildSafely(button);
    }

    private static void EnsureTracker(NMultiplayerPlayerState state)
    {
        if (state.GetNodeOrNull<LocalPlayerStateSwitchTracker>(TrackerName) != null)
        {
            return;
        }

        LocalPlayerStateSwitchTracker tracker = new()
        {
            Name = TrackerName
        };
        tracker.Initialize(state);
        state.AddChild(tracker);
    }

    private static void BindRightClick(NMultiplayerPlayerState state)
    {
        if (state.Hitbox.HasMeta(RightClickMetaKey))
        {
            return;
        }

        state.Hitbox.SetMeta(RightClickMetaKey, true);
        state.Hitbox.Connect(
            NClickableControl.SignalName.MouseReleased,
            Callable.From<InputEvent>((inputEvent) => OnHitboxMouseReleased(state, inputEvent)));
    }

    private static void OnHitboxMouseReleased(NMultiplayerPlayerState state, InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Right ||
            mouseButton.IsPressed())
        {
            return;
        }

        TrySwitchToPlayer(state.Player, "player-state-right-click");
    }

    private static void TrySwitchToPlayer(Player player, string source)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        if (!LocalControlSwitchGuard.TrySwitchTo(player.NetId, source))
        {
            return;
        }

        TreasureRoomRelicSynchronizer? treasureSynchronizer = RunManager.Instance?.TreasureRoomRelicSynchronizer;
        if (treasureSynchronizer == null || treasureSynchronizer.CurrentRelics == null)
        {
            return;
        }

        _ = TreasureRoomRelicSynchronizerPatch.TryAutoSwitchToNextUnpickedPlayer(
            treasureSynchronizer,
            player.NetId,
            "treasure-skip-picked-player");
    }

    private static Rect2 ResolveIdAnchorRect(NMultiplayerPlayerState state)
    {
        Label? idLabel = TryFindIdLabel(state);
        if (idLabel != null)
        {
            Vector2 size = idLabel.Size;
            if (size.X <= 2f)
            {
                size = new Vector2(92f, SmallButtonSize.Y);
            }

            return new Rect2(idLabel.GlobalPosition, size);
        }

        Vector2 fallbackPosition = state.GlobalPosition + FallbackOffset;
        return new Rect2(fallbackPosition, new Vector2(92f, SmallButtonSize.Y));
    }

    private static Vector2 ResolveSwitchButtonPosition(NMultiplayerPlayerState state, Rect2 idAnchorRect)
    {
        NHealthBar? healthBar = AccessTools.Field(typeof(NMultiplayerPlayerState), "_healthBar")?.GetValue(state) as NHealthBar;
        if (healthBar?.HpBarContainer != null && GodotObject.IsInstanceValid(healthBar.HpBarContainer))
        {
            Control hpBarContainer = healthBar.HpBarContainer;
            float targetX = hpBarContainer.GlobalPosition.X + hpBarContainer.Size.X - (SmallButtonSize.X * 0.5f) + HpAnchorXOffset;
            return new Vector2(targetX, idAnchorRect.Position.Y);
        }

        return idAnchorRect.Position + DefaultAnchorOffset;
    }

    private static void RefreshCombatTrackerState(NMultiplayerPlayerState state)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        Control? energyContainer = AccessTools.Field(typeof(NMultiplayerPlayerState), "_energyContainer")?.GetValue(state) as Control;
        Control? starContainer = AccessTools.Field(typeof(NMultiplayerPlayerState), "_starContainer")?.GetValue(state) as Control;
        Control? cardContainer = AccessTools.Field(typeof(NMultiplayerPlayerState), "_cardContainer")?.GetValue(state) as Control;
        if (energyContainer == null || starContainer == null || cardContainer == null || state.Player.PlayerCombatState == null)
        {
            return;
        }

        bool shouldShowCombatInfo = CombatManager.Instance.IsInProgress;
        if (!shouldShowCombatInfo)
        {
            energyContainer.Visible = false;
            starContainer.Visible = false;
            cardContainer.Visible = false;
            return;
        }

        energyContainer.Visible = true;
        cardContainer.Visible = true;
        starContainer.Visible = state.Player.Character is Regent || state.Player.PlayerCombatState.Stars > 0;
        AccessTools.Method(typeof(NMultiplayerPlayerState), "RefreshCombatValues")?.Invoke(state, Array.Empty<object>());
        AccessTools.Method(typeof(NMultiplayerPlayerState), "UpdateSelectionReticleWidth")?.Invoke(state, Array.Empty<object>());
    }

    private static void HideOriginalIdLabel(NMultiplayerPlayerState state)
    {
        Label? idLabel = TryFindIdLabel(state);
        if (idLabel != null)
        {
            idLabel.Visible = false;
        }
    }

    private static void RestoreOriginalIdLabel(NMultiplayerPlayerState state)
    {
        Label? idLabel = TryFindIdLabel(state);
        if (idLabel != null)
        {
            idLabel.Visible = true;
        }
    }

    private static Label? TryFindIdLabel(NMultiplayerPlayerState state)
    {
        string playerIdText = state.Player.NetId.ToString();
        foreach (Node child in EnumerateDescendants(state))
        {
            if (child is not Label label)
            {
                continue;
            }

            string name = label.Name.ToString();
            string text = label.Text ?? string.Empty;
            bool nameLooksLikeId = name.Contains("id", StringComparison.OrdinalIgnoreCase);
            bool textContainsPlayerId = text.Contains(playerIdText, StringComparison.Ordinal);
            if (nameLooksLikeId || textContainsPlayerId)
            {
                return label;
            }
        }

        return null;
    }

    private static IEnumerable<Node> EnumerateDescendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (Node nested in EnumerateDescendants(child))
            {
                yield return nested;
            }
        }
    }
}

internal sealed partial class LocalPlayerStateSwitchTracker : Node
{
    private NMultiplayerPlayerState? _state;

    public void Initialize(NMultiplayerPlayerState state)
    {
        _state = state;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_state == null || !GodotObject.IsInstanceValid(_state))
        {
            QueueFree();
            return;
        }

        LocalMultiplayerPlayerStateSwitchUi.Refresh(_state);
    }
}
