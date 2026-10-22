using System.Security.Cryptography;
using System.Text;

namespace OmniKit.Assistant;

/// <summary>Endpoint-bound credentials encrypted for the current Windows user.</summary>
internal sealed class CredentialStore(DirectoryInfo configDirectory)
{
    private const int MaximumKeyLength = 4096;
    private const int MaximumProtectedLength = 65536;
    private readonly string directory = Path.Combine(configDirectory.FullName, "credentials");
    private readonly object gate = new();

    public bool Exists(Uri endpoint)
    {
        lock (gate)
        {
            var file = new FileInfo(GetPath(endpoint));
            try { return file.Exists && file.Length is > 0 and <= MaximumProtectedLength; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

    public void Save(Uri endpoint, string key)
    {
        key = key.Trim();
        if (key.Length is 0 or > MaximumKeyLength || key.Any(c => c <= ' ' || c > '~'))
            throw new InvalidOperationException("Enter a valid API key without spaces or line breaks.");

        byte[] plaintext = Encoding.UTF8.GetBytes(key);
        string? temporaryFile = null;
        lock (gate)
        {
            try
            {
                var ciphertext = ProtectedData.Protect(plaintext, Entropy(endpoint), DataProtectionScope.CurrentUser);
                Directory.CreateDirectory(directory);
                temporaryFile = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
                File.WriteAllBytes(temporaryFile, ciphertext);
                File.Move(temporaryFile, GetPath(endpoint), true);
            }
            catch (Exception ex) when (IsStorageError(ex))
            {
                throw new InvalidOperationException("The API key could not be saved to protected Windows storage.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
                if (temporaryFile is not null)
                {
                    try { File.Delete(temporaryFile); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }

    public string? Read(Uri endpoint)
    {
        lock (gate)
        {
            byte[]? plaintext = null;
            try
            {
                var path = GetPath(endpoint);
                if (!File.Exists(path)) return null;
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (input.Length is <= 0 or > MaximumProtectedLength)
                    throw new CryptographicException();
                byte[] ciphertext = new byte[(int)input.Length];
                input.ReadExactly(ciphertext);
                plaintext = ProtectedData.Unprotect(ciphertext, Entropy(endpoint), DataProtectionScope.CurrentUser);
                var key = Encoding.UTF8.GetString(plaintext);
                if (key.Length is 0 or > MaximumKeyLength || key.Any(c => c <= ' ' || c > '~'))
                    throw new CryptographicException();
                return key;
            }
            catch (Exception ex) when (IsStorageError(ex))
            {
                throw new InvalidOperationException("The saved API key cannot be read. Save it again in settings.");
            }
            finally
            {
                if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    public void Delete(Uri endpoint)
    {
        lock (gate)
        {
            try { File.Delete(GetPath(endpoint)); }
            catch (Exception ex) when (IsStorageError(ex))
            {
                throw new InvalidOperationException("The saved API key could not be removed from protected storage.");
            }
        }
    }

    private string GetPath(Uri endpoint) =>
        Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint.AbsoluteUri))) + ".dpapi");

    private static byte[] Entropy(Uri endpoint) =>
        SHA256.HashData(Encoding.UTF8.GetBytes("OmniKit.Provider.ApiKey.v1|" + endpoint.AbsoluteUri));

    private static bool IsStorageError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or CryptographicException or
            PlatformNotSupportedException or System.Security.SecurityException;
}
