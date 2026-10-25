using System.Numerics;

namespace OmniKit.Ui;

internal readonly record struct CompanionPhysicsOptions(bool Perch, bool Contain, bool Fall, bool Grounded = false, bool AutoHop = false);
internal enum CompanionPlacement { None, Carried, Perched, Contained, Falling, Floor, Returning, Jumping }
internal enum CompanionNavigationStatus { None, Moving, Reached, Blocked }

/// <summary>Companion-only motion. Surfaces are immutable observations; no foreign UI is changed.</summary>
internal sealed class CompanionPhysics
{
    private ulong surfaceId;
    private Vector2 lastSurfaceOrigin;
    private Vector2 grabOffset;
    private Vector2 carryPointer;
    private float carryTime;
    private Vector2 velocity;
    private float reactionTime;
    private CompanionPose reaction;
    private bool reactionFacingLeft;
    private float direction = 1;
    private float perchWalkTime;
    private bool perchTurnPending;
    private const ulong RoamingPlaneId = 0xFFFFFFFFFFFFFFFEul;
    private readonly List<CompanionSurface> groundedSurfaces = [];
    private CompanionHop hop;
    private float hopTime;
    private CompanionSurface hopTarget;
    private Vector2 hopSize;
    private bool returningHome;
    private readonly List<HopFailure> hopFailures = [];
    private HopExcursion? hopExcursion;
    private float hopClock;
    private Vector2 stableGoal;
    private bool hasStableGoal;
    private Vector2 routeSize;
    private Vector2 routeViewportMinimum;
    private Vector2 routeViewportMaximum;
    private CompanionLayout? routeArea;
    private readonly record struct HopExcursion(CompanionSurface Target, Vector2 Start, Vector2? Goal);
    private readonly record struct HopFailure(CompanionSurface Target, Vector2 Position, float GoalDistance, int Count, float Until);


    public CompanionPlacement Placement { get; private set; }
    public bool Active => Placement != CompanionPlacement.None;
    public bool IsCarried => Placement == CompanionPlacement.Carried;
    // Normal grounded roaming still counts as awake time for the nap routine.
    public bool IsOnRoamingPlane => Placement == CompanionPlacement.Perched && surfaceId == RoamingPlaneId;
    public CompanionNavigationStatus NavigationStatus { get; private set; }
    public ulong AttachedSurfaceId => surfaceId;
    public Vector2 Position { get; private set; }
    public bool FacingLeft => reactionTime > 0 && reaction == CompanionPose.Push ? reactionFacingLeft : direction < 0;
    public CompanionPose Pose { get; private set; } = CompanionPose.Idle;
    public Vector2 ClipMinimum { get; private set; }
    public Vector2 ClipMaximum { get; private set; }

    public void Reset()
    {
        Placement = CompanionPlacement.None;
        surfaceId = 0;
        velocity = Vector2.Zero;
        reactionTime = 0;
        perchTurnPending = false;
        Pose = CompanionPose.Idle;
        returningHome = false;
        ClearHopMemory();
        NavigationStatus = CompanionNavigationStatus.None;
    }

    public void NavigateFrom(Vector2 position, bool facingLeft)
    {
        Reset();
        if (!Finite(position)) return;
        Position = position; direction = facingLeft ? -1 : 1;
        BeginReturn(); NavigationStatus = CompanionNavigationStatus.Moving;
    }

    public void PickUp(Vector2 position, Vector2 pointer, bool facingLeft)
    {
        returningHome = false;
        ClearHopMemory();
        Position = position;
        grabOffset = pointer - position;
        carryPointer = pointer;
        carryTime = 0;
        velocity = Vector2.Zero;
        direction = facingLeft ? -1 : 1;
        reactionTime = 0;
        surfaceId = 0;
        Placement = CompanionPlacement.Carried;
        Pose = CompanionPose.Carry;
    }

    public void Carry(Vector2 pointer, Vector2 viewportMinimum, Vector2 viewportMaximum, Vector2 size, float elapsed)
    {
        if (!IsCarried || !Finite(pointer)) return;
        carryTime = Math.Min(CompanionGrabAnchor.CenteringSeconds,
            carryTime + (float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0));
        var next = Clamp(CompanionGrabAnchor.CarryTopAt(pointer, size, grabOffset, carryTime), viewportMinimum, viewportMaximum, size);
        if (elapsed > .001f && float.IsFinite(elapsed))
        {
            // Only pointer travel imparts momentum. The cosmetic centering
            // transition must not throw a stationary, off-centre pickup sideways.
            var dragged = Clamp(Position + pointer - carryPointer, viewportMinimum, viewportMaximum, size);
            velocity.X = Math.Clamp((dragged.X - Position.X) / elapsed, -180, 180);
        }
        carryPointer = pointer;
        Position = next;
        ClipMinimum = viewportMinimum;
        ClipMaximum = viewportMaximum;
    }

