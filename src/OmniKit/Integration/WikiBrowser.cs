using Dalamud.Utility;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace OmniKit.Integration;

/// <summary>Optional navigation of a user-created Browsingway overlay through its public command.</summary>
internal sealed class WikiBrowser(Configuration config)
{
    private const string SearchUrl = "https://ffxiv.consolegameswiki.com/mediawiki/index.php?search=";
    private const string BrowserCommand = "/bw";

    public string Status { get; private set; } = "Wiki links use your chosen browser mode.";

    public bool IsAvailable => Services.PluginInterface.InstalledPlugins
        .Any(plugin => plugin.InternalName == "Browsingway" && plugin.IsLoaded)
        && Services.Commands.Commands.ContainsKey(BrowserCommand);

    public void Open(string query)
    {
        var search = query?.Trim() ?? string.Empty;
        if (search.Length is < 1 or > 500 || search.Any(char.IsControl))
        {
            Status = "Enter a wiki search from 1 to 500 characters.";
            return;
        }

        // The user supplies a search term, never a destination or command fragment.
        var url = SearchUrl + Uri.EscapeDataString(search);
        if (!config.WikiInGame)
        {
            try
            {
                Util.OpenLink(url);
                Status = "Wiki search requested in your system browser.";
            }
            catch (Exception)
            {
                Status = "The system browser could not be opened.";
            }
            return;
        }

        Status = "Preparing the in-game wiki navigation request…";
        _ = Services.Framework.RunOnFrameworkThread(() => SendInGame(url));
    }

    private unsafe void SendInGame(string url)
    {
        try
        {
            if (!config.WikiInGame)
            {
                Status = "Browser mode changed. Select the wiki result again.";
                return;
            }
            var framework = NativeFramework.Instance();
            if (framework == null || framework->WindowInactive || framework->IsExiting)
            {
                Status = "Focus the game before opening the in-game wiki.";
                return;
            }
            if (!IsAvailable)
            {
                Status = "Load Browsingway and create an overlay, or choose System browser in OmniKit settings.";
                return;
            }

            var name = config.WikiOverlayName ?? string.Empty;
            if (name.Length is < 1 or > 40 || !name.All(character => character is >= 'a' and <= 'z'
                    or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_'))
            {
                Status = "Use a Browsingway command name of 1–40 letters, digits, hyphens, or underscores.";
                return;
            }

            // Only the registered plugin command is dispatched; no native chat parser.
            // Browsingway lowercases its displayed Command Name. Its handler requires
            // an existing overlay and saves the requested URL; we change no other settings.
            var dispatched = Services.Commands.ProcessCommand($"{BrowserCommand} overlay {name.ToLowerInvariant()} url {url}");
            Status = dispatched
                ? "Navigation request sent to Browsingway. If nothing appears, check its named overlay and game chat."
                : "Browsingway's command is unavailable. Load it or choose System browser in settings.";
            // ProcessCommand only confirms dispatch. Browsingway exposes no acknowledgement
            // here for a missing overlay, visibility, successful page load, or render failure.
        }
        catch (Exception)
        {
            Status = "Browsingway navigation could not be requested. Check its overlay settings.";
        }
    }
}
