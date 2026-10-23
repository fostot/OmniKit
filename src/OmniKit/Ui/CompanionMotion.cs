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

/// <summary>Inspected atlas regions, rendered at a common scale and foot baseline.</summary>
internal static class CompanionAtlas
{
    // Generated rows are not exactly quarter-height. These padded regions contain
    // each complete pose without a neighbor, shared by both outfits of a species.
    private static readonly Vector4[] Bimble =
    [
        new(54, 20, 284, 323), new(355, 29, 586, 328), new(656, 18, 897, 319), new(975, 18, 1206, 327),
        new(48, 337, 280, 646), new(365, 340, 596, 645), new(660, 339, 899, 642), new(973, 338, 1207, 645),
        new(76, 655, 283, 941), new(379, 655, 587, 942), new(685, 655, 893, 941), new(1000, 655, 1203, 942),
        new(74, 946, 283, 1235), new(384, 945, 592, 1235), new(687, 946, 898, 1235), new(994, 945, 1206, 1235),
    ];
    private static readonly Vector4[] Moonbear =
    [
        new(72, 64, 279, 325), new(382, 70, 583, 327), new(684, 64, 886, 328), new(996, 64, 1200, 328),
        new(72, 373, 279, 636), new(384, 375, 585, 636), new(687, 373, 889, 632), new(997, 373, 1202, 634),
        new(77, 666, 277, 929), new(384, 665, 584, 929), new(684, 666, 887, 929), new(997, 666, 1198, 929),
        new(74, 949, 279, 1216), new(383, 949, 586, 1216), new(684, 948, 889, 1216), new(996, 948, 1202, 1216),
    ];

    public static string FileName(string avatar, string outfit)
        => $"{(avatar == "Moonbear" ? "moonbear" : "bimble")}-{(outfit == "Stargazer" ? "stargazer" : "motion")}.png";

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

    public static CompanionFrame GetFrame(string avatar, int frame, bool facingLeft, Vector2 position, Vector2 cellSize)
    {
        var bear = avatar == "Moonbear";
        var rect = (bear ? Moonbear : Bimble)[Math.Clamp(frame, 0, 15)];
        var start = new Vector2(rect.X, rect.Y) / 1254f;
        var end = new Vector2(rect.Z, rect.W) / 1254f;
        if (facingLeft) (start.X, end.X) = (end.X, start.X);
        var size = new Vector2(rect.Z - rect.X, rect.W - rect.Y) / (bear ? 330f : 380f) * cellSize;
        return new CompanionFrame(start, end,
            position + new Vector2((cellSize.X - size.X) * .5f, cellSize.Y * .9f - size.Y), size);
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

    public void Update(CompanionLayout layout, float elapsed, bool animate)
    {
        UpdateMovement(layout, elapsed, animate);
        gait.Update(Position - layout.Minimum, layout.SpriteSize.X, elapsed, Walking, FacingLeft, 0);
    }

    private void UpdateMovement(CompanionLayout layout, float elapsed, bool animate)
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
        Position = layout.Clamp(Position);
        destination = layout.Clamp(destination);
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
        Walking = true;
        FacingLeft = CompanionGait.FacingFor(delta, FacingLeft);
        var step = Math.Min(distance, Math.Max(8f, layout.SpriteSize.Y * .36f) * elapsed);
        Position = layout.Clamp(Position + delta / distance * step);
    }
}
