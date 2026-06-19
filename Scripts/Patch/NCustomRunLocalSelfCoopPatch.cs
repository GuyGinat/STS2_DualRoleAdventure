using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NMultiplayerHostSubmenu), nameof(NMultiplayerHostSubmenu.StartHost))]
internal static class NMultiplayerHostSubmenuCustomRunPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NMultiplayerHostSubmenu __instance, GameMode gameMode)
    {
        if (gameMode != GameMode.Custom)
        {
            return true;
        }

        NSubmenuStack? stack = AccessTools.Field(typeof(NSubmenu), "_stack").GetValue(__instance) as NSubmenuStack;
        if (stack == null)
        {
            return true;
        }

        LocalMultiControlLogger.Info("联机自定义模式改走本地多角色回环开局。");
        LocalSelfCoopSaveTag.ClearCurrentProfile();
        SaveManager.Instance.DeleteCurrentMultiplayerRun();

        ulong primaryPlayerId = LocalSelfCoopContext.ResolvePrimaryPlayerId();
        LocalSelfCoopContext.UseSavedWakuuPlayerIds(Array.Empty<ulong>());
        LocalSelfCoopSaveTag.MarkCurrentProfile(
            LocalSelfCoopContext.LocalPlayerIds.Take(LocalSelfCoopContext.DesiredLocalPlayerCount).ToList());

        LocalLoopbackHostGameService netService = new(primaryPlayerId);
        LocalSelfCoopContext.Enable(netService);

        NCustomRunScreen customRunScreen = stack.GetSubmenuType<NCustomRunScreen>();
        customRunScreen.InitializeMultiplayerAsHost(netService, LocalSelfCoopContext.LocalPlayerIds.Count);
        stack.Push(customRunScreen);
        NGame.Instance?.AddChildSafely(NFullscreenTextVfx.Create(LocalModText.EnteredLocalSelfCoopHint));
        return false;
    }
}

[HarmonyPatch(typeof(NCustomRunScreen), nameof(NCustomRunScreen.OnSubmenuOpened))]
internal static class NCustomRunScreenLocalPlayersPatch
{
    private const int MaxLocalAscensionLevel = 10;
    private static bool _isReconciling;

    [HarmonyPostfix]
    private static void Postfix(NCustomRunScreen __instance)
    {
        TryReconcileLocalPlayers(__instance);
    }

