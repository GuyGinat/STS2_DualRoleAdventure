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

        // Ctrl+Arrows repositioning moved to LocalGhostHandsOverlay.PollMoveKeys —
        // arrow-key events can be consumed by other nodes before reaching NGame._Input
        // (Ctrl+Right never arrived), so the overlay polls raw key state instead.
    }
}
