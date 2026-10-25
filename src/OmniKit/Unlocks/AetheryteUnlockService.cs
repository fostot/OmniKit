using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace OmniKit.Unlocks;

/// <summary>Explicit, non-persistent attunement runs. Native work stays on Framework.Update.</summary>
internal sealed unsafe class AetheryteUnlockService : IDisposable, IUnlockLegRuntime
{
    private sealed record CatalogData(IReadOnlyList<AetheryteDestination> Destinations, IReadOnlyList<TravelConnection> Connections);
    private readonly Task<CatalogData> loading;
    private readonly VnavmeshBridge navigation = new();
    private IReadOnlyList<AetheryteDestination> destinations = [];
    private IReadOnlyList<TravelConnection> connections = [];
    private IReadOnlyList<AetheryteUnlockOption> options = [];
    private readonly HashSet<uint> initialTargets = [];
    private readonly HashSet<uint> completedTargets = [];
    private readonly HashSet<uint> remaining = [];
    private readonly Dictionary<uint, string> failedTargets = [];
    private readonly HashSet<TravelConnection> failedConnections = [];
    private readonly Queue<(UnlockLeg Leg, TravelConnection? Connection)> route = [];
    private AetheryteUnlockCoordinator? legController;
    private UnlockLeg? leg;
    private TravelConnection? currentConnection;
    private AetheryteUnlockOption? currentTarget;
    private long nextScan;
    private long nextRunUpdate;
    private long meshWaitStarted;
    private ulong contentId;
    private uint requestedScope;
    private bool startRequested;
    private bool stopRequested;
    private bool running;
    private bool loaded;
    private bool disposed;
    private bool inputBlocked;
    private bool navigationRefused;
    private bool legIssuedNavigation;
    public bool Busy => running || startRequested;
    public uint CurrentTerritory { get; private set; }
    public string Status { get; private set; } = "Loading aetherytes and shards.";
    public (int Done, int Total) Progress => (completedTargets.Count, initialTargets.Count);
    public IReadOnlyList<AetheryteUnlockOption> GetOptions() => options;

