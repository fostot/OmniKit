using Dalamud.Interface;
using Dalamud.Plugin;
using Umbra.Common;
using Umbra.Widgets;

namespace OmniKit.Umbra;

[ToolbarWidget(
    "OmniKit.Launcher",
    "OmniKit",
    "Open OmniKit's launcher and settings from your Umbra toolbar.",
    ["omnikit", "search", "launcher", "interact", "moonblob", "companion", "moon"])]
public sealed class OmniKitWidget(
    WidgetInfo info,
    string? guid = null,
    Dictionary<string, object>? configValues = null)
    : StandardToolbarWidget(info, guid, configValues)
{
    private readonly MenuPopup.Header status = new("OmniKit");
    private readonly MenuPopup.Button search = new("Search & launch")
    {
        Icon = FontAwesomeIcon.Search,
    };
    private readonly MenuPopup.Button settings = new("Settings")
    {
        Icon = FontAwesomeIcon.Cog,
    };
    private long nextStatusUpdate;

    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Icon |
        StandardWidgetFeatures.Text |
        StandardWidgetFeatures.CustomizableIcon;

    protected override string DefaultIconType => IconTypeFontAwesome;
    protected override FontAwesomeIcon DefaultFontAwesomeIcon => FontAwesomeIcon.Moon;

    public override MenuPopup Popup { get; } = new();

    protected override void OnLoad()
    {
        SetText("OmniKit");
        search.OnClick = () => OpenPlugin(false);
        settings.OnClick = () => OpenPlugin(true);
        Popup.Add(status);
        Popup.Add(search);
        Popup.Add(settings);
        Popup.OnPopupOpen += RefreshStatus;
        RefreshStatus();
    }

    protected override void OnDraw()
    {
        if (Environment.TickCount64 < nextStatusUpdate) return;
        nextStatusUpdate = Environment.TickCount64 + 1000;
        RefreshStatus();
    }

    protected override void OnUnload()
    {
        Popup.OnPopupOpen -= RefreshStatus;
        search.OnClick = null;
        settings.OnClick = null;
    }

    private static IExposedPlugin? FindPlugin() =>
        Framework.DalamudPlugin.InstalledPlugins.FirstOrDefault(
            plugin => plugin.InternalName == "OmniKit" && plugin.IsLoaded);

    private void RefreshStatus()
    {
        var plugin = FindPlugin();
        search.IsDisabled = plugin?.HasMainUi != true;
        settings.IsDisabled = plugin?.HasConfigUi != true;
        status.Label = plugin is null ? "OmniKit is not loaded" : "OmniKit";
        Node.Tooltip = plugin is null
            ? "Enable OmniKit in Dalamud's Plugin Installer to use this widget."
            : "Open OmniKit search or configure camera interaction.";
    }

    private void OpenPlugin(bool configuration)
    {
        // Resolve at click time as Dalamud plugins can be unloaded independently.
        Framework.DalamudFramework.Run(() =>
        {
            var plugin = FindPlugin();
            if (configuration && plugin?.HasConfigUi == true)
                plugin.OpenConfigUi();
            else if (!configuration && plugin?.HasMainUi == true)
                plugin.OpenMainUi();
        });
    }
}
