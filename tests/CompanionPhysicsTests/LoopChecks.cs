using System.Numerics;
using OmniKit.Ui;

internal static class LoopChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
        foreach (var width in new[] { 1920f, 5120f })
        foreach (var rate in new[] { 60, 144 })
        foreach (var left in new[] { false, true })
        {
            var size = new Vector2(96); var maximum = new Vector2(width, 1080);
            float X(float x) => left ? width - x : x;
            Vector2 Point(float x, float y) => new(X(x) - (left ? size.X : 0), y);
            CompanionSurface Surface(ulong id, float x0, float x1, float y) =>
                new(id, new(Math.Min(X(x0), X(x1)), y), new(Math.Max(X(x0), X(x1)), y + 12), 12, 1, false);
            var ledge = Surface(20, 450, 530, 1030);
            var goalPlane = Surface(21, 850, 1100, 980);
            var goal = Point(900, 980 - size.Y * .9f);
            var pet = new CompanionPhysics(); pet.NavigateFrom(Point(540, 1080 - 96), left);
            var options = new CompanionPhysicsOptions(true, true, true, true, true);
            var bounded = true; var attempts = 0; var falls = 0; var wasJumping = false; var wasFalling = false;
            var lastAttempt = -100f; var intervals = new List<float>();
            void Step(int frame, IReadOnlyList<CompanionSurface>? surfaces = null, Vector2? destination = null,
                CompanionPhysicsOptions? flags = null, Vector2? body = null)
            {
                // Sub-pixel observations and nearby goal jitter must not reset failure memory.
                var jitter = new Vector2(MathF.Sin(frame * .07f) * .2f, 0);
                var observed = ledge with { Minimum = ledge.Minimum + jitter, Maximum = ledge.Maximum + jitter };
                pet.Update(surfaces ?? [observed, goalPlane], Vector2.Zero, maximum, body ?? size, 1f / rate, true, false,
                    flags ?? options, null, null, null, destination ?? goal + new Vector2(MathF.Sin(frame * .09f) * 2, 0));
                var jumping = pet.Placement == CompanionPlacement.Jumping;
                var falling = pet.Placement == CompanionPlacement.Falling;
                if (jumping && !wasJumping) { attempts++; intervals.Add(frame / (float)rate - lastAttempt); lastAttempt = frame / (float)rate; }
                if (falling && !wasFalling) falls++;
                wasJumping = jumping; wasFalling = falling;
                bounded &= float.IsFinite(pet.Position.X) && pet.Position.X >= 0 && pet.Position.X + (body ?? size).X <= width + .001f;
            }
            for (var frame = 0; frame < rate * 120; frame++) Step(frame);
            Check(bounded, "Cycle recovery remains finite and inside both viewport widths.");
            Check(attempts >= 2 && falls >= 2, "The actual route fixture reproduces repeated hop/fall excursions.");
            Check(attempts <= 3 && intervals.Skip(1).Any(interval => interval >= 5), "Repeated failed excursions settle after at most three attempts in an unchanged scene.");
            // Same surface ID at a genuinely different position is an available new route.
            ledge = ledge with { Minimum = ledge.Minimum + new Vector2(left ? -120 : 120, 0), Maximum = ledge.Maximum + new Vector2(left ? -120 : 120, 0) };
            var prior = attempts;
            for (var frame = rate * 120; frame < rate * 140 && attempts == prior; frame++) Step(frame);
            Check(attempts > prior, "Moving a surface while reusing its ID releases the old excursion suppression.");
            var bridge = Surface(22, 600, 920, 1010);
            for (var frame = 0; frame < rate * 30 && pet.NavigationStatus != CompanionNavigationStatus.Reached; frame++)
                Step(frame, [ledge, goalPlane, bridge], goal);
            Check(pet.NavigationStatus == CompanionNavigationStatus.Reached, "An alternate reachable bridge completes the route instead of repeating the bad ledge.");
            var before = Point(540, 1080 - 96);
            pet.PickUp(before, before + size * .5f, left);
            pet.Carry(before + size * .5f, Vector2.Zero, maximum, size, .016f);
            Check(pet.IsCarried, "A manual pickup rebases recovery without forcing a retry or teleport.");
            pet.Drop(before + size * .5f, [], Vector2.Zero, maximum, size, options);
            prior = attempts;
            for (var frame = 0; frame < rate * 10 && attempts == prior; frame++) Step(frame);
            Check(attempts > prior, "Manual pickup and placement release prior route quarantine.");
            before = pet.Position;
            for (var frame = 0; frame < rate; frame++) Step(frame, flags: options with { Grounded = false, AutoHop = false });
            Check(pet.Placement != CompanionPlacement.Jumping, "Grounded/auto-hop disabled cannot trigger recovery hops.");
            // A newly reachable destination is followed rather than permanently blacklisting a ledge.
            var floorGoal = Point(330, 1080 - 96);
            for (var frame = 0; frame < rate * 30 && pet.NavigationStatus != CompanionNavigationStatus.Reached; frame++)
                Step(frame, destination: floorGoal);
            Check(pet.NavigationStatus == CompanionNavigationStatus.Reached, "A meaningful destination change resumes ordinary supported walking.");
        }
        foreach (var rate in new[] { 60, 144 })
        foreach (var left in new[] { false, true })
        {
            var size = new Vector2(96); var maximum = new Vector2(1920, 1080);
            var area = new CompanionLayout(new(250, 100), new(1450, 1080), size);
            var upper = new CompanionSurface(30, new(600, 1030), new(1050, 1042), 12, 1, true);
            var pet = new CompanionPhysics(); var attempts = 0; var jumping = false;
            pet.NavigateFrom(new(left ? 1100 : 500, 984), left);
            for (var frame = 0; frame < rate * 180; frame++)
            {
                var waypoint = new Vector2(500 + MathF.Sin(frame * .08f) * 3, 984);
                pet.Update([upper], Vector2.Zero, maximum, size, 1f / rate, true, false,
                    new(true, true, true, true, true), waypoint, null, area);
                if (pet.Placement == CompanionPlacement.Jumping && !jumping) attempts++;
                jumping = pet.Placement == CompanionPlacement.Jumping;
            }
            Check(attempts is >= 2 and <= 3, "A wide ledge and wandering saved-area waypoints cannot erase the repeated excursion history.");
            var prior = attempts; var resized = new Vector2(100.8f);
            var resizedArea = new CompanionLayout(area.Minimum, area.Maximum, resized);
            var bounded = true;
            for (var frame = 0; frame < rate * 60; frame++)
            {
                pet.Update([upper], Vector2.Zero, maximum - new Vector2(80, 0), resized, 1f / rate, true, false,
                    new(true, true, true, true, true), new(500, 979.2f), null, resizedArea);
                if (pet.Placement == CompanionPlacement.Jumping && !jumping) attempts++;
                jumping = pet.Placement == CompanionPlacement.Jumping;
                bounded &= pet.Position.X >= 0 && pet.Position.X + resized.X <= maximum.X - 80 + .001f;
            }
            Check(attempts > prior && bounded, "Sprite and viewport resizing re-evaluates routes while keeping the entire cell bounded.");
        }
        // Isolate recovery on an already observed higher support. Inject only a
        // completed failure record; physics, geometry and progress checks are real.
        foreach (var supported in new[] { false, true })
        {
            var size = new Vector2(96); var maximum = new Vector2(1920, 1080);
            var oldTarget = new CompanionSurface(40, new(450, 1030), new(530, 1042), 12, 1, false);
            var alternate = new CompanionSurface(41, new(700, 750), new(1000, 762), 12, 1, false);
            var goal = new Vector2(1000, 600);
            var goalPlane = new CompanionSurface(42, new(950, 686.4f), new(1200, 698.4f), 12, 1, false);
            var observations = new[] { oldTarget, alternate, goalPlane };
            var pet = new CompanionPhysics();
            pet.NavigateFrom(new(750, 750 - size.Y * .9f - (supported ? 0 : 10)), false);
            var options = new CompanionPhysicsOptions(true, true, true, true, true);
            pet.Update(observations, Vector2.Zero, maximum, size, 0, true, false, options, null, null, null, goal);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var failureType = typeof(CompanionPhysics).GetNestedType("HopFailure", System.Reflection.BindingFlags.NonPublic)!;
            var failures = (System.Collections.IList)typeof(CompanionPhysics).GetField("hopFailures", flags)!.GetValue(pet)!;
            var failurePosition = new Vector2(500, 984);
            failures.Add(Activator.CreateInstance(failureType, oldTarget, failurePosition,
                Vector2.Distance(failurePosition, goal), 3, 99f)!);
            var suppressed = typeof(CompanionPhysics).GetMethod("HopSuppressed", flags)!;
            Check((bool)suppressed.Invoke(pet, [oldTarget, size])!, "The state fixture begins with the old ledge quarantined.");
            pet.Update(observations, Vector2.Zero, maximum, size, 0, true, false, options, null, null, null, goal);
            Check((bool)suppressed.Invoke(pet, [oldTarget, size])! != supported,
                supported ? "Verified progress on an unchanged higher alternate support releases the old quarantine."
                    : "An unsupported Returning position cannot masquerade as higher route progress.");
        }
        return checks;
    }
}