    public AetheryteUnlockService()
    {
        // Only public sheet/layout data is read in this task; character flags remain on the framework thread.
        loading = Task.Run(() => new CatalogData(AetheryteCatalog.Read(Services.Data), AetheryteCatalog.ReadConnections(Services.Data)));
        _ = loading.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public void Start(uint scope = 0)
    {
        if (disposed || Busy) return;
        requestedScope = scope; startRequested = true;
    }
    public void Stop() => stopRequested = true;

    public void Update(bool blocksActions)
    {
        if (disposed || !Services.Framework.IsInFrameworkUpdateThread) return;
        inputBlocked = blocksActions;
        CurrentTerritory = Services.ClientState.TerritoryType;
        try
        {
            if (stopRequested || Busy && ManualMovement())
            { stopRequested = false; End("Route stopped. No further movement or interaction is queued."); }
            if (!loaded)
            {
                if (!loading.IsCompleted) return;
                if (!loading.IsCompletedSuccessfully)
                { startRequested = false; Status = "Location data could not be loaded."; return; }
                destinations = loading.Result.Destinations; connections = loading.Result.Connections; loaded = true;
                Status = "Choose an area. Enable vnavmesh to start an unlock route.";
            }
            var now = Environment.TickCount64;
            if (now >= nextScan || startRequested)
            { RefreshSnapshot(); nextScan = now + 1000; }
            if (startRequested)
            { startRequested = false; BeginRun(); }
            if (!running) return;
            if (now < nextRunUpdate) return;
            nextRunUpdate = now + 100;
            if (legController == null)
            {
                if (!SameCharacter() || !Safe()) { End("Route stopped while the character or game state changed."); return; }
                if (!navigation.DependencyAvailable) { End("vnavmesh became unavailable; the route stopped."); return; }
                if (Loading() || !LoggedIn() || !navigation.IsReady)
                {
                    if (meshWaitStarted == 0) meshWaitStarted = now;
                    if (now - meshWaitStarted > 60_000) End("The area's navigation mesh did not become ready in time.");
                    else Status = "Waiting for the area's navigation mesh.";
                    return;
                }
                meshWaitStarted = 0;
                if (!navigation.IsIdle) { End(navigation.Status); return; }
                if (route.Count == 0 && !PlanNext()) return;
                var next = route.Dequeue(); leg = next.Leg; currentConnection = next.Connection;
                navigationRefused = false; legIssuedNavigation = false;
                legController = new(leg, this, now);
            }
            legController.Update(now);
            Status = (currentTarget == null ? "" : currentTarget.Name + ": ") + legController.Status;
            if (legController.Busy) return;
            if (legController.Fatal || navigationRefused || legIssuedNavigation && navigation.State == VnavmeshState.OwnershipLost)
            { End(navigationRefused ? navigation.Status : Status); return; }
            if (!legController.Succeeded)
            {
                if (currentTarget != null) failedTargets[currentTarget.Id] = legController.Status;
                if (currentConnection != null) failedConnections.Add(currentConnection);
                route.Clear();
            }
            else if (leg?.AetheryteId > 0)
            {
                RefreshSnapshot();
                if (options.Any(option => option.Id == leg.AetheryteId && option.Unlocked)) remaining.Remove(leg.AetheryteId);
                else { failedTargets[leg.AetheryteId] = "Attunement could not be confirmed."; route.Clear(); }
            }
            legController = null; leg = null; currentConnection = null;
        }
        catch (Exception exception)
        {
            Services.Log.Warning(exception, "Aetheryte route verification failed.");
            End("Route stopped because its state could not be verified.");
        }
    }

    private void BeginRun()
    {
        if (!LoggedIn() || !Safe()) { Status = "Finish the current action and return to the game before starting."; return; }
        if (!navigation.DependencyAvailable) { Status = "Install and enable vnavmesh before starting an unlock route."; return; }
        if (!navigation.IsReady) { Status = "Wait for vnavmesh to load this area's navigation mesh."; return; }
        if (navigation.IsRunning) { Status = "Stop other navigation before starting this route."; return; }
        initialTargets.Clear(); completedTargets.Clear(); remaining.Clear(); failedTargets.Clear(); failedConnections.Clear(); route.Clear();
        RefreshSnapshot();
        meshWaitStarted = 0; nextRunUpdate = 0;
        foreach (var option in options.Where(option => option.Eligible && !option.Unlocked && !option.IsVirtual
                     && (requestedScope == 0 || option.TerritoryId == requestedScope)))
        { initialTargets.Add(option.Id); remaining.Add(option.Id); }
        if (remaining.Count == 0) { Status = "No eligible unattuned aetherytes or shards in this area. Check any blocked entries."; return; }
        contentId = PlayerState.Instance()->ContentId;
        running = true; Status = "Planning the closest supported unlock route.";
    }

    private bool PlanNext()
    {
        RefreshSnapshot();
        foreach (var option in options.Where(option => option.Unlocked)) remaining.Remove(option.Id);
        var pending = options.Where(option => remaining.Contains(option.Id) && !failedTargets.ContainsKey(option.Id)).ToArray();
        if (remaining.Count == 0) { End($"Route complete: {Progress.Done} attunements confirmed."); return false; }
        var plan = UnlockRoutePlanner.SelectNext(pending, CurrentTerritory, Services.Objects.LocalPlayer!.Position,
            connections.Where(connection => !failedConnections.Contains(connection)).ToArray());
        if (plan.Target == null)
        {
            End($"{Progress.Done}/{Progress.Total} confirmed. " + (failedTargets.Count > 0
                ? "Some paths failed; review the blocked entries. " : "") + plan.Reason);
            return false;
        }
        currentTarget = plan.Target;
        foreach (var connection in plan.Legs)
            route.Enqueue((new(connection.FromTerritory, connection.ToTerritory, connection.FromPoint), connection));
        route.Enqueue((new(plan.Target.TerritoryId, plan.Target.TerritoryId, plan.Target.Position!.Value, plan.Target.Id), null));
        return true;
    }

    private void RefreshSnapshot()
    {
        if (running && Loading()) return;
        if (!LoggedIn())
        {
            options = destinations.Select(destination => new AetheryteUnlockOption(destination.Id, destination.Name,
                destination.Territory, false, false, "Log in to check attunement.", destination.TerritoryId,
                destination.Position, destination.IsAetheryte, destination.RequiredQuestId, destination.IsVirtual)).ToArray();
            return;
        }
        var actual = new Dictionary<uint, Vector3>();
        foreach (var obj in Services.Objects)
            if (obj != null && obj.IsValid() && obj.ObjectKind == ObjectKind.Aetheryte && AetheryteCatalog.Finite(obj.Position))
                actual[obj.BaseId] = obj.Position;
        var located = destinations.Select(destination => destination.TerritoryId == CurrentTerritory
            && actual.TryGetValue(destination.Id, out var position) ? destination with { Position = position } : destination);
        options = AetheryteCatalog.Snapshot(located, id => UIState.Instance()->IsAetheryteUnlocked(id), QuestManager.IsQuestComplete)
            .Select(option => failedTargets.TryGetValue(option.Id, out var failure) && !option.Unlocked
                ? option with { Eligible = false, Detail = failure } : option).ToArray();
        if (running && SameCharacter())
            foreach (var option in options.Where(option => option.Unlocked && initialTargets.Contains(option.Id))) completedTargets.Add(option.Id);
    }

    private bool LoggedIn() => Services.ClientState.IsLoggedIn && Services.Objects.LocalPlayer is { } player && player.IsValid()
        && PlayerState.Instance() != null && PlayerState.Instance()->IsLoaded && PlayerState.Instance()->ContentId != 0 && UIState.Instance() != null;
    private bool Loading() => Services.Conditions[ConditionFlag.BetweenAreas] || Services.Conditions[ConditionFlag.BetweenAreas51]
        || leg is { AetheryteId: 0 } && legIssuedNavigation && Services.ClientState.IsLoggedIn && !LoggedIn();
    private bool SameCharacter() => Services.ClientState.IsLoggedIn && PlayerState.Instance() != null
        && (PlayerState.Instance()->ContentId == contentId || Loading() && PlayerState.Instance()->ContentId == 0);
    private bool Safe()
    {
        if (inputBlocked || !Services.ClientState.IsLoggedIn || Services.ClientState.IsGPosing) return false;
        foreach (var flag in DangerFlags) if (Services.Conditions[flag]) return false;
        var framework = NativeFramework.Instance(); var ui = RaptureAtkModule.Instance();
        return framework != null && !framework->WindowInactive && !framework->IsExiting
            && (Loading() || ui != null && !ui->IsTextInputActive());
    }
    private static readonly ConditionFlag[] DangerFlags = [ConditionFlag.Unconscious, ConditionFlag.InCombat,
        ConditionFlag.Crafting, ConditionFlag.PreparingToCraft, ConditionFlag.ExecutingCraftingAction,
        ConditionFlag.Gathering, ConditionFlag.ExecutingGatheringAction, ConditionFlag.Mounted, ConditionFlag.Mounting,
        ConditionFlag.Mounting71, ConditionFlag.InFlight, ConditionFlag.RidingPillion];
    private static readonly ConditionFlag[] InteractionFlags = [ConditionFlag.Casting, ConditionFlag.Casting87,
        ConditionFlag.Occupied, ConditionFlag.Occupied30, ConditionFlag.Occupied33, ConditionFlag.Occupied38,
        ConditionFlag.Occupied39, ConditionFlag.OccupiedInEvent, ConditionFlag.OccupiedInQuestEvent,
        ConditionFlag.OccupiedInCutSceneEvent, ConditionFlag.WatchingCutscene, ConditionFlag.WatchingCutscene78];
    private static readonly VirtualKey[] ManualKeys = [VirtualKey.ESCAPE, VirtualKey.W, VirtualKey.A, VirtualKey.S, VirtualKey.D, VirtualKey.SPACE];
    private static bool ManualMovement() => ManualKeys.Any(key => Services.Keys[key]);

    UnlockObservation IUnlockLegRuntime.Observe()
    {
        var player = Services.Objects.LocalPlayer;
        var target = leg?.AetheryteId > 0 ? FindTarget(leg.AetheryteId) : null;
        var attuned = leg?.AetheryteId > 0 && LoggedIn() && UIState.Instance()->IsAetheryteUnlocked(leg.AetheryteId);
        return new(SameCharacter(), Safe() && (Loading() || player != null && player.IsValid()), Loading(),
            InteractionFlags.Any(flag => Services.Conditions[flag]), CurrentTerritory, player?.Position ?? Vector3.Zero,
            attuned, player != null && target != null && InRange(player, target));
    }
    private IGameObject? FindTarget(uint id) => Services.Objects.FirstOrDefault(obj => obj != null && obj.IsValid()
        && obj.ObjectKind == ObjectKind.Aetheryte && obj.BaseId == id && obj.IsTargetable);
    private static bool InRange(IGameObject player, IGameObject target) => Math.Abs(player.Position.Y - target.Position.Y) <= 6
        && Vector3.Distance(player.Position, target.Position) - Math.Clamp(target.HitboxRadius, 0, 5) <= 2.5f;

    bool IUnlockLegRuntime.StartNavigation(Vector3 destination)
    {
        if (!LoggedIn() || !Safe() || leg == null) return false;
        var player = Services.Objects.LocalPlayer!;
        var aim = destination;
        if (leg.AetheryteId != 0)
        {
            var target = FindTarget(leg.AetheryteId);
            var center = target?.Position ?? destination;
            var direction = player.Position - center; direction.Y = 0;
            direction = direction.LengthSquared() > .01f ? Vector3.Normalize(direction) : Vector3.UnitX;
            aim = center + direction * Math.Clamp((target?.HitboxRadius ?? 1) + 1.8f, 2, 5.5f);
        }
        var floor = navigation.PointOnFloor(aim);
        if (floor is not { } point || Vector2.Distance(new(aim.X, aim.Z), new(point.X, point.Z)) > 5) return false;
        var started = navigation.TryBeginPath(player.Position, point, CurrentTerritory);
        navigationRefused = navigation.BlockedByOtherNavigation; legIssuedNavigation = started;
        return started;
    }
    VnavmeshState IUnlockLegRuntime.PollNavigation() => navigation.Poll(CurrentTerritory);
    void IUnlockLegRuntime.StopNavigation() => navigation.Stop();
    bool IUnlockLegRuntime.Interact(uint id)
    {
        if (!LoggedIn() || !Safe() || leg?.AetheryteId != id || CurrentTerritory != leg.FromTerritory
            || UIState.Instance()->IsAetheryteUnlocked(id)) return false;
        if (!navigation.IsIdle) { navigationRefused = true; return false; }
        var target = FindTarget(id); var player = Services.Objects.LocalPlayer!;
        var system = TargetSystem.Instance();
        if (target == null || system == null || !InRange(player, target)) return false;
        var expectedId = target.GameObjectId; var expectedAddress = target.Address;
        var native = (NativeGameObject*)expectedAddress;
        if (!native->GetIsTargetable()) return false;
        var current = Services.Targets.Target;
        if (!(current != null && current.IsValid() && current.GameObjectId == expectedId && current.Address == expectedAddress)
            && !system->SetHardTarget(native)) return false;
        var confirmed = Services.Targets.Target;
        if (confirmed == null || !confirmed.IsValid() || confirmed.GameObjectId != expectedId || confirmed.Address != expectedAddress
            || confirmed.ObjectKind != ObjectKind.Aetheryte || confirmed.BaseId != id) return false;
        system->InteractWithObject(native, true);
        return true;
    }

    private void End(string message)
    {
        startRequested = false; running = false; legController?.Cancel();
        navigation.Stop(); legController = null; leg = null; currentConnection = null; route.Clear();
        Status = message;
    }
    public void Dispose() { disposed = true; End("Unlock route stopped."); navigation.Dispose(); }
}
