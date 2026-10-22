using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Game.ClientState.Keys;
using Microsoft.Data.Sqlite;
using OmniKit.Search;

namespace OmniKit.Storage;

public sealed record VoiceBinding(string Phrase, string ActionId);

/// <summary>Local settings only. Credentials, conversations and audio are not stored here.</summary>
public sealed class LocalStore
{
    private const int SchemaVersion = 1;
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
        if (existingVersion != 0 && existingVersion != SchemaVersion)
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
            PRAGMA user_version = 1;
            """;
        schema.ExecuteNonQuery();
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
            Apply(configuration, Parse(row.GetString(1)));
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
        Validate(settings);
        return JsonSerializer.Serialize(new Snapshot { Settings = settings }, JsonOptions);
    }

    public void ImportInto(Configuration target, string json)
    {
        ArgumentNullException.ThrowIfNull(target);
        var settings = Parse(json);
        // Importing preferences never grants fresh permission to send text to AI.
        settings.AiEnabled = settings.AiEnabled && target.AiEnabled &&
            ValidEndpoint(target.AiEndpoint, out var currentEndpoint) &&
            ValidEndpoint(settings.AiEndpoint, out var importedEndpoint) &&
            currentEndpoint!.AbsoluteUri == importedEndpoint!.AbsoluteUri;
        Apply(target, settings);
    }

    private static Settings Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumJsonBytes || Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
            throw new InvalidOperationException("Settings must be a non-empty JSON document no larger than 256 KiB.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            RequireFields(document.RootElement, [nameof(Snapshot.SchemaVersion), nameof(Snapshot.Settings)]);
            var settingsElement = document.RootElement.GetProperty(nameof(Snapshot.Settings));
            RequireFields(settingsElement, typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(field => field.Name));
            var bindings = settingsElement.GetProperty(nameof(Settings.VoiceBindings));
            if (bindings.ValueKind != JsonValueKind.Array || bindings.GetArrayLength() > 100)
                throw new InvalidOperationException("Voice bindings must be an array containing at most 100 entries.");
            foreach (var binding in bindings.EnumerateArray())
                RequireFields(binding, [nameof(VoiceBinding.Phrase), nameof(VoiceBinding.ActionId)]);
            var snapshot = JsonSerializer.Deserialize<Snapshot>(json, JsonOptions);
            if (snapshot is null || snapshot.SchemaVersion != SchemaVersion)
                throw new InvalidOperationException("Unsupported settings schema version.");
            Validate(snapshot.Settings);
            return snapshot.Settings;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Settings JSON contains invalid syntax or a value of the wrong type.");
        }
    }

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
        if (settings is null || settings.Version != 1)
            throw new InvalidOperationException("Unsupported configuration version.");
        if (!ValidKey(settings.LauncherKey) || !ValidKey(settings.SettingsKey) || !ValidKey(settings.InteractKey) ||
            !ValidModifier(settings.LauncherModifier) || !ValidModifier(settings.SettingsModifier))
            throw new InvalidOperationException("Choose valid keyboard keys and supported modifiers.");
        if ((settings.LauncherKey == settings.SettingsKey && ModifierGroup(settings.LauncherModifier) == ModifierGroup(settings.SettingsModifier)) ||
            (settings.LauncherModifier == 0 && settings.LauncherKey == settings.InteractKey) ||
            (settings.SettingsModifier == 0 && settings.SettingsKey == settings.InteractKey))
            throw new InvalidOperationException("Launcher, settings and interaction shortcuts must not conflict.");
        if (!FiniteRange(settings.InteractionCone, 0.025f, 0.3f) ||
            !FiniteRange(settings.InteractionDistance, 1f, 6f) ||
            !FiniteRange(settings.InteractionHudOffsetY, -400f, 400f))
            throw new InvalidOperationException("Interaction sensitivity, distance or HUD position is outside the supported range.");
        if (settings.MinerGearset is < -1 or > 99 || settings.BotanistGearset is < -1 or > 99)
            throw new InvalidOperationException("Gathering gearset indices must be -1 for automatic selection or between 0 and 99.");
        if (settings.Avatar is not ("Bimble" or "Moonbear"))
            throw new InvalidOperationException("Choose a supported OmniKit avatar.");
        if (string.IsNullOrEmpty(settings.WikiOverlayName) || settings.WikiOverlayName.Length > 40 ||
            settings.WikiOverlayName.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')))
            throw new InvalidOperationException("The in-game wiki overlay name must be 1–40 letters, digits, underscores or hyphens.");
        if (!ValidEndpoint(settings.AiEndpoint, out _))
            throw new InvalidOperationException("AI endpoints must use HTTPS or literal loopback HTTP, without URL credentials, queries or fragments.");
        if (settings.AiModel is null || settings.AiModel.Length > 200 || settings.AiModel.Any(char.IsControl))
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

    private static bool ValidKey(int key) => key is > 7 and < 255 &&
        Enum.IsDefined((VirtualKey)key) && !ValidModifier(key) && key is not (91 or 92 or >= 160 and <= 165);

    private static bool ValidModifier(int key) => key is 0 or 16 or 17 or 18;
    private static int ModifierGroup(int key) => key switch { 160 or 161 => 16, 162 or 163 => 17, 164 or 165 => 18, _ => key };
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
        target.LauncherModifier = (VirtualKey)value.LauncherModifier;
        target.SettingsKey = (VirtualKey)value.SettingsKey;
        target.SettingsModifier = (VirtualKey)value.SettingsModifier;
        target.SoftInteractEnabled = value.SoftInteractEnabled;
        target.InteractionHudEnabled = value.InteractionHudEnabled;
        target.InteractKey = (VirtualKey)value.InteractKey;
        target.InteractionCone = value.InteractionCone;
        target.InteractionDistance = value.InteractionDistance;
        target.InteractionHudOffsetY = value.InteractionHudOffsetY;
        target.GatheringAutoSwitch = value.GatheringAutoSwitch;
        target.GatheringRestore = value.GatheringRestore;
        target.MinerGearset = value.MinerGearset;
        target.BotanistGearset = value.BotanistGearset;
        target.AvatarEnabled = value.AvatarEnabled;
        target.AnimationsEnabled = value.AnimationsEnabled;
        target.Avatar = value.Avatar;
        target.WikiInGame = value.WikiInGame;
        target.WikiOverlayName = value.WikiOverlayName;
        target.VoiceEnabled = value.VoiceEnabled;
        target.AiEnabled = value.AiEnabled;
        target.AiEndpoint = value.AiEndpoint;
        target.AiModel = value.AiModel;
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
        public bool InteractionHudEnabled;
        public int InteractKey;
        public float InteractionCone;
        public float InteractionDistance;
        public float InteractionHudOffsetY;
        public bool GatheringAutoSwitch;
        public bool GatheringRestore;
        public int MinerGearset;
        public int BotanistGearset;
        public bool AvatarEnabled;
        public bool AnimationsEnabled;
        public string Avatar = "Bimble";
        public bool WikiInGame;
        public string WikiOverlayName = "omnikit";
        public bool VoiceEnabled;
        public bool AiEnabled;
        public string AiEndpoint = "https://api.openai.com/v1/responses";
        public string AiModel = "";
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
            InteractionHudEnabled = value.InteractionHudEnabled,
            InteractKey = (int)value.InteractKey,
            InteractionCone = value.InteractionCone,
            InteractionDistance = value.InteractionDistance,
            InteractionHudOffsetY = value.InteractionHudOffsetY,
            GatheringAutoSwitch = value.GatheringAutoSwitch,
            GatheringRestore = value.GatheringRestore,
            MinerGearset = value.MinerGearset,
            BotanistGearset = value.BotanistGearset,
            AvatarEnabled = value.AvatarEnabled,
            AnimationsEnabled = value.AnimationsEnabled,
            Avatar = value.Avatar,
            WikiInGame = value.WikiInGame,
            WikiOverlayName = value.WikiOverlayName,
            VoiceEnabled = value.VoiceEnabled,
            AiEnabled = value.AiEnabled,
            AiEndpoint = value.AiEndpoint,
            AiModel = value.AiModel,
            VoiceBindings = value.VoiceBindings,
        };
    }
}
