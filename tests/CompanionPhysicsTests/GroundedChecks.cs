using System.Numerics;
using OmniKit.Ui;

internal static class GroundedChecks
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
        var minimum = Vector2.Zero; var maximum = new Vector2(1000, 700); var size = new Vector2(96);
        var area = new CompanionLayout(new Vector2(250, 100), new Vector2(650, 450), size);
        var options = new CompanionPhysicsOptions(true, true, true, true, true);
        void Step(CompanionPhysics pet, IReadOnlyList<CompanionSurface>? surfaces = null, CompanionLayout? box = null,
            CompanionPhysicsOptions? flags = null, float dt = .016f, bool hover = false, bool animate = true, CompanionSurface? house = null)
            => pet.Update(surfaces ?? [], minimum, maximum, size, dt, animate, hover, flags ?? options, new Vector2(300, 354), house, box ?? area);

        var roaming = new CompanionPhysics();
        Step(roaming, dt: 0);
        Check(roaming.IsOnRoamingPlane && roaming.Position.Y == area.LastPosition.Y,
            "Grounded normal roaming initializes on the saved bottom plane and exposes nap-compatible ownership.");
        var initial = roaming.Position;
        for (var i = 0; i < 100; i++) Step(roaming);
        Check(roaming.Position.X > initial.X && roaming.Position.Y == initial.Y && roaming.IsOnRoamingPlane,
            "Normal grounded travel stays completely horizontal.");
        var stayedHome = true; var directions = new HashSet<bool>();
        for (var i = 0; i < 2200; i++)
        {
            Step(roaming);
            stayedHome &= roaming.IsOnRoamingPlane && roaming.Position.X >= area.Minimum.X
                && roaming.Position.X + size.X <= area.Maximum.X && roaming.Position.Y == area.LastPosition.Y;
            directions.Add(roaming.FacingLeft);
        }
        Check(stayedHome && directions.Count == 2, "Ordinary saved-area roaming turns at both ends and keeps the complete body in the drawn area.");
        var outsideLedge = new CompanionSurface(90, new Vector2(area.Maximum.X, area.Maximum.Y - 50),
            new Vector2(area.Maximum.X + 180, area.Maximum.Y - 38), 12, 0, false);
        var confined = new CompanionPhysics(); var stayedConfined = true;
        for (var i = 0; i < 1200; i++)
        {
            Step(confined, [outsideLedge]);
            stayedConfined &= confined.IsOnRoamingPlane && confined.Position.X >= area.Minimum.X
                && confined.Position.X + size.X <= area.Maximum.X;
        }
        Check(stayedConfined, "An automatic hop cannot escape the saved area onto an outside frame.");
        var insideLedge = new CompanionSurface(91, new Vector2(450, area.Maximum.Y - 50), new Vector2(620, area.Maximum.Y - 38), 12, 0, false);
        var within = new CompanionPhysics(); var hoppedInside = false;
        for (var i = 0; i < 600 && !hoppedInside; i++)
        {
            Step(within, [insideLedge]);
            hoppedInside = within.Placement == CompanionPlacement.Jumping;
        }
        Check(hoppedInside, "A reachable frame inside the saved area still permits a bounded automatic hop.");
        var paused = roaming.Position; Step(roaming, animate: false);
        Check(roaming.Position == paused, "Disabling animations freezes grounded movement.");
        roaming.Update([], minimum, maximum, size, .016f, true, false, options, initial, null, null);
        Check(!roaming.Active, "Omitting the saved plane releases normal physics ownership for the nap routine.");

        CompanionPhysics StartNear(CompanionSurface lower, bool left = false)
        {
            var pet = new CompanionPhysics();
            var point = new Vector2(left ? lower.Minimum.X + 35 : lower.Maximum.X - 35, lower.Minimum.Y + 4);
            pet.PickUp(point - size * .5f, point, left);
            pet.Drop(point, [lower], minimum, maximum, size, options);
            for (var i = 0; i < 24; i++) Step(pet, [lower], dt: .016f);
            return pet;
        }
        foreach (var left in new[] { false, true })
        {
            var lower = new CompanionSurface(21, new Vector2(300, 500), new Vector2(550, 512), 12, 1, false);
            var upper = new CompanionSurface(22,
                new Vector2(left ? 160 : 550, 455), new Vector2(left ? 300 : 690, 467), 12, 2, false);
            var pet = StartNear(lower, left);
            for (var i = 0; i < 200 && pet.Placement != CompanionPlacement.Jumping; i++) Step(pet, [lower, upper]);
            Check(pet.Placement == CompanionPlacement.Jumping, "A nearby higher ledge starts a short hop in either direction.");
            var previous = pet.Position;
            Step(pet, [lower, upper], hover: true);
            Check(pet.Position.Y < previous.Y, "Hover cannot suspend an ascending hop.");
            for (var i = 0; i < 200 && pet.Placement == CompanionPlacement.Jumping; i++) Step(pet, [lower, upper]);
            Check(pet.Placement == CompanionPlacement.Perched && pet.AttachedSurfaceId == upper.Id
                && Math.Abs(pet.Position.Y + size.Y * .9f - upper.Minimum.Y) < .001f, "The completed hop lands exactly on the observed upper plane.");
            var fallSeen = false;
            for (var i = 0; i < 1000 && !fallSeen; i++) { Step(pet, [lower, upper]); fallSeen |= pet.Placement == CompanionPlacement.Falling; }
            Check(fallSeen, "The visitor subsequently walks off the raised ledge instead of hitting an invisible wall.");

            pet = StartNear(lower, left);
            for (var i = 0; i < 200 && pet.Placement != CompanionPlacement.Jumping; i++) Step(pet, [lower, upper]);
            Step(pet, [lower, upper]); previous = pet.Position;
            Step(pet, [lower]);
            Check(pet.Placement == CompanionPlacement.Falling && pet.Position.Y < previous.Y,
                "A disappearing target cancels the hop while preserving actual upward velocity.");
        }

        foreach (var rise in new[] { 45f, 200f })
        {
            var saved = new CompanionLayout(new Vector2(250, 100), new Vector2(650, maximum.Y - rise), size);
            var pet = new CompanionPhysics();
            pet.PickUp(new Vector2(800, 550), new Vector2(848, 598), true);
            pet.Drop(new Vector2(848, 598), [], minimum, maximum, size, options);
            var hopped = false; var illegalSkyWalk = false; var previous = pet.Position;
            for (var i = 0; i < 3000; i++)
            {
                Step(pet, box: saved);
                hopped |= pet.Placement == CompanionPlacement.Jumping;
                illegalSkyWalk |= pet.Pose == CompanionPose.Walk && Math.Abs(pet.Position.Y - previous.Y) > .001f;
                previous = pet.Position;
                if (pet.IsOnRoamingPlane) break;
            }
            Check(!illegalSkyWalk, "Grounded returning never uses diagonal walking through open air.");
            Check(rise == 45 ? pet.IsOnRoamingPlane && hopped : !pet.IsOnRoamingPlane && !hopped && pet.Position.Y == maximum.Y - size.Y,
                rise == 45 ? "A reachable saved plane is regained by floor travel and a bounded hop." : "An unreachable isolated saved plane waits on the floor rather than teleporting.");
        }

        var source = new Vector2(400, 400);
        var destination = new CompanionSurface(31, new Vector2(485, 440), new Vector2(650, 452), 12, 1, false);
        Check(CompanionHop.TryCreate(source, size, true, destination, minimum, maximum, null, out var plan), "The collision fixture is a reachable hop.");
        var obstacle = new CompanionSurface(0, new Vector2(490, 350), new Vector2(620, 520), 0, 0);
        Check(!CompanionHop.TryCreate(source, size, true, destination, minimum, maximum, obstacle, out _), "The complete trajectory respects the house footprint.");
        Check(!CompanionHop.TryCreate(source, size, true, destination with { Id = 0 }, minimum, maximum, null, out _), "A house is never selected as a platform.");
        Check(!CompanionHop.TryCreate(new Vector2(float.NaN), size, true, destination, minimum, maximum, null, out _), "Invalid geometry fails closed.");
        Check(plan.At(0) == source && plan.At(plan.Duration) == plan.End && plan.Apex <= size.Y,
            "Ballistic endpoints and the one-cell height bound are exact.");

        foreach (var destinationHeight in new[] { 0f, 45f, 200f })
        {
            var target = new Vector2(480, maximum.Y - size.Y - destinationHeight);
            var porch = new CompanionSurface(50, target + new Vector2(-30, size.Y * .9f),
                target + new Vector2(180, size.Y * .9f + 2), 2, 0, false);
            var navigator = new CompanionPhysics(); var current = new Vector2(200, maximum.Y - size.Y);
            navigator.NavigateFrom(current, false);
            Check(navigator.Position == current, "Navigation begins at the real current position without a clamp or teleport.");
            var diagonalWalk = false; var previous = current;
            for (var i = 0; i < 1800; i++)
            {
                navigator.Update([porch], minimum, maximum, size, .016f, true, false, options,
                    null, null, null, target, 2);
                diagonalWalk |= navigator.Pose == CompanionPose.Walk && Math.Abs(navigator.Position.Y - previous.Y) > .001f;
                previous = navigator.Position;
                if (navigator.NavigationStatus is CompanionNavigationStatus.Reached or CompanionNavigationStatus.Blocked) break;
            }
            Check(!diagonalWalk, "Nap navigation never represents unsupported vertical travel as walking.");
            Check(destinationHeight < 100 ? navigator.NavigationStatus == CompanionNavigationStatus.Reached && navigator.Position == target
                : navigator.NavigationStatus == CompanionNavigationStatus.Blocked && navigator.Position.Y == maximum.Y - size.Y,
                "A supported reachable goal is exact; an unreachable porch reports blocked from the actual floor.");
        }
        var upperStart = new Vector2(400, 400);
        var highSupport = new CompanionSurface(60, new Vector2(300, upperStart.Y + size.Y * .9f), new Vector2(620, upperStart.Y + size.Y * .9f + 5), 5, 0, false);
        var downGoal = new Vector2(450, maximum.Y - size.Y);
        var down = new CompanionPhysics(); down.NavigateFrom(upperStart, true);
        var sawDownFall = false; var downWalkIsFlat = true; var priorDown = down.Position;
        for (var i = 0; i < 1800; i++)
        {
            down.Update([highSupport], minimum, maximum, size, .016f, true, false, options, null, null, null, downGoal, 2);
            sawDownFall |= down.Placement == CompanionPlacement.Falling;
            downWalkIsFlat &= down.Pose != CompanionPose.Walk || Math.Abs(down.Position.Y - priorDown.Y) < .001f;
            priorDown = down.Position;
            if (down.NavigationStatus == CompanionNavigationStatus.Reached) break;
        }
        Check(sawDownFall && downWalkIsFlat && down.NavigationStatus == CompanionNavigationStatus.Reached,
            "A lower goal is reached by walking off the support and falling, then walking along the floor.");
        var blocked = new CompanionPhysics(); blocked.NavigateFrom(new Vector2(200, maximum.Y - size.Y), false);
        blocked.Update([], minimum, maximum, size, .016f, true, false, options, null, null, null, new Vector2(450, 200), 2);
        Check(blocked.NavigationStatus == CompanionNavigationStatus.Blocked, "An unsupported floating destination is never invented as a plane.");
        var walled = new CompanionPhysics(); walled.NavigateFrom(new Vector2(200, maximum.Y - size.Y), false);
        for (var i = 0; i < 500; i++)
        {
            walled.Update([], minimum, maximum, size, .016f, true, false, options, null,
                new CompanionSurface(0, new Vector2(380, 550), new Vector2(580, 700), 0, 0), null, new Vector2(650, 604), 2);
            if (walled.NavigationStatus == CompanionNavigationStatus.Blocked) break;
        }
        Check(walled.NavigationStatus == CompanionNavigationStatus.Blocked && walled.Position.X + size.X <= 380,
            "A horizontal route stops at the house instead of walking over its footprint.");

        var ledge = new CompanionSurface(70, new Vector2(300, 500), new Vector2(550, 512), 12, 1, false);
        var nextLedge = new CompanionSurface(71, new Vector2(550, 455), new Vector2(690, 467), 12, 2, false);
        foreach (var change in new[] { "move", "disable", "resize" })
        {
            var visitor = StartNear(ledge);
            for (var i = 0; i < 200 && visitor.Placement != CompanionPlacement.Jumping; i++) Step(visitor, [ledge, nextLedge]);
            Check(visitor.Placement == CompanionPlacement.Jumping, "The cancellation fixture begins a real planned hop.");
            Step(visitor, [ledge, nextLedge]);
            var before = visitor.Position;
            if (change == "move") Step(visitor, [ledge, nextLedge with { Minimum = nextLedge.Minimum + new Vector2(10), Maximum = nextLedge.Maximum + new Vector2(10) }]);
            else if (change == "disable") Step(visitor, [ledge, nextLedge], flags: options with { AutoHop = false });
            else visitor.Update([ledge, nextLedge], minimum, maximum, size * 1.05f, .016f, true, false, options, null, null, area);
            Check(visitor.Placement == CompanionPlacement.Falling && visitor.Position.Y < before.Y,
                "Changing the hop target, setting or sprite size releases into the existing upward velocity.");
        }
        var noHop = StartNear(ledge);
        var sawJumpWithoutPermission = false;
        for (var i = 0; i < 400 && noHop.Placement != CompanionPlacement.Falling; i++)
        {
            Step(noHop, [ledge, nextLedge], flags: options with { AutoHop = false });
            sawJumpWithoutPermission |= noHop.Placement == CompanionPlacement.Jumping;
        }
        Check(!sawJumpWithoutPermission && noHop.Placement == CompanionPlacement.Falling, "Disabling automatic hops preserves ordinary walk-off falling.");
        var ownWithoutForeign = new CompanionPhysics();
        Step(ownWithoutForeign, flags: options with { Perch = false });
        Check(ownWithoutForeign.IsOnRoamingPlane, "The saved area remains a ground plane when foreign frame perching is disabled.");
        foreach (var badStart in new[] { new Vector2(-10, 400), new Vector2(950, 400), new Vector2(400, 650), new Vector2(400, -10) })
            Check(!CompanionHop.TryCreate(badStart, size, true, destination, minimum, maximum, null, out _), "Offscreen hop starts are rejected.");
        var coveredPet = new CompanionPhysics();
        coveredPet.Update([], minimum, maximum, size, 0, true, false, options, new Vector2(450, area.LastPosition.Y), null, area);
        var coveringHouse = new CompanionSurface(0, coveredPet.Position - new Vector2(20), coveredPet.Position + size + new Vector2(20), 0, 0);
        var coveredY = coveredPet.Position.Y; var coveredStepBounded = true;
        CompanionObstacle.TryBounds(coveringHouse, size, out var coverMin, out var coverMax);
        for (var i = 0; i < 500 && CompanionObstacle.Inside(coveredPet.Position, coverMin, coverMax); i++)
        {
            var before = coveredPet.Position; Step(coveredPet, house: coveringHouse);
            coveredStepBounded &= coveredPet.Position.Y == coveredY && Vector2.Distance(before, coveredPet.Position) <= size.Y * .34f * .016f + .001f;
        }
        Check(coveredStepBounded && !CompanionObstacle.Inside(coveredPet.Position, coverMin, coverMax),
            "A house dragged under a grounded pet is escaped horizontally without teleporting or changing walking height.");
        foreach (var edge in new[] { 48f, 96f, 192f })
        foreach (var right in new[] { false, true })
        {
            var body = new Vector2(edge); var start = new Vector2(400, 400);
            var firstX = right ? start.X + edge * .9f : start.X - edge * 1.9f;
            var highest = new CompanionSurface(80, new Vector2(firstX, 400), new Vector2(firstX + edge * 2, 412), 12, 0, false);
            Check(CompanionHop.TryCreate(start, body, right, highest, minimum, maximum, null, out var maximumHop)
                && maximumHop.Apex <= edge && maximumHop.At(0) == start
                && Math.Abs(maximumHop.End.Y + edge * .9f - highest.Minimum.Y) < .001f,
                "The maximum .9-cell climb remains a bounded one-cell hop at48/96/192px in both directions.");
        }

        return checks;
    }
}
