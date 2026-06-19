using System;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Select))]
internal static class NCharacterSelectButtonSelectPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NCharacterSelectButton __instance)
    {
        LocalSelfCoopContext.EnsureLobbySenderContext("character-button-select-prefix");
        return !TryReselectWhenAlreadySelected(__instance);
    }

    private static bool TryReselectWhenAlreadySelected(NCharacterSelectButton button)
    {
        if (!LocalSelfCoopContext.IsEnabled || button.IsLocked)
        {
            return false;
        }

        try
        {
            bool isSelected = AccessTools.Field(typeof(NCharacterSelectButton), "_isSelected")?.GetValue(button) as bool? ?? false;
            if (!isSelected)
            {
                return false;
            }

            ICharacterSelectButtonDelegate? selectDelegate =
                AccessTools.Field(typeof(NCharacterSelectButton), "_delegate")?.GetValue(button) as ICharacterSelectButtonDelegate;
            if (selectDelegate == null)
            {
                return false;
            }

            LocalSelfCoopContext.EnsureLobbySenderContext("character-button-reselect");
            selectDelegate.SelectCharacter(button, button.Character);
            AccessTools.Method(typeof(NCharacterSelectButton), "RefreshState")?.Invoke(button, Array.Empty<object>());
            LocalMultiControlLogger.Info($"允许重复选角重新提交流程: character={button.Character.Id.Entry}");
            return true;
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"重复选角补丁执行失败，回退原逻辑: {exception.Message}");
            return false;
        }
    }
}

[HarmonyPatch(typeof(NCharacterSelectButton), "OnPress")]
internal static class NCharacterSelectButtonOnPressPatch
{
    [HarmonyPrefix]
    private static void Prefix(NCharacterSelectButton __instance, ref bool __state)
    {
        LocalSelfCoopContext.EnsureLobbySenderContext("character-button-on-press");
        __state = AccessTools.Field(typeof(NCharacterSelectButton), "_isSelected")?.GetValue(__instance) as bool? ?? false;
    }

    [HarmonyPostfix]
    private static void Postfix(NCharacterSelectButton __instance, bool __state)
    {
        if (!LocalSelfCoopContext.IsEnabled || __instance.IsLocked)
        {
            return;
        }

        try
        {
            if (!__state)
            {
                return;
            }

            ICharacterSelectButtonDelegate? selectDelegate =
                AccessTools.Field(typeof(NCharacterSelectButton), "_delegate")?.GetValue(__instance) as ICharacterSelectButtonDelegate;
            if (selectDelegate == null)
            {
                return;
            }

            // 处理“已聚焦按钮重复点击不触发Select”的情况。
            LocalSelfCoopContext.EnsureLobbySenderContext("character-button-on-press-reselect");
            selectDelegate.SelectCharacter(__instance, __instance.Character);
            AccessTools.Method(typeof(NCharacterSelectButton), "RefreshState")?.Invoke(__instance, Array.Empty<object>());
            LocalMultiControlLogger.Info($"允许重复点击已选角色: character={__instance.Character.Id.Entry}");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"重复点击选角补丁执行失败: {exception.Message}");
        }
    }
}