    public void Drop(Vector2 pointer, IReadOnlyList<CompanionSurface> surfaces, Vector2 viewportMinimum,
        Vector2 viewportMaximum, Vector2 size, CompanionPhysicsOptions options)
    {
        if (!IsCarried) return;
        ClipMinimum = viewportMinimum;
        ClipMaximum = viewportMaximum;
        CompanionSurface? best = null;
        foreach (var surface in surfaces)
            if (surface.Contains(pointer) && (best == null || surface.Order > best.Value.Order)) best = surface;
        if (best is { } selected)
        {
            var topBand = Math.Clamp(selected.HeaderHeight, 10, Math.Max(10, selected.Size.Y));
            if (options.Perch && (!selected.CanContain || pointer.Y <= selected.Minimum.Y + topBand)
                && CanPerch(selected, size, viewportMinimum))
            {
                Attach(selected, CompanionPlacement.Perched);
                Position = new Vector2(Math.Clamp(pointer.X - size.X * .5f, selected.Minimum.X - size.X * .5f,
                    selected.Maximum.X - size.X * .5f), selected.Minimum.Y - size.Y * .9f);
                Position = Clamp(Position, viewportMinimum, viewportMaximum, size);
                React(CompanionPose.Land, .35f);
                return;
            }
            var innerMinimum = selected.Minimum + new Vector2(4, Math.Max(4, selected.HeaderHeight));
            var innerMaximum = selected.Maximum - new Vector2(4);
            if (options.Contain && selected.CanContain && Fits(innerMinimum, innerMaximum, size))
            {
                Attach(selected, CompanionPlacement.Contained);
                Position = Clamp(Position, innerMinimum, innerMaximum, size);
                React(CompanionPose.Land, .35f);
                return;
            }
        }
        if (options.Fall) BeginFall(); else BeginReturn();
    }

