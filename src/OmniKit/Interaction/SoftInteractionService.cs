using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using OmniKit.Input;
using NativeEventHandler = FFXIVClientStructs.FFXIV.Client.Game.Event.EventHandler;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using NativeVector3 = FFXIVClientStructs.FFXIV.Common.Math.Vector3;
using NativeTargetType = FFXIVClientStructs.FFXIV.Client.Game.Object.TargetType;
using NativeUiState = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState;

namespace OmniKit.Interaction;

/// <summary>
/// Camera-directed, one-press interaction. A fresh press selects and confirms the
/// hard target before acting. Explicit enemy modes may enable native auto-attack.
/// Never advances dialogue, selects menu choices, or repeats while a key is held.
/// Update must run on Dalamud's framework thread; DrawHud must run in UiBuilder.Draw.
/// </summary>
internal sealed unsafe class SoftInteractionService(Configuration config) : IDisposable
{
    private static readonly ConditionFlag[] BlockingConditions =
    [
        ConditionFlag.Unconscious, ConditionFlag.Casting,
        ConditionFlag.Casting87, ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51,
        ConditionFlag.WatchingCutscene, ConditionFlag.WatchingCutscene78,
        ConditionFlag.Occupied, ConditionFlag.Occupied30, ConditionFlag.OccupiedInEvent,
        ConditionFlag.OccupiedInQuestEvent, ConditionFlag.Occupied33,
        ConditionFlag.OccupiedInCutSceneEvent, ConditionFlag.Occupied38,
        ConditionFlag.Occupied39, ConditionFlag.OccupiedSummoningBell,
        ConditionFlag.Jumping, ConditionFlag.Jumping61, ConditionFlag.InFlight,
        ConditionFlag.RidingPillion,
    ];

    private Candidate? candidate;
    private Candidate? previewCandidate;
    private InteractionActionPreview? previewBase;
    private InteractionActionPreview? nextAction;
    private ActionRequest? requestedAction;
    private long nextPreviewAt;
    private (bool Items, bool Emotes, SoftTargetAction Action) previewOptions;
    private Vector2 viewportPosition;
    private Vector2 viewportSize;
    private bool uiCapturesInput;
    private readonly SoftInteractionPress interactionPress = new();
    private bool disposed;
    private bool faulted;
    private long lastScan;
    private long lastDraw;
    private bool lastInputBlocked;
    private bool observationInitialized;
    private uint observationTerritory;
    private long observationReadyAt;
    private bool hudPopupOpen;
    private bool hudDragging;
    private bool hudPositionDirty;
    private Vector2 hudDragStartMouse;
    private Vector2 hudDragStartOffset;
    private Vector2 hudBoundsPosition;
    private Vector2 hudBoundsSize;
    private Vector2 hudReadyBoundsPosition;
    private Vector2 hudReadyBoundsSize;
    private Candidate? hudReadyCandidate;

    private const string HudWindowId = "##OmniKitVisibleTargetControls";
    private const string HudPopupId = "##OmniKitVisibleTargetMenu";
    private static readonly ConditionFlag[] HudInteractionConditions =
    [
        ConditionFlag.WatchingCutscene, ConditionFlag.WatchingCutscene78, ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.OccupiedInEvent, ConditionFlag.OccupiedInQuestEvent, ConditionFlag.OccupiedSummoningBell,
        ConditionFlag.Gathering, ConditionFlag.ExecutingGatheringAction,
    ];
    private static readonly string[] HudInteractionAddons =
    [
        "Talk", "CutSceneSelectString", "Shop", "InclusionShop", "FreeShop", "JournalAccept", "JournalResult",
        "RetainerList", "RetainerTaskAsk", "RetainerTaskResult", "Gathering", "GatheringMasterpiece",
    ];
    private const ImGuiWindowFlags HudWindowFlags = ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus
        | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDocking;

    public string? CandidateName => candidate?.Name;
    public string CandidateCategoryName => candidate == null ? "None" : SoftTargetPolicy.Label(candidate.Category);
    public string? Hint { get; private set; }
    public bool ActionPaused { get; private set; } = true;
    public bool HudPreviewEnabled { get; set; }
    public bool HudHiddenForInteraction { get; private set; }
    public GatheringGearService Gathering { get; } = new(config);
    public QuestItemService QuestItems { get; } = new(config);
    public QuestEmoteService QuestEmotes { get; } = new(config);
    public QuestInventoryService Inventory { get; } = new();
    public bool InventoryActionPaused { get; private set; } = true;
    public InteractionActionPreview? NextAction => Volatile.Read(ref nextAction);
    public bool RequestNextAction(InteractionActionPreview expected)
        => candidate is { } selected && selected.Id == expected.TargetId && selected.Address == expected.Address
            && ReferenceEquals(NextAction, expected) && expected.CanUse
            && Interlocked.CompareExchange(ref requestedAction, new(selected, null, Environment.TickCount64), null) == null;
    public bool RequestQuestItem(QuestInventoryEntry item)
        => item.CanUse && Interlocked.CompareExchange(ref requestedAction, new(null, item, Environment.TickCount64), null) == null;
    public event Action? ConfigureHudRequested;

