using System.Numerics;
using System.Text.Json;
using OmniKit;
using OmniKit.Ui;

var checks = 0;
var failures = new List<string>();
var root = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? ".");
var defaults = new Configuration { Persist = _ => throw new InvalidOperationException("The synthetic harness cannot persist settings.") };
var viewport = new Vector2(120, 80);
var viewportSize = new Vector2(1280, 720);
var layout = CompanionLayout.Create(viewport, viewportSize, new(.16f, .18f, .64f, .78f), defaults.CompanionSize, 1, 1);
var originalLayout = layout;
var anchors = new[] { Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One,
    new Vector2(.5f, 0), new Vector2(.5f, 1), new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(.9f, .85f) };
foreach (var anchor in anchors)
{
    var home = CompanionHomeGeometry.Bounds(layout, viewport, viewportSize, anchor);
    Check(Fits(home.Minimum, home.Size, viewport, viewport + viewportSize), "House at " + anchor + " fits all viewport edges.");
    Check(layout == originalLayout, "Independent house placement never changes the drawn roaming rectangle.");
    var obstacle = new CompanionSurface(1, home.Minimum, home.Maximum, 0, 0);
    var motion = new CompanionMotion(19);
    var safe = CompanionObstacle.Destination(layout.Minimum, layout.SpriteSize, layout.Minimum, layout.Maximum, obstacle);
    motion.ResumeAt(safe, layout);
    var validRoam = true;
    var noOverlap = true;
    for (var tick = 0; tick < 600; tick++)
    {
        motion.Update(layout, .05f, true, obstacle);
        validRoam &= Fits(motion.Position, layout.SpriteSize, layout.Minimum, layout.Maximum);
        if (CompanionObstacle.TryBounds(obstacle, layout.SpriteSize, out var min, out var max))
            noOverlap &= !CompanionObstacle.Inside(motion.Position, min, max);
    }
    Check(validRoam, "Normal roaming remains within the original box with house " + anchor);
    Check(noOverlap, "Normal roaming routes around the house footprint at " + anchor);
    Cycle(anchor, home);
}

var house = CompanionHomeGeometry.Bounds(layout, viewport, viewportSize, new(.9f, .85f));
var start = layout.Minimum + new Vector2(20, 30);
var paused = AtState(CompanionHomeState.Approaching, outside: true);
var pausedPosition = paused.Position;
var pausedState = paused.State;
var pausedPhase = paused.SleepPhase;
for (var i = 0; i < 80; i++) Update(paused, .05f, true, true);
Check(paused.Position == pausedPosition && paused.State == pausedState && paused.SleepPhase == pausedPhase,
    "Pausing freezes position, state and phase without advancing the nap timer.");
Update(paused, float.NaN, true, false);
Update(paused, -1, true, false);
Check(paused.Position == pausedPosition && paused.State == pausedState, "Non-finite or negative elapsed time cannot advance the routine.");
var disabled = new CompanionHomeRoutine(17); disabled.Reset(15);
var disabledPosition = disabled.Position;
for (var i = 0; i < 400; i++) Update(disabled, .05f, false, false);
Check(!disabled.Active && disabled.Position == disabledPosition, "A disabled inactive routine never starts a trip.");
foreach (var state in new[] { CompanionHomeState.Approaching, CompanionHomeState.Entering, CompanionHomeState.Sleeping })
foreach (var moveHouse in new[] { false, true }) CheckCancellation(state, moveHouse);
foreach (var state in new[] { CompanionHomeState.Approaching, CompanionHomeState.Entering, CompanionHomeState.Sleeping })
    CheckCancellation(state, false, houseVisible: false);
var hidden = StartApproach();
hidden.Update(.05f, true, false, hidden.Position, hidden.FacingLeft, default, house, true, 15, 5, viewport, viewport + viewportSize);
Check(!hidden.Active, "An invisible layout cancels the cosmetic routine.");

