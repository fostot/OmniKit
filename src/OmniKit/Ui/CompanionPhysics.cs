using System.Numerics;

namespace OmniKit.Ui;

internal readonly record struct CompanionSurface(ulong Id, Vector2 Minimum, Vector2 Maximum, float HeaderHeight, int Order)
{
    public Vector2 Size => Maximum - Minimum;
    public bool Contains(Vector2 point) => point.X >= Minimum.X && point.Y >= Minimum.Y && point.X <= Maximum.X && point.Y <= Maximum.Y;
}

internal readonly record struct CompanionPhysicsOptions(bool Perch, bool Contain, bool Fall);
internal enum CompanionPlacement { None, Carried, Perched, Contained, Falling, Floor }

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
        if (options.Fall) BeginFall(); else Reset();
    }

    public void Update(IReadOnlyList<CompanionSurface> surfaces, Vector2 viewportMinimum, Vector2 viewportMaximum,
        Vector2 size, float elapsed, bool animate, bool hoverPaused, CompanionPhysicsOptions options)
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
                || Placement == CompanionPlacement.Contained && !options.Contain) { Reset(); return; }
            var current = attached.Value;
            Position += current.Minimum - lastSurfaceOrigin;
            lastSurfaceOrigin = current.Minimum;
            if (Placement == CompanionPlacement.Contained)
            {
                ClipMinimum = Vector2.Max(viewportMinimum, current.Minimum + new Vector2(4, Math.Max(4, current.HeaderHeight)));
                ClipMaximum = Vector2.Min(viewportMaximum, current.Maximum - new Vector2(4));
                if (!Fits(ClipMinimum, ClipMaximum, size)) { Reset(); return; }
                Position = Clamp(Position, ClipMinimum, ClipMaximum, size);
            }
            else
            {
                if (!CanPerch(current, size, viewportMinimum)) { Reset(); return; }
                Position = new Vector2(Position.X, current.Minimum.Y - size.Y * .9f);
                Position = Clamp(Position, viewportMinimum, viewportMaximum, size);
            }
        }
        if (IsCarried) { Pose = animate ? CompanionPose.Carry : CompanionPose.Idle; return; }
        if (Placement == CompanionPlacement.Falling && !options.Fall) { Reset(); return; }
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

        var speed = Math.Max(16, size.Y * .34f);
        if (Placement == CompanionPlacement.Falling)
        {
            var oldFoot = Position.Y + size.Y * .9f;
            velocity.Y = Math.Min(700, velocity.Y + 900 * dt);
            Position += velocity * dt;
            Position = new Vector2(Math.Clamp(Position.X, viewportMinimum.X, viewportMaximum.X - size.X), Position.Y);
            var foot = Position.Y + size.Y * .9f;
            var center = Position.X + size.X * .5f;
            CompanionSurface? landing = null;
            if (options.Perch)
                foreach (var surface in surfaces)
                    if (CanPerch(surface, size, viewportMinimum) && center >= surface.Minimum.X && center <= surface.Maximum.X
                        && oldFoot <= surface.Minimum.Y && foot >= surface.Minimum.Y
                        && (landing == null || surface.Minimum.Y < landing.Value.Minimum.Y
                            || surface.Minimum.Y == landing.Value.Minimum.Y && surface.Order > landing.Value.Order)) landing = surface;
            if (landing is { } ledge)
            {
                Attach(ledge, CompanionPlacement.Perched);
                Position = new Vector2(Position.X, ledge.Minimum.Y - size.Y * .9f);
                React(CompanionPose.Land, .5f);
            }
            else if (Position.Y + size.Y >= viewportMaximum.Y)
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
    }
    private void BeginFall() { Placement = CompanionPlacement.Falling; surfaceId = 0; velocity.Y = 0; Pose = CompanionPose.Fall; }
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
