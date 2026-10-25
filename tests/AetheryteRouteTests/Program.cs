using System.Numerics;
using OmniKit.Unlocks;

var checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
Vector3 P(float x) => new(x, 0, 0);
AetheryteUnlockOption Target(uint id, uint territory, float x, bool major = true) =>
    new(id, $"Destination {id}", $"Zone {territory}", false, true, "", territory, P(x), major);
TravelConnection Exit(uint from, float x, uint to, float arrival, bool verified = true) =>
    new(from, P(x), to, P(arrival), "Synthetic walking exit", Verified: verified);

var near = Target(1, 10, 10);
var far = Target(2, 10, 100);
var other = Target(3, 20, 1);
var route = UnlockRoutePlanner.SelectNext([far, other, near], 10, P(0), [Exit(10, 0, 20, 0)]);
Check(route.Target == near && route.Legs.Count == 0 && route.EstimatedDistance == 10,
    "closest current-zone target precedes cross-zone candidate");
route = UnlockRoutePlanner.SelectNext([far, near with { Unlocked = true }], 10, P(0));
Check(route.Target == far, "fresh attunement removes a visited target");
route = UnlockRoutePlanner.SelectNext([near with { Eligible = false }], 10, P(0));
Check(route.Target == null && route.Reason.Length > 0, "ineligible target never planned");
route = UnlockRoutePlanner.SelectNext([near with { Position = null }], 10, P(0));
Check(route.Target == null, "unknown coordinates never fabricated");
Check(UnlockRoutePlanner.SelectNext([near], 0, P(0)).Target == null, "unknown current territory blocks");
Check(UnlockRoutePlanner.SelectNext([near], 10, new(float.NaN, 0, 0)).Target == null, "invalid player position blocks");
Check(UnlockRoutePlanner.SelectNext([near with { Position = new(float.PositiveInfinity, 0, 0) }], 10, P(0)).Target == null,
    "invalid destination position blocks");
Check(UnlockRoutePlanner.SelectNext([other], 10, P(0)).Target == null, "territories without an edge stay disconnected");
Check(UnlockRoutePlanner.SelectNext([other], 10, P(0), [Exit(10, 0, 20, 0, false)]).Target == null,
    "unknown access gate excluded");
Check(UnlockRoutePlanner.SelectNext([other], 10, P(0), [Exit(10, 0, 20, 0) with { Kind = TravelKind.Boat }]).Target == null,
    "boats never inferred as walking edges");
Check(UnlockRoutePlanner.SelectNext([other], 10, P(0), [Exit(20, 0, 10, 0)]).Target == null,
    "directed exit never assumed reversible");
var first = Exit(10, 5, 15, 20);
var second = Exit(15, 25, 20, 0);
route = UnlockRoutePlanner.SelectNext([other], 10, P(0), [second, first]);
Check(route.Target == other && route.Legs.SequenceEqual(new[] { first, second }) && route.EstimatedDistance == 13,
    "ordered multi-zone legs include walking to each exit and final target");
var wrongEntrance = Exit(10, 1, 15, 1000);
var rightEntrance = Exit(10, 10, 15, 25);
route = UnlockRoutePlanner.SelectNext([other], 10, P(0), [wrongEntrance, rightEntrance, second]);
Check(route.Legs.SequenceEqual(new[] { rightEntrance, second }),
    "separate arrival states preserve a useful entrance rather than reducing to one distance per territory");
route = UnlockRoutePlanner.SelectNext([other], 10, P(0), [first, second, Exit(15, 20, 10, 0)]);
Check(route.Target == other && route.Legs.Count == 2, "cycles terminate without repeated legs");
Check(UnlockRoutePlanner.SelectNext([Target(5, 10, 10), near], 10, P(0)).Target == near,
    "equal-distance destination ordering deterministic");
var shard = Target(7, 10, 2, false);
Check(UnlockRoutePlanner.SelectNext([near, shard], 10, P(0)).Target == shard, "nearby aethernet shard included");
route = UnlockRoutePlanner.SelectNext([other], 10, P(0), [Exit(10, 0, 20, 0) with { FromPoint = new(float.NaN, 0, 0) }]);
Check(route.Target == null, "invalid transition coordinates excluded");