var runner = StartApproach();
var approachStart = runner.Position;
Update(runner, .05f, true, false);
var napStep = Vector2.Distance(approachStart, runner.Position);
var normal = new CompanionMotion(17);
normal.ResumeAt(start, layout);
normal.Update(layout, .05f, true);
for (var i = 0; i < 60 && !normal.Walking; i++) normal.Update(layout, .05f, true);
var normalStart = normal.Position;
normal.Update(layout, .05f, true);
var ratio = napStep / Vector2.Distance(normalStart, normal.Position);
Check(ratio is > 1.9f and < 2.2f, "Nap approach is approximately twice the normal roaming speed.");

var artwork = new CompanionNapArtwork(Path.Combine(root, "assets"));
var atlasCount = 0;
foreach (var avatar in Configuration.AvatarNames)
foreach (var outfit in new[] { "Default" })
{
    Check(artwork.TryGet(avatar, outfit, out var sheet) && sheet != null, "Nap artwork resolves " + avatar + "/" + outfit);
    if (sheet == null) continue;
    atlasCount++;
    foreach (var size in new[] { 96f, 192f })
    foreach (var mirrored in new[] { false, true })
    foreach (var artScale in new[] { 1f, .62f })
    for (var frameIndex = 0; frameIndex < 4; frameIndex++)
    {
        var frame = sheet.Frame(frameIndex, viewport, new(size), artScale, mirrored);
        var source = sheet.Frames[frameIndex];
        var sourceSize = new Vector2(source.Bounds[2] - source.Bounds[0], source.Bounds[3] - source.Bounds[1]);
        Check(Math.Abs(frame.Size.X / frame.Size.Y - sourceSize.X / sourceSize.Y) < .00001f,
            "Every nap pose keeps its source aspect at both sizes and facings.");
        Check(Fits(frame.Position, frame.Size, viewport, viewport + new Vector2(size)), "Every padded nap pose fits its logical cell.");
        var sourceAnchor = new Vector2(source.Anchor[0] - source.Bounds[0], source.Anchor[1] - source.Bounds[1]);
        var factor = size * artScale / sheet.ScaleReference;
        if (mirrored) sourceAnchor.X = sourceSize.X - sourceAnchor.X;
        Check(Vector2.Distance(frame.Position + sourceAnchor * factor, viewport + new Vector2(.5f, .9f) * size) < .0001f,
            "A common row scale anchors every nap pose to the same baseline.");
    }
}
Check(atlasCount == 8, "All eight active companions have complete nap artwork.");

if (!args.Contains("--logic-only")) NativeRender.Run(root, Check);

if (failures.Count > 0)
{
    foreach (var failure in failures) Console.WriteLine("FAIL: " + failure);
    throw new InvalidOperationException($"{failures.Count} failures in {checks} bounded home checks.");
}
Console.WriteLine($"PASS: {checks} bounded house, roaming, nap-cycle and eight-atlas checks{(args.Contains("--logic-only") ? "" : "; 128 production nap renders, 64 complete fallback poses and four sleep-symbol edge cases")}; no game/profile/network access.");

