using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace OmniKit.Assistant;

public sealed record ChatTurn([property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);
public sealed record ChatReply(string Text, bool Succeeded);

/// <summary>One bounded local conversation, encrypted for the current Windows account.</summary>
public sealed class ChatSessionStore
{
    private const int MaximumBlobBytes = 512 * 1024;
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes("OmniKit.ChatSession.v1"));
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false, MaxDepth = 5,
    };
    private readonly object gate = new();
    private readonly string directory;
    private readonly string connectionString;
    private string? binding;
    private ChatTurn[] turns = [];
    private long generation;
    private long diskGeneration;
    private bool persistenceBlocked;
    private bool replayBlocked;
    public string Status { get; private set; } = "No saved chat session.";
    public long Generation { get { lock (gate) return generation; } }
    public bool CanContinue { get { lock (gate) return !replayBlocked; } }

    public ChatSessionStore(DirectoryInfo dir)
    {
        ArgumentNullException.ThrowIfNull(dir);
        directory = dir.FullName;
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "omnikit.db"), Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false, DefaultTimeout = 3,
        }.ToString();
        try
        {
            using var connection = Open(); using var transaction = connection.BeginTransaction();
            EnsureSchema(connection, transaction);
            diskGeneration = ReadGeneration(connection, transaction);
            var loaded = Load(connection, transaction);
            var next = checked(diskGeneration + 1);
            SetGeneration(connection, transaction, next, diskGeneration);
            transaction.Commit(); generation = diskGeneration = next;
            binding = loaded?.Binding; turns = loaded?.Turns ?? [];
            Status = turns.Length > 0 ? "Protected local chat session restored." : "No saved chat session.";
        }
        catch
        {
            binding = null; turns = []; persistenceBlocked = true;
            Status = "Saved chat could not be read and was preserved. New replies stay in memory until New session succeeds.";
        }
    }

    public IReadOnlyList<ChatTurn> Snapshot(string binding)
    {
        binding = ValidBinding(binding);
        lock (gate)
        {
            if (replayBlocked) return Array.AsReadOnly(Array.Empty<ChatTurn>());
            if (!persistenceBlocked) VerifyReplayGeneration();
            if (replayBlocked) return Array.AsReadOnly(Array.Empty<ChatTurn>());
            if (this.binding != binding)
            {
                generation = checked(generation + 1); this.binding = binding; turns = [];
                TryPersist(new(1, binding, []));
            }
            return Array.AsReadOnly(turns.Select(turn => turn with { }).ToArray());
        }
    }

    public string LastAnswer(string binding) => Snapshot(binding).LastOrDefault(turn => turn.Role == "assistant")?.Content ?? "";

    /// <returns>True when the reply belongs to the current session, including a memory-only session.</returns>
    public bool Complete(long expectedGeneration, string binding, string question, string answer)
    {
        if (!TryBinding(binding, out binding) || !ValidText(question, 4000) || !ValidText(answer, 6050)) return false;
        lock (gate)
        {
            if (replayBlocked || generation != expectedGeneration || this.binding != binding) return false;
            var next = turns.Concat([new ChatTurn("user", question), new ChatTurn("assistant", answer)]).ToList();
            var characters = next.Sum(turn => turn.Content.Length);
            while (next.Count > 16 || characters > 24_000)
            { characters -= next[0].Content.Length + next[1].Content.Length; next.RemoveRange(0, 2); }
            turns = next.ToArray();
            return TryPersist(new(1, binding, turns));
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            try
            {
                using var connection = Open();
                using (var secureDelete = connection.CreateCommand())
                { secureDelete.CommandText = "PRAGMA secure_delete=ON"; secureDelete.ExecuteNonQuery(); }
                using var transaction = connection.BeginTransaction();
                EnsureSchema(connection, transaction, reset: true);
                var next = checked(Math.Max(generation, ReadGeneration(connection, transaction)) + 1);
                using (var delete = connection.CreateCommand())
                { delete.Transaction = transaction; delete.CommandText = "DELETE FROM OmniKitChatSession"; delete.ExecuteNonQuery(); }
                using (var version = connection.CreateCommand())
                {
                    version.Transaction = transaction;
                    version.CommandText = "UPDATE OmniKitChatSessionMeta SET SchemaVersion=1,Generation=$generation WHERE Id=1";
                    version.Parameters.AddWithValue("$generation", next); version.ExecuteNonQuery();
                }
                transaction.Commit();
                generation = diskGeneration = next; binding = null; turns = []; persistenceBlocked = replayBlocked = false;
                Status = "Saved local chat session cleared.";
            }
            catch
            {
                Status = "The saved chat could not be cleared. Its existing context was preserved.";
                throw new InvalidOperationException("The saved chat could not be cleared. Try New session again when local storage is available.");
            }
        }
    }

    private bool TryPersist(Payload payload)
    {
        if (persistenceBlocked)
        { Status = "Chat context is memory-only; unreadable saved data remains preserved until New session succeeds."; return !replayBlocked; }
        byte[]? plain = null; byte[]? encrypted = null;
        try
        {
            plain = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
            encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            if (encrypted.Length > MaximumBlobBytes) throw new InvalidOperationException();
            using var connection = Open(); using var transaction = connection.BeginTransaction();
            EnsureSchema(connection, transaction);
            SetGeneration(connection, transaction, generation, diskGeneration);
            using var write = connection.CreateCommand(); write.Transaction = transaction;
            write.CommandText = "INSERT INTO OmniKitChatSession(Id,Ciphertext) VALUES(1,$encrypted) ON CONFLICT(Id) DO UPDATE SET Ciphertext=excluded.Ciphertext";
            write.Parameters.Add("$encrypted", SqliteType.Blob).Value = encrypted; write.ExecuteNonQuery();
            transaction.Commit(); diskGeneration = generation;
            Status = "Chat context saved in protected local storage.";
        }
        catch (SessionConflictException)
        {
            BlockReplay();
        }
        catch
        { Status = "Chat context is available in memory but could not be saved to protected local storage."; }
        finally
        {
            if (plain != null) CryptographicOperations.ZeroMemory(plain);
            if (encrypted != null) CryptographicOperations.ZeroMemory(encrypted);
        }
        return !replayBlocked;
    }

    private void VerifyReplayGeneration()
    {
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder(connectionString)
                { Mode = SqliteOpenMode.ReadOnly }.ToString());
            connection.Open(); using var command = connection.CreateCommand();
            command.CommandText = "SELECT SchemaVersion,Generation FROM OmniKitChatSessionMeta WHERE Id=1";
            using var row = command.ExecuteReader();
            if (!row.Read() || row.GetInt32(0) != 1 || row.GetInt64(1) != diskGeneration) BlockReplay();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 or 10 or 13 or 14)
        { Status = "Saved chat could not be checked. Current context is available in memory only."; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { Status = "Saved chat could not be checked. Current context is available in memory only."; }
        catch { BlockReplay(); }
    }

    private void BlockReplay()
    {
        persistenceBlocked = replayBlocked = true; turns = []; binding = null; generation = checked(generation + 1);
        Status = "Saved chat changed outside this session. Context replay is blocked until New session succeeds.";
    }

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(directory);
        var connection = new SqliteConnection(connectionString);
        try { connection.Open(); return connection; } catch { connection.Dispose(); throw; }
    }
    private static void EnsureSchema(SqliteConnection connection, SqliteTransaction transaction, bool reset = false)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS OmniKitChatSessionMeta(Id INTEGER PRIMARY KEY CHECK(Id=1),SchemaVersion INTEGER NOT NULL,Generation INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS OmniKitChatSession(Id INTEGER PRIMARY KEY CHECK(Id=1),Ciphertext BLOB NOT NULL);
            INSERT OR IGNORE INTO OmniKitChatSessionMeta VALUES(1,1,0);
            """;
        command.ExecuteNonQuery();
        command.CommandText = "SELECT SchemaVersion FROM OmniKitChatSessionMeta WHERE Id=1";
        if (!reset && Convert.ToInt32(command.ExecuteScalar()) != 1) throw new SessionConflictException();
    }
    private static long ReadGeneration(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT Generation FROM OmniKitChatSessionMeta WHERE Id=1";
        var number = Convert.ToInt64(command.ExecuteScalar());
        if (number < 0 || number == long.MaxValue) throw new SessionConflictException();
        return number;
    }
    private static void SetGeneration(SqliteConnection connection, SqliteTransaction transaction, long value, long expected)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "UPDATE OmniKitChatSessionMeta SET Generation=$next WHERE Id=1 AND SchemaVersion=1 AND Generation=$expected";
        command.Parameters.AddWithValue("$next", value); command.Parameters.AddWithValue("$expected", expected);
        if (command.ExecuteNonQuery() != 1) throw new SessionConflictException();
    }
    private static Payload? Load(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT typeof(Ciphertext),length(Ciphertext) FROM OmniKitChatSession WHERE Id=1";
        using (var size = command.ExecuteReader())
        {
            if (!size.Read()) return null;
            if (size.GetString(0) != "blob" || size.GetInt64(1) is <= 0 or > MaximumBlobBytes) throw new SessionConflictException();
        }
        command.CommandText = "SELECT Ciphertext FROM OmniKitChatSession WHERE Id=1";
        byte[]? encrypted = null; byte[]? plain = null;
        try
        {
            encrypted = (byte[])command.ExecuteScalar()!;
            plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            if (plain.Length > MaximumBlobBytes) throw new SessionConflictException();
            using var document = JsonDocument.Parse(plain, new JsonDocumentOptions { MaxDepth = 5 });
            RequireFields(document.RootElement, ["schemaVersion", "binding", "turns"]);
            var payload = JsonSerializer.Deserialize<Payload>(plain, JsonOptions) ?? throw new SessionConflictException();
            if (payload.SchemaVersion != 1 || !TryBinding(payload.Binding, out var normalized) || payload.Binding != normalized
                || payload.Turns == null || payload.Turns.Length > 16 || payload.Turns.Length % 2 != 0) throw new SessionConflictException();
            var total = 0;
            for (var index = 0; index < payload.Turns.Length; index++)
            {
                var turn = payload.Turns[index];
                RequireFields(document.RootElement.GetProperty("turns")[index], ["role", "content"]);
                if (turn == null || turn.Role != (index % 2 == 0 ? "user" : "assistant")
                    || !ValidText(turn.Content, index % 2 == 0 ? 4000 : 6050)) throw new SessionConflictException();
                total += turn.Content.Length;
            }
            if (total > 24_000) throw new SessionConflictException();
            return payload;
        }
        finally
        {
            if (encrypted != null) CryptographicOperations.ZeroMemory(encrypted);
            if (plain != null) CryptographicOperations.ZeroMemory(plain);
        }
    }
    private sealed record Payload([property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("binding")] string Binding, [property: JsonPropertyName("turns")] ChatTurn[] Turns);
    private sealed class SessionConflictException : Exception;
    private static void RequireFields(JsonElement element, string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new SessionConflictException();
        var fields = names.ToHashSet(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject()) if (!fields.Remove(property.Name)) throw new SessionConflictException();
        if (fields.Count != 0) throw new SessionConflictException();
    }
    private static bool TryBinding(string? value, out string normalized)
    {
        normalized = "";
        if (value == null || value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character))) return false;
        normalized = value.ToUpperInvariant(); return true;
    }
    private static string ValidBinding(string value) => TryBinding(value, out var normalized) ? normalized
        : throw new InvalidOperationException("The chat connection identity is unavailable.");
    private static bool ValidText(string? text, int maximum)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum) return false;
        var span = text.AsSpan();
        while (!span.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(span, out var rune, out var consumed) != OperationStatus.Done
                || Rune.IsControl(rune) && rune.Value is not ('\r' or '\n' or '\t')) return false;
            span = span[consumed..];
        }
        return true;
    }
}