    public void Update(IReadOnlyList<CompanionSurface> surfaces, Vector2 viewportMinimum, Vector2 viewportMaximum,
        Vector2 size, float elapsed, bool animate, bool hoverPaused, CompanionPhysicsOptions options,
        Vector2? homePosition = null, CompanionSurface? obstacle = null, CompanionLayout? roamingArea = null,
        Vector2? navigationGoal = null, float speedMultiplier = 1)
    {
        NavigationStatus = navigationGoal.HasValue ? CompanionNavigationStatus.Moving : CompanionNavigationStatus.None;
        if (navigationGoal.HasValue) returningHome = true;
        CompanionSurface? homePlane = null;
        if (options.Grounded)
        {
            groundedSurfaces.Clear();
            foreach (var surface in surfaces) if (surface.Id != 0) groundedSurfaces.Add(surface);
            if (roamingArea is { IsVisible: true } area && Fits(area.Minimum, area.Maximum, size))
            {
                var footY = area.Maximum.Y - size.Y * .1f;
                homePlane = new CompanionSurface(RoamingPlaneId, new(area.Minimum.X, footY),
                    new(area.Maximum.X, footY + 1), 1, int.MinValue, false);
                groundedSurfaces.Add(homePlane.Value);
                if (!Active)
                {
                    Position = new(Math.Clamp(homePosition?.X ?? area.Minimum.X, area.Minimum.X, area.LastPosition.X), area.LastPosition.Y);
                    Attach(homePlane.Value, CompanionPlacement.Perched);
                    Pose = CompanionPose.Idle;
                }
            }
            else if (IsOnRoamingPlane && !navigationGoal.HasValue) { Reset(); return; }
            surfaces = groundedSurfaces;
        }
        if (!Active || !Fits(viewportMinimum, viewportMaximum, size)) { Reset(); return; }
        ClipMinimum = viewportMinimum;
        ClipMaximum = viewportMaximum;
        var dt = float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0;
        // The motion waypoint wanders inside the saved area. Its fixed center,
        // not each tiny waypoint, is the meaningful destination for return progress.
        var recoveryGoal = navigationGoal ?? (roamingArea is { IsVisible: true } recoveryArea
            ? (recoveryArea.Minimum + recoveryArea.LastPosition) * .5f : returningHome ? homePosition : null);
        UpdateHopMemory(surfaces, viewportMinimum, viewportMaximum, size, animate ? dt : 0, options.Grounded, recoveryGoal, roamingArea);
        CompanionSurface? attached = null;
        if (Placement is CompanionPlacement.Perched or CompanionPlacement.Contained)
        {
            foreach (var surface in surfaces) if (surface.Id == surfaceId) { attached = surface; break; }
            if (attached == null || Placement == CompanionPlacement.Perched && !options.Perch && !IsOnRoamingPlane
                || Placement == CompanionPlacement.Contained && (!options.Contain || !attached.Value.CanContain))
            {
                if (options.Fall) BeginFall(); else BeginReturn();
            }
            else
            {
                var current = attached.Value;
                Position += current.Minimum - lastSurfaceOrigin;
                lastSurfaceOrigin = current.Minimum;
                if (Placement == CompanionPlacement.Contained)
                {
                    var innerMinimum = Vector2.Max(viewportMinimum, current.Minimum + new Vector2(4, Math.Max(4, current.HeaderHeight)));
                    var innerMaximum = Vector2.Min(viewportMaximum, current.Maximum - new Vector2(4));
                    if (!Fits(innerMinimum, innerMaximum, size))
                    { if (options.Fall) BeginFall(); else BeginReturn(); }
                    else
                    {
                        ClipMinimum = innerMinimum;
                        ClipMaximum = innerMaximum;
                        Position = Clamp(Position, ClipMinimum, ClipMaximum, size);
                    }
                }
                else if (!CanPerch(current, size, viewportMinimum))
                { if (options.Fall) BeginFall(); else BeginReturn(); }
                else
                {
                    Position = new Vector2(Position.X, current.Minimum.Y - size.Y * .9f);
                    Position = Clamp(Position, viewportMinimum, viewportMaximum, size);
                }
            }
        }
        if (IsCarried) { Pose = animate ? CompanionPose.Carry : CompanionPose.Idle; return; }
        if (Placement == CompanionPlacement.Jumping)
        {
            var target = surfaces.FirstOrDefault(candidate => candidate.Id == hopTarget.Id);
            if (!options.Grounded || !options.AutoHop || !options.Fall || target.Id != hopTarget.Id
                || !SameRouteSurface(target, hopTarget, size) || hopSize != size
                || Clamp(hop.End, viewportMinimum, viewportMaximum, size) != hop.End)
                BeginBallisticFall();
        }
        if (Placement == CompanionPlacement.Falling && !options.Fall) BeginReturn();
        // A resized viewport must contain the entire cell even while animation is frozen.
        Position = Clamp(Position, viewportMinimum, viewportMaximum, size);
        if (!animate || dt <= 0) { Pose = CompanionPose.Idle; return; }
        if (hoverPaused && Placement is not (CompanionPlacement.Falling or CompanionPlacement.Jumping) && reactionTime <= 0)
        { Pose = CompanionPose.Idle; return; }
        if (reactionTime > 0)
        {
            reactionTime = Math.Max(0, reactionTime - dt);
            Pose = reaction;
            return;
        }

        var avoidHouse = CompanionObstacle.TryBounds(obstacle, size, out var blockMinimum, out var blockMaximum);
        if (avoidHouse && Placement == CompanionPlacement.Falling && CompanionObstacle.Inside(Position, blockMinimum, blockMaximum))
            Position = CompanionObstacle.NearestExit(Position, blockMinimum, blockMaximum, viewportMinimum, viewportMaximum - size);

        var speed = Math.Max(16, size.Y * .34f) * Math.Clamp(float.IsFinite(speedMultiplier) ? speedMultiplier : 1, .25f, 3);
        if (options.Grounded && avoidHouse && (Placement is CompanionPlacement.Perched or CompanionPlacement.Floor or CompanionPlacement.Returning)
            && CompanionObstacle.Inside(Position, blockMinimum, blockMaximum))
        {
            // A dragged house may cover the current support. Leave horizontally
            // at normal speed rather than projecting the pet into open air.
            var left = blockMinimum.X >= viewportMinimum.X;
            var right = blockMaximum.X <= viewportMaximum.X - size.X;
            if (!left && !right) { Pose = CompanionPose.Idle; NavigationStatus = CompanionNavigationStatus.Blocked; return; }
            var exit = left && (!right || Position.X - blockMinimum.X <= blockMaximum.X - Position.X)
                ? blockMinimum.X : blockMaximum.X;
            var step = Math.Clamp(exit - Position.X, -speed * dt, speed * dt);
            Position += new Vector2(step, 0); direction = Math.Sign(step);
            if (attached is { } covered && (Position.X + size.X * .5f < covered.Minimum.X || Position.X + size.X * .5f > covered.Maximum.X)
                && options.Fall) { velocity.X = direction * speed; BeginFall(); }
            else Pose = CompanionPose.Walk;
            return;
        }
        if (Placement == CompanionPlacement.Jumping)
        {
            var previous = Position;
            hopTime = Math.Min(hop.Duration, hopTime + dt);
            var next = hop.At(hopTime);
            velocity = hop.VelocityAt(hopTime);
            if (avoidHouse && CompanionObstacle.CrossesInterior(previous, next, blockMinimum, blockMaximum))
            { BeginBallisticFall(); return; }
            Position = next;
            if (CrossedSurface(surfaces, previous, Position, size, viewportMinimum, viewportMaximum, !options.Perch) is { } landing)
            {
                var fraction = (landing.Minimum.Y - previous.Y - size.Y * .9f) / (Position.Y - previous.Y);
                Position = new(float.Lerp(previous.X, Position.X, fraction), landing.Minimum.Y - size.Y * .9f);
                Attach(landing, CompanionPlacement.Perched);
                React(CompanionPose.Land, .35f);
            }
            else if (hopTime >= hop.Duration)
            {
                Attach(hopTarget, CompanionPlacement.Perched);
                React(CompanionPose.Land, .35f);
            }
            else Pose = CompanionPose.Fall;
            return;
        }
        if (Placement == CompanionPlacement.Floor && (homePosition.HasValue || navigationGoal.HasValue)) BeginReturn();
        if (options.Grounded && returningHome && Placement == CompanionPlacement.Perched && (!IsOnRoamingPlane || navigationGoal.HasValue))
            BeginReturn();
        if (Placement == CompanionPlacement.Returning)
        {
            if (options.Grounded)
            {
                ReturnOnPlanes(surfaces, homePlane, viewportMinimum, viewportMaximum, size, dt, speed, options, obstacle, navigationGoal);
                return;
            }
            if (homePosition is not { } home || !Finite(home)) { Pose = CompanionPose.Idle; return; }
            var destination = CompanionObstacle.Destination(home, size, viewportMinimum, viewportMaximum, obstacle);
            var previous = Position;
            Position = CompanionObstacle.Step(Position, destination, size, speed * dt, viewportMinimum, viewportMaximum, obstacle);
            if (Vector2.DistanceSquared(Position, destination) < .0001f)
            {
                Reset();
                return;
            }
            var delta = Position - previous;
            direction = CompanionGait.FacingFor(delta, direction < 0) ? -1 : 1;
            Pose = delta.LengthSquared() > .000001f ? CompanionPose.Walk : CompanionPose.Idle;
            return;
        }
        if (Placement == CompanionPlacement.Falling)
        {
            var previous = Position;
            velocity.Y = Math.Min(700, velocity.Y + 900 * dt);
            Position += velocity * dt;
            Position = new Vector2(Math.Clamp(Position.X, viewportMinimum.X, viewportMaximum.X - size.X), Position.Y);
            if (avoidHouse && CompanionObstacle.CrossesInterior(previous, Position, blockMinimum, blockMaximum))
            {
                // Drift to a free side before descending past the house. It is
                // an obstacle, never a landing platform or a foreign UI surface.
                var left = blockMinimum.X >= viewportMinimum.X;
                var right = blockMaximum.X <= viewportMaximum.X - size.X;
                var edge = left && (!right || Math.Abs(previous.X - blockMinimum.X) <= Math.Abs(previous.X - blockMaximum.X))
                    ? blockMinimum.X : right ? blockMaximum.X : previous.X;
                Position = new Vector2(previous.X + Math.Clamp(edge - previous.X, -180 * dt, 180 * dt),
                    Math.Min(Position.Y, Math.Max(previous.Y, blockMinimum.Y)));
                velocity.X = 0;
            }
            if ((options.Perch || options.Grounded) && CrossedSurface(surfaces,
                previous, Position, size, viewportMinimum, viewportMaximum, !options.Perch)
                is { } landing)
            {
                var fraction = (landing.Minimum.Y - previous.Y - size.Y * .9f) / (Position.Y - previous.Y);
                Position = new Vector2(float.Lerp(previous.X, Position.X, fraction), landing.Minimum.Y - size.Y * .9f);
                Attach(landing, CompanionPlacement.Perched);
                React(CompanionPose.Land, .35f);
                return;
            }
            if (Position.Y + size.Y >= viewportMaximum.Y)
            {
                Placement = CompanionPlacement.Floor;
                Position = Clamp(Position, viewportMinimum, viewportMaximum, size);
                velocity = Vector2.Zero;
                React(CompanionPose.Land, .5f);
            }
            else Pose = CompanionPose.Fall;
            return;
        }

        var proposed = Position + new Vector2(direction * speed * dt, 0);
        if (Placement == CompanionPlacement.Perched && attached is { } ledgeSurface)
        {
            if (options.Grounded && TryStartHop(surfaces, size, viewportMinimum, viewportMaximum, options, obstacle,
                confinement: IsOnRoamingPlane ? roamingArea : null)) return;
            if (options.Grounded && avoidHouse && (CompanionObstacle.Inside(proposed, blockMinimum, blockMaximum)
                || CompanionObstacle.CrossesInterior(Position, proposed, blockMinimum, blockMaximum)))
            { direction = -direction; React(CompanionPose.Idle, .3f); return; }
            if (IsOnRoamingPlane && !navigationGoal.HasValue
                && (proposed.X < ledgeSurface.Minimum.X || proposed.X + size.X > ledgeSurface.Maximum.X))
            {
                // The saved area is home, not an accidental drop-off. Explicit
                // navigation may leave it; foreign open ledges still have edges.
                Position = new(Math.Clamp(Position.X, ledgeSurface.Minimum.X,
                    Math.Max(ledgeSurface.Minimum.X, ledgeSurface.Maximum.X - size.X)), Position.Y);
                direction = -direction; React(CompanionPose.Idle, .3f); return;
            }
            perchWalkTime += dt;
            if (perchTurnPending && perchWalkTime >= 1.4f)
            {
                perchTurnPending = false;
                direction = -direction;
                React(CompanionPose.Idle, .3f);
                return;
            }
            var center = proposed.X + size.X * .5f;
            if (center < ledgeSurface.Minimum.X || center > ledgeSurface.Maximum.X)
            {
                if (options.Fall) { Position = Clamp(proposed, viewportMinimum, viewportMaximum, size); velocity.X = direction * speed; BeginFall(); return; }
                direction = -direction;
            }
            else Position = proposed;
            var clamped = Clamp(Position, viewportMinimum, viewportMaximum, size);
            if (clamped.X != Position.X) direction = -direction;
            Position = clamped;
        }
        else
        {
            var clamped = Clamp(proposed, ClipMinimum, ClipMaximum, size);
            Position = clamped;
            if (clamped.X != proposed.X)
            {
                React(Placement == CompanionPlacement.Contained ? CompanionPose.Push : CompanionPose.Idle, .75f);
                // Keep facing the wall during the push. Reverse after the reaction ends.
                direction = -direction;
                return;
            }
        }
        Pose = CompanionPose.Walk;
    }

