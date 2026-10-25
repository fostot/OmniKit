using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using OmniKit.Companions;

var root = Path.Combine(Path.GetTempPath(), "OmniKit-sayings-synthetic-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
void Reject(Action action, string name)
{
    var rejected = false;
    try { action(); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, name);
}
string Seed(int revision, params (string Id, string Companion, string Context, string Text)[] entries) =>
    JsonSerializer.Serialize(new { schemaVersion = 1, revision, entries = entries.Select(entry => new
    { id = entry.Id, companion = entry.Companion, context = entry.Context, text = entry.Text, enabled = true }) });
try
{
    var seedPath = Path.Combine(root, "synthetic-seed.json");
    File.WriteAllText(seedPath, Seed(1, ("builtin:one", "Universal", "Any", "Synthetic first line."),
        ("builtin:two", "Wink", "Walking", "Synthetic second line.")));
    var library = new SayingLibrary(root, seedPath);
    Check(library.GetAll().Count == 2 && library.GetAll().All(entry => entry.BuiltIn), "seed inserts built-in rows");
    Check(ReferenceEquals(library.GetAll(), library.GetAll()), "GetAll returns a cached immutable snapshot");
    var firstSnapshot = library.GetAll();
    var custom = library.Add("Mochi", "Held", "A local custom line with 'quotes' and a snowman ☃.");
    Check(custom.Id.StartsWith("custom:") && !custom.BuiltIn && library.GetAll().Count == 3,
        "custom sayings use local stable identifiers and parameterized text storage");
    Check(firstSnapshot.Count == 2 && !ReferenceEquals(firstSnapshot, library.GetAll()), "mutations publish a new snapshot without changing old readers");
    var revision = library.Revision;
    library.SetEnabled(custom.Id, true);
    Check(library.Revision == revision, "no-op changes do not spend a library revision");
    var edited = library.Edit("builtin:one", "Universal", "Idle", "User edited a built-in locally.");
    library.SetEnabled("builtin:one", false); library.Remove("builtin:two");
    var saved = library.ExportJson();
    var reloaded = new SayingLibrary(root, seedPath);
    Check(reloaded.GetAll().Single(entry => entry.Id == "builtin:one").Text == edited.Text
        && !reloaded.GetAll().Single(entry => entry.Id == "builtin:one").Enabled
        && reloaded.GetAll().Single(entry => entry.Id == "builtin:two").Deleted,
        "reseed never overwrites user edits, disabled state or soft-deletion");
    Check(reloaded.ExportJson() == saved, "database reload and reseed preserve the export exactly");
    File.WriteAllText(seedPath, Seed(2, ("builtin:one", "Universal", "Any", "Replacement must not win."),
        ("builtin:two", "Wink", "Walking", "Deleted must not return."), ("builtin:three", "Nova", "Home", "New revision line.")));
    library = new SayingLibrary(root, seedPath);
    Check(library.GetAll().Count == 4 && library.GetAll().Single(entry => entry.Id == "builtin:one").Text == edited.Text
        && library.GetAll().Single(entry => entry.Id == "builtin:two").Deleted,
        "a seed revision adds only previously unseen IDs");
    Check(!library.Restore("builtin:two").Deleted, "soft-deleted built-ins can be restored explicitly");
    Check(!library.Remove(custom.Id).BuiltIn && library.Restore(custom.Id).Text == custom.Text,
        "custom remove and restore preserve identity and text");

    var beforeInvalid = library.ExportJson();
    foreach (var text in new[] { "", "  ", "line\nbreak", "\ntrimmed control\n", "tab\there", "NUL\0here", "two\u2028lines", "bad\ud800", new string('a', 241) })
        Reject(() => library.Add("Universal", "Any", text), "invalid line is rejected before DB mutation");
    foreach (var values in new[] { ("Unknown", "Any"), ("Universal", "Flying"), ("universal", "Idle") })
        Reject(() => library.Add(values.Item1, values.Item2, "Valid text"), "only known companion/context names accepted");
    Reject(() => library.Edit("missing", "Universal", "Any", "No such row"), "editing missing row never creates it");
    Check(library.ExportJson() == beforeInvalid, "invalid mutations preserve the complete existing library");
    var emoji = library.Add("Universal", "Any", string.Concat(Enumerable.Repeat("😀", 240)));
    Check(emoji.Text.EnumerateRunes().Count() == 240, "character limit counts Unicode scalars rather than UTF-16 units");
    library.Remove(emoji.Id);

    var exported = library.ExportJson();
    var preview = library.PreviewImport(exported);
    Check(preview.Added == 0 && preview.Updated == 0 && preview.Unchanged == library.GetAll().Count,
        "round-trip import preview reports unchanged rows");
    var untouched = library.Add("Echo", "Waking", "Unmentioned row remains local.");
    var imported = JsonNode.Parse(exported)!;
    var importedRows = imported["entries"]!.AsArray();
    importedRows[0]!["text"] = "Explicit merge replacement.";
    importedRows.Add(JsonSerializer.SerializeToNode(new { id = "import:one", companion = "Universal", context = "Any",
        text = "Imported custom line.", enabled = true, deleted = false, builtIn = true }));
    var importJson = imported.ToJsonString();
    preview = library.PreviewImport(importJson);
    Check(preview.Added == 1 && preview.Updated == 1 && preview.Total == importedRows.Count,
        "preview separates added, updated and unchanged counts");
    var merged = library.MergeImport(importJson);
    Check(merged == preview && library.GetAll().Any(entry => entry.Id == untouched.Id)
        && !library.GetAll().Single(entry => entry.Id == "import:one").BuiltIn,
        "merge never deletes absent rows or trusts invented built-in attribution");
    Check(library.GetAll().Single(entry => entry.Id == "builtin:one").BuiltIn, "import preserves existing local built-in attribution");

    var baseline = library.ExportJson();
    foreach (var invalid in new[] { "null", "{}", "{broken", baseline.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"),
        baseline.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1"),
        new string(' ', SayingLibrary.MaximumJsonBytes + 1) })
    {
        Reject(() => library.PreviewImport(invalid), "invalid or unsupported JSON preview rejected");
        Reject(() => library.MergeImport(invalid), "invalid or unsupported JSON merge rejected");
    }
    foreach (var field in new[] { "id", "companion", "context", "text", "enabled", "deleted", "builtIn" })
    {
        var malformed = JsonNode.Parse(baseline)!;
        malformed["entries"]![0]!.AsObject().Remove(field);
        Reject(() => library.MergeImport(malformed.ToJsonString()), "missing entry field rejects whole import: " + field);
    }
    var duplicate = JsonNode.Parse(baseline)!;
    duplicate["entries"]!.AsArray().Add(duplicate["entries"]![0]!.DeepClone());
    Reject(() => library.MergeImport(duplicate.ToJsonString()), "duplicate IDs reject whole import");
    var badLater = JsonNode.Parse(baseline)!;
    badLater["entries"]![0]!["text"] = "Must roll back if any later entry is invalid.";
    badLater["entries"]![1]!["companion"] = "Invalid";
    Reject(() => library.MergeImport(badLater.ToJsonString()), "all import entries validate before any write");
    Check(library.ExportJson() == baseline, "failed import preserves every row and revision");

    using (var connection = new SqliteConnection($"Data Source={Path.Combine(root, "omnikit.db")};Pooling=False"))
    {
        connection.Open(); using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        Check(Convert.ToInt32(command.ExecuteScalar()) == 0, "sayings tables never alter the global settings schema");
        command.CommandText = "PRAGMA user_version=14; CREATE TABLE IF NOT EXISTS OmniKitSettings (Id INTEGER PRIMARY KEY, Json TEXT); INSERT OR IGNORE INTO OmniKitSettings VALUES(1,'synthetic untouched settings');";
        command.ExecuteNonQuery();
        command.CommandText = "CREATE TRIGGER RejectSayingWrite BEFORE UPDATE ON OmniKitSayings BEGIN SELECT RAISE(ABORT,'Synthetic write failure'); END;";
        command.ExecuteNonQuery();
    }
    var failed = false;
    try { library.Edit(custom.Id, "Mochi", "Home", "Synthetic blocked write"); } catch (SqliteException) { failed = true; }
    Check(failed && library.ExportJson() == baseline, "a SQL write failure preserves the cached complete library");
    var atomicImport = JsonNode.Parse(baseline)!;
    atomicImport["entries"]![0]!["text"] = "Synthetic blocked import update.";
    atomicImport["entries"]!.AsArray().Insert(0, JsonSerializer.SerializeToNode(new { id = "import:rollback", companion = "Universal", context = "Any",
        text = "Synthetic insert before a later failing update.", enabled = true, deleted = false, builtIn = false }));
    var mergeWriteFailed = false;
    try { library.MergeImport(atomicImport.ToJsonString()); } catch (SqliteException) { mergeWriteFailed = true; }
    Check(mergeWriteFailed && library.ExportJson() == baseline, "a later failed SQL merge write rolls back an earlier successful insert");
    var afterFailure = new SayingLibrary(root, seedPath);
    Check(afterFailure.ExportJson() == baseline, "a SQL write failure preserves the persisted complete library");
    using (var connection = new SqliteConnection($"Data Source={Path.Combine(root, "omnikit.db")};Pooling=False"))
    {
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "PRAGMA user_version;";
        Check(Convert.ToInt32(command.ExecuteScalar()) == 14, "library re-open preserves the settings database version");
        command.CommandText = "SELECT Json FROM OmniKitSettings WHERE Id=1";
        Check((string)command.ExecuteScalar()! == "synthetic untouched settings", "library operations do not rewrite the settings row");
    }

    var eligible = Enumerable.Range(0, 12).Select(index => new SayingEntry("test:" + index, "Universal", "Any", "Line " + index, true, false, false)).ToArray();
    var selector = new SayingController(() => eligible, new Random(7));
    var sequence = Enumerable.Range(0, 100).Select(_ => selector.GetNext("Mochi", "Idle")!).ToArray();
    Check(sequence.All(entry => entry != null), "universal Any rows serve known companions and contexts");
    Check(sequence.Select((entry, index) => (entry, index)).All(pair => sequence.Skip(Math.Max(0, pair.index - 5)).Take(Math.Min(5, pair.index))
        .All(previous => previous.Id != pair.entry.Id && previous.Text != pair.entry.Text)), "no repeat among previous five IDs or texts when pool permits");
    var sameSeed = new SayingController(() => eligible, new Random(7));
    Check(sequence.Select(entry => entry.Id).SequenceEqual(Enumerable.Range(0, 100).Select(_ => sameSeed.GetNext("Mochi", "Idle")!.Id)),
        "random injection gives deterministic selection");
    var scoped = new[] { eligible[0] with { Companion = "Mochi", Context = "Held" }, eligible[1] with { Companion = "Wink" },
        eligible[2] with { Context = "Walking" }, eligible[3] with { Enabled = false }, eligible[4] with { Deleted = true } };
    selector = new(() => scoped, new Random(1));
    Check(selector.GetNext("Mochi", "Held")?.Id == eligible[0].Id && selector.GetNext("Mochi", "Idle") == null,
        "exact companion/context filtering excludes other avatars, states, disabled and deleted rows");
    Check(selector.GetNext("Unknown", "Idle") == null && selector.GetNext("Mochi", "Unknown") == null,
        "unknown selection identifiers do not escape scope");
    var shortPool = eligible.Take(2).ToArray(); selector = new(() => shortPool, new Random(1));
    var shortSequence = Enumerable.Range(0, 20).Select(_ => selector.GetNext("Mochi", "Idle")!).ToArray();
    Check(shortSequence.Zip(shortSequence.Skip(1)).All(pair => pair.First.Id != pair.Second.Id), "small pools relax oldest history while avoiding immediate repeats");
    var duplicatedText = eligible.Concat([eligible[0] with { Id = "test:duplicate" }]).ToArray();
    selector = new(() => duplicatedText, new Random(2));
    var textSequence = Enumerable.Range(0, 100).Select(_ => selector.GetNext("Mochi", "Idle")!).ToArray();
    Check(textSequence.Select((entry, index) => (entry, index)).All(pair => textSequence.Skip(Math.Max(0, pair.index - 5)).Take(Math.Min(5, pair.index))
        .All(previous => previous.Text != pair.entry.Text)), "duplicate text under another ID does not bypass recent-text exclusion");
    IReadOnlyList<SayingEntry> live = eligible; selector = new(() => live, new Random(1));
    Check(selector.GetNext("Mochi", "Idle") != null, "selector reads cached snapshot provider");
    live = [];
    Check(selector.GetNext("Mochi", "Idle") == null, "controller immediately respects a replaced empty snapshot");
    var displayed = eligible[0]; live = [displayed];
    Check(selector.IsAvailable(displayed, "Mochi"), "an active universal displayed saying remains available");
    live = [displayed with { Companion = "Mochi", Context = "Walking" }];
    Check(selector.IsAvailable(displayed, "Mochi"), "context changes do not interrupt the current displayed line");
    foreach (var changed in new[] { displayed with { Enabled = false }, displayed with { Deleted = true },
        displayed with { Text = "A replacement line." }, displayed with { Companion = "Wink" }, displayed with { Id = "another:id" } })
    {
        live = [changed];
        Check(!selector.IsAvailable(displayed, "Mochi"), "disabled, deleted, edited, moved or removed displayed saying becomes unavailable");
    }
    live = [];
    Check(!selector.IsAvailable(displayed, "Mochi"), "a removed displayed entry is unavailable");

    string SyntheticImport(int start, int count, string text) => JsonSerializer.Serialize(new { schemaVersion = 1, revision = 0,
        entries = Enumerable.Range(start, count).Select(index => new { id = "synthetic:" + index, companion = "Universal", context = "Any",
            text, enabled = true, deleted = false, builtIn = false }) });
    var capacityLibrary = new SayingLibrary(Path.Combine(root, "capacity"), null);
    var full = SyntheticImport(0, SayingLibrary.MaximumEntries, "Synthetic capacity line.");
    Check(capacityLibrary.PreviewImport(full).Added == 2000 && capacityLibrary.GetAll().Count == 0,
        "capacity preview validates without changing the database");
    capacityLibrary.MergeImport(full);
    var capacityBefore = capacityLibrary.ExportJson();
    Reject(() => capacityLibrary.Add("Universal", "Any", "One too many."), "capacity cannot be exceeded by Add");
    Reject(() => capacityLibrary.PreviewImport(SyntheticImport(2000, 1, "Another row.")), "merge preview accounts for existing rows");
    Reject(() => capacityLibrary.MergeImport(SyntheticImport(2000, 1, "Another row.")), "merge cannot exceed total capacity");
    Check(capacityLibrary.ExportJson() == capacityBefore, "rejected capacity changes preserve every existing row");
    var sizeLibrary = new SayingLibrary(Path.Combine(root, "aggregate-size"), null);
    var longUnicode = string.Concat(Enumerable.Repeat("😀", 240));
    sizeLibrary.MergeImport(SyntheticImport(0, 300, longUnicode));
    var sizeBefore = sizeLibrary.ExportJson();
    Check(Encoding.UTF8.GetByteCount(sizeBefore) <= SayingLibrary.MaximumJsonBytes, "accepted libraries remain exportable within the JSON limit");
    Reject(() => sizeLibrary.PreviewImport(SyntheticImport(300, 100, longUnicode)), "preview validates merged JSON size, not just incoming bytes");
    Reject(() => sizeLibrary.MergeImport(SyntheticImport(300, 100, longUnicode)), "merge cannot leave an unexportable aggregate library");
    Check(sizeLibrary.ExportJson() == sizeBefore, "oversized aggregate merge preserves all prior data");
    var futureDirectory = Path.Combine(root, "future-schema"); Directory.CreateDirectory(futureDirectory);
    using (var connection = new SqliteConnection($"Data Source={Path.Combine(futureDirectory, "omnikit.db")};Pooling=False"))
    {
        connection.Open(); using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE OmniKitSayingLibraryMeta(Id INTEGER PRIMARY KEY,SchemaVersion INTEGER,SeedRevision INTEGER,Revision INTEGER); INSERT INTO OmniKitSayingLibraryMeta VALUES(1,2,99,12); PRAGMA user_version=14;";
        command.ExecuteNonQuery();
    }
    Reject(() => _ = new SayingLibrary(futureDirectory, null), "future library schema is refused without downgrade");
    using (var connection = new SqliteConnection($"Data Source={Path.Combine(futureDirectory, "omnikit.db")};Pooling=False"))
    {
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "SELECT SchemaVersion FROM OmniKitSayingLibraryMeta WHERE Id=1";
        Check(Convert.ToInt32(command.ExecuteScalar()) == 2, "unsupported library metadata survives a failed open");
    }
    var publicSeed = args.FirstOrDefault(argument => argument.StartsWith("--seed=", StringComparison.Ordinal))?[7..];
    if (publicSeed != null)
    {
        var seeded = new SayingLibrary(Path.Combine(root, "public-seed"), publicSeed);
        using var publicDocument = JsonDocument.Parse(File.ReadAllText(publicSeed));
        Check(seeded.GetAll().Count == publicDocument.RootElement.GetProperty("entries").GetArrayLength()
            && seeded.GetAll().All(entry => entry.Enabled && entry.BuiltIn && !entry.Deleted),
            "shipped public seed pack passes the production parser and populates only temporary storage");
        Console.WriteLine($"Public seed pack: {seeded.GetAll().Count} sayings accepted across {seeded.GetAll().Select(entry => entry.Companion).Distinct().Count()} scopes.");
    }
    Console.WriteLine($"PASS: {checks} sayings storage and selection checks; temporary test storage only.");
}
finally
{
    var resolved = Path.GetFullPath(root);
    var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("OmniKit-sayings-synthetic-", StringComparison.Ordinal))
        throw new InvalidOperationException("Refusing cleanup outside the named synthetic test directory.");
    Directory.Delete(resolved, true);
}
