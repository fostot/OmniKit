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

    public static Vector2 Outside(CompanionLayout layout, CompanionHomeRect home, bool facesLeft)
        => new(facesLeft ? home.Minimum.X - layout.SpriteSize.X * 1.04f : home.Maximum.X + layout.SpriteSize.X * .04f,
            home.Maximum.Y - layout.SpriteSize.Y);

    public static Vector2 Door(CompanionHomeRect home, Vector2 sprite, bool facesLeft)
        => home.At(facesLeft ? .31f : .69f, .87f) - sprite * new Vector2(.5f, .9f);

    public static Vector2 Corner(CompanionLayout layout, CompanionHomeRect home, bool facesLeft)
        => new(Outside(layout, home, facesLeft).X, home.Minimum.Y - layout.SpriteSize.Y * 1.05f);
}

internal enum CompanionHomeState { Roaming, Approaching, Entering, Settling, Sleeping, Waking, Leaving, Returning }

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
    private float leaveScale;
    private bool lastFacesLeft;
    private bool cancellationPending;
    private Vector2 spriteSize;
    private bool houseAvailable;
    public CompanionHomeState State { get; private set; }
    public bool Active => State != CompanionHomeState.Roaming;
    public bool UsesNapArt => State is CompanionHomeState.Entering or CompanionHomeState.Settling
        or CompanionHomeState.Sleeping or CompanionHomeState.Waking || State == CompanionHomeState.Leaving && approachLeg == 0;
    public bool Sleeping => State == CompanionHomeState.Sleeping;
    public Vector2 Position { get; private set; }
    public bool FacingLeft { get; private set; }
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
        awakeRemaining = Safe(interval, 15, 300, 45) * (.8f + random.NextSingle() * .4f);
    }

    public void Update(float elapsed, bool enabled, bool paused, Vector2 current, bool facingLeft,
        CompanionLayout layout, CompanionHomeRect house, bool facesLeft, float interval, float duration,
        Vector2 viewportMinimum, Vector2 viewportMaximum, bool houseVisible = true)
    {
        this.viewportMinimum = viewportMinimum; this.viewportMaximum = viewportMaximum;
        spriteSize = layout.SpriteSize; houseAvailable = houseVisible;
        var dt = float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0;
        if (!layout.IsVisible || !enabled && !Active)
        {
            if (Active || awakeRemaining <= 0) Reset(interval);
            return;
        }
        if (paused) return;
        if (Active && !cancellationPending && (!enabled || house.Minimum != lastHouseMinimum || house.Maximum != lastHouseMaximum))
        {
            cancellationPending = true;
            if (UsesNapArt)
            {
                var oldHouse = new CompanionHomeRect(lastHouseMinimum, lastHouseMaximum);
                BeginLeaving(CompanionHomeGeometry.Outside(layout, oldHouse, lastFacesLeft));
            }
            else SetState(CompanionHomeState.Returning);
        }
        lastHouseMinimum = house.Minimum; lastHouseMaximum = house.Maximum; lastFacesLeft = facesLeft;
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
        var outside = Vector2.Clamp(CompanionHomeGeometry.Outside(layout, house, facesLeft), viewportMinimum, lastPosition);
        var corner = Vector2.Clamp(CompanionHomeGeometry.Corner(layout, house, facesLeft), viewportMinimum, lastPosition);
        var door = CompanionHomeGeometry.Door(house, layout.SpriteSize, facesLeft);
        if (State == CompanionHomeState.Approaching)
        {
            var target = approachLeg == 0 ? corner : outside;
            if (WalkTo(target, layout, house, dt))
            {
                if (approachLeg++ == 0) return;
                entryStart = Position; FacingLeft = !facesLeft;
                SetState(CompanionHomeState.Entering);
            }
        }
        else if (State == CompanionHomeState.Entering)
        {
            var t = Smooth(stateTime / 1.4f);
            ArtScale = float.Lerp(1, .62f, t);
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
                BeginLeaving(outside);
            }
        }
        else if (State == CompanionHomeState.Leaving)
        {
            if (approachLeg == 0)
            {
                var t = Smooth(stateTime / 1.4f);
                Position = Vector2.Lerp(entryStart, leaveTarget, t); ArtScale = float.Lerp(leaveScale, 1, t);
                FacingLeft = facesLeft;
                if (stateTime >= 1.4f) { approachLeg = 1; SetState(CompanionHomeState.Returning); }
            }
        }
        else if (State == CompanionHomeState.Returning)
        {
            CompanionSurface? obstacle = houseAvailable ? new CompanionSurface(0, house.Minimum, house.Maximum, 0, 0) : null;
            var target = CompanionObstacle.Destination(returnDestination, layout.SpriteSize, layout.Minimum, layout.Maximum, obstacle);
            if (WalkTo(target, layout, house, dt)) Reset(interval);
        }
    }
    private void BeginLeaving(Vector2 outside)
    {
        entryStart = Position; leaveScale = ArtScale;
        leaveTarget = Vector2.Clamp(outside, viewportMinimum, Vector2.Max(viewportMinimum, viewportMaximum - spriteSize));
        approachLeg = 0; SetState(CompanionHomeState.Leaving);
    }

    private bool WalkTo(Vector2 destination, CompanionLayout layout, CompanionHomeRect home, float dt)
    {
        CompanionSurface? obstacle = houseAvailable ? new CompanionSurface(0, home.Minimum, home.Maximum, 0, 0) : null;
        // The nap trip may leave the drawn area. Normal roaming never uses these bounds.
        var minimum = viewportMinimum;
        var maximum = viewportMaximum;
        destination = CompanionObstacle.Destination(destination, layout.SpriteSize, minimum, maximum, obstacle);
        var next = CompanionObstacle.Step(Position, destination, layout.SpriteSize, layout.SpriteSize.Y * .74f * dt,
            minimum, maximum, obstacle);
        var delta = next - Position;
        FacingLeft = CompanionGait.FacingFor(delta, FacingLeft);
        Position = next;
        return Vector2.DistanceSquared(Position, destination) < .01f;
    }
    private void SetState(CompanionHomeState state) { State = state; stateTime = 0; }
    private static float Smooth(float t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
    private static float Safe(float value, float min, float max, float fallback)
        => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
