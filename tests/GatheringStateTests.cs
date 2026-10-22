// Run: dotnet run --project tests/GatheringStateTests.csproj -c Release
using OmniKit.Interaction;

var checks = 0;
void Check(bool condition, string reason)
{
    checks++;
    if (!condition) throw new Exception(reason);
}
GatheringTransition New(bool restore = true) => new(1, 21, 2, 16, 0, restore);
GatheringObservation Target() => new(2, 16, 123, true, false, false, true, false, true, true, true);
void Ready(GatheringTransition state)
{
    Check(state.Advance(Target(), 100) == GatheringDecision.None, "Must settle gear before interacting.");
    Check(state.Advance(Target(), 351) == GatheringDecision.Interact, "Stable equip allows one interaction.");
}
void Gathered(GatheringTransition state)
{
    Ready(state);
    Check(state.Advance(Target() with { Gathering = true, CanAct = false }, 500) == GatheringDecision.None,
        "Gathering observation must not perform an action.");
    Check(state.Advance(Target(), 1000) == GatheringDecision.None, "Gathering end needs a quiet interval.");
}

var complete = New();
Gathered(complete);
Check(complete.Advance(Target(), 1501) == GatheringDecision.Restore, "Restore after an observed gathering session.");
Check(complete.Advance(Target(), 1600) == GatheringDecision.None, "Never retry a pending restore.");
Check(complete.Advance(Target() with { Gearset = 1, Job = 21, OriginalEquipmentMatches = true }, 1700)
    == GatheringDecision.Complete && complete.Restored, "Verify actual original equipment before reporting restored.");
Check(complete.Advance(Target(), 1800) == GatheringDecision.None, "Completed session must be inert.");

var noStart = New();
Ready(noStart);
Check(noStart.Advance(Target(), 1000) == GatheringDecision.None, "Do not restore before gathering starts.");
Check(noStart.Advance(Target(), 8400) == GatheringDecision.Cancel, "Failed gathering start expires without restoring.");

var manualGear = New();
Ready(manualGear);
Check(manualGear.Advance(Target() with { Equipment = 999 }, 400) == GatheringDecision.Cancel,
    "A manual equipment edit must cancel restoration.");
var manualJob = New();
Check(manualJob.Advance(Target() with { Gearset = 3, Job = 20 }, 100) == GatheringDecision.Cancel,
    "An unrelated gearset/job must cancel a pending switch.");

var disappeared = New();
Check(disappeared.Advance(Target() with { TargetExists = false }, 100) == GatheringDecision.Cancel,
    "A vanished or recycled target must cancel delayed interaction.");
var interrupted = New();
Check(interrupted.Advance(Target() with { Interrupted = true }, 100) == GatheringDecision.Cancel,
    "A changed saved set or invalid context must cancel.");

var noRestore = New(false);
Gathered(noRestore);
Check(noRestore.Advance(Target(), 1501) == GatheringDecision.Complete && !noRestore.Restored,
    "Unrestorable original gear must remain untouched.");
var restoreDisabled = New();
Gathered(restoreDisabled);
Check(restoreDisabled.Advance(Target() with { RestoreEnabled = false }, 1501) == GatheringDecision.Complete,
    "Disabling restoration mid-session must be honored.");

var originalChanged = New();
Gathered(originalChanged);
Check(originalChanged.Advance(Target() with { OriginalSetUnchanged = false }, 1501) == GatheringDecision.Cancel,
    "Editing the original saved set must cancel restoration.");
var blocked = New();
Check(blocked.Advance(Target() with { CanAct = false }, 100) == GatheringDecision.None, "Blocked initial equip is inert.");
Check(blocked.Advance(Target() with { CanAct = false }, 400) == GatheringDecision.None, "Typing/UI must block interaction.");
Check(blocked.Advance(Target(), 500) == GatheringDecision.Interact, "An eligible pending request may finish once unblocked.");
Check(blocked.Advance(Target(), 600) == GatheringDecision.None, "A held request cannot repeat.");

var busyRestore = New();
Gathered(busyRestore);
Check(busyRestore.Advance(Target() with { CanAct = false }, 1501) == GatheringDecision.None,
    "Do not restore while the UI is busy.");
Check(busyRestore.Advance(Target() with { CanAct = false }, 6001) == GatheringDecision.Cancel,
    "A long busy state expires instead of surprising the user later.");
var mismatch = New();
Check(mismatch.Advance(Target() with { TargetEquipmentMatches = false }, 100) == GatheringDecision.None,
    "Correct job alone is insufficient without the expected equipment.");
Check(mismatch.Advance(Target() with { TargetEquipmentMatches = false }, 5001) == GatheringDecision.Cancel,
    "Failed equip times out without interaction or retries.");

var settling = New();
Check(settling.Advance(Target(), 100) == GatheringDecision.None, "Begin stable observation.");
Check(settling.Advance(Target() with { Equipment = 124 }, 300) == GatheringDecision.None,
    "An equipment change resets the settling interval.");
Check(settling.Advance(Target() with { Equipment = 124 }, 400) == GatheringDecision.None,
    "Do not interact while equipment is still settling.");
Check(settling.Advance(Target() with { Equipment = 124 }, 551) == GatheringDecision.Interact,
    "Use the final stable equipment snapshot.");

var restoreManual = New();
Gathered(restoreManual);
Check(restoreManual.Advance(Target(), 1501) == GatheringDecision.Restore, "Begin one restoration.");
Check(restoreManual.Advance(Target() with { Gearset = 7, Job = 5 }, 1600) == GatheringDecision.Cancel,
    "User choice during restoration must cancel further automatic work.");
Check(restoreManual.Advance(Target(), 1700) == GatheringDecision.None,
    "Cancellation must remain inert when the user switches back.");

var briefGatherGap = New();
Gathered(briefGatherGap);
Check(briefGatherGap.Advance(Target() with { Gathering = true, CanAct = false }, 1200) == GatheringDecision.None,
    "A short gathering-flag gap must not end the session.");
Check(briefGatherGap.Advance(Target(), 1400) == GatheringDecision.None, "Restart the end debounce after gathering resumes.");
Check(briefGatherGap.Advance(Target(), 1700) == GatheringDecision.None,
    "Do not count the earlier gap toward restoration.");

Console.WriteLine($"Gathering state: {checks} checks passed.");