    private void Attach(CompanionSurface surface, CompanionPlacement placement)
    {
        Placement = placement;
        surfaceId = surface.Id;
        lastSurfaceOrigin = surface.Minimum;
        velocity = Vector2.Zero;
        perchWalkTime = 0;
        perchTurnPending = placement == CompanionPlacement.Perched && surface.CanContain;
        if (surface.Id == RoamingPlaneId) returningHome = false;
    }
    private void BeginFall() { Placement = CompanionPlacement.Falling; surfaceId = 0; velocity.Y = 0; reactionTime = 0; Pose = CompanionPose.Fall; }
    private void BeginReturn() { Placement = CompanionPlacement.Returning; surfaceId = 0; velocity = Vector2.Zero; reactionTime = 0; Pose = CompanionPose.Walk; returningHome = true; }
    private void BeginBallisticFall() { Placement = CompanionPlacement.Falling; surfaceId = 0; reactionTime = 0; Pose = CompanionPose.Fall; }

    private bool TryStartHop(IReadOnlyList<CompanionSurface> surfaces, Vector2 size, Vector2 minimum, Vector2 maximum,
        CompanionPhysicsOptions options, CompanionSurface? obstacle, ulong? onlyTarget = null, CompanionLayout? confinement = null)
    {
        if (!options.AutoHop || !options.Fall) return false;
        CompanionSurface? best = null; CompanionHop bestHop = default;
        foreach (var target in surfaces)
        {
            if (HopSuppressed(target, size) || target.Id == surfaceId || target.Id == 0 || onlyTarget.HasValue && target.Id != onlyTarget
                || !options.Perch && target.Id != RoamingPlaneId) continue;
            if (!CompanionHop.TryCreate(Position, size, direction > 0, target, minimum, maximum, obstacle, out var planned)) continue;
            if (confinement is { } area && (Math.Min(planned.Start.X, planned.End.X) < area.Minimum.X
                || Math.Max(planned.Start.X, planned.End.X) + size.X > area.Maximum.X
                || planned.Start.Y - planned.Apex < area.Minimum.Y
                || Math.Max(planned.Start.Y, planned.End.Y) + size.Y > area.Maximum.Y)) continue;
            if (best == null || planned.Duration < bestHop.Duration || planned.Duration == bestHop.Duration && target.Order > best.Value.Order)
            { best = target; bestHop = planned; }
        }
        if (best == null) return false;
        hopExcursion = new(best.Value, Position, hasStableGoal ? stableGoal : null);
        hop = bestHop; hopTarget = best.Value; hopSize = size; hopTime = 0; velocity = hop.VelocityAt(0);
        surfaceId = 0; Placement = CompanionPlacement.Jumping; Pose = CompanionPose.Fall;
        return true;
    }

