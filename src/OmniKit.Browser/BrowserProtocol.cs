using System.Text;
using System.Text.Json;

namespace OmniKit.Browser;

internal sealed record BrowserCommand(string Kind, string? Url = null);

internal static class BrowserProtocol
{
    internal const int MaximumLineLength = 8192;
    private static readonly object OutputGate = new();

    internal static bool TryUrl(string? value, out string url)
    {
        url = "";
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        url = uri.AbsoluteUri;
        return true;
    }

    internal static BrowserCommand? Parse(string line)
    {
        try
        {
            using var json = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 3 });
            if (json.RootElement.ValueKind != JsonValueKind.Object) return null;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in json.RootElement.EnumerateObject())
                if (field.Name is not ("kind" or "url") || !keys.Add(field.Name) || field.Value.ValueKind != JsonValueKind.String) return null;
            if (!json.RootElement.TryGetProperty("kind", out var kindNode)) return null;
            var kind = kindNode.GetString();
            if (kind == "navigate")
                return keys.Count == 2 && json.RootElement.TryGetProperty("url", out var urlNode) && TryUrl(urlNode.GetString(), out var url)
                    ? new BrowserCommand(kind, url) : null;
            return keys.Count == 1 && kind is "toggle-controls" or "show" or "hide" or "shutdown" ? new BrowserCommand(kind) : null;
        }
        catch (JsonException) { return null; }
    }

    internal static bool AllowSubframe(string value)
    {
        if (TryUrl(value, out _)) return true;
        if (value.Length > 4096 || value.Any(char.IsControl)) return false;
        return value is "about:blank" or "about:srcdoc" || value.StartsWith("about:blank#", StringComparison.Ordinal) ||
            value.StartsWith("blob:", StringComparison.Ordinal) && TryUrl(value[5..], out _);
    }

    internal static async Task ReadAsync(TextReader input, Func<BrowserCommand, Task> dispatch, CancellationToken cancellation)
    {
        var buffer = new char[512];
        var line = new StringBuilder();
        var oversized = false;
        while (!cancellation.IsCancellationRequested)
        {
            var count = await input.ReadAsync(buffer.AsMemory(), cancellation);
            if (count == 0) break;
            for (var index = 0; index < count; index++)
            {
                var character = buffer[index];
                if (character == '\n')
                {
                    var command = oversized ? null : Parse(line.ToString().TrimEnd('\r'));
                    line.Clear(); oversized = false;
                    if (command != null) await dispatch(command);
                    else Emit("error", "The browser command was rejected.");
                }
                else if (line.Length < MaximumLineLength && !oversized) line.Append(character);
                else { oversized = true; line.Clear(); }
            }
        }
    }

    // Callers supply fixed diagnostic strings only. Never emit browsing data.
    internal static void Emit(string kind, string message, long? window = null)
    {
        lock (OutputGate)
        {
            try
            {
                var json = window.HasValue ? JsonSerializer.Serialize(new { kind, message, window = window.Value })
                    : JsonSerializer.Serialize(new { kind, message });
                Console.Out.WriteLine(json);
                Console.Out.Flush();
            }
            catch (IOException) { }
        }
    }
}
