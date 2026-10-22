using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace OmniKit.Search;

internal sealed class ActionCatalog
{
    private readonly Action openSettings;
    private readonly List<SearchEntry> gameEntries = [];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Free Company"] = "guild clan fc community social friends membership",
        ["Linkshells"] = "community guild chat groups social ls",
        ["Cross-world Linkshell"] = "community guild chat groups social cwls crossworld",
        ["Fellowship"] = "community guild groups social friends",
        ["Fellowship Finder"] = "community guild groups recruitment friends",
        ["Party Finder"] = "group groups recruitment raid dungeon party pf lfg",
        ["Duty Finder"] = "dungeon raid trial queue roulette duty df matchmaking",
        ["Inventory"] = "bags bag backpack items storage",
        ["Armoury Chest"] = "armor armour equipment gear weapons chest storage",
        ["Character"] = "gear equipment stats attributes profile character sheet",
        ["Character Configuration"] = "settings options preferences controls keybindings",
        ["System Configuration"] = "settings options preferences graphics audio sound video display",
        ["Crafting Log"] = "craft crafting recipe recipes culinarian cooking food",
        ["Gathering Log"] = "gather gathering harvest mining botany resources",
        ["Journal"] = "quest quests log journal mission missions story",
        ["Map"] = "map maps directions location zone travel",
        ["Teleport"] = "teleport travel aetheryte warp tp",
        ["Mount Guide"] = "mount mounts ride chocobo collection",
        ["Minion Guide"] = "pet pets minion minions companion collection",
        ["Actions & Traits"] = "skills spells abilities actions traits",
        ["Achievements"] = "achievement achievements rewards progress completion",
        ["Friend List"] = "friends friend social community",
    };

    public ActionCatalog(Action openSettings)
    {
        this.openSettings = openSettings;
        var english = Services.Data.GetExcelSheet<MainCommand>(Dalamud.Game.ClientLanguage.English)
            .ToDictionary(row => row.RowId, row => row.Name.ToString());
        foreach (var row in Services.Data.GetExcelSheet<MainCommand>())
        {
            var name = row.Name.ToString().Trim();
            if (row.Icon == 0 || name.Length == 0) continue;
            var englishName = english.GetValueOrDefault(row.RowId, name);
            // Destructive/session actions stay out of the initial launcher catalogue.
            if (englishName.Contains("Log Out", StringComparison.OrdinalIgnoreCase) ||
                englishName.Contains("Exit Game", StringComparison.OrdinalIgnoreCase)) continue;
            var id = row.RowId;
            var keywords = englishName + " " + string.Join(' ', Aliases.Where(pair => englishName.Contains(pair.Key, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value));
            gameEntries.Add(new SearchEntry($"game:{id}", name, "Open the game window", keywords,
                "FFXIV", () => ExecuteMainCommand(id), (uint)row.Icon));
        }
    }

    public IEnumerable<SearchEntry> GetEntries()
    {
        yield return new("community:fc", "Find a Free Company", "Recruiting groups on Community Finder", "community guild clan free company fc recruitment social friends",
            "The Lodestone", () => Util.OpenLink("https://na.finalfantasyxiv.com/lodestone/community_finder/?type=fc"));
        yield return new("community:ls", "Find a Linkshell", "Linkshell and cross-world linkshell recruitment", "community guild linkshell cwls ls crossworld social friends chat groups recruitment",
            "The Lodestone", () => Util.OpenLink("https://na.finalfantasyxiv.com/lodestone/community_finder/?type=ls"));
        yield return new("omnikit:settings", "OmniKit Settings", "Shortcuts, interaction, avatar and assistant", "settings options preferences controls bimble avatar voice ai interact interaction", "OmniKit", openSettings);
        foreach (var entry in gameEntries) yield return entry;
        foreach (var plugin in Services.PluginInterface.InstalledPlugins.Where(plugin => plugin.IsLoaded))
        {
            if (plugin.InternalName == "OmniKit") continue;
            var name = plugin.Name;
            if (plugin.HasMainUi)
                yield return new($"plugin:{plugin.InternalName}", name, "Open plugin", $"plugin addon {name}", "Dalamud", () => plugin.OpenMainUi());
            if (plugin.HasConfigUi)
                yield return new($"plugin-settings:{plugin.InternalName}", name + " Settings", "Configure plugin", $"plugin addon settings options {name}", "Dalamud", () => plugin.OpenConfigUi());
        }
    }

    private static unsafe void ExecuteMainCommand(uint id)
    {
        _ = Services.Framework.RunOnFrameworkThread(() =>
        {
            if (!Ui.ShortcutInput.GameAcceptsInput()) return;
            var ui = UIModule.Instance();
            if (ui != null) ui->ExecuteMainCommand(id);
        });
    }
}