    public void Update(bool inputBlocked)
    {
        if (disposed)
            return;

        lastInputBlocked = inputBlocked;

        // Use the physical state for the edge latch. Clearing IKeyState must not
        // manufacture a new press on the next frame, including OS key-repeat frames.
        var key = config.InteractKey;
        var down = HotkeyChord.IsKeyDown(key);
        var pressed = interactionPress.Sample((int)key, (int)config.InteractModifier, down);

        try
        {
            InventoryActionPaused = faulted || !Services.ClientState.IsLoggedIn
                || Services.Conditions[ConditionFlag.BetweenAreas] || Services.Conditions[ConditionFlag.BetweenAreas51]
                || GetActionPauseReason(inputBlocked, allowReleasedModifiers: true) != null;
            Inventory.Update(config.QuestItemsBarEnabled && !faulted);
            var click = Interlocked.Exchange(ref requestedAction, null);
            if (click != null)
            {
                // A click is consumed once on the framework thread. Delayed or
                // blocked clicks are discarded rather than retried later.
                interactionPress.Pause();
                if (Environment.TickCount64 - click.CreatedAt <= 250 && !faulted)
                {
                    if (click.Item is { } item)
                    {
                        if (Services.ClientState.IsLoggedIn && !Services.Conditions[ConditionFlag.BetweenAreas]
                            && !Services.Conditions[ConditionFlag.BetweenAreas51]
                            && GetActionPauseReason(inputBlocked, allowReleasedModifiers: true) == null)
                            Inventory.TryUse(item);
                    }
                    else if (config.SoftInteractEnabled && click.Target is { } target
                        && GetObservationUnavailableReason() == null && FindCandidate() == target
                        && GetActionPauseReason(inputBlocked, allowReleasedModifiers: true,
                            enemy: SoftTargetPolicy.IsEnemy(target.Category)) == null)
                    {
                        var current = Services.Objects.SearchById(target.Id);
                        if (current != null && current.IsValid() && current.Address == target.Address
                            && (SoftTargetPolicy.IsEnemy(target.Category)
                                || !Gathering.TryPrepare(current, () => InteractIfStillEligible(target))))
                            InteractIfStillEligible(target);
                    }
                }
                return;
            }
        }
        catch (Exception exception)
        {
            faulted = true;
            InventoryActionPaused = true;
            Volatile.Write(ref nextAction, null);
            Services.Log.Error("Quest action controls stopped ({ErrorType}).", exception.GetType().Name);
        }

        if (!config.SoftInteractEnabled || faulted)
        {
            Gathering.Dispose();
            candidate = null;
            Volatile.Write(ref nextAction, null);
            observationInitialized = false;
            ActionPaused = true;
            interactionPress.Pause();
            if (!faulted)
                Hint = "Soft interaction is off.";
            return;
        }

        try
        {
            if (GetObservationUnavailableReason() is { } unavailable)
            {
                // Cancel delayed equipment/interaction work without inspecting stale
                // world objects while zoning, logged out, or lacking a safe camera.
                Gathering.Dispose();
                Clear(unavailable);
                return;
            }

            // Gathering retains the non-combat object guards even if the camera
            // is currently looking at an enemy.
            Gathering.Update(GetActionPauseReason(inputBlocked, allowReleasedModifiers: true) != null);

            // Observation is independent of action permission. Menus, typing and
            // modifier keys pause the key, but keep the camera detector reviewable.
            var now = Environment.TickCount64;
            if (pressed || now - lastScan >= 33)
            {
                lastScan = now;
                candidate = FindCandidate();
            }

            if (candidate is not { } selected)
            {
                Volatile.Write(ref nextAction, null);
                var unavailableAction = GetActionPauseReason(inputBlocked, allowReleasedModifiers: !down);
                ActionPaused = unavailableAction != null;
                if (ActionPaused) interactionPress.Pause();
                Hint = unavailableAction ?? (config.InteractionTargets == InteractionTargets.None
                    ? "All target types are off. Choose target types in Interact settings."
                    : "Look toward an enabled target type.");
                return;
            }

            var pauseReason = GetActionPauseReason(inputBlocked, allowReleasedModifiers: !down,
                enemy: SoftTargetPolicy.IsEnemy(selected.Category));
            var action = SoftTargetPolicy.ActionFor(config.InteractionTargets, config.EnemyInteractMode, selected.Category);
            if (pauseReason == null && action == SoftTargetAction.None)
                pauseReason = "Choose what the interact key does to enemies in Interact settings.";
            ActionPaused = pauseReason != null;
            RefreshNextAction(selected, action, pauseReason);
            if (ActionPaused)
            {
                interactionPress.Pause();
                Hint = pauseReason;
                return;
            }

            var verb = action == SoftTargetAction.AttackEnemy ? "target and attack" : action == SoftTargetAction.TargetEnemy ? "target" : "interact with";
            Hint = $"{HotkeyChord.Format(key, config.InteractModifier)}: {verb} {selected.Name}";
            if (pressed && HotkeyChord.IsDown(key, config.InteractModifier))
            {
                // FindCandidate was rerun on this exact key edge. Resolve by ID again
                // so an object-table slot recycled during a zone change is never used.
                var current = Services.Objects.SearchById(selected.Id);
                if (current == null || current.Address != selected.Address || !current.IsValid())
                {
                    Clear("The interaction target is no longer available.");
                    return;
                }

                var targets = TargetSystem.Instance();
                if (targets == null)
                    return;

                Services.Keys[key] = false;
                interactionPress.Consume();
                if (SoftTargetPolicy.IsEnemy(selected.Category)
                    || !Gathering.TryPrepare(current, () => InteractIfStillEligible(selected)))
                    InteractIfStillEligible(selected);
            }
            else if (down && interactionPress.Consumed)
            {
                // Swallow repeats only while still eligible and unblocked. The same
                // physical hold can never generate a second native interaction.
                Services.Keys[key] = false;
            }
        }
        catch (Exception exception)
        {
            // Fail closed for this plugin session if a native binding is unavailable.
            // Do not log object names, character details, or other game-state content.
            faulted = true;
            Clear("Soft interaction stopped after an error. Reload OmniKit to retry.");
            Services.Log.Error("Soft interaction stopped ({ErrorType}).", exception.GetType().Name);
        }
    }

