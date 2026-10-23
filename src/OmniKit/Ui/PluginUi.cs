using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Utility;
using OmniKit.Assistant;
using OmniKit.Integration;
using OmniKit.Interaction;
using OmniKit.Search;
using OmniKit.Storage;
using OmniKit.Input;

namespace OmniKit.Ui;

internal sealed class PluginUi : IDisposable
{
    private readonly Configuration config;
    private readonly SoftInteractionService interaction;
    private readonly ChatService chat;
    private readonly VoiceInput voice;
    private readonly LocalStore store;
    private readonly SourceRegistry sources;
    private readonly ActionCatalog catalog;
    private readonly WikiBrowser wiki;
    private readonly FileDialogManager fileDialogs = new();
    private readonly CompactLauncher launcher = new();
    private readonly HotkeyCapture hotkeys = new();
    private readonly RoamingCompanion roaming;
    private bool mainOpen;
    private bool companionOpen;
    private bool settingsOpen;
    private bool focusSearch;
    private bool practiceVoice;
    private string query = "";
    private string question = "";
    private string answer = "";
    private string status = "";
    private string lastBrowserStatus = "";
    private long browserStatusRevision;
    private long browserRequestId;
    private bool trackingBrowserRequest;
    private bool expandBrowserSettings;
    private int capturedBrowserRequest;
    private string settingsSearch = "";
    private string apiKey = "";
    private string providerEndpoint;
    private string phrase = "";
    private string actionFilter = "";
    private string bindingAction = "";
    private string importJson = "";
    private string exportName = "omnikit-settings";
    private bool confirmImport;
    private bool gearFeedback;
    private int section;
    private int companionTab;
    private bool selectSettingsTab;
    private bool resetHudScroll;
    private int shownCompanionTab = -1;
    private long refreshedAt;
    private SearchEntry[] entries = [];
    private Task<string>? pendingAnswer;
    private CancellationTokenSource? request;
    private Task<string>? pendingConnection;
    private Task<IReadOnlyList<AiModelOption>>? pendingModels;
    private CancellationTokenSource? connectionRequest;
    private AiConnectionKind operationConnection;
    private bool refreshModelsAfterConnection;
    private AiModelOption[] chatGptModels = [];
    private string connectionStatus = "";
    private static readonly string[] Sections = ["Launcher", "Interact", "HUD", "Companion", "Data", "About"];
    private static readonly Vector4 Gold = new(.48f, .78f, .74f, 1);
    private static readonly Vector4 Muted = new(.63f, .65f, .69f, 1);
    public bool IsOpen => mainOpen || settingsOpen || companionOpen || hotkeys.IsCapturing || roaming.IsChoosingArea;
    public bool CapturingBinding => hotkeys.IsCapturing;
    public bool BrowserCapturesInput => wiki.CapturesInput;
    public bool BrowserReady => wiki.IsReady;
    public Func<string>? BrowserInterceptionStatus { get; set; }
    public bool TryQueueCapturedWebsite(string url)
    {
        if (!wiki.TryOpenUrl(url, requireReady: true)) return false;
        Interlocked.Exchange(ref capturedBrowserRequest, 1);
        return true;
    }
    public void OpenBrowser() => TrackBrowserRequest(wiki.Show);
    public void ToggleBrowserControls() => TrackBrowserRequest(wiki.ToggleControls);
    public void OpenWikiCommand(string text) => OpenWiki(text);
    public void OpenWebsite(string url) => TrackBrowserRequest(() => wiki.OpenUrl(url));
    public void HideBrowser() => TrackBrowserRequest(wiki.Hide);
    public void SuppressBindingKeys()
    {
        if (!hotkeys.IsCapturing) return;
        foreach (var key in Enum.GetValues<VirtualKey>())
            if ((int)key > 7 && Services.Keys.IsVirtualKeyValid((int)key) && HotkeyChord.IsKeyDown(key))
                Services.Keys[key] = false;
    }
    public bool CapturesText { get; private set; }
    public bool SearchHasFocus { get; private set; }

    public PluginUi(Configuration config, SoftInteractionService interaction, ChatService chat,
        VoiceInput voice, LocalStore store, SourceRegistry sources)
    {
        this.config = config; this.interaction = interaction; this.chat = chat;
        this.voice = voice; this.store = store; this.sources = sources;
        providerEndpoint = config.AiEndpoint;
        wiki = new WikiBrowser(config);
        catalog = new ActionCatalog(OpenSettings, OpenWebsite, OpenBrowser);
        roaming = new RoamingCompanion(config, () => OpenCompanion());
        companionOpen = !config.SetupCompleted;
        interaction.ConfigureHudRequested += OpenHudSettings;
        focusSearch = true;
    }

    public void OpenMain() { query = ""; status = ""; gearFeedback = false; mainOpen = true; launcher.Focus(); Refresh(); }
    public void ToggleMain() { if (mainOpen) mainOpen = false; else OpenMain(); }
    public void OpenSettings() { hotkeys.Cancel(); settingsOpen = true; selectSettingsTab = true; Refresh(); }
    private void OpenHudSettings() { section = 2; settingsSearch = ""; resetHudScroll = true; OpenSettings(); }
    private void OpenCompanion(string text = "") { question = text; companionOpen = true; mainOpen = false; focusSearch = true; }
    private void Refresh()
    {
        entries = catalog.GetEntries().Concat(GearsetActions.GetEntries()).Concat(sources.GetEntries())
            .Append(new SearchEntry("omnikit:companion", "Ask your companion", "Open your moon companion", "ask question chat ai bimble moonbear companion", "OmniKit", () => OpenCompanion()))
            .Concat(config.VoiceEnabled ? new[] { new SearchEntry("omnikit:speak", "Speak a command", "Listen for one phrase", "voice microphone speak command", "OmniKit", () => StartVoice(false)) } : []).ToArray();
        refreshedAt = Environment.TickCount64;
    }

    public void Update()
    {
        if (Interlocked.Exchange(ref capturedBrowserRequest, 0) != 0) TrackBrowserRequest(() => { });
        UpdateBrowserStatus();
        if (gearFeedback) status = GearsetActions.Status;
        if (IsOpen && Environment.TickCount64 - refreshedAt > 2000) Refresh();
        if (!config.VoiceEnabled && voice.IsListening) voice.Cancel();
        if (pendingAnswer is { IsCompleted: true })
        {
            answer = pendingAnswer.IsCompletedSuccessfully ? pendingAnswer.Result : "The request could not finish. Please try again.";
            pendingAnswer = null; request?.Dispose(); request = null;
        }
        UpdateConnection();
        if (!voice.TryTakeResult(out var heard)) return;
        if (!config.VoiceEnabled || !ShortcutInput.GameAcceptsInput()) { status = "Voice result discarded because the game is not ready."; return; }
        if (practiceVoice)
        {
            phrase = heard; practiceVoice = false;
            status = "Heard: " + heard + ". Choose an action below, then save the phrase.";
            return;
        }
        if (companionOpen) { question = heard; status = "Heard your question. Press Ask companion to send it, or Search wiki to look it up."; return; }
        query = heard; mainOpen = true;
        var binding = config.VoiceBindings.FirstOrDefault(b => SearchEngine.Normalize(b.Phrase) == SearchEngine.Normalize(heard));
        if (binding != null)
        {
            var action = entries.FirstOrDefault(e => e.Id == binding.ActionId);
            if (action != null) Activate(action);
            else status = "That voice shortcut's action is unavailable. Check its binding in Companion settings.";
            return;
        }
        var matches = SearchEngine.Find(entries, query);
        if (SearchEngine.Normalize(query).StartsWith("job ", StringComparison.Ordinal) && matches.Count == 1
            && matches[0].Id.StartsWith("gearset:", StringComparison.Ordinal)) Activate(matches[0]);
        else status = "Heard: " + heard + ". Choose a result.";
    }

