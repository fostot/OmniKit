using System.Numerics;

namespace OmniKit.Ui;

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

    public static (Vector2 Start, Vector2 End) FrameUv(int frame, int row, bool facingLeft)
    {
        frame = Math.Clamp(frame, 0, 3);
        row = Math.Clamp(row, 0, 1);
        var left = frame / 4f;
        var right = (frame + 1) / 4f;
        return (new Vector2(facingLeft ? right : left, row / 2f),
            new Vector2(facingLeft ? left : right, (row + 1) / 2f));
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

/// <summary>Frame-rate independent roaming with no game or renderer dependencies.</summary>
internal sealed class CompanionMotion(int seed = 0)
{
    private readonly Random random = seed == 0 ? new Random() : new Random(seed);
    private bool initialized;
    private Vector2 destination;
    private float pause;
    private float walkTime;

    public Vector2 Position { get; private set; }
    public bool FacingLeft { get; private set; }
    public bool Walking { get; private set; }
    public int Frame => Walking ? (int)(walkTime * 8) % 4 : 0;

    public void Reset() { initialized = false; Walking = false; walkTime = 0; }

    public void Update(CompanionLayout layout, float elapsed, bool animate)
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
        if (!animate) { walkTime = 0; return; }
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
            walkTime = 0;
            return;
        }
        Walking = true;
        if (Math.Abs(delta.X) > .1f) FacingLeft = delta.X < 0;
        var step = Math.Min(distance, Math.Max(8f, layout.SpriteSize.Y * .36f) * elapsed);
        Position = layout.Clamp(Position + delta / distance * step);
        walkTime = (walkTime + elapsed) % 1000;
    }
}
