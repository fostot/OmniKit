using System.Numerics;

namespace OmniKit.Ui;

public enum CompanionPose { Automatic, Walk, Idle, Talk, Carry, Fall, Land, Push }

internal readonly record struct CompanionLayout(Vector2 Minimum, Vector2 Maximum, Vector2 SpriteSize)
{
    public bool IsVisible => SpriteSize.X > 0 && SpriteSize.Y > 0;
    public Vector2 LastPosition => Vector2.Max(Minimum, Maximum - SpriteSize);
    public Vector2 Clamp(Vector2 position) => Vector2.Clamp(position, Minimum, LastPosition);

    public static CompanionLayout Create(Vector2 viewportPosition, Vector2 viewportSize,
        Vector4 normalizedBox, float logicalSize, float scale, float aspect)
    {
        if (!Finite(viewportPosition) || !Finite(viewportSize) || viewportSize.X <= 0 || viewportSize.Y <= 0)
            return default;
        if (!float.IsFinite(normalizedBox.X) || !float.IsFinite(normalizedBox.Y)
            || !float.IsFinite(normalizedBox.Z) || !float.IsFinite(normalizedBox.W))
            normalizedBox = new Vector4(.72f, .55f, .95f, .92f);
        var a = Vector2.Clamp(new Vector2(normalizedBox.X, normalizedBox.Y), Vector2.Zero, Vector2.One);
        var b = Vector2.Clamp(new Vector2(normalizedBox.Z, normalizedBox.W), Vector2.Zero, Vector2.One);
        var min = viewportPosition + Vector2.Min(a, b) * viewportSize;
        var max = viewportPosition + Vector2.Max(a, b) * viewportSize;
        var size = Math.Clamp(float.IsFinite(logicalSize) ? logicalSize : 96f, 48f, 192f)
            * Math.Clamp(float.IsFinite(scale) ? scale : 1f, .25f, 4f);
        aspect = float.IsFinite(aspect) && aspect > 0 ? aspect : 1f;
        var sprite = aspect >= 1 ? new Vector2(size, size / aspect) : new Vector2(size * aspect, size);
        var room = max - min;
        var fit = Math.Clamp(Math.Min(room.X / sprite.X, room.Y / sprite.Y), 0, 1);
        return new CompanionLayout(min, max, sprite * fit);
    }

    public static bool TryNormalizeArea(Vector2 start, Vector2 end, Vector2 viewportPosition,
        Vector2 viewportSize, Vector2 minimumSize, out Vector4 normalized)
    {
        normalized = default;
        if (!Finite(start) || !Finite(end) || !Finite(viewportPosition) || !Finite(viewportSize)
            || !Finite(minimumSize) || viewportSize.X <= 0 || viewportSize.Y <= 0)
            return false;
        start = Vector2.Clamp(start, viewportPosition, viewportPosition + viewportSize);
        end = Vector2.Clamp(end, viewportPosition, viewportPosition + viewportSize);
        var min = Vector2.Min(start, end);
        var max = Vector2.Max(start, end);
        if (max.X - min.X < Math.Max(1, minimumSize.X) || max.Y - min.Y < Math.Max(1, minimumSize.Y))
            return false;
        min = (min - viewportPosition) / viewportSize;
        max = (max - viewportPosition) / viewportSize;
        normalized = new Vector4(min, max.X, max.Y);
        return true;
    }

    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}

internal readonly record struct CompanionFrame(Vector2 UvStart, Vector2 UvEnd, Vector2 Position, Vector2 Size);

/// <summary>Walk phase follows distance in the current surface's coordinate space.</summary>
internal sealed class CompanionGait
{
    public const float DefaultCycleDistance = .45f;
    public const float PreviewSpeed = .36f;
    private bool initialized;
    private Vector2 previousPosition;
    private float previousWidth;
    private ulong previousSpace;

    public float Phase { get; private set; }
    public Vector2 Heading { get; private set; } = Vector2.UnitX;
    public int Frame => Math.Clamp((int)(Phase * 8), 0, 7);
    public void Reset() { Phase = 0; Heading = Vector2.UnitX; Rebase(); }
    public void Rebase() => initialized = false;

    public static float DirectionalCycleDistance(float cycleDistance, Vector2 heading)
        => cycleDistance * (1 - .58f * Math.Abs(heading.Y));

    public static bool FacingFor(Vector2 travel, bool previousFacingLeft)
    {
        if (!float.IsFinite(travel.X) || !float.IsFinite(travel.Y) || travel.LengthSquared() < .000001f) return previousFacingLeft;
        var horizontal = travel.X / travel.Length();
        return horizontal < -.24f || horizontal <= .24f && previousFacingLeft;
    }

