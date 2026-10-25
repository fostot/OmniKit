using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using OmniKit;
using OmniKit.Storage;

internal static class AccessoryFitChecks
{
    public static int Run(string testRoot)
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
        }

        var store = new LocalStore(Path.Combine(testRoot, "accessory-fits"));
        var fit = new CompanionAccessoryAdjustment("Wink", "HolidayStarlight", "AllSaintsWake", -.4f, .7f, 1.25f);
        var source = new Configuration
        {
            CompanionHolidayBody = "Starlight", CompanionSeasonalTheme = "AllSaintsWake",
            CompanionHolidayBodiesEnabled = false, CompanionAccessoryFits = [fit], AiEnabled = true,
            CompanionTransitionEffectsEnabled = false,
        };
        var json = store.Export(source);
        var imported = new Configuration();
        store.ImportInto(imported, json);
        Check(imported.CompanionHolidayBody == "Starlight" && imported.CompanionSeasonalTheme == "AllSaintsWake" &&
            !imported.CompanionHolidayBodiesEnabled && !imported.CompanionTransitionEffectsEnabled && imported.CompanionAccessoryFits.SequenceEqual(source.CompanionAccessoryFits),
            "Body selection and measured accessory fits round-trip independently of enable and accessory-theme preferences.");
        Check(!imported.AiEnabled, "Importing fit preferences cannot grant AI consent.");
        store.Save(source);
        var loaded = store.Load()!;
        Check(store.Export(loaded) == json && loaded.AiEnabled, "The explicit settings DTO preserves fits and existing consent in SQLite.");
        var upgraded = store.Upgrade(source);
        Check(!ReferenceEquals(upgraded.CompanionAccessoryFits, source.CompanionAccessoryFits) &&
            !ReferenceEquals(upgraded.CompanionAccessoryFits[0], source.CompanionAccessoryFits[0]) &&
            !ReferenceEquals(imported.CompanionAccessoryFits, source.CompanionAccessoryFits) &&
            !ReferenceEquals(loaded.CompanionAccessoryFits, source.CompanionAccessoryFits),
            "Upgrade, import and load detach both the fit list and its records.");
        source.CompanionAccessoryFits[0] = fit with { Scale = 2f };
        source.CompanionAccessoryFits.Add(fit with { Avatar = "Nova" });
        Check(upgraded.CompanionAccessoryFits.SequenceEqual(new[] { fit }) &&
            imported.CompanionAccessoryFits.SequenceEqual(new[] { fit }) &&
            loaded.CompanionAccessoryFits.SequenceEqual(new[] { fit }) && store.Export(store.Load()!) == json,
            "Mutating the source list cannot alter an upgraded/imported/loaded copy or a saved snapshot.");

        var target = new Configuration { CompanionAccessoryFits = [fit], AiEnabled = true };
        Action<Configuration> callback = _ => throw new Exception("Import must not save implicitly.");
        target.Persist = callback;
        void Invalid(string document, string reason)
        {
            var before = store.Export(target);
            var prior = target.CompanionAccessoryFits;
            var rejected = false;
            try { store.ImportInto(target, document); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && store.Export(target) == before && ReferenceEquals(prior, target.CompanionAccessoryFits) &&
                ReferenceEquals(callback, target.Persist), reason + " must be rejected without partial mutation.");
        }
        string Change(string property, JsonNode? value)
        {
            var document = JsonNode.Parse(json)!;
            document["Settings"]![property] = value;
            return document.ToJsonString();
        }
        JsonObject FitNode() => JsonSerializer.SerializeToNode(fit)!.AsObject();
        string FitChange(string property, JsonNode? value)
        {
            var valueNode = FitNode(); valueNode[property] = value;
            return Change("CompanionAccessoryFits", new JsonArray(valueNode));
        }
        foreach (var name in Configuration.SeasonalThemeNames)
        {
            var selection = new Configuration(); store.ImportInto(selection, Change("CompanionHolidayBody", name));
            Check(selection.CompanionHolidayBody == name, "All canonical holiday body choices round-trip.");
        }
        foreach (var value in new JsonNode?[] { null, JsonValue.Create("Random"), JsonValue.Create("starlight"), JsonValue.Create(1), JsonValue.Create(false) })
            Invalid(Change("CompanionHolidayBody", value), "Invalid holiday body key/type");
        foreach (var value in new JsonNode?[] { null, JsonValue.Create("true"), JsonValue.Create(1), new JsonArray() })
            Invalid(Change("CompanionTransitionEffectsEnabled", value), "Invalid transition preference type");
        foreach (var enabled in new[] { false, true })
        {
            var preferences = new Configuration { CompanionTransitionEffectsEnabled = enabled, AiEnabled = true };
            var transitionJson = store.Export(preferences);
            var transitionTarget = new Configuration(); store.ImportInto(transitionTarget, transitionJson);
            Check(transitionTarget.CompanionTransitionEffectsEnabled == enabled && !transitionTarget.AiEnabled,
                "Either transition preference imports without granting new AI consent.");
            transitionTarget.AiEnabled = true; store.ImportInto(transitionTarget, transitionJson);
            Check(transitionTarget.AiEnabled && transitionTarget.CompanionTransitionEffectsEnabled == enabled,
                "Either transition preference preserves existing same-provider consent.");
            store.Save(preferences);
            Check(store.Load() is { AiEnabled: true } restored && restored.CompanionTransitionEffectsEnabled == enabled,
                "Either transition preference round-trips through SQLite with local consent retained.");
        }
        var themes = Configuration.SeasonalThemeNames.Skip(1).ToArray();
        var looks = new[] { "Default", "Look1", "Look2", "Look3", "Look4" }.Concat(themes.Select(theme => "Holiday" + theme)).ToArray();
        foreach (var (key, values) in new[] { ("Avatar", Configuration.AvatarNames.ToArray()), ("Look", looks), ("Theme", themes) })
        {
            foreach (var value in values)
            {
                var preference = new Configuration(); store.ImportInto(preference, FitChange(key, value));
                Check(preference.CompanionAccessoryFits.Count == 1, "Every supported fit key is accepted.");
            }
            foreach (var value in new JsonNode?[] { null, JsonValue.Create(""), JsonValue.Create("Random"), JsonValue.Create("Auto"),
                JsonValue.Create("unknown"), JsonValue.Create(1), JsonValue.Create(true) })
                Invalid(FitChange(key, value), "Invalid fit key/type " + key);
        }
        Invalid(FitChange("Avatar", "wink"), "Noncanonical avatar casing");
        Invalid(FitChange("Look", "HolidayAuto"), "Noncanonical holiday look");
        Invalid(FitChange("Theme", "starlight"), "Noncanonical theme casing");
        foreach (var (field, minimum, maximum) in new[] { ("OffsetX", -1f, 1f), ("OffsetY", -1f, 1f), ("Scale", .25f, 3f) })
        {
            foreach (var value in new[] { minimum, maximum })
            {
                var preference = new Configuration(); store.ImportInto(preference, FitChange(field, value));
                Check(preference.CompanionAccessoryFits.Count == 1, "Fit numeric endpoints remain valid.");
            }
            foreach (var value in new JsonNode?[] { null, JsonValue.Create(minimum - .001f), JsonValue.Create(maximum + .001f),
                JsonValue.Create("NaN"), JsonValue.Create("1"), JsonValue.Create(true), JsonNode.Parse("1e999") })
                Invalid(FitChange(field, value), "Invalid fit number " + field);
        }
        foreach (var field in FitNode().Select(property => property.Key).ToArray())
        {
            var missing = FitNode(); missing.Remove(field);
            Invalid(Change("CompanionAccessoryFits", new JsonArray(missing)), "Missing fit field " + field);
            var duplicateJson = JsonSerializer.Serialize(fit).Replace("\"" + field + "\":", "\"" + field + "\":null,\"" + field + "\":", StringComparison.Ordinal);
            Invalid(Change("CompanionAccessoryFits", new JsonArray()).Replace("\"CompanionAccessoryFits\":[]",
                "\"CompanionAccessoryFits\":[" + duplicateJson + "]", StringComparison.Ordinal), "Duplicate fit field " + field);
        }
        Invalid(FitChange("Extra", "unapproved"), "Unknown fit field");
        foreach (var value in new JsonNode?[] { null, new JsonObject(), JsonValue.Create("[]"), new JsonArray((JsonNode?)null), new JsonArray(JsonValue.Create(1)) })
            Invalid(Change("CompanionAccessoryFits", value), "Invalid fit array/entry type");
        Invalid(Change("CompanionAccessoryFits", new JsonArray(FitNode(), FitNode())), "Duplicate fit identity");
        Invalid(Change("CompanionAccessoryFits", new JsonArray(Enumerable.Range(0, 1025).Select(_ => (JsonNode?)new JsonObject()).ToArray())), "Fit count above the cap");
        foreach (var field in new[] { "CompanionHolidayBody", "CompanionAccessoryFits", "CompanionTransitionEffectsEnabled" })
        {
            var missing = JsonNode.Parse(json)!; missing["Settings"]!.AsObject().Remove(field);
            Invalid(missing.ToJsonString(), "Missing schema ten field");
        }

        // Exercise every distinct identity at once; 8 avatars x 14 looks x 9 themes is
        // the complete current domain, and stays below the independent 1024-entry cap.
        var full = new Configuration
        {
            CompanionAccessoryFits = (from avatar in Configuration.AvatarNames from look in looks from theme in themes
                select new CompanionAccessoryAdjustment(avatar, look, theme, -.12345678f, -.9876543f, 2.9999998f)).ToList(),
        };
        var fullJson = store.Export(full);
        var fullTarget = new Configuration(); store.ImportInto(fullTarget, fullJson);
        store.Save(fullTarget);
        Check(fullTarget.CompanionAccessoryFits.Count == 1008 && store.Load()!.CompanionAccessoryFits.SequenceEqual(full.CompanionAccessoryFits),
            "The complete supported fit identity domain round-trips within the bounded JSON and SQLite contracts.");
        var beforeRejectedSave = store.Export(store.Load()!);
        full.VoiceBindings = Enumerable.Range(0, 100).Select(index => new VoiceBinding("fixture " + index + new string('界', 60), new string('界', 300))).ToList();
        var oversizedRejected = false;
        try { store.Save(full); } catch (InvalidOperationException) { oversizedRejected = true; }
        Check(oversizedRejected && store.Export(store.Load()!) == beforeRejectedSave,
            "Combined otherwise-valid fits and voice bindings cannot save a snapshot that exceeds the importer size limit.");
        foreach (var invalidFit in new CompanionAccessoryAdjustment?[] { null, fit with { Avatar = "Unknown" }, fit with { Scale = float.NaN },
            fit with { OffsetX = float.PositiveInfinity }, fit with { OffsetY = float.NegativeInfinity } })
        {
            var rejected = false;
            try { store.Save(new Configuration { CompanionAccessoryFits = [invalidFit!] }); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && store.Export(store.Load()!) == beforeRejectedSave, "Invalid native fit values leave the saved snapshot intact.");
        }
        foreach (var fits in new List<CompanionAccessoryAdjustment>?[] { null, [fit, fit], Enumerable.Repeat(fit, 1025).ToList() })
        {
            var rejected = false;
            try { store.Export(new Configuration { CompanionAccessoryFits = fits! }); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Invalid native fit collection cannot be exported.");
        }

        var legacy = JsonNode.Parse(json)!;
        legacy["SchemaVersion"] = 9; legacy["Settings"]!["Version"] = 9;
        legacy["Settings"]!.AsObject().Remove("CompanionHolidayBody"); legacy["Settings"]!.AsObject().Remove("CompanionAccessoryFits");
        legacy["Settings"]!.AsObject().Remove("CompanionTransitionEffectsEnabled");
        var legacyJson = legacy.ToJsonString();
        var fresh = new Configuration();
        Check(fresh.Version == 10 && fresh.CompanionHolidayBody == "Auto" && fresh.CompanionAccessoryFits.Count == 0 && fresh.CompanionTransitionEffectsEnabled,
            "New configurations default to automatic body selection, no fit overrides and enabled transition effects.");
        var fallback = JsonSerializer.Deserialize<Configuration>(legacy["Settings"]!.ToJsonString(), new JsonSerializerOptions { IncludeFields = true })!;
        fallback.CompanionHolidayBody = "TheRising"; fallback.CompanionAccessoryFits = [fit];
        fallback.CompanionTransitionEffectsEnabled = false;
        var fallbackBefore = JsonSerializer.Serialize(fallback, new JsonSerializerOptions { IncludeFields = true });
        var migratedFallback = store.Upgrade(fallback);
        Check(migratedFallback.Version == 10 && migratedFallback.CompanionHolidayBody == "Auto" && migratedFallback.CompanionAccessoryFits.Count == 0 && migratedFallback.CompanionTransitionEffectsEnabled &&
            JsonSerializer.Serialize(fallback, new JsonSerializerOptions { IncludeFields = true }) == fallbackBefore,
            "Legacy fallback initializes new fields on a detached copy and leaves its source untouched.");
        var legacyImport = new Configuration(); store.ImportInto(legacyImport, legacyJson);
        Check(!legacyImport.AiEnabled && legacyImport.CompanionHolidayBody == "Auto" && legacyImport.CompanionAccessoryFits.Count == 0 && legacyImport.CompanionTransitionEffectsEnabled,
            "Legacy import uses new defaults without granting consent.");
        legacyImport.AiEnabled = true; store.ImportInto(legacyImport, legacyJson);
        Check(legacyImport.AiEnabled, "Legacy import retains existing same-provider consent.");
        foreach (var field in new[] { "CompanionHolidayBody", "CompanionAccessoryFits", "CompanionTransitionEffectsEnabled" })
        {
            var mixed = legacy.DeepClone(); mixed["Settings"]![field] = JsonNode.Parse(json)!["Settings"]![field]!.DeepClone();
            Invalid(mixed.ToJsonString(), "Schema ten field in a schema nine snapshot");
        }
        string LegacyDatabase(string name, string document, bool failWrite = false)
        {
            var directory = Path.Combine(testRoot, name); Directory.CreateDirectory(directory);
            using var db = new SqliteConnection($"Data Source={Path.Combine(directory, "omnikit.db")};Pooling=False");
            db.Open(); using var command = db.CreateCommand();
            command.CommandText = "CREATE TABLE OmniKitSettings (Id INTEGER PRIMARY KEY, SchemaVersion INTEGER NOT NULL, Json TEXT NOT NULL); INSERT INTO OmniKitSettings VALUES (1, 9, $json); PRAGMA user_version = 9;";
            command.Parameters.AddWithValue("$json", document); command.ExecuteNonQuery();
            if (failWrite)
            {
                command.CommandText = "CREATE TRIGGER RejectMigration BEFORE UPDATE ON OmniKitSettings BEGIN SELECT RAISE(ABORT, 'Synthetic write failure'); END;";
                command.Parameters.Clear(); command.ExecuteNonQuery();
            }
            return directory;
        }
        (int Version, int RowVersion, string Json) ReadDatabase(string directory)
        {
            using var db = new SqliteConnection($"Data Source={Path.Combine(directory, "omnikit.db")};Pooling=False");
            db.Open(); using var command = db.CreateCommand(); command.CommandText = "PRAGMA user_version;";
            var version = Convert.ToInt32(command.ExecuteScalar()); command.CommandText = "SELECT SchemaVersion,Json FROM OmniKitSettings WHERE Id=1;";
            using var reader = command.ExecuteReader(); reader.Read(); return (version, reader.GetInt32(0), reader.GetString(1));
        }
        var legacyDirectory = LegacyDatabase("accessory-v9", legacyJson);
        var migrated = new LocalStore(legacyDirectory).Load()!;
        var row = ReadDatabase(legacyDirectory);
        Check(row.Version == 10 && row.RowVersion == 10 && JsonNode.Parse(row.Json)!["SchemaVersion"]!.GetValue<int>() == 10 &&
            migrated.AiEnabled && migrated.CompanionHolidayBody == "Auto" && migrated.CompanionAccessoryFits.Count == 0 && migrated.CompanionTransitionEffectsEnabled,
            "Schema nine database, row, and JSON migrate atomically with local consent retained.");
        var migratedSettings = JsonNode.Parse(store.Export(migrated))!["Settings"]!;
        foreach (var field in legacy["Settings"]!.AsObject())
            if (field.Key != "Version") Check(JsonNode.DeepEquals(migratedSettings[field.Key], field.Value), "Migration preserves prior field " + field.Key);
        var badLegacy = legacy.DeepClone(); badLegacy["Settings"]!["CompanionOrbitRingSpeed"] = 99f;
        foreach (var (name, document, failWrite) in new[] { ("accessory-v9-invalid", badLegacy.ToJsonString(), false), ("accessory-v9-write-failure", legacyJson, true) })
        {
            var directory = LegacyDatabase(name, document, failWrite);
            var rejected = false;
            try { _ = new LocalStore(directory); } catch (InvalidOperationException) { rejected = true; } catch (SqliteException) when (failWrite) { rejected = true; }
            Check(rejected && ReadDatabase(directory) == (9, 9, document), "A failed schema nine migration preserves the complete original row and both schema versions.");
        }
        return checks;
    }
}
