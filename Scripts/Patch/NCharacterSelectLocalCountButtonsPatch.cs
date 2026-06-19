using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuOpened))]
internal static class NCharacterSelectLocalCountButtonsOpenPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        LocalCharacterSelectCountButtons.Sync(__instance);
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen._Process))]
internal static class NCharacterSelectLocalCountButtonsProcessPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        LocalCharacterSelectCountButtons.Sync(__instance);
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuClosed))]
internal static class NCharacterSelectLocalCountButtonsClosePatch
{
    [HarmonyPrefix]
    private static void Prefix(NCharacterSelectScreen __instance)
    {
        LocalCharacterSelectCountButtons.Remove(__instance);
    }
}

internal static class LocalCharacterSelectCountButtons
{
    private const string PanelName = "LocalSelfCoopCountPanel";
    private const string MinusButtonName = "LocalSelfCoopMinusButton";
    private const string PlusButtonName = "LocalSelfCoopPlusButton";
    private const string PrevButtonName = "LocalSelfCoopPrevButton";
    private const string NextButtonName = "LocalSelfCoopNextButton";
    private const string LtHintIconName = "LocalSelfCoopLtHintIcon";
    private const string MinusHintIconName = "LocalSelfCoopMinusHintIcon";
    private const string PlusHintIconName = "LocalSelfCoopPlusHintIcon";
    private const string PrevHintIconName = "LocalSelfCoopPrevHintIcon";
    private const string NextHintIconName = "LocalSelfCoopNextHintIcon";
    private const string PlusSignName = "LocalSelfCoopHintPlusSign";
    private static readonly Vector2 ButtonSize = new Vector2(140f, 32f);
    private static readonly Vector2 HintIconSize = new Vector2(24f, 24f);
    private const float VerticalGapRatio = 0.5f;
    private const float HorizontalGapByIcon = 44f;

