using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using static Lumina.Data.Parsing.Layer.LayerCommon;

namespace OmniKit.Unlocks;

internal sealed record AetheryteUnlockOption(uint Id, string Name, string Territory, bool Unlocked,
    bool Eligible, string Detail, uint TerritoryId = 0, Vector3? Position = null,
    bool IsAetheryte = true, uint RequiredQuestId = 0, bool IsVirtual = false);

internal sealed record AetheryteDestination(uint Id, string Name, string Territory, uint TerritoryId,
    Vector3? Position, bool IsAetheryte, uint RequiredQuestId, bool IsVirtual = false);

/// <summary>Public game-sheet destinations. Character attunement stays in transient snapshots.</summary>
internal static class AetheryteCatalog
{
    public static IReadOnlyList<AetheryteDestination> Read(IDataManager data)
        => Read(data.GetExcelSheet<Aetheryte>(), path => data.GetFile<LgbFile>(path));

    internal static IReadOnlyList<AetheryteDestination> Read(IEnumerable<Aetheryte> rows, Func<string, LgbFile?> readFile)
    {
        var destinations = new List<AetheryteDestination>();
        var layouts = new Dictionary<uint, IReadOnlyList<LayoutPlacement>>();
        foreach (var row in rows)
        {
            if (row.RowId == 0) continue;
            var name = row.IsAetheryte && row.PlaceName.IsValid ? row.PlaceName.Value.Name.ExtractText()
                : row.AethernetName.IsValid ? row.AethernetName.Value.Name.ExtractText() : "";
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!row.Territory.IsValid || row.Territory.RowId == 0)
            {
                destinations.Add(new(row.RowId, name, "Location unavailable", 0, null,
                    row.IsAetheryte, row.RequiredQuest.RowId, row.Invisible));
                continue;
            }
            var territory = row.Territory.Value;
            var territoryName = territory.PlaceName.IsValid ? territory.PlaceName.Value.Name.ExtractText() : "";
            if (string.IsNullOrWhiteSpace(territoryName)) territoryName = $"Territory {territory.RowId}";
            if (!layouts.TryGetValue(territory.RowId, out var layout))
                layouts[territory.RowId] = layout = ReadLayout(readFile, territory);
            var locations = layout.Where(placement => placement.Object.Object is AetheryteInstanceObject aetheryte
                    && aetheryte.ParentData.BaseId == row.RowId)
                .Select(placement => Position(placement.Object)).Where(Finite).Distinct().ToArray();
            // Multiple disagreeing placements require a runtime layer choice; do not guess.
            Vector3? location = locations.Length == 1 ? locations[0] : null;
            destinations.Add(new(row.RowId, name, territoryName, territory.RowId, location,
                row.IsAetheryte, row.RequiredQuest.RowId, row.Invisible));
        }
        return destinations.OrderBy(row => row.Territory, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.Id).ToArray();
    }

    public static IReadOnlyList<AetheryteUnlockOption> Snapshot(IEnumerable<AetheryteDestination> destinations,
        Func<uint, bool> isUnlocked, Func<uint, bool> isQuestComplete)
    {
        return destinations.Select(destination =>
        {
            var unlocked = isUnlocked(destination.Id);
            var questReady = destination.RequiredQuestId == 0 || isQuestComplete(destination.RequiredQuestId);
            var located = destination.TerritoryId != 0 && destination.Position is { } position && Finite(position);
            var eligible = !unlocked && !destination.IsVirtual && questReady && located;
            var detail = unlocked ? "Attuned."
                : destination.IsVirtual ? "Aethernet exit destination; no physical shard to attune here."
                : !questReady ? "A required quest is incomplete."
                : !located ? "A verified world location is unavailable."
                : (destination.IsAetheryte ? "Aetheryte" : "Aethernet shard") + ": ready to visit; a verified route is required.";
            return new AetheryteUnlockOption(destination.Id, destination.Name, destination.Territory,
                unlocked, eligible, detail, destination.TerritoryId, destination.Position,
                destination.IsAetheryte, destination.RequiredQuestId, destination.IsVirtual);
        }).ToArray();
    }

    internal static bool Finite(Vector3 position) => float.IsFinite(position.X)
        && float.IsFinite(position.Y) && float.IsFinite(position.Z);

    /// <summary>
    /// Reads only unconditional walking exits with a matching destination PopRange.
    /// Layer-set, seasonal and unknown gates are excluded. No boats, fees or dialogues are inferred.
    /// </summary>
    public static IReadOnlyList<TravelConnection> ReadConnections(IDataManager data)
        => ReadConnections(data.GetExcelSheet<TerritoryType>(), path => data.GetFile<LgbFile>(path));

    internal static IReadOnlyList<TravelConnection> ReadConnections(IEnumerable<TerritoryType> rows, Func<string, LgbFile?> readFile)
    {
        var territories = rows.ToDictionary(territory => territory.RowId);
        var layouts = new Dictionary<uint, IReadOnlyList<LayoutPlacement>>();
        IReadOnlyList<LayoutPlacement> Layout(uint id)
        {
            if (!layouts.TryGetValue(id, out var layout))
                layouts[id] = layout = territories.TryGetValue(id, out var territory) ? ReadLayout(readFile, territory) : [];
            return layout;
        }
        var result = new List<TravelConnection>();
        foreach (var territory in territories.Values)
        {
            if (!OpenWorld(territory)) continue;
            foreach (var placement in Layout(territory.RowId))
            {
                if (!placement.Unconditional || placement.Object.Object is not ExitRangeInstanceObject exit
                    || exit.ExitType != ExitType.ZoneLine || exit.TerritoryType == 0
                    || exit.TerritoryType == territory.RowId || exit.DestInstanceId == 0
                    || !territories.TryGetValue(exit.TerritoryType, out var destinationTerritory)
                    || !OpenWorld(destinationTerritory)) continue;
                var arrivals = Layout(exit.TerritoryType).Where(target => target.Object.InstanceId == exit.DestInstanceId
                        && target.Object.Object is PopRangeInstanceObject)
                    .Select(target => Position(target.Object)).Where(Finite).Distinct().ToArray();
                var from = Position(placement.Object);
                if (arrivals.Length != 1 || !Finite(from)) continue;
                result.Add(new(territory.RowId, from, exit.TerritoryType, arrivals[0],
                    $"Walking exit {territory.RowId} → {exit.TerritoryType}", Verified: true));
            }
        }
        return result.Distinct().ToArray();
    }

    private sealed record LayoutPlacement(InstanceObject Object, bool Unconditional);

    private static bool OpenWorld(TerritoryType territory) => territory.RowId != 0 && territory.IsInUse
        && territory.ContentFinderCondition.RowId == 0 && territory.TerritoryIntendedUse.RowId is 0 or 1;

    private static IReadOnlyList<LayoutPlacement> ReadLayout(Func<string, LgbFile?> readFile, TerritoryType territory)
    {
        var background = territory.Bg.ExtractText();
        var separator = background.LastIndexOf('/');
        if (separator < 0) return [];
        var result = new List<LayoutPlacement>();
        foreach (var file in new[] { "planmap", "planevent" })
        {
            LgbFile? layout;
            try { layout = readFile($"bg/{background[..separator]}/{file}.lgb"); }
            catch { continue; } // An unreadable public layout supplies no verified positions or edges.
            if (layout == null) continue;
            foreach (var layer in layout.Layers)
            {
                var unconditional = layer.FestivalID == 0 && layer.IsTemporary == 0
                    && layer.LayerSetReferencedList.ReferencedType == LayerSetReferencedType.All;
                foreach (var instance in layer.InstanceObjects)
                    if (instance.Object is AetheryteInstanceObject or ExitRangeInstanceObject or PopRangeInstanceObject)
                        result.Add(new(instance, unconditional));
            }
        }
        return result;
    }

    private static Vector3 Position(InstanceObject instance) => new(instance.Transform.Translation.X,
        instance.Transform.Translation.Y, instance.Transform.Translation.Z);
}