    public void DrawHud()
    {
        if (disposed)
            return;

        var viewport = ImGui.GetMainViewport();
        viewportPosition = viewport.Pos;
        viewportSize = viewport.Size;
        lastDraw = Environment.TickCount64;
        CaptureUiInput();

        // Save a completed drag once, including when the candidate disappeared
        // before release. Do not write the configuration on every mouse movement.
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            hudDragging = false;
            if (hudPositionDirty)
            {
                hudPositionDirty = false;
                SaveHudConfiguration();
            }
        }

        HudHiddenForInteraction = IsWorldInteractionActive();
        if (HudHiddenForInteraction || !config.SoftInteractEnabled || !config.InteractionHudEnabled
            || faulted || candidate == null || candidate != hudReadyCandidate)
            hudReadyCandidate = null;
        // Shift can pause the game action before this draw. Allow its deliberate
        // edit gesture over the last ready text bounds, without enabling an action
        // or reviving stale targets. An unrelated Shift press creates no hitbox.
        var enteringModification = hudReadyCandidate != null && ImGui.GetIO().KeyShift
            && ImGui.IsMouseHoveringRect(hudReadyBoundsPosition, hudReadyBoundsPosition + hudReadyBoundsSize, false);
        var modifying = HudPreviewEnabled || hudPopupOpen || hudDragging || enteringModification;
        if (HudHiddenForInteraction || (!modifying && (!config.SoftInteractEnabled || !config.InteractionHudEnabled || faulted
            || candidate == null || ActionPaused || uiCapturesInput)))
        {
            CloseHudMenu();
            hudDragging = false;
            return;
        }

