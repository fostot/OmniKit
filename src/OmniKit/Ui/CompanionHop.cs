using System.Numerics;
namespace OmniKit.Ui;

/// <summary>Bounded ballistic geometry, independent of companion art and walking gait.</summary>
internal readonly record struct CompanionHop(Vector2 Start, Vector2 End, float Duration, float UpwardSpeed, float Apex)
{
    internal const float Gravity = 900;
    internal const float MaximumRise = .9f;
    public Vector2 At(float time)
    {
        time = Math.Clamp(time, 0, Duration);
        if (time == Duration) return End;
        return new(float.Lerp(Start.X, End.X, time / Duration), Start.Y - UpwardSpeed * time + Gravity * time * time * .5f);
    }
    public Vector2 VelocityAt(float time) => new((End.X - Start.X) / Duration,
        -UpwardSpeed + Gravity * Math.Clamp(time, 0, Duration));

    public static bool TryCreate(Vector2 start, Vector2 size, bool right, CompanionSurface target,
        Vector2 minimum, Vector2 maximum, CompanionSurface? house, out CompanionHop hop)
    {
        hop = default;
        if (!Finite(start) || !Finite(size) || !Finite(target.Minimum) || !Finite(target.Maximum)
            || !Finite(minimum) || !Finite(maximum) || size.X <= 0 || size.Y <= 0
            || maximum.X - minimum.X < size.X || maximum.Y - minimum.Y < size.Y
            || start.X < minimum.X || start.X + size.X > maximum.X || start.Y < minimum.Y || start.Y + size.Y > maximum.Y
            || target.Size.Y <= 0) return false;
        var rise = start.Y + size.Y * .9f - target.Minimum.Y;
        if (target.Id == 0 || target.Size.X < size.X * .5f || rise <= 0 || rise > size.Y * MaximumRise + .001f) return false;
        var edgeGap = right ? target.Minimum.X - start.X - size.X * .85f : start.X + size.X * .15f - target.Maximum.X;
        if (edgeGap < -size.X * .12f || edgeGap > size.X * .2f) return false;
        var end = new Vector2(right ? target.Minimum.X - size.X * .25f : target.Maximum.X - size.X * .75f,
            target.Minimum.Y - size.Y * .9f);
        var travel = end.X - start.X;
        if ((right ? travel <= 0 : travel >= 0) || Math.Abs(travel) > size.X * 1.25f) return false;
        var apex = Math.Min(size.Y, Math.Max(size.Y * .35f, rise + size.Y * .1f));
        var speed = MathF.Sqrt(2 * Gravity * apex);
        var duration = (speed + MathF.Sqrt(speed * speed - 2 * Gravity * rise)) / Gravity;
        if (apex > size.Y || Math.Abs(travel) / duration > size.Y * 2.5f || start.Y - apex < minimum.Y
            || end.X < minimum.X || end.X + size.X > maximum.X || end.Y + size.Y > maximum.Y) return false;
        var candidate = new CompanionHop(start, end, duration, speed, apex);
        if (CompanionObstacle.TryBounds(house, size, out var first, out var last))
        {
            var t0 = Math.Clamp((first.X - start.X) / travel * duration, 0, duration);
            var t1 = Math.Clamp((last.X - start.X) / travel * duration, 0, duration);
            if (t0 > t1) (t0, t1) = (t1, t0);
            if (t0 < t1)
            {
                var y0 = candidate.At(t0).Y; var y1 = candidate.At(t1).Y;
                var lowest = Math.Max(y0, y1); var highest = Math.Min(y0, y1);
                var peakTime = speed / Gravity;
                if (peakTime >= t0 && peakTime <= t1) highest = Math.Min(highest, candidate.At(peakTime).Y);
                if (highest < last.Y && lowest > first.Y) return false;
            }
        }
        hop = candidate; return true;
    }
    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
