using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Game.ClientState.Keys;
using Microsoft.Data.Sqlite;
using OmniKit;
using OmniKit.Input;
using OmniKit.Interaction;
using OmniKit.Assistant;
using OmniKit.Storage;

var testDirectory = Path.Combine(Path.GetTempPath(), "OmniKit-storage-test-" + Guid.NewGuid().ToString("N"));
var checks = 0;
Directory.CreateDirectory(testDirectory);
try
{
    var store = new LocalStore(testDirectory);
    Check(store.Load() is null, "An empty database must not invent settings.");
    var original = new Configuration
    {
        SetupCompleted = true,
        InteractionDistance = 4.5f,
        InteractionTargets = InteractionTargets.QuestObjects | InteractionTargets.NeutralEnemies,
        EnemyInteractMode = EnemyInteractMode.TargetOnly,
        MinerGearset = 2,
        Avatar = "Moonbear",
        AvatarEnabled = false,
        InteractModifier = ModifierKeys.Control | ModifierKeys.Shift,
        InteractionHudLocked = false,
        InteractionHudOffsetX = -220f,
        InteractionHudScale = 1.5f,
        InteractionHudOutline = true,
        InteractionHudEffect = HudTextEffect.SoftGlow,
        InteractionHudEffectIntensity = 1.7f,
        InteractionHudEffectSpeed = 2.3f,
        InteractionHudEffectAnimated = false,
        CompanionBoxLeft = .2f,
        CompanionSize = 120f,
        CompanionOutfit = "Stargazer",
        CompanionPalette = "Nebula",
        CompanionMagicEnabled = false,
        CompanionFrameInteractionEnabled = true,
        CompanionPickupEnabled = false,
        CompanionPerchEnabled = false,
        CompanionContainmentEnabled = false,
        CompanionFallingEnabled = false,
        CompanionHomeEnabled = false,
        CompanionGrabHandEnabled = false,
        CompanionCommentsEnabled = false,
        CompanionCommentInterval = 137f,
        CompanionBlinkEnabled = false,
        CompanionBlinkInterval = 7f,
        AiConnection = AiConnectionKind.ChatGpt,
        ChatGptModel = "fixture-chatgpt-model",
        CodexModel = "fixture-codex-model",
        ClaudeModel = "fixture-claude-model",
        WikiOverlayName = "fixture-overlay",
        BrowserInterceptLinks = true,
        QuestItemUseEnabled = true,
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
    var versionFourFields = new[]
    {
        "InteractionTargets", "EnemyInteractMode", "CompanionFrameInteractionEnabled", "CompanionPickupEnabled",
        "CompanionPerchEnabled", "CompanionContainmentEnabled", "CompanionFallingEnabled",
        "AiConnection", "ChatGptModel", "CodexModel", "ClaudeModel",
    };
    var versionFiveFields = new[] { "InteractionHudOutline", "InteractionHudEffect" };
    var versionSixFields = new[] { "InteractionHudEffectIntensity", "InteractionHudEffectSpeed", "InteractionHudEffectAnimated",
        "CompanionHomeEnabled", "CompanionGrabHandEnabled", "CompanionCommentsEnabled", "CompanionCommentInterval", "CompanionBlinkEnabled", "CompanionBlinkInterval", "BrowserInterceptLinks", "QuestItemUseEnabled" };
    var versionFiveDocument = JsonNode.Parse(exported)!;
    versionFiveDocument["SchemaVersion"] = 5;
    versionFiveDocument["Settings"]!["Version"] = 5;
    versionFiveDocument["Settings"]!["WikiInGame"] = false;
    foreach (var field in versionSixFields) versionFiveDocument["Settings"]!.AsObject().Remove(field);
    var versionFiveJson = versionFiveDocument.ToJsonString();
    var versionFourDocument = versionFiveDocument.DeepClone();
    versionFourDocument["SchemaVersion"] = 4;
    versionFourDocument["Settings"]!["Version"] = 4;
    foreach (var field in versionFiveFields) versionFourDocument["Settings"]!.AsObject().Remove(field);
    var versionFourJson = versionFourDocument.ToJsonString();
    var versionThreeDocument = versionFourDocument.DeepClone();
    versionThreeDocument["SchemaVersion"] = 3;
    versionThreeDocument["Settings"]!["Version"] = 3;
    foreach (var field in versionFourFields) versionThreeDocument["Settings"]!.AsObject().Remove(field);
    var versionThreeJson = versionThreeDocument.ToJsonString();
    var versionTwoDocument = versionFourDocument.DeepClone();
    versionTwoDocument["SchemaVersion"] = 2;
    versionTwoDocument["Settings"]!["Version"] = 2;
    foreach (var field in versionThreeFields.Concat(versionFourFields)) versionTwoDocument["Settings"]!.AsObject().Remove(field);
    var versionTwoJson = versionTwoDocument.ToJsonString();
    var legacyDocument = versionFourDocument.DeepClone();
    legacyDocument["SchemaVersion"] = 1;
    var legacySettings = legacyDocument["Settings"]!.AsObject();
    legacySettings["Version"] = 1;
    legacySettings["LauncherModifier"] = 18;
    legacySettings["SettingsModifier"] = 17;
    foreach (var field in versionTwoFields.Concat(versionThreeFields).Concat(versionFourFields)) legacySettings.Remove(field);
    var legacyJson = legacyDocument.ToJsonString();

    void CheckLegacy(Configuration migrated)
    {
        Check(migrated.Version == 6 && migrated.LauncherModifier == ModifierKeys.Alt && migrated.SettingsModifier == ModifierKeys.Control &&
            migrated.InteractModifier == ModifierKeys.None, "Legacy virtual-key modifiers must migrate to the correct masks.");
        var snapshot = JsonNode.Parse(store.Export(migrated))!["Settings"]!.AsObject();
        foreach (var field in legacySettings)
            if (field.Key is not ("Version" or "LauncherModifier" or "SettingsModifier" or "WikiInGame"))
                Check(JsonNode.DeepEquals(snapshot[field.Key], field.Value), "Migration must preserve legacy field " + field.Key);
        var defaults = new Configuration();
        var defaultSnapshot = JsonNode.Parse(store.Export(defaults))!["Settings"]!.AsObject();
        foreach (var field in versionTwoFields.Concat(versionThreeFields).Concat(versionFourFields).Concat(versionFiveFields).Concat(versionSixFields))
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
    Check(upgradedFallback.Version == 6 && upgradedFallback.LauncherModifier == ModifierKeys.Alt &&
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
    Check(migratedRow.DatabaseSchema == 6 && migratedRow.RowSchema == 6 && JsonNode.Parse(migratedRow.Json)!["SchemaVersion"]!.GetValue<int>() == 6,
        "Database version, row version and JSON schema must migrate together.");
    CheckLegacy(new LocalStore(legacyDirectory).Load()!);

    void CheckVersionTwo(Configuration migrated)
    {
        Check(migrated.Version == 6 && migrated.CompanionOutfit == "Navigator" && migrated.CompanionPalette == "Moonlight" && migrated.CompanionMagicEnabled &&
            migrated.InteractionTargets == InteractionTargets.DefaultWorld && migrated.EnemyInteractMode == EnemyInteractMode.Choose,
            "Version two must gain the new companion defaults.");
        var snapshot = JsonNode.Parse(store.Export(migrated))!["Settings"]!.AsObject();
        foreach (var field in versionTwoDocument["Settings"]!.AsObject())
            if (field.Key is not ("Version" or "WikiInGame")) Check(JsonNode.DeepEquals(snapshot[field.Key], field.Value), "Version two migration must preserve " + field.Key);
    }
    var versionTwoTarget = new Configuration();
    store.ImportInto(versionTwoTarget, versionTwoJson);
    CheckVersionTwo(versionTwoTarget);
    var versionTwoFallback = JsonSerializer.Deserialize<Configuration>(versionTwoDocument["Settings"]!.ToJsonString(), new JsonSerializerOptions { IncludeFields = true })!;
    CheckVersionTwo(store.Upgrade(versionTwoFallback));
    var versionTwoDirectory = LegacyDatabase("version-two", versionTwoJson, 2);
    CheckVersionTwo(new LocalStore(versionTwoDirectory).Load()!);
    Check(ReadDatabase(versionTwoDirectory).DatabaseSchema == 6 && ReadDatabase(versionTwoDirectory).RowSchema == 6,
        "Version two SQLite schema and row must migrate together.");

    void CheckVersionThree(Configuration migrated)
    {
        Check(migrated.Version == 6 && migrated.InteractionTargets == InteractionTargets.DefaultWorld && migrated.EnemyInteractMode == EnemyInteractMode.Choose,
            "Version three must gain world targets with enemies off and no preselected enemy action.");
        Check(!migrated.CompanionFrameInteractionEnabled && migrated.CompanionPickupEnabled && migrated.CompanionPerchEnabled &&
            migrated.CompanionContainmentEnabled && migrated.CompanionFallingEnabled,
            "Frame interaction must stay off when upgrading old preferences, with its sub-options ready to opt in.");
        Check(migrated.AiConnection == AiConnectionKind.ApiKey && migrated.ChatGptModel == "" && migrated.CodexModel == "" && migrated.ClaudeModel == "",
            "Legacy AI preferences must keep the API connection and get separate empty model choices.");
        var snapshot = JsonNode.Parse(store.Export(migrated))!["Settings"]!.AsObject();
        foreach (var field in versionThreeDocument["Settings"]!.AsObject())
            if (field.Key is not ("Version" or "WikiInGame")) Check(JsonNode.DeepEquals(snapshot[field.Key], field.Value), "Version three migration must preserve " + field.Key);
    }
    var versionThreeTarget = new Configuration();
    store.ImportInto(versionThreeTarget, versionThreeJson);
    CheckVersionThree(versionThreeTarget);
    var versionThreeFallback = JsonSerializer.Deserialize<Configuration>(versionThreeDocument["Settings"]!.ToJsonString(), new JsonSerializerOptions { IncludeFields = true })!;
    CheckVersionThree(store.Upgrade(versionThreeFallback));
    var versionThreeDirectory = LegacyDatabase("version-three", versionThreeJson, 3);
    CheckVersionThree(new LocalStore(versionThreeDirectory).Load()!);
    var versionThreeRow = ReadDatabase(versionThreeDirectory);
    Check(versionThreeRow.DatabaseSchema == 6 && versionThreeRow.RowSchema == 6 && JsonNode.Parse(versionThreeRow.Json)!["SchemaVersion"]!.GetValue<int>() == 6,
        "Version three SQLite schema, row and JSON must migrate together.");

    void CheckVersionFour(Configuration migrated)
    {
        Check(migrated.Version == 6 && !migrated.InteractionHudOutline && migrated.InteractionHudEffect == HudTextEffect.Shadow,
            "Version four must retain its shadow appearance without enabling an outline.");
        var snapshot = JsonNode.Parse(store.Export(migrated))!["Settings"]!.AsObject();
        foreach (var field in versionFourDocument["Settings"]!.AsObject())
            if (field.Key is not ("Version" or "WikiInGame")) Check(JsonNode.DeepEquals(snapshot[field.Key], field.Value), "Version four migration must preserve " + field.Key);
    }
    var versionFourTarget = new Configuration();
    store.ImportInto(versionFourTarget, versionFourJson);
    CheckVersionFour(versionFourTarget);
    var versionFourFallback = JsonSerializer.Deserialize<Configuration>(versionFourDocument["Settings"]!.ToJsonString(), new JsonSerializerOptions { IncludeFields = true })!;
    CheckVersionFour(store.Upgrade(versionFourFallback));
    var versionFourDirectory = LegacyDatabase("version-four", versionFourJson, 4);
    CheckVersionFour(new LocalStore(versionFourDirectory).Load()!);
    var versionFourRow = ReadDatabase(versionFourDirectory);
    Check(versionFourRow.DatabaseSchema == 6 && versionFourRow.RowSchema == 6 && JsonNode.Parse(versionFourRow.Json)!["SchemaVersion"]!.GetValue<int>() == 6,
        "Version four SQLite schema, row and JSON must migrate together.");
    var badVersionFour = versionFourDocument.DeepClone();
    badVersionFour["Settings"]!["InteractionHudScale"] = 3;
    var badVersionFourJson = badVersionFour.ToJsonString();
    var badVersionFourDirectory = LegacyDatabase("version-four-invalid", badVersionFourJson, 4);
    var badVersionFourRejected = false;
    try { _ = new LocalStore(badVersionFourDirectory); } catch (InvalidOperationException) { badVersionFourRejected = true; }
    var preservedVersionFour = ReadDatabase(badVersionFourDirectory);
    Check(badVersionFourRejected && preservedVersionFour.DatabaseSchema == 4 && preservedVersionFour.RowSchema == 4 && preservedVersionFour.Json == badVersionFourJson,
        "An invalid version four migration must preserve the complete original schema and snapshot.");

    void CheckVersionFive(Configuration migrated)
    {
        Check(migrated.Version == 6 && migrated.InteractionHudEffectIntensity == 1f && migrated.InteractionHudEffectSpeed == 1f &&
            migrated.InteractionHudEffectAnimated && migrated.WikiInGame && migrated.CompanionHomeEnabled && migrated.CompanionGrabHandEnabled && migrated.CompanionCommentsEnabled &&
            migrated.CompanionCommentInterval == 120f && migrated.CompanionBlinkEnabled && migrated.CompanionBlinkInterval == 3f && !migrated.BrowserInterceptLinks,
            "Version five must gain default effect controls and always use the in-game browser.");
        var snapshot = JsonNode.Parse(store.Export(migrated))!["Settings"]!.AsObject();
        foreach (var field in versionFiveDocument["Settings"]!.AsObject())
            if (field.Key is not ("Version" or "WikiInGame"))
                Check(JsonNode.DeepEquals(snapshot[field.Key], field.Value), "Version five migration must preserve " + field.Key);
    }
    var versionFiveTarget = new Configuration();
    store.ImportInto(versionFiveTarget, versionFiveJson);
    CheckVersionFive(versionFiveTarget);
    var versionFiveFallback = JsonSerializer.Deserialize<Configuration>(versionFiveDocument["Settings"]!.ToJsonString(), new JsonSerializerOptions { IncludeFields = true })!;
    CheckVersionFive(store.Upgrade(versionFiveFallback));
    Check(versionFiveFallback.Version == 5 && !versionFiveFallback.WikiInGame, "Fallback upgrade must not mutate the source configuration.");
    var versionFiveDirectory = LegacyDatabase("version-five", versionFiveJson, 5);
    CheckVersionFive(new LocalStore(versionFiveDirectory).Load()!);
    var versionFiveRow = ReadDatabase(versionFiveDirectory);
    Check(versionFiveRow.DatabaseSchema == 6 && versionFiveRow.RowSchema == 6 && JsonNode.Parse(versionFiveRow.Json)!["SchemaVersion"]!.GetValue<int>() == 6,
        "Version five SQLite schema, row and JSON must migrate together.");

    foreach (var oldDocument in new[] { legacyDocument, versionTwoDocument, versionThreeDocument, versionFourDocument, versionFiveDocument })
    {
        var oldSchema = oldDocument["SchemaVersion"]!.GetValue<int>();
        var consentDocument = oldDocument.DeepClone();
        consentDocument["Settings"]!["AiEnabled"] = true;
        var consentJson = consentDocument.ToJsonString();
        var directory = LegacyDatabase("consent-v" + oldSchema, consentJson, oldSchema);
        var consentLoaded = new LocalStore(directory).Load()!;
        Check(consentLoaded.AiEnabled && consentLoaded.WikiInGame && consentLoaded.WikiOverlayName == "fixture-overlay" &&
            consentLoaded.InteractionHudEffectIntensity == 1f && consentLoaded.InteractionHudEffectSpeed == 1f && consentLoaded.InteractionHudEffectAnimated &&
            consentLoaded.CompanionHomeEnabled && consentLoaded.CompanionGrabHandEnabled && consentLoaded.CompanionCommentsEnabled && consentLoaded.CompanionCommentInterval == 120f &&
            consentLoaded.CompanionBlinkEnabled && consentLoaded.CompanionBlinkInterval == 3f && !consentLoaded.BrowserInterceptLinks,
            "Each legacy migration must preserve local AI consent and overlay name while applying browser/effect defaults.");
        var unconsented = new Configuration();
        store.ImportInto(unconsented, consentJson);
        Check(!unconsented.AiEnabled && unconsented.WikiInGame && !unconsented.BrowserInterceptLinks,
            "Each legacy import must retain the browser policy without granting new AI consent.");
        var existingConsent = new Configuration { AiEnabled = true, AiConnection = consentLoaded.AiConnection };
        store.ImportInto(existingConsent, consentJson);
        Check(existingConsent.AiEnabled, "Each legacy import may preserve existing consent for the same provider and endpoint.");
    }

    foreach (var oldEffect in new[] { HudTextEffect.None, HudTextEffect.Shadow, HudTextEffect.SoftGlow, HudTextEffect.GentleShimmer })
    {
        var styledLegacy = versionFiveDocument.DeepClone();
        styledLegacy["Settings"]!["InteractionHudEffect"] = (int)oldEffect;
        var migratedStyle = new Configuration();
        store.ImportInto(migratedStyle, styledLegacy.ToJsonString());
        Check(migratedStyle.InteractionHudEffect == oldEffect && migratedStyle.InteractionHudOutline,
            "All four existing effect identifiers and independent outlines must survive schema five migration.");
    }
    Check((int)HudTextEffect.None == 0 && (int)HudTextEffect.Shadow == 1 && (int)HudTextEffect.SoftGlow == 2 && (int)HudTextEffect.GentleShimmer == 3,
        "Existing saved effect identifiers must not be renumbered.");

    var badVersionFive = versionFiveDocument.DeepClone();
    badVersionFive["Settings"]!["InteractionHudEffect"] = 999;
    var badVersionFiveJson = badVersionFive.ToJsonString();
    var badVersionFiveDirectory = LegacyDatabase("version-five-invalid", badVersionFiveJson, 5);
    var badVersionFiveRejected = false;
    try { _ = new LocalStore(badVersionFiveDirectory); } catch (InvalidOperationException) { badVersionFiveRejected = true; }
    var preservedVersionFive = ReadDatabase(badVersionFiveDirectory);
    Check(badVersionFiveRejected && preservedVersionFive.DatabaseSchema == 5 && preservedVersionFive.RowSchema == 5 && preservedVersionFive.Json == badVersionFiveJson,
        "A failed version five migration must preserve its original schema and complete snapshot.");

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
    Invalid(exported.Replace("\"SchemaVersion\": 6", "\"SchemaVersion\": 7"), "Future schema");
    Invalid(exported.Replace("\"SchemaVersion\": 6", "\"SchemaVersion\": 6, \"SchemaVersion\": 6"), "Duplicate root fields");
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
    Invalid(Change("AiConnection", 4), "Unknown AI connection");
    Invalid(Change("AiConnection", -1), "Negative AI connection");
    foreach (var field in new[] { "AiModel", "ChatGptModel", "CodexModel", "ClaudeModel" })
    {
        Invalid(Change(field, new string('x', 201)), "Oversize model " + field);
        Invalid(Change(field, null), "Null model " + field);
        Invalid(Change(field, "model\nvalue"), "Control character in model " + field);
    }
    foreach (var connection in Enum.GetValues<AiConnectionKind>())
    {
        var input = new Configuration { AiConnection = connection, AiEnabled = true, ChatGptModel = "chatgpt", CodexModel = "codex", ClaudeModel = "claude" };
        var destination = new Configuration { AiEnabled = true, AiConnection = (AiConnectionKind)(((int)connection + 1) % 4) };
        store.ImportInto(destination, store.Export(input));
        Check(destination.AiConnection == connection && !destination.AiEnabled && destination.ChatGptModel == "chatgpt" &&
            destination.CodexModel == "codex" && destination.ClaudeModel == "claude", "Importing a different connection must preserve separate models and revoke AI consent.");
    }
    foreach (var field in versionFourFields.Where(field => field.StartsWith("Companion", StringComparison.Ordinal)))
    {
        Invalid(Change(field, "true"), "Wrong frame feature flag type " + field);
        Invalid(Change(field, null), "Null frame feature flag " + field);
    }
    Invalid(Change("InteractionTargets", 1024), "Unknown interaction target bit");
    Invalid(Change("InteractionTargets", -1), "Negative interaction target mask");
    Invalid(Change("InteractionTargets", "Npcs"), "Non-numeric interaction target mask");
    Invalid(Change("EnemyInteractMode", -1), "Negative enemy mode");
    Invalid(Change("EnemyInteractMode", 3), "Unknown enemy mode");
    Invalid(Change("EnemyInteractMode", "TargetOnly"), "Non-numeric enemy mode");
    foreach (var targets in new[] { InteractionTargets.None, InteractionTargets.DefaultWorld, InteractionTargets.All, InteractionTargets.HostileEnemies, InteractionTargets.NeutralEnemies })
    foreach (var mode in Enum.GetValues<EnemyInteractMode>())
    {
        var selected = new Configuration { InteractionTargets = targets, EnemyInteractMode = mode };
        var selectedJson = store.Export(selected);
        var selectedTarget = new Configuration();
        store.ImportInto(selectedTarget, selectedJson);
        store.Save(selectedTarget);
        var selectedReload = store.Load()!;
        Check(selectedReload.InteractionTargets == targets && selectedReload.EnemyInteractMode == mode,
            "All-off, world and enemy selections must preserve each action mode across JSON and SQLite.");
    }
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
    Invalid(Change("InteractionHudEffect", 999), "Unknown HUD effect");
    Invalid(Change("InteractionHudEffect", -1), "Negative HUD effect");
    Invalid(Change("InteractionHudEffect", "Rainbow"), "Non-numeric HUD effect");
    Invalid(Change("InteractionHudEffectIntensity", -.001f), "Negative HUD intensity");
    Invalid(Change("InteractionHudEffectIntensity", 2.001f), "Excessive HUD intensity");
    Invalid(Change("InteractionHudEffectSpeed", .099f), "HUD speed below minimum");
    Invalid(Change("InteractionHudEffectSpeed", 3.001f), "HUD speed above maximum");
    Invalid(Change("InteractionHudEffectAnimated", "true"), "Non-boolean HUD animation flag");
    Invalid(Change("InteractionHudEffectAnimated", null), "Null HUD animation flag");
    foreach (var field in versionSixFields)
    {
        var missingEffectControl = JsonNode.Parse(exported)!;
        missingEffectControl["Settings"]!.AsObject().Remove(field);
        Invalid(missingEffectControl.ToJsonString(), "Missing current-schema effect control " + field);
        var mixedEffectSchema = versionFiveDocument.DeepClone();
        mixedEffectSchema["Settings"]![field] = JsonNode.Parse(exported)!["Settings"]![field]!.DeepClone();
        Invalid(mixedEffectSchema.ToJsonString(), "Current effect control inside legacy schema " + field);
    }
    foreach (var field in new[] { "InteractionHudEffectIntensity", "InteractionHudEffectSpeed", "CompanionCommentInterval", "CompanionBlinkInterval" })
    {
        Invalid(Change(field, "NaN"), "Non-finite text value " + field);
        Invalid(Change(field, null), "Null effect number " + field);
    }
    foreach (var effect in Enum.GetValues<HudTextEffect>())
    foreach (var outline in new[] { false, true })
    {
        var styled = new Configuration { InteractionHudEffect = effect, InteractionHudOutline = outline,
            InteractionHudEffectIntensity = outline ? 0f : 2f, InteractionHudEffectSpeed = outline ? .1f : 3f, InteractionHudEffectAnimated = !outline };
        var styledTarget = new Configuration();
        store.ImportInto(styledTarget, store.Export(styled));
        store.Save(styledTarget);
        var styledReload = store.Load()!;
        Check(styledReload.InteractionHudEffect == effect && styledReload.InteractionHudOutline == outline &&
            styledReload.InteractionHudEffectIntensity == styled.InteractionHudEffectIntensity &&
            styledReload.InteractionHudEffectSpeed == styled.InteractionHudEffectSpeed && styledReload.InteractionHudEffectAnimated == styled.InteractionHudEffectAnimated,
            "Outline and bounded effect controls must remain independent of every effect across JSON and SQLite.");
    }
    var oldBrowserPreference = new Configuration { WikiInGame = false, WikiOverlayName = "preserved-overlay" };
    var canonicalBrowser = store.Upgrade(oldBrowserPreference);
    Check(canonicalBrowser.WikiInGame && canonicalBrowser.WikiOverlayName == "preserved-overlay" && !oldBrowserPreference.WikiInGame,
        "The current browser policy must override old external preferences without mutating its input.");
    var browserImport = new Configuration();
    store.ImportInto(browserImport, Change("WikiInGame", false));
    Check(browserImport.WikiInGame && browserImport.WikiOverlayName == original.WikiOverlayName,
        "An imported false browser preference must not revive external browsing.");
    Invalid(Change("WikiInGame", "false"), "Non-boolean browser preference");
    Check(!new Configuration().QuestItemUseEnabled, "Quest item use must be off by default.");
    Invalid(Change("QuestItemUseEnabled", "true"), "Non-boolean quest item preference");
    Invalid(Change("QuestItemUseEnabled", null), "Null quest item preference");
    foreach (var enabled in new[] { false, true })
    {
        var questPreferences = new Configuration { QuestItemUseEnabled = enabled, AiEnabled = true };
        var importedQuest = new Configuration();
        store.ImportInto(importedQuest, store.Export(questPreferences));
        Check(importedQuest.QuestItemUseEnabled == enabled && !importedQuest.AiEnabled,
            "Either quest item preference round-trips through JSON without granting AI consent.");
        store.Save(importedQuest);
        Check(store.Load()!.QuestItemUseEnabled == enabled, "Either quest item preference round-trips through SQLite.");
    }
    foreach (var legacy in new[] { legacyJson, versionTwoJson, versionThreeJson, versionFourJson, versionFiveJson })
    {
        var migratedQuest = new Configuration { QuestItemUseEnabled = true };
        store.ImportInto(migratedQuest, legacy);
        Check(!migratedQuest.QuestItemUseEnabled, "Each legacy schema must default quest item use off.");
    }
    Check(!new Configuration().BrowserInterceptLinks, "Link capture must be off for new configurations.");
    Invalid(Change("BrowserInterceptLinks", "true"), "Non-boolean link capture preference");
    Invalid(Change("BrowserInterceptLinks", null), "Null link capture preference");
    foreach (var enabled in new[] { false, true })
    {
        var browserPreferences = new Configuration { BrowserInterceptLinks = enabled, AiEnabled = true };
        var importedBrowser = new Configuration();
        store.ImportInto(importedBrowser, store.Export(browserPreferences));
        Check(importedBrowser.BrowserInterceptLinks == enabled && !importedBrowser.AiEnabled,
            "Either link capture preference round-trips through JSON without granting AI consent.");
        store.Save(importedBrowser);
        var reloadedBrowser = store.Load()!;
        Check(reloadedBrowser.BrowserInterceptLinks == enabled && !reloadedBrowser.AiEnabled,
            "Either link capture preference round-trips through SQLite independently of AI consent.");
    }
    Invalid(Change("CompanionBoxLeft", .99), "Inverted companion rectangle");
    Invalid(Change("CompanionBoxBottom", .5), "Inverted companion rectangle height");
    Invalid(Change("CompanionBoxRight", 1.1), "Companion rectangle outside viewport");
    Invalid(Change("CompanionSize", 193), "Invalid companion size");
    Invalid(Change("CompanionCommentInterval", 29.99f), "Comment interval below minimum");
    Invalid(Change("CompanionCommentInterval", 600.01f), "Comment interval above maximum");
    Invalid(Change("CompanionBlinkInterval", .99f), "Blink interval below minimum");
    Invalid(Change("CompanionBlinkInterval", 10.01f), "Blink interval above maximum");
    foreach (var field in new[] { "CompanionHomeEnabled", "CompanionGrabHandEnabled", "CompanionCommentsEnabled", "CompanionBlinkEnabled" })
    {
        Invalid(Change(field, "true"), "Wrong companion preference type " + field);
        Invalid(Change(field, null), "Null companion preference " + field);
    }
    for (var mask = 0; mask < 16; mask++)
    {
        var personality = new Configuration { CompanionHomeEnabled = (mask & 1) != 0, CompanionGrabHandEnabled = (mask & 8) != 0, CompanionCommentsEnabled = (mask & 2) != 0,
            CompanionBlinkEnabled = (mask & 4) != 0, CompanionCommentInterval = (mask & 1) != 0 ? 30f : 600f,
            CompanionBlinkInterval = (mask & 2) != 0 ? 1f : 10f };
        var importedPersonality = new Configuration();
        store.ImportInto(importedPersonality, store.Export(personality));
        store.Save(importedPersonality);
        var reloadedPersonality = store.Load()!;
        Check(reloadedPersonality.CompanionHomeEnabled == personality.CompanionHomeEnabled &&
            reloadedPersonality.CompanionGrabHandEnabled == personality.CompanionGrabHandEnabled &&
            reloadedPersonality.CompanionCommentsEnabled == personality.CompanionCommentsEnabled &&
            reloadedPersonality.CompanionCommentInterval == personality.CompanionCommentInterval &&
            reloadedPersonality.CompanionBlinkEnabled == personality.CompanionBlinkEnabled &&
            reloadedPersonality.CompanionBlinkInterval == personality.CompanionBlinkInterval,
            "Home, comments and blinking must remain independent across JSON and SQLite, including interval endpoints.");
    }
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
    foreach (var invalidNumber in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    foreach (var field in new[] { nameof(Configuration.InteractionHudEffectIntensity), nameof(Configuration.InteractionHudEffectSpeed),
        nameof(Configuration.CompanionCommentInterval), nameof(Configuration.CompanionBlinkInterval) })
    {
        var invalidSettings = new Configuration();
        typeof(Configuration).GetField(field)!.SetValue(invalidSettings, invalidNumber);
        var rejectedNumber = false;
        try { store.Save(invalidSettings); } catch (InvalidOperationException) { rejectedNumber = true; }
        Check(rejectedNumber && store.Export(store.Load()!) == beforeRejectedSave,
            "Non-finite effect settings must be rejected before replacing the saved database snapshot.");
    }
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
    Console.WriteLine($"{checks} storage checks passed: v1-v5 migration to v6, bounded HUD effects/outline, companion home/comments/blinks, in-game browser policy, rollback, atomic validation, export whitelist and AI consent.");
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

void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
