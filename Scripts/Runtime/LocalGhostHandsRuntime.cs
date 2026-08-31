using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;
using GodotFileAccess = Godot.FileAccess;

namespace LocalMultiControl.Scripts.Runtime;

/// <summary>
/// Optional combat overlay that shows the hands of backgrounded characters behind and
/// above the active character's hand. Toggled with F8; position adjustable at runtime
/// with Ctrl+Arrows (Ctrl+Shift+Arrows for fine steps). Settings persist to user://.
/// </summary>
internal static class LocalGhostHandsRuntime
{
    private const string ConfigPath = "user://dual_role_adventure_settings.json";
    private const string OverlayNodeName = "LocalGhostHandsOverlay";
    private const ulong NudgeSaveThrottleMs = 800;

    public static bool Enabled { get; private set; }
    public static float OffsetX { get; private set; }
    public static float OffsetY { get; private set; } = -560f;
    public static float GhostScale { get; private set; } = 0.5f;

    private static bool _configLoaded;
    private static ulong _lastNudgeSaveMs;

    public static void OnCombatRoomReady(NCombatRoom room)
    {
        LoadConfigIfNeeded();
        if (!Enabled || !LocalSelfCoopContext.IsEnabled)
        {
            return;
        }

        TryAttachOverlay(room);
    }

    public static void Toggle()
    {
        LoadConfigIfNeeded();
        Enabled = !Enabled;
        SaveConfig();
        LocalMultiControlLogger.Info($"Ghost hands toggled: enabled={Enabled}, offset=({OffsetX:F0}, {OffsetY:F0}), scale={GhostScale:F2}");

        if (Enabled)
        {
            TryAttachOverlay(NCombatRoom.Instance);
        }
        else
        {
            FindOverlay()?.DisableAndFree();
        }

        NGame.Instance?.AddChildSafely(NFullscreenTextVfx.Create(Enabled
            ? LocalModText.GhostHandsOn
            : LocalModText.GhostHandsOff));
    }

    public static void Nudge(float deltaX, float deltaY)
    {
        LoadConfigIfNeeded();
        OffsetX += deltaX;
        OffsetY += deltaY;
        ClampOffsetsToScreen();

        ulong nowMs = Time.GetTicksMsec();
        if (nowMs - _lastNudgeSaveMs > NudgeSaveThrottleMs)
        {
            _lastNudgeSaveMs = nowMs;
            SaveConfig();
            LocalMultiControlLogger.Info($"Ghost hands offset: ({OffsetX:F0}, {OffsetY:F0})");
        }
    }

