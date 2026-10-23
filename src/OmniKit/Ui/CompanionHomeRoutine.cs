using System.Numerics;

namespace OmniKit.Ui;

internal readonly record struct CompanionHomeRect(Vector2 Minimum, Vector2 Maximum)
{
    public Vector2 Size => Maximum - Minimum;
    public Vector2 At(float x, float y) => Minimum + Size * new Vector2(x, y);
}

internal static class CompanionHomeGeometry
{
    public static CompanionHomeRect Bounds(CompanionLayout layout, Vector2 viewportPosition,
        Vector2 viewportSize, Vector2 anchor)
    {
        var edge = Math.Max(0, Math.Min(Math.Min(viewportSize.X, viewportSize.Y), layout.SpriteSize.Y * 1.3f));
        if (!float.IsFinite(anchor.X) || !float.IsFinite(anchor.Y)) anchor = new(.835f, .92f);
        var desired = viewportPosition + Vector2.Clamp(anchor, Vector2.Zero, Vector2.One) * viewportSize
            - new Vector2(edge * .5f, edge);
        var minimum = Vector2.Clamp(desired, viewportPosition,
            Vector2.Max(viewportPosition, viewportPosition + viewportSize - new Vector2(edge)));
        return new(minimum, minimum + new Vector2(edge));
    }

    public static Vector2 Outside(CompanionLayout layout, CompanionHomeRect home, bool facesLeft, bool grounded = false)
        => new(home.At(facesLeft ? .3365f : .6635f, 0).X - layout.SpriteSize.X * .5f,
            home.Maximum.Y + 4 - (grounded ? layout.SpriteSize.Y * .9f : 0));

    public static Vector2 Door(CompanionHomeRect home, Vector2 sprite, bool facesLeft)
        => home.At(facesLeft ? .3365f : .6635f, .872f) - sprite * new Vector2(.5f, .9f);

    // Measured from the original cropped house image: source x600..1040.
    // Keep the bed and roof masking heights used by the accepted nap painter.
    public static CompanionHomeRect Opening(CompanionHomeRect home, bool facesLeft)
        => new(home.At(facesLeft ? .133f : .46f, .55f), home.At(facesLeft ? .54f : .867f, .89f));

    public static Vector2 Corner(CompanionLayout layout, CompanionHomeRect home, bool facesLeft, bool grounded = false)
        => new(facesLeft ? home.Minimum.X - layout.SpriteSize.X - 4 : home.Maximum.X + 4,
            home.Maximum.Y + 4 - (grounded ? layout.SpriteSize.Y * .9f : 0));
}

internal enum CompanionHomeState { Roaming, Approaching, Entering, Settling, Sleeping, Waking, Leaving, Returning }
internal readonly record struct CompanionHomeTravel(Vector2 Position, bool FacingLeft, CompanionPose Pose, bool Blocked);

/// <summary>Local cosmetic behavior only. It cannot query AI or act on game objects.</summary>
internal sealed class CompanionHomeRoutine(int seed = 0)
{
    private readonly Random random = seed == 0 ? new Random() : new Random(seed);
    private float awakeRemaining;
    private float stateTime;
    private int approachLeg;
    private Vector2 entryStart;
    private Vector2 lastHouseMinimum;
    private Vector2 lastHouseMaximum;
    private Vector2 returnDestination;
    private Vector2 viewportMinimum;
    private Vector2 viewportMaximum;
    private Vector2 leaveTarget;
    private Vector2 leaveClearance;
    private Vector2 leaveCorner;
    private float leaveScale;
    private bool leaveFacesLeft;
    private bool lastFacesLeft;
    private bool cancellationPending;
    private Vector2 spriteSize;
    private bool houseAvailable;
    private CompanionHomeStyleFrame? lastStyleFrame;
    private Func<Vector2, Vector2, float, bool, CompanionHomeTravel>? navigator;
    private float restartInterval = 45;
    private float travelTime;
    private float travelStallTime;
    internal const float NavigationStallSeconds = 8;
    internal const float NavigationTimeoutSeconds = 120;
    internal const float IndoorScale = .558f;
    public CompanionHomeState State { get; private set; }
    public bool Active => State != CompanionHomeState.Roaming;
    public bool UsesNapArt => State is CompanionHomeState.Entering or CompanionHomeState.Settling
        or CompanionHomeState.Sleeping or CompanionHomeState.Waking || State == CompanionHomeState.Leaving && approachLeg < 3;
    public bool InFrontOfHome => State == CompanionHomeState.Leaving && approachLeg >= 1;
    public bool AccessoriesVisible => State is CompanionHomeState.Roaming or CompanionHomeState.Approaching
        or CompanionHomeState.Returning || State == CompanionHomeState.Leaving && approachLeg >= 2;
    public bool Sleeping => State == CompanionHomeState.Sleeping;
    public Vector2 Position { get; private set; }
    public bool FacingLeft { get; private set; }
    public CompanionPose TravelPose { get; private set; } = CompanionPose.Idle;
    public float ArtScale { get; private set; } = 1;
    public float SleepPhase => stateTime;
    public int ArtFrame => State switch
    {
        CompanionHomeState.Entering or CompanionHomeState.Leaving => 0,
        CompanionHomeState.Settling or CompanionHomeState.Waking => 1,
        _ => 2
    };