    public void Draw()
    {
        interaction.HudPreviewEnabled = false;
        CapturesText = ImGui.GetIO().WantTextInput;
        if (!mainOpen) SearchHasFocus = false;
        fileDialogs.Draw();
        roaming.Draw(ImGuiHelpers.GlobalScale);
        if (!IsOpen) { SearchHasFocus = false; return; }
        PushTheme();
        try { if (mainOpen) DrawMain(); if (companionOpen) DrawCompanionWindow(); if (settingsOpen) DrawSettings(); }
        finally { ImGui.PopStyleColor(15); ImGui.PopStyleVar(6); }
    }

    private static void PushTheme()
    {
        var s = ImGuiHelpers.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 8) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 5) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(7, 4) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 2 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 2 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 0);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.065f, .075f, .085f, .99f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.93f, .92f, .88f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.15f, .17f, .20f, 1));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.13f, .19f, .20f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.19f, .29f, .29f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.23f, .36f, .35f, 1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.13f, .20f, .20f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.18f, .28f, .28f, 1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Gold);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Gold);
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(.10f, .13f, .15f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(.17f, .26f, .26f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(.21f, .31f, .31f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabUnfocusedActive, new Vector4(.15f, .22f, .22f, 1));
    }

    private static void WindowSize(Vector2 size, Vector2 minimum)
    {
        var s = ImGuiHelpers.GlobalScale;
        ImGui.SetNextWindowSize(size * s, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(minimum * s, new Vector2(float.MaxValue));
    }

    private static void WindowGrip(ref bool open)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var edge = 20 * scale;
        ImGui.InvisibleButton("##move-window", new Vector2(Math.Max(1, width - edge - 8 * scale), edge));
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(ImGui.IsItemHovered() ? ImGuiCol.Text : ImGuiCol.TextDisabled);
        for (var line = 0; line < 3; line++)
            draw.AddLine(start + new Vector2(0, 5 + line * 4) * scale, start + new Vector2(20, 5 + line * 4) * scale, color);
        if (ImGui.IsItemHovered()) { ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll); ImGui.SetTooltip("Drag to move"); }
        ImGui.SetCursorScreenPos(start + new Vector2(width - edge, 0));
        if (ImGui.InvisibleButton("Close", new Vector2(edge))) open = false;
        var close = start + new Vector2(width - edge, 0);
        color = ImGui.GetColorU32(ImGui.IsItemHovered() ? ImGuiCol.Text : ImGuiCol.TextDisabled);
        draw.AddLine(close + new Vector2(5, 5) * scale, close + new Vector2(15, 15) * scale, color, 1.5f * scale);
        draw.AddLine(close + new Vector2(15, 5) * scale, close + new Vector2(5, 15) * scale, color, 1.5f * scale);
        ImGui.SetCursorScreenPos(start + new Vector2(0, edge + 5 * scale));
    }

    private float FooterHeight => ImGui.GetTextLineHeightWithSpacing() * (status.Length > 0 || voice.IsListening ? 2 : 1) + 2 * ImGuiHelpers.GlobalScale;

    private static void Footer(string message)
    {
        ImGui.BeginChild("footer", new Vector2(0, 0));
        UiText.Line(BuildVersion.Display + "  ·  by Zero", Muted);
        UiText.Status(message);
        ImGui.EndChild();
    }

    private void DrawMain()
    {
        launcher.Draw(ref mainOpen, ref query, entries, ImGuiHelpers.GlobalScale, Activate,
            () => { mainOpen = false; OpenSettings(); }, OpenCompanion, OpenWiki, gearFeedback ? status : "");
        SearchHasFocus = launcher.SearchHasFocus;
    }

    private void DrawCompanionWindow()
    {
        WindowSize(new(540, 400), new(480, 330));
        if (!ImGui.Begin("Moon companion###OmniKit.Companion.Minimal", ref companionOpen, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse)) { ImGui.End(); return; }
        try
        {
            WindowGrip(ref companionOpen);
            Header(config.SetupCompleted ? "XIV companion" : "Choose your setup");
            if (!config.SetupCompleted) { DrawWelcome(); Footer(""); return; }
            if (config.VoiceEnabled)
            {
                if (UiText.Button(voice.IsListening ? "Stop listening" : "Speak"))
                { if (voice.IsListening) voice.Cancel(); else StartVoice(false); }
            }
            ImGui.SetNextItemWidth(-1);
            if (focusSearch) { ImGui.SetKeyboardFocusHere(); focusSearch = false; }
            var submit = ImGui.InputTextWithHint("##question", "Ask about XIV...", ref question, 4000, ImGuiInputTextFlags.EnterReturnsTrue)
                && (ImGui.IsKeyPressed(ImGuiKey.Enter, false) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter, false));
            DrawAnswer(submit);
            Footer(voice.IsListening ? voice.Status : status);
        }
        finally { ImGui.End(); }
    }

    private void Header(string subtitle)
    {
        var s = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        if (config.AvatarEnabled)
        {
            var portraitSize = new Vector2(44) * s;
            if (ProceduralCompanion.IsProcedural(config.Avatar))
                ProceduralCompanion.Draw(ImGui.GetWindowDrawList(), origin, portraitSize, config, CompanionPose.Idle, (float)ImGui.GetTime());
            else
            {
                var path = Path.Combine(Services.PluginInterface.AssemblyLocation.DirectoryName!, "assets", config.Avatar == "Moonbear" ? "moonbear.png" : "bimble.png");
                var texture = Services.Textures.GetFromFile(path).GetWrapOrDefault();
                if (texture != null) ImGui.GetWindowDrawList().AddImage(texture.Handle, origin, origin + portraitSize);
            }
            ProceduralCompanion.DrawEffects(ImGui.GetWindowDrawList(), origin, portraitSize, config, CompanionPose.Idle, (float)ImGui.GetTime());
        }
        ImGui.SetCursorScreenPos(origin + new Vector2(config.AvatarEnabled ? 54 : 0, 1) * s);
        UiText.Line(config.AvatarEnabled ? config.Avatar : "OmniKit", Gold, outline: true);
        ImGui.SetCursorScreenPos(origin + new Vector2(config.AvatarEnabled ? 54 : 0, 23) * s);
        ImGui.TextWrapped(subtitle);
        ImGui.SetCursorScreenPos(origin + new Vector2(0, 50) * s);
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawWelcome()
    {
        ImGui.BeginChild("welcome", new Vector2(0, -FooterHeight));
        UiText.Line("Local tools", Gold, outline: true);
        ImGui.TextWrapped("Search commands, switch gearsets and use voice shortcuts. No AI conversation or account needed.");
        if (UiText.Button("Use without AI", new Vector2(-1, 0)))
        { config.AiEnabled = false; config.SetupCompleted = true; Save(); companionOpen = false; OpenMain(); }
        ImGui.Spacing(); UiText.Line("AI companion", Gold, outline: true);
        ImGui.TextWrapped("Ask about XIV. Submitted questions go to your provider; costs may apply. Replies cannot control the game.");
        if (UiText.Button("Set up AI", new Vector2(-1, 0)))
        { config.AiEnabled = false; config.SetupCompleted = true; Save(); companionOpen = false; section = 3; companionTab = 2; OpenSettings(); }
        UiText.Help("Connection choices", "ChatGPT, an installed Codex or Claude CLI, an API key, or a compatible local server. Connecting does not enable questions until you turn AI on. CLI connections may also include their own normal instructions and context.");
        ImGui.EndChild();
    }

    private void Activate(SearchEntry entry)
    {
        if (!ShortcutInput.GameAcceptsInput()) { status = "Return to the game and close chat typing first."; return; }
        try
        {
            var browserRequestBefore = browserRequestId;
            entry.Activate();
            gearFeedback = entry.Id.StartsWith("gearset:", StringComparison.Ordinal);
            mainOpen = gearFeedback;
            if (gearFeedback) status = "Requesting gear change...";
            else if (browserRequestBefore == browserRequestId) status = "";
        }
        catch (Exception ex)
        { status = "That action could not open. Check that its plugin is loaded."; Services.Log.Warning("OmniKit action failed ({ErrorType}).", ex.GetType().Name); }
    }

    private void DrawAnswer(bool submit)
    {
        if (!config.AiEnabled)
        {
            UiText.Line("AI off · local commands available", Muted);
            if (UiText.Button("Connect AI")) { section = 3; companionTab = 2; selectSettingsTab = true; OpenSettings(); }
        }
        else
        {
            var cliPaused = config.AiConnection is AiConnectionKind.CodexCli or AiConnectionKind.ClaudeCli;
            if (cliPaused) UiText.Status(CliChatBackend.SingleRequestLimitNotice);
            var canAsk = pendingAnswer == null && !ConnectionBusy && !chat.IsBusy && !cliPaused && !string.IsNullOrWhiteSpace(question);
            ImGui.BeginDisabled(!canAsk);
            var clicked = UiText.Button("Ask companion");
            UiText.Hint("Sends this question once. No automatic retries, follow-up generation or background AI. Canceling does not guarantee that the provider stops billing.");
            if (canAsk && (clicked || submit))
            { request = new CancellationTokenSource(); pendingAnswer = chat.AskAsync(question, request.Token); answer = "Thinking..."; }
            ImGui.EndDisabled();
            if (pendingAnswer != null) { ImGui.SameLine(); if (UiText.Button("Cancel")) request?.Cancel(); }
        }
        ImGui.SameLine(); if (UiText.Button("Search wiki")) OpenWiki(question);
        ImGui.BeginChild("answer", new Vector2(0, -FooterHeight));
        ImGui.TextWrapped(answer.Length == 0 ? "Ask about XIV, or search the wiki." : answer);
        ImGui.EndChild();
    }

    private void OpenWiki(string text)
    {
        TrackBrowserRequest(() => wiki.Open(text));
    }

    private void TrackBrowserRequest(Action action)
    {
        action();
        gearFeedback = false;
        browserRequestId++;
        trackingBrowserRequest = true;
        browserStatusRevision = wiki.StatusRevision - 1;
        var latest = wiki.Status;
        if (status.Length == 0 || status == lastBrowserStatus) status = latest;
        lastBrowserStatus = latest;
    }

    private void UpdateBrowserStatus()
    {
        if (!trackingBrowserRequest || browserStatusRevision == wiki.StatusRevision) return;
        browserStatusRevision = wiki.StatusRevision;
        var latest = wiki.Status;
        if (status == lastBrowserStatus) status = latest;
        lastBrowserStatus = latest;
        if (!wiki.HasError) return;
        trackingBrowserRequest = false;
        // An explicit browser action can close Search before its helper reports a failure.
        // Reveal its own status once, leaving unrelated footer messages intact.
        hotkeys.Cancel(); section = 4; settingsOpen = true; selectSettingsTab = true; expandBrowserSettings = true;
    }

    private void StartVoice(bool practice)
    {
        practiceVoice = practice;
        voice.Start(config.VoiceBindings.Select(b => b.Phrase).Concat(GearsetActions.GetPhrases()));
        status = voice.Status;
    }

    private void DrawSettings()
    {
        WindowSize(new(540, 400), new(480, 330));
        // New identity discards the oversized window size saved by the first preview.
        if (!ImGui.Begin("OmniKit Settings###OmniKit.Settings.Minimal", ref settingsOpen, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse)) { ImGui.End(); return; }
        try
        {
            WindowGrip(ref settingsOpen);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##settings-search", "Find a setting...", ref settingsSearch, 100))
            {
                hotkeys.Cancel();
                var filter = settingsSearch.ToLowerInvariant();
                if (new[] { "about", "version", "author", "credits", "commit" }.Any(filter.Contains)) section = 5;
                else if (new[] { "interact", "gather", "gear", "enemy", "enemies", "category", "categories", "target type", "attack", "quest", "shovel" }.Any(filter.Contains)) section = 1;
                else if (new[] { "hud", "target", "font", "text size", "outline", "glow", "shimmer", "rainbow", "neon", "glitch", "hologram", "flame", "frost", "sweep", "effect", "intensity" }.Any(filter.Contains)) section = 2;
                else if (new[] { "voice", "ai", "avatar", "companion", "brain", "bind", "train", "box", "frame", "pickup", "perch", "fall", "chatgpt", "codex", "claude", "provider", "account", "home", "house", "comment", "blink", "hand", "grab" }.Any(filter.Contains))
                { section = 3; companionTab = new[] { "ai", "brain", "chatgpt", "codex", "claude", "provider", "account" }.Any(filter.Contains) ? 2 : filter.Contains("voice") || filter.Contains("train") ? 1 : 0; }
                else if (new[] { "data", "export", "import", "json", "source", "umbra", "wiki", "browser", "extension" }.Any(filter.Contains)) section = 4;
                else if (filter.Contains("key") || filter.Contains("shortcut")) section = 0;
                selectSettingsTab = true;
            }
            if (ImGui.BeginTabBar("settings-tabs", ImGuiTabBarFlags.FittingPolicyScroll))
            {
                var requestedSection = selectSettingsTab ? section : -1;
                for (var i = 0; i < Sections.Length; i++)
                {
                    var flags = requestedSection == i ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
                    if (!UiText.BeginTabItem(Sections[i], flags)) continue;
                    if (section != i) hotkeys.Cancel();
                    section = i;
                    ImGui.BeginChild("settings-body-" + i, new Vector2(0, -FooterHeight));
                    switch (i)
                    {
                        case 0: DrawShortcuts(); break;
                        case 1: DrawInteraction(); break;
                        case 2: DrawHudSettings(); break;
                        case 3: DrawCompanion(); break;
                        case 4: DrawData(); break;
                        case 5: DrawAbout(); break;
                    }
                    ImGui.EndChild(); ImGui.EndTabItem();
                }
                ImGui.EndTabBar(); selectSettingsTab = false;
            }
            Footer(status);
        }
        finally { ImGui.End(); if (!settingsOpen) hotkeys.Cancel(); }
    }

    private void DrawShortcuts()
    {
        if (ImGui.Checkbox("Enable launcher shortcut", ref config.LauncherEnabled)) Save();
        var old = (config.LauncherKey, config.LauncherModifier, config.SettingsKey, config.SettingsModifier);
        var changed = hotkeys.Draw("Search menu", ref config.LauncherKey, ref config.LauncherModifier);
        changed |= hotkeys.Draw("Open settings", ref config.SettingsKey, ref config.SettingsModifier);
        if (changed)
        {
            if ((config.LauncherKey == config.SettingsKey && config.LauncherModifier == config.SettingsModifier)
                || (config.LauncherModifier == config.InteractModifier && config.LauncherKey == config.InteractKey)
                || (config.SettingsModifier == config.InteractModifier && config.SettingsKey == config.InteractKey))
            {
                (config.LauncherKey, config.LauncherModifier, config.SettingsKey, config.SettingsModifier) = old;
                status = "Choose different shortcuts for Search, Settings, and Interact.";
            }
            else Save();
        }
        if (UiText.Button("Open Search")) OpenMain();
        UiText.Help("Shortcut help", "Click a binding, hold Ctrl/Alt/Shift if needed, press a key, then release all keys. Escape cancels. /omnikit and /omnikit settings always work. Change Alt + Space if another desktop launcher uses it.");
        UiText.Help("Search examples", "community · guild · free company\njob paladin · job miner · a saved gearset name\ninventory · crafting log · a plugin name");
    }

    private void DrawInteraction()
    {
        if (ImGui.Checkbox("Enable soft interaction", ref config.SoftInteractEnabled)) { hotkeys.Cancel(); Save(); }
        UiText.Hint("Look toward a target, then press the interact key. Looking alone never performs an action.");
        if (!config.SoftInteractEnabled) return;
        var old = (config.InteractKey, config.InteractModifier);
        if (hotkeys.Draw("Interact key", ref config.InteractKey, ref config.InteractModifier))
        {
            if ((config.LauncherModifier == config.InteractModifier && config.InteractKey == config.LauncherKey) || (config.SettingsModifier == config.InteractModifier && config.InteractKey == config.SettingsKey))
            { (config.InteractKey, config.InteractModifier) = old; status = "The interact key needs its own shortcut."; }
            else Save();
        }
        if (ImGui.SliderFloat("Detection distance", ref config.InteractionDistance,
            Configuration.MinimumDetectionDistance, Configuration.MaximumDetectionDistance, "%.1f yalms")) Save();
        UiText.Hint("Detection range only. The game still controls interaction reach.");
        DrawInteractionTargets();
        if (ImGui.Checkbox("Use verified quest items", ref config.QuestItemUseEnabled)) Save();
        UiText.Hint("On Interact, use a proven quest item after selecting its target. First supported case: Tiller's Shovel on the cumbersome rocks in Rocks in a Hard Place. Debris keeps ordinary interaction. Unknown quests require manual item use.");
        if (config.QuestItemUseEnabled && interaction.QuestItems?.Status is { Length: > 0 } questItemStatus) UiText.Status(questItemStatus);
        VisibleTarget();
        if (UiText.CollapsingHeader("Gathering outfits"))
        {
        if (ImGui.Checkbox("Switch to gathering gear when I interact", ref config.GatheringAutoSwitch)) Save();
        if (config.GatheringAutoSwitch)
        {
            UiText.Hint("Uses saved Miner or Botanist gearsets before opening a gathering node.");
            GearCombo("Mining gear", ref config.MinerGearset, "Miner");
            GearCombo("Botany gear", ref config.BotanistGearset, "Botanist");
            if (ImGui.Checkbox("Return to my previous gear after gathering", ref config.GatheringRestore)) Save();
            UiText.Hint("Restore stops if you change job or gear yourself. OmniKit never chooses items or gathers for you.");
            UiText.Status(interaction.Gathering.Status);
        }
        }
        if (UiText.CollapsingHeader("Fine-tune targeting"))
        {
            if (ImGui.SliderFloat("Aim area", ref config.InteractionCone, .025f, .30f, "%.3f")) Save();
            UiText.Hint("A smaller aim area is more precise. Native range and usability checks still apply.");
        }
    }

    private void DrawInteractionTargets()
    {
        if (!UiText.CollapsingHeader("Target types", ImGuiTreeNodeFlags.DefaultOpen)) return;
        if (ImGui.BeginTable("world-targets", 2, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var (kind, label) in new[]
            {
                (InteractionTargets.Npcs, "NPCs / vendors"),
                (InteractionTargets.QuestObjects, "Quest objects"),
                (InteractionTargets.Loot, "Loot / treasure"),
                (InteractionTargets.Aetherytes, "Aetherytes"),
                (InteractionTargets.GatheringNodes, "Gathering nodes"),
                (InteractionTargets.Retainers, "Retainers"),
                (InteractionTargets.Housing, "Housing"),
                (InteractionTargets.OtherUsableObjects, "Other usable objects"),
            })
            {
                ImGui.TableNextColumn();
                TargetToggle(label, kind);
            }
            ImGui.EndTable();
        }
        Heading("Enemies");
        TargetToggle("Hostile enemies", InteractionTargets.HostileEnemies);
        TargetToggle("Neutral enemies", InteractionTargets.NeutralEnemies);
        UiText.Hint("Neutral means attackable but not currently marked hostile. This can change with game state.");
        if ((config.InteractionTargets & (InteractionTargets.HostileEnemies | InteractionTargets.NeutralEnemies)) != 0)
        {
            if (ImGui.RadioButton("Select target only", config.EnemyInteractMode == EnemyInteractMode.TargetOnly))
            { config.EnemyInteractMode = EnemyInteractMode.TargetOnly; Save(); }
            if (ImGui.RadioButton("Target and start auto-attack", config.EnemyInteractMode == EnemyInteractMode.StartAttack))
            { config.EnemyInteractMode = EnemyInteractMode.StartAttack; Save(); }
            if (config.EnemyInteractMode == EnemyInteractMode.Choose)
            {
                UiText.Status("Choose an enemy action.");
            }
            UiText.Help("Enemy behavior", "Press your interact key to use the selected action. Target only does not start auto-attack, but an existing auto-attack stays active. The game's normal range and target rules apply.");
        }
        if (config.InteractionTargets == InteractionTargets.None)
            UiText.Status("All target types are off.");
    }

    private void TargetToggle(string label, InteractionTargets kind)
    {
        var enabled = (config.InteractionTargets & kind) != 0;
        if (!ImGui.Checkbox(label, ref enabled)) return;
        config.InteractionTargets = enabled ? config.InteractionTargets | kind : config.InteractionTargets & ~kind;
        Save();
    }

    private void VisibleTarget()
    {
        var label = interaction.CandidateName ?? "None";
        UiText.Line("Target · " + label, interaction.CandidateName == null ? Muted : Gold, outline: true,
            detail: interaction.CandidateCategoryName + "\n" + (interaction.Hint ?? "Look toward a nearby usable object."));
    }

    private void DrawHudSettings()
    {
        interaction.HudPreviewEnabled = true;
        if (resetHudScroll) { ImGui.SetScrollY(0); resetHudScroll = false; }
        var fontPercent = config.InteractionHudScale * 100f;
        if (ImGui.SliderFloat("Font size", ref fontPercent, 50, 200, "%.0f%%", ImGuiSliderFlags.AlwaysClamp))
        { config.InteractionHudScale = fontPercent / 100f; Save(); }
        if (ImGui.Checkbox("Text outline", ref config.InteractionHudOutline)) Save();
        var effectLabel = config.InteractionHudEffect switch
        {
            HudTextEffect.SoftGlow => "Soft glow", HudTextEffect.GentleShimmer => "Gentle shimmer",
            HudTextEffect.NeonPulse => "Neon pulse", HudTextEffect.ChromaticGlitch => "Chromatic glitch",
            HudTextEffect.LightSweep => "Light sweep", _ => config.InteractionHudEffect.ToString(),
        };
        if (UiText.BeginCombo("Text effect", effectLabel))
        {
            foreach (var (effect, label) in new[] { (HudTextEffect.None, "None"), (HudTextEffect.Shadow, "Shadow"),
                         (HudTextEffect.SoftGlow, "Soft glow"), (HudTextEffect.GentleShimmer, "Gentle shimmer"),
                         (HudTextEffect.Rainbow, "Rainbow"), (HudTextEffect.NeonPulse, "Neon pulse"),
                         (HudTextEffect.ChromaticGlitch, "Chromatic glitch"), (HudTextEffect.Hologram, "Hologram"),
                         (HudTextEffect.Flame, "Flame"), (HudTextEffect.Frost, "Frost"), (HudTextEffect.LightSweep, "Light sweep") })
                if (UiText.Selectable(label, config.InteractionHudEffect == effect)) { config.InteractionHudEffect = effect; Save(); }
            ImGui.EndCombo();
        }
        if (config.InteractionHudEffect != HudTextEffect.None && UiText.CollapsingHeader("Effect settings"))
        {
            if (ImGui.SliderFloat("Intensity", ref config.InteractionHudEffectIntensity, 0, 2, "%.2f", ImGuiSliderFlags.AlwaysClamp)) Save();
            if (ImGui.Checkbox("Animate effect", ref config.InteractionHudEffectAnimated)) Save();
            ImGui.BeginDisabled(!config.InteractionHudEffectAnimated);
            if (ImGui.SliderFloat("Speed", ref config.InteractionHudEffectSpeed, .1f, 3, "%.2f", ImGuiSliderFlags.AlwaysClamp)) Save();
            ImGui.EndDisabled();
        }
        if (ImGui.Checkbox("Show HUD", ref config.InteractionHudEnabled)) Save();
        UiText.Hint("An editing preview is shown here even when the HUD is disabled. Active interactions hide both.");
        if (ImGui.Checkbox("Lock position", ref config.InteractionHudLocked)) Save();
        if (ImGui.SliderFloat("Horizontal offset", ref config.InteractionHudOffsetX, -800, 800, "%.0f")) Save();
        if (ImGui.SliderFloat("Vertical offset", ref config.InteractionHudOffsetY, -500, 500, "%.0f")) Save();
        if (UiText.Button("Reset HUD position")) { config.InteractionHudOffsetX = 0; config.InteractionHudOffsetY = 120; Save(); }
        UiText.Line(interaction.HudHiddenForInteraction ? "Preview hidden · interaction active" : config.InteractionHudEnabled ? "Preview active" : "Preview only · HUD off",
            interaction.HudHiddenForInteraction ? UiText.Warning : Muted);
        UiText.Help("HUD help", "Changes preview immediately. Shift + right-click the HUD to move, lock, modify or hide it. Text is temporarily hidden during NPC, vendor, quest, retainer and gathering interactions or cutscenes. No action runs while you edit.");
    }

    private void GearCombo(string label, ref int value, string job)
    {
        var sets = interaction.Gathering.GetGearsets();
        var selectedValue = value;
        var current = sets.FirstOrDefault(g => g.Index == selectedValue);
        if (!UiText.BeginCombo(label, value < 0 ? "Choose automatically" : current?.Name ?? "Saved set unavailable")) return;
        if (UiText.Selectable("Choose automatically", value < 0)) { value = -1; Save(); }
        foreach (var set in sets)
        {
            var name = Services.Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>(Dalamud.Game.ClientLanguage.English).GetRow(set.ClassJobId).Name.ToString();
            if (!name.Equals(job, StringComparison.OrdinalIgnoreCase)) continue;
            if (UiText.Selectable($"{set.Index + 1}. {set.Name}", value == set.Index)) { value = set.Index; Save(); }
        }
        ImGui.EndCombo();
    }

    private void DrawCompanion()
    {
        if (!ImGui.BeginTabBar("companion-options")) return;
        // Snapshot programmatic selection before drawing: a clicked earlier tab
        // must not make a later tab force itself selected in the same frame.
        var requestedTab = shownCompanionTab != companionTab ? companionTab : -1;
        var clickedTab = -1;
        foreach (var (index, title) in new[] { (0, "Appearance"), (1, "Voice"), (2, "AI") })
        {
            var flags = requestedTab == index ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            var visible = UiText.BeginTabItem(title, flags);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) clickedTab = index;
            if (!visible) continue;
            shownCompanionTab = index;
            switch (index) { case 0: DrawAppearance(); break; case 1: DrawVoiceSettings(); break; case 2: DrawAiSettings(); break; }
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
        companionTab = clickedTab >= 0 ? clickedTab : shownCompanionTab;
    }

    private void DrawAppearance()
    {
        if (ImGui.Checkbox("Show companion", ref config.AvatarEnabled)) Save();
        if (config.AvatarEnabled)
        {
            if (UiText.BeginCombo("Companion", AvatarLabel(config.Avatar)))
            {
                foreach (var avatar in Configuration.AvatarNames)
                    if (UiText.Selectable(AvatarLabel(avatar), config.Avatar == avatar)) { config.Avatar = avatar; Save(); }
                ImGui.EndCombo();
            }
            if (config.Avatar is "Bimble" or "Moonbear")
            {
                if (UiText.BeginCombo("Outfit", OutfitLabel(config.CompanionOutfit)))
                {
                    foreach (var outfit in Configuration.OutfitNames)
                        if (UiText.Selectable(OutfitLabel(outfit), config.CompanionOutfit == outfit)) { config.CompanionOutfit = outfit; Save(); }
                    ImGui.EndCombo();
                }
            }
            if (UiText.BeginCombo("Aura / shape color", config.CompanionPalette))
            {
                foreach (var palette in Configuration.PaletteNames)
                {
                    var picked = ImGui.ColorButton("##palette-" + palette, ProceduralCompanion.Palette(palette), ImGuiColorEditFlags.NoTooltip,
                        new Vector2(ImGui.GetTextLineHeight()));
                    ImGui.SameLine();
                    if (UiText.Selectable(palette, config.CompanionPalette == palette) || picked) { config.CompanionPalette = palette; Save(); }
                }
                ImGui.EndCombo();
            }
            if (ImGui.Checkbox("Animate companion", ref config.AnimationsEnabled)) Save();
            if (ImGui.Checkbox("Magic accents", ref config.CompanionMagicEnabled)) Save();
            ImGui.BeginDisabled(!config.AnimationsEnabled);
            if (UiText.BeginCombo("Preview animation", roaming.PreviewPose.ToString()))
            {
                foreach (var pose in Enum.GetValues<CompanionPose>())
                    if (UiText.Selectable(pose.ToString(), roaming.PreviewPose == pose)) roaming.PreviewPose = pose;
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Preview only; no speech playback.");
            ImGui.EndDisabled();
            if (ImGui.SliderFloat("Companion size", ref config.CompanionSize, 48, 192, "%.0f")) Save();
            if (UiText.Button("Draw roaming area")) { roaming.BeginChooseArea(); settingsOpen = false; }
            UiText.Hint("Drag a roaming box over the game. Escape cancels. Click the companion to talk.");
            Heading("Frame interaction");
            if (ImGui.Checkbox("Enable frame interaction", ref config.CompanionFrameInteractionEnabled)) Save();
            if (config.CompanionFrameInteractionEnabled)
            {
                if (ImGui.Checkbox("Pick up and drag", ref config.CompanionPickupEnabled)) Save();
                if (ImGui.Checkbox("Walk on window tops", ref config.CompanionPerchEnabled)) Save();
                if (ImGui.Checkbox("Stay inside a window when dropped inside", ref config.CompanionContainmentEnabled)) Save();
                if (ImGui.Checkbox("Fall when leaving a window", ref config.CompanionFallingEnabled)) Save();
                UiText.Help("Frame behavior", "Drop near a window's top to perch, or inside to roam within its edges. Leaving or closing it falls to the screen bottom, then walks home. With falling off, it walks home directly. Pickup works with animation off.");
            }
            if (UiText.CollapsingHeader("Home & personality"))
            {
                if (ImGui.Checkbox("Show companion home", ref config.CompanionHomeEnabled)) Save();
                UiText.Hint("Drag the house to move the roaming area. Its entrance faces inward near a screen edge.");
                if (ImGui.Checkbox("Show grab hand", ref config.CompanionGrabHandEnabled)) Save();
                UiText.Hint("Show the ready, pickup and release hand over the companion and its house.");
                if (ImGui.Checkbox("Local comments", ref config.CompanionCommentsEnabled)) Save();
                UiText.Hint("Occasional local text phrases. No AI request, microphone or speech playback.");
                ImGui.BeginDisabled(!config.CompanionCommentsEnabled);
                if (ImGui.SliderFloat("Comment interval", ref config.CompanionCommentInterval, 30, 600, "%.0f sec", ImGuiSliderFlags.AlwaysClamp)) Save();
                ImGui.EndDisabled();
                if (config.Avatar == "Wink")
                {
                    if (ImGui.Checkbox("Blink", ref config.CompanionBlinkEnabled)) Save();
                    UiText.Hint("Wink's occasional blink. Animate companion must also be enabled.");
                    ImGui.BeginDisabled(!config.CompanionBlinkEnabled || !config.AnimationsEnabled);
                    if (ImGui.SliderFloat("Blink interval", ref config.CompanionBlinkInterval, 1, 10, "%.1f sec", ImGuiSliderFlags.AlwaysClamp)) Save();
                    ImGui.EndDisabled();
                }
            }
        }
        if (UiText.Button("Open companion conversation")) OpenCompanion();
    }

    private void DrawVoiceSettings()
    {
        if (ImGui.Checkbox("Enable voice input", ref config.VoiceEnabled)) Save();
        UiText.Line("Windows speech · push to talk", Muted);
        UiText.Hint("The microphone opens only for Speak or Practice, then stops after one phrase. Typing always works.");
        if (config.VoiceEnabled) DrawVoiceBindings();
    }

    private void DrawAiSettings()
    {
        ImGui.SetNextItemWidth(-1);
        if (UiText.BeginCombo("##ai-connection", ConnectionLabel(config.AiConnection)))
        {
            foreach (var connection in Enum.GetValues<AiConnectionKind>())
                if (UiText.Selectable(ConnectionLabel(connection), connection == config.AiConnection) && connection != config.AiConnection)
                {
                    CancelAiOperations(); config.AiConnection = connection; config.AiEnabled = false;
                    chatGptModels = []; apiKey = ""; connectionStatus = "";
                    Save(); status = "Connection changed. Enable AI questions when this provider is ready.";
                }
            ImGui.EndCombo();
        }
        if (ImGui.Checkbox("Use AI for questions", ref config.AiEnabled))
        { if (!config.AiEnabled) CancelAiOperations(); Save(); }
        ImGui.TextWrapped("Submitted questions go to this provider; costs may apply.");
        UiText.Hint("AI replies cannot run commands or control the game. Local commands work with AI off. Connecting alone does not enable questions.");
        if (UiText.Button("Open companion")) OpenCompanion();
        switch (config.AiConnection)
        {
            case AiConnectionKind.ChatGpt: DrawChatGptConnection(); break;
            case AiConnectionKind.CodexCli:
            case AiConnectionKind.ClaudeCli: DrawCliConnection(); break;
            default: DrawApiConnection(); break;
        }
        if (ConnectionBusy)
        {
            UiText.Status("Connecting…");
            if (UiText.Button("Cancel connection")) connectionRequest?.Cancel();
        }
        if (connectionStatus.Length > 0) UiText.Status(connectionStatus);
    }

    private void DrawApiConnection()
    {
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##model", "Provider model name", ref config.AiModel, 200)) Save();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##api-key", "Paste API key here", ref apiKey, 4096, ImGuiInputTextFlags.Password);
        if (UiText.Button("Save key"))
        {
            try { chat.SaveApiKey(apiKey); apiKey = ""; status = "Key saved securely for this provider on this Windows account."; }
            catch { status = "The key could not be saved. Check the provider address and try again."; }
        }
        ImGui.SameLine(); if (UiText.Button("Remove saved key"))
        {
            try { chat.ForgetApiKey(); status = "Saved key removed for this provider."; }
            catch { status = "The saved key could not be removed. Check local storage access."; }
        }
        UiText.Line(chat.HasApiKey ? "Key saved · Windows protected" : "No saved key", Muted);
        UiText.Hint("The saved API key belongs to this provider and Windows account. It is excluded from settings exports.");
        if (UiText.CollapsingHeader("Other provider or local AI server"))
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##provider", ref providerEndpoint, 500);
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                if (Uri.TryCreate(providerEndpoint, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || (uri.Scheme == "http" && System.Net.IPAddress.TryParse(uri.Host, out var ip) && System.Net.IPAddress.IsLoopback(ip)))
                    && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0)
                {
                    if (!string.Equals(config.AiEndpoint, providerEndpoint, StringComparison.Ordinal))
                    {
                        CancelAiOperations(); config.AiEndpoint = providerEndpoint; config.AiEnabled = false; apiKey = "";
                        Save(); status = "Address changed. Enable AI questions when this provider is ready.";
                    }
                }
                else status = "Use a full HTTPS Responses address, or HTTP on 127.0.0.1 for a local server.";
            }
            ImGui.TextWrapped("Responses-compatible HTTPS or local HTTP endpoint. Local servers may not need a key.");
            UiText.Hint("Changing providers disables AI questions and never sends the old provider's key to the new address.");
        }
    }

    private void DrawChatGptConnection()
    {
        if (connectionStatus.Length == 0 && !ConnectionBusy) UiText.Status(chat.ChatGpt.Status);
        var accounts = chat.ChatGpt.Accounts;
        var active = accounts.FirstOrDefault(account => account.IsActive);
        ImGui.BeginDisabled(ConnectionBusy || pendingAnswer != null);
        if (accounts.Count > 0)
        {
            ImGui.SetNextItemWidth(-1);
            if (UiText.BeginCombo("##chatgpt-account", FitLabel(active?.Label ?? "Choose a saved account", ImGui.GetContentRegionAvail().X - 40 * ImGuiHelpers.GlobalScale)))
            {
                foreach (var account in accounts)
                {
                    var label = account.Label + (account.IsConnected ? "" : " · reconnect");
                    if (UiText.Selectable(MenuLabel(label) + "##" + account.Id, account.IsActive))
                    {
                        try
                        {
                            chat.ChatGpt.SelectAccount(account.Id); config.AiEnabled = false; chatGptModels = []; Save();
                            if (chat.ChatGpt.IsConnected) StartModelRefresh();
                            else connectionStatus = "Account selected. Reconnect it before enabling AI questions.";
                        }
                        catch { connectionStatus = "The saved account could not be selected. Try again."; }
                    }
                    if (ImGui.IsItemHovered()) WrappedTooltip(label);
                }
                ImGui.EndCombo();
            }
        }
        if (UiText.Button("Continue with ChatGPT"))
        {
            config.AiEnabled = false; Save();
            StartConnection(token => chat.ChatGpt.SignInAsync(token), refreshModels: true);
        }
        UiText.Hint("Opens browser sign-in. OmniKit keeps its own protected connection and never copies another app's login.");
        if (accounts.Count > 0)
        {
            ImGui.SameLine();
            if (UiText.Button("Add account"))
            {
                config.AiEnabled = false; Save();
                StartConnection(token => chat.ChatGpt.SignInAsync(token, true), refreshModels: true);
            }
            if (UiText.Button("Sign out selected account"))
            {
                config.AiEnabled = false; chatGptModels = []; Save();
                StartConnection(token => chat.ChatGpt.SignOutAsync(token));
            }
        }
        ImGui.BeginDisabled(!chat.ChatGpt.IsConnected);
        if (UiText.Button("Refresh available models")) StartModelRefresh();
        ImGui.EndDisabled();
        ImGui.EndDisabled();
        ImGui.SetNextItemWidth(-1);
        var selected = chatGptModels.FirstOrDefault(model => model.Id == config.ChatGptModel);
        var preview = selected?.Name ?? (config.ChatGptModel.Length > 0 ? config.ChatGptModel : "Choose a ChatGPT model");
        ImGui.BeginDisabled(ConnectionBusy || chatGptModels.Length == 0);
        if (UiText.BeginCombo("##chatgpt-model", FitLabel(preview, ImGui.GetContentRegionAvail().X - 40 * ImGuiHelpers.GlobalScale)))
        {
            foreach (var model in chatGptModels)
            {
                if (UiText.Selectable(MenuLabel(model.Name) + "##" + model.Id, model.Id == config.ChatGptModel))
                { config.ChatGptModel = model.Id; Save(); }
                if (ImGui.IsItemHovered()) WrappedTooltip(model.Name);
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        if (chatGptModels.Length == 0)
            UiText.Line(chat.ChatGpt.IsConnected ? "No models · refresh above" : "Connect to load models", Muted);
    }

    private void DrawCliConnection()
    {
        UiText.Status(CliChatBackend.SingleRequestLimitNotice);
        var codex = config.AiConnection == AiConnectionKind.CodexCli;
        ImGui.TextWrapped("Uses your CLI login and may include its normal instructions and context.");
        if (connectionStatus.Length == 0 && !ConnectionBusy) UiText.Status(chat.CliStatus);
        ImGui.BeginDisabled(ConnectionBusy || pendingAnswer != null);
        if (UiText.Button("Check CLI connection")) StartConnection(chat.ProbeCliAsync);
        ImGui.SameLine();
        if (UiText.Button("Sign in to CLI")) StartConnection(chat.SignInCliAsync);
        ImGui.EndDisabled();
        ImGui.SetNextItemWidth(-1);
        if (codex)
        {
            if (ImGui.InputTextWithHint("##codex-model", "Model (optional; uses CLI default)", ref config.CodexModel, 200)) Save();
        }
        else if (ImGui.InputTextWithHint("##claude-model", "Model (optional; uses CLI default)", ref config.ClaudeModel, 200)) Save();
        UiText.Help("CLI privacy & limits", "OmniKit does not copy the CLI's credentials. Action tools are disabled; game state is not sent. " +
            (codex ? "Only verified Codex CLI " + CliChatBackend.SupportedCodexVersion + " is enabled. Other versions need an isolation check." : "Claude uses its own browser sign-in and supported restricted mode."));
    }

    private bool ConnectionBusy => pendingConnection != null || pendingModels != null;

    private void StartConnection(Func<CancellationToken, Task<string>> operation, bool refreshModels = false)
    {
        if (ConnectionBusy) return;
        connectionRequest = new CancellationTokenSource(); operationConnection = config.AiConnection;
        refreshModelsAfterConnection = refreshModels; connectionStatus = "";
        try { pendingConnection = operation(connectionRequest.Token); }
        catch { connectionStatus = "The connection could not start. Try again."; connectionRequest.Dispose(); connectionRequest = null; }
    }

    private void StartModelRefresh()
    {
        if (ConnectionBusy) return;
        connectionRequest = new CancellationTokenSource(); operationConnection = config.AiConnection;
        connectionStatus = "";
        try { pendingModels = chat.ChatGpt.ListModelsAsync(connectionRequest.Token); }
        catch { connectionStatus = "Models could not be requested. Try again."; connectionRequest.Dispose(); connectionRequest = null; }
    }

    private void UpdateConnection()
    {
        if (pendingConnection is { IsCompleted: true })
        {
            var current = operationConnection == config.AiConnection;
            var canceled = connectionRequest?.IsCancellationRequested == true;
            if (current) connectionStatus = pendingConnection.IsCompletedSuccessfully ? pendingConnection.Result : "The connection operation could not finish.";
            pendingConnection = null; connectionRequest?.Dispose(); connectionRequest = null;
            var refresh = current && !canceled && refreshModelsAfterConnection && chat.ChatGpt.IsConnected
                && connectionStatus.StartsWith("Connected to ChatGPT.", StringComparison.Ordinal);
            refreshModelsAfterConnection = false;
            if (refresh) StartModelRefresh();
        }
        if (pendingModels is { IsCompleted: true })
        {
            if (operationConnection == config.AiConnection && connectionRequest?.IsCancellationRequested != true)
            {
                if (pendingModels.IsCompletedSuccessfully) ApplyModels(pendingModels.Result);
                else
                {
                    _ = pendingModels.Exception; // Observe failure without displaying provider text or a stack trace.
                    connectionStatus = chat.ChatGpt.Status;
                    if (chatGptModels.Length > 0) connectionStatus += " Previous model choices were kept.";
                }
            }
            else if (operationConnection == config.AiConnection) connectionStatus = "Model refresh canceled. Previous choices were kept.";
            pendingModels = null; connectionRequest?.Dispose(); connectionRequest = null;
        }
    }

    private void ApplyModels(IReadOnlyList<AiModelOption> models)
    {
        chatGptModels = models.ToArray();
        var selected = ChatGptConnection.PreferredModel(models, config.ChatGptModel);
        var changed = selected != config.ChatGptModel;
        var saved = true;
        if (changed) { config.ChatGptModel = selected; saved = Save(); }
        if (models.Count == 0) connectionStatus = chat.ChatGpt.Status;
        else if (selected.Length == 0) connectionStatus = $"Loaded {models.Count} models. Choose a model for text chat; no automatic choice was made.";
        else
        {
            var name = models.First(model => model.Id == selected).Name;
            connectionStatus = changed
                ? $"Selected {name} as the lightweight default by model name. You can choose another model."
                : $"Loaded {models.Count} models. Kept your selection: {name}.";
        }
        if (!saved) connectionStatus += " This preference could not be saved.";
    }

    private void CancelAiOperations() { request?.Cancel(); connectionRequest?.Cancel(); }

    private static string ConnectionLabel(AiConnectionKind connection) => connection switch
    {
        AiConnectionKind.ChatGpt => "Continue with ChatGPT",
        AiConnectionKind.CodexCli => "Codex CLI",
        AiConnectionKind.ClaudeCli => "Claude CLI",
        _ => "API key / local server",
    };

    private static string MenuLabel(string value)
        => FitLabel(value, ImGui.GetWindowWidth() - 40 * ImGuiHelpers.GlobalScale);

    private static string FitLabel(string value, float maximum)
    {
        maximum = Math.Max(80, maximum);
        if (ImGui.CalcTextSize(value).X <= maximum) return value;
        var length = value.Length;
        while (length > 1 && ImGui.CalcTextSize(value[..length] + "...").X > maximum) length--;
        return value[..length] + "...";
    }

    private static void WrappedTooltip(string value)
    {
        UiText.Tooltip(value);
    }

    private void DrawVoiceBindings()
    {
        if (UiText.Button(voice.IsListening ? "Cancel listening" : "Practice a phrase"))
        { if (voice.IsListening) voice.Cancel(); else StartVoice(true); }
        UiText.Status(voice.Status);
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##phrase", "Phrase, for example suit up", ref phrase, 96);
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##action-filter", "Find the action to bind", ref actionFilter, 120);
        var chosen = entries.FirstOrDefault(e => e.Id == bindingAction);
        ImGui.SetNextItemWidth(-1);
        if (UiText.BeginCombo("##binding-action", chosen?.Title ?? "Choose what the phrase does"))
        {
            foreach (var entry in SearchEngine.Find(entries, actionFilter, 100))
                if (UiText.Selectable(entry.Title + "##" + entry.Id, bindingAction == entry.Id)) bindingAction = entry.Id;
            ImGui.EndCombo();
        }
        ImGui.BeginDisabled(SearchEngine.Normalize(phrase).Length == 0 || chosen == null || config.VoiceBindings.Count >= 100);
        if (UiText.Button("Save voice shortcut"))
        {
            var normalized = SearchEngine.Normalize(phrase);
            config.VoiceBindings.RemoveAll(b => SearchEngine.Normalize(b.Phrase) == normalized);
            config.VoiceBindings.Add(new VoiceBinding(phrase.Trim(), bindingAction));
            if (Save()) { phrase = ""; status = "Voice shortcut saved. Use Speak in the launcher to try it."; }
        }
        ImGui.EndDisabled();
        UiText.Help("Voice shortcut help", "Say job followed by a job name, or bind a phrase to an action. Practice fills in what Windows heard; it does not retrain a model. Recognition runs on Windows for one phrase per button press.");
        for (var i = 0; i < config.VoiceBindings.Count; i++)
        {
            var binding = config.VoiceBindings[i]; ImGui.PushID(i);
            if (ImGui.SmallButton("Remove")) { config.VoiceBindings.RemoveAt(i--); Save(); ImGui.PopID(); continue; }
            ImGui.SameLine(); ImGui.TextWrapped(binding.Phrase + " → " + (entries.FirstOrDefault(e => e.Id == binding.ActionId)?.Title ?? "Unavailable action"));
            ImGui.PopID();
        }
    }

    private void DrawData()
    {
        ImGui.TextWrapped("Exports preferences, gearsets and voice phrases. No credentials, recordings or chat history.");
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##export-name", "Settings filename", ref exportName, 80);
        if (UiText.Button("Export settings to JSON"))
        {
            try
            {
                var name = string.Concat(exportName.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
                if (name.Length == 0) name = "omnikit-settings";
                var folder = Path.Combine(Services.PluginInterface.ConfigDirectory.FullName, "exports"); Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                File.WriteAllText(path, store.Export(config));
                status = "JSON exported. Use Open settings folder to find it in exports.";
            }
            catch { status = "Export failed. Check available disk space and folder access."; }
        }
        if (UiText.Button("Copy settings JSON")) { ImGui.SetClipboardText(store.Export(config)); status = "Settings JSON copied. It contains no saved credentials."; }
        ImGui.SameLine(); if (UiText.Button("Open settings folder")) Util.OpenLink(Services.PluginInterface.ConfigDirectory.FullName);
        if (confirmImport) ImGui.SetNextItemOpen(true);
        if (UiText.CollapsingHeader("Import settings"))
        {
        ImGui.TextWrapped("Replaces preferences and voice shortcuts; AI turns off. Gearset numbers belong to the current character.");
        if (UiText.Button("Choose JSON file"))
            fileDialogs.OpenFileDialog("Import OmniKit settings", ".json", (ok, path) =>
            {
                if (!ok) return;
                try
                {
                    using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (input.Length > 262144) throw new IOException();
                    using var reader = new StreamReader(input);
                    importJson = reader.ReadToEnd(); confirmImport = true; settingsOpen = true; section = 4; selectSettingsTab = true;
                }
                catch { status = "Could not read that JSON file. Choose an OmniKit export under 256 KB."; }
            });
        ImGui.InputTextMultiline("##import", ref importJson, 262144, new Vector2(-1, 120 * ImGuiHelpers.GlobalScale));
        if (UiText.Button("Import pasted JSON") || confirmImport) { ImGui.OpenPopup("Replace settings?"); confirmImport = false; }
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos + viewport.WorkSize / 2, ImGuiCond.Always, new Vector2(.5f));
        ImGui.SetNextWindowSize(new Vector2(Math.Min(360 * ImGuiHelpers.GlobalScale, viewport.WorkSize.X - 32 * ImGuiHelpers.GlobalScale), 0), ImGuiCond.Always);
        if (ImGui.BeginPopupModal("Replace settings?", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
        {
            ImGui.TextWrapped("Replace preferences and voice shortcuts? AI will be turned off.");
            if (UiText.Button("Import"))
            {
                try
                {
                    var imported = store.Upgrade(config);
                    store.ImportInto(imported, importJson); imported.AiEnabled = false;
                    store.Save(imported);
                    store.ImportInto(config, store.Export(imported));
                    CancelAiOperations(); chatGptModels = []; apiKey = ""; connectionStatus = "";
                    providerEndpoint = config.AiEndpoint;
                    request?.Cancel(); voice.Cancel();
                    status = "Settings imported. AI is off until you enable it."; importJson = "";
                }
                catch { status = "Import rejected. Use an OmniKit export with valid settings. Your settings were kept."; }
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine(); if (UiText.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        }
        UiText.Help("Plugins & Umbra", "Loaded plugin windows appear in Search. Plugins may share named actions without collecting private plugin data. OmniKit appears in Umbra's Plugins menu; load the OmniKit.Umbra sidecar in Umbra Settings → Plugins for its own toolbar widget.");
        var revealBrowser = expandBrowserSettings;
        if (revealBrowser) { ImGui.SetNextItemOpen(true); expandBrowserSettings = false; }
        if (UiText.CollapsingHeader("In-game browser"))
        {
            if (revealBrowser) ImGui.SetScrollHereY(.1f);
            DrawBrowserSettings();
        }
        if (UiText.Button("Open setup again")) { config.SetupCompleted = false; Save(); OpenCompanion(); }
    }

    private void DrawBrowserSettings()
    {
        UiText.Status(wiki.Status);
        if (UiText.Button("Open browser")) OpenBrowser();
        ImGui.SameLine(); if (UiText.Button("Browser controls")) ToggleBrowserControls();
        UiText.Hint("The website stays visible. The browser's icon shows or hides navigation, address and extension controls. Manage unpacked Manifest V3 extensions inside those controls.");
        if (ImGui.Checkbox("Capture game & plugin web links", ref config.BrowserInterceptLinks))
        { Save(); if (config.BrowserInterceptLinks) OpenBrowser(); }
        UiText.Hint("Optional: route standard HTTP/HTTPS link opens from this game process when OmniKit's browser is ready. Recognized authentication and non-web links pass through. Custom browser launchers and other processes cannot be captured.");
        if (config.BrowserInterceptLinks && BrowserInterceptionStatus != null) UiText.Status(BrowserInterceptionStatus());
    }

    private void DrawAbout()
    {
        Heading("OmniKit · by Zero");
        UiText.Line(BuildVersion.Display, Gold, outline: true);
        if (UiText.Button("Copy build details"))
        {
            ImGui.SetClipboardText($"OmniKit {BuildVersion.Display}\nby Zero\nSource revision: {BuildVersion.CommitCount}\nCommit: {BuildVersion.CommitSha}");
            status = "Complete build details copied.";
        }
        const string projectUrl = "https://github.com/fostot/OmniKit";
        if (UiText.Button("Copy project link")) { ImGui.SetClipboardText(projectUrl); status = "Project link copied."; }
        UiText.Hint(projectUrl);
        UiText.Help("Source details", "Source revision " + BuildVersion.CommitCount + "\n" + BuildVersion.CommitSha);
        UiText.Help("Credits", "Built for Dalamud, with native Umbra integration. Companion artwork was generated with OpenAI image tools. Dependency licenses and artwork provenance are included in the project.");
    }

    private static void Heading(string title) { ImGui.Spacing(); UiText.Line(title, Gold, outline: true); }

    private static string AvatarLabel(string avatar) => avatar switch
    {
        "Bimble" => "Bimble · moon bunny",
        "Moonbear" => "Moonbear · space bear",
        "Moonblob" => "Moonblob · rolling blob",
        "Wink" => "Wink · blinking eye",
        "Nova" => "Nova · neon burst",
        _ => avatar,
    };

    private static string OutfitLabel(string outfit) => outfit == "Stargazer" ? "Stargazer (violet/silver)" : "Navigator (navy/gold)";

    private bool Save()
    {
        try { config.Save(); status = ""; return true; }
        catch { status = "Could not save settings. Check the values and local storage access."; return false; }
    }

    public void Dispose()
    {
        interaction.ConfigureHudRequested -= OpenHudSettings;
        wiki?.Dispose();
        hotkeys.Cancel(); roaming.CancelChooseArea(); CancelAiOperations(); request?.Dispose(); connectionRequest?.Dispose(); apiKey = "";
    }
}
