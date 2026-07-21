using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom._Ready))]
internal static class NCombatRoomGhostHandsPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCombatRoom __instance)
    {
        LocalGhostHandsRuntime.OnCombatRoomReady(__instance);
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame._Input))]
internal static class GhostHandsHotkeysPatch
{
    [HarmonyPostfix]
    private static void Postfix(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey keyEvent || !LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        Key keycode = keyEvent.Keycode;
        Key physicalKeycode = keyEvent.PhysicalKeycode;

        if ((keycode == Key.F8 || physicalKeycode == Key.F8) && keyEvent.IsReleased())
        {
            LocalGhostHandsRuntime.Toggle();
            return;
        }

        if (!LocalGhostHandsRuntime.Enabled || !keyEvent.CtrlPressed)
        {
            return;
        }

        Vector2 direction = Vector2.Zero;
        if (keycode == Key.Left || physicalKeycode == Key.Left)
        {
            direction = Vector2.Left;
        }
        else if (keycode == Key.Right || physicalKeycode == Key.Right)
        {
            direction = Vector2.Right;
        }
        else if (keycode == Key.Up || physicalKeycode == Key.Up)
        {
            direction = Vector2.Up;
        }
        else if (keycode == Key.Down || physicalKeycode == Key.Down)
        {
            direction = Vector2.Down;
        }

        if (direction == Vector2.Zero)
        {
            return;
        }

        if (keyEvent.Pressed)
        {
            // Echo events included: holding the arrow keeps nudging.
            float step = keyEvent.ShiftPressed ? 4f : 20f;
            LocalGhostHandsRuntime.Nudge(direction.X * step, direction.Y * step);
        }
        else if (keyEvent.IsReleased())
        {
            LocalGhostHandsRuntime.CommitOffsets();
        }
    }
}