    public void Update(Vector2 position, float cellWidth, float elapsed, bool walking, bool facingLeft,
        ulong space, float cycleDistance = DefaultCycleDistance, bool preview = false, bool directionalStride = false)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(cellWidth) || cellWidth <= 0)
        { Rebase(); return; }
        var dt = float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0;
        var continuous = initialized && previousWidth == cellWidth && previousSpace == space;
        var delta = position - previousPosition;
        var distance = delta.Length();
        previousPosition = position; previousWidth = cellWidth; previousSpace = space;
        initialized = true;
        if (!walking || dt <= 0) return;
        if (!float.IsFinite(cycleDistance) || cycleDistance < .1f || cycleDistance > 8f) cycleDistance = DefaultCycleDistance;
        if (preview)
        {
            Heading = facingLeft ? -Vector2.UnitX : Vector2.UnitX;
            Phase = (Phase + PreviewSpeed * dt / cycleDistance) % 1f;
        }
        // Relocation, resize, or a changed attachment rebases the sample.
        // It must not race through poses as if the character walked that distance.
        else if (continuous && float.IsFinite(distance) && distance <= Math.Max(2f, cellWidth * dt * 1.25f))
        {
            if (distance > .0001f)
            {
                var desired = delta / distance;
                var angle = MathF.Atan2(Heading.X * desired.Y - Heading.Y * desired.X, Vector2.Dot(Heading, desired));
                var step = Math.Clamp(angle, -MathF.Tau * dt * 2, MathF.Tau * dt * 2);
                var cosine = MathF.Cos(step); var sine = MathF.Sin(step);
                Heading = Vector2.Normalize(new Vector2(Heading.X * cosine - Heading.Y * sine, Heading.X * sine + Heading.Y * cosine));
            }
            if (directionalStride) cycleDistance = DirectionalCycleDistance(cycleDistance, Heading);
            Phase = (Phase + distance / cellWidth / cycleDistance) % 1f;
        }
    }
}

/// <summary>Shared frame selection for the approved companion states.</summary>
internal static class CompanionAtlas
{
    public static int SelectFrame(CompanionPose pose, float seconds, bool animated)
    {
        if (!animated) return 8;
        seconds = float.IsFinite(seconds) ? Math.Max(0, seconds) % 1000 : 0;
        // Time-only callers are previews; automatic walking uses CompanionGait.
        if (pose == CompanionPose.Walk) return (int)(seconds * CompanionGait.PreviewSpeed / CompanionGait.DefaultCycleDistance * 8) % 8;
        if (pose == CompanionPose.Talk) return 12 + (int)(seconds * 6) % 4;
        if (pose == CompanionPose.Carry) return 16 + (int)(seconds * 3) % 2;
        if (pose == CompanionPose.Fall) return 18 + (int)(seconds * 4) % 2;
        if (pose == CompanionPose.Land) return seconds < .22f ? 20 : 21;
        if (pose == CompanionPose.Push) return 22 + (int)(seconds * 3) % 2;
        // A short blink at the end of each relaxed four-second idle loop.
        return (seconds % 4) switch { < 3.6f => 8, < 3.72f => 9, < 3.84f => 10, _ => 11 };
    }

}

/// <summary>Frame-rate independent roaming with no game or renderer dependencies.</summary>
internal sealed class CompanionMotion(int seed = 0)
{
    private readonly Random random = seed == 0 ? new Random() : new Random(seed);
    private bool initialized;
    private Vector2 destination;
    private float pause;
    private readonly CompanionGait gait = new();

    public Vector2 Position { get; private set; }
    public bool FacingLeft { get; private set; }
    public bool Walking { get; private set; }
    public int Frame => Walking ? gait.Frame : 8;

    public void Reset() { initialized = false; Walking = false; gait.Reset(); }

    public void ResumeAt(Vector2 position, CompanionLayout layout)
    {
        initialized = true; Position = layout.Clamp(position); destination = Position; pause = 0;
        Walking = false; gait.Rebase();
    }

    public void Update(CompanionLayout layout, float elapsed, bool animate, CompanionSurface? obstacle = null)
    {
        UpdateMovement(layout, elapsed, animate, obstacle);
        gait.Update(Position - layout.Minimum, layout.SpriteSize.X, elapsed, Walking, FacingLeft, 0);
    }

    private void UpdateMovement(CompanionLayout layout, float elapsed, bool animate, CompanionSurface? obstacle)
    {
        if (!layout.IsVisible) { Walking = false; return; }
        if (!initialized)
        {
            initialized = true;
            Position = new Vector2(layout.Minimum.X, (layout.Minimum.Y + layout.LastPosition.Y) * .5f);
            destination = new Vector2(layout.LastPosition.X, Position.Y);
            FacingLeft = false;
            pause = 0;
        }
        Position = CompanionObstacle.Destination(Position, layout.SpriteSize, layout.Minimum, layout.Maximum, obstacle);
        destination = CompanionObstacle.Destination(destination, layout.SpriteSize, layout.Minimum, layout.Maximum, obstacle);
        Walking = false;
        if (!animate) return;
        elapsed = float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0;
        if (elapsed <= 0) return;
        if (pause > 0)
        {
            pause = Math.Max(0, pause - elapsed);
            if (pause == 0)
                destination = layout.Minimum + (layout.LastPosition - layout.Minimum)
                    * new Vector2(random.NextSingle(), random.NextSingle());
            return;
        }

        var delta = destination - Position;
        var distance = delta.Length();
        if (distance <= .5f)
        {
            Position = destination;
            pause = .8f + random.NextSingle() * 1.7f;
            return;
        }
        var step = Math.Min(distance, Math.Max(8f, layout.SpriteSize.Y * .36f) * elapsed);
        var next = CompanionObstacle.Step(Position, destination, layout.SpriteSize, step, layout.Minimum, layout.Maximum, obstacle);
        Walking = Vector2.DistanceSquared(Position, next) > .00001f;
        FacingLeft = CompanionGait.FacingFor(next - Position, FacingLeft);
        Position = next;
    }
}
