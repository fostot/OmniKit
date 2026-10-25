using System.Numerics;
using OmniKit.Unlocks;

var checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
var destination = new Vector3(10, 0, 0);
(AetheryteUnlockCoordinator Coordinator, FakeRuntime Runtime) Create(bool attune = true)
{
    var runtime = new FakeRuntime();
    return (new(new(1, attune ? 1u : 2u, destination, attune ? 42u : 0u), runtime, 0), runtime);
}

foreach (var invalid in new[] { new UnlockLeg(0, 1, destination, 42), new UnlockLeg(1, 0, destination),
    new UnlockLeg(1, 1, destination), new UnlockLeg(1, 2, destination, 42),
    new UnlockLeg(1, 1, new(float.NaN, 0, 0), 42) })
{
    var fake = new FakeRuntime(); var invalidCoordinator = new AetheryteUnlockCoordinator(invalid, fake, 0);
    invalidCoordinator.Update(1);
    Check(!invalidCoordinator.Busy && invalidCoordinator.Fatal && fake.Observations == 0, "invalid leg rejected before runtime access");
}

var (coordinator, runtime) = Create();
runtime.Observation = runtime.Observation with { Attuned = true };
coordinator.Update(1);
Check(coordinator.Succeeded && runtime.Starts == 0 && runtime.Interactions == 0, "existing native attunement completes without action");

(coordinator, runtime) = Create();
runtime.Observation = runtime.Observation with { InRange = true };
coordinator.Update(1);
Check(coordinator.Busy && runtime.Interactions == 1 && runtime.Starts == 0, "in-range interaction issued exactly once");
runtime.Observation = runtime.Observation with { InteractionBusy = true };
for (var tick = 2; tick < 20; tick++) coordinator.Update(tick);
Check(coordinator.Busy && !coordinator.Succeeded && runtime.Interactions == 1,
    "our interaction busy state does not advance menus or mark attunement");
runtime.Observation = runtime.Observation with { Attuned = true };
coordinator.Update(20); coordinator.Update(21);
Check(coordinator.Succeeded && !coordinator.Fatal && runtime.Interactions == 1, "native attunement bit confirms completion");

(coordinator, runtime) = Create();
coordinator.Update(1);
Check(runtime.Starts == 1 && runtime.LastDestination == destination && coordinator.Busy, "one ground move begins");
runtime.Observation = runtime.Observation with { InRange = true };
coordinator.Update(2);
Check(runtime.Stops == 1 && runtime.Interactions == 1 && coordinator.Busy, "stop owned navigation before one attunement interaction");
coordinator.Update(30_003);
Check(!coordinator.Busy && !coordinator.Succeeded && !coordinator.Fatal && runtime.Interactions == 1,
    "30-second wait cannot invent an attunement bit or repeat interaction");

(coordinator, runtime) = Create(); coordinator.Update(1);
runtime.Navigation = VnavmeshState.PathEnded;
coordinator.Update(2);
Check(!coordinator.Busy && !coordinator.Succeeded && !coordinator.Fatal && runtime.Interactions == 0,
    "path completion outside range is recoverable failure");

(coordinator, runtime) = Create(); coordinator.Update(1);
runtime.Navigation = VnavmeshState.PathEnded; runtime.Observation = runtime.Observation with { InRange = true };
coordinator.Update(2);
Check(coordinator.Busy && runtime.Interactions == 1 && !coordinator.Succeeded,
    "path completion plus range still waits for physical attunement");

(coordinator, runtime) = Create(); runtime.InteractionAccepted = false;
runtime.Observation = runtime.Observation with { InRange = true };
coordinator.Update(1); coordinator.Update(2);
Check(!coordinator.Busy && !coordinator.Succeeded && !coordinator.Fatal && runtime.Interactions == 1,
    "recycled or missing target rejection is never retried");

(coordinator, runtime) = Create(); runtime.Observation = runtime.Observation with { InteractionBusy = true };
coordinator.Update(1); coordinator.Update(15_001);
Check(!coordinator.Busy && !coordinator.Fatal && runtime.Starts == 0 && runtime.Interactions == 0,
    "busy-before-start wait is bounded to 15 seconds");

