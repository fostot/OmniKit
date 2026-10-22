using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using OmniKit.Assistant;
using OmniKit.Integration;
using OmniKit.Interaction;
using OmniKit.Storage;
using OmniKit.Ui;

namespace OmniKit;

public sealed class Plugin : IDalamudPlugin
{
    public Configuration Configuration { get; }
    private readonly LocalStore store;
    private readonly SourceRegistry sources;
    private readonly SoftInteractionService interaction;
    private readonly ChatService chat;
    private readonly VoiceInput voice;
    private readonly PluginUi ui;
    private readonly ShortcutInput shortcuts = new();
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
        store = new LocalStore(pluginInterface.ConfigDirectory.FullName);
        Configuration = store.Load() ?? pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Persist = store.Save;
        Configuration.Save();
        sources = new SourceRegistry();
        interaction = new SoftInteractionService(Configuration);
        chat = new ChatService(Configuration);
        voice = new VoiceInput();
        ui = new PluginUi(Configuration, interaction, chat, voice, store, sources);
        Services.Commands.AddHandler("/omnikit", new CommandInfo(OnCommand)
        { HelpMessage = "Open OmniKit. Use /omnikit settings to configure it." });
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenMain;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        Services.Framework.Update += Update;
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("settings", StringComparison.OrdinalIgnoreCase)) OpenSettings();
        else OpenMain();
    }
    private void OpenMain() => ui.OpenMain();
    private void OpenSettings() => ui.OpenSettings();
    private void Update(IFramework framework)
    {
        if (disposed) return;
        shortcuts.Update(Configuration, ui.CapturesText, ui.SearchHasFocus, ui.ToggleMain, OpenSettings);
        ui.Update();
        interaction.Update(ui.IsOpen);
    }
    private void Draw()
    {
        if (disposed) return;
        ui.Draw();
        interaction.DrawHud();
    }
    public void Dispose()
    {
        disposed = true;
        Services.Framework.Update -= Update;
        var builder = Services.PluginInterface.UiBuilder;
        builder.Draw -= Draw;
        builder.OpenMainUi -= OpenMain;
        builder.OpenConfigUi -= OpenSettings;
        Services.Commands.RemoveHandler("/omnikit");
        ui.Dispose(); voice.Dispose(); chat.Dispose();
        interaction.Dispose(); sources.Dispose();
    }
}
