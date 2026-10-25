using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using OmniKit.Assistant;
using OmniKit.Automation;
using OmniKit.Unlocks;
using OmniKit.Companions;
using OmniKit.Integration;
using OmniKit.Interaction;
using OmniKit.Jobs;
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
    private readonly QuestActionHud actionHud;
    private readonly WebLinkInterceptor webLinks;
    private readonly JobSwitchService jobSwitches;
    private readonly CharacterWindowJobSwitcher characterJobSwitcher;
    private readonly NomNomService nomNom;
    private readonly AetheryteUnlockService unlocks;
    private readonly SayingLibrary? sayings;
    private bool requestedWebCapture;
    private readonly ShortcutInput shortcuts = new();
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
        store = new LocalStore(pluginInterface.ConfigDirectory.FullName);
        Configuration = store.Load() ?? store.Upgrade(pluginInterface.GetPluginConfig() as Configuration ?? new Configuration());
        Configuration.Persist = store.Save;
        Configuration.Save();
        try
        {
            sayings = new SayingLibrary(pluginInterface.ConfigDirectory.FullName, Path.Combine(pluginInterface.AssemblyLocation.DirectoryName!, "assets", "companion-sayings.json"));
        }
        catch (Exception exception)
        {
            Services.Log.Warning(exception, "The local sayings library could not be opened; existing data was preserved.");
        }
        sources = new SourceRegistry();
        interaction = new SoftInteractionService(Configuration);
        jobSwitches = new JobSwitchService(Configuration);
        jobSwitches.Feedback = (message, isError) =>
        {
            if (isError) Services.Chat.PrintError("[OmniKit] " + message);
            else Services.Chat.Print("[OmniKit] " + message);
        };
        characterJobSwitcher = new CharacterWindowJobSwitcher(Configuration, jobSwitches);
        nomNom = new NomNomService(Configuration);
        unlocks = new AetheryteUnlockService();
        jobSwitches.ExternalBusy = () => nomNom.Busy || unlocks.Busy;
        chat = new ChatService(Configuration, WebLinkInterceptor.Bypass);
        voice = new VoiceInput();
        ui = new PluginUi(Configuration, interaction, chat, voice, store, sources);
        ui.SayingsLibrary = sayings;
        ui.JobSwitchStatus = () => jobSwitches.Busy ? jobSwitches.Status : characterJobSwitcher.Status + " " + jobSwitches.Status;
        ui.JobSwitcherWarning = () => characterJobSwitcher.ConflictWarning;
        ui.ConfirmOtherSwitchersDisabled = characterJobSwitcher.ConfirmOtherSwitchersDisabled;
        ui.OpenJobSwitcherConflictConfiguration = characterJobSwitcher.OpenConflictConfiguration;
        ui.NomNomStatus = () => nomNom.Status;
        ui.NomNomFoods = nomNom.GetFoodOptions;
        ui.NomNomAutomaticFoodId = () => nomNom.AutomaticFoodId;
        ui.RefreshNomNomFoods = nomNom.RefreshFoods;
        ui.Aetherytes = unlocks.GetOptions;
        ui.AetheryteUnlockStatus = () => unlocks.Status;
        ui.AetheryteUnlockBusy = () => unlocks.Busy;
        ui.AetheryteUnlockProgress = () => unlocks.Progress;
        ui.AetheryteCurrentTerritory = () => unlocks.CurrentTerritory;
        ui.StartAetheryteUnlocks = () => unlocks.Start();
        ui.StartAetheryteZoneUnlocks = unlocks.Start;
        ui.StopAetheryteUnlocks = unlocks.Stop;
        actionHud = new QuestActionHud(Configuration, interaction);
        webLinks = new WebLinkInterceptor(Services.Interop, TryQueueWebLink);
        ui.BrowserInterceptionStatus = () => webLinks.IsEnabled && !ui.BrowserReady
            ? "Web link capture is ready; a link opens the browser on demand." : webLinks.Status;
        requestedWebCapture = Configuration.BrowserInterceptLinks;
        webLinks.SetEnabled(requestedWebCapture);
        Services.Commands.AddHandler("/omnikit", new CommandInfo(OnCommand)
        { HelpMessage = "Open OmniKit. /omnikit settings, /omnikit wiki <search>, /omnikit browser [controls|hide|URL]." });
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenMain;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        Services.Framework.Update += Update;
    }

    private void OnCommand(string command, string args)
    {
        var request = args.Trim();
        if (request.Equals("settings", StringComparison.OrdinalIgnoreCase)) OpenSettings();
        else if (request.Equals("unlock stop", StringComparison.OrdinalIgnoreCase)) unlocks.Stop();
        else if (request.Equals("browser", StringComparison.OrdinalIgnoreCase)) ui.OpenBrowser();
        else if (request.Equals("browser controls", StringComparison.OrdinalIgnoreCase)) ui.ToggleBrowserControls();
        else if (request.Equals("browser hide", StringComparison.OrdinalIgnoreCase)) ui.HideBrowser();
        else if (request.Equals("wiki", StringComparison.OrdinalIgnoreCase)) ui.OpenBrowser();
        else if (request.StartsWith("wiki ", StringComparison.OrdinalIgnoreCase)) ui.OpenWikiCommand(request[5..]);
        else if (request.StartsWith("browser ", StringComparison.OrdinalIgnoreCase)) ui.OpenWebsite(request[8..]);
        else OpenMain();
    }
    private void OpenMain() => ui.OpenMain();
    private void OpenSettings() => ui.OpenSettings();
    private bool TryQueueWebLink(string url)
    {
        return !Volatile.Read(ref disposed) && Configuration.BrowserInterceptLinks
            && ui.TryQueueCapturedWebsite(url);
    }
    private void Update(IFramework framework)
    {
        if (disposed) return;
        characterJobSwitcher.Update();
        jobSwitches.Update();
        if (requestedWebCapture != Configuration.BrowserInterceptLinks)
        {
            requestedWebCapture = Configuration.BrowserInterceptLinks;
            webLinks.SetEnabled(requestedWebCapture);
        }
        ui.SuppressBindingKeys();
        var browserInput = ui.BrowserCapturesInput;
        unlocks.Update(ui.CapturesText || browserInput || ui.CapturingBinding || jobSwitches.Busy || nomNom.Busy);
        nomNom.Update(ui.IsOpen || browserInput || jobSwitches.Busy || unlocks.Busy);
        shortcuts.Update(Configuration, ui.CapturesText || browserInput, ui.SearchHasFocus && !browserInput, ui.ToggleMain, OpenSettings, ui.CapturingBinding);
        ui.Update();
        interaction.Update(ui.IsOpen || browserInput || jobSwitches.Busy || nomNom.Busy || unlocks.Busy);
    }
    private void Draw()
    {
        if (disposed) return;
        ui.Draw();
        interaction.DrawHud();
        actionHud.Draw(Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale);
        interaction.CaptureUiInput(actionHud.OwnsActionInput);
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
        characterJobSwitcher.Dispose();
        unlocks.Dispose();
        nomNom.Dispose();
        jobSwitches.Dispose();
        webLinks.Dispose();
        ui.Dispose(); voice.Dispose(); chat.Dispose();
        interaction.Dispose(); sources.Dispose();
    }
}