(coordinator, runtime) = Create(); runtime.Observation = runtime.Observation with { InteractionBusy = true };
coordinator.Update(1); runtime.Observation = runtime.Observation with { InteractionBusy = false };
coordinator.Update(5_000); coordinator.Update(5_001);
Check(coordinator.Busy && runtime.Starts == 1, "initial busy state may clear without repeating navigation");

foreach (var observation in new[] { new UnlockObservation(false, true, false, false, 1, Vector3.Zero, false, false),
    new UnlockObservation(true, false, false, false, 1, Vector3.Zero, false, false),
    new UnlockObservation(true, true, false, true, 1, Vector3.Zero, false, false),
    new UnlockObservation(true, true, false, false, 3, Vector3.Zero, false, false) })
{
    (coordinator, runtime) = Create(); coordinator.Update(1); runtime.Observation = observation;
    coordinator.Update(2); coordinator.Update(3);
    Check(!coordinator.Busy && coordinator.Fatal && runtime.Stops == 1 && runtime.Interactions == 0,
        "character, safety, unexpected interaction, or wrong territory terminates owned control");
}

(coordinator, runtime) = Create(); coordinator.Update(1);
runtime.Navigation = VnavmeshState.OwnershipLost;
runtime.Observation = runtime.Observation with { InRange = true, Attuned = true };
coordinator.Update(2);
Check(coordinator.Fatal && !coordinator.Succeeded && runtime.Stops == 0 && runtime.Interactions == 0,
    "foreign navigation overrides apparent arrival and is never stopped");

(coordinator, runtime) = Create(); coordinator.Update(1);
runtime.LoseOwnershipOnStop = true; runtime.Observation = runtime.Observation with { InRange = true };
coordinator.Update(2);
Check(coordinator.Fatal && runtime.Interactions == 0, "ownership loss during stop prevents interaction");

(coordinator, runtime) = Create(); coordinator.Update(1);
runtime.IgnoreStop = true; runtime.Observation = runtime.Observation with { InRange = true };
coordinator.Update(2);
Check(coordinator.Fatal && runtime.Interactions == 0, "unconfirmed navigation stop prevents interaction");

(coordinator, runtime) = Create(); runtime.Observation = runtime.Observation with { InRange = true };
coordinator.Update(1); runtime.Observation = runtime.Observation with { Attuned = true };
coordinator.Update(30_002);
Check(!coordinator.Busy && !coordinator.Succeeded && !coordinator.Fatal,
    "late attunement observation cannot extend the 30-second command window");

(coordinator, runtime) = Create(false); coordinator.Update(1);
runtime.Navigation = VnavmeshState.PathEnded; runtime.Observation = runtime.Observation with { Position = destination };
coordinator.Update(2); runtime.Observation = runtime.Observation with { Territory = 2 }; coordinator.Update(15_003);
Check(!coordinator.Busy && !coordinator.Succeeded && !coordinator.Fatal,
    "unobserved late crossing cannot extend the 15-second exit window");

(coordinator, runtime) = Create(false); coordinator.Update(1);
runtime.Navigation = VnavmeshState.PathEnded; runtime.Observation = runtime.Observation with { Position = destination };
coordinator.Update(2); runtime.Observation = runtime.Observation with { Territory = 3 }; coordinator.Update(15_003);
Check(coordinator.Fatal && !coordinator.Succeeded, "wrong-zone safety violation remains fatal after the exit deadline");

(coordinator, runtime) = Create(); runtime.Navigation = VnavmeshState.FindingPath;
coordinator.Update(1); coordinator.Cancel(); runtime.Navigation = VnavmeshState.Moving;
var pollsAfterCancel = runtime.Polls; coordinator.Update(2); coordinator.Cancel();
Check(coordinator.Fatal && !coordinator.Busy && runtime.Stops == 1 && runtime.Polls == pollsAfterCancel
    && runtime.Starts == 1 && runtime.Interactions == 0, "late path result after cancellation is ignored");

foreach (var navigation in new[] { VnavmeshState.Failed, VnavmeshState.Cancelled, VnavmeshState.Idle })
{
    (coordinator, runtime) = Create(); coordinator.Update(1); runtime.Navigation = navigation;
    coordinator.Update(2); coordinator.Update(3);
    Check(!coordinator.Busy && !coordinator.Fatal && runtime.Starts == 1 && runtime.Interactions == 0,
        "navigation failure is recoverable without queued retry");
}

