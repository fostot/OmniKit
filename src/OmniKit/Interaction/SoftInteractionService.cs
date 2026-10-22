using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using NativeEventHandler = FFXIVClientStructs.FFXIV.Client.Game.Event.EventHandler;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using NativeVector3 = FFXIVClientStructs.FFXIV.Common.Math.Vector3;

namespace OmniKit.Interaction;

/// <summary>
/// Camera-directed, one-press interaction. This service never writes hard/soft targets,
/// advances dialogue, selects menu choices, or repeats an interaction while a key is held.
/// Update must run on Dalamud's framework thread; DrawHud must run in UiBuilder.Draw.
/// </summary>
internal sealed unsafe class SoftInteractionService(Configuration config) : IDisposable
{
    private static readonly ConditionFlag[] BlockingConditions =
    [
        ConditionFlag.Unconscious, ConditionFlag.InCombat, ConditionFlag.Casting,
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
    private Vector2 viewportPosition;
    private Vector2 viewportSize;
    private bool uiCapturesInput;
    private bool wasKeyDown;
    private bool keyInitialized;
    private bool consumedPress;
    private VirtualKey lastKey;
    private bool disposed;
    private bool faulted;
    private long lastScan;
    private long lastDraw;
    private bool lastInputBlocked;

    public string? CandidateName => candidate?.Name;
    public string? Hint { get; private set; }
    public GatheringGearService Gathering { get; } = new(config);

    public void Update(bool inputBlocked)
    {
        if (disposed)
            return;

        lastInputBlocked = inputBlocked;

        // Use the physical state for the edge latch. Clearing IKeyState must not
        // manufacture a new press on the next frame, including OS key-repeat frames.
        var key = config.InteractKey;
        var down = IsPhysicalKeyDown(key);
        if (!keyInitialized || key != lastKey)
        {
            keyInitialized = true;
            lastKey = key;
            wasKeyDown = down;
            consumedPress = false;
        }

        var pressed = down && !wasKeyDown;
        wasKeyDown = down;
        if (!down)
            consumedPress = false;

        if (!config.SoftInteractEnabled || faulted)
        {
            Gathering.Dispose();
            candidate = null;
            if (!faulted)
                Hint = "Soft interaction is off.";
            return;
        }

        try
        {
            Gathering.Update(inputBlocked || uiCapturesInput);
            if (!IsUsableKey(key))
            {
                Clear("Choose a keyboard key other than a modifier.");
                return;
            }

            if (IsBlocked(inputBlocked))
            {
                Clear("Paused while a menu, text input, cutscene, or busy state is active.");
                return;
            }

            // Modified shortcuts belong to the game or other plugins.
            if (IsPhysicalKeyDown(VirtualKey.CONTROL) || IsPhysicalKeyDown(VirtualKey.MENU)
                || IsPhysicalKeyDown(VirtualKey.SHIFT))
            {
                Clear("Release modifier keys to interact.");
                return;
            }

            var now = Environment.TickCount64;
            if (pressed || now - lastScan >= 33)
            {
                lastScan = now;
                candidate = FindCandidate();
            }

            if (candidate is not { } selected)
            {
                Hint = "Look toward a nearby interactable NPC or object.";
                return;
            }

            Hint = $"{key}: interact with {selected.Name}";
            if (pressed)
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
                consumedPress = true;
                if (!Gathering.TryPrepare(current, () => InteractIfStillEligible(selected)))
                    InteractIfStillEligible(selected);
            }
            else if (down && consumedPress)
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
        uiCapturesInput = ImGui.GetIO().WantCaptureKeyboard || ImGui.GetIO().WantTextInput
            || ImGui.IsAnyItemActive();

        if (!config.SoftInteractEnabled || !config.InteractionHudEnabled || faulted
            || uiCapturesInput || candidate is not { } selected)
            return;

        // A draw-list overlay has no hit box and cannot steal focus from the game.
        // Native highlight/target fields are untouched, so other plugins retain theirs.
        var draw = ImGui.GetBackgroundDrawList();
        var scale = Math.Clamp(ImGui.GetFontSize() / 17f, 0.75f, 2.5f);
        var text = $"[{config.InteractKey}]  {selected.Name}";
        var availableWidth = Math.Max(80f, viewportSize.X * 0.75f - 32f * scale);
        while (text.Length > 8 && ImGui.CalcTextSize(text).X > availableWidth)
            text = text[..^2].TrimEnd('…') + "…";

        var textSize = ImGui.CalcTextSize(text);
        var padding = new Vector2(14, 9) * scale;
        var boxSize = textSize + padding * 2;
        var center = viewportPosition + viewportSize * 0.5f;
        var offset = float.IsFinite(config.InteractionHudOffsetY) ? config.InteractionHudOffsetY : 120f;
        var topLeft = new Vector2(center.X - boxSize.X * 0.5f, center.Y + offset);
        topLeft = Vector2.Clamp(topLeft, viewportPosition + new Vector2(8),
            Vector2.Max(viewportPosition + new Vector2(8), viewportPosition + viewportSize - boxSize - new Vector2(8)));

        draw.AddRectFilled(topLeft, topLeft + boxSize, 0xEB25201C, 7f * scale);
        draw.AddRect(topLeft, topLeft + boxSize, 0xC59BC9DD, 7f * scale);
        draw.AddText(topLeft + padding, 0xFFF0F3F6, text);
        draw.AddCircle(selected.ScreenPosition, 6f * scale, 0xE39BC9DD, 20, 1.5f * scale);
    }

    public void Dispose()
    {
        disposed = true;
        Gathering.Dispose();
        candidate = null;
        Hint = null;
        // No hooks, target mutations, or persistent keyboard capture to restore.
    }

    private bool InteractIfStillEligible(Candidate expected)
    {
        if (disposed || faulted || !config.SoftInteractEnabled || IsBlocked(lastInputBlocked))
            return false;
        var actual = FindCandidate();
        if (actual == null || actual.Id != expected.Id || actual.Address != expected.Address)
            return false;
        var target = Services.Objects.SearchById(actual.Id);
        var system = TargetSystem.Instance();
        if (target == null || system == null || target.Address != expected.Address)
            return false;
        // Preserve the native range/state/LOS checks. Its return value is not treated
        // as proof that the game accepted or completed the requested interaction.
        system->InteractWithObject((NativeGameObject*)target.Address, true);
        return true;
    }

    private bool IsBlocked(bool inputBlocked)
    {
        if (inputBlocked || uiCapturesInput || !Services.ClientState.IsLoggedIn
            || Services.ClientState.IsGPosing || Services.GameGui.GameUiHidden
            || Services.Objects.LocalPlayer == null || viewportSize.X < 1 || viewportSize.Y < 1
            || Environment.TickCount64 - lastDraw > 250)
            return true;

        foreach (var flag in BlockingConditions)
            if (Services.Conditions[flag])
                return true;

        var framework = NativeFramework.Instance();
        if (framework == null || framework->WindowInactive || framework->IsExiting
            || framework->BGCollisionModule == null || framework->BGCollisionModule->ShuttingDown
            || framework->BGCollisionModule->LoadInProgressCounter != 0)
            return true;

        var ui = RaptureAtkModule.Instance();
        if (ui == null || ui->IsTextInputActive() || ui->AtkUnitManager == null)
            return true;

        var focused = ui->AtkUnitManager->FocusedAddon;
        return focused != null && focused->IsVisible;
    }

    private Candidate? FindCandidate()
    {
        var player = Services.Objects.LocalPlayer;
        var targets = TargetSystem.Instance();
        var cameraManager = CameraManager.Instance();
        var camera = cameraManager == null ? null : cameraManager->GetActiveCamera();
        if (player == null || targets == null || camera == null)
            return null;

        var center = viewportPosition + viewportSize * 0.5f;
        var cone = float.IsFinite(config.InteractionCone) ? Math.Clamp(config.InteractionCone, 0.025f, 0.3f) : 0.1f;
        var radius = Math.Min(viewportSize.X, viewportSize.Y) * cone;
        var maxDistance = float.IsFinite(config.InteractionDistance) ? Math.Clamp(config.InteractionDistance, 1f, 6f) : 3.5f;
        Vector3 cameraPosition = camera->SceneCamera.Position;
        Candidate? best = null;
        var bestScore = float.MaxValue;

        foreach (var obj in Services.Objects)
        {
            if (obj == null || obj.GameObjectId == player.GameObjectId || !IsInteractiveKind(obj.ObjectKind)
                || !obj.IsValid() || !obj.IsTargetable)
                continue;

            var distance = Math.Max(0, Vector3.Distance(player.Position, obj.Position)
                - Math.Max(0, player.HitboxRadius) - Math.Max(0, obj.HitboxRadius));
            if (!float.IsFinite(distance) || distance > maxDistance)
                continue;

            var native = (NativeGameObject*)obj.Address;
            if (native == null || !native->GetIsTargetable() || !targets->IsObjectInViewRange(native))
                continue;

            var worldCenter = new NativeVector3();
            native->GetCenterPosition(&worldCenter);
            Vector3 position = worldCenter;
            if (!Services.GameGui.WorldToScreen(position, out var screenPosition))
                continue;

            var screenDistance = Vector2.Distance(center, screenPosition);
            if (!float.IsFinite(screenDistance) || screenDistance > radius)
                continue;

            // Center alignment dominates, with distance as a tie breaker. The small
            // incumbent bonus prevents two adjacent NPCs flickering back and forth.
            var score = screenDistance / radius + (distance / maxDistance) * 0.1f;
            if (candidate?.Id == obj.GameObjectId)
                score -= 0.075f;
            if (score >= bestScore || !HasActiveInteraction(native, obj.ObjectKind)
                || !HasCameraLineOfSight(cameraPosition, position, obj.HitboxRadius))
                continue;

            var name = obj.Name.TextValue;
            if (string.IsNullOrWhiteSpace(name))
                name = "Interact";
            best = new Candidate(obj.GameObjectId, obj.Address, name, screenPosition);
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

    private static bool IsInteractiveKind(ObjectKind kind) => kind is ObjectKind.EventNpc
        or ObjectKind.EventObj or ObjectKind.Treasure or ObjectKind.Aetheryte
        or ObjectKind.GatheringPoint or ObjectKind.Retainer or ObjectKind.HousingEventObject
        or ObjectKind.ReactionEventObject;

    private static bool IsUsableKey(VirtualKey key) => (int)key > 7
        && key is not (VirtualKey.SHIFT or VirtualKey.CONTROL or VirtualKey.MENU
            or VirtualKey.LSHIFT or VirtualKey.RSHIFT or VirtualKey.LCONTROL
            or VirtualKey.RCONTROL or VirtualKey.LMENU or VirtualKey.RMENU)
        && Services.Keys.IsVirtualKeyValid((int)key);

    private void Clear(string reason)
    {
        candidate = null;
        Hint = reason;
    }

    private static bool IsPhysicalKeyDown(VirtualKey key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern short GetAsyncKeyState(int virtualKey);

    private sealed record Candidate(ulong Id, nint Address, string Name, Vector2 ScreenPosition);
}