    public void Reset(float interval = 45)
    {
        State = CompanionHomeState.Roaming;
        stateTime = 0; approachLeg = 0; ArtScale = 1;
        cancellationPending = false;
        travelTime = 0; travelStallTime = 0; TravelPose = CompanionPose.Idle;
        awakeRemaining = Safe(interval, 15, 300, 45) * (.8f + random.NextSingle() * .4f);
    }

    public void Update(float elapsed, bool enabled, bool paused, Vector2 current, bool facingLeft,
        CompanionLayout layout, CompanionHomeRect house, bool facesLeft, float interval, float duration,
        Vector2 viewportMinimum, Vector2 viewportMaximum, bool houseVisible = true,
        CompanionHomeStyleFrame? styleFrame = null,
        Func<Vector2, Vector2, float, bool, CompanionHomeTravel>? navigator = null)
    {
        this.viewportMinimum = viewportMinimum; this.viewportMaximum = viewportMaximum;
        this.navigator = navigator; restartInterval = Safe(interval, 15, 300, 45);
        if (styleFrame is { } fitted) { house = new(fitted.Minimum, fitted.Maximum); facesLeft = fitted.Mirrored; }
        spriteSize = layout.SpriteSize; houseAvailable = houseVisible;
        var dt = float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0;
        if (!layout.IsVisible || !enabled && !Active)
        {
            if (Active || awakeRemaining <= 0) Reset(interval);
            return;
        }
        if (paused) return;
        if (Active && !cancellationPending && (!enabled || house.Minimum != lastHouseMinimum || house.Maximum != lastHouseMaximum
            || styleFrame != lastStyleFrame))
        {
            cancellationPending = true;
            if (UsesNapArt)
            {
                var oldHouse = new CompanionHomeRect(lastHouseMinimum, lastHouseMaximum);
                BeginLeaving(layout, oldHouse, lastFacesLeft, lastStyleFrame);
            }
            else SetState(CompanionHomeState.Returning);
        }
        lastHouseMinimum = house.Minimum; lastHouseMaximum = house.Maximum; lastFacesLeft = facesLeft; lastStyleFrame = styleFrame;
        if (!Active)
        {
            if (awakeRemaining <= 0) { Reset(interval); return; }
            awakeRemaining -= dt;
            if (awakeRemaining > 0) return;
            Position = current; FacingLeft = facingLeft;
            returnDestination = layout.Clamp(current);
            State = CompanionHomeState.Approaching; approachLeg = 0;
        }
        stateTime += dt;
        var lastPosition = Vector2.Max(viewportMinimum, viewportMaximum - layout.SpriteSize);
        var outside = Vector2.Clamp(styleFrame?.OutsidePosition(layout.SpriteSize, grounded: navigator != null)
            ?? CompanionHomeGeometry.Outside(layout, house, facesLeft, navigator != null), viewportMinimum, lastPosition);
        var corner = Vector2.Clamp(CompanionHomeGeometry.Corner(layout, house, facesLeft, navigator != null), viewportMinimum, lastPosition);
        var door = styleFrame?.BedPosition(layout.SpriteSize) ?? CompanionHomeGeometry.Door(house, layout.SpriteSize, facesLeft);
        if (State == CompanionHomeState.Approaching)
        {
            var target = approachLeg == 0 ? corner : outside;
            if (WalkTo(target, layout, house, dt, forecourt: approachLeg == 1))
            {
                if (approachLeg++ == 0) return;
                entryStart = Position; FacingLeft = !facesLeft;
                TravelPose = CompanionPose.Idle;
                SetState(CompanionHomeState.Entering);
            }
        }
        else if (State == CompanionHomeState.Entering)
        {
            var t = Smooth(stateTime / 1.4f);
            ArtScale = float.Lerp(1, IndoorScale, t);
            Position = Vector2.Lerp(entryStart, door, t);
            if (stateTime >= 1.4f) SetState(CompanionHomeState.Settling);
        }
        else if (State == CompanionHomeState.Settling)
        {
            if (stateTime >= .7f) SetState(CompanionHomeState.Sleeping);
        }
        else if (State == CompanionHomeState.Sleeping)
        {
            if (stateTime >= Safe(duration, 5, 120, 20)) SetState(CompanionHomeState.Waking);
        }
        else if (State == CompanionHomeState.Waking)
        {
            if (stateTime >= 1)
            {
                BeginLeaving(layout, house, facesLeft, styleFrame);
            }
        }
        else if (State == CompanionHomeState.Leaving)
        {
            if (approachLeg == 0)
            {
                var t = Smooth(stateTime / 1.4f);
                Position = Vector2.Lerp(entryStart, leaveTarget, t); ArtScale = leaveScale;
                FacingLeft = leaveFacesLeft;
                if (stateTime >= 1.4f) { approachLeg = 1; stateTime = 0; }
            }
            else if (approachLeg == 1)
            {
                // Take a visible step away from the opening while still small.
                // This short foreground path clears the door before growth.
                Position = Vector2.Lerp(leaveTarget, leaveClearance, Smooth(stateTime / .45f));
                ArtScale = leaveScale;
                if (stateTime >= .45f) { approachLeg = 2; stateTime = 0; }
            }
            else if (approachLeg == 2)
            {
                // The visitor has reached the outside threshold. It now paints
                // in front of the house, so growth cannot pass through its roof.
                ArtScale = float.Lerp(leaveScale, 1, Smooth(stateTime / .8f));
                if (stateTime >= .8f) { approachLeg = 3; stateTime = 0; }
            }
            else if (WalkTo(leaveCorner, layout, house, dt, forecourt: true)) SetState(CompanionHomeState.Returning);
        }
        else if (State == CompanionHomeState.Returning)
        {
            CompanionSurface? obstacle = houseAvailable ? new CompanionSurface(0, house.Minimum, house.Maximum, 0, 0) : null;
            var target = CompanionObstacle.Destination(returnDestination, layout.SpriteSize, layout.Minimum, layout.Maximum, obstacle);
            if (WalkTo(target, layout, house, dt)) Reset(interval);
        }
    }
    private void BeginLeaving(CompanionLayout layout, CompanionHomeRect house, bool facesLeft, CompanionHomeStyleFrame? styleFrame = null)
    {
        entryStart = Position; leaveScale = ArtScale; leaveFacesLeft = facesLeft;
        var lastPosition = Vector2.Max(viewportMinimum, viewportMaximum - spriteSize);
        leaveTarget = Vector2.Clamp(styleFrame?.OutsidePosition(layout.SpriteSize, grounded: navigator != null)
            ?? CompanionHomeGeometry.Outside(layout, house, facesLeft, navigator != null), viewportMinimum, lastPosition);
        leaveCorner = Vector2.Clamp(CompanionHomeGeometry.Corner(layout, house, facesLeft, navigator != null), viewportMinimum, lastPosition);
        leaveClearance = Vector2.Clamp(leaveTarget + new Vector2((facesLeft ? -1 : 1) * spriteSize.X * .35f, 0), viewportMinimum, lastPosition);
        approachLeg = 0; SetState(CompanionHomeState.Leaving);
    }

