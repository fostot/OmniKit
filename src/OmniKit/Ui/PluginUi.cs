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
    private bool mainOpen;
    private bool settingsOpen;
    private bool focusSearch;
    private bool askMode;
    private bool practiceVoice;
    private string query = "";
    private string answer = "";
    private string status = "";
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
    private int selected;
    private long refreshedAt;
    private SearchEntry[] entries = [];
    private Task<string>? pendingAnswer;
    private CancellationTokenSource? request;
    private static readonly string[] Sections = ["Shortcuts", "Interact & gear", "Companion", "Data"];
    private static readonly Vector4 Gold = new(.84f, .70f, .44f, 1);
    private static readonly Vector4 Muted = new(.63f, .65f, .69f, 1);
    public bool IsOpen => mainOpen || settingsOpen;
    public bool CapturesText { get; private set; }
    public bool SearchHasFocus { get; private set; }

    public PluginUi(Configuration config, SoftInteractionService interaction, ChatService chat,
        VoiceInput voice, LocalStore store, SourceRegistry sources)
    {
        this.config = config; this.interaction = interaction; this.chat = chat;
        this.voice = voice; this.store = store; this.sources = sources;
        providerEndpoint = config.AiEndpoint;
        catalog = new ActionCatalog(OpenSettings);
        wiki = new WikiBrowser(config);
        mainOpen = !config.SetupCompleted;
        focusSearch = true;
    }

    public void OpenMain() { mainOpen = true; focusSearch = true; Refresh(); }
    public void ToggleMain() { if (mainOpen) mainOpen = false; else OpenMain(); }
    public void OpenSettings() { settingsOpen = true; Refresh(); }
    private void Refresh()
    {
        entries = catalog.GetEntries().Concat(GearsetActions.GetEntries()).Concat(sources.GetEntries()).ToArray();
        refreshedAt = Environment.TickCount64;
    }

    public void Update()
    {
        if (gearFeedback) status = GearsetActions.Status;
        if (IsOpen && Environment.TickCount64 - refreshedAt > 2000) Refresh();
        if (!config.VoiceEnabled && voice.IsListening) voice.Cancel();
        if (pendingAnswer is { IsCompleted: true })
        {
            answer = pendingAnswer.IsCompletedSuccessfully ? pendingAnswer.Result : "The request could not finish. Please try again.";
            pendingAnswer = null; request?.Dispose(); request = null;
        }
        if (!voice.TryTakeResult(out var heard)) return;
        if (!config.VoiceEnabled || !ShortcutInput.GameAcceptsInput()) { status = "Voice result discarded because the game is not ready."; return; }
        if (practiceVoice)
        {
            phrase = heard; practiceVoice = false;
            status = "Heard: " + heard + ". Choose an action below, then save the phrase.";
            return;
        }
        query = heard; mainOpen = true;
        if (askMode) { status = "Heard your question. Press Ask companion to send it, or Search wiki to look it up."; return; }
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
        else status = "Heard: " + heard + ". Choose a result, or select Ask.";
    }

    public void Draw()
    {
        CapturesText = ImGui.GetIO().WantTextInput;
        if (!mainOpen) SearchHasFocus = false;
        fileDialogs.Draw();
        if (!IsOpen) return;
        PushTheme();
        try { if (mainOpen) DrawMain(); if (settingsOpen) DrawSettings(); }
        finally { ImGui.PopStyleColor(11); ImGui.PopStyleVar(5); }
    }

    private static void PushTheme()
    {
        var s = ImGuiHelpers.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(22, 18) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 10) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 8) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6 * s);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.095f, .105f, .13f, .98f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.93f, .92f, .88f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.15f, .17f, .20f, 1));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.25f, .22f, .17f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.36f, .31f, .22f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.43f, .35f, .23f, 1));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.28f, .25f, .20f, 1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.34f, .30f, .23f, 1));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Gold);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Gold);
    }

    private static void WindowSize(Vector2 size, Vector2 minimum)
    {
        var s = ImGuiHelpers.GlobalScale;
        ImGui.SetNextWindowSize(size * s, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(minimum * s, new Vector2(float.MaxValue));
    }

    private void DrawMain()
    {
        WindowSize(new(690, 580), new(600, 480));
        if (!ImGui.Begin("OmniKit###OmniKit.Main", ref mainOpen, ImGuiWindowFlags.NoCollapse)) { ImGui.End(); return; }
        try
        {
            Header(config.SetupCompleted ? "Your XIV toolkit" : "A little help from the moon");
            if (!config.SetupCompleted) { DrawWelcome(); return; }
            if (ImGui.Button(askMode ? "Search" : "Search selected")) askMode = false;
            ImGui.SameLine(); if (ImGui.Button(askMode ? "Ask selected" : "Ask")) askMode = true;
            ImGui.SameLine(); if (ImGui.Button("Settings")) OpenSettings();
            if (config.VoiceEnabled)
            {
                ImGui.SameLine();
                if (ImGui.Button(voice.IsListening ? "Stop listening" : "Speak"))
                { if (voice.IsListening) voice.Cancel(); else StartVoice(false); }
            }
            ImGui.SetNextItemWidth(-1);
            if (focusSearch) { ImGui.SetKeyboardFocusHere(); focusSearch = false; }
            var submit = ImGui.InputTextWithHint("##query", askMode ? "Ask about XIV..." : "Try community, job paladin, or inventory...",
                ref query, 4000, ImGuiInputTextFlags.EnterReturnsTrue);
            SearchHasFocus = ImGui.IsItemActive();
            if (ImGui.IsItemEdited()) { selected = 0; gearFeedback = false; }
            if (askMode) DrawAnswer(submit); else DrawResults(submit);
            ImGui.BeginChild("main-status", new Vector2(0, 0));
            ImGui.TextWrapped(voice.IsListening ? voice.Status : "Local commands work without AI.  /omnikit settings");
            if (status.Length > 0) ImGui.TextWrapped(status);
            ImGui.EndChild();
        }
        finally { ImGui.End(); }
    }

    private void Header(string subtitle)
    {
        var s = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        if (config.AvatarEnabled)
        {
            var path = Path.Combine(Services.PluginInterface.AssemblyLocation.DirectoryName!, "assets", config.Avatar == "Moonbear" ? "moonbear.png" : "bimble.png");
            var texture = Services.Textures.GetFromFile(path).GetWrapOrDefault();
            if (texture != null)
            {
                var bob = config.AnimationsEnabled ? (float)Math.Sin(ImGui.GetTime() * 2) * 2 * s : 0;
                ImGui.GetWindowDrawList().AddImage(texture.Handle, origin + new Vector2(0, bob), origin + new Vector2(76, 76) * s + new Vector2(0, bob));
            }
        }
        ImGui.SetCursorScreenPos(origin + new Vector2(config.AvatarEnabled ? 92 : 0, 8) * s);
        ImGui.TextColored(Gold, "O M N I K I T");
        ImGui.SetCursorScreenPos(origin + new Vector2(config.AvatarEnabled ? 92 : 0, 36) * s);
        ImGui.TextUnformatted(subtitle);
        ImGui.SetCursorScreenPos(origin + new Vector2(0, 86) * s);
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawWelcome()
    {
        ImGui.BeginChild("welcome", new Vector2(0, 0));
        ImGui.TextWrapped("Start simple. You can change everything later.");
        ImGui.Spacing(); ImGui.TextColored(Gold, "Use without AI");
        ImGui.TextWrapped("OmniKit has a local command matcher. It recognizes names, common words like guild or community, and your saved voice shortcuts. It opens game windows and plugins, switches saved gearsets, and runs the interact HUD. No AI account is needed.");
        ImGui.TextColored(Muted, "It does not invent answers or hold an open-ended conversation.");
        if (ImGui.Button("Use without AI", new Vector2(-1, 0)))
        { config.AiEnabled = false; config.SetupCompleted = true; Save(); focusSearch = true; }
        ImGui.Spacing(); ImGui.TextColored(Gold, "Add an AI companion");
        ImGui.TextWrapped("AI adds flexible explanations and conversation about XIV. Questions you submit are sent to your chosen provider, which may charge for use. Local commands still run on your PC. AI replies cannot control the game.");
        if (ImGui.Button("Set up AI", new Vector2(-1, 0)))
        { config.AiEnabled = false; config.SetupCompleted = true; Save(); section = 2; OpenSettings(); }
        ImGui.TextWrapped("This preview supports an API key or a compatible local server. Account sign-in is planned; a ChatGPT or Claude subscription is not an API key.");
        ImGui.EndChild();
    }

    private void DrawResults(bool submit)
    {
        var results = SearchEngine.Find(entries, query);
        selected = Math.Clamp(selected, 0, Math.Max(0, results.Count - 1));
        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
        {
            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) selected = Math.Min(selected + 1, results.Count - 1);
            if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)) selected = Math.Max(selected - 1, 0);
        }
        if (submit && results.Count > 0) Activate(results[selected]);
        var footer = ImGui.GetTextLineHeightWithSpacing() * (status.Length > 0 ? 4 : 2);
        ImGui.BeginChild("results", new Vector2(0, -footer));
        if (results.Count == 0)
        {
            ImGui.TextWrapped("No matching command. Try a shorter name, or search a XIV wiki.");
            if (query.Trim().Length > 0 && ImGui.Button("Search XIV wiki in browser")) OpenWiki(query);
        }
        for (var i = 0; i < results.Count; i++)
        {
            var entry = results[i];
            ImGui.PushID(entry.Id);
            var start = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var rowHeight = ImGui.GetTextLineHeight() * 2 + 16 * ImGuiHelpers.GlobalScale;
            if (ImGui.Selectable("##action", selected == i, ImGuiSelectableFlags.None, new Vector2(0, rowHeight))) Activate(entry);
            var draw = ImGui.GetWindowDrawList();
            var inset = new Vector2(10, 5) * ImGuiHelpers.GlobalScale;
            draw.AddText(start + inset, ImGui.GetColorU32(ImGuiCol.Text), Fit(entry.Title, width - 24));
            draw.AddText(start + inset + new Vector2(0, ImGui.GetTextLineHeight() + 3), ImGui.ColorConvertFloat4ToU32(Muted), Fit(entry.Source + "  /  " + entry.Description, width - 24));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(entry.Title + "\n" + entry.Description);
            ImGui.PopID();
        }
        ImGui.EndChild();
    }

    private static string Fit(string value, float width)
    {
        if (ImGui.CalcTextSize(value).X <= width) return value;
        while (value.Length > 1 && ImGui.CalcTextSize(value + "...").X > width) value = value[..^1];
        return value + "...";
    }

    private void Activate(SearchEntry entry)
    {
        if (!ShortcutInput.GameAcceptsInput()) { status = "Return to the game and close chat typing first."; return; }
        try
        {
            entry.Activate();
            gearFeedback = entry.Id.StartsWith("gearset:", StringComparison.Ordinal);
            mainOpen = gearFeedback;
            status = gearFeedback ? "Requesting gear change..." : "";
        }
        catch (Exception ex)
        { status = "That action could not open. Check that its plugin is loaded."; Services.Log.Warning("OmniKit action failed ({ErrorType}).", ex.GetType().Name); }
    }

    private void DrawAnswer(bool submit)
    {
        if (!config.AiEnabled)
        {
            ImGui.TextWrapped("AI is off. Search and voice shortcuts still work locally.");
            if (ImGui.Button("Connect AI")) { section = 2; OpenSettings(); }
        }
        else
        {
            ImGui.BeginDisabled(pendingAnswer != null || string.IsNullOrWhiteSpace(query));
            if (ImGui.Button("Ask companion") || (submit && pendingAnswer == null))
            { request = new CancellationTokenSource(); pendingAnswer = chat.AskAsync(query, request.Token); answer = "Thinking..."; }
            ImGui.EndDisabled();
            if (pendingAnswer != null) { ImGui.SameLine(); if (ImGui.Button("Cancel")) request?.Cancel(); }
        }
        ImGui.SameLine(); if (ImGui.Button("Search wiki")) OpenWiki(query);
        var footer = ImGui.GetTextLineHeightWithSpacing() * (status.Length > 0 ? 4 : 2);
        ImGui.BeginChild("answer", new Vector2(0, -footer));
        ImGui.TextWrapped(answer.Length == 0 ? "Ask a question, or look it up on the XIV wiki. Wiki links currently open in your browser." : answer);
        ImGui.EndChild();
    }

    private void OpenWiki(string text)
    {
        wiki.Open(text); status = wiki.Status;
    }

    private void StartVoice(bool practice)
    {
        practiceVoice = practice;
        voice.Start(config.VoiceBindings.Select(b => b.Phrase).Concat(GearsetActions.GetPhrases()));
        status = voice.Status;
    }

    private void DrawSettings()
    {
        WindowSize(new(700, 660), new(620, 500));
        if (!ImGui.Begin("OmniKit Settings###OmniKit.Settings", ref settingsOpen, ImGuiWindowFlags.NoCollapse)) { ImGui.End(); return; }
        try
        {
            ImGui.TextColored(Gold, "MAKE IT YOURS");
            ImGui.TextColored(Muted, "Changes save automatically on this PC.");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##settings-search", "Find a setting: key, voice, gear, AI, export...", ref settingsSearch, 100))
            {
                var filter = settingsSearch.ToLowerInvariant();
                if (new[] { "interact", "hud", "gather", "gear", "target" }.Any(filter.Contains)) section = 1;
                else if (new[] { "voice", "ai", "avatar", "companion", "brain", "bind", "train" }.Any(filter.Contains)) section = 2;
                else if (new[] { "data", "export", "import", "json", "source", "umbra" }.Any(filter.Contains)) section = 3;
                else if (filter.Contains("key") || filter.Contains("shortcut")) section = 0;
            }
            for (var i = 0; i < Sections.Length; i++)
            {
                if (i > 0) ImGui.SameLine();
                if (ImGui.Button((section == i ? "• " : "") + Sections[i])) section = i;
            }
            ImGui.Spacing();
            ImGui.BeginChild("settings-body", new Vector2(0, -ImGui.GetTextLineHeightWithSpacing() * 3));
            switch (section)
            {
                case 0: DrawShortcuts(); break;
                case 1: DrawInteraction(); break;
                case 2: DrawCompanion(); break;
                case 3: DrawData(); break;
            }
            ImGui.EndChild();
            ImGui.BeginChild("settings-status", new Vector2(0, 0));
            ImGui.TextWrapped(status.Length > 0 ? status : "Tip: every feature here is optional. Start with Search, then turn on what helps.");
            ImGui.EndChild();
        }
        finally { ImGui.End(); }
    }

    private void DrawShortcuts()
    {
        Heading("Open OmniKit");
        if (ImGui.Checkbox("Enable launcher shortcut", ref config.LauncherEnabled)) Save();
        var old = (config.LauncherKey, config.LauncherModifier, config.SettingsKey, config.SettingsModifier, config.InteractKey);
        var changed = KeyPicker("Search menu", ref config.LauncherKey, ref config.LauncherModifier);
        changed |= KeyPicker("Settings", ref config.SettingsKey, ref config.SettingsModifier);
        if (changed)
        {
            if ((config.LauncherKey == config.SettingsKey && config.LauncherModifier == config.SettingsModifier)
                || (config.LauncherModifier == 0 && config.LauncherKey == config.InteractKey)
                || (config.SettingsModifier == 0 && config.SettingsKey == config.InteractKey))
            {
                (config.LauncherKey, config.LauncherModifier, config.SettingsKey, config.SettingsModifier, config.InteractKey) = old;
                status = "Choose different shortcuts for Search, Settings, and Interact.";
            }
            else Save();
        }
        ImGui.TextWrapped("Press your shortcut while playing. You can always type /omnikit or /omnikit settings in chat.");
        ImGui.TextWrapped("Alt + Space may already belong to a desktop launcher. Change one shortcut if both open.");
        Heading("Try a command");
        ImGui.TextWrapped("community • guild • free company\njob paladin • job miner • a saved gearset name\ninventory • crafting log • a plugin name");
        if (ImGui.Button("Open Search")) OpenMain();
    }

    private void DrawInteraction()
    {
        Heading("Look, then interact");
        if (ImGui.Checkbox("Enable soft interaction", ref config.SoftInteractEnabled)) Save();
        ImGui.TextWrapped("Look toward a nearby NPC or usable object, then press your interact key. Nothing happens automatically just because you look at it.");
        if (!config.SoftInteractEnabled) return;
        var old = config.InteractKey;
        if (KeyCombo("Interact key", ref config.InteractKey))
        {
            if ((config.LauncherModifier == 0 && config.InteractKey == config.LauncherKey) || (config.SettingsModifier == 0 && config.InteractKey == config.SettingsKey))
            { config.InteractKey = old; status = "The interact key needs its own shortcut."; }
            else Save();
        }
        if (ImGui.Checkbox("Show the current target on screen", ref config.InteractionHudEnabled)) Save();
        if (config.InteractionHudEnabled)
        {
            if (ImGui.SliderFloat("Prompt height", ref config.InteractionHudOffsetY, -400, 400, "%.0f")) Save();
            ImGui.TextColored(Muted, "Negative moves it up. Positive moves it down.");
        }
        ImGui.TextWrapped(interaction.CandidateName is { } target ? "Current target: " + target : interaction.Hint ?? "Look at a nearby interactable object to find a target.");
        Heading("Gathering outfits");
        if (ImGui.Checkbox("Switch to gathering gear when I interact", ref config.GatheringAutoSwitch)) Save();
        if (config.GatheringAutoSwitch)
        {
            ImGui.TextWrapped("Save a Miner or Botanist gearset in the game's Character window first. OmniKit changes to it before opening a node.");
            GearCombo("Mining gear", ref config.MinerGearset, "Miner");
            GearCombo("Botany gear", ref config.BotanistGearset, "Botanist");
            if (ImGui.Checkbox("Return to my previous gear after gathering", ref config.GatheringRestore)) Save();
            ImGui.TextWrapped("Returning stops if you change your gear or job yourself. OmniKit does not choose items or gather for you.");
            ImGui.TextWrapped(interaction.Gathering.Status);
        }
        if (ImGui.CollapsingHeader("Fine-tune targeting"))
        {
            if (ImGui.SliderFloat("Aim area", ref config.InteractionCone, .025f, .30f, "%.3f")) Save();
            if (ImGui.SliderFloat("Maximum distance", ref config.InteractionDistance, 1, 6, "%.1f yalms")) Save();
            ImGui.TextWrapped("A smaller aim area is more precise. The game still checks whether you can use the target.");
        }
    }

    private void GearCombo(string label, ref int value, string job)
    {
        var sets = interaction.Gathering.GetGearsets();
        var selectedValue = value;
        var current = sets.FirstOrDefault(g => g.Index == selectedValue);
        if (!ImGui.BeginCombo(label, value < 0 ? "Choose automatically" : current?.Name ?? "Saved set unavailable")) return;
        if (ImGui.Selectable("Choose automatically", value < 0)) { value = -1; Save(); }
        foreach (var set in sets)
        {
            var name = Services.Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>(Dalamud.Game.ClientLanguage.English).GetRow(set.ClassJobId).Name.ToString();
            if (!name.Equals(job, StringComparison.OrdinalIgnoreCase)) continue;
            if (ImGui.Selectable($"{set.Index + 1}. {set.Name}", value == set.Index)) { value = set.Index; Save(); }
        }
        ImGui.EndCombo();
    }

    private void DrawCompanion()
    {
        Heading("Your moon companion");
        if (ImGui.Checkbox("Show companion", ref config.AvatarEnabled)) Save();
        if (config.AvatarEnabled)
        {
            if (ImGui.BeginCombo("Appearance", config.Avatar))
            {
                foreach (var avatar in new[] { "Bimble", "Moonbear" })
                    if (ImGui.Selectable(avatar, config.Avatar == avatar)) { config.Avatar = avatar; Save(); }
                ImGui.EndCombo();
            }
            if (ImGui.Checkbox("Animate companion", ref config.AnimationsEnabled)) Save();
        }
        Heading("Voice shortcuts");
        if (ImGui.Checkbox("Enable voice input", ref config.VoiceEnabled)) Save();
        ImGui.TextWrapped("Speech recognition runs on Windows. The microphone opens only when you press Speak or Practice, then stops after one phrase. Typing always works.");
        if (config.VoiceEnabled) DrawVoiceBindings();
        Heading("Optional AI");
        ImGui.TextWrapped("Your local command matcher works without AI: names, common words, saved voice phrases, jobs and game actions. AI adds open-ended answers about XIV. Only questions you submit with Ask are sent to the provider.");
        if (ImGui.Checkbox("Use AI for questions", ref config.AiEnabled))
        { if (!config.AiEnabled) request?.Cancel(); Save(); }
        if (!config.AiEnabled) return;
        ImGui.TextWrapped("1. Choose your provider and model.\n2. Paste its API key and save it.\n3. Open Search, select Ask, and try a question.");
        ImGui.TextWrapped("API use may cost money. Account sign-in is not available in this preview.");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##model", "Provider model name", ref config.AiModel, 200)) Save();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##api-key", "Paste API key here", ref apiKey, 4096, ImGuiInputTextFlags.Password);
        if (ImGui.Button("Save key"))
        {
            try { chat.SaveApiKey(apiKey); apiKey = ""; status = "Key saved securely for this provider on this Windows account."; }
            catch { status = "The key could not be saved. Check the provider address and try again."; }
        }
        ImGui.SameLine(); if (ImGui.Button("Remove saved key"))
        {
            try { chat.ForgetApiKey(); status = "Saved key removed for this provider."; }
            catch { status = "The saved key could not be removed. Check local storage access."; }
        }
        ImGui.TextColored(Muted, chat.HasApiKey ? "A key is saved for this provider." : "No key is saved for this provider.");
        if (ImGui.CollapsingHeader("Other provider or local AI server"))
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##provider", ref providerEndpoint, 500);
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                if (Uri.TryCreate(providerEndpoint, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || (uri.Scheme == "http" && System.Net.IPAddress.TryParse(uri.Host, out var ip) && System.Net.IPAddress.IsLoopback(ip)))
                    && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0)
                { config.AiEndpoint = providerEndpoint; Save(); }
                else status = "Use a full HTTPS Responses address, or HTTP on 127.0.0.1 for a local server.";
            }
            ImGui.TextWrapped("Use a Responses-compatible endpoint. Local servers can work without a key. Changing the address does not send your old key to the new provider.");
        }
    }

    private void DrawVoiceBindings()
    {
        ImGui.TextWrapped("Say job followed by a job name, or teach a shortcut by linking a phrase to an action below. Practice fills in what Windows heard; it does not retrain an AI model.");
        if (ImGui.Button(voice.IsListening ? "Cancel listening" : "Practice a phrase"))
        { if (voice.IsListening) voice.Cancel(); else StartVoice(true); }
        ImGui.TextWrapped(voice.Status);
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##phrase", "Phrase, for example suit up", ref phrase, 96);
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##action-filter", "Find the action to bind", ref actionFilter, 120);
        var chosen = entries.FirstOrDefault(e => e.Id == bindingAction);
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##binding-action", chosen?.Title ?? "Choose what the phrase does"))
        {
            foreach (var entry in SearchEngine.Find(entries, actionFilter, 100))
                if (ImGui.Selectable(entry.Title + "##" + entry.Id, bindingAction == entry.Id)) bindingAction = entry.Id;
            ImGui.EndCombo();
        }
        ImGui.BeginDisabled(SearchEngine.Normalize(phrase).Length == 0 || chosen == null || config.VoiceBindings.Count >= 100);
        if (ImGui.Button("Save voice shortcut"))
        {
            var normalized = SearchEngine.Normalize(phrase);
            config.VoiceBindings.RemoveAll(b => SearchEngine.Normalize(b.Phrase) == normalized);
            config.VoiceBindings.Add(new VoiceBinding(phrase.Trim(), bindingAction));
            if (Save()) { phrase = ""; status = "Voice shortcut saved. Use Speak in the launcher to try it."; }
        }
        ImGui.EndDisabled();
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
        Heading("Your settings stay here");
        ImGui.TextWrapped("Settings and voice shortcuts are saved in a local database. Exports include your preferences, chosen gearset numbers and voice phrases. They never include API keys, recordings or chat history.");
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##export-name", "Settings filename", ref exportName, 80);
        if (ImGui.Button("Export settings to JSON"))
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
        if (ImGui.Button("Copy settings JSON")) { ImGui.SetClipboardText(store.Export(config)); status = "Settings JSON copied. It contains no saved credentials."; }
        ImGui.SameLine(); if (ImGui.Button("Open settings folder")) Util.OpenLink(Services.PluginInterface.ConfigDirectory.FullName);
        Heading("Import settings");
        ImGui.TextWrapped("Paste settings JSON below. Import replaces preferences and voice shortcuts. AI remains off until you enable it again; save any provider key separately. Gearset numbers refer to the current character's sets.");
        if (ImGui.Button("Choose JSON file"))
            fileDialogs.OpenFileDialog("Import OmniKit settings", ".json", (ok, path) =>
            {
                if (!ok) return;
                try
                {
                    using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (input.Length > 262144) throw new IOException();
                    using var reader = new StreamReader(input);
                    importJson = reader.ReadToEnd(); confirmImport = true; settingsOpen = true; section = 3;
                }
                catch { status = "Could not read that JSON file. Choose an OmniKit export under 256 KB."; }
            });
        ImGui.InputTextMultiline("##import", ref importJson, 262144, new Vector2(-1, 120 * ImGuiHelpers.GlobalScale));
        if (ImGui.Button("Import pasted JSON") || confirmImport) { ImGui.OpenPopup("Replace settings?"); confirmImport = false; }
        if (ImGui.BeginPopupModal("Replace settings?", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Replace your preferences and voice shortcuts with this JSON?");
            if (ImGui.Button("Import"))
            {
                try
                {
                    store.ImportInto(config, importJson); config.AiEnabled = false; providerEndpoint = config.AiEndpoint;
                    request?.Cancel(); voice.Cancel();
                    if (Save()) { status = "Settings imported. AI is off until you enable it."; importJson = ""; }
                }
                catch { status = "Import rejected. Use an OmniKit export with valid settings. Your settings were kept."; }
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine(); if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        Heading("Other plugins & Umbra");
        ImGui.TextWrapped("Loaded plugins with windows appear in Search automatically. Plugins can also share named actions through OmniKit's integration interface. Private plugin data is not collected.");
        ImGui.TextWrapped("OmniKit appears in Umbra's Plugins menu. For its own toolbar widget, load OmniKit.Umbra.dll in Umbra Settings → Plugins, then add the OmniKit widget.");
        Heading("Wiki pages in game");
        if (ImGui.Checkbox("Open wiki searches inside Browsingway", ref config.WikiInGame)) Save();
        if (config.WikiInGame)
        {
            ImGui.TextWrapped("Install Browsingway. Open /bw config, press +, and name an overlay omnikit. Keep it visible and enabled; turn off Click Through, Type Through and ACT/IINACT optimizations. OmniKit sends wiki searches to that overlay.");
            ImGui.TextColored(Muted, wiki.IsAvailable ? "Browsingway is loaded." : "Browsingway is not loaded.");
            var name = config.WikiOverlayName;
            if (ImGui.InputText("Overlay name", ref name, 40) && name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            { config.WikiOverlayName = name; Save(); }
            if (ImGui.Button("Test wiki overlay")) OpenWiki("gathering");
        }
        if (ImGui.Button("Open setup again")) { config.SetupCompleted = false; Save(); OpenMain(); }
    }

    private static void Heading(string title) { ImGui.Spacing(); ImGui.TextColored(Gold, title); ImGui.Spacing(); }

    private static bool KeyPicker(string label, ref VirtualKey key, ref VirtualKey modifier)
    {
        ImGui.PushID(label); ImGui.TextUnformatted(label);
        var changed = false;
        ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
        var name = modifier == VirtualKey.MENU ? "Alt" : modifier == VirtualKey.CONTROL ? "Ctrl" : modifier == VirtualKey.SHIFT ? "Shift" : "No modifier";
        if (ImGui.BeginCombo("##modifier", name))
        {
            foreach (var item in new[] { ((VirtualKey)0, "No modifier"), (VirtualKey.CONTROL, "Ctrl"), (VirtualKey.MENU, "Alt"), (VirtualKey.SHIFT, "Shift") })
                if (ImGui.Selectable(item.Item2, modifier == item.Item1)) { modifier = item.Item1; changed = true; }
            ImGui.EndCombo();
        }
        ImGui.SameLine(); changed |= KeyCombo("##key", ref key);
        ImGui.PopID(); return changed;
    }

    private static bool KeyCombo(string label, ref VirtualKey key)
    {
        var changed = false; ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
        if (!ImGui.BeginCombo(label, key.ToString())) return false;
        foreach (var code in Enumerable.Range(0x30, 10).Concat(Enumerable.Range(0x41, 26)).Concat(Enumerable.Range(0x70, 24)).Concat(new[] { 0x20, 0x09, 0x2D, 0x2E, 0x24, 0x23, 0x21, 0x22 }))
        {
            var value = (VirtualKey)code;
            if (ImGui.Selectable(value.ToString(), value == key)) { key = value; changed = true; }
        }
        ImGui.EndCombo(); return changed;
    }

    private bool Save()
    {
        try { config.Save(); status = "Saved."; return true; }
        catch { status = "Could not save settings. Check the values and local storage access."; return false; }
    }

    public void Dispose() { request?.Cancel(); request?.Dispose(); apiKey = ""; }
}
