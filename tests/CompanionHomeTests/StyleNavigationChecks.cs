using System.Numerics;
using OmniKit.Ui;

internal static class StyleNavigationChecks
{
    internal static void Run(string root, Action<bool, string> check)
    {
        var library = new CompanionHomeStyleLibrary(Path.Combine(root, "assets"));
        foreach (var id in CompanionHomeStyleLibrary.HomeIds.Where(id => id != "Random"))
        foreach (var size in new[] { 96f, 192f })
        foreach (var mirrored in new[] { false, true })
        {
            var viewport = new Vector2(size * 8, size * 5);
            var layout = new CompanionLayout(new(10), new(viewport.X * .45f, viewport.Y - 10), new(size));
            var home = new CompanionHomeRect(new(size * 4, size), new(size * 5.3f, size * 2.3f));
            var style = library.GetHome(id).Place(home.Minimum, home.Maximum, mirrored);
            var routine = new CompanionHomeRoutine(17); routine.Reset(15);
            var start = layout.Minimum + new Vector2(20);
            var started = false; var completed = false; var aligned = true;
            var delegated = 0; var wronglyDelegated = false; var sawForecourt = false; var sawFallPose = false;
            var states = new HashSet<CompanionHomeState>();
            for (var tick = 0; tick < 4000; tick++)
            {
                var beforeUsesNapArt = routine.UsesNapArt;
                routine.Update(.05f, true, false, start, false, layout, home, mirrored, 15, 5, Vector2.Zero, viewport,
                    styleFrame: style, navigator: Navigate);
                if (!routine.Active && !started) continue;
                started = true; states.Add(routine.State);
                if (routine.UsesNapArt && routine.State != CompanionHomeState.Leaving)
                    aligned &= Math.Abs(routine.Position.X - style.BedPosition(layout.SpriteSize).X) < .01f;
                if (routine.State == CompanionHomeState.Leaving && routine.ArtScale > CompanionHomeRoutine.IndoorScale + .0001f)
                    check(routine.InFrontOfHome && routine.AccessoriesVisible,
                        "Growth and restored accessories paint in front of the house: " + id);
                if (routine.State == CompanionHomeState.Entering)
                {
                    check(!routine.AccessoriesVisible, "Accessories hide when the visible doorway entry starts.");
                    if (!routine.InFrontOfHome)
                        check(routine.ArtScale <= CompanionHomeRoutine.IndoorScale + .0001f
                            && routine.Position.Y + size * .9f <= style.Threshold.Y + .001f,
                            "A visitor passes behind the arch only after it fits and crosses the painted doorstep: " + id);
                    if (routine.ArtScale > CompanionHomeRoutine.IndoorScale + .0001f)
                        check(routine.InFrontOfHome && Vector2.Distance(routine.Position, style.OutsidePosition(new(size), grounded: true)) < .01f,
                            "Shrinking stays outside in the foreground, without cutting the visitor through a side wall: " + id);
                }
                if (routine.State is CompanionHomeState.Settling or CompanionHomeState.Sleeping or CompanionHomeState.Waking)
                    check(!routine.AccessoriesVisible && !routine.InFrontOfHome, "Styled indoor poses hide accessories and retain the wall mask.");
                if (!routine.Active) { completed = true; break; }

                CompanionHomeTravel Navigate(Vector2 current, Vector2 destination, float dt, bool forecourt)
                {
                    delegated++; wronglyDelegated |= beforeUsesNapArt && routine.State != CompanionHomeState.Leaving; sawForecourt |= forecourt;
                    var delta = destination - current; var distance = delta.Length();
                    var next = distance <= size * .74f * dt ? destination : current + delta / distance * (size * .74f * dt);
                    // One deliberately reported fall proves the pose travels through the public boundary.
                    var pose = delegated == 1 ? CompanionPose.Fall : CompanionPose.Walk;
                    if (delegated == 2) sawFallPose = routine.TravelPose == CompanionPose.Fall;
                    return new(next, delta.X < 0, pose, false);
                }
            }
            check(started && completed && Enum.GetValues<CompanionHomeState>().All(states.Contains), "Styled navigation reaches all nap states: " + id);
            check(delegated > 0 && sawForecourt && !wronglyDelegated, "Travel callback owns approach/forecourt/return, never doorway/sleep: " + id);
            check(sawFallPose, "Travel pose reports the navigator's fall state.");
            check(aligned, "Measured bed and threshold keep entry/exit vertically aligned: " + id);
            // The fake callback intentionally has no obstacle routing. Real obstacle behavior belongs to the physics tests.
            check(Vector2.Distance(routine.Position, start) < .1f, "Styled routine returns to the caller's original point: " + id);
        }

        foreach (var id in CompanionHomeStyleLibrary.HomeIds.Where(id => id != "Random"))
        foreach (var companionEdge in new[] { 96f, 192f })
        foreach (var homeEdge in new[] { 48f, 384f })
        {
            var independentLayout = new CompanionLayout(Vector2.Zero, new(1500, 1000), new(companionEdge));
            var independentHome = CompanionHomeGeometry.Bounds(independentLayout, Vector2.Zero, new(1500, 1000), new(.65f, .6f), homeEdge);
            check(Vector2.Distance(independentHome.Size, new(homeEdge)) < .001f, "Explicit home size is independent of companion size.");
            var frame = library.GetHome(id).Place(independentHome.Minimum, independentHome.Maximum, false);
            var visitor = Sample(independentLayout, independentHome, frame, CompanionHomeState.Entering, 0);
            var thresholdPosition = visitor.Position;
            for (var i = 0; i < 15; i++) visitor.Update(.05f, true, false, visitor.Position, false,
                independentLayout, independentHome, false, 15, 5, Vector2.Zero, independentLayout.Maximum, styleFrame: frame);
            check(visitor.Position == thresholdPosition && visitor.ArtScale <= CompanionHomeRoutine.IndoorScale + .001f,
                "Full shrinking completes outside before turning or crossing.");
            var aperture = frame.OpeningMaximum - frame.OpeningMinimum;
            check(visitor.ArtScale * companionEdge <= aperture.X * .78f + .001f
                && visitor.ArtScale * companionEdge <= aperture.Y * .75f + .001f,
                "Even a large companion fits the independently smallest measured opening.");
            var oldSupport = visitor.Position + independentLayout.SpriteSize * new Vector2(.5f, .9f);
            var resized = independentLayout with { SpriteSize = independentLayout.SpriteSize * .75f };
            visitor.Update(.01f, true, true, visitor.Position, false, resized, independentHome, false, 15, 5,
                Vector2.Zero, independentLayout.Maximum, styleFrame: frame);
            visitor.Update(.01f, true, false, visitor.Position, false, resized, independentHome, false, 15, 5,
                Vector2.Zero, independentLayout.Maximum, styleFrame: frame);
            check(visitor.State == CompanionHomeState.Leaving && visitor.InFrontOfHome,
                "Resize after a paused shrink cancels into the safe foreground exit.");
            check(Vector2.Distance(oldSupport, visitor.Position + resized.SpriteSize * new Vector2(.5f, .9f)) < companionEdge * .01f,
                "Resize preserves the drawn support point before safe exit.");
        }

        var box = new CompanionLayout(new(20), new(700, 600), new(96));
        var bounds = new CompanionHomeRect(new(450, 150), new(575, 275));
        var fitted = library.GetHome("LanternHut").Place(bounds.Minimum, bounds.Maximum, false);
        var initial = new Vector2(40, 40);
        var actual = initial + new Vector2(3, 7);
        var blocked = Approach((_, _, _, _) => new(actual, true, CompanionPose.Fall, true));
        check(!blocked.Active && blocked.Position == actual && blocked.TravelPose == CompanionPose.Fall,
            "Blocked navigation ends the nap without teleporting away from its actual finite result.");
        var invalid = Approach((_, _, _, _) => new(new(float.NaN, 0), false, CompanionPose.Walk, false));
        check(!invalid.Active && invalid.Position == initial, "Non-finite navigation output keeps the last actual position.");
        var stalled = Approach((p, _, _, _) => new(p, false, CompanionPose.Idle, false));
        var wasActive = stalled.Active;
        for (var i = 0; i < 200 && stalled.Active; i++) Update(stalled, (p, _, _, _) => new(p, false, CompanionPose.Idle, false));
        check(wasActive && !stalled.Active && stalled.Position == initial, "A stationary delegate times out without a free-space fallback.");
        var moving = Approach((p, _, _, _) => new(p + new Vector2(.1f, 0), false, CompanionPose.Walk, false));
        var ticks = 0;
        while (moving.Active && ticks++ < 2500) Update(moving, (p, _, _, _) => new(p + new Vector2(.1f, 0), false, CompanionPose.Walk, false));
        check(!moving.Active && ticks < 2500 && moving.Position.X > initial.X + 200,
            "A moving but never-arriving route also has a bounded timeout and retains actual position.");

        CompanionHomeRoutine Approach(Func<Vector2, Vector2, float, bool, CompanionHomeTravel> navigate)
        {
            var routine = new CompanionHomeRoutine(17); routine.Reset(15); var called = false;
            for (var i = 0; i < 500 && !called; i++) Update(routine, (a, b, c, d) => { called = true; return navigate(a, b, c, d); });
            check(called, "Synthetic setup reached the delegated approach.");
            return routine;
        }
        void Update(CompanionHomeRoutine routine, Func<Vector2, Vector2, float, bool, CompanionHomeTravel> navigate)
            => routine.Update(.05f, true, false, initial, false, box, bounds, false, 15, 5, Vector2.Zero, new(900, 700),
                styleFrame: fitted, navigator: navigate);
    }

    internal static CompanionHomeRoutine Sample(CompanionLayout layout, CompanionHomeRect house,
        CompanionHomeStyleFrame style, CompanionHomeState state, float seconds)
    {
        var routine = new CompanionHomeRoutine(17); routine.Reset(15);
        var start = new Vector2(style.Mirrored ? house.Minimum.X - layout.SpriteSize.X - 4 : house.Maximum.X + 4, house.Maximum.Y + 4);
        for (var tick = 0; tick < 2000; tick++)
        {
            routine.Update(.05f, true, false, start, false, layout, house, style.Mirrored, 15, 5,
                layout.Minimum, layout.Maximum, styleFrame: style);
            if (routine.State == state && routine.SleepPhase + .0001f >= seconds) return routine;
        }
        throw new InvalidOperationException("Synthetic styled nap did not reach its requested pose.");
    }
}