    private void ReturnOnPlanes(IReadOnlyList<CompanionSurface> surfaces, CompanionSurface? home,
        Vector2 minimum, Vector2 maximum, Vector2 size, float dt, float speed,
        CompanionPhysicsOptions options, CompanionSurface? obstacle, Vector2? navigationGoal)
    {
        var destination = navigationGoal ?? (home is { } own
            ? new Vector2((own.Minimum.X + own.Maximum.X - size.X) * .5f, own.Minimum.Y - size.Y * .9f) : new Vector2(float.NaN));
        if (!Finite(destination) || Clamp(destination, minimum, maximum, size) != destination)
        { Pose = CompanionPose.Idle; NavigationStatus = CompanionNavigationStatus.Blocked; return; }
        var destinationFoot = destination + size * new Vector2(.5f, .9f);
        var foot = Position + size * new Vector2(.5f, .9f);
        var floor = maximum.Y - size.Y * .1f;
        var targetSupported = Math.Abs(destinationFoot.Y - floor) < .01f || surfaces.Any(candidate =>
            Math.Abs(candidate.Minimum.Y - destinationFoot.Y) < .01f
            && destinationFoot.X >= candidate.Minimum.X && destinationFoot.X <= candidate.Maximum.X);
        if (!targetSupported) { Pose = CompanionPose.Idle; NavigationStatus = CompanionNavigationStatus.Blocked; return; }
        CompanionSurface? support = null;
        foreach (var candidate in surfaces)
            if (Math.Abs(foot.Y - candidate.Minimum.Y) <= .01f && foot.X >= candidate.Minimum.X && foot.X <= candidate.Maximum.X
                && (support == null || candidate.Order > support.Value.Order)) support = candidate;
        if (!navigationGoal.HasValue && support?.Id == RoamingPlaneId && home is { } savedPlane)
        { Attach(savedPlane, CompanionPlacement.Perched); Pose = CompanionPose.Idle; return; }
        if (support == null && Math.Abs(foot.Y - floor) > .01f)
        { if (options.Fall) BeginFall(); else { Pose = CompanionPose.Idle; NavigationStatus = CompanionNavigationStatus.Blocked; } return; }
        if (Vector2.DistanceSquared(Position, destination) <= .01f)
        { Position = destination; Pose = CompanionPose.Idle; NavigationStatus = CompanionNavigationStatus.Reached; return; }
        var goalX = destination.X;
        var approach = goalX;
        CompanionSurface? route = null;
        var bestScore = float.MaxValue;
        if (options.AutoHop && options.Fall)
        foreach (var candidate in surfaces)
        {
            var rise = foot.Y - candidate.Minimum.Y;
            if (HopSuppressed(candidate, size) || candidate.Id == 0 || candidate.Id == support?.Id || rise <= 0 || rise > size.Y * CompanionHop.MaximumRise + .001f
                || candidate.Minimum.Y < destinationFoot.Y - .01f || !options.Perch && candidate.Id != RoamingPlaneId) continue;
            var leftApproach = candidate.Minimum.X - size.X * .85f;
            var rightApproach = candidate.Maximum.X - size.X * .15f;
            for (var side = 0; side < 2; side++)
            {
                var targetX = side == 0 ? leftApproach : rightApproach;
                var takeoff = new Vector2(targetX, Position.Y);
                if (targetX < minimum.X || targetX + size.X > maximum.X) continue;
                if (CompanionObstacle.TryBounds(obstacle, size, out var blockedMinimum, out var blockedMaximum)
                    && (CompanionObstacle.Inside(takeoff, blockedMinimum, blockedMaximum)
                        || CompanionObstacle.CrossesInterior(Position, takeoff, blockedMinimum, blockedMaximum))) continue;
                // A nearby edge can sit behind the house. Rank only approaches whose
                // horizontal leg and actual bounded jump are clear, including the far edge.
                if (!CompanionHop.TryCreate(takeoff, size, side == 0, candidate, minimum, maximum, obstacle, out _)) continue;
                var score = Math.Abs(candidate.Minimum.Y - destinationFoot.Y) * 2 + Math.Abs(Position.X - targetX);
                if (score < bestScore) { route = candidate; approach = targetX; bestScore = score; }
            }
        }
        if (route == null && destinationFoot.Y < foot.Y - .01f)
        {
            // Walking underneath an unreachable higher goal is not progress.
            // Keep the actual support while a failed route cools down or changes.
            Pose = CompanionPose.Idle; NavigationStatus = CompanionNavigationStatus.Blocked; return;
        }
        if (route is { } next && Math.Abs(Position.X - approach) <= size.X * .2f)
        {
            direction = approach < next.Minimum.X ? 1 : -1;
            if (TryStartHop(surfaces, size, minimum, maximum, options, obstacle, next.Id)) return;
        }
        if (destinationFoot.Y > foot.Y + .01f && support is { } upper)
        {
            var leftExit = upper.Minimum.X - size.X * .5f - 1;
            var rightExit = upper.Maximum.X - size.X * .5f + 1;
            approach = Math.Abs(Position.X - leftExit) < Math.Abs(Position.X - rightExit) ? leftExit : rightExit;
            if (approach < minimum.X || approach + size.X > maximum.X)
                approach = approach == leftExit ? rightExit : leftExit;
        }
        var delta = Math.Clamp(approach - Position.X, -speed * dt, speed * dt);
        var proposed = Clamp(Position + new Vector2(delta, 0), minimum, maximum, size);
        if (CompanionObstacle.TryBounds(obstacle, size, out var first, out var last)
            && (CompanionObstacle.Inside(proposed, first, last) || CompanionObstacle.CrossesInterior(Position, proposed, first, last)))
        { Pose = CompanionPose.Idle; NavigationStatus = CompanionNavigationStatus.Blocked; return; }
        if (support is { } current && (proposed.X + size.X * .5f < current.Minimum.X || proposed.X + size.X * .5f > current.Maximum.X))
        {
            if (options.Fall) { Position = proposed; velocity.X = Math.Sign(delta) * speed; BeginFall(); }
            else { Pose = CompanionPose.Idle; NavigationStatus = CompanionNavigationStatus.Blocked; }
            return;
        }
        Position = proposed;
        if (Math.Abs(delta) > .0001f) direction = Math.Sign(delta);
        Pose = Math.Abs(delta) > .0001f ? CompanionPose.Walk : CompanionPose.Idle;
        if (Pose == CompanionPose.Idle) NavigationStatus = CompanionNavigationStatus.Blocked;
    }