    internal static void TryReconcileLocalPlayers(NCustomRunScreen screen)
    {
        if (_isReconciling)
        {
            return;
        }

        if (!LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        StartRunLobby lobby = screen.Lobby;
        if (lobby.NetService is not LocalLoopbackHostGameService loopbackService)
        {
            return;
        }

        EnsureLobbyMaxCapacity(lobby);
        List<ulong> targetPlayerIds = LocalSelfCoopContext.LocalPlayerIds
            .Take(LocalSelfCoopContext.DesiredLocalPlayerCount)
            .ToList();
        if (targetPlayerIds.Count <= 1)
        {
            return;
        }

        bool needsReconcile = targetPlayerIds.Any((id) => lobby.Players.All((player) => player.id != id))
                              || lobby.Players.Any((player) =>
                                  LocalSelfCoopContext.LocalPlayerIds.Contains(player.id) && !targetPlayerIds.Contains(player.id));
        if (!needsReconcile)
        {
            return;
        }

        _isReconciling = true;
        try
        {
            UnlockState unlockState = SaveManager.Instance.GenerateUnlockStateFromProgress();
            SerializableUnlockState serializableUnlockState = unlockState.ToSerializable();

            int added = 0;
            foreach (ulong playerId in targetPlayerIds)
            {
                bool exists = lobby.Players.Any((player) => player.id == playerId);
                if (exists)
                {
                    continue;
                }

                loopbackService.SetCurrentSenderId(playerId);
                _ = lobby.AddLocalHostPlayerInternal(serializableUnlockState, MaxLocalAscensionLevel);
                added++;
            }

            List<ulong> removablePlayerIds = LocalSelfCoopContext.LocalPlayerIds
                .Skip(targetPlayerIds.Count)
                .ToList();
            int removed = 0;
            foreach (ulong removableId in removablePlayerIds)
            {
                int playerIndex = lobby.Players.FindIndex((player) => player.id == removableId);
                if (playerIndex < 0)
                {
                    continue;
                }

                LobbyPlayer removedPlayer = lobby.Players[playerIndex];
                lobby.Players.RemoveAt(playerIndex);
                lobby.InputSynchronizer.OnPlayerDisconnected(removedPlayer.id);
                screen.RemotePlayerDisconnected(removedPlayer);
                removed++;
            }

            bool readyChanged = false;
            for (int i = 0; i < lobby.Players.Count; i++)
            {
                LobbyPlayer player = lobby.Players[i];
                if (player.id == LocalSelfCoopContext.PrimaryPlayerId || player.isReady)
                {
                    continue;
                }

                player.isReady = true;
                lobby.Players[i] = player;
                screen.PlayerChanged(player, false);
                readyChanged = true;
            }

            LocalSelfCoopContext.EnsureLobbySenderContext("custom-run-opened");
            LocalMultiControlLogger.Info(
                $"自定义模式大厅本地人数已同步: target={targetPlayerIds.Count}, actual={lobby.Players.Count}, added={added}, removed={removed}, readyChanged={readyChanged}");
        }
        finally
        {
            _isReconciling = false;
        }
    }

    private static void EnsureLobbyMaxCapacity(StartRunLobby lobby)
    {
        const int maxLocalPlayerCount = 12;
        if (lobby.MaxPlayers >= maxLocalPlayerCount)
        {
            return;
        }

        AccessTools.Field(typeof(StartRunLobby), "<MaxPlayers>k__BackingField")?.SetValue(lobby, maxLocalPlayerCount);
    }
}

[HarmonyPatch(typeof(NCustomRunScreen), nameof(NCustomRunScreen._Process))]
internal static class NCustomRunScreenLocalPlayersProcessPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCustomRunScreen __instance)
    {
        NCustomRunScreenLocalPlayersPatch.TryReconcileLocalPlayers(__instance);
    }
}

[HarmonyPatch(typeof(NCustomRunScreen), "OnEmbarkPressed")]
internal static class NCustomRunEmbarkGuardPatch
{
    [HarmonyPrefix]
    private static void Prefix(NCustomRunScreen __instance)
    {
        if (!LocalSelfCoopContext.IsEnabled || __instance.Lobby.NetService is not LocalLoopbackHostGameService loopbackService)
        {
            return;
        }

        NCustomRunScreenLocalPlayersPatch.TryReconcileLocalPlayers(__instance);
        loopbackService.SetCurrentSenderId(LocalSelfCoopContext.PrimaryPlayerId);
        LocalContext.NetId = LocalSelfCoopContext.PrimaryPlayerId;
        LocalMultiControlLogger.Info($"自定义模式开始前强制校正 sender 到主角色: {LocalSelfCoopContext.PrimaryPlayerId}");
    }
}

[HarmonyPatch(typeof(NCustomRunScreen), nameof(NCustomRunScreen.OnSubmenuOpened))]
internal static class NCustomRunLocalCountButtonsOpenPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCustomRunScreen __instance)
    {
        LocalCustomRunCountButtons.Sync(__instance);
    }
}

[HarmonyPatch(typeof(NCustomRunScreen), nameof(NCustomRunScreen._Process))]
internal static class NCustomRunLocalCountButtonsProcessPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCustomRunScreen __instance)
    {
        LocalCustomRunCountButtons.Sync(__instance);
    }
}

[HarmonyPatch(typeof(NCustomRunScreen), nameof(NCustomRunScreen.OnSubmenuClosed))]
internal static class NCustomRunLocalCountButtonsClosePatch
{
    [HarmonyPrefix]
    private static void Prefix(NCustomRunScreen __instance)
    {
        LocalCustomRunCountButtons.Remove(__instance);
    }
}

