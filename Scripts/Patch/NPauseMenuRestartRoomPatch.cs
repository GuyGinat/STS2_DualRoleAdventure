using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NPauseMenu), nameof(NPauseMenu._Ready))]
internal static class NPauseMenuRestartRoomPatch
{
    private const string RestartRoomButtonName = "LocalRestartRoomButton";

    private static bool _isRestarting;

    [HarmonyPostfix]
    private static void Postfix(NPauseMenu __instance)
    {
        try
        {
            if (!ShouldInjectRestartButton())
            {
                return;
            }

            Control? buttonContainer = AccessTools.Field(typeof(NPauseMenu), "_buttonContainer")?.GetValue(__instance) as Control;
            NPauseMenuButton? saveAndQuitButton = AccessTools.Field(typeof(NPauseMenu), "_saveAndQuitButton")?.GetValue(__instance) as NPauseMenuButton;
            if (buttonContainer == null || saveAndQuitButton == null)
            {
                return;
            }

            if (buttonContainer.GetNodeOrNull<NPauseMenuButton>(RestartRoomButtonName) != null)
            {
                return;
            }

            Node.DuplicateFlags duplicateFlags = Node.DuplicateFlags.Groups |
                                                 Node.DuplicateFlags.Scripts |
                                                 Node.DuplicateFlags.UseInstantiation;
            NPauseMenuButton? restartRoomButton = saveAndQuitButton.Duplicate((int)duplicateFlags) as NPauseMenuButton;
            if (restartRoomButton == null)
            {
                LocalMultiControlLogger.Warn("ESC 菜单重启按钮复制失败。");
                return;
            }

            restartRoomButton.Name = RestartRoomButtonName;
            restartRoomButton.Connect(
                NClickableControl.SignalName.Released,
                Callable.From<NButton>((_) => OnRestartRoomPressed(__instance)));

            MegaLabel? label = restartRoomButton.GetNodeOrNull<MegaLabel>("Label");
            label?.SetTextAutoSize(LocalModText.RestartRoomButton);

            buttonContainer.AddChild(restartRoomButton);
            buttonContainer.MoveChild(restartRoomButton, saveAndQuitButton.GetIndex());
            RefreshFocusNeighbors(buttonContainer);
            LocalMultiControlLogger.Info("已注入 ESC 重启房间按钮。");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Error($"注入 ESC 重启房间按钮失败: {exception}");
        }
    }

    private static bool ShouldInjectRestartButton()
    {
        if (!LocalSelfCoopContext.IsEnabled || !RunManager.Instance.IsInProgress)
        {
            return false;
        }

        if (RunManager.Instance.NetService.Type == NetGameType.Client)
        {
            return false;
        }

        return true;
    }

    private static void RefreshFocusNeighbors(Control buttonContainer)
    {
        List<NPauseMenuButton> buttons = buttonContainer
            .GetChildren()
            .OfType<NPauseMenuButton>()
            .Where((button) => button.Visible)
            .ToList();
        for (int index = 0; index < buttons.Count; index++)
        {
            NPauseMenuButton button = buttons[index];
            button.FocusNeighborLeft = button.GetPath();
            button.FocusNeighborRight = button.GetPath();
            button.FocusNeighborTop = (index > 0 ? buttons[index - 1] : button).GetPath();
            button.FocusNeighborBottom = (index < buttons.Count - 1 ? buttons[index + 1] : button).GetPath();
        }
    }

    private static void OnRestartRoomPressed(NPauseMenu pauseMenu)
    {
        if (_isRestarting)
        {
            return;
        }

        TaskHelper.RunSafely(RestartRoomAsync(pauseMenu));
    }

    private static async Task RestartRoomAsync(NPauseMenu pauseMenu)
    {
        _isRestarting = true;
        try
        {
            DisablePauseMenuButtons(pauseMenu);
            LocalSelfCoopSaveTag.MarkCurrentProfile(
                LocalSelfCoopContext.LocalPlayerIds.Take(LocalSelfCoopContext.DesiredLocalPlayerCount).ToList(),
                LocalSelfCoopContext.GetWakuuPlayerIdsSnapshot());
            LocalSelfCoopContext.Disable("pause-restart-room");

            LocalMultiControlLogger.Info("收到 ESC 重启房间请求，准备回到主菜单并快速读档。");
            NGame? game = NGame.Instance;
            if (game == null)
            {
                LocalMultiControlLogger.Warn("快速重启失败：NGame.Instance 为空。");
                _isRestarting = false;
                return;
            }

            await game.ReturnToMainMenu();
            bool loaded = await LocalQuickRestartLoader.TryLoadDirectlyAsync();
            if (!loaded)
            {
                game.AddChildSafely(NFullscreenTextVfx.Create(LocalModText.RestartRoomFailed));
            }
        }
        catch (Exception exception)
        {
            _isRestarting = false;
            LocalMultiControlLogger.Error($"ESC 重启房间失败: {exception}");
        }
        finally
        {
            _isRestarting = false;
        }
    }

    private static void DisablePauseMenuButtons(NPauseMenu pauseMenu)
    {
        Control? buttonContainer = AccessTools.Field(typeof(NPauseMenu), "_buttonContainer")?.GetValue(pauseMenu) as Control;
        if (buttonContainer != null)
        {
            foreach (NPauseMenuButton button in buttonContainer.GetChildren().OfType<NPauseMenuButton>())
            {
                button.Disable();
            }
        }

        NBackButton? backButton = AccessTools.Field(typeof(NPauseMenu), "_backButton")?.GetValue(pauseMenu) as NBackButton;
        backButton?.Disable();
    }
}
