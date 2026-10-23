using System.Numerics;

namespace OmniKit.Ui;

internal readonly record struct CompanionPhysicsOptions(bool Perch, bool Contain, bool Fall);
internal enum CompanionPlacement { None, Carried, Perched, Contained, Falling, Floor, Returning }

/// <summary>Companion-only motion. Surfaces are immutable observations; no foreign UI is changed.</summary>
internal sealed class CompanionPhysics
{
    private ulong surfaceId;
    private Vector2 lastSurfaceOrigin;
    private Vector2 grabOffset;
    private Vector2 velocity;
    private float reactionTime;
    private CompanionPose reaction;
    private bool reactionFacingLeft;
    private float direction = 1;
    private float perchWalkTime;
    private bool perchTurnPending;

    public CompanionPlacement Placement { get; private set; }
    public bool Active => Placement != CompanionPlacement.None;
    public bool IsCarried => Placement == CompanionPlacement.Carried;
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
    }

    public void PickUp(Vector2 position, Vector2 pointer, bool facingLeft)
    {
        Position = position;
        grabOffset = pointer - position;
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
        var next = Clamp(pointer - grabOffset, viewportMinimum, viewportMaximum, size);
        if (elapsed > .001f && float.IsFinite(elapsed))
            velocity.X = Math.Clamp((next.X - Position.X) / elapsed, -180, 180);
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
            if (options.Perch && pointer.Y <= selected.Minimum.Y + topBand && CanPerch(selected, size, viewportMinimum))
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
            if (options.Contain && Fits(innerMinimum, innerMaximum, size))
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
        Vector2? homePosition = null, CompanionSurface? obstacle = null)
    {
        if (!Active || !Fits(viewportMinimum, viewportMaximum, size)) { Reset(); return; }
        ClipMinimum = viewportMinimum;
        ClipMaximum = viewportMaximum;
        var dt = float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0;
        CompanionSurface? attached = null;
        if (Placement is CompanionPlacement.Perched or CompanionPlacement.Contained)
        {
            foreach (var surface in surfaces) if (surface.Id == surfaceId) { attached = surface; break; }
            if (attached == null || Placement == CompanionPlacement.Perched && !options.Perch
                || Placement == CompanionPlacement.Contained && !options.Contain)
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
        if (Placement == CompanionPlacement.Falling && !options.Fall) BeginReturn();
        // A resized viewport must contain the entire cell even while animation is frozen.
        Position = Clamp(Position, viewportMinimum, viewportMaximum, size);
        if (!animate || dt <= 0) { Pose = CompanionPose.Idle; return; }
        if (hoverPaused && Placement != CompanionPlacement.Falling && reactionTime <= 0) { Pose = CompanionPose.Idle; return; }
        if (reactionTime > 0)
        {
            reactionTime = Math.Max(0, reactionTime - dt);
            Pose = reaction;
            return;
        }

        var avoidHouse = CompanionObstacle.TryBounds(obstacle, size, out var blockMinimum, out var blockMaximum);
        if (avoidHouse && Placement == CompanionPlacement.Falling && CompanionObstacle.Inside(Position, blockMinimum, blockMaximum))
            Position = CompanionObstacle.NearestExit(Position, blockMinimum, blockMaximum, viewportMinimum, viewportMaximum - size);

        var speed = Math.Max(16, size.Y * .34f);
        if (Placement == CompanionPlacement.Floor && homePosition.HasValue) BeginReturn();
        if (Placement == CompanionPlacement.Returning)
        {
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
            if (options.Perch && CrossedSurface(surfaces, previous, Position, size, viewportMinimum, viewportMaximum)
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
        perchTurnPending = placement == CompanionPlacement.Perched;
    }
    private void BeginFall() { Placement = CompanionPlacement.Falling; surfaceId = 0; velocity.Y = 0; reactionTime = 0; Pose = CompanionPose.Fall; }
    private void BeginReturn() { Placement = CompanionPlacement.Returning; surfaceId = 0; velocity = Vector2.Zero; reactionTime = 0; Pose = CompanionPose.Walk; }

    private static CompanionSurface? CrossedSurface(IReadOnlyList<CompanionSurface> surfaces, Vector2 previous,
        Vector2 next, Vector2 size, Vector2 viewportMinimum, Vector2 viewportMaximum)
    {
        var foot = previous.Y + size.Y * .9f;
        var descent = next.Y - previous.Y;
        if (descent <= 0) return null;
        CompanionSurface? best = null;
        foreach (var surface in surfaces)
        {
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
