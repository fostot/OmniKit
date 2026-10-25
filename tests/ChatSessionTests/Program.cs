using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using OmniKit.Assistant;

var directory = Path.Combine(Path.GetTempPath(), "OmniKit-chat-session-synthetic-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var checks = 0;
var a = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("synthetic binding A")));
var b = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("synthetic binding B")));
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
ChatSessionStore Store(string name) => new(new DirectoryInfo(Path.Combine(directory, name)));
SqliteConnection Open(string name)
{
    var connection = new SqliteConnection($"Data Source={Path.Combine(directory, name, "omnikit.db")};Pooling=False");
    connection.Open(); return connection;
}
byte[] Cipher(string name)
{
    using var connection = Open(name); using var command = connection.CreateCommand();
    command.CommandText = "SELECT Ciphertext FROM OmniKitChatSession WHERE Id=1";
    return (byte[])command.ExecuteScalar()!;
}
void WriteCipher(string name, byte[] bytes)
{
    using var connection = Open(name); using var command = connection.CreateCommand();
    command.CommandText = "INSERT INTO OmniKitChatSession VALUES(1,$blob) ON CONFLICT(Id) DO UPDATE SET Ciphertext=excluded.Ciphertext";
    command.Parameters.Add("$blob", SqliteType.Blob).Value = bytes; command.ExecuteNonQuery();
}
try
{
    var store = Store("basic");
    Check(store.Snapshot(a).Count == 0 && store.LastAnswer(a) == "", "new session begins empty");
    var generation = store.Generation;
    const string question = "Synthetic-private-question-5c8239: How does an aetheryte work?";
    const string answer = "Synthetic-private-answer-92ba17: This is a local test response.";
    Check(store.Complete(generation, a, question, answer), "complete pair accepted for the active binding and generation");
    var snapshot = store.Snapshot(a);
    Check(snapshot.Count == 2 && snapshot[0] == new ChatTurn("user", question)
        && snapshot[1] == new ChatTurn("assistant", answer) && store.LastAnswer(a) == answer, "ordered complete pair and last answer retained");
    Check(!ReferenceEquals(snapshot, store.Snapshot(a)), "callers receive copied immutable snapshots");
    var blob = Cipher("basic");
    Check(!Encoding.UTF8.GetString(blob).Contains(question, StringComparison.Ordinal)
        && !Encoding.UTF8.GetString(blob).Contains(answer, StringComparison.Ordinal), "database stores ciphertext instead of conversation text");
    var rawDatabase = File.ReadAllBytes(Path.Combine(directory, "basic", "omnikit.db"));
    Check(!Encoding.UTF8.GetString(rawDatabase).Contains("Synthetic-private-question-5c8239", StringComparison.Ordinal)
        && !Encoding.UTF8.GetString(rawDatabase).Contains("Synthetic-private-answer-92ba17", StringComparison.Ordinal),
        "neither test question nor answer appears in raw SQLite pages");
    var restored = Store("basic");
    Check(restored.Snapshot(a).SequenceEqual(snapshot) && restored.LastAnswer(a) == answer,
        "DPAPI session survives construction of a replacement store");
    Check(JsonSerializer.Serialize(snapshot[0]).Contains("\"role\"") && JsonSerializer.Serialize(snapshot[0]).Contains("\"content\""),
        "provider-facing turn JSON has lowercase role/content fields");
    Check(new ChatReply("Synthetic", false).Succeeded == false, "reply result explicitly separates success from text");
    store = restored;
    generation = store.Generation;
    store.Snapshot(b);
    Check(store.Generation > generation && store.Snapshot(b).Count == 0
        && !store.Complete(generation, a, question, answer), "scope change clears context and rejects old completion");
    Check(Store("basic").Snapshot(b).Count == 0, "scope replacement persists without resurrecting the previous conversation");

    store = Store("reset"); store.Snapshot(a); generation = store.Generation;
    store.Complete(generation, a, question, answer); store.Reset();
    Check(store.Generation > generation && !store.Complete(generation, a, question, answer), "reset invalidates pending completions");
    using (var connection = Open("reset"))
    {
        using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM OmniKitChatSession";
        Check(Convert.ToInt32(command.ExecuteScalar()) == 0, "reset deletes the persisted encrypted context row");
    }
    Check(Store("reset").Snapshot(a).Count == 0, "cleared context stays empty after restart");

    store = Store("bounded"); store.Snapshot(a); generation = store.Generation;
    for (var index = 0; index < 12; index++) Check(store.Complete(generation, a, "Question " + index, "Answer " + index), "valid bounded pair accepted");
    snapshot = store.Snapshot(a);
    Check(snapshot.Count == 16 && snapshot[0].Content == "Question 4" && snapshot[^1].Content == "Answer 11",
        "only the last eight complete pairs are retained");
    store = Store("characters"); store.Snapshot(a); generation = store.Generation;
    for (var index = 0; index < 5; index++) store.Complete(generation, a, new string((char)('a' + index), 3000), new string('z', 3000));
    snapshot = store.Snapshot(a);
    Check(snapshot.Count == 8 && snapshot.Sum(turn => turn.Content.Length) == 24_000 && snapshot[0].Content[0] == 'b',
        "combined text budget trims whole oldest pairs at exactly 24000 characters");
    foreach (var pair in new[] { (new string('q', 4001), "answer"), ("question", new string('a', 6051)),
        ("bad\ud800", "answer"), ("question", "bad\udc00"), ("NUL\0question", "answer"), (" ", "answer") })
        Check(!store.Complete(generation, a, pair.Item1, pair.Item2), "oversize, invalid Unicode or empty text is not persisted");
    Check(store.Complete(generation, a, "Unicode 😀\nquestion", "Unicode answer 😀\twith tabs"), "valid Unicode and multiline chat remain intact");
    Check(Store("characters").Snapshot(a).All(turn => turn.Role is "user" or "assistant"), "restored pairs retain only allowed roles");

    store = Store("corrupt"); store.Snapshot(a); store.Complete(store.Generation, a, question, answer);
    var corrupt = RandomNumberGenerator.GetBytes(128); WriteCipher("corrupt", corrupt);
    store = Store("corrupt"); Check(store.Snapshot(a).Count == 0, "unreadable DPAPI data fails closed to an empty session");
    Check(store.Complete(store.Generation, a, "Memory-only synthetic question", "Memory-only synthetic answer")
        && store.Snapshot(a).Count == 2 && Cipher("corrupt").SequenceEqual(corrupt),
        "corrupt saved context is preserved while new replies remain in memory");
    store.Reset(); store.Snapshot(a);
    Check(store.Complete(store.Generation, a, question, answer) && Store("corrupt").LastAnswer(a) == answer,
        "explicit reset permits protected persistence again after corrupt data");

    store = Store("future"); store.Snapshot(a); store.Complete(store.Generation, a, question, answer);
    var futureBlob = Cipher("future");
    using (var connection = Open("future"))
    { using var command = connection.CreateCommand(); command.CommandText = "UPDATE OmniKitChatSessionMeta SET SchemaVersion=2"; command.ExecuteNonQuery(); }
    store = Store("future"); store.Snapshot(a); store.Complete(store.Generation, a, "Memory question", "Memory answer");
    Check(Cipher("future").SequenceEqual(futureBlob) && store.Status.Contains("memory", StringComparison.OrdinalIgnoreCase),
        "future metadata is not overwritten by current-code completion");
    store.Reset(); Check(store.Snapshot(a).Count == 0, "explicit reset may clear a future-version session");

    store = Store("invalid-payload"); store.Snapshot(a);
    var entropy = SHA256.HashData(Encoding.UTF8.GetBytes("OmniKit.ChatSession.v1"));
    foreach (var payload in new[]
    {
        "{\"schemaVersion\":2,\"binding\":\"" + a + "\",\"turns\":[]}",
        "{\"schemaVersion\":1,\"binding\":\"" + a + "\",\"turns\":[{\"role\":\"system\",\"content\":\"Synthetic\"}]}",
        "{\"schemaVersion\":1,\"schemaVersion\":1,\"binding\":\"" + a + "\",\"turns\":[]}",
    })
    {
        var plain = Encoding.UTF8.GetBytes(payload); var encrypted = ProtectedData.Protect(plain, entropy, DataProtectionScope.CurrentUser);
        CryptographicOperations.ZeroMemory(plain); WriteCipher("invalid-payload", encrypted);
        var invalid = Store("invalid-payload"); Check(invalid.Snapshot(a).Count == 0 && Cipher("invalid-payload").SequenceEqual(encrypted),
            "invalid decrypted payload is preserved without entering provider context");
        invalid.Reset();
    }
    WriteCipher("invalid-payload", new byte[512 * 1024 + 1]);
    Check(Store("invalid-payload").Snapshot(a).Count == 0, "oversize ciphertext is rejected before decryption");

    store = Store("failed-clear"); store.Snapshot(a); store.Complete(store.Generation, a, question, answer);
    var clearBefore = Cipher("failed-clear");
    using (var connection = Open("failed-clear"))
    {
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TRIGGER RejectSessionClear BEFORE DELETE ON OmniKitChatSession BEGIN SELECT RAISE(ABORT,'Synthetic clear failure'); END;";
        command.ExecuteNonQuery();
    }
    var clearFailed = false;
    try { store.Reset(); } catch (InvalidOperationException exception) { clearFailed = true; Check(!exception.Message.Contains(question), "clear error never includes conversation text"); }
    Check(clearFailed && Cipher("failed-clear").SequenceEqual(clearBefore) && store.LastAnswer(a) == answer
        && store.Status != "Saved local chat session cleared.", "failed reset does not claim success or discard preserved context");

    var staleStore = Store("conflict"); staleStore.Snapshot(a);
    var staleGeneration = staleStore.Generation; staleStore.Complete(staleGeneration, a, question, answer);
    var replacement = Store("conflict"); replacement.Reset();
    Check(!staleStore.Complete(staleGeneration, a, "Synthetic late question", "Synthetic late answer")
        && staleStore.Snapshot(a).Count == 0 && staleStore.LastAnswer(a) == "" && staleStore.Generation > staleGeneration,
        "observed cross-instance reset rejects completion and blocks stale in-memory replay");
    Check(!staleStore.Complete(staleStore.Generation, a, question, answer), "conflicted client cannot continue replay before explicit reset");
    var replacementGeneration = replacement.Generation;
    staleStore.Reset();
    Check(staleStore.Generation > replacementGeneration, "explicit reset advances beyond both local and current disk generation");
    staleStore.Snapshot(a);
    Check(staleStore.Complete(staleStore.Generation, a, question, answer), "explicit reset re-enables a conflicted client safely");

    var replayStore = Store("snapshot-conflict"); replayStore.Snapshot(a);
    replayStore.Complete(replayStore.Generation, a, question, answer);
    var replayGeneration = replayStore.Generation;
    Check(replayStore.CanContinue, "a valid active session permits inference");
    var resetter = Store("snapshot-conflict"); resetter.Reset();
    Check(replayStore.Snapshot(a).Count == 0 && !replayStore.CanContinue && replayStore.Generation > replayGeneration,
        "snapshot detects external reset before any stale provider context can be returned");
    Check(!replayStore.Complete(replayStore.Generation, a, question, answer), "snapshot-conflicted session rejects completion before explicit reset");
    replayStore.Reset(); replayStore.Snapshot(a);
    Check(replayStore.CanContinue, "successful new session restores inference permission");
    using (var connection = Open("snapshot-conflict"))
    { using var command = connection.CreateCommand(); command.CommandText = "UPDATE OmniKitChatSessionMeta SET SchemaVersion=2"; command.ExecuteNonQuery(); }
    Check(replayStore.Snapshot(a).Count == 0 && !replayStore.CanContinue,
        "snapshot blocks replay immediately after an external unsupported schema change");

    store = Store("write-failure"); store.Snapshot(a);
    using (var connection = Open("write-failure"))
    {
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TRIGGER RejectSessionWrite BEFORE UPDATE ON OmniKitChatSession BEGIN SELECT RAISE(ABORT,'Synthetic write failure'); END; PRAGMA user_version=14; CREATE TABLE OmniKitSettings(Id INTEGER PRIMARY KEY,Json TEXT); INSERT INTO OmniKitSettings VALUES(1,'synthetic settings');";
        command.ExecuteNonQuery();
    }
    Check(store.Complete(store.Generation, a, question, answer) && store.LastAnswer(a) == answer
        && store.Status.Contains("memory", StringComparison.OrdinalIgnoreCase), "persistence failure retains the current successful reply in memory");
    using (var connection = Open("write-failure"))
    {
        using var command = connection.CreateCommand(); command.CommandText = "PRAGMA user_version";
        Check(Convert.ToInt32(command.ExecuteScalar()) == 14, "chat tables never change settings schema version");
        command.CommandText = "SELECT Json FROM OmniKitSettings WHERE Id=1";
        Check((string)command.ExecuteScalar()! == "synthetic settings", "chat persistence leaves settings content untouched");
    }
    var blockedPath = Path.Combine(directory, "unavailable"); File.WriteAllText(blockedPath, "synthetic path blocker");
    store = new(new DirectoryInfo(blockedPath)); store.Snapshot(a);
    Check(store.Complete(store.Generation, a, question, answer) && store.LastAnswer(a) == answer,
        "unavailable persistence still supports a bounded in-memory session");
    Console.WriteLine($"PASS: {checks} protected chat-session checks; temporary synthetic data and current-user DPAPI only.");
}
finally
{
    var resolved = Path.GetFullPath(directory);
    var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
        || !Path.GetFileName(resolved).StartsWith("OmniKit-chat-session-synthetic-", StringComparison.Ordinal))
        throw new InvalidOperationException("Refusing cleanup outside the named synthetic test directory.");
    Directory.Delete(resolved, true);
}
