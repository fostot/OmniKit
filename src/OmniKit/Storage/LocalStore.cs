using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Game.ClientState.Keys;
using Microsoft.Data.Sqlite;
using OmniKit.Input;
using OmniKit.Search;

namespace OmniKit.Storage;

public sealed record VoiceBinding(string Phrase, string ActionId);

/// <summary>Local settings only. Credentials, conversations and audio are not stored here.</summary>
public sealed class LocalStore
{
    private const int SchemaVersion = 7;
    private const int MaximumJsonBytes = 256 * 1024;
    private readonly string connectionString;
    private readonly object gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
    };

    public LocalStore(string configDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        Directory.CreateDirectory(configDirectory);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(configDirectory, "omnikit.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 3,
        }.ToString();
        using var connection = Open();
        using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        var existingVersion = Convert.ToInt32(version.ExecuteScalar());
        if (existingVersion is < 0 or > SchemaVersion)
            throw new InvalidOperationException("This settings database uses an unsupported schema. The database was preserved.");
        using var transaction = connection.BeginTransaction();
        using var schema = connection.CreateCommand();
        schema.Transaction = transaction;
        schema.CommandText = """
            CREATE TABLE IF NOT EXISTS OmniKitSettings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                SchemaVersion INTEGER NOT NULL,
                Json TEXT NOT NULL
            );
            """;
        schema.ExecuteNonQuery();
        if (existingVersion < SchemaVersion)
        {
            using var read = connection.CreateCommand();
            read.Transaction = transaction;
            read.CommandText = "SELECT SchemaVersion, Json FROM OmniKitSettings WHERE Id = 1;";
            string? migratedJson = null;
            using (var row = read.ExecuteReader())
            {
                if (row.Read())
                {
                    var settings = Parse(row.GetString(1), row.GetInt32(0));
                    migratedJson = JsonSerializer.Serialize(new Snapshot { Settings = settings }, JsonOptions);
                }
            }
            if (migratedJson != null)
            {
                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = "UPDATE OmniKitSettings SET SchemaVersion = $schema, Json = $json WHERE Id = 1;";
                update.Parameters.AddWithValue("$schema", SchemaVersion);
                update.Parameters.AddWithValue("$json", migratedJson);
                update.ExecuteNonQuery();
            }
        }
        using var setVersion = connection.CreateCommand();
        setVersion.Transaction = transaction;
        setVersion.CommandText = $"PRAGMA user_version = {SchemaVersion};";
        setVersion.ExecuteNonQuery();
        transaction.Commit();
    }

    public Configuration? Load()
    {
        lock (gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT SchemaVersion, Json FROM OmniKitSettings WHERE Id = 1;";
            using var row = command.ExecuteReader();
            if (!row.Read()) return null;
            if (row.GetInt32(0) != SchemaVersion)
                throw new InvalidOperationException("This saved configuration uses an unsupported schema. The database was preserved.");
            var configuration = new Configuration();
            Apply(configuration, Parse(row.GetString(1), row.GetInt32(0)));
            return configuration;
        }
    }

    public void Save(Configuration configuration)
    {
        // Validate and serialize before opening a write transaction.
        var json = Export(configuration);
        lock (gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO OmniKitSettings (Id, SchemaVersion, Json) VALUES (1, $schema, $json)
                ON CONFLICT(Id) DO UPDATE SET SchemaVersion = excluded.SchemaVersion, Json = excluded.Json;
                """;
            command.Parameters.AddWithValue("$schema", SchemaVersion);
            command.Parameters.AddWithValue("$json", json);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    public string Export(Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var settings = Settings.From(configuration);
        UpgradeLegacy(settings);
        Validate(settings);
        return JsonSerializer.Serialize(new Snapshot { Settings = settings }, JsonOptions);
    }

    /// <summary>Upgrade the legacy Dalamud configuration fallback without changing its source object.</summary>
    public Configuration Upgrade(Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var settings = Settings.From(configuration);
        UpgradeLegacy(settings);
        Validate(settings);
        var result = new Configuration();
        Apply(result, settings);
        return result;
    }

    public void ImportInto(Configuration target, string json)
    {
        ArgumentNullException.ThrowIfNull(target);
        var settings = Parse(json);
        // Importing preferences never grants fresh permission to send text to AI.
        settings.AiEnabled = settings.AiEnabled && target.AiEnabled && settings.AiConnection == (int)target.AiConnection &&
            ValidEndpoint(target.AiEndpoint, out var currentEndpoint) &&
            ValidEndpoint(settings.AiEndpoint, out var importedEndpoint) &&
            currentEndpoint!.AbsoluteUri == importedEndpoint!.AbsoluteUri;
        Apply(target, settings);
    }

    private static Settings Parse(string json, int? expectedSchema = null)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumJsonBytes || Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
            throw new InvalidOperationException("Settings must be a non-empty JSON document no larger than 256 KiB.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            RequireFields(document.RootElement, [nameof(Snapshot.SchemaVersion), nameof(Snapshot.Settings)]);
            var schema = document.RootElement.GetProperty(nameof(Snapshot.SchemaVersion));
            if (!schema.TryGetInt32(out var schemaVersion) || schemaVersion is < 1 or > SchemaVersion ||
                (expectedSchema.HasValue && schemaVersion != expectedSchema.Value))
                throw new InvalidOperationException("Unsupported or inconsistent settings schema version.");
            var settingsElement = document.RootElement.GetProperty(nameof(Snapshot.Settings));
            var fields = typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(field => field.Name);
            if (schemaVersion == 1) fields = fields.Except(VersionTwoFields);
            if (schemaVersion < 3) fields = fields.Except(VersionThreeFields);
            if (schemaVersion < 4) fields = fields.Except(VersionFourFields);
            if (schemaVersion < 5) fields = fields.Except(VersionFiveFields);
            if (schemaVersion < 6) fields = fields.Except(VersionSixFields);
            if (schemaVersion < 7) fields = fields.Except(VersionSevenFields);
            RequireFields(settingsElement, fields);
            var bindings = settingsElement.GetProperty(nameof(Settings.VoiceBindings));
            if (bindings.ValueKind != JsonValueKind.Array || bindings.GetArrayLength() > 100)
                throw new InvalidOperationException("Voice bindings must be an array containing at most 100 entries.");
            foreach (var binding in bindings.EnumerateArray())
                RequireFields(binding, [nameof(VoiceBinding.Phrase), nameof(VoiceBinding.ActionId)]);
            var snapshot = JsonSerializer.Deserialize<Snapshot>(json, JsonOptions);
            if (snapshot is null || snapshot.Settings.Version != schemaVersion)
                throw new InvalidOperationException("Unsupported settings schema version.");
            UpgradeLegacy(snapshot.Settings);
            Validate(snapshot.Settings);
            return snapshot.Settings;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Settings JSON contains invalid syntax or a value of the wrong type.");
        }
    }

    private static readonly string[] VersionTwoFields =
    [
        nameof(Settings.InteractModifier), nameof(Settings.InteractionHudLocked),
        nameof(Settings.InteractionHudOffsetX), nameof(Settings.InteractionHudScale),
        nameof(Settings.CompanionBoxLeft), nameof(Settings.CompanionBoxTop), nameof(Settings.CompanionBoxRight),
        nameof(Settings.CompanionBoxBottom), nameof(Settings.CompanionSize),
    ];

    private static void UpgradeLegacy(Settings settings)
    {
        if (settings.Version is < 1 or > SchemaVersion) return;
        // Old values are Win32 virtual-key codes, never bit masks. Convert on a
        // detached DTO so a failure cannot partially alter the live configuration.
        if (settings.Version == 1)
        {
            var launcher = LegacyModifier(settings.LauncherModifier);
            var setup = LegacyModifier(settings.SettingsModifier);
            settings.LauncherModifier = (int)launcher;
            settings.SettingsModifier = (int)setup;
            settings.InteractModifier = 0;
        }
        if (settings.Version < 3)
        {
            settings.CompanionOutfit = "Navigator";
            settings.CompanionPalette = "Moonlight";
            settings.CompanionMagicEnabled = true;
        }
        if (settings.Version < 4)
        {
            settings.InteractionTargets = (int)Interaction.InteractionTargets.DefaultWorld;
            settings.EnemyInteractMode = (int)Interaction.EnemyInteractMode.Choose;
            settings.CompanionFrameInteractionEnabled = false;
            settings.CompanionPickupEnabled = true;
            settings.CompanionPerchEnabled = true;
            settings.CompanionContainmentEnabled = true;
            settings.CompanionFallingEnabled = true;
            settings.AiConnection = (int)Assistant.AiConnectionKind.ApiKey;
            settings.ChatGptModel = ""; settings.CodexModel = ""; settings.ClaudeModel = "";
        }
        if (settings.Version < 5)
        {
            settings.InteractionHudOutline = false;
            settings.InteractionHudEffect = (int)Interaction.HudTextEffect.Shadow;
        }
        if (settings.Version < 6)
        {
            settings.InteractionHudEffectIntensity = 1f;
            settings.InteractionHudEffectSpeed = 1f;
            settings.InteractionHudEffectAnimated = true;
            settings.CompanionHomeEnabled = true;
            settings.CompanionGrabHandEnabled = true;
            settings.CompanionCommentsEnabled = true;
            settings.CompanionCommentInterval = 120f;
            settings.CompanionBlinkEnabled = true;
            settings.CompanionBlinkInterval = 3f;
            settings.BrowserInterceptLinks = false;
            settings.QuestItemUseEnabled = false;
        }
        if (settings.Version < 7)
        {
            if (settings.Avatar is "Bimble" or "Moonbear") settings.Avatar = "Moonblob";
            settings.InteractionHudOutlineThickness = 1f;
            settings.QuestEmoteUseEnabled = false;
            settings.CompanionHomeX = (float)(((double)settings.CompanionBoxLeft + settings.CompanionBoxRight) * .5);
            settings.CompanionHomeY = settings.CompanionBoxBottom;
            settings.CompanionGrabHandScale = .40f;
            settings.CompanionNapsEnabled = true;
            settings.CompanionNapInterval = 45f;
            settings.CompanionNapDuration = 20f;
            settings.CompanionNapZsEnabled = true;
        }
        // Web commands now always use the in-game browser, including imported
        // settings that still contain the former external-browser preference.
        settings.WikiInGame = true;
        settings.Version = SchemaVersion;
    }

    private static readonly string[] VersionThreeFields =
        [nameof(Settings.CompanionOutfit), nameof(Settings.CompanionPalette), nameof(Settings.CompanionMagicEnabled)];

    private static readonly string[] VersionFiveFields =
        [nameof(Settings.InteractionHudOutline), nameof(Settings.InteractionHudEffect)];

    private static readonly string[] VersionSixFields =
        [nameof(Settings.InteractionHudEffectIntensity), nameof(Settings.InteractionHudEffectSpeed), nameof(Settings.InteractionHudEffectAnimated),
        nameof(Settings.CompanionHomeEnabled), nameof(Settings.CompanionGrabHandEnabled), nameof(Settings.CompanionCommentsEnabled), nameof(Settings.CompanionCommentInterval),
        nameof(Settings.CompanionBlinkEnabled), nameof(Settings.CompanionBlinkInterval), nameof(Settings.BrowserInterceptLinks), nameof(Settings.QuestItemUseEnabled)];

    private static readonly string[] VersionSevenFields =
        [nameof(Settings.InteractionHudOutlineThickness), nameof(Settings.QuestEmoteUseEnabled), nameof(Settings.CompanionGrabHandScale), nameof(Settings.CompanionNapsEnabled), nameof(Settings.CompanionNapInterval),
        nameof(Settings.CompanionNapDuration), nameof(Settings.CompanionNapZsEnabled), nameof(Settings.CompanionHomeX), nameof(Settings.CompanionHomeY)];

    private static readonly string[] VersionFourFields =
    [
        nameof(Settings.InteractionTargets), nameof(Settings.EnemyInteractMode),
        nameof(Settings.CompanionFrameInteractionEnabled), nameof(Settings.CompanionPickupEnabled),
        nameof(Settings.CompanionPerchEnabled), nameof(Settings.CompanionContainmentEnabled), nameof(Settings.CompanionFallingEnabled),
        nameof(Settings.AiConnection), nameof(Settings.ChatGptModel), nameof(Settings.CodexModel), nameof(Settings.ClaudeModel),
    ];

    private static ModifierKeys LegacyModifier(int key) => key switch
    {
        0 => ModifierKeys.None,
        16 => ModifierKeys.Shift,
        17 => ModifierKeys.Control,
        18 => ModifierKeys.Alt,
        _ => throw new InvalidOperationException("Legacy settings contain an unsupported modifier key."),
    };

    private static void RequireFields(JsonElement value, IEnumerable<string> fields)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Settings contain an invalid object.");
        var remaining = fields.ToHashSet(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!remaining.Remove(property.Name))
                throw new InvalidOperationException("Settings contain an unknown or duplicate field.");
        if (remaining.Count != 0)
            throw new InvalidOperationException("Settings are missing required fields.");
    }

    private static void Validate(Settings settings)
    {
        if (settings is null || settings.Version != SchemaVersion)
            throw new InvalidOperationException("Unsupported configuration version.");
        if (!Enum.IsDefined((Interaction.HudTextEffect)settings.InteractionHudEffect))
            throw new InvalidOperationException("Choose a supported HUD text effect.");
        if (!FiniteRange(settings.InteractionHudOutlineThickness, .5f, 4f))
            throw new InvalidOperationException("HUD outline thickness is outside the supported range.");
        if (!FiniteRange(settings.InteractionHudEffectIntensity, 0f, 2f) ||
            !FiniteRange(settings.InteractionHudEffectSpeed, .1f, 3f))
            throw new InvalidOperationException("HUD effect intensity or speed is outside the supported range.");
        if (!FiniteRange(settings.CompanionCommentInterval, 30f, 600f) || !FiniteRange(settings.CompanionBlinkInterval, 1f, 10f))
            throw new InvalidOperationException("Companion comment or blink timing is outside the supported range.");
        if (!FiniteRange(settings.CompanionGrabHandScale, .20f, 1f) ||
            !FiniteRange(settings.CompanionNapInterval, 15f, 300f) || !FiniteRange(settings.CompanionNapDuration, 5f, 120f))
            throw new InvalidOperationException("Companion hand size or nap timing is outside the supported range.");
        if (!FiniteRange(settings.CompanionHomeX, 0f, 1f) || !FiniteRange(settings.CompanionHomeY, 0f, 1f))
            throw new InvalidOperationException("The companion home position must be inside the viewport.");
        if ((settings.InteractionTargets & ~(int)Interaction.InteractionTargets.All) != 0 ||
            !Enum.IsDefined((Interaction.EnemyInteractMode)settings.EnemyInteractMode))
            throw new InvalidOperationException("Choose supported interaction target types and an enemy action.");
        if (!ValidKey(settings.LauncherKey) || !ValidKey(settings.SettingsKey) || !ValidKey(settings.InteractKey) ||
            !HotkeyChord.IsValidModifiers((ModifierKeys)settings.LauncherModifier) || !HotkeyChord.IsValidModifiers((ModifierKeys)settings.SettingsModifier) ||
            !HotkeyChord.IsValidModifiers((ModifierKeys)settings.InteractModifier))
            throw new InvalidOperationException("Choose valid keyboard keys and supported modifiers.");
        if ((settings.LauncherKey == settings.SettingsKey && settings.LauncherModifier == settings.SettingsModifier) ||
            (settings.LauncherModifier == settings.InteractModifier && settings.LauncherKey == settings.InteractKey) ||
            (settings.SettingsModifier == settings.InteractModifier && settings.SettingsKey == settings.InteractKey))
            throw new InvalidOperationException("Launcher, settings and interaction shortcuts must not conflict.");
        if (!FiniteRange(settings.InteractionCone, 0.025f, 0.3f) ||
            !FiniteRange(settings.InteractionDistance, Configuration.MinimumDetectionDistance, Configuration.MaximumDetectionDistance) ||
            !FiniteRange(settings.InteractionHudOffsetX, -10000f, 10000f) ||
            !FiniteRange(settings.InteractionHudOffsetY, -10000f, 10000f) ||
            !FiniteRange(settings.InteractionHudScale, .5f, 2f))
            throw new InvalidOperationException("Interaction sensitivity, distance or HUD position is outside the supported range.");
        if (!FiniteRange(settings.CompanionBoxLeft, 0f, 1f) || !FiniteRange(settings.CompanionBoxRight, 0f, 1f) ||
            !FiniteRange(settings.CompanionBoxTop, 0f, 1f) || !FiniteRange(settings.CompanionBoxBottom, 0f, 1f) ||
            settings.CompanionBoxLeft >= settings.CompanionBoxRight || settings.CompanionBoxTop >= settings.CompanionBoxBottom ||
            !FiniteRange(settings.CompanionSize, 48f, 192f))
            throw new InvalidOperationException("The companion area must be an ordered rectangle inside the viewport, with a supported companion size.");
        if (settings.MinerGearset is < -1 or > 99 || settings.BotanistGearset is < -1 or > 99)
            throw new InvalidOperationException("Gathering gearset indices must be -1 for automatic selection or between 0 and 99.");
        if (!Configuration.AvatarNames.Contains(settings.Avatar) || !Configuration.OutfitNames.Contains(settings.CompanionOutfit) ||
            !Configuration.PaletteNames.Contains(settings.CompanionPalette))
            throw new InvalidOperationException("Choose a supported OmniKit avatar.");
        if (string.IsNullOrEmpty(settings.WikiOverlayName) || settings.WikiOverlayName.Length > 40 ||
            settings.WikiOverlayName.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')))
            throw new InvalidOperationException("The in-game wiki overlay name must be 1–40 letters, digits, underscores or hyphens.");
        if (!ValidEndpoint(settings.AiEndpoint, out _))
            throw new InvalidOperationException("AI endpoints must use HTTPS or literal loopback HTTP, without URL credentials, queries or fragments.");
        if (!Enum.IsDefined((Assistant.AiConnectionKind)settings.AiConnection))
            throw new InvalidOperationException("Choose a supported AI connection.");
        if (new[] { settings.AiModel, settings.ChatGptModel, settings.CodexModel, settings.ClaudeModel }
            .Any(model => model is null || model.Length > 200 || model.Any(char.IsControl)))
            throw new InvalidOperationException("The AI model name is invalid.");
        if (settings.VoiceBindings is null || settings.VoiceBindings.Count > 100)
            throw new InvalidOperationException("At most 100 voice bindings are supported.");
        var phrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in settings.VoiceBindings)
        {
            if (binding is null || string.IsNullOrWhiteSpace(binding.Phrase) || binding.Phrase.Length > 96 || binding.Phrase.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(binding.ActionId) || binding.ActionId.Length > 300 || binding.ActionId.Any(char.IsControl))
                throw new InvalidOperationException("Each voice binding needs a valid phrase and action identifier.");
            var normalized = SearchEngine.Normalize(binding.Phrase.Normalize(NormalizationForm.FormKC));
            if (normalized.Length == 0 || !phrases.Add(normalized))
                throw new InvalidOperationException("Voice binding phrases must be unique, ignoring case and extra spaces.");
        }
    }

    private static bool ValidKey(int key) => key is > 7 and < 255 && HotkeyChord.IsValidKey((VirtualKey)key);
    private static bool FiniteRange(float value, float minimum, float maximum) => float.IsFinite(value) && value >= minimum && value <= maximum;

    private static bool ValidEndpoint(string? value, out Uri? endpoint)
    {
        endpoint = null;
        if (value is null || value.Length > 2048 || value.Any(char.IsControl) || !Uri.TryCreate(value, UriKind.Absolute, out endpoint)) return false;
        if (endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0) return false;
        return endpoint.Scheme == Uri.UriSchemeHttps || (endpoint.Scheme == Uri.UriSchemeHttp &&
            IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    private static void Apply(Configuration target, Settings value)
    {
        // All potentially failing validation/allocation happens before assignments.
        var voiceBindings = value.VoiceBindings.Select(binding => new VoiceBinding(binding.Phrase, binding.ActionId)).ToList();
        target.Version = value.Version;
        target.SetupCompleted = value.SetupCompleted;
        target.LauncherEnabled = value.LauncherEnabled;
        target.LauncherKey = (VirtualKey)value.LauncherKey;
        target.LauncherModifier = (ModifierKeys)value.LauncherModifier;
        target.SettingsKey = (VirtualKey)value.SettingsKey;
        target.SettingsModifier = (ModifierKeys)value.SettingsModifier;
        target.SoftInteractEnabled = value.SoftInteractEnabled;
        target.QuestItemUseEnabled = value.QuestItemUseEnabled;
        target.QuestEmoteUseEnabled = value.QuestEmoteUseEnabled;
        target.InteractionTargets = (Interaction.InteractionTargets)value.InteractionTargets;
        target.EnemyInteractMode = (Interaction.EnemyInteractMode)value.EnemyInteractMode;
        target.InteractionHudEnabled = value.InteractionHudEnabled;
        target.InteractKey = (VirtualKey)value.InteractKey;
        target.InteractModifier = (ModifierKeys)value.InteractModifier;
        target.InteractionCone = value.InteractionCone;
        target.InteractionDistance = value.InteractionDistance;
        target.InteractionHudOffsetY = value.InteractionHudOffsetY;
        target.InteractionHudLocked = value.InteractionHudLocked;
        target.InteractionHudOffsetX = value.InteractionHudOffsetX;
        target.InteractionHudScale = value.InteractionHudScale;
        target.InteractionHudOutline = value.InteractionHudOutline;
        target.InteractionHudOutlineThickness = value.InteractionHudOutlineThickness;
        target.InteractionHudEffect = (Interaction.HudTextEffect)value.InteractionHudEffect;
        target.InteractionHudEffectIntensity = value.InteractionHudEffectIntensity;
        target.InteractionHudEffectSpeed = value.InteractionHudEffectSpeed;
        target.InteractionHudEffectAnimated = value.InteractionHudEffectAnimated;
        target.GatheringAutoSwitch = value.GatheringAutoSwitch;
        target.GatheringRestore = value.GatheringRestore;
        target.MinerGearset = value.MinerGearset;
        target.BotanistGearset = value.BotanistGearset;
        target.AvatarEnabled = value.AvatarEnabled;
        target.AnimationsEnabled = value.AnimationsEnabled;
        target.Avatar = value.Avatar;
        target.CompanionOutfit = value.CompanionOutfit;
        target.CompanionPalette = value.CompanionPalette;
        target.CompanionMagicEnabled = value.CompanionMagicEnabled;
        target.CompanionFrameInteractionEnabled = value.CompanionFrameInteractionEnabled;
        target.CompanionPickupEnabled = value.CompanionPickupEnabled;
        target.CompanionPerchEnabled = value.CompanionPerchEnabled;
        target.CompanionContainmentEnabled = value.CompanionContainmentEnabled;
        target.CompanionFallingEnabled = value.CompanionFallingEnabled;
        target.CompanionHomeEnabled = value.CompanionHomeEnabled;
        target.CompanionHomeX = value.CompanionHomeX;
        target.CompanionHomeY = value.CompanionHomeY;
        target.CompanionGrabHandEnabled = value.CompanionGrabHandEnabled;
        target.CompanionGrabHandScale = value.CompanionGrabHandScale;
        target.CompanionNapsEnabled = value.CompanionNapsEnabled;
        target.CompanionNapInterval = value.CompanionNapInterval;
        target.CompanionNapDuration = value.CompanionNapDuration;
        target.CompanionNapZsEnabled = value.CompanionNapZsEnabled;
        target.CompanionCommentsEnabled = value.CompanionCommentsEnabled;
        target.CompanionCommentInterval = value.CompanionCommentInterval;
        target.CompanionBlinkEnabled = value.CompanionBlinkEnabled;
        target.CompanionBlinkInterval = value.CompanionBlinkInterval;
        target.CompanionBoxLeft = value.CompanionBoxLeft;
        target.CompanionBoxTop = value.CompanionBoxTop;
        target.CompanionBoxRight = value.CompanionBoxRight;
        target.CompanionBoxBottom = value.CompanionBoxBottom;
        target.CompanionSize = value.CompanionSize;
        target.WikiInGame = value.WikiInGame;
        target.BrowserInterceptLinks = value.BrowserInterceptLinks;
        target.WikiOverlayName = value.WikiOverlayName;
        target.VoiceEnabled = value.VoiceEnabled;
        target.AiEnabled = value.AiEnabled;
        target.AiConnection = (Assistant.AiConnectionKind)value.AiConnection;
        target.AiEndpoint = value.AiEndpoint;
        target.AiModel = value.AiModel;
        target.ChatGptModel = value.ChatGptModel;
        target.CodexModel = value.CodexModel;
        target.ClaudeModel = value.ClaudeModel;
        target.VoiceBindings = voiceBindings;
    }

    // This explicit DTO is the export whitelist. Never serialize Configuration,
    // its persistence callback, credential storage, or future fields implicitly.
    private sealed class Snapshot
    {
        public Snapshot() { }
        public int SchemaVersion = LocalStore.SchemaVersion;
        public Settings Settings = new();
    }

    private sealed class Settings
    {
        public Settings() { }
        public int Version;
        public bool SetupCompleted;
        public bool LauncherEnabled;
        public int LauncherKey;
        public int LauncherModifier;
        public int SettingsKey;
        public int SettingsModifier;
        public bool SoftInteractEnabled;
        public bool QuestItemUseEnabled;
        public bool QuestEmoteUseEnabled;
        public int InteractionTargets;
        public int EnemyInteractMode;
        public bool InteractionHudEnabled;
        public int InteractKey;
        public int InteractModifier;
        public float InteractionCone;
        public float InteractionDistance;
        public float InteractionHudOffsetY;
        public bool InteractionHudLocked = true;
        public float InteractionHudOffsetX;
        public float InteractionHudScale = 1f;
        public bool InteractionHudOutline;
        public float InteractionHudOutlineThickness = 1f;
        public int InteractionHudEffect = (int)Interaction.HudTextEffect.Shadow;
        public float InteractionHudEffectIntensity = 1f;
        public float InteractionHudEffectSpeed = 1f;
        public bool InteractionHudEffectAnimated = true;
        public bool GatheringAutoSwitch;
        public bool GatheringRestore;
        public int MinerGearset;
        public int BotanistGearset;
        public bool AvatarEnabled;
        public bool AnimationsEnabled;
        public string Avatar = "Moonblob";
        public string CompanionOutfit = "Navigator";
        public string CompanionPalette = "Moonlight";
        public bool CompanionMagicEnabled = true;
        public bool CompanionFrameInteractionEnabled;
        public bool CompanionPickupEnabled = true;
        public bool CompanionPerchEnabled = true;
        public bool CompanionContainmentEnabled = true;
        public bool CompanionFallingEnabled = true;
        public bool CompanionHomeEnabled = true;
        public float CompanionHomeX = .835f;
        public float CompanionHomeY = .92f;
        public bool CompanionGrabHandEnabled = true;
        public float CompanionGrabHandScale = .40f;
        public bool CompanionNapsEnabled = true;
        public float CompanionNapInterval = 45f;
        public float CompanionNapDuration = 20f;
        public bool CompanionNapZsEnabled = true;
        public bool CompanionCommentsEnabled = true;
        public float CompanionCommentInterval = 120f;
        public bool CompanionBlinkEnabled = true;
        public float CompanionBlinkInterval = 3f;
        public float CompanionBoxLeft = .72f;
        public float CompanionBoxTop = .55f;
        public float CompanionBoxRight = .95f;
        public float CompanionBoxBottom = .92f;
        public float CompanionSize = 96f;
        public bool WikiInGame = true;
        public bool BrowserInterceptLinks = false;
        public string WikiOverlayName = "omnikit";
        public bool VoiceEnabled;
        public bool AiEnabled;
        public int AiConnection;
        public string AiEndpoint = "https://api.openai.com/v1/responses";
        public string AiModel = "";
        public string ChatGptModel = "";
        public string CodexModel = "";
        public string ClaudeModel = "";
        public List<VoiceBinding> VoiceBindings = [];

        public static Settings From(Configuration value) => new()
        {
            Version = value.Version,
            SetupCompleted = value.SetupCompleted,
            LauncherEnabled = value.LauncherEnabled,
            LauncherKey = (int)value.LauncherKey,
            LauncherModifier = (int)value.LauncherModifier,
            SettingsKey = (int)value.SettingsKey,
            SettingsModifier = (int)value.SettingsModifier,
            SoftInteractEnabled = value.SoftInteractEnabled,
            QuestItemUseEnabled = value.QuestItemUseEnabled,
            QuestEmoteUseEnabled = value.QuestEmoteUseEnabled,
            InteractionTargets = (int)value.InteractionTargets,
            EnemyInteractMode = (int)value.EnemyInteractMode,
            InteractionHudEnabled = value.InteractionHudEnabled,
            InteractKey = (int)value.InteractKey,
            InteractModifier = (int)value.InteractModifier,
            InteractionCone = value.InteractionCone,
            InteractionDistance = value.InteractionDistance,
            InteractionHudOffsetY = value.InteractionHudOffsetY,
            InteractionHudLocked = value.InteractionHudLocked,
            InteractionHudOffsetX = value.InteractionHudOffsetX,
            InteractionHudScale = value.InteractionHudScale,
            InteractionHudOutline = value.InteractionHudOutline,
            InteractionHudOutlineThickness = value.InteractionHudOutlineThickness,
            InteractionHudEffect = (int)value.InteractionHudEffect,
            InteractionHudEffectIntensity = value.InteractionHudEffectIntensity,
            InteractionHudEffectSpeed = value.InteractionHudEffectSpeed,
            InteractionHudEffectAnimated = value.InteractionHudEffectAnimated,
            GatheringAutoSwitch = value.GatheringAutoSwitch,
            GatheringRestore = value.GatheringRestore,
            MinerGearset = value.MinerGearset,
            BotanistGearset = value.BotanistGearset,
            AvatarEnabled = value.AvatarEnabled,
            AnimationsEnabled = value.AnimationsEnabled,
            Avatar = value.Avatar,
            CompanionOutfit = value.CompanionOutfit,
            CompanionPalette = value.CompanionPalette,
            CompanionMagicEnabled = value.CompanionMagicEnabled,
            CompanionFrameInteractionEnabled = value.CompanionFrameInteractionEnabled,
            CompanionPickupEnabled = value.CompanionPickupEnabled,
            CompanionPerchEnabled = value.CompanionPerchEnabled,
            CompanionContainmentEnabled = value.CompanionContainmentEnabled,
            CompanionFallingEnabled = value.CompanionFallingEnabled,
            CompanionHomeEnabled = value.CompanionHomeEnabled,
            CompanionHomeX = value.CompanionHomeX,
            CompanionHomeY = value.CompanionHomeY,
            CompanionGrabHandEnabled = value.CompanionGrabHandEnabled,
            CompanionGrabHandScale = value.CompanionGrabHandScale,
            CompanionNapsEnabled = value.CompanionNapsEnabled,
            CompanionNapInterval = value.CompanionNapInterval,
            CompanionNapDuration = value.CompanionNapDuration,
            CompanionNapZsEnabled = value.CompanionNapZsEnabled,
            CompanionCommentsEnabled = value.CompanionCommentsEnabled,
            CompanionCommentInterval = value.CompanionCommentInterval,
            CompanionBlinkEnabled = value.CompanionBlinkEnabled,
            CompanionBlinkInterval = value.CompanionBlinkInterval,
            CompanionBoxLeft = value.CompanionBoxLeft,
            CompanionBoxTop = value.CompanionBoxTop,
            CompanionBoxRight = value.CompanionBoxRight,
            CompanionBoxBottom = value.CompanionBoxBottom,
            CompanionSize = value.CompanionSize,
            WikiInGame = value.WikiInGame,
            BrowserInterceptLinks = value.BrowserInterceptLinks,
            WikiOverlayName = value.WikiOverlayName,
            VoiceEnabled = value.VoiceEnabled,
            AiEnabled = value.AiEnabled,
            AiConnection = (int)value.AiConnection,
            AiEndpoint = value.AiEndpoint,
            AiModel = value.AiModel,
            ChatGptModel = value.ChatGptModel,
            CodexModel = value.CodexModel,
            ClaudeModel = value.ClaudeModel,
            VoiceBindings = value.VoiceBindings,
        };
    }
}
