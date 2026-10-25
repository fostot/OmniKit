using System.Numerics;

namespace OmniKit.Unlocks;

internal sealed record UnlockLeg(uint FromTerritory, uint ToTerritory, Vector3 Destination, uint AetheryteId = 0);

internal readonly record struct UnlockObservation(bool SameCharacter, bool Safe, bool Loading,
    bool InteractionBusy, uint Territory, Vector3 Position, bool Attuned, bool InRange);

internal interface IUnlockLegRuntime
{
    UnlockObservation Observe();
    bool StartNavigation(Vector3 destination);
    VnavmeshState PollNavigation();
    void StopNavigation();
    bool Interact(uint aetheryteId);
}

/// <summary>One finite walking or attunement leg. Commands never establish arrival or attunement.</summary>
internal sealed class AetheryteUnlockCoordinator
{
    private enum Stage { Ready, Navigating, AwaitZone, AwaitAttunement, Done }
    private readonly UnlockLeg leg;
    private readonly IUnlockLegRuntime runtime;
    private readonly long deadline;
    private readonly long busyDeadline;
    private long stageDeadline;
    private long? loadingStarted;
    private Stage stage;
    private bool ownsNavigation;
    public bool Busy => stage != Stage.Done;
    public bool Succeeded { get; private set; }
    public bool Fatal { get; private set; }
    public string Status { get; private set; } = "Preparing the route leg.";

    public AetheryteUnlockCoordinator(UnlockLeg leg, IUnlockLegRuntime runtime, long now)
    {
        this.leg = leg; this.runtime = runtime;
        deadline = now + 240_000; busyDeadline = now + 15_000;
        if (leg.FromTerritory == 0 || leg.ToTerritory == 0 || !Finite(leg.Destination)
            || (leg.AetheryteId == 0 ? leg.FromTerritory == leg.ToTerritory : leg.FromTerritory != leg.ToTerritory))
            Finish(false, true, "The route leg has invalid territory or coordinate data.");
    }

    public void Update(long now)
    {
        if (!Busy) return;
        try { UpdateCore(now); }
        catch { Finish(false, true, "The route state could not be verified; automatic control ended."); }
    }

