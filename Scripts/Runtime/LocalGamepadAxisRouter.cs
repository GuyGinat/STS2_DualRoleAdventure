using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace LocalMultiControl.Scripts.Runtime;

internal static class LocalGamepadAxisRouter
{
    private const string PollerName = "LocalGamepadAxisPoller";

    private const float TriggerThreshold = 0.55f;
    private const float ReleaseThreshold = 0.30f;

    private const ulong RepeatStartDelayMs = 220;
    private const ulong RepeatIntervalMs = 90;

    private static int _horizontalDirection;
    private static int _verticalDirection;

    private static ulong _nextHorizontalRepeatAtMs;
    private static ulong _nextVerticalRepeatAtMs;
    private static bool _isLtHeld;
    private static bool _ltComboUsed;
    private static ulong _ltPressedAtMs;
    private static ulong _lastIdleLogAtMs;

    public static void EnsurePollerAttached()
    {
        NGame? game = NGame.Instance;
        if (game == null || !GodotObject.IsInstanceValid(game))
        {
            return;
        }

        if (game.GetNodeOrNull<LocalGamepadAxisPoller>(PollerName) != null)
        {
            return;
        }

        LocalGamepadAxisPoller poller = new()
        {
            Name = PollerName
        };
        game.AddChild(poller);
    }