    /// <summary>
    /// Keeps the overlay reachable on screen. LayoutRow places rows at
    /// (viewport.X * 0.5 + OffsetX, viewport.Y + OffsetY), so these bounds keep the
    /// row anchor visible. Also applied to loaded config, so a position saved
    /// off-screen (Workshop report, 2026-08-26) heals itself on next launch.
    /// </summary>
    public static void ClampOffsetsToScreen()
    {
        Vector2 viewport = NGame.Instance?.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920f, 1080f);
        OffsetX = Mathf.Clamp(OffsetX, -viewport.X * 0.5f + 100f, viewport.X * 0.5f - 100f);
        OffsetY = Mathf.Clamp(OffsetY, -viewport.Y + 60f, 0f);
    }

    public static void CommitOffsets()
    {
        SaveConfig();
        LocalMultiControlLogger.Info($"Ghost hands offset saved: ({OffsetX:F0}, {OffsetY:F0})");
    }

    private static void TryAttachOverlay(NCombatRoom? room)
    {
        try
        {
            NPlayerHand? hand = room?.Ui?.Hand;
            if (hand == null || FindOverlay() != null)
            {
                return;
            }

            Node parent = hand.GetParent();
            LocalGhostHandsOverlay overlay = new() { Name = OverlayNodeName };
            parent.AddChild(overlay);
            parent.MoveChild(overlay, hand.GetIndex());
            LocalMultiControlLogger.Info("Ghost hands overlay attached behind the player hand.");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"Failed to attach ghost hands overlay: {exception.Message}");
        }
    }

    private static LocalGhostHandsOverlay? FindOverlay()
    {
        NPlayerHand? hand = NCombatRoom.Instance?.Ui?.Hand;
        return hand?.GetParent()?.GetNodeOrNull<LocalGhostHandsOverlay>(OverlayNodeName);
    }

    private static void LoadConfigIfNeeded()
    {
        if (_configLoaded)
        {
            return;
        }

        _configLoaded = true;
        try
        {
            using GodotFileAccess? file = GodotFileAccess.Open(ConfigPath, GodotFileAccess.ModeFlags.Read);
            if (file == null)
            {
                return;
            }

            Variant parsed = Json.ParseString(file.GetAsText());
            if (parsed.VariantType != Variant.Type.Dictionary)
            {
                return;
            }

            Godot.Collections.Dictionary settings = parsed.AsGodotDictionary();
            if (settings.TryGetValue("ghostHandsEnabled", out Variant enabled))
            {
                Enabled = enabled.AsBool();
            }

            if (settings.TryGetValue("ghostHandsOffsetX", out Variant offsetX))
            {
                OffsetX = (float)offsetX.AsDouble();
            }

            if (settings.TryGetValue("ghostHandsOffsetY", out Variant offsetY))
            {
                OffsetY = (float)offsetY.AsDouble();
            }

            if (settings.TryGetValue("ghostHandsScale", out Variant scale))
            {
                GhostScale = Mathf.Clamp((float)scale.AsDouble(), 0.2f, 1.5f);
            }

            ClampOffsetsToScreen();
            LocalMultiControlLogger.Info($"Ghost hands config loaded: enabled={Enabled}, offset=({OffsetX:F0}, {OffsetY:F0}), scale={GhostScale:F2}");
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"Failed to load ghost hands config: {exception.Message}");
        }
    }

    private static void SaveConfig()
    {
        try
        {
            // Merge over the existing file so keys owned by other features
            // (e.g. extraCrossCharacterCardReward) survive a ghost-hands save.
            Godot.Collections.Dictionary settings = new();
            if (GodotFileAccess.FileExists(ConfigPath))
            {
                using GodotFileAccess? existingFile = GodotFileAccess.Open(ConfigPath, GodotFileAccess.ModeFlags.Read);
                if (existingFile != null)
                {
                    Variant parsed = Json.ParseString(existingFile.GetAsText());
                    if (parsed.VariantType == Variant.Type.Dictionary)
                    {
                        settings = parsed.AsGodotDictionary();
                    }
                }
            }

            settings["ghostHandsEnabled"] = Enabled;
            settings["ghostHandsOffsetX"] = OffsetX;
            settings["ghostHandsOffsetY"] = OffsetY;
            settings["ghostHandsScale"] = GhostScale;

            using GodotFileAccess? file = GodotFileAccess.Open(ConfigPath, GodotFileAccess.ModeFlags.Write);
            file?.StoreString(Json.Stringify(settings, "  "));
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"Failed to save ghost hands config: {exception.Message}");
        }
    }
}

/// <summary>
/// Full-rect passive overlay living just behind NPlayerHand. Polls backgrounded
/// players' hand piles and mirrors them with pooled, non-interactive NCard nodes.
/// All card nodes are returned to the game's NodePool on every rebuild and on exit.
/// </summary>
internal sealed partial class LocalGhostHandsOverlay : Control
{
    private const double RefreshIntervalSec = 0.25;
    private const float CardSpacingFactor = 0.72f;
    private const float RowGapPixels = 26f;
    private const float RowAlpha = 0.85f;

    private sealed class GhostRow
    {
        public ulong PlayerId;
        public List<CardModel> Cards = new();
        public readonly List<NCard> CardNodes = new();
        public Control Container = null!;
        public Label SlotLabel = null!;
    }

