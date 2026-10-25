using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;

var measureOnly = args.Contains("--measure-hotkeys");
var appearance15Only = args.Contains("--appearance15");
var caseFilter = args.FirstOrDefault(argument => argument.StartsWith("--case=", StringComparison.Ordinal))?[7..].Split(',');
var scaleFilter = args.FirstOrDefault(argument => argument.StartsWith("--scale=", StringComparison.Ordinal))?[8..];
var paths = args.Where(argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToArray();
var pluginPath = Path.GetFullPath(paths.Length > 0 ? paths[0] : "src/OmniKit/bin/Release/OmniKit.dll");
var output = Path.GetFullPath(paths.Length > 1 ? paths[1] : ".local/visualharness/settings");
var hostPath = Environment.GetEnvironmentVariable("DALAMUD_LIB_PATH") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (var directory in new[] { Path.GetDirectoryName(pluginPath)!, hostPath })
    {
        var path = Path.Combine(directory, name.Name + ".dll");
        if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
    }
    return null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginPath);
var uiType = assembly.GetType("OmniKit.Ui.PluginUi", throwOnError: true)!;
var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
var draw = uiType.GetMethod("DrawSettings", flags)!;
var theme = uiType.GetMethod("PushTheme", BindingFlags.Static | BindingFlags.NonPublic)!;
if (args.Contains("--companions") || args.Contains("--companion-poses"))
{
    var prefix = args.Contains("--companion-poses") ? "poses-" : "";
    RenderCompanions(prefix + "animated", animated: true, time: .75f);
    RenderCompanions(prefix + "animated-later", animated: true, time: 2.1f);
    var first = RenderCompanions(prefix + "static", animated: false, time: .75f);
    var second = RenderCompanions(prefix + "static-later", animated: false, time: 6.2f);
    if (!File.ReadAllBytes(first).AsSpan().SequenceEqual(File.ReadAllBytes(second)))
        throw new InvalidOperationException("Disabled animations must remain pixel-identical as time changes.");
    Console.WriteLine("PASS: procedural companions rendered; geometry stays in bounds, disabled animation stays static and disabled effects emit no geometry.");
    return;
}
var cases = new[]
{
    (Name: "wardrobe-costume-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "wardrobe-avatar-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "wardrobe-nib", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "launcher", Section: 0, Tab: 0, Enabled: false, Scroll: true),
    (Name: "quest-items", Section: 6, Tab: 0, Enabled: true, Scroll: true),
    (Name: "seasonal", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "interact", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-types", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-quest-on", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "interact-quest-error", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "interact-enemy-choose", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "interact-enemy-target", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-enemy-attack", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-none", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "hud", Section: 2, Tab: 0, Enabled: true, Scroll: true),
    (Name: "hud-outline", Section: 2, Tab: 0, Enabled: true, Scroll: true),
    (Name: "hud-long", Section: 2, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "appearance15-looks-default", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-looks-random", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-looks-timer-off", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-looks-specific", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-looks-orbit-on", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-looks-orbit-off", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-looks-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-styles-default", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-styles-random", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-styles-specific", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-styles-holiday", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-home-style-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-hand-style-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-grounded-on", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-grounded-off", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance15-hop-off", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "companion-navigation", Section: 3, Tab: 0, Enabled: false, Scroll: false),
    (Name: "frame-on", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "frame-static", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "companion-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "palette-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "pose-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "voice-off", Section: 3, Tab: 1, Enabled: false, Scroll: false),
    (Name: "voice-on", Section: 3, Tab: 1, Enabled: true, Scroll: true),
    (Name: "ai-off", Section: 3, Tab: 2, Enabled: false, Scroll: false),
    (Name: "ai-on", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-middle", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-provider-menu", Section: 3, Tab: 2, Enabled: false, Scroll: false),
    (Name: "ai-provider-switch", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-chatgpt", Section: 3, Tab: 2, Enabled: false, Scroll: true),
    (Name: "ai-chatgpt-connected", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-chatgpt-middle", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-account-menu", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-model-menu", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-account-tooltip", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-model-tooltip", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-model-discovery", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-model-manual", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-model-empty", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-model-failure", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-endpoint", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-codex", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-claude", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-cli-busy", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-cli-cancel", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-cli-error", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "data", Section: 4, Tab: 0, Enabled: false, Scroll: true),
    (Name: "data-browser-capture", Section: 4, Tab: 0, Enabled: true, Scroll: true),
    (Name: "data-browser-capture-off", Section: 4, Tab: 0, Enabled: false, Scroll: true),
    (Name: "data-browser-capture-error", Section: 4, Tab: 0, Enabled: true, Scroll: true),
    (Name: "data-browser-capture-long", Section: 4, Tab: 0, Enabled: true, Scroll: true),
    (Name: "about", Section: 5, Tab: 0, Enabled: false, Scroll: true),
    (Name: "conversation-local", Section: -1, Tab: 0, Enabled: false, Scroll: false),
    (Name: "conversation-ai", Section: -1, Tab: 2, Enabled: true, Scroll: false),
    (Name: "conversation-codex", Section: -1, Tab: 2, Enabled: true, Scroll: false),
    (Name: "conversation-claude", Section: -1, Tab: 2, Enabled: true, Scroll: false),
    (Name: "conversation-busy", Section: -1, Tab: 2, Enabled: true, Scroll: false),
    (Name: "conversation-setup", Section: -1, Tab: 0, Enabled: false, Scroll: true),
    (Name: "launcher-help", Section: 0, Tab: 0, Enabled: false, Scroll: false),
    (Name: "launcher-examples", Section: 0, Tab: 0, Enabled: false, Scroll: false),
    (Name: "interact-off", Section: 1, Tab: 0, Enabled: false, Scroll: false),
    (Name: "interact-gathering", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-fine", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-enemy-help", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "hud-help", Section: 2, Tab: 0, Enabled: true, Scroll: false),
    (Name: "hud-effects", Section: 2, Tab: 0, Enabled: true, Scroll: false),
    (Name: "hud-none", Section: 2, Tab: 0, Enabled: false, Scroll: false),
    (Name: "hud-effects-static", Section: 2, Tab: 0, Enabled: true, Scroll: false),
    (Name: "hud-effect-menu", Section: 2, Tab: 0, Enabled: true, Scroll: false),
    (Name: "hud-effect-bottom-menu", Section: 2, Tab: 0, Enabled: true, Scroll: false),
    (Name: "frame-help", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "home", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "home-only", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "home-off", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "home-wink", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "home-wink-off", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "voice-help", Section: 3, Tab: 1, Enabled: true, Scroll: false),
    (Name: "ai-cli-help", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "data-import", Section: 4, Tab: 0, Enabled: false, Scroll: false),
    (Name: "data-confirm", Section: 4, Tab: 0, Enabled: false, Scroll: false),
    (Name: "data-plugins", Section: 4, Tab: 0, Enabled: false, Scroll: false),
    (Name: "data-browser", Section: 4, Tab: 0, Enabled: false, Scroll: false),
    (Name: "data-browser-error", Section: 4, Tab: 0, Enabled: false, Scroll: false),
    (Name: "about-source", Section: 5, Tab: 0, Enabled: false, Scroll: false),
    (Name: "about-credits", Section: 5, Tab: 0, Enabled: false, Scroll: false),
    (Name: "conversation-help", Section: -1, Tab: 0, Enabled: false, Scroll: false),
    (Name: "status-error", Section: 2, Tab: 0, Enabled: true, Scroll: false),
};
CheckBrowserStatus();
var renders = 0;
foreach (var scale in new[] { 1f, 1.5f, 2f })
foreach (var scenario in cases)
{
    if (measureOnly && scenario.Name != "launcher") continue;
    if (appearance15Only && !scenario.Name.StartsWith("appearance15-", StringComparison.Ordinal)) continue;
    if (caseFilter is not null && !caseFilter.Contains(scenario.Name)) continue;
    if (scaleFilter is not null && scale.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) != scaleFilter) continue;
    Render(scenario.Name, scenario.Section, scenario.Tab, scenario.Enabled, scale, scroll: false);
    var expanded = new[] { "launcher-help", "launcher-examples", "interact-gathering", "interact-fine", "interact-enemy-help",
        "hud-help", "hud-effects", "hud-effects-static", "frame-help", "voice-help", "ai-cli-help", "ai-endpoint",
        "data-import", "data-plugins", "data-browser", "about-source", "about-credits", "conversation-help" }.Contains(scenario.Name);
    if ((scenario.Scroll || expanded) && !measureOnly) Render(scenario.Name, scenario.Section, scenario.Tab, scenario.Enabled, scale, scroll: true);
}
Console.WriteLine($"PASS: {renders} actual compiled settings renders completed without starting game, voice or network services.");

unsafe void Render(string name, int section, int tab, bool enabled, float scale, bool scroll)
{
    var config = Activator.CreateInstance(Type("OmniKit.Configuration"))!;
    var wardrobeCase = name.StartsWith("wardrobe-", StringComparison.Ordinal);
    var appearance15 = name.StartsWith("appearance15-", StringComparison.Ordinal) || wardrobeCase;
    Set(config, "Avatar", "Moonblob");
    if (appearance15)
    {
        Set(config, "AvatarEnabled", true);
        Set(config, "CompanionFrameInteractionEnabled", false);
        Set(config, "CompanionHomeEnabled", true); Set(config, "CompanionGrabHandEnabled", true);
        Set(config, "CompanionHolidayHomeEnabled", name == "appearance15-styles-holiday");
        Set(config, "CompanionVariant", name.Contains("random", StringComparison.Ordinal) || name.Contains("timer", StringComparison.Ordinal) ? "Random"
            : name.EndsWith("specific", StringComparison.Ordinal) ? "Look4" : "Default");
        Set(config, "CompanionLookAutoRotateEnabled", name == "appearance15-looks-random");
        Set(config, "CompanionLookInterval", 30f);
        if (name.Contains("orbit", StringComparison.Ordinal))
        {
            Set(config, "Avatar", "Orbit"); Set(config, "CompanionOrbitRingsEnabled", name.EndsWith("on", StringComparison.Ordinal));
            Set(config, "CompanionOrbitRingSpeed", 3f);
        }
        if (name.StartsWith("appearance15-styles-", StringComparison.Ordinal))
        {
            Set(config, "CompanionHomeStyle", name.EndsWith("random", StringComparison.Ordinal) ? "Random"
                : name.EndsWith("specific", StringComparison.Ordinal) ? "CrystalPod" : "Default");
            Set(config, "CompanionHandStyle", name.EndsWith("random", StringComparison.Ordinal) ? "Random"
                : name.EndsWith("specific", StringComparison.Ordinal) ? "HologramMitt" : "Default");
        }
        Set(config, "CompanionGroundedEnabled", name != "appearance15-grounded-off");
        Set(config, "CompanionAutoHopEnabled", name != "appearance15-hop-off");
    }
    if (wardrobeCase)
    {
        Set(config, "CompanionCostume", "None");
        Set(config, "CompanionHolidayBodiesEnabled", false);
        if (name == "wardrobe-nib") Set(config, "Avatar", "Nib");
    }
    var persistField = config.GetType().GetField("Persist", flags)!;
    persistField.SetValue(config, System.Linq.Expressions.Expression.Lambda(persistField.FieldType,
        System.Linq.Expressions.Expression.Empty(), System.Linq.Expressions.Expression.Parameter(config.GetType())).Compile());
    Set(config, "SoftInteractEnabled", section == 1 && enabled);
    Set(config, "VoiceEnabled", tab == 1 && enabled);
    Set(config, "AiEnabled", tab == 2 && enabled);
    if (name == "companion-navigation") Set(config, "AiEnabled", true);
    if (name == "ai-provider-switch")
        foreach (var field in new[] { "AiModel", "ChatGptModel", "CodexModel", "ClaudeModel" }) Set(config, field, field + "-fixture");
    var chatGptCase = name.StartsWith("ai-chatgpt", StringComparison.Ordinal) || name.StartsWith("ai-account-", StringComparison.Ordinal) || name.StartsWith("ai-model-", StringComparison.Ordinal);
    var cliCase = name is "ai-codex" or "ai-claude" or "ai-cli-busy" or "ai-cli-error" or "ai-cli-cancel" or "ai-cli-help" or "conversation-codex" or "conversation-claude";
    if (chatGptCase || cliCase)
        Set(config, "AiConnection", Enum.Parse(Type("OmniKit.Assistant.AiConnectionKind"),
            chatGptCase ? "ChatGpt" : name is "ai-claude" or "conversation-claude" ? "ClaudeCli" : "CodexCli"));
    if (name.StartsWith("frame-", StringComparison.Ordinal))
    {
        Set(config, "CompanionFrameInteractionEnabled", true);
        Set(config, "AnimationsEnabled", name != "frame-static");
    }
    if (name.StartsWith("home",StringComparison.Ordinal))
    {
        Set(config,"CompanionFrameInteractionEnabled",name!="home-only");
        if(name.Contains("wink",StringComparison.Ordinal)) Set(config,"Avatar","Wink");
        if(name.EndsWith("off",StringComparison.Ordinal))
        {
            Set(config,"CompanionHomeEnabled",false); Set(config,"CompanionCommentsEnabled",false);
            Set(config,"CompanionBlinkEnabled",false); Set(config,"AnimationsEnabled",false);
            Set(config,"CompanionGrabHandEnabled",false);
        }
    }
    if (name.StartsWith("interact-enemy-", StringComparison.Ordinal))
    {
        Set(config, "InteractionTargets", Enum.Parse(Type("OmniKit.Interaction.InteractionTargets"), "All"));
        Set(config, "EnemyInteractMode", Enum.Parse(Type("OmniKit.Interaction.EnemyInteractMode"),
            name.EndsWith("target", StringComparison.Ordinal) ? "TargetOnly" : name.EndsWith("attack", StringComparison.Ordinal) ? "StartAttack" : "Choose"));
    }
    if (name == "interact-none") Set(config, "InteractionTargets", Enum.Parse(Type("OmniKit.Interaction.InteractionTargets"), "None"));
    if (name is "interact-quest-on" or "interact-quest-error") Set(config, "QuestItemUseEnabled", true);
    if (section == -1)
    {
        Set(config, "Avatar", "Moonblob");
        Set(config, "SetupCompleted", name is not ("conversation-setup" or "conversation-help"));
        Set(config, "VoiceEnabled", name == "conversation-ai");
    }
    var ui = RuntimeHelpers.GetUninitializedObject(uiType);
    foreach (var field in uiType.GetFields(flags).Where(field => field.FieldType == typeof(string))) field.SetValue(ui, "");
    Set(ui, "config", config);
    Set(ui, "providerEndpoint", config.GetType().GetField("AiEndpoint")!.GetValue(config));
    Set(ui, "settingsOpen", true);
    if(name=="status-error") Set(ui,"status","Could not save settings. Synthetic failure details remain available here instead of being erased by a routine Saved message.");
    if(name=="data-confirm") Set(ui,"confirmImport",true);
    if(name=="hud-none") Set(config,"InteractionHudEffect",Enum.Parse(Type("OmniKit.Interaction.HudTextEffect"),"None"));
    if(name=="hud-outline")
    {
        Set(config,"InteractionHudOutline",true);
        Set(config,"InteractionHudOutlineThickness",4f);
    }
    if(name.StartsWith("hud-effects",StringComparison.Ordinal))
    {
        Set(config,"InteractionHudEffect",Enum.Parse(Type("OmniKit.Interaction.HudTextEffect"),"Flame"));
        Set(config,"InteractionHudEffectAnimated",name!="hud-effects-static");
    }
    Set(ui, "companionOpen", section == -1);
    Set(ui, "section", section); Set(ui, "companionTab", tab); Set(ui, "shownCompanionTab", -1);
    Set(ui, "selectSettingsTab", true);
    Set(ui, "hotkeys", Activator.CreateInstance(Type("OmniKit.Ui.HotkeyCapture"), nonPublic: true));
    var fixtureRoaming = RuntimeHelpers.GetUninitializedObject(Type("OmniKit.Ui.RoamingCompanion"));
    Set(fixtureRoaming, "assetDirectory", Path.GetFullPath("assets"));
    if (wardrobeCase) fixtureRoaming.GetType().GetFields(flags).Single(field => field.FieldType == config.GetType()).SetValue(fixtureRoaming, config);
    Set(ui, "roaming", fixtureRoaming);
    var interactionType = Type("OmniKit.Interaction.SoftInteractionService");
    var interaction = RuntimeHelpers.GetUninitializedObject(interactionType);
    if (name == "interact-quest-error")
    {
        var itemService = RuntimeHelpers.GetUninitializedObject(Type("OmniKit.Interaction.QuestItemService"));
        Set(itemService, "<Status>k__BackingField", "The item is not usable on this target right now. Check your range and state.");
        Set(interaction, "<QuestItems>k__BackingField", itemService);
    }
    if (name == "hud-long")
    {
        var candidateType = interactionType.GetNestedType("Candidate", BindingFlags.NonPublic)!;
        Set(interaction, "candidate", Activator.CreateInstance(candidateType, 0UL, IntPtr.Zero,
            "A deliberately very long visible target name used to check that every word wraps inside the settings content area without covering the scrollbar",
            Enum.Parse(Type("OmniKit.Interaction.InteractionTargets"), "Npcs")));
    }
    Set(ui, "interaction", interaction);
    var wiki = RuntimeHelpers.GetUninitializedObject(Type("OmniKit.Integration.WikiBrowser"));
    Set(wiki, "status", "In-game browser ready.");
    if(name=="data-browser-error")
    {
        Set(wiki,"status","Browser could not start. Check the WebView2 / .NET Desktop runtimes and update OmniKit.");
        Set(wiki,"hasError",true); Set(ui,"expandBrowserSettings",true);
        Set(ui,"status","A separate settings warning remains visible.");
    }
    Set(ui,"wiki",wiki);
    var browserCaptureCase = name.StartsWith("data-browser-capture", StringComparison.Ordinal);
    var captureStatusCalls = 0;
    Vector2? captureStatusPosition = null;
    if (browserCaptureCase)
    {
        Set(config, "BrowserInterceptLinks", enabled);
        Set(ui, "expandBrowserSettings", true);
        var captureStatus = name switch
        {
            "data-browser-capture-error" => "Web-link capture failed. Standard browser opening remains available.",
            "data-browser-capture-long" => "Web-link capture is enabled for standard HTTP and HTTPS links in the game process. Authentication links and non-web actions pass through. Custom launchers and other processes cannot be captured. This is deliberately long synthetic status text for checking the complete hover detail.",
            _ => "Web-link capture enabled for this game process.",
        };
        uiType.GetProperty("BrowserInterceptionStatus", flags)!.SetValue(ui, (Func<string>)(() =>
        {
            captureStatusCalls++;
            captureStatusPosition = ImGui.GetCursorScreenPos();
            return captureStatus;
        }));
    }
    Set(ui, "voice", Activator.CreateInstance(Type("OmniKit.Assistant.VoiceInput"), nonPublic: true));
    // HasApiKey inspects only an empty harness path, never the user's credential store.
    var chat = RuntimeHelpers.GetUninitializedObject(Type("OmniKit.Assistant.ChatService"));
    Set(chat, "config", config);
    if (name == "conversation-busy") Set(chat, "busy", 1);
    if (name is "conversation-busy" or "conversation-codex" or "conversation-claude")
        Set(ui, "question", "Synthetic question must remain unsent.");
    var emptyCredentials = new DirectoryInfo(Path.Combine(output, "empty-credentials-" + Guid.NewGuid().ToString("N")));
    Set(chat, "credentials", Activator.CreateInstance(Type("OmniKit.Assistant.CredentialStore"), emptyCredentials));
    using var chatGpt = (IDisposable)Activator.CreateInstance(Type("OmniKit.Assistant.ChatGptConnection"), [emptyCredentials, null])!;
    Set(chat, "<ChatGpt>k__BackingField", chatGpt);
    var cli = RuntimeHelpers.GetUninitializedObject(Type("OmniKit.Assistant.CliChatBackend"));
    Set(cli, "status", name == "ai-cli-error" ? "This Codex CLI version has not passed OmniKit's isolation check. No question was sent." : "Choose a CLI and check its connection.");
    Set(chat, "cli", cli);
    var modelType = Type("OmniKit.Assistant.AiModelOption");
    var models = Array.CreateInstance(modelType, chatGptCase && name != "ai-chatgpt" ? 2 : 0);
    if (models.Length > 0)
    {
        models.SetValue(Activator.CreateInstance(modelType, "fixture-model-a", "A deliberately long available model display name that stays inside the native menu"), 0);
        models.SetValue(Activator.CreateInstance(modelType, "fixture-model-b", "Second test model"), 1);
        Set(config, "ChatGptModel", "fixture-model-a");
        var accountType = Type("OmniKit.Assistant.ChatGptAccountInfo");
        var accounts = Array.CreateInstance(accountType, 2);
        accounts.SetValue(Activator.CreateInstance(accountType, "fixture-account-a", "A deliberately long synthetic ChatGPT account label to verify native menu bounds", true, true), 0);
        accounts.SetValue(Activator.CreateInstance(accountType, "fixture-account-b", "Second test account", false, false), 1);
        Set(chatGpt, "accounts", accounts);
        Set(chatGpt, "status", "Connected to the selected ChatGPT account.");
    }
    Set(ui, "chatGptModels", models);
    using var pendingCancellation = new CancellationTokenSource();
    var pendingOperation = new TaskCompletionSource<string>();
    using var cancellationRegistration = pendingCancellation.Token.Register(() => pendingOperation.TrySetResult("Synthetic connection canceled."));
    if (name is "ai-cli-busy" or "ai-cli-cancel")
    {
        Set(ui, "pendingConnection", pendingOperation.Task);
        Set(ui, "connectionRequest", pendingCancellation);
        Set(ui, "operationConnection", config.GetType().GetField("AiConnection")!.GetValue(config));
    }
    Set(ui, "chat", chat);
    var modelUpdateCase = name is "ai-model-discovery" or "ai-model-manual" or "ai-model-empty" or "ai-model-failure";
    if (modelUpdateCase)
    {
        var modelListType = typeof(IReadOnlyList<>).MakeGenericType(modelType);
        var received = Array.CreateInstance(modelType, name == "ai-model-empty" ? 0 : 2);
        if (received.Length > 0)
        {
            received.SetValue(Activator.CreateInstance(modelType, "gpt-fixture-pro", "Synthetic Pro"), 0);
            received.SetValue(Activator.CreateInstance(modelType, "gpt-fixture-nano", "Synthetic Nano"), 1);
        }
        if (name == "ai-model-manual") Set(config, "ChatGptModel", "gpt-fixture-pro");
        Set(chatGpt, "status", name == "ai-model-empty"
            ? "ChatGPT returned no selectable models for this account. Sign-in succeeded, but no model was offered for selection."
            : name == "ai-model-failure" ? "ChatGPT declined model access (401). Continue with ChatGPT to reconnect." : "Loaded 2 available ChatGPT model(s).");
        var taskMethod = typeof(Task).GetMethods().Single(method => method.IsGenericMethodDefinition &&
            method.Name == (name == "ai-model-failure" ? "FromException" : "FromResult"));
        var task = taskMethod.MakeGenericMethod(modelListType).Invoke(null,
            [name == "ai-model-failure" ? new InvalidOperationException("Untrusted fixture text must not be shown.") : received]);
        Set(ui, "pendingModels", task);
        Set(ui, "operationConnection", config.GetType().GetField("AiConnection")!.GetValue(config));
        Set(ui, "connectionRequest", pendingCancellation);
        uiType.GetMethod("UpdateConnection", flags)!.Invoke(ui, null);
        var expected = name switch { "ai-model-empty" => "", "ai-model-discovery" => "gpt-fixture-nano", "ai-model-manual" => "gpt-fixture-pro", _ => "fixture-model-a" };
        if (!Equals(config.GetType().GetField("ChatGptModel")!.GetValue(config), expected))
            throw new InvalidOperationException("Model refresh did not preserve or choose the expected preference.");
        var message = (string)uiType.GetField("connectionStatus", flags)!.GetValue(ui)!;
        if (message.Contains("Untrusted fixture", StringComparison.Ordinal) ||
            (name == "ai-model-failure" && (!message.Contains("401", StringComparison.Ordinal) || ((Array)uiType.GetField("chatGptModels", flags)!.GetValue(ui)!).Length != 2)))
            throw new InvalidOperationException("Failed refresh did not preserve choices and the sanitized failure status.");
        if (uiType.GetField("pendingModels", flags)!.GetValue(ui) != null)
            throw new InvalidOperationException("Completed model refresh was not cleared.");
    }
    var popupCase = name.EndsWith("-menu", StringComparison.Ordinal) || name.EndsWith("-tooltip", StringComparison.Ordinal);
    var navigationCase = name == "companion-navigation";
    var providerSwitch = name == "ai-provider-switch";
    var entriesField = uiType.GetField("entries", flags)!;
    entriesField.SetValue(ui, Array.CreateInstance(entriesField.FieldType.GetElementType()!, 0));
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(560, 410) * scale;
        io.FontGlobalScale = scale;
        io.DeltaTime = 1f / 60;
        io.IniFilename = null; io.LogFilename = null;
        var font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        if (File.Exists(font)) io.Fonts.AddFontFromFileTTF(font, 17);
        else io.Fonts.AddFontDefault();
        io.Fonts.Build();
        var renderer = new SoftwareRenderer();
        ImGui.StyleColorsDark();
        var measured = new Dictionary<string, (Vector2 Min, Vector2 Max, bool Visible)>();
        var observer = Type("OmniKit.Ui.UiText").GetProperty("ItemMeasured", BindingFlags.Static | BindingFlags.NonPublic)!;
        observer.SetValue(null, (Action<string, Vector2, Vector2, bool>)((label, min, max, visible) => measured[label] = (min,max,visible)));
        var targetControl = name switch
        {
            "companion-menu" => "Companion", "palette-menu" => "Aura / shape color",
            "pose-menu" => "Preview animation", "ai-provider-menu" => "##ai-connection",
            "ai-account-menu" or "ai-account-tooltip" => "##chatgpt-account", "ai-model-menu" or "ai-model-tooltip" => "##chatgpt-model",
            "ai-endpoint" => "Other provider or local AI server", "ai-cli-cancel" => "Cancel connection",
            "launcher-help" => "Shortcut help", "launcher-examples" => "Search examples", "hud-help" => "HUD help",
            "hud-effects" or "hud-effects-static" => "Effect settings", "interact-gathering" => "Gathering outfits", "interact-fine" => "Fine-tune targeting",
            "hud-effect-menu" or "hud-effect-bottom-menu" => "Text effect",
            "home" or "home-only" or "home-off" or "home-wink" or "home-wink-off" => "Home & personality",
            "seasonal" => "Seasonal looks & rotation",
            "interact-enemy-help" => "Enemy behavior", "frame-help" => "Frame behavior", "voice-help" => "Voice shortcut help",
            "ai-cli-help" => "CLI privacy & limits", "data-import" => "Import settings", "data-plugins" => "Plugins & Umbra",
            "data-browser" => "In-game browser", "about-source" => "Source details", "about-credits" => "Credits",
            "conversation-help" => "Connection choices",
            "conversation-busy" or "conversation-codex" or "conversation-claude" => "Ask companion", _ => null,
        };
        var appearanceHeader = name == "wardrobe-costume-menu" ? "Seasonal looks & rotation"
            : name == "wardrobe-nib" ? "Looks" : !appearance15 ? null : name.Contains("looks-", StringComparison.Ordinal) ? "Looks"
            : name.Contains("styles-", StringComparison.Ordinal) || name.Contains("style-menu", StringComparison.Ordinal) ? "Home & personality" : null;
        var appearanceTarget = name == "wardrobe-costume-menu" ? "Painted costume" : name == "wardrobe-avatar-menu" ? "Companion"
            : name == "wardrobe-nib" ? "Seasonal looks & rotation" : !appearance15 ? null : name == "appearance15-hand-style-menu" || name == "appearance15-styles-random" ? "Hand style"
            : appearanceHeader == "Home & personality" ? "Home style"
            : appearanceHeader == "Looks" ? "Look" : "Draw roaming area";
        bool PointAt(string label, bool click, bool scrollTo = true)
        {
            var key = measured.ContainsKey(label) ? label : measured.Keys.FirstOrDefault(key => key.Contains(label, StringComparison.Ordinal));
            if (key != null && measured[key] is { Visible:true } item)
            {
                io.AddMousePosEvent((item.Min.X+item.Max.X)/2,(item.Min.Y+item.Max.Y)/2);
                if(click) io.AddMouseButtonEvent(0,true);
                return true;
            }
            if(scrollTo)
            {
                io.AddMousePosEvent(280*scale,250*scale);
                var direction = key != null && measured[key].Max.Y < io.DisplaySize.Y * .5f ? 1 : -1;
                io.AddMouseWheelEvent(0, direction);
            }
            return false;
        }
        var clickedFrame = -1;
        var appearanceReady = -1; var appearanceScrolled = false;
        var wardrobeChoices = new HashSet<string>(StringComparer.Ordinal);
        var wardrobeExpected = name == "wardrobe-avatar-menu" ? new[] { "Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism", "Nib", "Echo", "Fizz", "Glim", "Rune" }
                .Select(avatar => (string)uiType.GetMethod("AvatarLabel", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [avatar])!).ToArray()
            : name == "wardrobe-costume-menu" ? new[] { "None", "Auto", "All Saints' Wake", "Starlight Celebration", "Make It Rain", "Moonfire Faire" } : Array.Empty<string>();
        (int Left, int Top, int Right, int Bottom)? appearanceMenuBounds = null;
        var appearanceSelection = name switch
        {
            "wardrobe-costume-menu" => (Field: "CompanionCostume", Value: "Starlight", Label: "Starlight Celebration"),
            "wardrobe-avatar-menu" => (Field: "Avatar", Value: "Nib",
                Label: (string)uiType.GetMethod("AvatarLabel", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, ["Nib"])!),
            "appearance15-looks-menu" => (Field: "CompanionVariant", Value: "Look1",
                Label: (string)fixtureRoaming.GetType().GetMethod("LookLabel", flags)!.Invoke(fixtureRoaming, ["Moonblob", "Look1"])!),
            "appearance15-home-style-menu" => (Field: "CompanionHomeStyle", Value: "CrystalPod", Label: "Crystal pod"),
            "appearance15-hand-style-menu" => (Field: "CompanionHandStyle", Value: "HologramMitt", Label: "Hologram mitt"),
            _ => (Field: "", Value: "", Label: ""),
        };
        var totalFrames = appearance15 ? 80 : providerSwitch ? 40 : navigationCase ? 28 : targetControl != null ? 60 : name == "data-browser-capture-long" ? 12 : 8;
        for (var frame = 0; frame < totalFrames; frame++)
        {
            if (appearance15 && frame >= 3)
            {
                if (appearanceHeader != null && clickedFrame < 0)
                { if (PointAt(appearanceHeader, true)) clickedFrame = frame; }
                else if (appearanceHeader != null && frame == clickedFrame + 1) io.AddMouseButtonEvent(0, false);
                else if (appearanceReady < 0 && (appearanceHeader == null || frame >= clickedFrame + 3))
                { if (PointAt(appearanceTarget!, popupCase || name == "wardrobe-nib")) appearanceReady = frame; }
                else if ((popupCase || name == "wardrobe-nib") && frame == appearanceReady + 1) io.AddMouseButtonEvent(0, false);
                else if (!popupCase && !appearanceScrolled && appearanceReady >= 0 && frame >= appearanceReady + 3)
                {
                    // Settle the chosen row near the content top, so the
                    // disclosed controls below it are visible in the proof.
                    var target = name == "wardrobe-nib" ? "Painted costume" : appearanceTarget!;
                    if (!measured.TryGetValue(target, out var targetItem)) continue;
                    var top = targetItem.Min.Y;
                    if (Math.Abs(top - 150 * scale) <= 12 * scale) appearanceScrolled = true;
                    else
                    {
                        io.AddMousePosEvent(280 * scale, 250 * scale);
                        io.AddMouseWheelEvent(0, top < 150 * scale ? .25f : -.25f);
                    }
                }
            }
            if (name == "wardrobe-avatar-menu" && appearanceReady >= 0 && frame > appearanceReady + 3 && frame < 74)
            {
                // Visit the native popup's scroll range, then bring the selected new identity into view.
                var visibleChoice = wardrobeExpected.FirstOrDefault(label => measured.TryGetValue(label, out var item) && item.Visible);
                if (visibleChoice != null && PointAt(visibleChoice, false, false))
                {
                    if (frame < 60) io.AddMouseWheelEvent(0, wardrobeChoices.Count < 13 ? -.25f : 0);
                    else if (!measured.TryGetValue("Nib", out var nib) || !nib.Visible || nib.Max.Y - nib.Min.Y < ImGui.GetTextLineHeight())
                        io.AddMouseWheelEvent(0, .25f);
                }
            }
            if (appearance15 && popupCase && frame == 75 && !PointAt(appearanceSelection.Label, true, false))
                throw new InvalidOperationException("The appearance menu selection is not visible: " + appearanceSelection.Label);
            if (appearance15 && popupCase && frame == 76) io.AddMouseButtonEvent(0, false);
            if (providerSwitch)
            {
                if (frame is 3 or 12 or 21 or 30)
                {
                    Set(config, "AiEnabled", true);
                    if(!PointAt("##ai-connection",true)) throw new InvalidOperationException("Provider chooser is not visible.");
                }
                if (frame is 4 or 13 or 22 or 31 or 8 or 17 or 26 or 35) io.AddMouseButtonEvent(0, false);
                if (frame is 7 or 16 or 25 or 34)
                {
                    var label=frame switch {7=>"Codex CLI",16=>"Claude CLI",25=>"Continue with ChatGPT",_=>"API key / local server"};
                    if(!PointAt(label,true)) throw new InvalidOperationException("Provider option is not visible: "+label);
                }
            }
            if (navigationCase)
            {
                if (frame is 3 or 7 or 11 or 15)
                {
                    var label=frame==7 ? "Appearance" : frame==11 ? "Voice" : "AI";
                    if(!PointAt("tab:"+label,true)) throw new InvalidOperationException("Companion tab is not visible: "+label);
                }
                if (frame is 4 or 8 or 12 or 16) io.AddMouseButtonEvent(0,false);
                if (frame is 19 or 23) Set(ui,"companionTab",frame==19 ? 0 : 2);
            }
            if(targetControl != null && frame>=3)
            {
                if(clickedFrame<0 && PointAt(targetControl,true)) clickedFrame=frame;
                else if(frame==clickedFrame+1) io.AddMouseButtonEvent(0,false);
                if(clickedFrame>=0 && frame>=clickedFrame+2 && name.EndsWith("-tooltip",StringComparison.Ordinal))
                    PointAt(name=="ai-account-tooltip" ? "##fixture-account-a" : "##fixture-model-a",false,false);
                if(name=="hud-effect-bottom-menu" && frame==clickedFrame+3 && PointAt("None",false,false))
                    io.AddMouseWheelEvent(0,-10);
                if(scroll && !popupCase && name!="ai-cli-cancel" && frame==clickedFrame+3)
                { io.AddMousePosEvent(280*scale,250*scale); io.AddMouseWheelEvent(0,-15); }
            }
            if(scroll && frame>=3 && targetControl==null && !(name == "data-browser-capture-long" && frame >= 5))
            { io.AddMousePosEvent(io.DisplaySize.X/2,io.DisplaySize.Y/2); io.AddMouseWheelEvent(0,-15); }
            else if(name is "ai-middle" or "ai-chatgpt-middle" && frame==3)
            { io.AddMousePosEvent(io.DisplaySize.X/2,io.DisplaySize.Y/2); io.AddMouseWheelEvent(0,-1); }
            else if(section==1 && frame==3 && name!="interact" && targetControl==null)
            { io.AddMousePosEvent(200*scale,210*scale); io.AddMouseWheelEvent(0,-2); }
            else if(name.StartsWith("frame-",StringComparison.Ordinal) && frame==3 && targetControl==null)
            { io.AddMousePosEvent(200*scale,210*scale); io.AddMouseWheelEvent(0,-2); }
            if (name == "data-browser-capture-long" && frame >= 5 && captureStatusPosition is { } capturePosition)
                io.AddMousePosEvent(capturePosition.X + 60 * scale, capturePosition.Y + 8 * scale);
            measured.Clear();
            ImGui.NewFrame();
            if (frame > 0) ImGui.SetWindowSize(section == -1 ? "Moon companion###OmniKit.Companion.Minimal" : "OmniKit Settings###OmniKit.Settings.Minimal", new Vector2(480, 330) * scale);
            ImGui.SetNextWindowPos(new Vector2(40, 40) * scale);
            theme.Invoke(null, null);
            try
            {
                (section == -1 ? uiType.GetMethod("DrawCompanionWindow", flags)! : draw).Invoke(ui, null);
                if (measureOnly && frame == 5)
                {
                    var style = ImGui.GetStyle();
                    var available = 480 * scale - 2 * style.WindowPadding.X - style.ScrollbarSize;
                    var label = Math.Max(ImGui.CalcTextSize("Open settings").X, ImGui.CalcTextSize("Interact key").X);
                    var row = label + ImGui.CalcTextSize("Use one key with Ctrl, Alt or Shift").X + ImGui.CalcTextSize("Cancel").X
                        + 4 * style.FramePadding.X + 2 * style.ItemSpacing.X;
                    var instruction = ImGui.CalcTextSize("Hold modifiers, press a key, then release all keys. Escape cancels.").X;
                    Console.WriteLine($"Capture scale{scale:0.0}: available={available:0.0}px, longest row={row:0.0}px, instruction={instruction:0.0}px.");
                    if (row > available || instruction > available) Console.WriteLine("OVERFLOW: the capture row or instruction exceeds its available width.");
                }
            }
            finally { ImGui.PopStyleColor(15); ImGui.PopStyleVar(6); }
            ImGui.Render();
            if (wardrobeCase && popupCase && frame < 75)
                foreach (var label in wardrobeExpected)
                    if (measured.TryGetValue(label, out var item) && item.Visible)
                    {
                        if (item.Max.Y - item.Min.Y >= ImGui.GetTextLineHeight()) wardrobeChoices.Add(label);
                        // Native combo rows may be partially clipped while scrolling. Count a choice
                        // only once its full text height is visible, and preserve ImGui's screen margin.
                        if (item.Min.X < 2 || item.Min.Y < 2 || item.Max.X > io.DisplaySize.X - 2 || item.Max.Y > io.DisplaySize.Y - 2)
                        {
                            renderer.Save(ImGui.GetDrawData(), Path.Combine(output, name + "-gutter-failure.png"));
                            throw new InvalidOperationException($"Wardrobe dropdown choice escaped the viewport gutter: {label}, {item.Min}..{item.Max}, frame {frame}, popup-ready {appearanceReady}.");
                        }
                    }
            if (appearance15 && popupCase && frame == 74)
            {
                if (!ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel))
                    throw new InvalidOperationException("The appearance dropdown did not open: " + name);
                if (name == "appearance15-looks-menu" && appearanceSelection.Label == "Look 1")
                    throw new InvalidOperationException("The Looks menu did not use reviewed artwork names.");
                appearanceMenuBounds = renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"{name}-top-scale{scale:0.0}.png"));
            }
            if (providerSwitch && frame is 11 or 20 or 29 or 38)
            {
                var expected = frame switch { 11 => "CodexCli", 20 => "ClaudeCli", 29 => "ChatGpt", _ => "ApiKey" };
                if (config.GetType().GetField("AiConnection")!.GetValue(config)!.ToString() != expected ||
                    (bool)config.GetType().GetField("AiEnabled")!.GetValue(config)!)
                    throw new InvalidOperationException("Provider selection did not switch connection and revoke AI consent.");
                foreach (var field in new[] { "AiModel", "ChatGptModel", "CodexModel", "ClaudeModel" })
                    if (!Equals(config.GetType().GetField(field)!.GetValue(config), field + "-fixture"))
                        throw new InvalidOperationException("Provider selection changed another provider's model.");
                renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"ai-provider-switch-{expected}-scale{scale:0.0}.png"));
            }
            if (name == "ai-cli-cancel") uiType.GetMethod("UpdateConnection", flags)!.Invoke(ui, null);
            if (navigationCase && frame is 6 or 10 or 14 or 18 or 22 or 26)
            {
                var expected = frame is 10 or 22 ? 0 : frame == 14 ? 1 : 2;
                var actual = (int)uiType.GetField("shownCompanionTab", flags)!.GetValue(ui)!;
                renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"companion-navigation-{frame}-scale{scale:0.0}.png"));
                if (actual != expected)
                    throw new InvalidOperationException($"Companion tab navigation at frame {frame}: expected {expected}, actual {actual}, scale {scale}.");
            }
        }
        observer.SetValue(null,null);
        if (appearance15)
        {
            if (appearanceReady < 0 || appearanceHeader != null && clickedFrame < 0)
            {
                renderer.Save(ImGui.GetDrawData(), Path.Combine(output, name + "-unreachable.png"));
                throw new InvalidOperationException("New appearance controls were not reachable: " + name
                    + $" (header frame {clickedFrame}, target frame {appearanceReady}; {string.Join(", ", measured.Keys)})");
            }
            foreach (var item in measured.Values.Where(item => item.Visible))
                if (item.Min.X < 8 * scale || item.Min.Y < 8 * scale || item.Max.X > io.DisplaySize.X - 8 * scale || item.Max.Y > io.DisplaySize.Y - 8 * scale)
                    throw new InvalidOperationException("New appearance item escaped the viewport gutter: " + name);
            if (popupCase)
            {
                if (appearanceMenuBounds == null || !Equals(config.GetType().GetField(appearanceSelection.Field)!.GetValue(config), appearanceSelection.Value))
                    throw new InvalidOperationException("Selecting an appearance option did not save the expected fixture choice: " + name);
                if (appearanceSelection.Field == "CompanionVariant"
                    && (bool)config.GetType().GetField("CompanionHolidayBodiesEnabled")!.GetValue(config)!)
                    throw new InvalidOperationException("Selecting an everyday look must leave holiday-body mode.");
            }
        }
        if (wardrobeCase)
        {
            if (popupCase && wardrobeChoices.Count != wardrobeExpected.Length)
                throw new InvalidOperationException("Not all wardrobe choices became visible: " + string.Join(", ", wardrobeExpected.Except(wardrobeChoices)));
            if (name == "wardrobe-nib")
            {
                if ((bool)fixtureRoaming.GetType().GetMethod("HasEverydayLooks")!.Invoke(fixtureRoaming, ["Nib"])!
                    || (bool)fixtureRoaming.GetType().GetMethod("HasHolidayBodies")!.Invoke(fixtureRoaming, ["Nib"])!
                    || measured.ContainsKey("Look") || measured.ContainsKey("Body theme")
                    || !measured.ContainsKey("Painted costume"))
                    throw new InvalidOperationException("Nib revealed an unavailable everyday/holiday selector or hid its painted costumes.");
            }
        }
        if(targetControl != null && clickedFrame<0) throw new InvalidOperationException("Control was not reachable: "+targetControl+" in "+name);
        if (browserCaptureCase)
        {
            if ((captureStatusCalls > 0) != enabled)
                throw new InvalidOperationException("Browser capture status must only be queried when enabled.");
            if (!Equals(config.GetType().GetField("BrowserInterceptLinks")!.GetValue(config), enabled))
                throw new InvalidOperationException("Rendering browser capture status changed its setting.");
            if (scroll)
                foreach (var label in new[] { "Open browser", "Browser controls", "Open setup again" })
                    if (!measured.TryGetValue(label, out var item) || !item.Visible)
                        throw new InvalidOperationException("Expanded browser control is not reachable: " + label);
        }
        if (name is "conversation-busy" or "conversation-codex" or "conversation-claude")
        {
            if (uiType.GetField("pendingAnswer", flags)!.GetValue(ui) != null)
                throw new InvalidOperationException("Clicking the disabled Ask control submitted a question: " + name);
            if (!measured.TryGetValue("Search wiki", out var wikiControl) || !wikiControl.Visible)
                throw new InvalidOperationException("The local wiki action must remain visible while AI is paused.");
        }
        var id = $"{name}-{(scroll ? "bottom" : "top")}-scale{scale:0.0}";
        if (name == "ai-cli-cancel" && (!pendingCancellation.IsCancellationRequested || uiType.GetField("pendingConnection", flags)!.GetValue(ui) != null))
        {
            renderer.Save(ImGui.GetDrawData(), Path.Combine(output, id + "-failure.png"));
            throw new InvalidOperationException("Cancel connection did not cancel and clear the synthetic pending operation.");
        }
        if (popupCase)
        {
            if (!appearance15 && !ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel))
                throw new InvalidOperationException(id + " did not open a native dropdown.");
            foreach (var (field, value) in new[] { ("Avatar", name == "wardrobe-avatar-menu" ? "Nib" : "Moonblob"), ("CompanionOutfit", "Navigator"), ("CompanionPalette", "Moonlight") })
                if (!Equals(config.GetType().GetField(field)!.GetValue(config), value))
                    throw new InvalidOperationException(id + " unexpectedly selected a different setting.");
            if (name.StartsWith("hud-effect-",StringComparison.Ordinal))
            {
                var label=name=="hud-effect-bottom-menu" ? "Light sweep" : "None";
                if (!measured.TryGetValue(label,out var item) || !item.Visible)
                    throw new InvalidOperationException("HUD effect menu option is not reachable: "+label);
                if (config.GetType().GetField("InteractionHudEffect")!.GetValue(config)!.ToString() != "Shadow")
                    throw new InvalidOperationException("Opening the effect menu changed the saved effect.");
            }
        }
        var bounds = appearanceMenuBounds ?? renderer.Save(ImGui.GetDrawData(), Path.Combine(output, id + ".png"));
        if (name == "data-confirm")
        {
            if (!ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel))
                throw new InvalidOperationException("Import confirmation did not open.");
            foreach (var label in new[] { "Import", "Cancel" })
                if (!measured.TryGetValue(label, out var item) || !item.Visible || item.Min.X < 8 * scale || item.Min.Y < 8 * scale
                    || item.Max.X > io.DisplaySize.X - 8 * scale || item.Max.Y > io.DisplaySize.Y - 8 * scale)
                    throw new InvalidOperationException("Import confirmation button escaped the viewport gutter: " + label);
        }
        else if (bounds.Left < 2 || bounds.Top < 2 || bounds.Right >= io.DisplaySize.X - 2 || bounds.Bottom >= io.DisplaySize.Y - 2)
            throw new InvalidOperationException(id + " reaches the viewport edge.");
        if (name == "wardrobe-avatar-menu") WardrobeHeaderChecks.Draw(assembly, ui, renderer, scale, output);
        renders++;
    }
    finally { ImGui.DestroyContext(context); }
}