    private void ClearHopMemory()
    {
        hopFailures.Clear(); hopExcursion = null; hasStableGoal = false;
    }

    private static bool SameRouteSurface(CompanionSurface first, CompanionSurface second, Vector2 size)
    {
        // A few pixels of observation jitter is not a new ledge. A moved/reused ID is.
        var tolerance = Math.Max(4, size.Y * .1f);
        return first.Id == second.Id && Vector2.DistanceSquared(first.Minimum, second.Minimum) <= tolerance * tolerance
            && Vector2.DistanceSquared(first.Maximum, second.Maximum) <= tolerance * tolerance;
    }

    private bool HopSuppressed(CompanionSurface target, Vector2 size)
        => hopFailures.Any(failure => (failure.Count >= 3 || hopClock < failure.Until) && SameRouteSurface(failure.Target, target, size));

    private void UpdateHopMemory(IReadOnlyList<CompanionSurface> surfaces, Vector2 minimum, Vector2 maximum,
        Vector2 size, float dt, bool grounded, Vector2? goal, CompanionLayout? area)
    {
        hopClock += dt;
        var materiallyChanged = Vector2.DistanceSquared(routeSize, size) > 1
            || Vector2.DistanceSquared(routeViewportMinimum, minimum) > 16
            || Vector2.DistanceSquared(routeViewportMaximum, maximum) > 16
            || area.HasValue != routeArea.HasValue
            || area is { } nextArea && routeArea is { } previousArea
                && (Vector2.DistanceSquared(nextArea.Minimum, previousArea.Minimum) > size.Y * size.Y * .0625f
                    || Vector2.DistanceSquared(nextArea.Maximum, previousArea.Maximum) > size.Y * size.Y * .0625f)
            || goal.HasValue != hasStableGoal
            || goal is { } destination && Vector2.DistanceSquared(destination, stableGoal) > size.Y * size.Y * .5625f;
        if (!grounded || materiallyChanged)
        {
            ClearHopMemory();
            routeSize = size; routeViewportMinimum = minimum; routeViewportMaximum = maximum; routeArea = area;
            if (goal is { } destinationGoal) { stableGoal = destinationGoal; hasStableGoal = true; }
        }
        if (!grounded) return;
        var foot = Position + size * new Vector2(.5f, .9f);
        CompanionSurface? currentSupport = null;
        foreach (var candidate in surfaces)
            if (Finite(candidate.Minimum) && Finite(candidate.Maximum) && candidate.Size.Y > 0
                && Math.Abs(candidate.Minimum.Y - foot.Y) <= .01f && foot.X >= candidate.Minimum.X && foot.X <= candidate.Maximum.X
                && (currentSupport == null || candidate.Order > currentSupport.Value.Order)) currentSupport = candidate;
        var supported = Placement is (CompanionPlacement.Perched or CompanionPlacement.Floor or CompanionPlacement.Returning)
            && (currentSupport.HasValue || Math.Abs(foot.Y - (maximum.Y - size.Y * .1f)) <= .01f);
        for (var i = hopFailures.Count - 1; i >= 0; i--)
        {
            var failure = hopFailures[i];
            var target = surfaces.FirstOrDefault(candidate => candidate.Id == failure.Target.Id);
            var currentDistance = hasStableGoal ? Vector2.Distance(Position, stableGoal) : 0;
            var progress = supported && currentSupport?.Id != failure.Target.Id && hasStableGoal
                && currentDistance < failure.GoalDistance - size.Y * .75f;
            if (!SameRouteSurface(failure.Target, target, size)) hopFailures.RemoveAt(i);
            else if (progress) hopFailures[i] = failure with { Count = 0, Until = hopClock, GoalDistance = currentDistance };
            // Walking away and back is not a new route. Three failed excursions
            // stay quarantined until observed progress or an actual scene change.
        }
        if (hopExcursion is not { } excursion) return;
        var observed = surfaces.FirstOrDefault(candidate => candidate.Id == excursion.Target.Id);
        if (!SameRouteSurface(excursion.Target, observed, size)) { hopExcursion = null; return; }
        // A temporary gain in height during the jump is not route progress. Judge
        // only after settling back on its departure plane (or a lower support).
        if (!supported || surfaceId == excursion.Target.Id || Position.Y < excursion.Start.Y - size.Y * .1f) return;
        hopExcursion = null;
        if (excursion.Goal is { } targetGoal && Vector2.Distance(Position, targetGoal)
            < Vector2.Distance(excursion.Start, targetGoal) - size.Y * .75f) return;
        var index = hopFailures.FindIndex(failure => SameRouteSurface(failure.Target, excursion.Target, size));
        var count = index >= 0 ? Math.Min(4, hopFailures[index].Count + 1) : 1;
        var until = hopClock + Math.Min(30, 5 * (1 << (count - 1)));
        var distance = hasStableGoal ? Vector2.Distance(Position, stableGoal) : 0;
        var remembered = new HopFailure(excursion.Target, Position, distance, count, until);
        if (index >= 0) hopFailures[index] = remembered;
        else { if (hopFailures.Count == 4) hopFailures.RemoveAt(0); hopFailures.Add(remembered); }
    }

