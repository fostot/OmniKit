using System.Numerics;

namespace OmniKit.Ui;

internal readonly record struct CompanionSurface(ulong Id, Vector2 Minimum, Vector2 Maximum, float HeaderHeight, int Order,
    bool CanContain = true)
{
    public Vector2 Size => Maximum - Minimum;
    public bool Contains(Vector2 point) => point.X >= Minimum.X && point.Y >= Minimum.Y && point.X <= Maximum.X && point.Y <= Maximum.Y;
}

/// <summary>Bounded, deterministic routing around one visible house footprint.</summary>
internal static class CompanionObstacle
{
    public static Vector2 Step(Vector2 position, Vector2 destination, Vector2 size, float maxStep,
        Vector2 minimum, Vector2 maximum, CompanionSurface? obstacle)
    {
        if (!ValidArea(size, minimum, maximum)) return Finite(minimum) ? minimum : Vector2.Zero;
        position = Vector2.Clamp(Finite(position) ? position : minimum, minimum, maximum - size);
        if (!float.IsFinite(maxStep) || maxStep <= 0) return position;
        destination = Destination(Finite(destination) ? destination : position, size, minimum, maximum, obstacle);
        if (TryBounds(obstacle, size, out var first, out var last))
        {
            position = NearestExit(position, first, last, minimum, maximum - size);
            destination = ReturnWaypoint(position, destination, first, last, minimum, maximum - size);
        }
        var delta = destination - position;
        var distance = delta.Length();
        return distance <= maxStep ? destination : position + delta / distance * maxStep;
    }

    public static Vector2 Destination(Vector2 destination, Vector2 size, Vector2 minimum, Vector2 maximum, CompanionSurface? obstacle)
    {
        if (!ValidArea(size, minimum, maximum)) return Finite(minimum) ? minimum : Vector2.Zero;
        destination = Vector2.Clamp(Finite(destination) ? destination : minimum, minimum, maximum - size);
        return TryBounds(obstacle, size, out var first, out var last)
            ? NearestExit(destination, first, last, minimum, maximum - size) : destination;
    }

    internal static bool TryBounds(CompanionSurface? obstacle, Vector2 size, out Vector2 minimum, out Vector2 maximum)
    {
        minimum = maximum = default;
        if (obstacle is not { } house || !Finite(house.Minimum) || !Finite(house.Maximum) || house.Size.X <= 0 || house.Size.Y <= 0) return false;
        minimum = house.Minimum - size - new Vector2(4);
        maximum = house.Maximum + new Vector2(4);
        return true;
    }

    internal static bool Inside(Vector2 point, Vector2 minimum, Vector2 maximum) =>
        point.X > minimum.X + .001f && point.X < maximum.X - .001f && point.Y > minimum.Y + .001f && point.Y < maximum.Y - .001f;

    internal static Vector2 NearestExit(Vector2 point, Vector2 minimum, Vector2 maximum, Vector2 viewportMinimum, Vector2 viewportMaximum)
    {
        if (!Inside(point, minimum, maximum)) return point;
        Span<Vector2> exits = [new(minimum.X, point.Y), new(maximum.X, point.Y), new(point.X, minimum.Y), new(point.X, maximum.Y)];
        var best = point; var distance = float.MaxValue;
        foreach (var exit in exits)
        {
            if (exit.X < viewportMinimum.X || exit.Y < viewportMinimum.Y || exit.X > viewportMaximum.X || exit.Y > viewportMaximum.Y) continue;
            var current = Vector2.DistanceSquared(point, exit);
            if (current < distance) { best = exit; distance = current; }
        }
        return best;
    }

    internal static bool CrossesInterior(Vector2 from, Vector2 to, Vector2 minimum, Vector2 maximum)
    {
        var first = 0f; var last = 1f;
        for (var axis = 0; axis < 2; axis++)
        {
            var delta = to[axis] - from[axis];
            if (Math.Abs(delta) < .00001f)
            { if (from[axis] <= minimum[axis] + .001f || from[axis] >= maximum[axis] - .001f) return false; continue; }
            var a = (minimum[axis] + .001f - from[axis]) / delta;
            var b = (maximum[axis] - .001f - from[axis]) / delta;
            first = Math.Max(first, Math.Min(a, b)); last = Math.Min(last, Math.Max(a, b));
            if (first > last) return false;
        }
        return first <= last;
    }

    private static Vector2 ReturnWaypoint(Vector2 from, Vector2 to, Vector2 minimum, Vector2 maximum, Vector2 viewportMinimum, Vector2 viewportMaximum)
    {
        if (!CrossesInterior(from, to, minimum, maximum)) return to;
        // Six-node visibility graph: start, destination, and four fixed house
        // corners. No path allocation or foreign-UI state is retained.
        Span<Vector2> nodes = [from, to, minimum, new(maximum.X, minimum.Y), maximum, new(minimum.X, maximum.Y)];
        Span<float> cost = stackalloc float[6]; cost.Fill(float.MaxValue); cost[0] = 0;
        Span<int> parent = stackalloc int[6]; parent.Fill(-1);
        Span<bool> visited = stackalloc bool[6]; visited.Clear();
        for (var step = 0; step < 6; step++)
        {
            var current = -1;
            for (var i = 0; i < 6; i++) if (!visited[i] && cost[i] < float.MaxValue && (current < 0 || cost[i] < cost[current])) current = i;
            if (current < 0 || current == 1) break;
            visited[current] = true;
            for (var i = 1; i < 6; i++)
            {
                var next = nodes[i];
                if (visited[i] || next.X < viewportMinimum.X || next.Y < viewportMinimum.Y
                    || next.X > viewportMaximum.X || next.Y > viewportMaximum.Y || CrossesInterior(nodes[current], next, minimum, maximum)) continue;
                var distance = cost[current] + Vector2.Distance(nodes[current], next);
                if (distance < cost[i]) { cost[i] = distance; parent[i] = current; }
            }
        }
        if (parent[1] < 0) return from;
        var waypoint = 1;
        for (var i = 0; i < 6 && parent[waypoint] > 0; i++) waypoint = parent[waypoint];
        return nodes[waypoint];
    }
    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    private static bool ValidArea(Vector2 size, Vector2 minimum, Vector2 maximum) => Finite(size) && Finite(minimum) && Finite(maximum)
        && size.X > 0 && size.Y > 0 && maximum.X - minimum.X >= size.X && maximum.Y - minimum.Y >= size.Y;
}
