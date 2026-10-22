using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OmniKit.Assistant;

internal sealed record ChatGptIdentity(string Subject, string Email);

/// <summary>Validates only OpenAI-issued RS256 ID tokens against a separately fetched, pinned JWKS.</summary>
internal static class ChatGptTokenValidator
{
    internal const string Issuer = "https://auth.openai.com";

    internal static ChatGptIdentity Validate(string token, string jwks, string clientId, string? nonce, DateTimeOffset now)
    {
        if (token.Length > 65536 || jwks.Length > 262144 || !ValidClientId(clientId)) throw Invalid();
        var parts = token.Split('.');
        if (parts.Length != 3) throw Invalid();
        using var header = JsonDocument.Parse(Decode(parts[0]));
        using var body = JsonDocument.Parse(Decode(parts[1]));
        using var keys = JsonDocument.Parse(jwks);
        RejectDuplicates(header.RootElement); RejectDuplicates(body.RootElement); RejectDuplicates(keys.RootElement);
        if (Text(header.RootElement, "alg") != "RS256" || header.RootElement.TryGetProperty("jku", out _) ||
            header.RootElement.TryGetProperty("x5u", out _) || header.RootElement.TryGetProperty("crit", out _) ||
            header.RootElement.TryGetProperty("b64", out _)) throw Invalid();
        var kid = Text(header.RootElement, "kid");
        if (kid.Length is 0 or > 256 || !keys.RootElement.TryGetProperty("keys", out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 64) throw Invalid();
        var matches = array.EnumerateArray().Where(key => Text(key, "kid") == kid && Text(key, "kty") == "RSA").ToArray();
        if (matches.Length != 1) throw Invalid();
        var key = matches[0];
        if (key.TryGetProperty("use", out var use) && use.GetString() != "sig") throw Invalid();
        if (key.TryGetProperty("alg", out var alg) && alg.GetString() != "RS256") throw Invalid();
        var modulus = Decode(Text(key, "n")); var exponent = Decode(Text(key, "e"));
        if (modulus.Length is < 256 or > 1024 || exponent.Length is < 1 or > 8) throw Invalid();
        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });
        if (!rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw Invalid();
        var claims = body.RootElement;
        if (Text(claims, "iss") != Issuer || !claims.TryGetProperty("aud", out var audience)) throw Invalid();
        var audiences = audience.ValueKind == JsonValueKind.String ? [audience.GetString()!] :
            audience.ValueKind == JsonValueKind.Array ? audience.EnumerateArray().Select(value => value.GetString() ?? "").ToArray() : [];
        if (!audiences.Contains(clientId, StringComparer.Ordinal) || (audiences.Length > 1 && Text(claims, "azp") != clientId)) throw Invalid();
        if (claims.TryGetProperty("azp", out var party) && party.GetString() != clientId) throw Invalid();
        var time = now.ToUnixTimeSeconds();
        if (!claims.TryGetProperty("exp", out var expiry) || !expiry.TryGetInt64(out var expires) || expires <= time) throw Invalid();
        foreach (var field in new[] { "nbf", "iat" })
            if (claims.TryGetProperty(field, out var value) && (!value.TryGetInt64(out var at) || at > time + 60)) throw Invalid();
        if (nonce is not null && !Equal(Text(claims, "nonce"), nonce)) throw Invalid();
        var subject = Text(claims, "sub");
        if (subject.Length is 0 or > 512 || subject.Any(char.IsControl)) throw Invalid();
        var email = Text(claims, "email");
        if (email.Length > 254 || email.Any(char.IsControl)) email = "";
        return new ChatGptIdentity(subject, email);
    }

    internal static bool ValidClientId(string value) => value is not "dynamic_agent_client" && value.Length is > 0 and <= 256 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    internal static bool Equal(string a, string b) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    internal static string Encode(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    internal static byte[] Decode(string text)
    {
        if (text.Length is 0 or > 65536 || text.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))) throw Invalid();
        return Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/') + new string('=', (4 - text.Length % 4) % 4));
    }
    internal static string Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString()! : "";
    internal static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in value.EnumerateObject()) { if (!names.Add(field.Name)) throw Invalid(); RejectDuplicates(field.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicates(item);
    }
    private static InvalidOperationException Invalid() => new("ChatGPT identity verification failed. Start a fresh sign-in.");
}