    public static void Tick()
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            Reset();
            return;
        }

        if (!_isLtHeld)
        {
            ResetAxisRepeatState();
            return;
        }

        bool hasAxis = TryGetRightStickAxis(out int joypadId, out float axisX, out float axisY);
        ResolveStickDirectionFromActions(out int actionHorizontal, out int actionVertical);

        ulong now = Time.GetTicksMsec();
        bool noAxisInput = !hasAxis || (Mathf.Abs(axisX) < TriggerThreshold && Mathf.Abs(axisY) < TriggerThreshold);
        bool noActionInput = actionHorizontal == 0 && actionVertical == 0;
        if (noAxisInput && noActionInput)
        {
            if (now - _lastIdleLogAtMs >= 1200)
            {
                _lastIdleLogAtMs = now;
                int joypadCount = Input.GetConnectedJoypads().Count;
                LocalMultiControlLogger.Info(
                    $"[LT组合] LT按住中，未检测到右摇杆输入: joypads={joypadCount}, joypad={joypadId}, x={axisX:F2}, y={axisY:F2}, actionX={actionHorizontal}, actionY={actionVertical}");
            }
        }

        bool isInRun = RunManager.Instance.IsInProgress;
        bool preferVertical = Mathf.Abs(axisY) >= Mathf.Abs(axisX);
        if (actionVertical != 0)
        {
            preferVertical = true;
        }
        else if (actionHorizontal != 0)
        {
            preferVertical = false;
        }

        int verticalTarget = ResolveDirectionWithHysteresis(axisY, _verticalDirection);
        if (actionVertical != 0)
        {
            verticalTarget = actionVertical;
        }
        HandleVertical(verticalTarget, now, isInRun);

        int horizontalTarget = 0;
        if (!isInRun && !preferVertical)
        {
            horizontalTarget = ResolveDirectionWithHysteresis(axisX, _horizontalDirection);
            if (actionHorizontal != 0)
            {
                horizontalTarget = actionHorizontal;
            }
        }

        HandleHorizontal(horizontalTarget, now);
    }

    public static bool TryInterceptControllerInput(InputEvent inputEvent)
    {
        if (!LocalSelfCoopContext.IsEnabled)
        {
            return false;
        }

        if (inputEvent.IsActionPressed(Controller.faceButtonNorth) && TryHandleWakuuHotkey())
        {
            return true;
        }

        if (inputEvent.IsActionPressed(Controller.leftTrigger))
        {
            HandleLtPressed("ninputmanager");
            return true;
        }

        if (inputEvent.IsActionReleased(Controller.leftTrigger))
        {
            HandleLtReleased("ninputmanager");
            return true;
        }

        return false;
    }

    private static bool TryHandleWakuuHotkey()
    {
        if (RunManager.Instance.IsInProgress)
        {
            return false;
        }

        if (LocalSelfCoopContext.ActiveCharacterSelectScreen == null)
        {
            return false;
        }

        if (_isLtHeld)
        {
            return ToggleAllWakuuByHotkey();
        }

        return ToggleCurrentWakuuByHotkey();
    }

    private static bool ToggleCurrentWakuuByHotkey()
    {
        ulong playerId = LocalSelfCoopContext.CurrentLobbyEditingPlayerId;
        bool nextEnabled = !LocalSelfCoopContext.IsWakuuEnabled(playerId);
        bool changed = LocalSelfCoopContext.SetWakuuEnabled(playerId, nextEnabled, "gamepad:Y");
        if (!changed)
        {
            LocalMultiControlLogger.Info($"[LT组合] Y切换当前瓦库失败或无变化: player={playerId}, target={nextEnabled}");
            return false;
        }

        _ltComboUsed |= _isLtHeld;
        LocalMultiControlLogger.Info($"[LT组合] Y切换当前瓦库成功: player={playerId}, enabled={nextEnabled}");
        return true;
    }

    private static bool ToggleAllWakuuByHotkey()
    {
        bool allEnabled = LocalSelfCoopContext.LocalPlayerIds
            .Take(LocalSelfCoopContext.DesiredLocalPlayerCount)
            .All(LocalSelfCoopContext.IsWakuuEnabled);
        bool targetEnabled = !allEnabled;

        bool changed = LocalSelfCoopContext.SetAllWakuuEnabled(targetEnabled, "gamepad:LT+Y");
        if (!changed)
        {
            LocalMultiControlLogger.Info($"[LT组合] LT+Y切换全体瓦库失败或无变化: target={targetEnabled}");
            return false;
        }

        _ltComboUsed = true;
        LocalMultiControlLogger.Info($"[LT组合] LT+Y切换全体瓦库成功: enabled={targetEnabled}");
        return true;
    }

    public static bool ShouldBlockOriginalControllerInput(InputEvent inputEvent)
    {
        if (!LocalSelfCoopContext.IsEnabled || !_isLtHeld)
        {
            return false;
        }

        if (inputEvent is InputEventJoypadButton or InputEventJoypadMotion)
        {
            return true;
        }

        if (inputEvent is InputEventAction actionEvent &&
            Controller.AllControllerInputs.Contains(actionEvent.Action))
        {
            return true;
        }

        return false;
    }

    private static void HandleVertical(int targetDirection, ulong now, bool isInRun)
    {
        if (targetDirection == 0)
        {
            _verticalDirection = 0;
            return;
        }

        if (_verticalDirection != targetDirection)
        {
            _verticalDirection = targetDirection;
            TriggerVertical(targetDirection, isInRun);
            _nextVerticalRepeatAtMs = now + RepeatStartDelayMs;
            return;
        }

        if (now < _nextVerticalRepeatAtMs)
        {
            return;
        }

        TriggerVertical(targetDirection, isInRun);
        _nextVerticalRepeatAtMs = now + RepeatIntervalMs;
    }

    private static void HandleHorizontal(int targetDirection, ulong now)
    {
        if (targetDirection == 0)
        {
            _horizontalDirection = 0;
            return;
        }

        if (_horizontalDirection != targetDirection)
        {
            _horizontalDirection = targetDirection;
            TriggerHorizontal(targetDirection);
            _nextHorizontalRepeatAtMs = now + RepeatStartDelayMs;
            return;
        }

        if (now < _nextHorizontalRepeatAtMs)
        {
            return;
        }

        TriggerHorizontal(targetDirection);
        _nextHorizontalRepeatAtMs = now + RepeatIntervalMs;
    }

    private static void TriggerVertical(int direction, bool isInRun)
    {
        _ltComboUsed = true;

        if (isInRun)
        {
            if (direction < 0)
            {
                bool switched = LocalControlSwitchGuard.TrySwitchPrevious("gamepad:lt+right-stick-up");
                LocalMultiControlLogger.Info($"[LT组合] 摇杆上触发切人，结果={switched}");
                return;
            }

            bool switchedNext = LocalControlSwitchGuard.TrySwitchNext("gamepad:lt+right-stick-down");
            LocalMultiControlLogger.Info($"[LT组合] 摇杆下触发切人，结果={switchedNext}");
            return;
        }

        bool switchedLobby = LocalSelfCoopContext.SwitchLobbyEditingPlayer(next: direction > 0);
        LocalMultiControlLogger.Info($"[LT组合] 选角界面摇杆{(direction > 0 ? "下" : "上")}切编辑角色，结果={switchedLobby}");
    }

    private static void TriggerHorizontal(int direction)
    {
        _ltComboUsed = true;
        bool changed = LocalSelfCoopContext.AdjustDesiredLocalPlayerCount(
            direction > 0 ? 1 : -1,
            "gamepad:lt+right-stick-x");
        LocalMultiControlLogger.Info($"[LT组合] 选角界面摇杆{(direction > 0 ? "右" : "左")}调人数，结果={changed}");
    }

    private static int ResolveDirectionWithHysteresis(float axisValue, int currentDirection)
    {
        if (currentDirection == 0)
        {
            if (axisValue >= TriggerThreshold)
            {
                return 1;
            }

            if (axisValue <= -TriggerThreshold)
            {
                return -1;
            }

            return 0;
        }

        if (currentDirection > 0)
        {
            if (axisValue <= -TriggerThreshold)
            {
                return -1;
            }

            return axisValue <= ReleaseThreshold ? 0 : 1;
        }

        if (axisValue >= TriggerThreshold)
        {
            return 1;
        }

        return axisValue >= -ReleaseThreshold ? 0 : -1;
    }

    private static int? TryGetJoypadId()
    {
        Godot.Collections.Array<int> connectedJoypads = Input.GetConnectedJoypads();
        if (connectedJoypads.Count == 0)
        {
            return null;
        }

        return connectedJoypads[0];
    }

    private static bool TryGetRightStickAxis(out int joypadId, out float axisX, out float axisY)
    {
        joypadId = -1;
        axisX = 0f;
        axisY = 0f;

        Godot.Collections.Array<int> connectedJoypads = Input.GetConnectedJoypads();
        if (connectedJoypads.Count == 0)
        {
            return false;
        }

        float bestMagnitude = -1f;
        foreach (int id in connectedJoypads)
        {
            float x = Input.GetJoyAxis(id, JoyAxis.RightX);
            float y = Input.GetJoyAxis(id, JoyAxis.RightY);
            float magnitude = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
            if (magnitude <= bestMagnitude)
            {
                continue;
            }

            bestMagnitude = magnitude;
            joypadId = id;
            axisX = x;
            axisY = y;
        }

        return joypadId >= 0;
    }

    private static void ResolveStickDirectionFromActions(out int horizontal, out int vertical)
    {
        horizontal = 0;
        vertical = 0;

        // 兼容不同输入层映射：优先读取 joystick_*，回退到 dpad_*。
        bool right = Input.IsActionPressed(Controller.joystickRight) || Input.IsActionPressed(Controller.dPadEast);
        bool left = Input.IsActionPressed(Controller.joystickLeft) || Input.IsActionPressed(Controller.dPadWest);
        bool down = Input.IsActionPressed(Controller.joystickDown) || Input.IsActionPressed(Controller.dPadSouth);
        bool up = Input.IsActionPressed(Controller.joystickUp) || Input.IsActionPressed(Controller.dPadNorth);

        if (right && !left)
        {
            horizontal = 1;
        }
        else if (left && !right)
        {
            horizontal = -1;
        }

        if (down && !up)
        {
            vertical = 1;
        }
        else if (up && !down)
        {
            vertical = -1;
        }
    }

    private static void Reset()
    {
        _isLtHeld = false;
        _ltComboUsed = false;
        _ltPressedAtMs = 0;
        _lastIdleLogAtMs = 0;
        ResetAxisRepeatState();
    }

    private static void ResetAxisRepeatState()
    {
        _horizontalDirection = 0;
        _verticalDirection = 0;
        _nextHorizontalRepeatAtMs = 0;
        _nextVerticalRepeatAtMs = 0;
    }

    private static void HandleLtPressed(string source)
    {
        if (_isLtHeld)
        {
            return;
        }

        _isLtHeld = true;
        _ltComboUsed = false;
        _ltPressedAtMs = Time.GetTicksMsec();
        _lastIdleLogAtMs = 0;
        ResetAxisRepeatState();
        LocalMultiControlLogger.Info($"[LT组合] 检测到 LT 按下，开始拦截原逻辑: source={source}");
    }

    private static void HandleLtReleased(string source)
    {
        if (!_isLtHeld)
        {
            return;
        }

        ulong holdMs = Time.GetTicksMsec() - _ltPressedAtMs;
        bool comboUsed = _ltComboUsed;
        _isLtHeld = false;
        _ltComboUsed = false;
        _ltPressedAtMs = 0;
        ResetAxisRepeatState();

        if (!comboUsed)
        {
            LocalMultiControlLogger.Info($"[LT组合] LT 释放且未使用右摇杆，回放原 LT 逻辑: holdMs={holdMs}, source={source}");
            ReplayOriginalLtAction();
            return;
        }

        LocalMultiControlLogger.Info($"[LT组合] LT+右摇杆组合已消费，本次不触发原 LT 逻辑: holdMs={holdMs}, source={source}");
    }

    private static void ReplayOriginalLtAction()
    {
        InputEventAction pressed = new()
        {
            Action = MegaInput.viewDrawPile,
            Pressed = true
        };
        Input.ParseInputEvent(pressed);

        InputEventAction released = new()
        {
            Action = MegaInput.viewDrawPile,
            Pressed = false
        };
        Input.ParseInputEvent(released);
    }
}

internal sealed partial class LocalGamepadAxisPoller : Node
{
    public override void _Ready()
    {
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        LocalGamepadAxisRouter.Tick();
    }
}
