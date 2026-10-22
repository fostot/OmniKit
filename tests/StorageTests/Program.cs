using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Game.ClientState.Keys;
using Microsoft.Data.Sqlite;
using OmniKit;
using OmniKit.Input;
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
        AvatarEnabled = false,
        InteractModifier = ModifierKeys.Control | ModifierKeys.Shift,
        InteractionHudLocked = false,
        InteractionHudOffsetX = -220f,
        InteractionHudScale = 1.5f,
        CompanionBoxLeft = .2f,
        CompanionSize = 120f,
        CompanionOutfit = "Stargazer",
        CompanionPalette = "Nebula",
        CompanionMagicEnabled = false,
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

    var versionTwoFields = new[]
    {
        "InteractModifier", "InteractionHudLocked", "InteractionHudOffsetX", "InteractionHudScale",
        "CompanionBoxLeft", "CompanionBoxTop", "CompanionBoxRight", "CompanionBoxBottom", "CompanionSize",
    };
    var versionThreeFields = new[] { "CompanionOutfit", "CompanionPalette", "CompanionMagicEnabled" };
    var versionTwoDocument = JsonNode.Parse(exported)!;
    versionTwoDocument["SchemaVersion"] = 2;
    versionTwoDocument["Settings"]!["Version"] = 2;
    foreach (var field in versionThreeFields) versionTwoDocument["Settings"]!.AsObject().Remove(field);
    var versionTwoJson = versionTwoDocument.ToJsonString();
    var legacyDocument = JsonNode.Parse(exported)!;
    legacyDocument["SchemaVersion"] = 1;
    var legacySettings = legacyDocument["Settings"]!.AsObject();
    legacySettings["Version"] = 1;
    legacySettings["LauncherModifier"] = 18;
    legacySettings["SettingsModifier"] = 17;
    foreach (var field in versionTwoFields.Concat(versionThreeFields)) legacySettings.Remove(field);
    var legacyJson = legacyDocument.ToJsonString();

    void CheckLegacy(Configuration migrated)
    {
        Check(migrated.Version == 3 && migrated.LauncherModifier == ModifierKeys.Alt && migrated.SettingsModifier == ModifierKeys.Control &&
            migrated.InteractModifier == ModifierKeys.None, "Legacy virtual-key modifiers must migrate to the correct masks.");
        var snapshot = JsonNode.Parse(store.Export(migrated))!["Settings"]!.AsObject();
        foreach (var field in legacySettings)
            if (field.Key is not ("Version" or "LauncherModifier" or "SettingsModifier"))
                Check(JsonNode.DeepEquals(snapshot[field.Key], field.Value), "Migration must preserve legacy field " + field.Key);
        var defaults = new Configuration();
        var defaultSnapshot = JsonNode.Parse(store.Export(defaults))!["Settings"]!.AsObject();
        foreach (var field in versionTwoFields.Concat(versionThreeFields))
            Check(JsonNode.DeepEquals(snapshot[field], defaultSnapshot[field]), "Migration must use the default for new field " + field);
    }

    var legacyTarget = new Configuration();
    store.ImportInto(legacyTarget, legacyJson);
    CheckLegacy(legacyTarget);
    var deserializedLegacy = JsonSerializer.Deserialize<Configuration>(legacySettings.ToJsonString(), new JsonSerializerOptions { IncludeFields = true })!;
    CheckLegacy(store.Upgrade(deserializedLegacy));
    var legacyFallback = new Configuration
    {
        Version = 1,
        LauncherModifier = (ModifierKeys)18,
        SettingsModifier = (ModifierKeys)17,
        InteractionHudOffsetY = -73f,
        AvatarEnabled = false,
        AnimationsEnabled = false,
    };
    var upgradedFallback = store.Upgrade(legacyFallback);
    Check(upgradedFallback.Version == 3 && upgradedFallback.LauncherModifier == ModifierKeys.Alt &&
        upgradedFallback.SettingsModifier == ModifierKeys.Control && upgradedFallback.InteractionHudOffsetY == -73f &&
        !upgradedFallback.AvatarEnabled && !upgradedFallback.AnimationsEnabled &&
        legacyFallback.Version == 1 && (int)legacyFallback.LauncherModifier == 18,
        "The legacy Dalamud configuration fallback must upgrade a validated copy and retain disabled features.");

    string LegacyDatabase(string name, string json, int schema = 1)
    {
        var directory = Path.Combine(testDirectory, name);
        Directory.CreateDirectory(directory);
        using var database = new SqliteConnection($"Data Source={Path.Combine(directory, "omnikit.db")};Pooling=False");
        database.Open();
        using var command = database.CreateCommand();
        command.CommandText = """
            CREATE TABLE OmniKitSettings (Id INTEGER PRIMARY KEY CHECK (Id = 1), SchemaVersion INTEGER NOT NULL, Json TEXT NOT NULL);
            INSERT INTO OmniKitSettings (Id, SchemaVersion, Json) VALUES (1, $schema, $json);
            """;
        command.Parameters.AddWithValue("$json", json);
        command.Parameters.AddWithValue("$schema", schema);
        command.ExecuteNonQuery();
        command.CommandText = "PRAGMA user_version = " + schema;
        command.Parameters.Clear();
        command.ExecuteNonQuery();
        return directory;
    }

    (int DatabaseSchema, int RowSchema, string Json) ReadDatabase(string directory)
    {
        using var database = new SqliteConnection($"Data Source={Path.Combine(directory, "omnikit.db")};Pooling=False");
        database.Open();
        using var command = database.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var schema = Convert.ToInt32(command.ExecuteScalar());
        command.CommandText = "SELECT SchemaVersion, Json FROM OmniKitSettings WHERE Id = 1;";
        using var row = command.ExecuteReader();
        Check(row.Read(), "The saved row must remain present.");
        return (schema, row.GetInt32(0), row.GetString(1));
    }

    var legacyDirectory = LegacyDatabase("legacy-valid", legacyJson);
    var migratedStore = new LocalStore(legacyDirectory);
    CheckLegacy(migratedStore.Load()!);
    var migratedRow = ReadDatabase(legacyDirectory);
    Check(migratedRow.DatabaseSchema == 3 && migratedRow.RowSchema == 3 && JsonNode.Parse(migratedRow.Json)!["SchemaVersion"]!.GetValue<int>() == 3,
        "Database version, row version and JSON schema must migrate together.");
    CheckLegacy(new LocalStore(legacyDirectory).Load()!);

    void CheckVersionTwo(Configuration migrated)
    {
        Check(migrated.Version == 3 && migrated.CompanionOutfit == "Navigator" && migrated.CompanionPalette == "Moonlight" && migrated.CompanionMagicEnabled,
            "Version two must gain the new companion defaults.");
        var snapshot = JsonNode.Parse(store.Export(migrated))!["Settings"]!.AsObject();
        foreach (var field in versionTwoDocument["Settings"]!.AsObject())
            if (field.Key != "Version") Check(JsonNode.DeepEquals(snapshot[field.Key], field.Value), "Version two migration must preserve " + field.Key);
    }
    var versionTwoTarget = new Configuration();
    store.ImportInto(versionTwoTarget, versionTwoJson);
    CheckVersionTwo(versionTwoTarget);
    var versionTwoFallback = JsonSerializer.Deserialize<Configuration>(versionTwoDocument["Settings"]!.ToJsonString(), new JsonSerializerOptions { IncludeFields = true })!;
    CheckVersionTwo(store.Upgrade(versionTwoFallback));
    var versionTwoDirectory = LegacyDatabase("version-two", versionTwoJson, 2);
    CheckVersionTwo(new LocalStore(versionTwoDirectory).Load()!);
    Check(ReadDatabase(versionTwoDirectory).DatabaseSchema == 3 && ReadDatabase(versionTwoDirectory).RowSchema == 3,
        "Version two SQLite schema and row must migrate together.");

    var badLegacy = JsonNode.Parse(legacyJson)!;
    badLegacy["Settings"]!["InteractionDistance"] = 99;
    var badLegacyJson = badLegacy.ToJsonString();
    var failedDirectory = LegacyDatabase("legacy-invalid", badLegacyJson);
    var migrationRejected = false;
    try { _ = new LocalStore(failedDirectory); } catch (InvalidOperationException) { migrationRejected = true; }
    var preservedRow = ReadDatabase(failedDirectory);
    Check(migrationRejected && preservedRow.DatabaseSchema == 1 && preservedRow.RowSchema == 1 && preservedRow.Json == badLegacyJson,
        "A failing migration must roll back the database version and preserve the original row verbatim.");
    var writeFailureDirectory = LegacyDatabase("legacy-write-error", legacyJson);
    using (var database = new SqliteConnection($"Data Source={Path.Combine(writeFailureDirectory, "omnikit.db")};Pooling=False"))
    {
        database.Open();
        using var command = database.CreateCommand();
        command.CommandText = "CREATE TRIGGER RejectMigration BEFORE UPDATE ON OmniKitSettings BEGIN SELECT RAISE(ABORT, 'Simulated write error'); END;";
        command.ExecuteNonQuery();
    }
    var writeRejected = false;
    try { _ = new LocalStore(writeFailureDirectory); } catch (SqliteException) { writeRejected = true; }
    var writeFailureRow = ReadDatabase(writeFailureDirectory);
    Check(writeRejected && writeFailureRow.DatabaseSchema == 1 && writeFailureRow.RowSchema == 1 && writeFailureRow.Json == legacyJson,
        "A database write failure during migration must preserve the legacy schema and complete original snapshot.");

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
    Invalid(exported.Replace("\"SchemaVersion\": 3", "\"SchemaVersion\": 4"), "Future schema");
    Invalid(exported.Replace("\"SchemaVersion\": 3", "\"SchemaVersion\": 3, \"SchemaVersion\": 3"), "Duplicate root fields");
    Invalid(exported.Replace("\"Avatar\": \"Moonbear\"", "\"Avatar\": \"Moonbear\", \"Avatar\": \"Bimble\""), "Duplicate nested fields");
    Invalid(Change("UnapprovedFutureField", "should not import"), "Unknown fields");
    var missing = JsonNode.Parse(exported)!;
    missing["Settings"]!.AsObject().Remove("Avatar");
    Invalid(missing.ToJsonString(), "Missing fields");
    Invalid(Change("LauncherEnabled", "true"), "Wrong boolean type");
    Invalid(Change("Avatar", null), "Null string");
    Invalid(Change("CompanionOutfit", "Unknown"), "Unknown outfit");
    Invalid(Change("CompanionPalette", "Unknown"), "Unknown palette");
    Invalid(Change("CompanionMagicEnabled", "true"), "Wrong effect flag type");
    foreach (var avatar in Configuration.AvatarNames)
    {
        var avatarTarget = new Configuration();
        store.ImportInto(avatarTarget, Change("Avatar", avatar));
        Check(avatarTarget.Avatar == avatar, "Every supported companion must import and export.");
    }
    Invalid(Change("InteractionDistance", 99), "Invalid interaction distance");
    foreach (var distance in new[] { Configuration.MinimumDetectionDistance, 12f, Configuration.MaximumDetectionDistance })
    {
        var distanceTarget = new Configuration();
        store.ImportInto(distanceTarget, Change("InteractionDistance", distance));
        store.Save(distanceTarget);
        Check(store.Load()!.InteractionDistance == distance, "The full detection distance range must survive import and database reload.");
    }
    Invalid(Change("InteractionDistance", Configuration.MaximumDetectionDistance + .1f), "Detection distance above maximum");
    Invalid(Change("InteractionDistance", Configuration.MinimumDetectionDistance - .1f), "Detection distance below minimum");
    Invalid(Change("InteractionHudOffsetX", 10001), "Invalid HUD offset");
    Invalid(Change("InteractionHudScale", 0.49), "Invalid HUD scale");
    Invalid(Change("CompanionBoxLeft", .99), "Inverted companion rectangle");
    Invalid(Change("CompanionBoxBottom", .5), "Inverted companion rectangle height");
    Invalid(Change("CompanionBoxRight", 1.1), "Companion rectangle outside viewport");
    Invalid(Change("CompanionSize", 193), "Invalid companion size");
    Invalid(Change("MinerGearset", 100), "Invalid gearset index");
    Invalid(Change("WikiOverlayName", "an invalid overlay"), "Invalid wiki overlay name");
    Invalid(Change("InteractKey", 1), "Mouse key binding");
    Invalid(Change("InteractKey", 65536 + 69), "Overflowed key code");
    Invalid(Change("SettingsKey", 17), "Modifier-only key binding");
    Invalid(Change("LauncherModifier", 65), "Invalid modifier");
    Invalid(Change("LauncherModifier", 162), "Unsupported side-specific modifier");
    Invalid(Change("InteractModifier", 8), "Unknown modifier bit");
    Invalid(Change("Version", 1), "Mixed configuration and snapshot versions");
    Invalid(badLegacyJson, "Invalid legacy import");
    var badLegacyModifier = JsonNode.Parse(legacyJson)!;
    badLegacyModifier["Settings"]!["SettingsModifier"] = 65;
    Invalid(badLegacyModifier.ToJsonString(), "Invalid legacy modifier");
    Invalid(Change("InteractKey", 162), "Side-specific modifier as interact key");
    var collision = JsonNode.Parse(exported)!;
    collision["Settings"]!["LauncherKey"] = (int)VirtualKey.F;
    collision["Settings"]!["LauncherModifier"] = (int)(ModifierKeys.Control | ModifierKeys.Shift);
    Invalid(collision.ToJsonString(), "Shortcut collision");
    collision["Settings"]!["LauncherModifier"] = (int)ModifierKeys.Control;
    var distinctChords = new Configuration();
    store.ImportInto(distinctChords, collision.ToJsonString());
    Check(distinctChords.LauncherKey == distinctChords.InteractKey && distinctChords.LauncherModifier != distinctChords.InteractModifier,
        "The same main key is valid with distinct exact modifier masks.");
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
    Console.WriteLine("Storage checks passed: v1/v2 JSON/Dalamud/SQLite migration to v3, rollback, companion wardrobe/palette/effects persistence, atomic validation, export whitelist and AI consent.");
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
