using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OmniKit.Assistant;

internal sealed record ChatGptCallback(string Code, string ClientId);

internal static class ChatGptLoopback
{
    internal static async Task<ChatGptCallback> ReceiveAsync(TcpListener listener, Uri callback, string state, string? issuedClientId, CancellationToken ct)
    {
        using var socket = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
        if (socket.Client.RemoteEndPoint is not IPEndPoint remote || !IPAddress.IsLoopback(remote.Address)) throw Invalid();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var stream = socket.GetStream();
        var bytes = new byte[16384];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(count, bytes.Length - count), timeout.Token).ConfigureAwait(false);
            if (read == 0) throw Invalid();
            count += read;
            if (count >= 4 && Encoding.ASCII.GetString(bytes, 0, count).Contains("\r\n\r\n", StringComparison.Ordinal)) break;
        }
        ChatGptCallback result;
        try
        {
            if (bytes.AsSpan(0, count).ContainsAnyInRange((byte)128, byte.MaxValue)) throw Invalid();
            result = Parse(Encoding.ASCII.GetString(bytes, 0, count), callback, state, issuedClientId);
        }
        catch
        {
            await Reply(stream, false, timeout.Token).ConfigureAwait(false);
            throw Invalid();
        }
        await Reply(stream, true, timeout.Token).ConfigureAwait(false);
        return result;
    }

    internal static ChatGptCallback Parse(string request, Uri callback, string state, string? issuedClientId)
    {
        if (request.Length is 0 or > 16384 || !request.EndsWith("\r\n\r\n", StringComparison.Ordinal)) throw Invalid();
        var lines = request.Split("\r\n", StringSplitOptions.None);
        var first = lines[0].Split(' ');
        if (first.Length != 3 || first[0] != "GET" || first[2] is not ("HTTP/1.1" or "HTTP/1.0") ||
            !first[1].StartsWith("/auth/callback?", StringComparison.Ordinal) || first[1].Contains('#')) throw Invalid();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            if (line.Length == 0) continue;
            var split = line.IndexOf(':');
            if (split <= 0 || char.IsWhiteSpace(line[0]) || !headers.TryAdd(line[..split], line[(split + 1)..].Trim())) throw Invalid();
        }
        if (!headers.TryGetValue("Host", out var host) || host != callback.Authority ||
            headers.ContainsKey("Transfer-Encoding") || headers.TryGetValue("Content-Length", out var length) && length != "0") throw Invalid();
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in first[1][("/auth/callback?".Length)..].Split('&'))
        {
            var split = pair.IndexOf('=');
            if (split <= 0 || pair.Length > 8192) throw Invalid();
            var key = Decode(pair[..split]); var value = Decode(pair[(split + 1)..]);
            if (!query.TryAdd(key, value)) throw Invalid();
        }
        if (!query.TryGetValue("state", out var actualState) || !ChatGptTokenValidator.Equal(actualState, state)) throw Invalid();
        if (query.ContainsKey("error")) throw Invalid();
        if (query.TryGetValue("iss", out var issuer) && issuer != ChatGptTokenValidator.Issuer) throw Invalid();
        if (!query.TryGetValue("code", out var code) || code.Length is 0 or > 4096 || code.Any(char.IsControl)) throw Invalid();
        var client = query.GetValueOrDefault("client_id", issuedClientId ?? "");
        if (!ChatGptTokenValidator.ValidClientId(client) || issuedClientId is not null && client != issuedClientId) throw Invalid();
        return new(code, client);
    }

    private static string Decode(string value)
    {
        for (var i = 0; i < value.Length; i++)
            if (value[i] == '%' && (i + 2 >= value.Length || !Uri.IsHexDigit(value[++i]) || !Uri.IsHexDigit(value[++i]))) throw Invalid();
        return Uri.UnescapeDataString(value.Replace('+', ' '));
    }
    private static async Task Reply(NetworkStream stream, bool accepted, CancellationToken ct)
    {
        var body = accepted ? "OmniKit received the sign-in response. Return to the game settings to check the result." : "The sign-in response was not accepted. Return to OmniKit and start again.";
        var response = $"HTTP/1.1 {(accepted ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'none'\r\nConnection: close\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(response), ct).ConfigureAwait(false);
    }
    private static InvalidOperationException Invalid() => new("The ChatGPT sign-in callback was not accepted.");
}