    private static CompanionSurface? CrossedSurface(IReadOnlyList<CompanionSurface> surfaces, Vector2 previous,
        Vector2 next, Vector2 size, Vector2 viewportMinimum, Vector2 viewportMaximum, bool homeOnly = false)
    {
        var foot = previous.Y + size.Y * .9f;
        var descent = next.Y - previous.Y;
        if (descent <= 0) return null;
        CompanionSurface? best = null;
        foreach (var surface in surfaces)
        {
            if (homeOnly && surface.Id != RoamingPlaneId) continue;
            // Strictly cross from above: walking off a ledge must not immediately
            // catch that same plane while the trailing edge still overlaps it.
            if (!Finite(surface.Minimum) || !Finite(surface.Maximum) || surface.Size.Y <= 0
                || !CanPerch(surface, size, viewportMinimum) || surface.Minimum.Y <= foot + .001f
                || surface.Minimum.Y > foot + descent || surface.Minimum.Y + size.Y * .1f > viewportMaximum.Y)
                continue;
            var fraction = (surface.Minimum.Y - foot) / descent;
            var left = float.Lerp(previous.X, next.X, fraction);
            if (left + size.X <= surface.Minimum.X || left >= surface.Maximum.X) continue;
            if (best == null || surface.Minimum.Y < best.Value.Minimum.Y
                || surface.Minimum.Y == best.Value.Minimum.Y && surface.Order > best.Value.Order)
                best = surface;
        }
        return best;
    }

