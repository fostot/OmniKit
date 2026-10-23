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

    private static float FooterHeight => ImGui.GetTextLineHeightWithSpacing() * 2 + 2 * ImGuiHelpers.GlobalScale;

    private static void Footer(string message)
    {
        ImGui.BeginChild("footer", new Vector2(0, 0));
        ImGui.TextColored(Muted, BuildVersion.Display + "  ·  by Zero");
        ImGui.TextWrapped(message);
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
            Header(config.SetupCompleted ? "Your XIV companion" : "Start with local tools, add AI when you want.");
            if (!config.SetupCompleted) { DrawWelcome(); Footer("Choose a starting point above."); return; }
            if (config.VoiceEnabled)
            {
                if (ImGui.Button(voice.IsListening ? "Stop listening" : "Speak"))
                { if (voice.IsListening) voice.Cancel(); else StartVoice(false); }
            }
            ImGui.SetNextItemWidth(-1);
            if (focusSearch) { ImGui.SetKeyboardFocusHere(); focusSearch = false; }
            var submit = ImGui.InputTextWithHint("##question", "Ask about XIV...", ref question, 4000, ImGuiInputTextFlags.EnterReturnsTrue);
            DrawAnswer(submit);
            Footer(voice.IsListening ? voice.Status : status.Length > 0 ? status : "Local commands work without AI.");
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
        ImGui.TextColored(Gold, config.AvatarEnabled ? config.Avatar : "OmniKit");
        ImGui.SetCursorScreenPos(origin + new Vector2(config.AvatarEnabled ? 54 : 0, 23) * s);
        ImGui.TextWrapped(subtitle);
        ImGui.SetCursorScreenPos(origin + new Vector2(0, 50) * s);
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawWelcome()
    {
        ImGui.BeginChild("welcome", new Vector2(0, -FooterHeight));
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
        ImGui.TextWrapped("Choose Continue with ChatGPT, your installed Codex or Claude CLI, an API key, or a compatible local server. Connecting an account does not enable AI questions until you turn them on.");
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
            ImGui.BeginDisabled(pendingAnswer != null || ConnectionBusy || string.IsNullOrWhiteSpace(question));
            if (ImGui.Button("Ask companion") || (submit && pendingAnswer == null && !ConnectionBusy))
            { request = new CancellationTokenSource(); pendingAnswer = chat.AskAsync(question, request.Token); answer = "Thinking..."; }
            ImGui.EndDisabled();
            if (pendingAnswer != null) { ImGui.SameLine(); if (ImGui.Button("Cancel")) request?.Cancel(); }
        }
        ImGui.SameLine(); if (ImGui.Button("Search wiki")) OpenWiki(question);
        ImGui.BeginChild("answer", new Vector2(0, -FooterHeight));
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
                else if (new[] { "interact", "gather", "gear", "enemy", "enemies", "category", "categories", "target type", "attack" }.Any(filter.Contains)) section = 1;
                else if (new[] { "hud", "target", "font", "text size", "outline", "glow", "shimmer" }.Any(filter.Contains)) section = 2;
                else if (new[] { "voice", "ai", "avatar", "companion", "brain", "bind", "train", "box", "frame", "pickup", "perch", "fall", "chatgpt", "codex", "claude", "provider", "account" }.Any(filter.Contains))
                { section = 3; companionTab = new[] { "ai", "brain", "chatgpt", "codex", "claude", "provider", "account" }.Any(filter.Contains) ? 2 : filter.Contains("voice") || filter.Contains("train") ? 1 : 0; }
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
            Footer(status.Length > 0 ? status : "Changes save automatically.");
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
        ImGui.TextWrapped("Look toward a nearby enabled target, then press your interact key. Nothing happens automatically just because you look at it.");
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
        ImGui.TextWrapped("How far to look for targets. The game still controls how close you must be to interact.");
        DrawInteractionTargets();
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
            ImGui.TextWrapped("A smaller aim area is more precise. The game still checks whether you can use the target.");
        }
    }

    private void DrawInteractionTargets()
    {
        if (!ImGui.CollapsingHeader("Target types", ImGuiTreeNodeFlags.DefaultOpen)) return;
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
        ImGui.TextWrapped("Neutral includes attackable enemies not currently marked hostile. Their category can change with game state.");
        if ((config.InteractionTargets & (InteractionTargets.HostileEnemies | InteractionTargets.NeutralEnemies)) != 0)
        {
            ImGui.TextWrapped("Choose what the interact key does to enemies:");
            if (ImGui.RadioButton("Select target only", config.EnemyInteractMode == EnemyInteractMode.TargetOnly))
            { config.EnemyInteractMode = EnemyInteractMode.TargetOnly; Save(); }
            if (ImGui.RadioButton("Target and start auto-attack", config.EnemyInteractMode == EnemyInteractMode.StartAttack))
            { config.EnemyInteractMode = EnemyInteractMode.StartAttack; Save(); }
            if (config.EnemyInteractMode == EnemyInteractMode.Choose)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Gold);
                ImGui.TextWrapped("Choose an action above before using the interact key on enemies.");
                ImGui.PopStyleColor();
            }
            ImGui.TextWrapped("Select target only does not start auto-attack. An existing auto-attack stays active.");
            ImGui.TextWrapped("Looking only updates the HUD. Press the interact key to use your chosen enemy action. Auto-attack follows the game's normal range and target rules.");
        }
        if (config.InteractionTargets == InteractionTargets.None)
            ImGui.TextWrapped("All target types are off. Enable a type to find interaction targets.");
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
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.TextWrapped("Visible target: " + (interaction.CandidateName ?? "None"));
        ImGui.PopStyleColor();
        ImGui.TextColored(Muted, "Target type: " + interaction.CandidateCategoryName);
        ImGui.TextWrapped(interaction.Hint ?? "Look toward a nearby usable object.");
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
        { HudTextEffect.SoftGlow => "Soft glow", HudTextEffect.GentleShimmer => "Gentle shimmer", _ => config.InteractionHudEffect.ToString() };
        if (ImGui.BeginCombo("Text effect", effectLabel))
        {
            foreach (var (effect, label) in new[] { (HudTextEffect.None, "None"), (HudTextEffect.Shadow, "Shadow"),
                         (HudTextEffect.SoftGlow, "Soft glow"), (HudTextEffect.GentleShimmer, "Gentle shimmer") })
                if (ImGui.Selectable(label, config.InteractionHudEffect == effect)) { config.InteractionHudEffect = effect; Save(); }
            ImGui.EndCombo();
        }
        ImGui.TextWrapped("Live preview on screen. No action runs while you edit.");
        if (interaction.HudHiddenForInteraction) ImGui.TextWrapped("Preview is hidden while interacting, talking, gathering, or watching a cutscene.");
        if (ImGui.Checkbox("Show visible target on screen", ref config.InteractionHudEnabled)) Save();
        if (!config.InteractionHudEnabled) ImGui.TextWrapped("The preview is shown only while editing. The HUD is hidden during play.");
        if (ImGui.Checkbox("Lock position", ref config.InteractionHudLocked)) Save();
        if (ImGui.SliderFloat("Horizontal offset", ref config.InteractionHudOffsetX, -800, 800, "%.0f")) Save();
        if (ImGui.SliderFloat("Vertical offset", ref config.InteractionHudOffsetY, -500, 500, "%.0f")) Save();
        if (ImGui.Button("Reset HUD position")) { config.InteractionHudOffsetX = 0; config.InteractionHudOffsetY = 120; Save(); }
        ImGui.TextWrapped("Shift + right-click the visible text for Move, Lock, Hide, or these settings.");
        VisibleTarget();
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
        // Snapshot programmatic selection before drawing: a clicked earlier tab
        // must not make a later tab force itself selected in the same frame.
        var requestedTab = shownCompanionTab != companionTab ? companionTab : -1;
        var clickedTab = -1;
        foreach (var (index, title) in new[] { (0, "Appearance"), (1, "Voice"), (2, "AI") })
        {
            var flags = requestedTab == index ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            var visible = ImGui.BeginTabItem(title, flags);
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
        Heading("Your moon companion");
        if (ImGui.Checkbox("Show companion", ref config.AvatarEnabled)) Save();
        if (config.AvatarEnabled)
        {
            if (ImGui.BeginCombo("Companion", AvatarLabel(config.Avatar)))
            {
                foreach (var avatar in Configuration.AvatarNames)
                    if (ImGui.Selectable(AvatarLabel(avatar), config.Avatar == avatar)) { config.Avatar = avatar; Save(); }
                ImGui.EndCombo();
            }
            if (config.Avatar is "Bimble" or "Moonbear")
            {
                if (ImGui.BeginCombo("Outfit", OutfitLabel(config.CompanionOutfit)))
                {
                    foreach (var outfit in Configuration.OutfitNames)
                        if (ImGui.Selectable(OutfitLabel(outfit), config.CompanionOutfit == outfit)) { config.CompanionOutfit = outfit; Save(); }
                    ImGui.EndCombo();
                }
            }
            if (ImGui.BeginCombo("Aura / shape color", config.CompanionPalette))
            {
                foreach (var palette in Configuration.PaletteNames)
                {
                    var picked = ImGui.ColorButton("##palette-" + palette, ProceduralCompanion.Palette(palette), ImGuiColorEditFlags.NoTooltip,
                        new Vector2(ImGui.GetTextLineHeight()));
                    ImGui.SameLine();
                    if (ImGui.Selectable(palette, config.CompanionPalette == palette) || picked) { config.CompanionPalette = palette; Save(); }
                }
                ImGui.EndCombo();
            }
            if (ImGui.Checkbox("Animate companion", ref config.AnimationsEnabled)) Save();
            if (ImGui.Checkbox("Magic accents", ref config.CompanionMagicEnabled)) Save();
            ImGui.BeginDisabled(!config.AnimationsEnabled);
            if (ImGui.BeginCombo("Preview animation", roaming.PreviewPose.ToString()))
            {
                foreach (var pose in Enum.GetValues<CompanionPose>())
                    if (ImGui.Selectable(pose.ToString(), roaming.PreviewPose == pose)) roaming.PreviewPose = pose;
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Preview only; no speech playback.");
            ImGui.EndDisabled();
            if (ImGui.SliderFloat("Companion size", ref config.CompanionSize, 48, 192, "%.0f")) Save();
            if (ImGui.Button("Draw roaming area")) { roaming.BeginChooseArea(); settingsOpen = false; }
            ImGui.TextWrapped("Drag a box over the game. Your companion walks inside it. Escape cancels. Click the companion to talk.");
            Heading("Frame interaction");
            if (ImGui.Checkbox("Enable frame interaction", ref config.CompanionFrameInteractionEnabled)) Save();
            if (config.CompanionFrameInteractionEnabled)
            {
                if (ImGui.Checkbox("Pick up and drag", ref config.CompanionPickupEnabled)) Save();
                if (ImGui.Checkbox("Walk on window tops", ref config.CompanionPerchEnabled)) Save();
                if (ImGui.Checkbox("Stay inside a window when dropped inside", ref config.CompanionContainmentEnabled)) Save();
                if (ImGui.Checkbox("Fall when leaving a window", ref config.CompanionFallingEnabled)) Save();
                ImGui.TextWrapped("Drag your companion onto a supported window. Drop near its top to perch, or inside it to roam within its edges.");
                ImGui.TextWrapped("Supports visible rectangular game and Dalamud windows, including OmniKit and compatible Umbra panels. Hidden, closed or unsupported windows return your companion to its saved area.");
                if (!config.AnimationsEnabled) ImGui.TextWrapped("Animations are off. You can still pick up and place your companion.");
            }
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
        ImGui.TextWrapped("AI answers questions about XIV. Local commands work without it. Connect below, then enable questions when ready.");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##ai-connection", ConnectionLabel(config.AiConnection)))
        {
            foreach (var connection in Enum.GetValues<AiConnectionKind>())
                if (ImGui.Selectable(ConnectionLabel(connection), connection == config.AiConnection) && connection != config.AiConnection)
                {
                    CancelAiOperations(); config.AiConnection = connection; config.AiEnabled = false;
                    chatGptModels = []; apiKey = ""; connectionStatus = "";
                    Save(); status = "Connection changed. Enable AI questions when this provider is ready.";
                }
            ImGui.EndCombo();
        }
        if (ImGui.Checkbox("Use AI for questions", ref config.AiEnabled))
        { if (!config.AiEnabled) CancelAiOperations(); Save(); }
        if (ImGui.Button("Open companion")) OpenCompanion();
        switch (config.AiConnection)
        {
            case AiConnectionKind.ChatGpt: DrawChatGptConnection(); break;
            case AiConnectionKind.CodexCli:
            case AiConnectionKind.ClaudeCli: DrawCliConnection(); break;
            default: DrawApiConnection(); break;
        }
        if (ConnectionBusy)
        {
            ImGui.TextWrapped("Waiting for the connection operation...");
            if (ImGui.Button("Cancel connection")) connectionRequest?.Cancel();
        }
        if (connectionStatus.Length > 0) ImGui.TextWrapped(connectionStatus);
        ImGui.Spacing();
        ImGui.TextWrapped("Only questions you submit with Ask are sent. Replies cannot run commands or control the game. Provider limits and charges may apply.");
    }

    private void DrawApiConnection()
    {
        Heading("API key or local server");
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
                {
                    if (!string.Equals(config.AiEndpoint, providerEndpoint, StringComparison.Ordinal))
                    {
                        CancelAiOperations(); config.AiEndpoint = providerEndpoint; config.AiEnabled = false; apiKey = "";
                        Save(); status = "Address changed. Enable AI questions when this provider is ready.";
                    }
                }
                else status = "Use a full HTTPS Responses address, or HTTP on 127.0.0.1 for a local server.";
            }
            ImGui.TextWrapped("Use a Responses-compatible endpoint. Local servers can work without a key. Changing the address does not send your old key to the new provider.");
        }
    }

    private void DrawChatGptConnection()
    {
        Heading("ChatGPT account");
        ImGui.TextWrapped(chat.ChatGpt.Status);
        ImGui.TextWrapped("Sign-in opens your browser. OmniKit keeps its own protected connection; it does not copy another app's login.");
        var accounts = chat.ChatGpt.Accounts;
        var active = accounts.FirstOrDefault(account => account.IsActive);
        ImGui.BeginDisabled(ConnectionBusy || pendingAnswer != null);
        if (accounts.Count > 0)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##chatgpt-account", FitLabel(active?.Label ?? "Choose a saved account", ImGui.GetContentRegionAvail().X - 40 * ImGuiHelpers.GlobalScale)))
            {
                foreach (var account in accounts)
                {
                    var label = account.Label + (account.IsConnected ? "" : " · reconnect");
                    if (ImGui.Selectable(MenuLabel(label) + "##" + account.Id, account.IsActive))
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
        if (ImGui.Button("Continue with ChatGPT"))
        {
            config.AiEnabled = false; Save();
            StartConnection(token => chat.ChatGpt.SignInAsync(token), refreshModels: true);
        }
        if (accounts.Count > 0)
        {
            ImGui.SameLine();
            if (ImGui.Button("Add account"))
            {
                config.AiEnabled = false; Save();
                StartConnection(token => chat.ChatGpt.SignInAsync(token, true), refreshModels: true);
            }
            if (ImGui.Button("Sign out selected account"))
            {
                config.AiEnabled = false; chatGptModels = []; Save();
                StartConnection(token => chat.ChatGpt.SignOutAsync(token));
            }
        }
        ImGui.BeginDisabled(!chat.ChatGpt.IsConnected);
        if (ImGui.Button("Refresh available models")) StartModelRefresh();
        ImGui.EndDisabled();
        ImGui.EndDisabled();
        ImGui.SetNextItemWidth(-1);
        var selected = chatGptModels.FirstOrDefault(model => model.Id == config.ChatGptModel);
        var preview = selected?.Name ?? (config.ChatGptModel.Length > 0 ? config.ChatGptModel : "Choose a ChatGPT model");
        ImGui.BeginDisabled(ConnectionBusy || chatGptModels.Length == 0);
        if (ImGui.BeginCombo("##chatgpt-model", FitLabel(preview, ImGui.GetContentRegionAvail().X - 40 * ImGuiHelpers.GlobalScale)))
        {
            foreach (var model in chatGptModels)
            {
                if (ImGui.Selectable(MenuLabel(model.Name) + "##" + model.Id, model.Id == config.ChatGptModel))
                { config.ChatGptModel = model.Id; Save(); }
                if (ImGui.IsItemHovered()) WrappedTooltip(model.Name);
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        if (chatGptModels.Length == 0)
            ImGui.TextWrapped(chat.ChatGpt.IsConnected ? "Refresh available models above. The result or error appears below." : "Connect and refresh models to choose one available to this account.");
    }

    private void DrawCliConnection()
    {
        var codex = config.AiConnection == AiConnectionKind.CodexCli;
        Heading(codex ? "Codex CLI" : "Claude CLI");
        ImGui.TextWrapped("Uses your installed CLI and its own sign-in. OmniKit never copies its credentials.");
        ImGui.TextWrapped("The CLI may include its own global instructions and runtime context. Action tools are disabled; game state is not sent.");
        if (codex) ImGui.TextWrapped("Only the verified Codex CLI version " + CliChatBackend.SupportedCodexVersion + " is enabled. Other versions need an isolation check first.");
        ImGui.TextWrapped(chat.CliStatus);
        ImGui.BeginDisabled(ConnectionBusy || pendingAnswer != null);
        if (ImGui.Button("Check CLI connection")) StartConnection(chat.ProbeCliAsync);
        ImGui.SameLine();
        if (ImGui.Button("Sign in to CLI")) StartConnection(chat.SignInCliAsync);
        ImGui.EndDisabled();
        ImGui.SetNextItemWidth(-1);
        if (codex)
        {
            if (ImGui.InputTextWithHint("##codex-model", "Model (optional; uses CLI default)", ref config.CodexModel, 200)) Save();
        }
        else if (ImGui.InputTextWithHint("##claude-model", "Model (optional; uses CLI default)", ref config.ClaudeModel, 200)) Save();
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
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(Math.Min(360 * ImGuiHelpers.GlobalScale, ImGui.GetIO().DisplaySize.X - 40 * ImGuiHelpers.GlobalScale));
        ImGui.TextUnformatted(value);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
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
        if (ImGui.BeginPopupModal("Replace settings?", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
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
                    CancelAiOperations(); chatGptModels = []; apiKey = ""; connectionStatus = "";
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

    private void DrawAbout()
    {
        Heading("OmniKit · by Zero");
        ImGui.TextWrapped("Search, interact, and explore XIV with your moon companion.");
        Heading("Version");
        ImGui.TextWrapped(BuildVersion.Display);
        ImGui.TextColored(Muted, "Source revision " + BuildVersion.CommitCount);
        Heading("Full source commit");
        ImGui.TextWrapped(BuildVersion.CommitSha);
        if (ImGui.Button("Copy build details"))
        {
            ImGui.SetClipboardText($"OmniKit {BuildVersion.Display}\nby Zero\nSource revision: {BuildVersion.CommitCount}\nCommit: {BuildVersion.CommitSha}");
            status = "Complete build details copied.";
        }
        Heading("Project & credits");
        const string projectUrl = "https://github.com/fostot/OmniKit";
        ImGui.TextWrapped(projectUrl);
        if (ImGui.Button("Copy project link")) { ImGui.SetClipboardText(projectUrl); status = "Project link copied."; }
        ImGui.TextWrapped("Built for Dalamud, with native Umbra integration. Companion artwork was generated with OpenAI image tools. Open-source dependency licenses and artwork provenance are included in the project.");
    }

    private static void Heading(string title) { ImGui.Spacing(); ImGui.TextColored(Gold, title); ImGui.Spacing(); }

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
        try { config.Save(); status = "Saved."; return true; }
        catch { status = "Could not save settings. Check the values and local storage access."; return false; }
    }

    public void Dispose()
    {
        interaction.ConfigureHudRequested -= OpenHudSettings;
        hotkeys.Cancel(); roaming.CancelChooseArea(); CancelAiOperations(); request?.Dispose(); connectionRequest?.Dispose(); apiKey = "";
    }
}