    public static void Sync(NCharacterSelectScreen screen)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            Remove(screen);
            return;
        }

        Control panel = EnsurePanel(screen);
        UpdateLayout(screen, panel);
    }

    public static void Remove(NCharacterSelectScreen screen)
    {
        Control? existingPanel = screen.GetNodeOrNull<Control>(PanelName);
        existingPanel?.QueueFreeSafely();
    }

    private static Control EnsurePanel(NCharacterSelectScreen screen)
    {
        Control? existingPanel = screen.GetNodeOrNull<Control>(PanelName);
        if (existingPanel != null)
        {
            return existingPanel;
        }

        Control panel = new Control
        {
            Name = PanelName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 80
        };

        LocalSimpleTextButton minusButton = CreateCountButton(MinusButtonName, "-", false);
        minusButton.Connect(NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>((_) => OnAdjustPlayerCount(-1)));
        panel.AddChild(minusButton);

        LocalSimpleTextButton plusButton = CreateCountButton(PlusButtonName, "+", true);
        plusButton.Connect(NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>((_) => OnAdjustPlayerCount(1)));
        panel.AddChild(plusButton);

        LocalSimpleTextButton prevButton = CreateCountButton(PrevButtonName, string.Empty, false);
        prevButton.Connect(NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>((_) => OnSwitchLobbyPlayer(false)));
        panel.AddChild(prevButton);

        LocalSimpleTextButton nextButton = CreateCountButton(NextButtonName, string.Empty, true);
        nextButton.Connect(NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>((_) => OnSwitchLobbyPlayer(true)));
        panel.AddChild(nextButton);

        EnsureHintIcon(panel, LtHintIconName);
        EnsureHintIcon(panel, MinusHintIconName);
        EnsureHintIcon(panel, PlusHintIconName);
        EnsureHintIcon(panel, PrevHintIconName);
        EnsureHintIcon(panel, NextHintIconName);
        EnsurePlusSign(panel);

        screen.AddChildSafely(panel);
        LocalMultiControlLogger.Info("角色选择页已创建本地人数 +/- 实体按钮。");
        return panel;
    }

    private static LocalSimpleTextButton CreateCountButton(string name, string text, bool mirrorImageX)
    {
        LocalSimpleTextButton button = new LocalSimpleTextButton
        {
            Name = name,
            ButtonText = text,
            FocusMode = Control.FocusModeEnum.None,
            FontSize = 20,
            Size = ButtonSize,
            CustomMinimumSize = ButtonSize,
            ImageScale = Vector2.One * 1.5f,
            MirrorImageX = mirrorImageX
        };
        return button;
    }

    private static void UpdateLayout(NCharacterSelectScreen screen, Control panel)
    {
        NConfirmButton? embarkButton =
            AccessTools.Field(typeof(NCharacterSelectScreen), "_embarkButton")?.GetValue(screen) as NConfirmButton;
        if (embarkButton == null)
        {
            return;
        }

        // 注意：该坐标经过实机对齐，目的是避免与确认按钮重叠导致 + 按钮不可点击。
        // 请不要随意改回靠右布局，如需改动先实测“+ 按钮在 2->3/4 人时可稳定点击”。
        Viewport? viewport = screen.GetViewport();
        if (viewport == null)
        {
            return;
        }

        float horizontalGap = HorizontalGapByIcon;
        float verticalGap = ButtonSize.Y * VerticalGapRatio;
        float secondColumnX = ButtonSize.X + horizontalGap;
        float secondRowY = ButtonSize.Y + verticalGap;
        float panelWidth = secondColumnX + ButtonSize.X;
        float panelHeight = secondRowY + ButtonSize.Y;
        Vector2 viewportSize = viewport.GetVisibleRect().Size;

        panel.Position = new Vector2(viewportSize.X - panelWidth - 18f, viewportSize.Y - panelHeight - 18f);

        if (panel.GetNodeOrNull<LocalSimpleTextButton>(MinusButtonName) is { } minusButton)
        {
            minusButton.Position = Vector2.Zero;
        }

        if (panel.GetNodeOrNull<LocalSimpleTextButton>(PlusButtonName) is { } plusButton)
        {
            plusButton.Position = new Vector2(secondColumnX, 0f);
        }

        if (panel.GetNodeOrNull<LocalSimpleTextButton>(PrevButtonName) is { } prevButton)
        {
            prevButton.Position = new Vector2(0f, secondRowY);
        }

        if (panel.GetNodeOrNull<LocalSimpleTextButton>(NextButtonName) is { } nextButton)
        {
            nextButton.Position = new Vector2(secondColumnX, secondRowY);
        }

        RefreshHintIcons(panel, secondColumnX, secondRowY);
    }

    private static void OnAdjustPlayerCount(int delta)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        string source = delta > 0 ? "ui-button:+" : "ui-button:-";
        if (!LocalSelfCoopContext.AdjustDesiredLocalPlayerCount(delta, source))
        {
            return;
        }

        int targetCount = LocalSelfCoopContext.DesiredLocalPlayerCount;
        NGame.Instance?.AddChildSafely(NFullscreenTextVfx.Create(LocalModText.LocalPlayerCount(targetCount)));
        LocalMultiControlLogger.Info($"通过实体按钮调整本地人数成功: {targetCount}");

        NCharacterSelectScreen? activeScreen = LocalSelfCoopContext.ActiveCharacterSelectScreen;
        if (activeScreen != null && GodotObject.IsInstanceValid(activeScreen))
        {
            Sync(activeScreen);
        }
    }

    private static void OnSwitchLobbyPlayer(bool next)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        LocalSelfCoopContext.SwitchLobbyEditingPlayer(next);
    }

    private static void EnsureHintIcon(Control panel, string nodeName)
    {
        if (panel.GetNodeOrNull<TextureRect>(nodeName) != null)
        {
            return;
        }

        TextureRect icon = new()
        {
            Name = nodeName,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddChild(icon);
    }

    private static void EnsurePlusSign(Control panel)
    {
        if (panel.GetNodeOrNull<Label>(PlusSignName) != null)
        {
            return;
        }

        Label plusSign = new()
        {
            Name = PlusSignName,
            Text = "+",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        plusSign.AddThemeFontSizeOverride("font_size", 16);
        plusSign.AddThemeColorOverride("font_color", new Color("f3efe6"));
        plusSign.AddThemeColorOverride("font_outline_color", new Color("111111"));
        plusSign.AddThemeConstantOverride("outline_size", 3);
        panel.AddChild(plusSign);
    }

    private static void RefreshHintIcons(Control panel, float secondColumnX, float secondRowY)
    {
        bool shouldShowHints = NControllerManager.Instance?.IsUsingController ?? false;
        Texture2D? lt = NControllerManager.Instance?.GetHotkeyIcon(Controller.leftTrigger);
        Texture2D? left = NControllerManager.Instance?.GetHotkeyIcon(Controller.dPadWest);
        Texture2D? right = NControllerManager.Instance?.GetHotkeyIcon(Controller.dPadEast);
        Texture2D? up = NControllerManager.Instance?.GetHotkeyIcon(Controller.dPadNorth);
        Texture2D? down = NControllerManager.Instance?.GetHotkeyIcon(Controller.dPadSouth);

        PlaceHint(panel, LtHintIconName, lt, new Vector2(secondColumnX * 0.5f - 10f, secondRowY - 44f));
        PlaceHint(panel, MinusHintIconName, left, new Vector2(8f, 4f));
        PlaceHint(panel, PlusHintIconName, right, new Vector2(secondColumnX + 8f, 4f));
        PlaceHint(panel, PrevHintIconName, up, new Vector2(8f, secondRowY + 4f));
        PlaceHint(panel, NextHintIconName, down, new Vector2(secondColumnX + 8f, secondRowY + 4f));

        if (panel.GetNodeOrNull<Label>(PlusSignName) is { } plusSign)
        {
            plusSign.Position = new Vector2(secondColumnX * 0.5f + 18f, secondRowY - 40f);
            plusSign.Visible = shouldShowHints && lt != null;
        }

        SetHintVisible(panel, LtHintIconName, shouldShowHints);
        SetHintVisible(panel, MinusHintIconName, shouldShowHints);
        SetHintVisible(panel, PlusHintIconName, shouldShowHints);
        SetHintVisible(panel, PrevHintIconName, shouldShowHints);
        SetHintVisible(panel, NextHintIconName, shouldShowHints);
    }

    private static void PlaceHint(Control panel, string nodeName, Texture2D? texture, Vector2 position)
    {
        if (panel.GetNodeOrNull<TextureRect>(nodeName) is not { } icon)
        {
            return;
        }

        icon.Texture = texture;
        icon.Size = HintIconSize;
        icon.CustomMinimumSize = HintIconSize;
        icon.Position = position;
        icon.Visible = texture != null;
    }

    private static void SetHintVisible(Control panel, string nodeName, bool visible)
    {
        if (panel.GetNodeOrNull<TextureRect>(nodeName) is { } icon)
        {
            icon.Visible = icon.Texture != null && visible;
        }
    }
}
