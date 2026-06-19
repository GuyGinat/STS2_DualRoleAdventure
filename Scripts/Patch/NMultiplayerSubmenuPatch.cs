using System;
using System.Collections.Generic;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NMultiplayerSubmenu), "StartLoad")]
internal static class NMultiplayerSubmenuPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NMultiplayerSubmenu __instance)
    {
        if (!LocalSelfCoopSaveTag.TryReadCurrentProfile(out List<ulong> playerIds) || playerIds.Count < 2)
        {
            return true;
        }

        ulong primaryPlayerId = playerIds[0];
        LocalMultiControlLogger.Info($"检测到本地多控存档标记，尝试继续游戏: {string.Join(",", playerIds)}");
        LocalSelfCoopContext.UseSavedPlayerIds(playerIds);

        ReadSaveResult<SerializableRun> readSaveResult = SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(primaryPlayerId);
        if (!readSaveResult.Success || readSaveResult.SaveData == null)
        {
            NSubmenuButton? loadButton = AccessTools.Field(typeof(NMultiplayerSubmenu), "_loadButton")?.GetValue(__instance) as NSubmenuButton;
            loadButton?.Disable();
            NErrorPopup? popup = NErrorPopup.Create(
                new LocString("main_menu_ui", "INVALID_SAVE_POPUP.title"),
                new LocString("main_menu_ui", "INVALID_SAVE_POPUP.description_run"),
                new LocString("main_menu_ui", "INVALID_SAVE_POPUP.dismiss"),
                showReportBugButton: true);

            if (popup != null && NModalContainer.Instance != null)
            {
                NModalContainer.Instance.Add(popup);
                NModalContainer.Instance.ShowBackstop();
            }

            LocalMultiControlLogger.Warn("本地多控存档读取失败，已弹出坏档提示。");
            return false;
        }

        if (!LocalSelfCoopContext.IsSaveOwnedByLocalSelfCoop(readSaveResult.SaveData))
        {
            LocalMultiControlLogger.Warn("检测到存档玩家ID与本地多控标记不一致，回退原生多人读档流程。");
            LocalSelfCoopSaveTag.ClearCurrentProfile();
            return true;
        }

        NSubmenuStack? stack = AccessTools.Field(typeof(NSubmenu), "_stack")?.GetValue(__instance) as NSubmenuStack;
        if (stack == null)
        {
            LocalMultiControlLogger.Warn("未找到子菜单栈，回退原生多人读档流程。");
            return true;
        }

        LocalLoopbackHostGameService netService = new LocalLoopbackHostGameService(primaryPlayerId);
        LocalSelfCoopContext.Enable(netService);
        NMultiplayerLoadGameScreen loadGameScreen = stack.GetSubmenuType<NMultiplayerLoadGameScreen>();
        loadGameScreen.InitializeAsHost(netService, readSaveResult.SaveData);
        stack.Push(loadGameScreen);
        LocalMultiControlLogger.Info("已使用本地回环服务打开多人读档界面。");
        return false;
    }
}