var catalog = new[]
{
    new AetheryteDestination(1, "Major", "Zone", 10, P(10), true, 0),
    new AetheryteDestination(2, "Shard", "Zone", 10, P(2), false, 0),
    new AetheryteDestination(3, "Quest", "Zone", 10, P(3), true, 100),
    new AetheryteDestination(4, "Unknown", "Zone", 10, null, false, 0),
};
var options = AetheryteCatalog.Snapshot(catalog, _ => false, _ => false);
Check(options[0].Eligible && options[1].Eligible && !options[1].IsAetheryte,
    "major and shard both eligible when located and unattuned");
Check(!options[2].Eligible && options[2].Detail.Contains("quest"), "required quest gate excluded");
Check(!options[3].Eligible && options[3].Position == null, "unknown shard stays visible without fabricated position");
options = AetheryteCatalog.Snapshot(catalog, id => id == 2, _ => true);
Check(options[1].Unlocked && !options[1].Eligible && options[0].Eligible,
    "minor attunement checked with exact sheet ID independently of major");
Check(options[2].Eligible, "completed prerequisite unlocks eligibility");
options = AetheryteCatalog.Snapshot(catalog, _ => true, _ => true);
Check(options.All(option => option.Unlocked && !option.Eligible), "attuned entries remain visible but never routed");
Check(catalog[1].Position == P(2) && catalog[2].RequiredQuestId == 100, "transient snapshot does not mutate public catalog");
options = AetheryteCatalog.Snapshot([catalog[1] with { IsVirtual = true }], _ => false, _ => true);
Check(!options[0].Eligible && options[0].IsVirtual && options[0].Detail.Contains("no physical shard"),
    "virtual aethernet exit remains visible without inventing an attunement target");
Check(!new TravelConnection(10, P(0), 20, P(0), "Unknown gate").Verified,
    "transition access must be verified explicitly");
Check(UnlockRoutePlanner.SelectNext([near with { IsVirtual = true }], 10, P(0)).Target == null,
    "planner independently excludes a virtual exit even if eligibility is inconsistent");

// Optional reproducible read-only check of public installation data. No player state is accessed.
var sqpack = args.FirstOrDefault(argument => argument.StartsWith("--sqpack=", StringComparison.Ordinal))?[9..];
if (sqpack != null)
{
    using var gameData = new Lumina.GameData(sqpack);
    var loadTimer = System.Diagnostics.Stopwatch.StartNew();
    var actual = AetheryteCatalog.Read(gameData.Excel.GetSheet<Lumina.Excel.Sheets.Aetheryte>(),
        path => gameData.GetFile<Lumina.Data.Files.LgbFile>(path));
    var catalogMilliseconds = loadTimer.ElapsedMilliseconds;
    loadTimer.Restart();
    var connections = AetheryteCatalog.ReadConnections(gameData.Excel.GetSheet<Lumina.Excel.Sheets.TerritoryType>(),
        path => gameData.GetFile<Lumina.Data.Files.LgbFile>(path));
    var connectionsMilliseconds = loadTimer.ElapsedMilliseconds;
    Check(actual.Count > 0 && actual.Select(row => row.Id).Distinct().Count() == actual.Count,
        "public sheet catalog contains uniquely identified destinations");
    Check(actual.Where(row => row.IsAetheryte && !row.IsVirtual).All(row => row.Position is { } p && AetheryteCatalog.Finite(p)),
        "all physical major destinations have verified layout coordinates in this installation");
    Check(actual.Where(row => !row.IsAetheryte && !row.IsVirtual).All(row => row.Position is { } p && AetheryteCatalog.Finite(p)),
        "all physical shards have verified layout coordinates in this installation");
    Check(connections.Count > 0 && connections.All(edge => edge.Verified && edge.Kind == TravelKind.WalkExit
        && AetheryteCatalog.Finite(edge.FromPoint) && AetheryteCatalog.Finite(edge.ToPoint)),
        "only fully located verified walking transitions are emitted");
    Console.WriteLine($"Public game data: {actual.Count} named destinations, {actual.Count(row => row.IsAetheryte && !row.IsVirtual)} major, "
        + $"{actual.Count(row => !row.IsAetheryte && !row.IsVirtual)} physical shards, {actual.Count(row => row.IsVirtual)} virtual exits; "
        + $"{connections.Count} unconditional directed walking transitions. Conditional exits and boats remain excluded.");
    Console.WriteLine($"Public asset read: catalog {catalogMilliseconds} ms; connections {connectionsMilliseconds} ms in this run.");
}
Console.WriteLine($"PASS: {checks} aetheryte catalog and route checks; no native calls or player data.");