    private const float MoveSpeedPixelsPerSec = 600f;
    private const float FineMoveSpeedPixelsPerSec = 120f;

    private readonly List<GhostRow> _rows = new();
    private double _sinceRefresh = RefreshIntervalSec;
    private bool _wasNudging;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta)
    {
        PollMoveKeys(delta);

        _sinceRefresh += delta;
        if (_sinceRefresh < RefreshIntervalSec)
        {
            return;
        }

        _sinceRefresh = 0;
        try
        {
            Refresh();
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"Ghost hands refresh failed: {exception.Message}");
        }
    }

    public override void _ExitTree()
    {
        ClearRows();
    }

    /// <summary>
    /// Ctrl+Arrows repositioning, polled per frame instead of handled in NGame._Input:
    /// other nodes consume some arrow-key events before they reach NGame (Ctrl+Right
    /// never arrived — Workshop report, 2026-08-26), and polling sees the raw key state.
    /// </summary>
    private void PollMoveKeys(double delta)
    {
        if (!LocalGhostHandsRuntime.Enabled || !Input.IsKeyPressed(Key.Ctrl))
        {
            FinishNudgeIfNeeded();
            return;
        }

        Vector2 direction = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.Left))
        {
            direction += Vector2.Left;
        }

        if (Input.IsPhysicalKeyPressed(Key.Right))
        {
            direction += Vector2.Right;
        }

        if (Input.IsPhysicalKeyPressed(Key.Up))
        {
            direction += Vector2.Up;
        }

        if (Input.IsPhysicalKeyPressed(Key.Down))
        {
            direction += Vector2.Down;
        }

        if (direction == Vector2.Zero)
        {
            FinishNudgeIfNeeded();
            return;
        }

        _wasNudging = true;
        float speed = Input.IsKeyPressed(Key.Shift) ? FineMoveSpeedPixelsPerSec : MoveSpeedPixelsPerSec;
        LocalGhostHandsRuntime.Nudge(direction.X * speed * (float)delta, direction.Y * speed * (float)delta);
    }

    private void FinishNudgeIfNeeded()
    {
        if (_wasNudging)
        {
            _wasNudging = false;
            LocalGhostHandsRuntime.CommitOffsets();
        }
    }

    public void DisableAndFree()
    {
        ClearRows();
        QueueFree();
    }

    private void Refresh()
    {
        if (!LocalGhostHandsRuntime.Enabled || !LocalSelfCoopContext.IsEnabled)
        {
            ClearRows();
            return;
        }

        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        ulong? localNetId = LocalContext.NetId;
        if (runState == null || !localNetId.HasValue)
        {
            ClearRows();
            return;
        }

        List<Player> others = runState.Players
            .Where((player) => player.NetId != localNetId.Value)
            .ToList();
        if (others.Count == 0)
        {
            ClearRows();
            return;
        }

        bool structureChanged = others.Count != _rows.Count;
        if (!structureChanged)
        {
            for (int index = 0; index < others.Count; index++)
            {
                if (_rows[index].PlayerId != others[index].NetId)
                {
                    structureChanged = true;
                    break;
                }
            }
        }

        if (structureChanged)
        {
            ClearRows();
            foreach (Player player in others)
            {
                _rows.Add(CreateRow(player, runState));
            }
        }

        for (int index = 0; index < others.Count; index++)
        {
            GhostRow row = _rows[index];
            List<CardModel> handCards = ReadHand(others[index]);
            if (!handCards.SequenceEqual(row.Cards))
            {
                RebuildRowCards(row, handCards);
            }

            LayoutRow(row, index);
        }
    }

    private static List<CardModel> ReadHand(Player player)
    {
        try
        {
            return PileType.Hand.GetPile(player).Cards.ToList();
        }
        catch (Exception)
        {
            return new List<CardModel>();
        }
    }

    private GhostRow CreateRow(Player player, RunState runState)
    {
        Control container = new()
        {
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            Modulate = new Color(1f, 1f, 1f, RowAlpha)
        };
        AddChild(container);

        int slotIndex = runState.GetPlayerSlotIndex(player);
        Label slotLabel = new()
        {
            Text = LocalModText.RoleSlot((slotIndex + 1).ToString()),
            MouseFilter = MouseFilterEnum.Ignore
        };
        slotLabel.AddThemeFontSizeOverride("font_size", 22);
        slotLabel.AddThemeColorOverride("font_color", new Color("f3efe6"));
        slotLabel.AddThemeColorOverride("font_outline_color", new Color("111111"));
        slotLabel.AddThemeConstantOverride("outline_size", 4);
        container.AddChild(slotLabel);

        return new GhostRow
        {
            PlayerId = player.NetId,
            Container = container,
            SlotLabel = slotLabel
        };
    }

    private void RebuildRowCards(GhostRow row, List<CardModel> handCards)
    {
        foreach (NCard cardNode in row.CardNodes)
        {
            ReleaseCardNode(cardNode);
        }

        row.CardNodes.Clear();
        row.Cards = handCards;

        foreach (CardModel card in handCards)
        {
            NCard? cardNode = NCard.Create(card);
            if (cardNode == null)
            {
                continue;
            }

            DisableInteractionRecursively(cardNode);
            cardNode.Scale = new Vector2(LocalGhostHandsRuntime.GhostScale, LocalGhostHandsRuntime.GhostScale);
            row.Container.AddChild(cardNode);
            row.CardNodes.Add(cardNode);
        }
    }

    private void LayoutRow(GhostRow row, int rowIndex)
    {
        float scale = LocalGhostHandsRuntime.GhostScale;
        Vector2 cardSize = NCard.defaultSize * scale;
        float spacing = cardSize.X * CardSpacingFactor;
        int count = row.CardNodes.Count;
        float rowWidth = count > 0 ? spacing * (count - 1) + cardSize.X : cardSize.X;

        Vector2 viewport = GetViewportRect().Size;
        float centerX = viewport.X * 0.5f + LocalGhostHandsRuntime.OffsetX;
        float baseY = viewport.Y + LocalGhostHandsRuntime.OffsetY - rowIndex * (cardSize.Y + RowGapPixels);

        row.Container.Position = new Vector2(centerX - rowWidth * 0.5f, baseY);
        for (int index = 0; index < count; index++)
        {
            row.CardNodes[index].Position = new Vector2(index * spacing, 0f);
        }

        row.SlotLabel.Position = new Vector2(-64f, cardSize.Y * 0.5f - 14f);
    }

    private void ClearRows()
    {
        foreach (GhostRow row in _rows)
        {
            foreach (NCard cardNode in row.CardNodes)
            {
                ReleaseCardNode(cardNode);
            }

            row.CardNodes.Clear();
            if (GodotObject.IsInstanceValid(row.Container))
            {
                row.Container.QueueFree();
            }
        }

        _rows.Clear();
    }

    private static void ReleaseCardNode(NCard cardNode)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(cardNode))
            {
                return;
            }

            cardNode.GetParent()?.RemoveChild(cardNode);
            NodePool.Free(cardNode);
        }
        catch (Exception exception)
        {
            LocalMultiControlLogger.Warn($"Failed to return ghost card node to pool: {exception.Message}");
            try
            {
                cardNode.QueueFree();
            }
            catch
            {
                // Node is already invalid; nothing left to clean up.
            }
        }
    }

    private static void DisableInteractionRecursively(Control control)
    {
        control.MouseFilter = MouseFilterEnum.Ignore;
        control.FocusMode = FocusModeEnum.None;
        foreach (Node child in control.GetChildren())
        {
            if (child is Control childControl)
            {
                DisableInteractionRecursively(childControl);
            }
        }
    }
}