    public static CompanionLayout HomeArea(CompanionLayout layout, bool enabled)
    {
        if (!enabled || !layout.IsVisible) return layout;
        var home = layout.Clamp(new Vector2((layout.Minimum.X + layout.Maximum.X - layout.SpriteSize.X) * .5f,
            layout.Maximum.Y - layout.SpriteSize.Y));
        return new CompanionLayout(Vector2.Max(layout.Minimum, home - layout.SpriteSize * new Vector2(1, .65f)),
            Vector2.Min(layout.Maximum, home + layout.SpriteSize * new Vector2(2, 1)), layout.SpriteSize);
    }

    public static (Vector2 Minimum, Vector2 Maximum) HomeBounds(CompanionLayout layout)
    {
        var room = layout.Maximum - layout.Minimum;
        var edge = Math.Max(0, Math.Min(Math.Min(room.X, room.Y), layout.SpriteSize.Y * 1.3f));
        var top = new Vector2((layout.Minimum.X + layout.Maximum.X - edge) * .5f, layout.Maximum.Y - edge);
        return (top, top + new Vector2(edge));
    }

    public static Vector4 TranslateHomeBox(Vector4 original, Vector2 delta)
    {
        if (!Finite(delta)) return original;
        var minimum = Vector2.Min(new Vector2(original.X, original.Y), new Vector2(original.Z, original.W));
        var maximum = Vector2.Max(new Vector2(original.X, original.Y), new Vector2(original.Z, original.W));
        var translation = Vector2.Clamp(delta, -minimum, Vector2.One - maximum);
        return new Vector4(minimum + translation, maximum.X + translation.X, maximum.Y + translation.Y);
    }

    public static bool HomeFacesLeft(float middle, float viewportLeft, float viewportWidth, bool previous)
        => middle > viewportLeft + viewportWidth * .55f || middle >= viewportLeft + viewportWidth * .45f && previous;

    private void React(CompanionPose pose, float duration)
    {
        reaction = Pose = pose;
        reactionTime = duration;
        reactionFacingLeft = direction < 0;
    }
    private static bool CanPerch(CompanionSurface surface, Vector2 size, Vector2 viewportMinimum)
        => surface.Size.X >= size.X * .5f && surface.Minimum.Y - size.Y * .9f >= viewportMinimum.Y;
    private static bool Fits(Vector2 min, Vector2 max, Vector2 size)
        => Finite(min) && Finite(max) && Finite(size) && size.X > 0 && size.Y > 0 && max.X - min.X >= size.X && max.Y - min.Y >= size.Y;
    private static Vector2 Clamp(Vector2 position, Vector2 min, Vector2 max, Vector2 size)
        => Vector2.Clamp(position, min, Vector2.Max(min, max - size));
    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