    private bool WalkTo(Vector2 destination, CompanionLayout layout, CompanionHomeRect home, float dt, bool forecourt = false)
    {
        if (navigator != null)
        {
            if (dt <= 0) return false;
            var before = Position;
            var travel = navigator(before, destination, dt, forecourt);
            var finite = float.IsFinite(travel.Position.X) && float.IsFinite(travel.Position.Y);
            if (finite)
            {
                Position = travel.Position; FacingLeft = travel.FacingLeft;
                TravelPose = Enum.IsDefined(travel.Pose) ? travel.Pose : CompanionPose.Idle;
            }
            travelTime += dt;
            travelStallTime = finite && Vector2.DistanceSquared(Position, before) > .0001f ? 0 : travelStallTime + dt;
            if (!finite || travel.Blocked || travelStallTime >= NavigationStallSeconds || travelTime >= NavigationTimeoutSeconds)
            {
                // Preserve the actual reached point for the caller's physics handoff.
                // A failed grounded route must never silently switch to free-space travel.
                var pose = TravelPose;
                Reset(restartInterval); TravelPose = pose;
                return false;
            }
            return Vector2.DistanceSquared(Position, destination) < .01f;
        }
        CompanionSurface? obstacle = houseAvailable ? new CompanionSurface(0, home.Minimum, home.Maximum, 0, 0) : null;
        // The nap trip may leave the drawn area. Normal roaming never uses these bounds.
        var minimum = viewportMinimum;
        var maximum = viewportMaximum;
        Vector2 next;
        if (forecourt)
        {
            // The final approach walks across the visible foreground threshold,
            // before nap art or wall occlusion starts. At the screen bottom this
            // lane shares the house's ground plane; never move the saved house.
            destination = Vector2.Clamp(destination, minimum, Vector2.Max(minimum, maximum - layout.SpriteSize));
            var distance = Vector2.Distance(Position, destination);
            next = distance <= layout.SpriteSize.Y * .74f * dt ? destination
                : Position + (destination - Position) / distance * (layout.SpriteSize.Y * .74f * dt);
        }
        else
        {
            destination = CompanionObstacle.Destination(destination, layout.SpriteSize, minimum, maximum, obstacle);
            next = CompanionObstacle.Step(Position, destination, layout.SpriteSize, layout.SpriteSize.Y * .74f * dt,
                minimum, maximum, obstacle);
        }
        var delta = next - Position;
        FacingLeft = CompanionGait.FacingFor(delta, FacingLeft);
        TravelPose = delta.LengthSquared() > .0001f ? CompanionPose.Walk : CompanionPose.Idle;
        Position = next;
        return Vector2.DistanceSquared(Position, destination) < .01f;
    }
    private void SetState(CompanionHomeState state) { State = state; stateTime = 0; }
    private static float Smooth(float t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
    private static float Safe(float value, float min, float max, float fallback)
        => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