    private void UpdateCore(long now)
    {
        var observation = runtime.Observe();
        if (!observation.SameCharacter)
        { Finish(false, true, "The character changed; the route was cancelled."); return; }
        if (!observation.Safe)
        { Finish(false, true, "The game became unsafe for this route; automatic control ended."); return; }
        if (observation.Territory != leg.FromTerritory && observation.Territory != leg.ToTerritory
            && !(observation.Loading && observation.Territory == 0))
        { Finish(false, true, "The character entered an unexpected territory."); return; }
        if (now > deadline)
        { Finish(false, false, "The route leg exceeded its time limit."); return; }
        if (stage == Stage.AwaitAttunement && now > stageDeadline)
        { Finish(false, false, "The game did not confirm attunement. Check this destination manually."); return; }
        if (stage == Stage.AwaitZone && now > stageDeadline && loadingStarted == null)
        { Finish(false, false, "The walking exit did not change territory in time."); return; }
        if (observation.Loading)
        {
            loadingStarted ??= now;
            if (now - loadingStarted.Value > 45_000)
                Finish(false, false, "The expected area transition did not finish in time.");
            else Status = "Waiting for the area to finish loading.";
            return;
        }
        loadingStarted = null;
        if (!Finite(observation.Position))
        { Finish(false, true, "The player position is unavailable."); return; }
        if (stage == Stage.Navigating && observation.InteractionBusy)
        { Finish(false, true, "Another interaction interrupted movement; the route was cancelled."); return; }
        var navigation = stage == Stage.Navigating ? runtime.PollNavigation() : VnavmeshState.Idle;
        if (navigation == VnavmeshState.OwnershipLost)
        {
            ownsNavigation = false;
            Finish(false, true, "Navigation changed outside this route; automatic control ended.");
            return;
        }
        if (leg.AetheryteId == 0 && observation.Territory == leg.ToTerritory)
        { Finish(true, false, "The expected territory was reached."); return; }
        if (leg.AetheryteId != 0 && observation.Attuned)
        { Finish(true, false, "Attunement was confirmed by the game."); return; }

        switch (stage)
        {
            case Stage.Ready:
                if (observation.InteractionBusy)
                {
                    if (now > busyDeadline) Finish(false, false, "The game remained busy before this route could start.");
                    else Status = "Waiting for the current interaction to end.";
                    return;
                }
                if (leg.AetheryteId != 0 && observation.InRange)
                { BeginAttunement(now); return; }
                if (!runtime.StartNavigation(leg.Destination))
                { Finish(false, false, "A ground path could not be started for this destination."); return; }
                ownsNavigation = true; stage = Stage.Navigating;
                Status = "Following the ground route.";
                return;
            case Stage.Navigating:
                if (leg.AetheryteId != 0 && observation.InRange)
                {
                    StopOwnedNavigation();
                    var stopped = runtime.PollNavigation();
                    if (stopped is not (VnavmeshState.Cancelled or VnavmeshState.PathEnded or VnavmeshState.Idle))
                    { Finish(false, true, "Navigation cleanup could not be verified before interaction."); return; }
                    BeginAttunement(now);
                    return;
                }
                if (navigation is VnavmeshState.FindingPath or VnavmeshState.Moving) return;
                if (navigation != VnavmeshState.PathEnded)
                { Finish(false, false, "Navigation stopped before this destination was reached."); return; }
                ownsNavigation = false;
                if (leg.AetheryteId != 0)
                { Finish(false, false, "The path ended outside attunement range."); return; }
                if (Vector3.Distance(observation.Position, leg.Destination) > 2)
                { Finish(false, false, "The path ended before the walking exit was reached."); return; }
                stage = Stage.AwaitZone; stageDeadline = now + 15_000;
                Status = "Waiting for the walking exit to change territory.";
                return;
            case Stage.AwaitZone:
                if (observation.InteractionBusy)
                { Finish(false, true, "Another interaction interrupted the walking exit."); return; }
                if (now > stageDeadline)
                    Finish(false, false, "The walking exit did not change territory. This connection was not completed.");
                return;
            case Stage.AwaitAttunement:
                // Occupied/event flags from our one interaction are expected here. No menus are advanced.
                if (now > stageDeadline)
                    Finish(false, false, "The game did not confirm attunement. Check this destination manually.");
                return;
        }
    }

    private void BeginAttunement(long now)
    {
        // Transition before dispatch, so an exception or rejected interaction cannot cause a second attempt.
        stage = Stage.AwaitAttunement; stageDeadline = now + 30_000;
        if (!runtime.Interact(leg.AetheryteId))
        { Finish(false, false, "The matching aetheryte or shard was unavailable for interaction."); return; }
        Status = "Waiting for the game to confirm attunement.";
    }

    private void StopOwnedNavigation()
    {
        if (!ownsNavigation) return;
        ownsNavigation = false;
        runtime.StopNavigation(); // The adapter rechecks the waypoint suffix; the provider has no caller ownership token.
    }

    private void Finish(bool succeeded, bool fatal, string status)
    {
        stage = Stage.Done; Succeeded = succeeded; Fatal = fatal; Status = status;
        try { StopOwnedNavigation(); }
        catch { Succeeded = false; Fatal = true; Status = "Owned navigation cleanup could not be verified."; }
    }

    public void Cancel()
    {
        if (Busy) Finish(false, true, "The route was cancelled; no further action is queued.");
    }

    private static bool Finite(Vector3 point) => float.IsFinite(point.X)
        && float.IsFinite(point.Y) && float.IsFinite(point.Z);
}