void Cycle(Vector2 anchor, CompanionHomeRect home)
{
    var routine = new CompanionHomeRoutine(17);
    routine.Reset(15);
    var facesLeft = anchor.X >= .5f;
    var current = CompanionObstacle.Destination(layout.Minimum + new Vector2(15), layout.SpriteSize,
        layout.Minimum, layout.Maximum, new CompanionSurface(1, home.Minimum, home.Maximum, 0, 0));
    var states = new HashSet<CompanionHomeState>();
    var started = false;
    var completed = false;
    var validPositions = true;
    var largestStep = 0f;
    var leftDrawnBox = false;
    for (var tick = 0; tick < 4000; tick++)
    {
        var before = started ? routine.Position : current;
        routine.Update(.05f, true, false, current, false, layout, home, facesLeft, 15, 5, viewport, viewport + viewportSize);
        if (!routine.Active && !started) continue;
        started = true;
        states.Add(routine.State);
        validPositions &= Fits(routine.Position, layout.SpriteSize, viewport, viewport + viewportSize);
        largestStep = Math.Max(largestStep, Vector2.Distance(before, routine.Position));
        leftDrawnBox |= !Fits(routine.Position, layout.SpriteSize, layout.Minimum, layout.Maximum);
        if (!routine.Active) { completed = true; break; }
    }
    Check(started && completed && Enum.GetValues<CompanionHomeState>().All(states.Contains), "Full bounded enter/settle/sleep/wake/leave/return cycle at " + anchor);
    Check(validPositions, "Nap positions remain within the viewport at " + anchor);
    Check(largestStep <= layout.SpriteSize.Y * .13f, "No single-frame nap teleport at " + anchor + "; largest step " + largestStep);
    Check(Vector2.Distance(routine.Position, current) < .1f, "Nap trip returns to its original roaming position at " + anchor);
    if (anchor.X == 1) Check(leftDrawnBox, "Only the nap trip leaves the roaming rectangle to reach a distant home.");
}

CompanionHomeRoutine StartApproach()
{
    var routine = new CompanionHomeRoutine(17); routine.Reset(15);
    for (var i = 0; i < 400 && !routine.Active; i++) Update(routine, .05f, true, false);
    Check(routine.State == CompanionHomeState.Approaching, "Synthetic setup reaches the nap approach.");
    return routine;
}
void Update(CompanionHomeRoutine routine, float elapsed, bool enabled, bool pause) =>
    routine.Update(elapsed, enabled, pause, start, false, layout, house, true, 15, 5, viewport, viewport + viewportSize);
CompanionHomeRoutine AtState(CompanionHomeState state, bool outside = false)
{
    var routine = StartApproach();
    for (var tick = 0; tick < 3000; tick++)
    {
        if (routine.State == state && (!outside || !Fits(routine.Position, layout.SpriteSize, layout.Minimum, layout.Maximum))) return routine;
        Update(routine, .05f, true, false);
    }
    throw new InvalidOperationException("Could not reach synthetic cancellation state " + state);
}
void CheckCancellation(CompanionHomeState state, bool moveHouse, bool houseVisible = true)
{
    var routine = AtState(state, state == CompanionHomeState.Approaching);
    var updatedHouse = moveHouse ? CompanionHomeGeometry.Bounds(layout, viewport, viewportSize, new(.1f, .1f)) : house;
    var largestStep = 0f;
    var positionsFit = true;
    for (var tick = 0; tick < 3000; tick++)
    {
        var before = routine.Position;
        routine.Update(.05f, moveHouse, false, start, false, layout, updatedHouse, !moveHouse, 15, 5, viewport, viewport + viewportSize, houseVisible);
        largestStep = Math.Max(largestStep, Vector2.Distance(before, routine.Position));
        positionsFit &= Fits(routine.Position, layout.SpriteSize, viewport, viewport + viewportSize);
        if (!routine.Active) break;
    }
    Check(!routine.Active && Vector2.Distance(routine.Position, start) < .1f,
        (moveHouse ? "Moving the home" : houseVisible ? "Disabling naps" : "Hiding the house") + " during " + state + " returns continuously to the original roaming location.");
    Check(positionsFit && largestStep <= layout.SpriteSize.Y * .13f,
        "Cancellation must not teleport or escape the viewport during " + state + "; largest step " + largestStep);
}
void Check(bool passed, string message) { checks++; if (!passed) failures.Add(message); }
static bool Fits(Vector2 position, Vector2 size, Vector2 min, Vector2 max) =>
    float.IsFinite(position.X) && float.IsFinite(position.Y) && position.X >= min.X - .01f && position.Y >= min.Y - .01f &&
    position.X + size.X <= max.X + .01f && position.Y + size.Y <= max.Y + .01f;