Type Type(string name) => assembly.GetType(name, throwOnError: true)!;
void Set(object target, string name, object? value) => target.GetType().GetField(name, flags)!.SetValue(target, value);

void CheckBrowserStatus()
{
    var ui=RuntimeHelpers.GetUninitializedObject(uiType);
    var wiki=RuntimeHelpers.GetUninitializedObject(Type("OmniKit.Integration.WikiBrowser"));
    foreach(var field in uiType.GetFields(flags).Where(field=>field.FieldType==typeof(string))) field.SetValue(ui,"");
    Set(ui,"wiki",wiki); Set(ui,"hotkeys",Activator.CreateInstance(Type("OmniKit.Ui.HotkeyCapture"),nonPublic:true));
    var publish=wiki.GetType().GetMethod("SetStatus",flags)!;
    var track=uiType.GetMethod("TrackBrowserRequest",flags)!;
    var poll=uiType.GetMethod("UpdateBrowserStatus",flags)!;
    track.Invoke(ui,[(Action)(()=>publish.Invoke(wiki,["Opening in-game browser…",false]))]);
    publish.Invoke(wiki,["Page loaded in game.",false]); poll.Invoke(ui,null);
    if((string)uiType.GetField("status",flags)!.GetValue(ui)! != "Page loaded in game.")
        throw new InvalidOperationException("Async browser status did not replace its initial opening message.");
    Set(ui,"status","Unrelated settings warning.");
    publish.Invoke(wiki,["Browser files missing. Update OmniKit.",true]); poll.Invoke(ui,null);
    if((string)uiType.GetField("status",flags)!.GetValue(ui)! != "Unrelated settings warning."
        || !(bool)uiType.GetField("settingsOpen",flags)!.GetValue(ui)!
        || !(bool)uiType.GetField("expandBrowserSettings",flags)!.GetValue(ui)!
        || (int)uiType.GetField("section",flags)!.GetValue(ui)! != 4)
        throw new InvalidOperationException("Browser failure was not revealed separately from unrelated status.");
    Set(ui,"settingsOpen",false);
    publish.Invoke(wiki,["Browser files missing. Update OmniKit.",true]); poll.Invoke(ui,null);
    if((bool)uiType.GetField("settingsOpen",flags)!.GetValue(ui)!)
        throw new InvalidOperationException("The same browser failure repeatedly reopened settings.");
}