internal static class LocalCustomRunCountButtons
{
    private const string PanelName = "LocalCustomRunCountPanel";
    private const string MinusButtonName = "LocalCustomRunMinusButton";
    private const string PlusButtonName = "LocalCustomRunPlusButton";
    private const string PrevButtonName = "LocalCustomRunPrevButton";
    private const string NextButtonName = "LocalCustomRunNextButton";
    private static readonly Vector2 ButtonSize = new(140f, 32f);
    private const float VerticalGapRatio = 0.5f;
    private const float HorizontalGap = 44f;

    public static void Sync(NCustomRunScreen screen)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            Remove(screen);
            return;
        }

        Control panel = EnsurePanel(screen);
        UpdateLayout(screen, panel);
    }

    public static void Remove(NCustomRunScreen screen)
    {
        Control? existingPanel = screen.GetNodeOrNull<Control>(PanelName);
        existingPanel?.QueueFreeSafely();
    }

    private static Control EnsurePanel(NCustomRunScreen screen)
    {
        Control? existingPanel = screen.GetNodeOrNull<Control>(PanelName);
        if (existingPanel != null)
        {
            return existingPanel;
        }

        Control panel = new()
        {
            Name = PanelName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 80
        };

        LocalSimpleTextButton minusButton = CreateButton(MinusButtonName, "-", false);
        minusButton.Connect(NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>((_) => OnAdjustPlayerCount(-1)));
        panel.AddChild(minusButton);

        LocalSimpleTextButton plusButton = CreateButton(PlusButtonName, "+", true);
        plusButton.Connect(NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>((_) => OnAdjustPlayerCount(1)));
        panel.AddChild(plusButton);

        LocalSimpleTextButton prevButton = CreateButton(PrevButtonName, string.Empty, false);
        prevButton.Connect(NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>((_) => LocalSelfCoopContext.SwitchLobbyEditingPlayer(false)));
        panel.AddChild(prevButton);

        LocalSimpleTextButton nextButton = CreateButton(NextButtonName, string.Empty, true);
        nextButton.Connect(NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>((_) => LocalSelfCoopContext.SwitchLobbyEditingPlayer(true)));
        panel.AddChild(nextButton);

        screen.AddChildSafely(panel);
        return panel;
    }

    private static LocalSimpleTextButton CreateButton(string name, string text, bool mirrorX)
    {
        return new LocalSimpleTextButton
        {
            Name = name,
            ButtonText = text,
            FocusMode = Control.FocusModeEnum.None,
            FontSize = 20,
            Size = ButtonSize,
            CustomMinimumSize = ButtonSize,
            ImageScale = Vector2.One * 1.5f,
            MirrorImageX = mirrorX
        };
    }

    private static void UpdateLayout(NCustomRunScreen screen, Control panel)
    {
        Viewport? viewport = screen.GetViewport();
        if (viewport == null)
        {
            return;
        }

        float verticalGap = ButtonSize.Y * VerticalGapRatio;
        float secondColumnX = ButtonSize.X + HorizontalGap;
        float secondRowY = ButtonSize.Y + verticalGap;
        float panelWidth = secondColumnX + ButtonSize.X;
        float panelHeight = secondRowY + ButtonSize.Y;
        Vector2 viewportSize = viewport.GetVisibleRect().Size;

        panel.Position = new Vector2(viewportSize.X - panelWidth - 18f, viewportSize.Y - panelHeight - 18f);

        panel.GetNodeOrNull<LocalSimpleTextButton>(MinusButtonName)!.Position = Vector2.Zero;
        panel.GetNodeOrNull<LocalSimpleTextButton>(PlusButtonName)!.Position = new Vector2(secondColumnX, 0f);
        panel.GetNodeOrNull<LocalSimpleTextButton>(PrevButtonName)!.Position = new Vector2(0f, secondRowY);
        panel.GetNodeOrNull<LocalSimpleTextButton>(NextButtonName)!.Position = new Vector2(secondColumnX, secondRowY);
    }

    private static void OnAdjustPlayerCount(int delta)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        string source = delta > 0 ? "custom-ui-button:+" : "custom-ui-button:-";
        if (!LocalSelfCoopContext.AdjustDesiredLocalPlayerCount(delta, source))
        {
            return;
        }

        int targetCount = LocalSelfCoopContext.DesiredLocalPlayerCount;
        NGame.Instance?.AddChildSafely(NFullscreenTextVfx.Create(LocalModText.LocalPlayerCount(targetCount)));
        LocalMultiControlLogger.Info($"通过自定义模式实体按钮调整本地人数成功: {targetCount}");
    }
}