        // The normal locked HUD is draw-list text without an input window. Its
        // text-bounds editor is created only for deliberate HUD interaction.
        var draw = ImGui.GetForegroundDrawList();
        var font = ImGui.GetFont();
        var hudScale = float.IsFinite(config.InteractionHudScale)
            ? Math.Clamp(config.InteractionHudScale, 0.5f, 2f) : 1f;
        // Only deliberate modification shows a sample while interaction is paused.
        var prefix = modifying ? string.Empty : $"[{HotkeyChord.Format(config.InteractKey, config.InteractModifier)}]  ";
        var suffix = HudPreviewEnabled ? "  · Preview" : string.Empty;
        var originalName = candidate?.Name ?? "Visible target";
        var name = originalName;
        var available = Vector2.Max(Vector2.One, viewportSize - new Vector2(16));
        var fontSize = ImGui.GetFontSize() * hudScale;
        var inset = Vector2.Zero; var boundsSize = Vector2.Zero;
        HudTextMetrics metrics = default;
        // Fit actual glyph advances and ink overhang plus the worst effect extent.
        // The same fixed bounds govern clamping, dragging and the Shift hitbox.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var padding = HudTextRenderer.Padding(config.InteractionHudEffect, fontSize,
                config.InteractionHudEffectIntensity, config.InteractionHudOutline, config.InteractionHudOutlineThickness);
            var nameWidth = Math.Max(0, available.X - padding.X * 2
                - HudTextRenderer.Measure(font, fontSize, prefix, string.Empty, suffix).Size.X);
            name = HudTextRenderer.Elide(font, fontSize, originalName, nameWidth);
            metrics = HudTextRenderer.Measure(font, fontSize, prefix, name, suffix);
            inset = padding - metrics.InkMinimum;
            boundsSize = metrics.InkMaximum - metrics.InkMinimum + padding * 2;
            var fit = Math.Min(available.X / Math.Max(1, boundsSize.X), available.Y / Math.Max(1, boundsSize.Y));
            if (fit >= 1 || attempt == 3) break;
            fontSize *= Math.Max(.01f, fit * .99f);
        }
        var center = viewportPosition + viewportSize * 0.5f;
        var offset = new Vector2(
            float.IsFinite(config.InteractionHudOffsetX) ? config.InteractionHudOffsetX : 0f,
            float.IsFinite(config.InteractionHudOffsetY) ? config.InteractionHudOffsetY : 120f);
        var anchor = inset + new Vector2(metrics.Size.X * .5f, 0);
        var topLeft = ClampHudPosition(center + offset - anchor, boundsSize);
        DrawHudControls(ref topLeft, boundsSize, center, anchor, enteringModification);
        if (!config.InteractionHudEnabled && !HudPreviewEnabled)
            return;
        if (!modifying)
        {
            hudReadyBoundsPosition = topLeft;
            hudReadyBoundsSize = boundsSize;
            hudReadyCandidate = candidate;
        }

        draw.PushClipRect(viewportPosition, viewportPosition + viewportSize, true);
        try
        {
            HudTextRenderer.Draw(draw, font, fontSize, topLeft + inset, prefix, name, suffix,
                config.InteractionHudEffect, config.InteractionHudOutline, config.InteractionHudEffectIntensity,
                config.InteractionHudEffectSpeed, config.InteractionHudEffectAnimated, ImGui.GetTime(), config.InteractionHudOutlineThickness);
        }
        finally { draw.PopClipRect(); }
    }

    private void RefreshNextAction(Candidate selected, SoftTargetAction action, string? paused)
    {
        var now = Environment.TickCount64;
        var options = (config.QuestItemUseEnabled, config.QuestEmoteUseEnabled, action);
        if (previewCandidate != selected || previewOptions != options || now >= nextPreviewAt)
        {
            previewOptions = options;
            previewCandidate = selected;
            nextPreviewAt = now + 333;
            var target = Services.Objects.SearchById(selected.Id);
            var quest = target == null || !target.IsValid() || target.Address != selected.Address
                ? null : QuestItems.Preview(target) ?? QuestEmotes.Preview(target);
            previewBase = quest is { } required
                ? new(selected.Id, selected.Address, required.Name, required.Kind == QuestActionKind.Emote ? "Emote" : "Use quest item",
                    required.IconId, required.CanUse, required.Reason)
                : new(selected.Id, selected.Address, selected.Name, action == SoftTargetAction.AttackEnemy ? "Target + attack"
                    : action == SoftTargetAction.TargetEnemy ? "Target" : "Interact", 0,
                    action != SoftTargetAction.None, string.Empty);
        }
        Volatile.Write(ref nextAction, previewBase == null ? null : previewBase with
        { CanUse = previewBase.CanUse && paused == null, Reason = paused ?? previewBase.Reason });
    }

    public void CaptureUiInput(bool ownActionButton = false)
    {
        var io = ImGui.GetIO();
        uiCapturesInput = io.WantTextInput || hudPopupOpen || hudDragging
            || !ownActionButton && (io.WantCaptureKeyboard || ImGui.IsAnyItemActive());
    }
    private static bool IsWorldInteractionActive()
    {
        // World-interaction lifecycle flags cover the gaps between dialogue pages,
        // quest prompts and retainer/gathering windows. UI capture, generic Occupied,
        // player chat, inventory and settings alone do not suppress observation or HUD.
        var conditions = Services.Conditions;
        if (conditions != null)
            foreach (var flag in HudInteractionConditions)
                if (conditions[flag]) return true;
        var gui = Services.GameGui;
        if (gui == null) return false;
        foreach (var name in HudInteractionAddons)
        {
            var addon = gui.GetAddonByName(name, 1);
            if (!addon.IsNull && addon.IsVisible) return true;
        }
        return false;
    }

    private void DrawHudControls(ref Vector2 topLeft, Vector2 textSize, Vector2 center, Vector2 anchor, bool enteringModification)
    {
        var shift = ImGui.GetIO().KeyShift;
        // Removing the active key hint narrows the text. Retain the original
        // gesture bounds while entering so that a click on that hint still opens.
        var hitPosition = enteringModification ? Vector2.Min(topLeft, hudReadyBoundsPosition) : topLeft;
        var hitSize = enteringModification
            ? Vector2.Max(topLeft + textSize, hudReadyBoundsPosition + hudReadyBoundsSize) - hitPosition : textSize;
        var hovered = ImGui.IsMouseHoveringRect(hitPosition, hitPosition + hitSize, false);
        if (config.InteractionHudLocked)
            hudDragging = false;
        if (config.InteractionHudLocked && !(shift && hovered) && !hudPopupOpen && !hudDragging)
            return;

        hudBoundsPosition = hitPosition;
        hudBoundsSize = hitSize;
        var visible = BeginHudWindow(hitPosition, hitSize);
        try
        {
            if (!visible) return;
            var buttons = ImGuiButtonFlags.MouseButtonRight;
            if (!config.InteractionHudLocked) buttons |= ImGuiButtonFlags.MouseButtonLeft;
            ImGui.InvisibleButton("##visible-target-text", hitSize, buttons);
            if (shift && hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !hudPopupOpen)
            {
                ImGui.OpenPopup(HudPopupId, ImGuiPopupFlags.None);
                // Choose a side before ImGui's viewport clamp can push the menu
                // back over the foreground text near the bottom of the screen.
                var popupHeight = 4 * ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y * 2;
                var popupY = topLeft.Y + textSize.Y + 6;
                if (popupY + popupHeight > viewportPosition.Y + viewportSize.Y - 8)
                    popupY = Math.Max(viewportPosition.Y + 8, topLeft.Y - popupHeight - 6);
                ImGui.SetNextWindowPos(new Vector2(topLeft.X, popupY), ImGuiCond.Appearing);
            }

            if (!config.InteractionHudLocked && !hudPopupOpen && ImGui.IsItemActive()
                && ImGui.IsMouseDown(ImGuiMouseButton.Left) && !hudDragging)
            {
                hudDragging = true;
                hudDragStartMouse = ImGui.GetMousePos();
                // Start from the actual clamped position, not an off-screen setting.
                hudDragStartOffset = topLeft - center + anchor;
            }
            if (hudDragging && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var moved = hudDragStartOffset + ImGui.GetMousePos() - hudDragStartMouse;
                topLeft = ClampHudPosition(center + moved - anchor, textSize);
                var actualOffset = topLeft - center + anchor;
                if (config.InteractionHudOffsetX != actualOffset.X || config.InteractionHudOffsetY != actualOffset.Y)
                {
                    config.InteractionHudOffsetX = actualOffset.X;
                    config.InteractionHudOffsetY = actualOffset.Y;
                    hudPositionDirty = true;
                }
            }

            if (ImGui.BeginPopup(HudPopupId, ImGuiWindowFlags.None))
            {
                hudDragging = false;
                if (ImGui.MenuItem(config.InteractionHudLocked ? "Unlock" : "Lock", false, true))
                {
                    config.InteractionHudLocked = !config.InteractionHudLocked;
                    SaveHudConfiguration();
                }
                if (ImGui.MenuItem("Move", false, true))
                {
                    config.InteractionHudLocked = false;
                    SaveHudConfiguration();
                }
                if (ImGui.MenuItem("Modify", false, true))
                    ConfigureHudRequested?.Invoke();
                if (ImGui.MenuItem("Hide", false, true))
                {
                    config.InteractionHudEnabled = false;
                    SaveHudConfiguration();
                }
                ImGui.EndPopup();
            }
            hudPopupOpen = ImGui.IsPopupOpen(HudPopupId, ImGuiPopupFlags.None);
            uiCapturesInput |= hudPopupOpen || hudDragging;
        }
        finally
        {
            ImGui.End();
        }
    }

    private static bool BeginHudWindow(Vector2 position, Vector2 size, bool noInputs = false)
    {
        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(Vector2.Max(size, Vector2.One), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.One);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        var visible = ImGui.Begin(HudWindowId, HudWindowFlags | (noInputs ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None));
        // Window layout has captured these values. Restore defaults before drawing
        // the context menu so its controls retain their normal visible gutters.
        ImGui.PopStyleVar(3);
        return visible;
    }

    private void CloseHudMenu()
    {
        if (!hudPopupOpen) return;
        // Dismiss the popup in its owning ID scope. No target means no input hitbox
        // or empty HUD window, including this one-frame popup cleanup.
        BeginHudWindow(hudBoundsPosition, hudBoundsSize, noInputs: true);
        if (ImGui.BeginPopup(HudPopupId, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs))
        {
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        ImGui.End();
        hudPopupOpen = false;
    }

    private Vector2 ClampHudPosition(Vector2 position, Vector2 size) => Vector2.Clamp(position,
        viewportPosition + new Vector2(8),
        Vector2.Max(viewportPosition + new Vector2(8), viewportPosition + viewportSize - size - new Vector2(8)));

    private void SaveHudConfiguration()
    {
        try { config.Save(); }
        catch (Exception exception)
        {
            Hint = "The HUD change could not be saved.";
            Services.Log.Error("HUD settings could not be saved ({ErrorType}).", exception.GetType().Name);
        }
    }

    public void Dispose()
    {
        disposed = true;
        Gathering.Dispose();
        candidate = null;
        Hint = null;
        ActionPaused = true;
        // No hooks, target mutations, or persistent keyboard capture to restore.
    }

    private bool InteractIfStillEligible(Candidate expected)
    {
        if (disposed || faulted || !config.SoftInteractEnabled
            || GetObservationUnavailableReason() != null
            || GetActionPauseReason(lastInputBlocked, allowReleasedModifiers: true,
                enemy: SoftTargetPolicy.IsEnemy(expected.Category)) != null)
            return false;
        var actual = FindCandidate();
        if (actual == null || actual.Id != expected.Id || actual.Address != expected.Address || actual.Category != expected.Category)
            return false;
        var action = SoftTargetPolicy.ActionFor(config.InteractionTargets, config.EnemyInteractMode, actual.Category);
        if (action == SoftTargetAction.None) return false;
        var target = Services.Objects.SearchById(actual.Id);
        var system = TargetSystem.Instance();
        if (target == null || system == null || target.GameObjectId != actual.Id || target.Address != actual.Address
            || !target.IsValid() || !target.IsTargetable)
            return false;
        var native = (NativeGameObject*)target.Address;
        if (native == null || !native->GetIsTargetable() || Classify(target, native) != actual.Category)
            return false;

        // This callback runs on the fresh press, or only after gathering has
        // finished preparing. Looking at a candidate never changes the hard target.
        bool HasExpectedTarget()
        {
            var hardTarget = Services.Targets.Target;
            return hardTarget != null && hardTarget.IsValid()
                && hardTarget.GameObjectId == actual.Id && hardTarget.Address == actual.Address;
        }
        return SoftTargetPolicy.AfterConfirmedTarget(
            () => HasExpectedTarget() || system->SetHardTarget(native),
            // Confirm again after selection; a replaced target must never act.
            HasExpectedTarget,
            () =>
            {
                if (action == SoftTargetAction.TargetEnemy) return true;
                if (action == SoftTargetAction.AttackEnemy)
                {
                    var uiState = NativeUiState.Instance();
                    return uiState != null
                        && (uiState->WeaponState.AutoAttackState.Get() || uiState->WeaponState.AutoAttackState.Set(true));
                }
                var itemUse = QuestItems.TryUse(target);
                if (itemUse != QuestItemUseResult.NotApplicable) return itemUse == QuestItemUseResult.Used;
                var emoteUse = QuestEmotes.TryUse(target);
                if (emoteUse != QuestEmoteUseResult.NotApplicable) return emoteUse == QuestEmoteUseResult.Used;
                // Preserve native range/state/LOS checks. The return value is not
                // proof that the game accepted or completed the interaction.
                system->InteractWithObject(native, true);
                return true;
            });
    }

    private string? GetObservationUnavailableReason()
    {
        // These managed state checks precede object/camera access. UI capture alone
        // is safe for observation and deliberately does not appear in this guard.
        if (!Services.ClientState.IsLoggedIn)
            return ObservationUnavailable("Detection unavailable while logged out.");
        if (Services.Conditions[ConditionFlag.BetweenAreas] || Services.Conditions[ConditionFlag.BetweenAreas51])
            return ObservationUnavailable("Detection unavailable while changing areas.");
        if (Services.ClientState.IsGPosing || Services.Conditions[ConditionFlag.WatchingCutscene]
            || Services.Conditions[ConditionFlag.WatchingCutscene78]
            || Services.Conditions[ConditionFlag.OccupiedInCutSceneEvent])
            return ObservationUnavailable("Detection unavailable during a cutscene or group pose.");
        if (viewportSize.X < 1 || viewportSize.Y < 1 || Environment.TickCount64 - lastDraw > 250)
            return ObservationUnavailable("Detection waiting for the game viewport.");

        var framework = NativeFramework.Instance();
        if (framework == null || framework->IsExiting)
            return ObservationUnavailable("Detection unavailable while the game is closing or initializing.");
        if (framework->BGCollisionModule == null || framework->BGCollisionModule->ShuttingDown
            || framework->BGCollisionModule->LoadInProgressCounter != 0)
            return ObservationUnavailable("Detection waiting for the area's collision data.");
        var player = Services.Objects.LocalPlayer;
        if (player == null || !player.IsValid())
            return ObservationUnavailable("Detection waiting for the local player.");
        var cameras = CameraManager.Instance();
        if (TargetSystem.Instance() == null || cameras == null || cameras->GetActiveCamera() == null)
            return ObservationUnavailable("Detection waiting for a usable game camera.");

        var now = Environment.TickCount64;
        var territory = Services.ClientState.TerritoryType;
        if (!observationInitialized || territory != observationTerritory)
        {
            observationInitialized = true;
            observationTerritory = territory;
            observationReadyAt = now + 250;
        }
        return now < observationReadyAt ? "Detection waiting for the area to stabilize." : null;
    }

    private string ObservationUnavailable(string reason)
    {
        observationInitialized = false;
        return reason;
    }

    private string? GetActionPauseReason(bool inputBlocked, bool allowReleasedModifiers = false, bool enemy = false)
    {
        if (inputBlocked)
            return "Interaction paused while OmniKit is open; camera detection remains active.";
        if (hudPopupOpen || hudDragging)
            return "Interaction paused while editing the visible-target HUD.";
        if (uiCapturesInput)
            return "Interaction paused while a plugin is using keyboard input; camera detection remains active.";
        if (Services.GameGui.GameUiHidden)
            return "Interaction paused while the game UI is hidden.";
        if (!IsUsableKey(config.InteractKey))
            return "Interaction paused: choose a keyboard key other than a modifier.";
        if (!HotkeyChord.IsValidModifiers(config.InteractModifier)
            || HotkeyChord.IsKeyDown(VirtualKey.LWIN) || HotkeyChord.IsKeyDown(VirtualKey.RWIN))
            return "Interaction paused while a system shortcut is active or the shortcut is invalid.";
        var modifiers = HotkeyChord.CurrentModifiers;
        if (modifiers != config.InteractModifier && !(allowReleasedModifiers && modifiers == ModifierKeys.None))
            return $"Interaction paused: use {HotkeyChord.Format(config.InteractKey, config.InteractModifier)} without extra modifiers.";
        foreach (var flag in BlockingConditions)
            if (Services.Conditions[flag])
                return "Interaction paused while the character is busy; camera detection remains active.";
        if (!enemy && Services.Conditions[ConditionFlag.InCombat])
            return "Object interaction paused during combat; camera detection remains active.";
        var framework = NativeFramework.Instance();
        if (framework == null || framework->WindowInactive || framework->IsExiting)
            return "Interaction paused while the game is not focused.";

        var ui = RaptureAtkModule.Instance();
        if (ui == null || ui->AtkUnitManager == null)
            return "Interaction paused while game text input or UI initialization is active.";

        var focused = ui->AtkUnitManager->FocusedAddon;
        return SoftInteractionUiPolicy.PauseReason(ui->IsTextInputActive(), IsWorldInteractionActive(),
            focused != null && focused->IsVisible, focused == null ? null : focused->NameString);
    }

    private Candidate? FindCandidate()
    {
        var player = Services.Objects.LocalPlayer;
        var targets = TargetSystem.Instance();
        var cameraManager = CameraManager.Instance();
        var camera = cameraManager == null ? null : cameraManager->GetActiveCamera();
        if (player == null || targets == null || camera == null)
            return null;

        var cone = float.IsFinite(config.InteractionCone) ? Math.Clamp(config.InteractionCone, 0.025f, 0.3f) : 0.1f;
        var maxDistance = float.IsFinite(config.InteractionDistance)
            ? Math.Clamp(config.InteractionDistance, Configuration.MinimumDetectionDistance, Configuration.MaximumDetectionDistance)
            : Configuration.DefaultDetectionDistance;
        Vector3 cameraPosition = camera->SceneCamera.Position;
        Candidate? best = null;
        var bestScore = float.MaxValue;

        foreach (var obj in Services.Objects)
        {
            if (obj == null || obj.GameObjectId == player.GameObjectId
                || !obj.IsValid() || !obj.IsTargetable)
                continue;

            var distance = Math.Max(0, Vector3.Distance(player.Position, obj.Position)
                - Math.Max(0, player.HitboxRadius) - Math.Max(0, obj.HitboxRadius));
            if (!float.IsFinite(distance) || distance > maxDistance)
                continue;

            var native = (NativeGameObject*)obj.Address;
            if (native == null || !native->GetIsTargetable() || !targets->IsObjectInViewRange(native))
                continue;
            var category = Classify(obj, native);
            if (!SoftTargetPolicy.IsEnabled(config.InteractionTargets, category)) continue;

            var worldCenter = new NativeVector3();
            native->GetCenterPosition(&worldCenter);
            Vector3 position = worldCenter;
            var projectedOnScreen = Services.GameGui.WorldToScreen(position, out var screenPosition);
            if (!SoftTargetGeometry.TryScore(screenPosition, projectedOnScreen, viewportPosition, viewportSize,
                    cone, distance, maxDistance, candidate?.Id == obj.GameObjectId, out var score))
                continue;

            // Geometry only ranks visible aim candidates; native eligibility,
            // active handlers and the camera collision ray remain mandatory.
            if (score >= bestScore || (!SoftTargetPolicy.IsEnemy(category) && !HasActiveInteraction(native, obj.ObjectKind)
                    && !QuestItems.CanOfferTarget(obj) && !QuestEmotes.CanOfferTarget(obj))
                || !HasCameraLineOfSight(cameraPosition, position, obj.HitboxRadius))
                continue;

            var name = obj.Name.TextValue;
            if (string.IsNullOrWhiteSpace(name))
                name = "Interact";
            best = new Candidate(obj.GameObjectId, obj.Address, name, category);
            bestScore = score;
        }

        return best;
    }

    private static bool HasActiveInteraction(NativeGameObject* obj, ObjectKind kind)
    {
        // A selectable NPC is not necessarily a usable NPC. Event-backed objects
        // must expose an active native handler before we offer the interaction.
        if (kind is ObjectKind.EventNpc or ObjectKind.EventObj)
        {
            var handlers = stackalloc NativeEventHandler*[32];
            var count = obj->GetEventHandlersImpl(handlers);
            for (var i = 0; i < Math.Clamp(count, 0, 32); ++i)
                if (handlers[i] != null && handlers[i]->IsActive(obj))
                    return true;
            return false;
        }

        return obj->EventHandler == null || obj->EventHandler->IsActive(obj);
    }

    private static bool HasCameraLineOfSight(Vector3 camera, Vector3 target, float hitboxRadius)
    {
        var direction = target - camera;
        var distance = direction.Length();
        if (!float.IsFinite(distance) || distance < 0.001f)
            return false;

        // Stop at the near surface of the target so its own collision geometry
        // does not hide it. Native InteractWithObject separately checks player LOS.
        var tolerance = float.IsFinite(hitboxRadius) ? Math.Clamp(hitboxRadius, 0.1f, 0.75f) : 0.1f;
        var rayDistance = distance - tolerance;
        return rayDistance <= 0 || !BGCollisionModule.RaycastMaterialFilter(camera,
            direction / distance, out _, rayDistance);
    }

    private static InteractionTargets Classify(IGameObject obj, NativeGameObject* native)
    {
        if (obj.ObjectKind == ObjectKind.BattleNpc)
        {
            if (obj is not IBattleNpc npc || npc.BattleNpcKind != BattleNpcSubKind.Combatant
                || native->IsDead() || native->GetTargetType() != NativeTargetType.Enemy)
                return InteractionTargets.None;
            // Use the game's hostility flag, not InCombat. This can change with
            // game state; it is not a lookup of permanent species aggression.
            return (npc.StatusFlags & StatusFlags.Hostile) != 0
                ? InteractionTargets.HostileEnemies : InteractionTargets.NeutralEnemies;
        }
        return obj.ObjectKind switch
        {
            ObjectKind.EventNpc => InteractionTargets.Npcs,
            ObjectKind.EventObj => InteractionTargets.QuestObjects,
            ObjectKind.Treasure => InteractionTargets.Loot,
            ObjectKind.Aetheryte => InteractionTargets.Aetherytes,
            ObjectKind.GatheringPoint => InteractionTargets.GatheringNodes,
            ObjectKind.Retainer => InteractionTargets.Retainers,
            ObjectKind.HousingEventObject => InteractionTargets.Housing,
            ObjectKind.ReactionEventObject => InteractionTargets.OtherUsableObjects,
            _ => InteractionTargets.None,
        };
    }

    private static bool IsUsableKey(VirtualKey key) => HotkeyChord.IsValidKey(key)
        && Services.Keys.IsVirtualKeyValid((int)key);

    private void Clear(string reason)
    {
        candidate = null;
        Volatile.Write(ref nextAction, null);
        Hint = reason;
        ActionPaused = true;
        interactionPress.Pause();
    }

    private sealed record Candidate(ulong Id, nint Address, string Name, InteractionTargets Category);
    private sealed record ActionRequest(Candidate? Target, QuestInventoryEntry? Item, long CreatedAt);
}
