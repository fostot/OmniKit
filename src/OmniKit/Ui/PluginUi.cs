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
    private int shownCompanionTab = -1;
    private long refreshedAt;
    private SearchEntry[] entries = [];
    private Task<string>? pendingAnswer;
    private CancellationTokenSource? request;
    private static readonly string[] Sections = ["Launcher", "Interact", "HUD", "Companion", "Data"];
    private static readonly Vector4 Gold = new(.84f, .70f, .44f, 1);
    private static readonly Vector4 Muted = new(.63f, .65f, .69f, 1);
    public bool IsOpen => mainOpen || settingsOpen || companionOpen || hotkeys.IsCapturing || roaming.IsChoosingArea;
    public bool CapturingBinding => hotkeys.IsCapturing;
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
        catalog = new ActionCatalog(OpenSettings);
        wiki = new WikiBrowser(config);
        roaming = new RoamingCompanion(config, () => OpenCompanion());
        companionOpen = !config.SetupCompleted;
        interaction.ConfigureHudRequested += OpenHudSettings;
        focusSearch = true;
    }

    public void OpenMain() { query = ""; status = ""; gearFeedback = false; mainOpen = true; launcher.Focus(); Refresh(); }
    public void ToggleMain() { if (mainOpen) mainOpen = false; else OpenMain(); }
    public void OpenSettings() { hotkeys.Cancel(); settingsOpen = true; selectSettingsTab = true; Refresh(); }
    private void OpenHudSettings() { section = 2; selectSettingsTab = true; OpenSettings(); }
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
        CapturesText = ImGui.GetIO().WantTextInput;
        if (!mainOpen) SearchHasFocus = false;
        fileDialogs.Draw();
        roaming.Draw(ImGuiHelpers.GlobalScale);
        if (!IsOpen) { SearchHasFocus = false; return; }
        PushTheme();
        try { if (mainOpen) DrawMain(); if (companionOpen) DrawCompanionWindow(); if (settingsOpen) DrawSettings(); }
        finally { ImGui.PopStyleColor(11); ImGui.PopStyleVar(5); }
    }

    private static void PushTheme()
    {
        var s = ImGuiHelpers.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 7) * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 5) * s);
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
        launcher.Draw(ref mainOpen, ref query, entries, ImGuiHelpers.GlobalScale, Activate,
            () => { mainOpen = false; OpenSettings(); }, OpenCompanion, OpenWiki, gearFeedback ? status : "");
        SearchHasFocus = launcher.SearchHasFocus;
    }

    private void DrawCompanionWindow()
    {
        WindowSize(new(560, 490), new(480, 380));
        if (!ImGui.Begin("Moon companion###OmniKit.Companion", ref companionOpen, ImGuiWindowFlags.NoCollapse)) { ImGui.End(); return; }
        try
        {
            Header(config.SetupCompleted ? "Your XIV toolkit" : "A little help from the moon");
            if (!config.SetupCompleted) { DrawWelcome(); return; }
            if (config.VoiceEnabled)
            {
                if (ImGui.Button(voice.IsListening ? "Stop listening" : "Speak"))
                { if (voice.IsListening) voice.Cancel(); else StartVoice(false); }
            }
            ImGui.SetNextItemWidth(-1);
            if (focusSearch) { ImGui.SetKeyboardFocusHere(); focusSearch = false; }
            var submit = ImGui.InputTextWithHint("##question", "Ask about XIV...", ref question, 4000, ImGuiInputTextFlags.EnterReturnsTrue);
            DrawAnswer(submit);
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
        { config.AiEnabled = false; config.SetupCompleted = true; Save(); companionOpen = false; OpenMain(); }
        ImGui.Spacing(); ImGui.TextColored(Gold, "Add an AI companion");
        ImGui.TextWrapped("AI adds flexible explanations and conversation about XIV. Questions you submit are sent to your chosen provider, which may charge for use. Local commands still run on your PC. AI replies cannot control the game.");
        if (ImGui.Button("Set up AI", new Vector2(-1, 0)))
        { config.AiEnabled = false; config.SetupCompleted = true; Save(); companionOpen = false; section = 3; companionTab = 2; OpenSettings(); }
        ImGui.TextWrapped("This preview supports an API key or a compatible local server. Account sign-in is planned; a ChatGPT or Claude subscription is not an API key.");
        ImGui.EndChild();
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
            if (ImGui.Button("Connect AI")) { section = 3; companionTab = 2; selectSettingsTab = true; OpenSettings(); }
        }
        else
        {
            ImGui.BeginDisabled(pendingAnswer != null || string.IsNullOrWhiteSpace(question));
            if (ImGui.Button("Ask companion") || (submit && pendingAnswer == null))
            { request = new CancellationTokenSource(); pendingAnswer = chat.AskAsync(question, request.Token); answer = "Thinking..."; }
            ImGui.EndDisabled();
            if (pendingAnswer != null) { ImGui.SameLine(); if (ImGui.Button("Cancel")) request?.Cancel(); }
        }
        ImGui.SameLine(); if (ImGui.Button("Search wiki")) OpenWiki(question);
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
        WindowSize(new(590, 460), new(520, 360));
        // New identity discards the oversized window size saved by the first preview.
        if (!ImGui.Begin("OmniKit Settings###OmniKit.Settings.Compact", ref settingsOpen, ImGuiWindowFlags.NoCollapse)) { ImGui.End(); return; }
        try
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##settings-search", "Find a setting...", ref settingsSearch, 100))
            {
                hotkeys.Cancel();
                var filter = settingsSearch.ToLowerInvariant();
                if (filter.Contains("hud") || filter.Contains("target")) section = 2;
                else if (new[] { "interact", "gather", "gear" }.Any(filter.Contains)) section = 1;
                else if (new[] { "voice", "ai", "avatar", "companion", "brain", "bind", "train", "box" }.Any(filter.Contains))
                { section = 3; companionTab = filter.Contains("ai") || filter.Contains("brain") ? 2 : filter.Contains("voice") || filter.Contains("train") ? 1 : 0; }
                else if (new[] { "data", "export", "import", "json", "source", "umbra", "wiki" }.Any(filter.Contains)) section = 4;
                else if (filter.Contains("key") || filter.Contains("shortcut")) section = 0;
                selectSettingsTab = true;
            }
            if (ImGui.BeginTabBar("settings-tabs", ImGuiTabBarFlags.FittingPolicyScroll))
            {
                var requestedSection = selectSettingsTab ? section : -1;
                for (var i = 0; i < Sections.Length; i++)
                {
                    var flags = requestedSection == i ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
                    if (!ImGui.BeginTabItem(Sections[i], flags)) continue;
                    if (section != i) hotkeys.Cancel();
                    section = i;
                    ImGui.BeginChild("settings-body-" + i, new Vector2(0, -ImGui.GetTextLineHeightWithSpacing() * 2));
                    switch (i)
                    {
                        case 0: DrawShortcuts(); break;
                        case 1: DrawInteraction(); break;
                        case 2: DrawHudSettings(); break;
                        case 3: DrawCompanion(); break;
                        case 4: DrawData(); break;
                    }
                    ImGui.EndChild(); ImGui.EndTabItem();
                }
                ImGui.EndTabBar(); selectSettingsTab = false;
            }
            ImGui.BeginChild("settings-status", new Vector2(0, 0));
            ImGui.TextWrapped(status.Length > 0 ? status : "Changes save automatically.");
            ImGui.EndChild();
        }
        finally { ImGui.End(); if (!settingsOpen) hotkeys.Cancel(); }
    }

    private void DrawShortcuts()
    {
        Heading("Open OmniKit");
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
        ImGui.TextWrapped("Press your shortcut while playing. You can always type /omnikit or /omnikit settings in chat.");
        ImGui.TextWrapped("Alt + Space may already belong to a desktop launcher. Change one shortcut if both open.");
        Heading("Try a command");
        ImGui.TextWrapped("community • guild • free company\njob paladin • job miner • a saved gearset name\ninventory • crafting log • a plugin name");
        if (ImGui.Button("Open Search")) OpenMain();
    }

    private void DrawInteraction()
    {
        Heading("Look, then interact");
        if (ImGui.Checkbox("Enable soft interaction", ref config.SoftInteractEnabled)) { hotkeys.Cancel(); Save(); }
        ImGui.TextWrapped("Look toward a nearby NPC or usable object, then press your interact key. Nothing happens automatically just because you look at it.");
        if (!config.SoftInteractEnabled) return;
        var old = (config.InteractKey, config.InteractModifier);
        if (hotkeys.Draw("Interact key", ref config.InteractKey, ref config.InteractModifier))
        {
            if ((config.LauncherModifier == config.InteractModifier && config.InteractKey == config.LauncherKey) || (config.SettingsModifier == config.InteractModifier && config.InteractKey == config.SettingsKey))
            { (config.InteractKey, config.InteractModifier) = old; status = "The interact key needs its own shortcut."; }
            else Save();
        }
        VisibleTarget();
        if (ImGui.CollapsingHeader("Gathering outfits"))
        {
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
        }
        if (ImGui.CollapsingHeader("Fine-tune targeting"))
        {
            if (ImGui.SliderFloat("Aim area", ref config.InteractionCone, .025f, .30f, "%.3f")) Save();
            if (ImGui.SliderFloat("Maximum distance", ref config.InteractionDistance, 1, 6, "%.1f yalms")) Save();
            ImGui.TextWrapped("A smaller aim area is more precise. The game still checks whether you can use the target.");
        }
    }

    private void VisibleTarget()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.TextWrapped("Visible target: " + (interaction.CandidateName ?? "None"));
        ImGui.PopStyleColor();
        ImGui.TextWrapped(interaction.Hint ?? "Look toward a nearby usable object.");
    }

    private void DrawHudSettings()
    {
        if (ImGui.Checkbox("Show visible target on screen", ref config.InteractionHudEnabled)) Save();
        ImGui.TextWrapped("Styled text only. It stays visible through menus and disappears when there is no target.");
        VisibleTarget();
        if (!config.InteractionHudEnabled) return;
        if (ImGui.Checkbox("Lock position", ref config.InteractionHudLocked)) Save();
        ImGui.TextWrapped("Shift + right-click the visible text for Move, Lock, Hide, or these settings.");
        if (ImGui.SliderFloat("Text size", ref config.InteractionHudScale, .5f, 2, "%.2f")) Save();
        if (ImGui.SliderFloat("Horizontal offset", ref config.InteractionHudOffsetX, -800, 800, "%.0f")) Save();
        if (ImGui.SliderFloat("Vertical offset", ref config.InteractionHudOffsetY, -500, 500, "%.0f")) Save();
        if (ImGui.Button("Reset HUD position")) { config.InteractionHudOffsetX = 0; config.InteractionHudOffsetY = 120; Save(); }
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
        if (!ImGui.BeginTabBar("companion-options")) return;
        foreach (var (index, title) in new[] { (0, "Appearance"), (1, "Voice"), (2, "AI") })
        {
            var flags = shownCompanionTab != companionTab && companionTab == index ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            if (!ImGui.BeginTabItem(title, flags)) continue;
            shownCompanionTab = index;
            switch (index) { case 0: DrawAppearance(); break; case 1: DrawVoiceSettings(); break; case 2: DrawAiSettings(); break; }
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
        companionTab = shownCompanionTab;
    }

    private void DrawAppearance()
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
            if (ImGui.SliderFloat("Companion size", ref config.CompanionSize, 48, 192, "%.0f")) Save();
            if (ImGui.Button("Draw roaming area")) { roaming.BeginChooseArea(); settingsOpen = false; }
            ImGui.TextWrapped("Drag a box over the game. Your companion walks inside it. Escape cancels. Click the companion to talk.");
        }
        if (ImGui.Button("Open companion conversation")) OpenCompanion();
    }

    private void DrawVoiceSettings()
    {
        Heading("Voice shortcuts");
        if (ImGui.Checkbox("Enable voice input", ref config.VoiceEnabled)) Save();
        ImGui.TextWrapped("Speech recognition runs on Windows. The microphone opens only when you press Speak or Practice, then stops after one phrase. Typing always works.");
        if (config.VoiceEnabled) DrawVoiceBindings();
    }

    private void DrawAiSettings()
    {
        Heading("Optional AI");
        ImGui.TextWrapped("Your local command matcher works without AI: names, common words, saved voice phrases, jobs and game actions. AI adds open-ended answers about XIV. Only questions you submit with Ask are sent to the provider.");
        if (ImGui.Checkbox("Use AI for questions", ref config.AiEnabled))
        { if (!config.AiEnabled) request?.Cancel(); Save(); }
        if (!config.AiEnabled) return;
        ImGui.TextWrapped("1. Choose your provider and model.\n2. Paste its API key and save it.\n3. Open your companion and ask a question.");
        if (ImGui.Button("Open companion")) OpenCompanion();
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
                    importJson = reader.ReadToEnd(); confirmImport = true; settingsOpen = true; section = 4; selectSettingsTab = true;
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
                    var imported = store.Upgrade(config);
                    store.ImportInto(imported, importJson); imported.AiEnabled = false;
                    store.Save(imported);
                    store.ImportInto(config, store.Export(imported));
                    providerEndpoint = config.AiEndpoint;
                    request?.Cancel(); voice.Cancel();
                    status = "Settings imported. AI is off until you enable it."; importJson = "";
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
        if (ImGui.Button("Open setup again")) { config.SetupCompleted = false; Save(); OpenCompanion(); }
    }

    private static void Heading(string title) { ImGui.Spacing(); ImGui.TextColored(Gold, title); ImGui.Spacing(); }

    private bool Save()
    {
        try { config.Save(); status = "Saved."; return true; }
        catch { status = "Could not save settings. Check the values and local storage access."; return false; }
    }

    public void Dispose()
    {
        interaction.ConfigureHudRequested -= OpenHudSettings;
        hotkeys.Cancel(); roaming.CancelChooseArea(); request?.Cancel(); request?.Dispose(); apiKey = "";
    }
}