[HarmonyPatch(typeof(NCustomRunScreen), nameof(NCustomRunScreen.OnSubmenuOpened))]
internal static class NCustomRunSelectionSyncOpenPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCustomRunScreen __instance)
    {
        LocalCustomRunSelectionSync.TrySync(__instance);
    }
}

[HarmonyPatch(typeof(NCustomRunScreen), nameof(NCustomRunScreen.PlayerChanged))]
internal static class NCustomRunSelectionSyncPlayerChangedPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCustomRunScreen __instance)
    {
        LocalCustomRunSelectionSync.TrySync(__instance);
    }
}

[HarmonyPatch(typeof(NCustomRunScreen), nameof(NCustomRunScreen._Process))]
internal static class NCustomRunSelectionSyncProcessPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCustomRunScreen __instance)
    {
        LocalCustomRunSelectionSync.TrySync(__instance);
    }
}

internal static class LocalCustomRunSelectionSync
{
    private static bool _isSyncing;

    public static void TrySync(NCustomRunScreen screen)
    {
        if (!LocalSelfCoopContext.IsEnabled || _isSyncing)
        {
            return;
        }

        StartRunLobby lobby = screen.Lobby;
        if (lobby.NetService is not LocalLoopbackHostGameService)
        {
            return;
        }

        _isSyncing = true;
        try
        {
            Control? charButtonContainer = AccessTools.Field(typeof(NCustomRunScreen), "_charButtonContainer")
                ?.GetValue(screen) as Control;
            if (charButtonContainer == null)
            {
                return;
            }

            ulong editingPlayerId = LocalContext.NetId ?? LocalSelfCoopContext.PrimaryPlayerId;
            if (lobby.Players.All((player) => player.id != editingPlayerId))
            {
                editingPlayerId = LocalSelfCoopContext.PrimaryPlayerId;
            }

            LobbyPlayer editingPlayer = lobby.Players.FirstOrDefault((player) => player.id == editingPlayerId);

            List<NCharacterSelectButton> buttons = charButtonContainer.GetChildren().OfType<NCharacterSelectButton>().ToList();
            foreach (NCharacterSelectButton button in buttons)
            {
                foreach (LobbyPlayer player in lobby.Players)
                {
                    button.OnRemotePlayerDeselected(player.id);
                }
            }

            NCharacterSelectButton? selectedButton = null;
            foreach (NCharacterSelectButton button in buttons)
            {
                bool isSelected = button.Character == editingPlayer.character;
                AccessTools.Field(typeof(NCharacterSelectButton), "_isSelected")?.SetValue(button, isSelected);
                if (isSelected)
                {
                    selectedButton = button;
                }
            }

            foreach (LobbyPlayer player in lobby.Players)
            {
                if (player.id == editingPlayer.id)
                {
                    continue;
                }

                NCharacterSelectButton? targetButton = buttons.FirstOrDefault((button) => button.Character == player.character);
                targetButton?.OnRemotePlayerSelected(player.id);
            }

            foreach (NCharacterSelectButton button in buttons)
            {
                AccessTools.Method(typeof(NCharacterSelectButton), "RefreshState")?.Invoke(button, Array.Empty<object>());
            }

            AccessTools.Field(typeof(NCustomRunScreen), "_selectedButton")?.SetValue(screen, selectedButton);
        }
        finally
        {
            _isSyncing = false;
        }
    }
}
