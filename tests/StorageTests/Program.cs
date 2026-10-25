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
    Check(new Configuration().Avatar == "Moonblob" && Configuration.AvatarNames.SequenceEqual(
        new[] { "Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism", "Nib", "Echo", "Fizz", "Glim", "Rune" }),
        "New preferences expose all thirteen companions and preserve the Moonblob default.");
    var original = new Configuration
    {
        SetupCompleted = true,
        LauncherWidth = 840f,
        LauncherTextScale = 1.2f,
        LauncherResultCount = 9,
        LauncherCenterX = .73f,
        LauncherTop = .31f,
        InteractionDistance = 4.5f,
        InteractionTargets = InteractionTargets.QuestObjects | InteractionTargets.NeutralEnemies,
        EnemyInteractMode = EnemyInteractMode.TargetOnly,
        MinerGearset = 2,
        Avatar = "Moonblob",
        AvatarEnabled = false,
        InteractModifier = ModifierKeys.Control | ModifierKeys.Shift,
        InteractionHudLocked = false,
        InteractionHudOffsetX = -220f,
        InteractionHudScale = 1.5f,
        InteractionHudOutline = true,
        InteractionHudOutlineThickness = 2.75f,
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
        CompanionHomeX = .14f,
        CompanionHomeY = .3f,
        CompanionGrabHandEnabled = false,
        CompanionGrabHandScale = .65f,
        CompanionNapsEnabled = false,
        CompanionNapInterval = 77f,
        CompanionNapDuration = 31f,
        CompanionNapZsEnabled = false,
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
        QuestEmoteUseEnabled = true,
        NextActionEnabled = false,
        NextActionLocked = false,
        NextActionX = .13f,
        NextActionY = .27f,
        QuestItemsBarEnabled = false,
        QuestItemsBarLocked = false,
        QuestItemsBarX = .73f,
        QuestItemsBarY = .91f,
        QuestItemsButtonSize = 48f,
        CompanionFourierBlinkEnabled = true,
        CompanionSeasonalEnabled = false,
        CompanionSeasonalTheme = "Starlight",
        CompanionAutoRotateEnabled = true,
        CompanionRotationInterval = 180f,
        CompanionVariant = "Look3",
        CompanionLookAutoRotateEnabled = true,
        CompanionLookInterval = 480f,
        LauncherHeight = 32f,
        CompanionHomeStyle = "CrystalPod",
        CompanionHolidayHomeEnabled = false,
        CompanionHolidayBodiesEnabled = false,
        CompanionHandStyle = "MagicalPaw",
        CompanionGroundedEnabled = false,
        CompanionAutoHopEnabled = false,
        CompanionOrbitRingsEnabled = false,
        CompanionOrbitRingSpeed = 2f,
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
    var versionSevenFields = new[] { "InteractionHudOutlineThickness", "QuestEmoteUseEnabled", "CompanionGrabHandScale", "CompanionNapsEnabled", "CompanionNapInterval", "CompanionNapDuration", "CompanionNapZsEnabled", "CompanionHomeX", "CompanionHomeY" };
    var versionEightFields = new[] { "NextActionEnabled", "NextActionLocked", "NextActionX", "NextActionY",
        "QuestItemsBarEnabled", "QuestItemsBarLocked", "QuestItemsBarX", "QuestItemsBarY", "QuestItemsButtonSize",
        "CompanionFourierBlinkEnabled", "CompanionSeasonalEnabled", "CompanionSeasonalTheme", "CompanionAutoRotateEnabled", "CompanionRotationInterval",
        "LauncherWidth", "LauncherTextScale", "LauncherResultCount", "LauncherCenterX", "LauncherTop" };
    var versionNineFields = new[] { "CompanionVariant", "CompanionLookAutoRotateEnabled", "CompanionLookInterval", "LauncherHeight", "CompanionHomeStyle", "CompanionHolidayHomeEnabled", "CompanionHolidayBodiesEnabled", "CompanionHandStyle", "CompanionGroundedEnabled", "CompanionAutoHopEnabled", "CompanionOrbitRingsEnabled", "CompanionOrbitRingSpeed" };
    var versionTenFields = new[] { "CompanionHolidayBody", "CompanionAccessoryFits", "CompanionTransitionEffectsEnabled" };
    var versionTwelveFields = new[] { "CompanionCostume" };
    var versionThirteenFields = new[] { "CompanionSceneScale", "CompanionHomeSize", "CompanionHandSize", "CompanionBodyMotionStrength", "CompanionMotionReactions" };
    var versionTwelveDocument = JsonNode.Parse(exported)!;
    versionTwelveDocument["SchemaVersion"] = 12; versionTwelveDocument["Settings"]!["Version"] = 12;
    foreach (var field in versionThirteenFields) versionTwelveDocument["Settings"]!.AsObject().Remove(field);
    foreach (var (pet, hand) in new[] { (48f, .2f), (120f, .65f), (192f, 1f) })
    {
        var legacySizes = versionTwelveDocument.DeepClone();
        legacySizes["Settings"]!["CompanionSize"] = pet;
        legacySizes["Settings"]!["CompanionGrabHandScale"] = hand;
        var migrated = new Configuration(); store.ImportInto(migrated, legacySizes.ToJsonString());
        Check(migrated.CompanionSceneScale == 1f && migrated.CompanionSize == pet &&
            migrated.CompanionHomeSize == Math.Clamp(pet * 1.3f, 48f, 384f) &&
            migrated.CompanionHandSize == Math.Clamp(pet * hand, 8f, 192f), "Schema twelve migration preserves independently derived scene sizes.");
        if (pet == 48f && hand == .2f)
            Check(Math.Abs(migrated.CompanionHomeSize - 62.4f) < .00001f && Math.Abs(migrated.CompanionHandSize - 9.6f) < .00001f,
                "The smallest legacy companion retains its exact62.4home and9.6hand extents.");
        store.Save(migrated); Check(store.Export(store.Load()!) == store.Export(migrated), "Migrated scene sizes round-trip.");
    }
    foreach (var (field, minimum, maximum) in new[] { ("CompanionBodyMotionStrength", 0f, 1.5f), ("CompanionSceneScale", .5f, 2f), ("CompanionHomeSize", 48f, 384f), ("CompanionHandSize", 8f, 192f) })
    {
        foreach (var value in new JsonNode?[] { JsonValue.Create(minimum - .1f), JsonValue.Create(maximum + .1f), null, JsonValue.Create("NaN") })
        {
            var invalidSize = JsonNode.Parse(exported)!; invalidSize["Settings"]![field] = value;
            Invalid(invalidSize.ToJsonString(), "Malformed scene size: " + field);
        }
        var missingSize = JsonNode.Parse(exported)!; missingSize["Settings"]!.AsObject().Remove(field);
        Invalid(missingSize.ToJsonString(), "Required scene size: " + field);
    }
    foreach (var lower in new[] { true, false })
    {
        var sizes = new Configuration { CompanionSceneScale = lower ? .5f : 2f,
            CompanionSize = lower ? 48f : 192f, CompanionHomeSize = lower ? 48f : 384f,
            CompanionHandSize = lower ? 8f : 192f, CompanionBodyMotionStrength = lower ? 0f : 1.5f,
            CompanionMotionReactions = lower };
        var sizeJson = store.Export(sizes); var copy = new Configuration(); store.ImportInto(copy, sizeJson);
        store.Save(copy); Check(store.Export(store.Load()!) == sizeJson, "Independent size and motion endpoints round-trip through JSON and SQLite.");
        var home = OmniKit.Ui.CompanionSceneSizing.HomeEdge(copy, 1.5f);
        var hand = OmniKit.Ui.CompanionSceneSizing.HandEdge(copy, 1.5f);
        copy.CompanionSize = 96;
        Check(home == OmniKit.Ui.CompanionSceneSizing.HomeEdge(copy, 1.5f) && hand == OmniKit.Ui.CompanionSceneSizing.HandEdge(copy, 1.5f),
            "Changing the companion size leaves home and hand extents unchanged.");
        copy.CompanionSceneScale = 1f;
        Check(Math.Abs(home / OmniKit.Ui.CompanionSceneSizing.HomeEdge(copy, 1.5f) - sizes.CompanionSceneScale) < .0001f &&
            Math.Abs(hand / OmniKit.Ui.CompanionSceneSizing.HandEdge(copy, 1.5f) - sizes.CompanionSceneScale) < .0001f,
            "Scene scale multiplies home and hand together.");
    }
    foreach (var value in new JsonNode?[] { null, JsonValue.Create(1), JsonValue.Create("true") })
    {
        var invalidReaction = JsonNode.Parse(exported)!; invalidReaction["Settings"]!["CompanionMotionReactions"] = value;
        Invalid(invalidReaction.ToJsonString(), "Motion reactions require a JSON boolean.");
    }
    if (args.Contains("--scene-only"))
    {
        Console.WriteLine($"Storage scene migration: {checks} focused checks passed.");
        return;
    }
    var versionNineDocument = versionTwelveDocument.DeepClone();
    versionNineDocument["SchemaVersion"] = 9;
    versionNineDocument["Settings"]!["Version"] = 9;
    foreach (var field in versionTenFields) versionNineDocument["Settings"]!.AsObject().Remove(field);
    foreach (var field in versionTwelveFields) versionNineDocument["Settings"]!.AsObject().Remove(field);
    var versionEightDocument = versionNineDocument.DeepClone();
    versionEightDocument["SchemaVersion"] = 8;
    versionEightDocument["Settings"]!["Version"] = 8;
    foreach (var field in versionNineFields) versionEightDocument["Settings"]!.AsObject().Remove(field);
    var versionEightJson = versionEightDocument.ToJsonString();
    var versionSevenDocument = versionEightDocument.DeepClone();
    versionSevenDocument["SchemaVersion"] = 7;
    versionSevenDocument["Settings"]!["Version"] = 7;
    foreach (var field in versionEightFields) versionSevenDocument["Settings"]!.AsObject().Remove(field);
    var versionSevenJson = versionSevenDocument.ToJsonString();
    var versionSixDocument = versionSevenDocument.DeepClone();
    versionSixDocument["SchemaVersion"] = 6;
    versionSixDocument["Settings"]!["Version"] = 6;
    foreach (var field in versionSevenFields) versionSixDocument["Settings"]!.AsObject().Remove(field);
    var versionSixJson = versionSixDocument.ToJsonString();
    var versionFiveDocument = versionSixDocument.DeepClone();
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
    foreach (var oldDocument in new[] { legacyDocument, versionTwoDocument, versionThreeDocument, versionFourDocument,
        versionFiveDocument, versionSixDocument, versionSevenDocument, versionEightDocument, versionNineDocument })
    {
        var defaultsTarget = new Configuration { CompanionHolidayBody = "TheRising",
            CompanionAccessoryFits = [new("Nova", "Default", "Starlight", 0, 0, 1)], CompanionTransitionEffectsEnabled = false };
        store.ImportInto(defaultsTarget, oldDocument.ToJsonString());
        Check(defaultsTarget.CompanionHolidayBody == "Auto" && defaultsTarget.CompanionAccessoryFits.Count == 0 && defaultsTarget.CompanionTransitionEffectsEnabled,
            "Each legacy schema initializes holiday body, fit and transition preferences independently of current values.");
    }

    void CheckLegacy(Configuration migrated)
    {
        Check(migrated.Version == 13 && migrated.LauncherModifier == ModifierKeys.Alt && migrated.SettingsModifier == ModifierKeys.Control &&
            migrated.InteractModifier == ModifierKeys.None, "Legacy virtual-key modifiers must migrate to the correct masks.");
        var snapshot = JsonNode.Parse(store.Export(migrated))!["Settings"]!.AsObject();
        foreach (var field in legacySettings)
            if (field.Key is not ("Version" or "LauncherModifier" or "SettingsModifier" or "WikiInGame"))
                Check(JsonNode.DeepEquals(snapshot[field.Key], field.Value), "Migration must preserve legacy field " + field.Key);
        var defaults = new Configuration();
        var defaultSnapshot = JsonNode.Parse(store.Export(defaults))!["Settings"]!.AsObject();
        foreach (var field in versionTwoFields.Concat(versionThreeFields).Concat(versionFourFields).Concat(versionFiveFields).Concat(versionSixFields).Concat(versionSevenFields).Concat(versionEightFields).Concat(versionNineFields).Concat(versionTenFields).Concat(versionTwelveFields).Concat(versionThirteenFields))
            Check(field == nameof(Configuration.CompanionHomeX)
                ? Math.Abs(migrated.CompanionHomeX - defaults.CompanionHomeX) < .000001f
                : field == nameof(Configuration.CompanionHomeSize) ? Math.Abs(migrated.CompanionHomeSize - defaults.CompanionHomeSize) < .0001f
                : JsonNode.DeepEquals(snapshot[field], defaultSnapshot[field]), "Migration must use the default for new field " + field);
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
    Check(upgradedFallback.Version == 13 && upgradedFallback.LauncherModifier == ModifierKeys.Alt &&
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
    Check(migratedRow.DatabaseSchema == 13 && migratedRow.RowSchema == 13 && JsonNode.Parse(migratedRow.Json)!["SchemaVersion"]!.GetValue<int>() == 13,
        "Database version, row version and JSON schema must migrate together.");
    CheckLegacy(new LocalStore(legacyDirectory).Load()!);

    void CheckVersionTwo(Configuration migrated)
    {
        Check(migrated.Version == 13 && migrated.CompanionOutfit == "Navigator" && migrated.CompanionPalette == "Moonlight" && migrated.CompanionMagicEnabled &&
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
    Check(ReadDatabase(versionTwoDirectory).DatabaseSchema == 13 && ReadDatabase(versionTwoDirectory).RowSchema == 13,
        "Version two SQLite schema and row must migrate together.");

    void CheckVersionThree(Configuration migrated)
    {
        Check(migrated.Version == 13 && migrated.InteractionTargets == InteractionTargets.DefaultWorld && migrated.EnemyInteractMode == EnemyInteractMode.Choose,
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
    Check(versionThreeRow.DatabaseSchema == 13 && versionThreeRow.RowSchema == 13 && JsonNode.Parse(versionThreeRow.Json)!["SchemaVersion"]!.GetValue<int>() == 13,
        "Version three SQLite schema, row and JSON must migrate together.");

    void CheckVersionFour(Configuration migrated)
    {
        Check(migrated.Version == 13 && !migrated.InteractionHudOutline && migrated.InteractionHudEffect == HudTextEffect.Shadow,
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
    Check(versionFourRow.DatabaseSchema == 13 && versionFourRow.RowSchema == 13 && JsonNode.Parse(versionFourRow.Json)!["SchemaVersion"]!.GetValue<int>() == 13,
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
        Check(migrated.Version == 13 && migrated.InteractionHudEffectIntensity == 1f && migrated.InteractionHudEffectSpeed == 1f &&
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
    Check(versionFiveRow.DatabaseSchema == 13 && versionFiveRow.RowSchema == 13 && JsonNode.Parse(versionFiveRow.Json)!["SchemaVersion"]!.GetValue<int>() == 13,
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

    void CheckNapDefaults(Configuration value)
    {
        Check(value.InteractionHudOutlineThickness == 1f && !value.QuestEmoteUseEnabled,
            "New and migrated settings must use the original outline weight and leave quest emotes disabled.");
        Check(value.CompanionGrabHandScale == .40f && value.CompanionNapsEnabled && value.CompanionNapInterval == 45f &&
            value.CompanionNapDuration == 20f && value.CompanionNapZsEnabled,
            "New and migrated preferences must use the requested hand size and nap defaults.");
    }
    var fresh = new Configuration();
    CheckNapDefaults(fresh);
    Check(fresh.CompanionHomeX == .835f && fresh.CompanionHomeY == .92f, "New home coordinates must use the viewport defaults.");
    foreach (var oldJson in new[] { legacyJson, versionTwoJson, versionThreeJson, versionFourJson, versionFiveJson, versionSixJson })
    {
        var upgraded = new Configuration();
        store.ImportInto(upgraded, oldJson);
        CheckNapDefaults(upgraded);
        Check(Math.Abs(upgraded.CompanionHomeX - (upgraded.CompanionBoxLeft + upgraded.CompanionBoxRight) * .5f) < .000001f &&
            upgraded.CompanionHomeY == upgraded.CompanionBoxBottom,
            "Every legacy schema must retain the former house location derived from its saved roaming box.");
    }
    void CheckVersionSix(Configuration migrated)
    {
        Check(migrated.Version == 13, "Version six must migrate to the current configuration version.");
        CheckNapDefaults(migrated);
        var snapshot = JsonNode.Parse(store.Export(migrated))!["Settings"]!.AsObject();
        foreach (var field in versionSixDocument["Settings"]!.AsObject())
            if (field.Key != "Version")
                Check(JsonNode.DeepEquals(snapshot[field.Key], field.Value), "Version six migration must preserve " + field.Key);
        Check(Math.Abs(migrated.CompanionHomeX - .575f) < .000001f && migrated.CompanionHomeY == original.CompanionBoxBottom,
            "Migration must derive home coordinates from the customized box rather than the new-install defaults.");
    }
    var versionSixTarget = new Configuration();
    store.ImportInto(versionSixTarget, versionSixJson);
    CheckVersionSix(versionSixTarget);
    var versionSixFallback = JsonSerializer.Deserialize<Configuration>(versionSixDocument["Settings"]!.ToJsonString(), new JsonSerializerOptions { IncludeFields = true })!;
    CheckVersionSix(store.Upgrade(versionSixFallback));
    Check(versionSixFallback.Version == 6, "Fallback migration must leave its source object untouched.");
    var versionSixDirectory = LegacyDatabase("version-six", versionSixJson, 6);
    CheckVersionSix(new LocalStore(versionSixDirectory).Load()!);
    var versionSixRow = ReadDatabase(versionSixDirectory);
    Check(versionSixRow.DatabaseSchema == 13 && versionSixRow.RowSchema == 13 && JsonNode.Parse(versionSixRow.Json)!["SchemaVersion"]!.GetValue<int>() == 13,
        "Version six database, row and JSON versions must upgrade together.");
    CheckVersionSix(new LocalStore(versionSixDirectory).Load()!);

    var consentSix = versionSixDocument.DeepClone();
    consentSix["Settings"]!["AiEnabled"] = true;
    var consentSixJson = consentSix.ToJsonString();
    var consentSixDirectory = LegacyDatabase("version-six-consent", consentSixJson, 6);
    Check(new LocalStore(consentSixDirectory).Load()!.AiEnabled, "Version six database migration must retain existing local AI consent.");
    var importedSix = new Configuration();
    store.ImportInto(importedSix, consentSixJson);
    Check(!importedSix.AiEnabled, "Version six import must not grant AI consent.");
    importedSix.AiEnabled = true;
    store.ImportInto(importedSix, consentSixJson);
    Check(importedSix.AiEnabled, "Version six import may retain consent for the same connection and endpoint.");

    var invalidSix = versionSixDocument.DeepClone();
    invalidSix["Settings"]!["CompanionBoxLeft"] = 1f;
    var invalidSixJson = invalidSix.ToJsonString();
    var invalidSixDirectory = LegacyDatabase("version-six-invalid", invalidSixJson, 6);
    var rejectedSix = false;
    try { _ = new LocalStore(invalidSixDirectory); } catch (InvalidOperationException) { rejectedSix = true; }
    var preservedSix = ReadDatabase(invalidSixDirectory);
    Check(rejectedSix && preservedSix == (6, 6, invalidSixJson), "Invalid version six settings must roll back both schema versions and preserve JSON verbatim.");
    var failureSixDirectory = LegacyDatabase("version-six-write-failure", versionSixJson, 6);
    using (var database = new SqliteConnection($"Data Source={Path.Combine(failureSixDirectory, "omnikit.db")};Pooling=False"))
    {
        database.Open();
        using var command = database.CreateCommand();
        command.CommandText = "CREATE TRIGGER RejectMigration BEFORE UPDATE ON OmniKitSettings BEGIN SELECT RAISE(ABORT, 'Synthetic write failure'); END;";
        command.ExecuteNonQuery();
    }
    var failedSixWrite = false;
    try { _ = new LocalStore(failureSixDirectory); } catch (SqliteException) { failedSixWrite = true; }
    Check(failedSixWrite && ReadDatabase(failureSixDirectory) == (6, 6, versionSixJson),
        "A version six migration write failure must preserve the old schema and entire snapshot.");

    var defaultsEight = new Configuration();
    Check(defaultsEight.LauncherWidth == 640f && defaultsEight.LauncherTextScale == 1f && defaultsEight.LauncherResultCount == 7
        && defaultsEight.LauncherCenterX == .5f && defaultsEight.LauncherTop == .20f,
        "Launcher customization defaults preserve the original width, text size, result count and position.");
    Check(defaultsEight.NextActionEnabled && defaultsEight.NextActionLocked && defaultsEight.NextActionX == .5f && defaultsEight.NextActionY == .76f
        && defaultsEight.QuestItemsBarEnabled && defaultsEight.QuestItemsBarLocked && defaultsEight.QuestItemsBarX == .5f
        && defaultsEight.QuestItemsBarY == .84f && defaultsEight.QuestItemsButtonSize == 40f,
        "New action widgets start enabled, locked and at their supported default positions and size.");
    Check(!defaultsEight.CompanionFourierBlinkEnabled && defaultsEight.CompanionSeasonalEnabled && defaultsEight.CompanionSeasonalTheme == "Auto"
        && !defaultsEight.CompanionAutoRotateEnabled && defaultsEight.CompanionRotationInterval == 300f,
        "The original blink and selected companion stay the defaults while seasonal looks use Auto.");
    void CheckVersionSeven(Configuration migrated)
    {
        Check(migrated.Version == 13, "Version seven must migrate to the current configuration version.");
        var snapshot = JsonNode.Parse(store.Export(migrated))!["Settings"]!.AsObject();
        var defaults = JsonNode.Parse(store.Export(new Configuration()))!["Settings"]!.AsObject();
        foreach (var field in versionEightFields)
            Check(JsonNode.DeepEquals(snapshot[field], defaults[field]), "Version eight migration uses the default for " + field);
        foreach (var field in versionSevenDocument["Settings"]!.AsObject())
            if (field.Key != "Version") Check(JsonNode.DeepEquals(snapshot[field.Key], field.Value),
                "Version seven migration preserves every prior preference, including outline, avatar, home and naps: " + field.Key);
    }
    var sevenTarget = new Configuration();
    store.ImportInto(sevenTarget, versionSevenJson);
    CheckVersionSeven(sevenTarget);
    var sevenFallback = JsonSerializer.Deserialize<Configuration>(versionSevenDocument["Settings"]!.ToJsonString(), new JsonSerializerOptions { IncludeFields = true })!;
    var sevenBefore = JsonSerializer.Serialize(sevenFallback, new JsonSerializerOptions { IncludeFields = true });
    CheckVersionSeven(store.Upgrade(sevenFallback));
    Check(JsonSerializer.Serialize(sevenFallback, new JsonSerializerOptions { IncludeFields = true }) == sevenBefore,
        "Version seven fallback migration never changes its original object.");
    var sevenDirectory = LegacyDatabase("version-seven", versionSevenJson, 7);
    CheckVersionSeven(new LocalStore(sevenDirectory).Load()!);
    var sevenRow = ReadDatabase(sevenDirectory);
    Check(sevenRow.DatabaseSchema == 13 && sevenRow.RowSchema == 13 && JsonNode.Parse(sevenRow.Json)!["SchemaVersion"]!.GetValue<int>() == 13,
        "Version seven database, row and JSON versions migrate atomically to the current schema.");
    var consentSeven = versionSevenDocument.DeepClone(); consentSeven["Settings"]!["AiEnabled"] = true;
    var consentSevenJson = consentSeven.ToJsonString();
    Check(new LocalStore(LegacyDatabase("version-seven-consent", consentSevenJson, 7)).Load()!.AiEnabled,
        "A version seven local database migration preserves previously granted AI consent.");
    var importedSeven = new Configuration(); store.ImportInto(importedSeven, consentSevenJson);
    Check(!importedSeven.AiEnabled, "A version seven import never grants new AI consent.");
    importedSeven.AiEnabled = true; store.ImportInto(importedSeven, consentSevenJson);
    Check(importedSeven.AiEnabled, "A version seven import may preserve existing consent for the same provider and endpoint.");
    var invalidSeven = versionSevenDocument.DeepClone(); invalidSeven["Settings"]!["CompanionHomeX"] = 2f;
    var invalidSevenJson = invalidSeven.ToJsonString();
    var invalidSevenDirectory = LegacyDatabase("version-seven-invalid", invalidSevenJson, 7);
    var rejectedSeven = false;
    try { _ = new LocalStore(invalidSevenDirectory); } catch (InvalidOperationException) { rejectedSeven = true; }
    Check(rejectedSeven && ReadDatabase(invalidSevenDirectory) == (7, 7, invalidSevenJson),
        "Invalid version seven settings preserve both legacy schema values and the complete JSON snapshot.");
    var failedSevenDirectory = LegacyDatabase("version-seven-write-failure", versionSevenJson, 7);
    using (var database = new SqliteConnection($"Data Source={Path.Combine(failedSevenDirectory, "omnikit.db")};Pooling=False"))
    {
        database.Open(); using var command = database.CreateCommand();
        command.CommandText = "CREATE TRIGGER RejectMigration BEFORE UPDATE ON OmniKitSettings BEGIN SELECT RAISE(ABORT, 'Synthetic write failure'); END;";
        command.ExecuteNonQuery();
    }
    var failedSevenWrite = false;
    try { _ = new LocalStore(failedSevenDirectory); } catch (SqliteException) { failedSevenWrite = true; }
    Check(failedSevenWrite && ReadDatabase(failedSevenDirectory) == (7, 7, versionSevenJson),
        "Failed version seven database migration rolls back the schema and entire saved snapshot.");

    void CheckVersionEight(Configuration migrated)
    {
        Check(migrated.Version == 13, "Version eight must migrate to the current schema.");
        var snapshot = JsonNode.Parse(store.Export(migrated))!["Settings"]!.AsObject();
        var defaults = JsonNode.Parse(store.Export(new Configuration()))!["Settings"]!.AsObject();
        foreach (var field in versionNineFields)
            Check(JsonNode.DeepEquals(snapshot[field], defaults[field]), "Version nine migration defaults " + field);
        foreach (var field in versionEightDocument["Settings"]!.AsObject())
            if (field.Key is not ("Version" or "AiEnabled"))
                Check(JsonNode.DeepEquals(snapshot[field.Key], field.Value), "Version eight preserves " + field.Key);
    }
    var eightTarget = new Configuration(); store.ImportInto(eightTarget, versionEightJson); CheckVersionEight(eightTarget);
    var eightFallback = JsonSerializer.Deserialize<Configuration>(versionEightDocument["Settings"]!.ToJsonString(), new JsonSerializerOptions { IncludeFields = true })!;
    var eightBefore = JsonSerializer.Serialize(eightFallback, new JsonSerializerOptions { IncludeFields = true });
    CheckVersionEight(store.Upgrade(eightFallback));
    Check(JsonSerializer.Serialize(eightFallback, new JsonSerializerOptions { IncludeFields = true }) == eightBefore, "Version eight fallback is not mutated.");
    var eightDirectory = LegacyDatabase("version-eight", versionEightJson, 8);
    CheckVersionEight(new LocalStore(eightDirectory).Load()!);
    var eightRow = ReadDatabase(eightDirectory);
    Check(eightRow.DatabaseSchema == 13 && eightRow.RowSchema == 13 && JsonNode.Parse(eightRow.Json)!["SchemaVersion"]!.GetValue<int>() == 13,
        "Version eight migration commits database, row and JSON versions atomically.");
    var consentEight = versionEightDocument.DeepClone(); consentEight["Settings"]!["AiEnabled"] = true;
    Check(new LocalStore(LegacyDatabase("version-eight-consent", consentEight.ToJsonString(), 8)).Load()!.AiEnabled, "Version eight local consent survives migration.");
    var importedEight = new Configuration(); store.ImportInto(importedEight, consentEight.ToJsonString());
    Check(!importedEight.AiEnabled, "Version eight import cannot grant AI consent.");
    var invalidEight = versionEightDocument.DeepClone(); invalidEight["Settings"]!["CompanionHomeX"] = 2f;
    var invalidEightJson = invalidEight.ToJsonString();
    var invalidEightDirectory = LegacyDatabase("version-eight-invalid", invalidEightJson, 8);
    var rejectedEight = false;
    try { _ = new LocalStore(invalidEightDirectory); } catch (InvalidOperationException) { rejectedEight = true; }
    Check(rejectedEight && ReadDatabase(invalidEightDirectory) == (8, 8, invalidEightJson), "Invalid version eight data leaves the old database intact.");
    var failedEightDirectory = LegacyDatabase("version-eight-write-failure", versionEightJson, 8);
    using (var database = new SqliteConnection($"Data Source={Path.Combine(failedEightDirectory, "omnikit.db")};Pooling=False"))
    {
        database.Open(); using var command = database.CreateCommand();
        command.CommandText = "CREATE TRIGGER RejectMigration BEFORE UPDATE ON OmniKitSettings BEGIN SELECT RAISE(ABORT, 'Synthetic write failure'); END;";
        command.ExecuteNonQuery();
    }
    var failedEightWrite = false;
    try { _ = new LocalStore(failedEightDirectory); } catch (SqliteException) { failedEightWrite = true; }
    Check(failedEightWrite && ReadDatabase(failedEightDirectory) == (8, 8, versionEightJson), "Failed version eight writes roll back completely.");

    foreach (var field in versionNineFields)
    {
        var missingNine = JsonNode.Parse(exported)!; missingNine["Settings"]!.AsObject().Remove(field);
        Invalid(missingNine.ToJsonString(), "Missing schema nine field " + field);
        var mixedNine = versionEightDocument.DeepClone(); mixedNine["Settings"]![field] = JsonNode.Parse(exported)!["Settings"]![field]!.DeepClone();
        Invalid(mixedNine.ToJsonString(), "Schema nine field cannot enter schema eight " + field);
    }
    foreach (var field in new[] { "CompanionLookAutoRotateEnabled", "CompanionHolidayHomeEnabled", "CompanionHolidayBodiesEnabled", "CompanionGroundedEnabled", "CompanionAutoHopEnabled", "CompanionOrbitRingsEnabled" })
    {
        Invalid(Change(field, "true"), "Non-boolean style toggle " + field);
        Invalid(Change(field, null), "Null style toggle " + field);
    }
    foreach (var (field, choices) in new[] { ("CompanionVariant", Configuration.CompanionVariantNames), ("CompanionHomeStyle", Configuration.HomeStyleNames), ("CompanionHandStyle", Configuration.HandStyleNames) })
    {
        Invalid(Change(field, "UnknownStyle"), "Unknown style " + field);
        Invalid(Change(field, null), "Null style " + field);
        foreach (var choice in choices)
        {
            var target = new Configuration(); store.ImportInto(target, Change(field, choice));
            Check(JsonNode.Parse(store.Export(target))!["Settings"]![field]!.GetValue<string>() == choice, "Supported style roundtrips " + choice);
        }
    }
    foreach (var (field, minimum, maximum) in new[] { ("LauncherHeight", 24f, 120f), ("CompanionLookInterval", 30f, 3600f), ("CompanionOrbitRingSpeed", .2f, 3f) })
    {
        Invalid(Change(field, minimum - .01f), "Style value below minimum " + field);
        Invalid(Change(field, maximum + .01f), "Style value above maximum " + field);
        Invalid(Change(field, "NaN"), "Non-numeric style value " + field);
        foreach (var value in new[] { minimum, maximum })
        {
            var target = new Configuration(); store.ImportInto(target, Change(field, value));
            Check(JsonNode.Parse(store.Export(target))!["Settings"]![field]!.GetValue<float>() == value, "Style value boundary " + field);
        }
    }

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

    foreach (var oldDocument in new[] { legacyDocument, versionTwoDocument, versionThreeDocument, versionFourDocument, versionFiveDocument, versionSixDocument })
    {
        var schema = oldDocument["SchemaVersion"]!.GetValue<int>();
        var expected = new Configuration();
        store.ImportInto(expected, oldDocument.ToJsonString());
        var expectedJson = store.Export(expected);
        foreach (var retired in new[] { "Bimble", "Moonbear" })
        {
            var retiredDocument = oldDocument.DeepClone();
            retiredDocument["Settings"]!["Avatar"] = retired;
            var retiredJson = retiredDocument.ToJsonString();
            var imported = new Configuration();
            store.ImportInto(imported, retiredJson);
            Check(imported.Avatar == "Moonblob" && store.Export(imported) == expectedJson,
                "Each legacy retired-avatar import maps to Moonblob while preserving every other migrated preference.");
            var fallback = JsonSerializer.Deserialize<Configuration>(retiredDocument["Settings"]!.ToJsonString(), new JsonSerializerOptions { IncludeFields = true })!;
            Check(store.Export(store.Upgrade(fallback)) == expectedJson && fallback.Avatar == retired && fallback.Version == schema,
                "Retired-avatar fallback upgrades must map a detached copy and leave the original untouched.");
            var directory = LegacyDatabase($"retired-v{schema}-{retired}", retiredJson, schema);
            Check(store.Export(new LocalStore(directory).Load()!) == expectedJson,
                "Legacy SQLite migration must preserve settings and replace only the retired avatar selection.");
        }
        var unknown = oldDocument.DeepClone(); unknown["Settings"]!["Avatar"] = "UnknownCompanion";
        Invalid(unknown.ToJsonString(), "Unknown legacy avatar");
    }

    Invalid("{", "Malformed JSON");
    Invalid("null", "Null root");
    Invalid(new string(' ', 262145), "Oversize input");
    Invalid(new string('界', 100000), "Oversize UTF-8 input");
    Invalid(exported.Replace("\"SchemaVersion\": 13", "\"SchemaVersion\": 14"), "Future schema");
    Invalid(exported.Replace("\"SchemaVersion\": 13", "\"SchemaVersion\": 13, \"SchemaVersion\": 13"), "Duplicate root fields");
    Invalid(exported.Replace("\"Avatar\": \"Moonblob\"", "\"Avatar\": \"Moonblob\", \"Avatar\": \"Wink\""), "Duplicate nested fields");
    Invalid(Change("UnapprovedFutureField", "should not import"), "Unknown fields");
    foreach (var field in versionEightFields)
    {
        var missingEight = JsonNode.Parse(exported)!; missingEight["Settings"]!.AsObject().Remove(field);
        Invalid(missingEight.ToJsonString(), "Missing schema eight field " + field);
        var mixedEight = versionSevenDocument.DeepClone(); mixedEight["Settings"]![field] = JsonNode.Parse(exported)!["Settings"]![field]!.DeepClone();
        Invalid(mixedEight.ToJsonString(), "Schema eight field in a version seven snapshot " + field);
    }
    foreach (var field in new[] { "NextActionEnabled", "NextActionLocked", "QuestItemsBarEnabled", "QuestItemsBarLocked",
        "CompanionFourierBlinkEnabled", "CompanionSeasonalEnabled", "CompanionAutoRotateEnabled" })
    {
        Invalid(Change(field, "true"), "Non-boolean schema eight toggle " + field);
        Invalid(Change(field, null), "Null schema eight toggle " + field);
    }
    foreach (var (field, minimum, maximum) in new[] { ("NextActionX", 0f, 1f), ("NextActionY", 0f, 1f),
        ("QuestItemsBarX", 0f, 1f), ("QuestItemsBarY", 0f, 1f), ("QuestItemsButtonSize", 24f, 56f), ("CompanionRotationInterval", 30f, 3600f),
        ("LauncherWidth", 320f, 1200f), ("LauncherTextScale", .75f, 1.5f), ("LauncherCenterX", .05f, .95f), ("LauncherTop", 0f, .9f) })
    {
        Invalid(Change(field, minimum - .01f), "Schema eight numeric field below minimum: " + field);
        Invalid(Change(field, maximum + .01f), "Schema eight numeric field above maximum: " + field);
        Invalid(Change(field, "NaN"), "Schema eight non-finite numeric text: " + field);
        Invalid(Change(field, null), "Schema eight null numeric field: " + field);
    }
    foreach (var invalidCount in new JsonNode?[] { JsonValue.Create(2), JsonValue.Create(13), JsonValue.Create(7.5f),
        JsonValue.Create("7"), null })
        Invalid(Change("LauncherResultCount", invalidCount), "Launcher result count outside its integer contract");
    foreach (var field in new[] { "LauncherWidth", "LauncherTextScale", "LauncherCenterX", "LauncherTop" })
    foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    {
        var invalid = new Configuration(); typeof(Configuration).GetField(field)!.SetValue(invalid, value);
        var rejected = false;
        try { store.Export(invalid); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Non-finite launcher values cannot be persisted or exported: " + field);
    }
    Invalid(Change("CompanionSeasonalTheme", "UnknownTheme"), "Unknown seasonal theme");
    Invalid(Change("CompanionSeasonalTheme", null), "Null seasonal theme");
    foreach (var theme in Configuration.SeasonalThemeNames)
    foreach (var lower in new[] { false, true })
    {
        var choice = new Configuration { NextActionEnabled = lower, NextActionLocked = !lower,
            NextActionX = lower ? 0 : 1, NextActionY = lower ? 1 : 0,
            QuestItemsBarEnabled = !lower, QuestItemsBarLocked = lower,
            QuestItemsBarX = lower ? 1 : 0, QuestItemsBarY = lower ? 0 : 1, QuestItemsButtonSize = lower ? 24 : 56,
            CompanionFourierBlinkEnabled = lower, CompanionSeasonalEnabled = !lower, CompanionSeasonalTheme = theme,
            CompanionAutoRotateEnabled = lower, CompanionRotationInterval = lower ? 30 : 3600,
            LauncherWidth = lower ? 320 : 1200, LauncherTextScale = lower ? .75f : 1.5f,
            LauncherResultCount = lower ? 3 : 12, LauncherCenterX = lower ? .05f : .95f, LauncherTop = lower ? 0 : .9f };
        var expected = store.Export(choice); var imported = new Configuration(); store.ImportInto(imported, expected); store.Save(imported);
        Check(store.Export(store.Load()!) == expected, "Schema eight independent toggles, numeric endpoints and every theme round-trip through JSON and SQLite.");
    }
    foreach (var field in versionSevenFields)
    {
        var missingNap = JsonNode.Parse(exported)!;
        missingNap["Settings"]!.AsObject().Remove(field);
        Invalid(missingNap.ToJsonString(), "Missing current-schema companion preference " + field);
        var mixedNap = versionSixDocument.DeepClone();
        mixedNap["Settings"]![field] = JsonNode.Parse(exported)!["Settings"]![field]!.DeepClone();
        Invalid(mixedNap.ToJsonString(), "Version seven preference in version six schema " + field);
    }
    foreach (var (field, minimum, maximum) in new[]
    {
        ("InteractionHudOutlineThickness", .5f, 4f),
        ("CompanionGrabHandScale", .20f, 1f), ("CompanionNapInterval", 15f, 300f), ("CompanionNapDuration", 5f, 120f),
        ("CompanionHomeX", 0f, 1f), ("CompanionHomeY", 0f, 1f),
    })
    {
        Invalid(Change(field, minimum - .01f), "Below minimum " + field);
        Invalid(Change(field, maximum + .01f), "Above maximum " + field);
        Invalid(Change(field, "NaN"), "Non-finite text " + field);
        Invalid(Change(field, null), "Null number " + field);
    }
    foreach (var field in new[] { "CompanionNapsEnabled", "CompanionNapZsEnabled", "QuestEmoteUseEnabled" })
    {
        Invalid(Change(field, "true"), "Wrong nap flag type " + field);
        Invalid(Change(field, null), "Null nap flag " + field);
    }
    for (var mask = 0; mask < 8; mask++)
    {
        var naps = new Configuration
        {
            CompanionNapsEnabled = (mask & 1) != 0, CompanionNapZsEnabled = (mask & 2) != 0,
            CompanionGrabHandScale = (mask & 4) != 0 ? .20f : 1f,
            CompanionNapInterval = (mask & 4) != 0 ? 15f : 300f, CompanionNapDuration = (mask & 4) != 0 ? 5f : 120f,
            CompanionHomeX = (mask & 4) != 0 ? 0f : 1f, CompanionHomeY = (mask & 4) != 0 ? 1f : 0f,
            InteractionHudOutline = (mask & 1) != 0, InteractionHudOutlineThickness = (mask & 4) != 0 ? .5f : 4f,
            QuestEmoteUseEnabled = (mask & 2) != 0,
        };
        var savedNaps = store.Export(naps);
        var importedNaps = new Configuration();
        store.ImportInto(importedNaps, savedNaps);
        store.Save(importedNaps);
        Check(store.Export(store.Load()!) == savedNaps,
            "Independent nap, quest emote and outline flags, bounded numeric endpoints and house coordinates must round-trip through JSON and SQLite.");
    }
    var missing = JsonNode.Parse(exported)!;
    missing["Settings"]!.AsObject().Remove("Avatar");
    Invalid(missing.ToJsonString(), "Missing fields");
    Invalid(Change("LauncherEnabled", "true"), "Wrong boolean type");
    Invalid(Change("Avatar", null), "Null string");
    foreach (var retired in new[] { "Bimble", "Moonbear" })
    {
        Invalid(Change("Avatar", retired), "Retired avatar in current schema");
        var rejected = false;
        try { store.Export(new Configuration { Avatar = retired }); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Current-schema exports must not silently restore or remap retired avatar selections.");
    }
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
        store.Save(avatarTarget);
        var reloaded = store.Load()!;
        Check(reloaded.Avatar == avatar && reloaded.CompanionOutfit == original.CompanionOutfit,
            "Every active companion must round-trip through JSON/SQLite and preserve its compatibility outfit preference.");
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
    foreach (var field in new[] { nameof(Configuration.InteractionHudOutlineThickness), nameof(Configuration.InteractionHudEffectIntensity), nameof(Configuration.InteractionHudEffectSpeed),
        nameof(Configuration.CompanionCommentInterval), nameof(Configuration.CompanionBlinkInterval), nameof(Configuration.CompanionGrabHandScale),
        nameof(Configuration.CompanionNapInterval), nameof(Configuration.CompanionNapDuration), nameof(Configuration.CompanionHomeX), nameof(Configuration.CompanionHomeY),
        nameof(Configuration.NextActionX), nameof(Configuration.NextActionY), nameof(Configuration.QuestItemsBarX), nameof(Configuration.QuestItemsBarY),
        nameof(Configuration.QuestItemsButtonSize), nameof(Configuration.CompanionRotationInterval) })
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
    checks += AccessoryFitChecks.Run(testDirectory);
    Console.WriteLine($"{checks} storage checks passed: v1-v12 migration to v13, costume selection and fits, bounded UI controls, rollback, atomic validation, export whitelist and AI consent.");
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
