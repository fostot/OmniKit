using System.Text.Json.Nodes;
using Dalamud.Game.ClientState.Keys;
using Microsoft.Data.Sqlite;
using OmniKit;
using OmniKit.Storage;

var testDirectory = Path.Combine(Path.GetTempPath(), "OmniKit-storage-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testDirectory);
try
{
    var store = new LocalStore(testDirectory);
    Check(store.Load() is null, "An empty database must not invent settings.");
    var original = new Configuration
    {
        SetupCompleted = true,
        InteractionDistance = 4.5f,
        MinerGearset = 2,
        Avatar = "Moonbear",
        VoiceBindings = [new VoiceBinding("show my bags", "game:123")],
    };
    Action<Configuration> callback = _ => throw new Exception("Import must not call persistence implicitly.");
    original.Persist = callback;
    var exported = store.Export(original);
    var expectedFields = typeof(Configuration).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
        .Select(field => field.Name).Append(nameof(Configuration.Version)).Order().ToArray();
    var exportedFields = JsonNode.Parse(exported)!["Settings"]!.AsObject().Select(field => field.Key).Order().ToArray();
    Check(expectedFields.SequenceEqual(exportedFields), "Every current settings field must have an explicit whitelist entry.");
    Check(!exported.Contains("Persist", StringComparison.Ordinal), "Persistence callbacks must not be exported.");
    Check(!exported.Contains("ApiKey", StringComparison.OrdinalIgnoreCase), "Credentials must not be exported.");
    store.Save(original);
    var loaded = store.Load() ?? throw new Exception("Saved settings were not loaded.");
    Check(store.Export(loaded) == exported, "Database round-trip must preserve all whitelisted fields.");
    loaded.InteractionDistance = 5;
    store.Save(loaded);
    Check(store.Load()!.InteractionDistance == 5, "A later save must atomically replace the prior snapshot.");

    void Invalid(string json, string description)
    {
        var before = store.Export(original);
        var priorBindings = original.VoiceBindings;
        var rejected = false;
        try { store.ImportInto(original, json); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, description + " must be rejected.");
        Check(store.Export(original) == before && ReferenceEquals(priorBindings, original.VoiceBindings) && ReferenceEquals(callback, original.Persist),
            description + " must not partially change the target.");
    }

    string Change(string field, JsonNode? value)
    {
        var document = JsonNode.Parse(exported)!;
        document["Settings"]![field] = value;
        return document.ToJsonString();
    }

    Invalid("{", "Malformed JSON");
    Invalid("null", "Null root");
    Invalid(new string(' ', 262145), "Oversize input");
    Invalid(new string('界', 100000), "Oversize UTF-8 input");
    Invalid(exported.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 2"), "Future schema");
    Invalid(exported.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 1, \"SchemaVersion\": 1"), "Duplicate root fields");
    Invalid(exported.Replace("\"Avatar\": \"Moonbear\"", "\"Avatar\": \"Moonbear\", \"Avatar\": \"Bimble\""), "Duplicate nested fields");
    Invalid(Change("UnapprovedFutureField", "should not import"), "Unknown fields");
    var missing = JsonNode.Parse(exported)!;
    missing["Settings"]!.AsObject().Remove("Avatar");
    Invalid(missing.ToJsonString(), "Missing fields");
    Invalid(Change("LauncherEnabled", "true"), "Wrong boolean type");
    Invalid(Change("Avatar", null), "Null string");
    Invalid(Change("InteractionDistance", 99), "Invalid interaction distance");
    Invalid(Change("MinerGearset", 100), "Invalid gearset index");
    Invalid(Change("WikiOverlayName", "an invalid overlay"), "Invalid wiki overlay name");
    Invalid(Change("InteractKey", 1), "Mouse key binding");
    Invalid(Change("SettingsKey", 17), "Modifier-only key binding");
    Invalid(Change("LauncherModifier", 65), "Invalid modifier");
    Invalid(Change("LauncherModifier", 162), "Unsupported side-specific modifier");
    Invalid(Change("InteractKey", 162), "Side-specific modifier as interact key");
    var collision = JsonNode.Parse(exported)!;
    collision["Settings"]!["LauncherKey"] = (int)VirtualKey.F;
    collision["Settings"]!["LauncherModifier"] = 0;
    Invalid(collision.ToJsonString(), "Shortcut collision");
    Invalid(Change("AiEndpoint", "https://user:secret@example.invalid/v1/responses"), "Credential-bearing endpoint");
    Invalid(Change("AiEndpoint", "https://example.invalid/v1/responses?key=secret"), "Query-bearing endpoint");
    Invalid(Change("AiEndpoint", "http://example.invalid/v1/responses"), "Remote unencrypted endpoint");
    Invalid(Change("AiEndpoint", "file:///example"), "Non-HTTP endpoint");
    Invalid(Change("VoiceBindings", null), "Null binding list");
    Invalid(Change("VoiceBindings", new JsonArray
    {
        new JsonObject { ["Phrase"] = "Open Bags", ["ActionId"] = "game:1" },
        new JsonObject { ["Phrase"] = " open  bags ", ["ActionId"] = "game:2" },
    }), "Duplicate voice phrases");
    Invalid(Change("VoiceBindings", new JsonArray
    {
        new JsonObject { ["Phrase"] = "Open Bags!", ["ActionId"] = "game:1" },
        new JsonObject { ["Phrase"] = "open bags", ["ActionId"] = "game:2" },
    }), "Punctuation-equivalent voice phrases");
    Invalid(Change("VoiceBindings", new JsonArray
    {
        new JsonObject { ["Phrase"] = "!!!", ["ActionId"] = "game:1" },
    }), "Empty normalized voice phrase");
    var badBinding = new JsonArray { new JsonObject { ["Phrase"] = "bags", ["ActionId"] = "game:1", ["Command"] = "unused" } };
    Invalid(Change("VoiceBindings", badBinding), "Unknown binding fields");

    var local = new Configuration();
    store.ImportInto(local, Change("AiEndpoint", "http://127.0.0.1:1234/v1/responses"));
    Check(local.AiEndpoint.StartsWith("http://127.0.0.1", StringComparison.Ordinal), "Explicit literal loopback HTTP must be supported.");
    var aiOn = JsonNode.Parse(exported)!;
    aiOn["Settings"]!["AiEnabled"] = true;
    store.ImportInto(original, aiOn.ToJsonString());
    Check(!original.AiEnabled, "Import cannot grant new AI consent.");
    original.AiEnabled = true;
    store.ImportInto(original, aiOn.ToJsonString());
    Check(original.AiEnabled, "Import can retain existing consent for the same provider endpoint.");
    aiOn["Settings"]!["AiEndpoint"] = "https://another.example.invalid/v1/responses";
    store.ImportInto(original, aiOn.ToJsonString());
    Check(!original.AiEnabled, "Changing provider endpoint requires fresh consent.");
    original.AiEnabled = true;
    store.Save(original);
    Check(store.Load()!.AiEnabled, "Local database restore must preserve prior local consent.");

    var beforeRejectedSave = store.Export(store.Load()!);
    original.InteractionDistance = float.NaN;
    var failedSave = false;
    try { store.Save(original); } catch (InvalidOperationException) { failedSave = true; }
    Check(failedSave && store.Export(store.Load()!) == beforeRejectedSave, "Invalid saves must preserve the previous database snapshot.");
    using (var database = new SqliteConnection($"Data Source={Path.Combine(testDirectory, "omnikit.db")};Pooling=False"))
    {
        database.Open();
        using var command = database.CreateCommand();
        command.CommandText = "PRAGMA user_version = 99;";
        command.ExecuteNonQuery();
    }
    var futureRejected = false;
    try { _ = new LocalStore(testDirectory); } catch (InvalidOperationException) { futureRejected = true; }
    Check(futureRejected, "Future database versions must be preserved and rejected.");
    Console.WriteLine("Storage checks passed: SQLite persistence, atomic validation, export whitelist, schema/type/range/key/URL/voice checks and AI consent.");
}
finally
{
    // The path was generated above under the temp directory exclusively for this test.
    var resolved = Path.GetFullPath(testDirectory);
    var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
        !Path.GetFileName(resolved).StartsWith("OmniKit-storage-test-", StringComparison.Ordinal))
        throw new InvalidOperationException("Refusing cleanup outside the generated test directory.");
    Directory.Delete(resolved, true);
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
