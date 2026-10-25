using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace OmniKit.Companions;

internal sealed record SayingEntry(string Id, string Companion, string Context, string Text,
    bool Enabled, bool Deleted, bool BuiltIn);

internal sealed record SayingImportPreview(int Added, int Updated, int Unchanged, int Total, int Deleted);

/// <summary>Local-only sayings. This library never logs text or modifies the settings schema.</summary>
internal sealed class SayingLibrary
{
    public const int MaximumEntries = 2000;
    public const int MaximumTextCharacters = 240;
    public const int MaximumJsonBytes = 1024 * 1024;
    public static IReadOnlyList<string> Contexts { get; } = Array.AsReadOnly(new[]
        { "Any", "Idle", "Walking", "Perched", "Returning", "Home", "Held", "Waking" });
    public static IReadOnlyList<string> Companions { get; } = Array.AsReadOnly(
        new[] { "Universal" }.Concat(Configuration.AvatarNames).ToArray());
    private readonly string connectionString;
    private readonly object gate = new();
    private IReadOnlyList<SayingEntry> snapshot = [];
    public int Revision { get; private set; }
    private static readonly JsonSerializerOptions JsonOptions = new()
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public SayingLibrary(string configDirectory, string? seedPackPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);
        var seed = seedPackPath == null ? new Pack(1, []) : ReadSeed(seedPackPath);
        Directory.CreateDirectory(configDirectory);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(configDirectory, "omnikit.db"), Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false, DefaultTimeout = 3,
        }.ToString();
        using var connection = Open(); using var transaction = connection.BeginTransaction();
        using (var schema = connection.CreateCommand())
        {
            schema.Transaction = transaction;
            schema.CommandText = """
                CREATE TABLE IF NOT EXISTS OmniKitSayingLibraryMeta (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1), SchemaVersion INTEGER NOT NULL,
                    SeedRevision INTEGER NOT NULL, Revision INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS OmniKitSayings (
                    Id TEXT PRIMARY KEY, Companion TEXT NOT NULL, Context TEXT NOT NULL, Text TEXT NOT NULL,
                    Enabled INTEGER NOT NULL CHECK (Enabled IN (0,1)),
                    Deleted INTEGER NOT NULL CHECK (Deleted IN (0,1)),
                    BuiltIn INTEGER NOT NULL CHECK (BuiltIn IN (0,1)));
                INSERT OR IGNORE INTO OmniKitSayingLibraryMeta VALUES (1,1,0,0);
                """;
            schema.ExecuteNonQuery();
        }
        using (var version = connection.CreateCommand())
        {
            version.Transaction = transaction; version.CommandText = "SELECT SchemaVersion FROM OmniKitSayingLibraryMeta WHERE Id=1";
            if (Convert.ToInt32(version.ExecuteScalar()) != 1)
                throw new InvalidOperationException("The sayings library schema is unsupported. Its data was preserved.");
        }
        var existing = ReadAll(connection, transaction).ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        if (existing.Count + seed.Entries.Count(entry => !existing.ContainsKey(entry.Id)) > MaximumEntries)
            throw new InvalidOperationException("The sayings library has no room for this seed revision. Existing entries were preserved.");
        var added = 0;
        foreach (var entry in seed.Entries)
            if (!existing.ContainsKey(entry.Id)) { Write(connection, transaction, entry with { BuiltIn = true }, insertOnly: true); added++; }
        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE OmniKitSayingLibraryMeta SET SeedRevision=MAX(SeedRevision,$seed), Revision=Revision+$changed WHERE Id=1";
            update.Parameters.AddWithValue("$seed", seed.Revision); update.Parameters.AddWithValue("$changed", added > 0 ? 1 : 0);
            update.ExecuteNonQuery();
        }
        var loaded = ReadAll(connection, transaction); var revision = ReadRevision(connection, transaction);
        _ = Serialize(loaded, revision);
        transaction.Commit(); snapshot = loaded; Revision = revision;
    }

    public IReadOnlyList<SayingEntry> GetAll() { lock (gate) return snapshot; }
    public SayingEntry Add(string companion, string context, string text)
    {
        var entry = Validate(new("custom:" + Guid.NewGuid().ToString("N"), companion, context, text, true, false, false));
        lock (gate)
        {
            using var connection = Open(); using var transaction = connection.BeginTransaction();
            if (ReadAll(connection, transaction).Count >= MaximumEntries) throw new InvalidOperationException("The sayings library is full.");
            Write(connection, transaction, entry); Publish(connection, transaction, true); return entry;
        }
    }
    public SayingEntry Edit(string id, string companion, string context, string text)
        => Change(id, entry => Validate(entry with { Companion = companion, Context = context, Text = text }));
    public SayingEntry SetEnabled(string id, bool enabled) => Change(id, entry => entry with { Enabled = enabled });
    public SayingEntry Remove(string id) => Change(id, entry => entry with { Deleted = true });
    public SayingEntry Restore(string id) => Change(id, entry => entry with { Deleted = false });

    private SayingEntry Change(string id, Func<SayingEntry, SayingEntry> change)
    {
        ValidateId(id);
        lock (gate)
        {
            using var connection = Open(); using var transaction = connection.BeginTransaction();
            var current = ReadAll(connection, transaction).SingleOrDefault(entry => entry.Id == id)
                ?? throw new InvalidOperationException("That saying is no longer available.");
            var next = Validate(change(current));
            if (next != current) Write(connection, transaction, next);
            Publish(connection, transaction, next != current); return next;
        }
    }

    public string ExportJson()
    {
        lock (gate)
        {
            return Serialize(snapshot, Revision);
        }
    }
    public SayingImportPreview PreviewImport(string json)
    {
        var pack = Parse(json, false);
        lock (gate) return Preview(snapshot, pack.Entries, Revision);
    }
    public SayingImportPreview MergeImport(string json)
    {
        var pack = Parse(json, false);
        lock (gate)
        {
            using var connection = Open(); using var transaction = connection.BeginTransaction();
            var current = ReadAll(connection, transaction);
            var preview = Preview(current, pack.Entries, ReadRevision(connection, transaction));
            var known = current.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            foreach (var entry in pack.Entries)
            {
                // Built-in attribution is local metadata; importing cannot invent it or remove it.
                var next = entry with { BuiltIn = known.TryGetValue(entry.Id, out var old) && old.BuiltIn };
                if (old != next) Write(connection, transaction, next);
            }
            Publish(connection, transaction, preview.Added + preview.Updated > 0); return preview;
        }
    }
    private static SayingImportPreview Preview(IReadOnlyList<SayingEntry> current, IReadOnlyList<SayingEntry> incoming, int revision)
    {
        var known = current.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var added = 0; var updated = 0; var unchanged = 0;
        foreach (var entry in incoming)
        {
            if (!known.TryGetValue(entry.Id, out var old)) added++;
            else if (entry with { BuiltIn = old.BuiltIn } == old) unchanged++;
            else updated++;
        }
        if (current.Count + added > MaximumEntries) throw new InvalidOperationException("This import would exceed 2000 sayings. No entries were changed.");
        foreach (var entry in incoming)
            known[entry.Id] = entry with { BuiltIn = known.TryGetValue(entry.Id, out var previous) && previous.BuiltIn };
        _ = Serialize(known.Values.ToArray(), revision + (added + updated > 0 ? 1 : 0));
        return new(added, updated, unchanged, incoming.Count, incoming.Count(entry => entry.Deleted));
    }

    private void Publish(SqliteConnection connection, SqliteTransaction transaction, bool changed)
    {
        if (changed)
        {
            using var revision = connection.CreateCommand(); revision.Transaction = transaction;
            revision.CommandText = "UPDATE OmniKitSayingLibraryMeta SET Revision=Revision+1 WHERE Id=1"; revision.ExecuteNonQuery();
        }
        var loaded = ReadAll(connection, transaction); var number = ReadRevision(connection, transaction);
        _ = Serialize(loaded, number);
        transaction.Commit(); snapshot = loaded; Revision = number;
    }
    private static string Serialize(IReadOnlyList<SayingEntry> entries, int revision)
    {
        var json = JsonSerializer.Serialize(new { SchemaVersion = 1, Revision = revision, Entries = entries }, JsonOptions);
        if (Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
            throw new InvalidOperationException("The sayings library would exceed its one MiB JSON export limit. Existing entries were preserved.");
        return json;
    }
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        try { connection.Open(); return connection; } catch { connection.Dispose(); throw; }
    }
    private static int ReadRevision(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT Revision FROM OmniKitSayingLibraryMeta WHERE Id=1";
        return Convert.ToInt32(command.ExecuteScalar());
    }
    private static IReadOnlyList<SayingEntry> ReadAll(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT Id,Companion,Context,Text,Enabled,Deleted,BuiltIn FROM OmniKitSayings ORDER BY Companion,Context,Id";
        using var rows = command.ExecuteReader(); var result = new List<SayingEntry>();
        while (rows.Read())
        {
            if (result.Count >= MaximumEntries) throw new InvalidOperationException("The sayings library exceeds its supported size.");
            result.Add(Validate(new(rows.GetString(0), rows.GetString(1), rows.GetString(2), rows.GetString(3),
                rows.GetBoolean(4), rows.GetBoolean(5), rows.GetBoolean(6))));
        }
        return Array.AsReadOnly(result.ToArray());
    }
    private static void Write(SqliteConnection connection, SqliteTransaction transaction, SayingEntry entry, bool insertOnly = false)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = insertOnly ? """
            INSERT OR IGNORE INTO OmniKitSayings (Id,Companion,Context,Text,Enabled,Deleted,BuiltIn)
            VALUES ($id,$companion,$context,$text,$enabled,$deleted,$builtin);
            """ : """
            INSERT INTO OmniKitSayings (Id,Companion,Context,Text,Enabled,Deleted,BuiltIn)
            VALUES ($id,$companion,$context,$text,$enabled,$deleted,$builtin)
            ON CONFLICT(Id) DO UPDATE SET Companion=excluded.Companion,Context=excluded.Context,Text=excluded.Text,
                Enabled=excluded.Enabled,Deleted=excluded.Deleted,BuiltIn=excluded.BuiltIn;
            """;
        command.Parameters.AddWithValue("$id", entry.Id); command.Parameters.AddWithValue("$companion", entry.Companion);
        command.Parameters.AddWithValue("$context", entry.Context); command.Parameters.AddWithValue("$text", entry.Text);
        command.Parameters.AddWithValue("$enabled", entry.Enabled); command.Parameters.AddWithValue("$deleted", entry.Deleted);
        command.Parameters.AddWithValue("$builtin", entry.BuiltIn); command.ExecuteNonQuery();
    }
    private sealed record Pack(int Revision, IReadOnlyList<SayingEntry> Entries);
    private static Pack ReadSeed(string path)
    {
        if (!File.Exists(path)) throw new InvalidOperationException("The sayings seed pack is missing.");
        if (new FileInfo(path).Length > MaximumJsonBytes) throw new InvalidOperationException("The sayings seed pack exceeds one MiB.");
        return Parse(File.ReadAllText(path), true);
    }
    private static Pack Parse(string json, bool seed)
    {
        if (json == null || Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
            throw new InvalidOperationException("Sayings JSON must be no larger than one MiB.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 6 });
            var root = document.RootElement; RequireFields(root, ["schemaVersion", "revision", "entries"]);
            if (!root.GetProperty("schemaVersion").TryGetInt32(out var schema) || schema != 1
                || !root.GetProperty("revision").TryGetInt32(out var revision) || revision < 0)
                throw new InvalidOperationException("The sayings JSON schema or revision is unsupported.");
            var entries = root.GetProperty("entries");
            if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > MaximumEntries)
                throw new InvalidOperationException("Sayings JSON must contain at most 2000 entries.");
            var result = new List<SayingEntry>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in entries.EnumerateArray())
            {
                RequireFields(item, seed ? ["id", "companion", "context", "text", "enabled"]
                    : ["id", "companion", "context", "text", "enabled", "deleted", "builtIn"]);
                var entry = Validate(new(item.GetProperty("id").GetString()!, item.GetProperty("companion").GetString()!,
                    item.GetProperty("context").GetString()!, item.GetProperty("text").GetString()!,
                    item.GetProperty("enabled").GetBoolean(), !seed && item.GetProperty("deleted").GetBoolean(),
                    seed || item.GetProperty("builtIn").GetBoolean()));
                if (seed && !entry.Id.StartsWith("builtin:", StringComparison.Ordinal))
                    throw new InvalidOperationException("Seed sayings need stable built-in identifiers.");
                if (!ids.Add(entry.Id)) throw new InvalidOperationException("Sayings JSON contains duplicate identifiers.");
                result.Add(entry);
            }
            return new(revision, result);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            // Do not include parser exceptions: they can contain private input values or excerpts.
            throw new InvalidOperationException("Sayings JSON is invalid. Check its fields, limits, identifiers and values.");
        }
    }
    private static void RequireFields(JsonElement item, string[] names)
    {
        if (item.ValueKind != JsonValueKind.Object) throw new InvalidOperationException();
        var remaining = names.ToHashSet(StringComparer.Ordinal);
        foreach (var property in item.EnumerateObject()) if (!remaining.Remove(property.Name)) throw new InvalidOperationException();
        if (remaining.Count != 0) throw new InvalidOperationException();
    }
    private static SayingEntry Validate(SayingEntry entry)
    {
        ValidateId(entry.Id);
        if (!Companions.Contains(entry.Companion, StringComparer.Ordinal) || !Contexts.Contains(entry.Context, StringComparer.Ordinal))
            throw new InvalidOperationException("Choose a known companion and saying context.");
        if (string.IsNullOrWhiteSpace(entry.Text)) throw new InvalidOperationException("A saying needs text.");
        if (entry.Text.Any(char.IsControl) || entry.Text.Contains('\u2028') || entry.Text.Contains('\u2029'))
            throw new InvalidOperationException("Sayings must be a single line without control characters.");
        var text = entry.Text.Trim(); var span = text.AsSpan(); var count = 0;
        while (!span.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(span, out var rune, out var consumed) != OperationStatus.Done
                || Rune.IsControl(rune) || Rune.GetUnicodeCategory(rune) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
                || ++count > MaximumTextCharacters)
                throw new InvalidOperationException("Sayings must be one line containing 1 to 240 Unicode characters without control characters.");
            span = span[consumed..];
        }
        return entry with { Text = text };
    }
    private static void ValidateId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 96 || id.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character is not (':' or '-' or '_' or '.')))
            throw new InvalidOperationException("The saying identifier is invalid.");
    }
}
