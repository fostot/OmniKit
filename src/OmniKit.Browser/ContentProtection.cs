namespace OmniKit.Browser;

/// <summary>Small local hostname policy. No page content, cookies or browsing history are collected.</summary>
internal static class ContentProtection
{
    private static readonly HashSet<string> BlockedHosts = LoadHosts();
    internal static int RuleCount => BlockedHosts.Count;

    internal static bool ShouldBlock(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return false;
        var host = uri.IdnHost.TrimEnd('.');
        while (host.Length > 0)
        {
            if (BlockedHosts.Contains(host)) return true;
            var separator = host.IndexOf('.');
            if (separator < 0) break;
            host = host[(separator + 1)..];
        }
        return false;
    }

    private static HashSet<string> LoadHosts()
    {
        using var stream = typeof(ContentProtection).Assembly.GetManifestResourceStream("OmniKit.Browser.blocked-hosts.txt")
            ?? throw new InvalidOperationException("Browser protection rules are missing.");
        using var reader = new StreamReader(stream);
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            var host = line.Trim();
            if (host.Length == 0 || host.StartsWith('#')) continue;
            if (host.Length > 253 || host.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-')) || !host.Contains('.'))
                throw new InvalidOperationException("Browser protection rules are invalid.");
            hosts.Add(host);
        }
        return hosts;
    }

    // Document-start, including child frames. Ordinary anchor links remain native
    // navigation; script-created popup windows are disabled while protection is on.
    internal const string PopupScript = """
        (() => {
          try { Object.defineProperty(window, 'open', { value: () => null, writable: false, configurable: false }); }
          catch (_) { window.open = () => null; }
        })();
        """;
}