unsafe string RenderCompanions(string name, bool animated, float time)
{
    var helper = Type("OmniKit.Ui.ProceduralCompanion");
    var renderBody = helper.GetMethod("Draw", BindingFlags.Static | BindingFlags.Public)!;
    var renderEffects = helper.GetMethod("DrawEffects", BindingFlags.Static | BindingFlags.Public)!;
    var poses = args.Contains("--companion-poses");
    var columns = poses ? new[] { "Carry", "Fall", "Land", "Push" } : new[] { "Moonlight", "Aurora", "Nebula", "Rose", "Ember", "Starlight" };
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(columns.Length * 140, 510); io.DeltaTime = 1f / 60;
        io.IniFilename = null; io.LogFilename = null;
        io.Fonts.AddFontDefault(); io.Fonts.Build();
        var renderer = new SoftwareRenderer();
        ImGui.NewFrame();
        var list = ImGui.GetForegroundDrawList();
        var avatars = new[] { "Moonblob", "Wink", "Nova" };
        for (var row = 0; row < avatars.Length; row++)
        for (var column = 0; column < columns.Length; column++)
        {
            var config = Activator.CreateInstance(Type("OmniKit.Configuration"))!;
            Set(config, "Avatar", avatars[row]); Set(config, "CompanionPalette", poses ? "Moonlight" : columns[column]); Set(config, "AnimationsEnabled", animated);
            var pose = Enum.Parse(Type("OmniKit.Ui.CompanionPose"), poses ? columns[column] : "Walk");
            var position = new Vector2(column * 140 + 10, row * 170 + 15);
            var size = new Vector2(120);
            object[] parameters = [list, position, size, config, pose, time, false];
            var startVertex = list.VtxBuffer.Size;
            renderBody.Invoke(null, parameters);
            renderEffects.Invoke(null, parameters);
            for (var vertex = startVertex; vertex < list.VtxBuffer.Size; vertex++)
            {
                var point = list.VtxBuffer[vertex].Pos;
                if (point.X < position.X || point.Y < position.Y || point.X > position.X + size.X || point.Y > position.Y + size.Y)
                    throw new InvalidOperationException("Companion geometry escaped its sprite rectangle.");
            }
            Set(config, "CompanionMagicEnabled", false);
            var vertices = list.VtxBuffer.Size;
            renderEffects.Invoke(null, parameters);
            if (list.VtxBuffer.Size != vertices) throw new InvalidOperationException("Disabled magic must emit no geometry.");
            list.AddText(position + new Vector2(0, 125), 0xFFF0F1F4, avatars[row]);
            list.AddText(position + new Vector2(0, 142), 0xFFA0B3BA, columns[column]);
        }
        ImGui.Render();
        var path = Path.Combine(output, "procedural-" + name + ".png");
        renderer.Save(ImGui.GetDrawData(), path);
        return path;
    }
    finally { ImGui.DestroyContext(context); }
}