(coordinator, runtime) = Create(); coordinator.Update(1); coordinator.Update(240_001);
Check(!coordinator.Busy && !coordinator.Fatal && runtime.Stops == 1, "overall 240-second leg timeout stops owned navigation");

(coordinator, runtime) = Create(false); coordinator.Update(1);
runtime.Navigation = VnavmeshState.PathEnded;
runtime.Observation = runtime.Observation with { Position = destination, Attuned = true, InRange = true };
coordinator.Update(2);
Check(coordinator.Busy && !coordinator.Succeeded && runtime.Interactions == 0,
    "standing at exit and path completion cannot prove a territory transition");
coordinator.Update(15_003);
Check(!coordinator.Busy && !coordinator.Fatal && !coordinator.Succeeded, "non-transitioning exit waits at most 15 seconds");

(coordinator, runtime) = Create(false); coordinator.Update(1);
runtime.Observation = runtime.Observation with { Loading = true, Territory = 0 };
coordinator.Update(2);
runtime.Observation = runtime.Observation with { Territory = 2 }; coordinator.Update(40_000);
Check(coordinator.Busy && !coordinator.Succeeded, "expected loading never itself confirms arrival");
runtime.Observation = runtime.Observation with { Loading = false }; coordinator.Update(40_001);
Check(coordinator.Succeeded && !coordinator.Fatal && runtime.Interactions == 0, "expected unloaded territory confirms exit arrival");

(coordinator, runtime) = Create(false); coordinator.Update(1);
runtime.Observation = runtime.Observation with { Loading = true, Territory = 0 };
coordinator.Update(2); coordinator.Update(45_003);
Check(!coordinator.Busy && !coordinator.Fatal && !coordinator.Succeeded, "loading transition wait bounded to 45 seconds");

(coordinator, runtime) = Create(false); coordinator.Update(1);
runtime.Observation = runtime.Observation with { Loading = true, Territory = 3 }; coordinator.Update(2);
Check(coordinator.Fatal && !coordinator.Succeeded, "wrong destination is fatal even while loading");

(coordinator, runtime) = Create(); runtime.NavigationAccepted = false;
coordinator.Update(1); coordinator.Update(2);
Check(!coordinator.Busy && !coordinator.Fatal && runtime.Starts == 1 && runtime.Stops == 0,
    "rejected navigation never claims ownership or retries");

(coordinator, runtime) = Create(); runtime.ThrowObserve = true; coordinator.Update(1);
Check(coordinator.Fatal && !coordinator.Busy, "runtime observation failure ends the leg safely");

(coordinator, runtime) = Create(); coordinator.Update(1); runtime.ThrowStop = true;
runtime.Observation = runtime.Observation with { InRange = true }; coordinator.Update(2);
Check(coordinator.Fatal && !coordinator.Busy && runtime.Interactions == 0, "unverifiable navigation cleanup prevents interaction");

Console.WriteLine($"PASS: {checks} finite unlock leg protocol checks; synthetic runtime only.");

sealed class FakeRuntime : IUnlockLegRuntime
{
    public UnlockObservation Observation = new(true, true, false, false, 1, Vector3.Zero, false, false);
    public VnavmeshState Navigation = VnavmeshState.Moving;
    public bool NavigationAccepted = true, InteractionAccepted = true, LoseOwnershipOnStop, ThrowObserve, ThrowStop, IgnoreStop;
    public int Starts, Stops, Interactions, Observations, Polls;
    public Vector3 LastDestination;
    public UnlockObservation Observe() { Observations++; if (ThrowObserve) throw new InvalidOperationException(); return Observation; }
    public bool StartNavigation(Vector3 point) { Starts++; LastDestination = point; return NavigationAccepted; }
    public VnavmeshState PollNavigation() { Polls++; return Navigation; }
    public void StopNavigation() { Stops++; if (ThrowStop) throw new InvalidOperationException(); if (!IgnoreStop) Navigation = LoseOwnershipOnStop ? VnavmeshState.OwnershipLost : VnavmeshState.Cancelled; }
    public bool Interact(uint id) { if (id != 42) throw new Exception("Unexpected target identity."); Interactions++; return InteractionAccepted; }
}
