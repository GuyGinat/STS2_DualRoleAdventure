using System;
using System.Collections.Generic;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NHandImageCollection), "UpdateHandVisibility")]
internal static class NHandImageCollectionUpdateVisibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NHandImageCollection __instance)
    {
        if (!LocalSelfCoopContext.IsEnabled || !LocalSelfCoopContext.UseSingleAdventureMode)
        {
            return true;
        }

        PeerInputSynchronizer? synchronizer =
            AccessTools.Field(typeof(NHandImageCollection), "_synchronizer")?.GetValue(__instance) as PeerInputSynchronizer;
        List<NHandImage>? hands =
            AccessTools.Field(typeof(NHandImageCollection), "_hands")?.GetValue(__instance) as List<NHandImage>;
        if (synchronizer == null || hands == null)
        {
            return false;
        }

        bool hasLocalScreen = TryGetScreenType(synchronizer, LocalContext.NetId ?? 0UL, out NetScreenType localScreenType);
        foreach (NHandImage hand in hands)
        {
            NetScreenType handScreenType = default;
            bool hasHandScreen = TryGetScreenType(synchronizer, hand.Player.NetId, out handScreenType);
            if (!hasHandScreen)
            {
                // 正式版已移除 IsSinglePlayerOrFakeMultiplayer，这里直接回退到本地当前屏幕状态，
                // 避免宝箱 UI 初始化阶段因缺失远端输入状态而中断整套界面创建。
                if (!hasLocalScreen)
                {
                    hand.Visible = false;
                    continue;
                }

                handScreenType = localScreenType;
            }

            bool shouldShow = hasLocalScreen &&
                handScreenType == NetScreenType.SharedRelicPicking &&
                localScreenType == NetScreenType.SharedRelicPicking;
            if (!hand.Visible && shouldShow)
            {
                hand.AnimateIn();
            }

            hand.Visible = shouldShow;
        }

        bool cursorShown = !hasLocalScreen || localScreenType != NetScreenType.SharedRelicPicking;
        NGame.Instance?.CursorManager.SetCursorShown(cursorShown);
        return false;
    }

    private static bool TryGetScreenType(PeerInputSynchronizer synchronizer, ulong playerId, out NetScreenType screenType)
    {
        try
        {
            screenType = synchronizer.GetScreenType(playerId);
            return true;
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("PeerInputState for non-existent player"))
        {
            LocalMultiControlLogger.Warn($"宝箱手势层跳过缺失输入状态玩家: player={playerId}");
            screenType = default;
            return false;
        }
    }
}
