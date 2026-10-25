using System.Numerics;

namespace OmniKit.Unlocks;

internal enum TravelKind { WalkExit, Boat }

/// <summary>A directed transition. Unknown access conditions must leave Verified false.</summary>
internal sealed record TravelConnection(uint FromTerritory, Vector3 FromPoint, uint ToTerritory,
    Vector3 ToPoint, string Name, TravelKind Kind = TravelKind.WalkExit, bool Verified = false);

internal sealed record UnlockRoutePlan(AetheryteUnlockOption? Target,
    IReadOnlyList<TravelConnection> Legs, string Reason, double EstimatedDistance = 0);

internal static class UnlockRoutePlanner
{
    public static UnlockRoutePlan SelectNext(IReadOnlyList<AetheryteUnlockOption> snapshot,
        uint currentTerritory, Vector3 position, IReadOnlyList<TravelConnection>? verifiedEdges = null)
    {
        if (currentTerritory == 0 || !AetheryteCatalog.Finite(position))
            return new(null, [], "The current territory or player position is unavailable.");
        var candidates = snapshot.Where(option => option.Eligible && !option.Unlocked && !option.IsVirtual
                && option.TerritoryId != 0 && option.Position is { } p && AetheryteCatalog.Finite(p))
            .OrderBy(option => option.Id).ToArray();
        var local = candidates.Where(option => option.TerritoryId == currentTerritory)
            .OrderBy(option => Distance(position, option.Position!.Value)).ThenBy(option => option.Id).FirstOrDefault();
        if (local != null)
            return new(local, [], "Visit the closest unattuned destination in the current territory.",
                Distance(position, local.Position!.Value));

        // Each arrival point is a distinct state: reducing to one value per territory
        // can discard a better exit reached from another entrance in that territory.
        var edges = (verifiedEdges ?? []).Where(edge => edge.Verified && edge.Kind == TravelKind.WalkExit
                && edge.FromTerritory != 0 && edge.ToTerritory != 0 && edge.FromTerritory != edge.ToTerritory
                && AetheryteCatalog.Finite(edge.FromPoint) && AetheryteCatalog.Finite(edge.ToPoint)).ToArray();
        var distances = Enumerable.Repeat(double.PositiveInfinity, edges.Length + 1).ToArray();
        var previous = Enumerable.Repeat(-1, edges.Length + 1).ToArray();
        var queue = new PriorityQueue<int, double>();
        distances[0] = 0; queue.Enqueue(0, 0);
        AetheryteUnlockOption? best = null;
        var bestDistance = double.PositiveInfinity;
        var bestState = -1;
        while (queue.TryDequeue(out var state, out var cost))
        {
            if (cost != distances[state] || cost > bestDistance) continue;
            var territory = state == 0 ? currentTerritory : edges[state - 1].ToTerritory;
            var arrival = state == 0 ? position : edges[state - 1].ToPoint;
            foreach (var candidate in candidates.Where(option => option.TerritoryId == territory))
            {
                var total = cost + Distance(arrival, candidate.Position!.Value);
                if (total < bestDistance || total == bestDistance && candidate.Id < best!.Id)
                { best = candidate; bestDistance = total; bestState = state; }
            }
            for (var index = 0; index < edges.Length; index++)
            {
                if (edges[index].FromTerritory != territory) continue;
                var nextCost = cost + Distance(arrival, edges[index].FromPoint) + 1;
                if (nextCost >= distances[index + 1]) continue;
                distances[index + 1] = nextCost; previous[index + 1] = state;
                queue.Enqueue(index + 1, nextCost);
            }
        }
        if (best == null)
            return new(null, [], candidates.Length == 0 ? "No eligible unattuned destinations remain."
                : "No verified walking connection reaches the remaining destinations. Boats and unknown access conditions are not automated.");
        var legs = new List<TravelConnection>();
        for (var state = bestState; state > 0; state = previous[state]) legs.Add(edges[state - 1]);
        legs.Reverse();
        return new(best, legs, "Follow verified walking exits to the closest reachable destination.", bestDistance);
    }

    private static double Distance(Vector3 first, Vector3 second)
    {
        var x = (double)first.X - second.X; var y = (double)first.Y - second.Y; var z = (double)first.Z - second.Z;
        return Math.Sqrt(x * x + y * y + z * z);
    }
}
