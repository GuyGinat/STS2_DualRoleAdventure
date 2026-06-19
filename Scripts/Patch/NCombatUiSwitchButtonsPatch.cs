using Godot;
using HarmonyLib;
using LocalMultiControl.Scripts.Runtime;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Patch;

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi._Ready))]
internal static class NCombatUiReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCombatUi __instance)
    {
        LocalCombatSwitchButtons.Ensure(__instance);
    }
}

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Activate))]
internal static class NCombatUiActivatePatch
{
    [HarmonyPostfix]
    private static void Postfix(NCombatUi __instance, CombatState state)
    {
        LocalCombatSwitchButtons.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Enable))]
internal static class NCombatUiEnablePatch
{
    [HarmonyPostfix]
    private static void Postfix(NCombatUi __instance)
    {
        LocalCombatSwitchButtons.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Disable))]
internal static class NCombatUiDisablePatch
{
    [HarmonyPostfix]
    private static void Postfix(NCombatUi __instance)
    {
        LocalCombatSwitchButtons.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Deactivate))]
internal static class NCombatUiDeactivatePatch
{
    [HarmonyPostfix]
    private static void Postfix(NCombatUi __instance)
    {
        LocalCombatSwitchButtons.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi._ExitTree))]
internal static class NCombatUiExitPatch
{
    [HarmonyPrefix]
    private static void Prefix(NCombatUi __instance)
    {
        LocalCombatSwitchButtons.Remove(__instance);
    }
}

internal static class LocalCombatSwitchButtons
{
    private const string ContainerName = "LocalCombatSwitchContainer";
    private const string PrevButtonName = "LocalCombatSwitchPrevButton";
    private const string NextButtonName = "LocalCombatSwitchNextButton";
    private const string TrackerName = "LocalCombatSwitchTracker";
    private static readonly Vector2 PingShowPosRatio = new Vector2(1536f, 932f) / NGame.devResolution;
    private static readonly Vector2 EndTurnAnchorOffset = new Vector2(-28f, -42f);

    public static void Ensure(NCombatUi combatUi)
    {
        if (combatUi.GetNodeOrNull<Control>(ContainerName) != null)
        {
            return;
        }

        Control container = new Control
        {
            Name = ContainerName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TopLevel = true,
            ZIndex = 120
        };

        LocalSimpleTextButton prevButton = new LocalSimpleTextButton
        {
            Name = PrevButtonName,
            ButtonText = string.Empty,
            FocusMode = Control.FocusModeEnum.None,
            FontSize = 20,
            Size = new Vector2(140f, 32f),
            CustomMinimumSize = new Vector2(140f, 32f),
            ImageScale = Vector2.One * 1.5f
        };
        prevButton.Connect(
            MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl.SignalName.Released,
            Callable.From<MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl>((_) =>
                LocalControlSwitchGuard.TrySwitchPrevious("combat-ui-up")));
        container.AddChild(prevButton);

        LocalSimpleTextButton nextButton = new LocalSimpleTextButton
        {
            Name = NextButtonName,
            ButtonText = string.Empty,
            FocusMode = Control.FocusModeEnum.None,
            FontSize = 20,
            Size = new Vector2(140f, 32f),
            CustomMinimumSize = new Vector2(140f, 32f),
            Position = new Vector2(136f, 0f),
            ImageScale = Vector2.One * 1.5f,
            MirrorImageX = true
        };
        nextButton.Connect(
            MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl.SignalName.Released,
            Callable.From<MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl>((_) =>
                LocalControlSwitchGuard.TrySwitchNext("combat-ui-down")));
        container.AddChild(nextButton);

        combatUi.AddChildSafely(container);
        EnsureTracker(combatUi);
        LocalMultiControlLogger.Info("战斗界面已创建 Ping 右侧上下切人按钮。");
    }

    private static void EnsureTracker(NCombatUi combatUi)
    {
        if (combatUi.GetNodeOrNull<LocalCombatSwitchTracker>(TrackerName) != null)
        {
            return;
        }

        // 注意：不要再用 NCombatUi._Process 打补丁。
        // 该方法在目标版本不存在，曾导致 Harmony 初始化失败，进而引发“无法开始/读档异常”。
        // 这里改为挂一个本地跟随节点逐帧刷新，兼容性更稳。
        LocalCombatSwitchTracker tracker = new LocalCombatSwitchTracker
        {
            Name = TrackerName
        };
        tracker.Initialize(combatUi);
        combatUi.AddChild(tracker);
    }

    public static void Refresh(NCombatUi combatUi)
    {
        Control? container = combatUi.GetNodeOrNull<Control>(ContainerName);
        if (container == null)
        {
            return;
        }

        int runPlayerCount = RunManager.Instance.DebugOnlyGetState()?.Players.Count ?? 0;
        bool hasMultiplePlayers = LocalMultiControlRuntime.SessionState.OrderedPlayerIds.Count > 1 || runPlayerCount > 1;
        bool shouldShow = LocalSelfCoopContext.IsEnabled
                          && RunManager.Instance.IsInProgress
                          && CombatManager.Instance.IsInProgress
                          && hasMultiplePlayers;

        container.Visible = shouldShow;
        if (!shouldShow)
        {
            return;
        }

        Viewport? viewport = combatUi.GetViewport();
        if (viewport == null)
        {
            return;
        }

        NEndTurnButton? endTurnButton = FindEndTurnButton(combatUi);
        if (endTurnButton != null)
        {
            container.GlobalPosition = endTurnButton.GlobalPosition + EndTurnAnchorOffset;
            return;
        }

        Vector2 fallbackAnchor = PingShowPosRatio * viewport.GetVisibleRect().Size;
        container.GlobalPosition = fallbackAnchor;
    }

    public static void Remove(NCombatUi combatUi)
    {
        Control? container = combatUi.GetNodeOrNull<Control>(ContainerName);
        container?.QueueFreeSafely();
    }

    private static NEndTurnButton? FindEndTurnButton(Node root)
    {
        if (root is NEndTurnButton endTurnButton)
        {
            return endTurnButton;
        }

        foreach (Node child in root.GetChildren())
        {
            NEndTurnButton? found = FindEndTurnButton(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}

internal sealed partial class LocalCombatSwitchTracker : Node
{
    private NCombatUi? _combatUi;

    public void Initialize(NCombatUi combatUi)
    {
        _combatUi = combatUi;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_combatUi == null || !GodotObject.IsInstanceValid(_combatUi))
        {
            QueueFree();
            return;
        }

        LocalCombatSwitchButtons.Refresh(_combatUi);
        LocalMultiControlRuntime.TryAutoEndTurnForRelicControlledPlayer();
    }
}
