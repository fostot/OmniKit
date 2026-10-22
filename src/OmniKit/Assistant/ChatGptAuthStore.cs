using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OmniKit.Assistant;

internal sealed class ChatGptAuthState
{
    public string HostId { get; set; } = "";
    public string ActiveId { get; set; } = "";
    public List<ChatGptRegistration> Accounts { get; set; } = [];
}

internal sealed class ChatGptRegistration
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ClientId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Email { get; set; } = "";
    public string Label { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string IdToken { get; set; } = "";
    public string Scope { get; set; } = "";
    public long ExpiresAt { get; set; }
    public bool CanUsePlan => Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("chatgpt.tokens.use.direct", StringComparer.Ordinal);
    public void ClearTokens() { AccessToken = ""; RefreshToken = ""; IdToken = ""; Scope = ""; ExpiresAt = 0; }
}

/// <summary>Only DPAPI ciphertext is persisted. An exclusive lock serializes rotating refreshes across processes.</summary>
internal sealed class ChatGptAuthStore(DirectoryInfo configDirectory)
{
    private const int MaximumBytes = 1048576;
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes("OmniKit.ChatGPT.OAuth.v1"));
    private readonly string directory = Path.Combine(configDirectory.FullName, "credentials");
    private string FilePath => Path.Combine(directory, "chatgpt.dpapi");

    internal async Task<FileStream> LockAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try { return TryLock(); }
            catch (IOException) { await Task.Delay(80, ct).ConfigureAwait(false); }
        }
    }
    internal FileStream TryLock()
    {
        Directory.CreateDirectory(directory);
        return new FileStream(Path.Combine(directory, "chatgpt.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    internal ChatGptAuthState Read()
    {
        if (!File.Exists(FilePath)) return new();
        byte[]? plaintext = null;
        try
        {
            using var input = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length is <= 0 or > MaximumBytes) throw new CryptographicException();
            var encrypted = new byte[(int)input.Length]; input.ReadExactly(encrypted);
            plaintext = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            var value = JsonSerializer.Deserialize<ChatGptAuthState>(plaintext) ?? throw new CryptographicException();
            if (!ValidHost(value.HostId) || value.Accounts is null || value.Accounts.Count > 32 ||
                value.Accounts.Any(account => !ChatGptTokenValidator.ValidClientId(account.ClientId) || account.Subject.Length is 0 or > 512 ||
                    !Guid.TryParseExact(account.Id, "N", out _) || account.Label.Length > 300 || account.Label.Any(char.IsControl) ||
                    account.AccessToken.Length > 65536 || account.RefreshToken.Length > 65536 || account.IdToken.Length > 65536 || account.Scope.Length > 4096) ||
                value.Accounts.Select(account => account.Id).Distinct().Count() != value.Accounts.Count) throw new CryptographicException();
            return value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException or NullReferenceException)
        { throw new InvalidOperationException("Saved ChatGPT credentials could not be read from protected Windows storage."); }
        finally { if (plaintext != null) CryptographicOperations.ZeroMemory(plaintext); }
    }
    internal void Save(ChatGptAuthState value)
    {
        if (!ValidHost(value.HostId) || value.Accounts.Count > 32) throw new InvalidOperationException("Invalid ChatGPT registration storage.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        var temporary = Path.Combine(directory, ".chatgpt-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            if (bytes.Length > MaximumBytes - 4096) throw new InvalidOperationException("Too many saved ChatGPT registrations.");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(temporary, ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser));
            File.Move(temporary, FilePath, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    internal static bool ValidHost(string value) => value.StartsWith("urn:uuid:", StringComparison.Ordinal) && Guid.TryParseExact(value[9..], "D", out _);
}
