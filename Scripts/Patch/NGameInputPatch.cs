using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NGame), nameof(NGame._Input))]
internal static class NGameInputPatch
{
    [HarmonyPostfix]
    private static void Postfix(InputEvent inputEvent)
    {
        LocalGamepadAxisRouter.EnsurePollerAttached();

        if (inputEvent is not InputEventKey keyEvent || !keyEvent.IsReleased())
        {
            return;
        }

        Key keycode = keyEvent.Keycode;
        Key physicalKeycode = keyEvent.PhysicalKeycode;

        bool isPrevious = keycode == Key.Bracketleft ||
                          physicalKeycode == Key.Bracketleft ||
                          keycode == Key.T ||
                          physicalKeycode == Key.T;

        bool isNext = keycode == Key.Bracketright ||
                      physicalKeycode == Key.Bracketright ||
                      keycode == Key.R ||
                      physicalKeycode == Key.R ||
                      keycode == Key.Slash ||
                      physicalKeycode == Key.Slash;

        bool isDecreasePlayerCount = keycode == Key.Minus || physicalKeycode == Key.Minus;
        bool isIncreasePlayerCount = keycode == Key.Equal ||
                                     physicalKeycode == Key.Equal ||
                                     keycode == Key.Plus ||
                                     physicalKeycode == Key.Plus;

        if (!RunManager.Instance.IsInProgress &&
            LocalSelfCoopContext.IsEnabled &&
            (isDecreasePlayerCount || isIncreasePlayerCount))
        {
            int delta = isIncreasePlayerCount ? 1 : -1;
            string hotkeyLabel = isIncreasePlayerCount ? "+ / =" : "-";
            if (LocalSelfCoopContext.AdjustDesiredLocalPlayerCount(delta, $"hotkey:{hotkeyLabel}"))
            {
                int targetCount = LocalSelfCoopContext.DesiredLocalPlayerCount;
                LocalMultiControlLogger.Info($"检测到人数热键 {hotkeyLabel}，本地人数已调整为 {targetCount}");
                NGame.Instance?.AddChildSafely(NFullscreenTextVfx.Create(LocalModText.LocalPlayerCount(targetCount)));
            }

            return;
        }

        if (!isPrevious && !isNext)
        {
            return;
        }

        if (isPrevious)
        {
            LocalMultiControlLogger.Info("检测到切换热键: [ / T (反切)");
            if (RunManager.Instance.IsInProgress)
            {
                LocalMultiControlRuntime.SwitchPreviousControlledPlayer("hotkey:[/T");
            }
            else
            {
                LocalSelfCoopContext.SwitchLobbyEditingPlayer(next: false);
            }

            return;
        }

        LocalMultiControlLogger.Info("检测到切换热键: ] / R (正切)");
        if (RunManager.Instance.IsInProgress)
        {
            LocalMultiControlRuntime.SwitchNextControlledPlayer("hotkey:]/R");
        }
        else
        {
            LocalSelfCoopContext.SwitchLobbyEditingPlayer(next: true);
        }
    }
}
