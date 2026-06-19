using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Saves;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NMultiplayerHostSubmenu), nameof(NMultiplayerHostSubmenu._Ready))]
internal static class NMultiplayerHostSubmenuPatch
{
    private const string LocalSelfCoopButtonName = "LocalSelfCoopButton";
    private const float CardGap = 26f;

    [HarmonyPostfix]
    private static void Postfix(NMultiplayerHostSubmenu __instance)
    {
        try
        {
            if (__instance.GetNodeOrNull<NSubmenuButton>(LocalSelfCoopButtonName) != null)
            {
                LocalMultiControlLogger.Info("联机菜单入口已存在，跳过重复注入。");
                return;
            }

            NSubmenuButton? standardButton = __instance.GetNodeOrNull<NSubmenuButton>("StandardButton");
            NSubmenuButton? dailyButton = __instance.GetNodeOrNull<NSubmenuButton>("DailyButton");
            NSubmenuButton? customButton = __instance.GetNodeOrNull<NSubmenuButton>("CustomRunButton");
            if (standardButton == null)
            {
                LocalMultiControlLogger.Warn("未找到 StandardButton，无法注入本地多角色入口。");
                return;
            }

            NSubmenuButton templateButton = customButton ?? standardButton;
            NSubmenuButton button = CreateStyledButton(templateButton);
            button.Name = LocalSelfCoopButtonName;
            EnsureVisualResourcesUnique(button);
            ApplyButtonText(button);
            button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>((_) => OnLocalSelfCoopPressed(__instance)));

            Control container = templateButton.GetParent<Control>();
            container.AddChild(button);
            int targetIndex = Math.Min(templateButton.GetIndex() + 1, container.GetChildCount() - 1);
            container.MoveChild(button, targetIndex);

            ArrangeFourButtonsHorizontally(standardButton, dailyButton, customButton, button);
            LocalMultiControlLogger.Info("联机菜单已注入卡片样式入口：单人多角色（四卡并列）。");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Error($"注入“单人多角色”入口失败: {exception}");
        }
    }

    private static NSubmenuButton CreateStyledButton(NSubmenuButton templateButton)
    {
        const Node.DuplicateFlags duplicateFlags = Node.DuplicateFlags.Groups |
                                                   Node.DuplicateFlags.Scripts |
                                                   Node.DuplicateFlags.UseInstantiation;
        return templateButton.Duplicate((int)duplicateFlags) as NSubmenuButton
            ?? throw new InvalidOperationException("复制模板按钮失败。");
    }

    private static void EnsureVisualResourcesUnique(NSubmenuButton button)
    {
        Node? bgPanelNode = button.FindChild("BgPanel", recursive: true, owned: false);
        if (bgPanelNode is CanvasItem bgPanel && bgPanel.Material is ShaderMaterial material)
        {
            bgPanel.Material = material.Duplicate() as ShaderMaterial;
        }
    }

    private static void ApplyButtonText(NSubmenuButton button)
    {
        Node? titleNode = button.FindChild("Title", recursive: true, owned: false);
        if (titleNode is Label title)
        {
            title.Text = LocalModText.LocalSelfCoopCardTitle;
        }

        Node? descriptionNode = button.FindChild("Description", recursive: true, owned: false);
        if (descriptionNode is RichTextLabel description)
        {
            description.Text = LocalModText.LocalSelfCoopCardDescription;
        }
    }

    private static void ArrangeFourButtonsHorizontally(
        NSubmenuButton standardButton,
        NSubmenuButton? dailyButton,
        NSubmenuButton? customButton,
        NSubmenuButton localButton)
    {
        if (dailyButton == null || customButton == null)
        {
            return;
        }

        List<NSubmenuButton> originalButtons = new() { standardButton, dailyButton, customButton };
        originalButtons = originalButtons.OrderBy((item) => item.Position.X).ToList();

        float y = originalButtons[0].Position.Y;
        float cardWidth = originalButtons[0].Size.X;
        if (cardWidth <= 1f)
        {
            cardWidth = localButton.Size.X;
        }

        if (cardWidth <= 1f)
        {
            return;
        }

        float centerX = originalButtons.Average((item) => item.Position.X + item.Size.X * 0.5f);
        float totalWidth = cardWidth * 4f + CardGap * 3f;
        float startX = centerX - totalWidth * 0.5f;
        List<NSubmenuButton> arranged = new() { localButton, originalButtons[0], originalButtons[1], originalButtons[2] };
        for (int index = 0; index < arranged.Count; index++)
        {
            NSubmenuButton button = arranged[index];
            float x = startX + index * (cardWidth + CardGap);
            button.Position = new Vector2(x, y);
        }
    }

    private static void OnLocalSelfCoopPressed(NMultiplayerHostSubmenu submenu)
    {
        LocalMultiControlLogger.Info("进入单人多角色流程。");
        LocalSelfCoopSaveTag.ClearCurrentProfile();
        SaveManager.Instance.DeleteCurrentMultiplayerRun();
        LocalMultiControlLogger.Info("已清理历史多人存档，避免旧格式校验干扰。");

        NSubmenuStack? stack = GetStack(submenu);
        if (stack == null)
        {
            LocalMultiControlLogger.Warn("无法打开角色选择：未找到 NSubmenuStack。");
            return;
        }

        ulong primaryPlayerId = LocalSelfCoopContext.ResolvePrimaryPlayerId();
        LocalSelfCoopContext.UseSavedWakuuPlayerIds(Array.Empty<ulong>());
        LocalSelfCoopSaveTag.MarkCurrentProfile(LocalSelfCoopContext.LocalPlayerIds.Take(LocalSelfCoopContext.DesiredLocalPlayerCount).ToList());
        LocalLoopbackHostGameService netService = new LocalLoopbackHostGameService(primaryPlayerId);
        LocalSelfCoopContext.Enable(netService);

        NCharacterSelectScreen characterSelectScreen = stack.GetSubmenuType<NCharacterSelectScreen>();
        LocalSelfCoopContext.ActiveCharacterSelectScreen = characterSelectScreen;
        // 以最大容量初始化大厅，实际活跃人数由 LocalSelfCoopContext 按目标人数裁剪到2~12。
        characterSelectScreen.InitializeMultiplayerAsHost(netService, LocalSelfCoopContext.LocalPlayerIds.Count);
        if (!LocalSelfCoopContext.BootstrapLocalPlayers(characterSelectScreen))
        {
            LocalMultiControlLogger.Warn("初始化本地多角色队伍失败，已回退到默认流程。");
        }

        stack.Push(characterSelectScreen);
        NGame.Instance?.AddChildSafely(NFullscreenTextVfx.Create(LocalModText.EnteredLocalSelfCoopHint));
        LocalMultiControlLogger.Info("已跳转到本地多角色队伍角色选择界面。");
    }

    private static NSubmenuStack? GetStack(NSubmenu submenu)
    {
        return AccessTools.Field(typeof(NSubmenu), "_stack").GetValue(submenu) as NSubmenuStack;
    }
}
