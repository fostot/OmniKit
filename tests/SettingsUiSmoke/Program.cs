using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;

var measureOnly = args.Contains("--measure-hotkeys");
var appearance15Only = args.Contains("--appearance15");
var scene13Only = args.Contains("--scene13");
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
    (Name: "scene13-look-timer", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-motion-help", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-home-quiet", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-look", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-look-random", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-look-orbit", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-scene", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-scene-hand-off", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-motion", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-motion-frame", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-motion-static", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-motion-wink", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-home", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-home-off", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-seasonal", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-seasonal-holiday", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "scene13-seasonal-off", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "wardrobe-costume-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "wardrobe-avatar-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "wardrobe-nib", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "launcher", Section: 0, Tab: 0, Enabled: false, Scroll: true),
    (Name: "launcher-appearance", Section: 0, Tab: 0, Enabled: true, Scroll: true),
    (Name: "launcher-keys", Section: 0, Tab: 0, Enabled: true, Scroll: true),
    (Name: "settings-group-navigation", Section: 0, Tab: 0, Enabled: true, Scroll: false),
    (Name: "settings-search-jumps", Section: 2, Tab: 0, Enabled: true, Scroll: false),
    (Name: "job-switcher-off", Section: 0, Tab: 0, Enabled: false, Scroll: true),
    (Name: "job-switcher-on", Section: 0, Tab: 0, Enabled: true, Scroll: true),
    (Name: "job-switcher-conflict", Section: 0, Tab: 0, Enabled: true, Scroll: true),
    (Name: "job-switcher-help", Section: 0, Tab: 0, Enabled: true, Scroll: true),
    (Name: "nomnom-off", Section: 7, Tab: 0, Enabled: false, Scroll: true),
    (Name: "nomnom-on", Section: 7, Tab: 0, Enabled: true, Scroll: true),
    (Name: "nomnom-auto", Section: 7, Tab: 0, Enabled: true, Scroll: true),
    (Name: "nomnom-auto-menu", Section: 7, Tab: 0, Enabled: true, Scroll: false),
    (Name: "nomnom-empty", Section: 7, Tab: 0, Enabled: true, Scroll: true),
    (Name: "nomnom-missing", Section: 7, Tab: 0, Enabled: true, Scroll: true),
    (Name: "nomnom-menu", Section: 7, Tab: 0, Enabled: true, Scroll: false),
    (Name: "nomnom-help", Section: 7, Tab: 0, Enabled: true, Scroll: true),
    (Name: "nomnom-error", Section: 7, Tab: 0, Enabled: true, Scroll: true),
    (Name: "settings-tree-menu", Section: 0, Tab: 0, Enabled: false, Scroll: false),
    (Name: "settings-tree-wide", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "settings-tree-wide-max", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "settings-tree-wide-companion", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "settings-tree-wide-720", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "settings-tree-wide-960", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "settings-tree-aetheryte", Section: 0, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-empty", Section: 8, Tab: 0, Enabled: false, Scroll: false),
    (Name: "aetheryte-missing", Section: 8, Tab: 0, Enabled: false, Scroll: false),
    (Name: "aetheryte-ready", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-active", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-long", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-start", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-stop", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-area-menu", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-area-jump", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-browse", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-zone-change", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-all", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-start-all", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-current-unknown", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-footer-error", Section: 8, Tab: 0, Enabled: true, Scroll: false),
    (Name: "aetheryte-virtual", Section: 8, Tab: 0, Enabled: true, Scroll: true),
    (Name: "quest-items", Section: 6, Tab: 0, Enabled: true, Scroll: true),
    (Name: "quest-items-bar", Section: 6, Tab: 0, Enabled: true, Scroll: true),
    (Name: "seasonal", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "interact", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-types", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-people", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "interact-objects", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "interact-quest-on", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "interact-quest-error", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "interact-enemy-choose", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "interact-enemy-target", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-enemy-attack", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-none", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "hud", Section: 2, Tab: 0, Enabled: true, Scroll: true),
    (Name: "hud-position", Section: 2, Tab: 0, Enabled: true, Scroll: true),
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
    (Name: "sayings-library", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-empty", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-disabled", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-removed", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-more", Section: 3, Tab: 3, Enabled: true, Scroll: true),
    (Name: "sayings-scope-menu", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-context-menu", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-add", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-add-save", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-unicode-save", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-select", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-edit", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-remove", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-restore", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-details", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-toggle", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-current", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-current-draft", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-import-preview", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-import-apply", Section: 3, Tab: 3, Enabled: true, Scroll: false),
    (Name: "sayings-tree", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "settings-tree-wide-sayings", Section: 3, Tab: 3, Enabled: true, Scroll: false),
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
    (Name: "data-export-options", Section: 4, Tab: 0, Enabled: false, Scroll: true),
    (Name: "data-browser-capture", Section: 4, Tab: 0, Enabled: true, Scroll: true),
    (Name: "data-browser-capture-off", Section: 4, Tab: 0, Enabled: false, Scroll: true),
    (Name: "data-browser-capture-error", Section: 4, Tab: 0, Enabled: true, Scroll: true),
    (Name: "data-browser-capture-long", Section: 4, Tab: 0, Enabled: true, Scroll: true),
    (Name: "about", Section: 5, Tab: 0, Enabled: false, Scroll: true),
    (Name: "about-links", Section: 5, Tab: 0, Enabled: false, Scroll: false),
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
    if (scene13Only && !scenario.Name.StartsWith("scene13-", StringComparison.Ordinal)) continue;
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
    Set(ui, "launcherTab", name.StartsWith("job-switcher-", StringComparison.Ordinal) ? 2 : name is "launcher-help" or "launcher-keys" || measureOnly ? 1 : 0);
    Set(ui, "interactionTab", name.Contains("gathering", StringComparison.Ordinal) ? 2 : name.StartsWith("interact-quest", StringComparison.Ordinal) ? 3
        : name.StartsWith("interact-enemy", StringComparison.Ordinal) || name is "interact-types" or "interact-none" or "interact-people" or "interact-objects" ? 1 : 0);
    Set(ui, "hudTab", name.StartsWith("hud-effect", StringComparison.Ordinal) || name == "hud-none" ? 1 : name == "hud-position" ? 2 : 0);
    Set(ui, "questItemsTab", name == "quest-items-bar" ? 1 : 0);
    Set(ui, "dataTab", name.StartsWith("data-browser", StringComparison.Ordinal) ? 2 : name == "data-plugins" ? 1 : 0);
    Set(ui, "aboutTab", name == "about-links" ? 1 : 0);
    if (name == "settings-search-jumps")
    {
        foreach (var check in new[] { (0, "gearset", "launcherTab", 2), (0, "shortcut", "launcherTab", 1),
            (1, "gathering", "interactionTab", 2), (1, "quest emote", "interactionTab", 3), (1, "enemy targets", "interactionTab", 1),
            (2, "font size", "hudTab", 0), (2, "effect intensity", "hudTab", 1), (2, "position offset", "hudTab", 2),
            (4, "browser", "dataTab", 2), (4, "umbra source", "dataTab", 1), (6, "item bar", "questItemsTab", 1) })
        {
            Set(ui, "section", check.Item1);
            uiType.GetMethod("SelectSettingsSubsection", flags)!.Invoke(ui, [check.Item2]);
            if ((int)uiType.GetField(check.Item3, flags)!.GetValue(ui)! != check.Item4) throw new InvalidOperationException("Search did not select " + check.Item3);
        }
        Set(ui, "hudTab", 0);
    }
    if (name is "settings-tree-wide-max" or "settings-tree-wide-720" or "settings-tree-wide-960") Set(ui, "settingsTreeWidth", 999f);
    var unlockStarts = 0;
    var unlockStops = 0;
    uint unlockScope = uint.MaxValue;
    uint observedTerritory = name == "aetheryte-current-unknown" ? 0u : 100u;
    var unlockBusy = name is "aetheryte-active" or "aetheryte-stop";
    if (name.StartsWith("aetheryte-", StringComparison.Ordinal) || name.StartsWith("settings-tree-", StringComparison.Ordinal))
    {
        var optionType = Type("OmniKit.Unlocks.AetheryteUnlockOption");
        var options = Array.CreateInstance(optionType, name is "aetheryte-empty" or "aetheryte-missing" ? 0 : 18);
        for (var index = 0; index < options.Length; index++)
        {
            var title = name == "aetheryte-long" ? "A deliberately long synthetic aetheryte destination name to check catalogue clipping " + index : "Fixture aetheryte " + (index + 1);
            var zoneIndex = index / 6;
            var zoneNames = new[] { "Central Thanalan", "New Gridania", "Western Thanalan" };
            var argsForOption = new object?[] { (uint)(index + 1), title, zoneNames[zoneIndex], index < 4, index % 3 != 0,
                index % 3 == 0 ? "This synthetic destination is gated by an unsupported connection." : "Reachable by a supported route." };
            var constructor = optionType.GetConstructors().OrderByDescending(value => value.GetParameters().Length).First();
            var parameters = constructor.GetParameters();
            var values = new object?[parameters.Length];
            Array.Copy(argsForOption, values, argsForOption.Length);
            for (var extra = argsForOption.Length; extra < values.Length; extra++)
                values[extra] = parameters[extra].Name == "TerritoryId" ? (uint)(100 + zoneIndex)
                    : parameters[extra].Name == "IsVirtual" ? index == 17 : parameters[extra].DefaultValue;
            options.SetValue(constructor.Invoke(values), index);
        }
        var property = uiType.GetProperty("Aetherytes", flags)!;
        property.SetValue(ui, System.Linq.Expressions.Expression.Lambda(property.PropertyType,
            System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.Constant(options), property.PropertyType.GetMethod("Invoke")!.ReturnType)).Compile());
        uiType.GetProperty("AetheryteUnlockBusy", flags)!.SetValue(ui, (Func<bool>)(() => unlockBusy));
        uiType.GetProperty("AetheryteUnlockProgress", flags)!.SetValue(ui, (Func<(int, int)>)(() => unlockBusy ? (2, 6) : (0, 0)));
        uiType.GetProperty("AetheryteUnlockStatus", flags)!.SetValue(ui, (Func<string>)(() => name == "aetheryte-missing"
            ? "vnavmesh is unavailable. Install and enable the required navigation plugin before starting."
            : unlockBusy ? "Routing to the next supported destination. Unsupported or gated travel will stop with a reason."
            : "Ready to check reachable missing aetherytes."));
        uiType.GetProperty("StartAetheryteUnlocks", flags)!.SetValue(ui, (Action)(() => { unlockStarts++; unlockBusy = true; }));
        uiType.GetProperty("StopAetheryteUnlocks", flags)!.SetValue(ui, (Action)(() => { unlockStops++; unlockBusy = false; }));
        uiType.GetProperty("AetheryteCurrentTerritory", flags)!.SetValue(ui, (Func<uint>)(() => observedTerritory));
        uiType.GetProperty("StartAetheryteZoneUnlocks", flags)!.SetValue(ui, (Action<uint>)(territory => { unlockStarts++; unlockScope = territory; unlockBusy = true; }));
        if (name is "aetheryte-browse" or "aetheryte-zone-change")
        {
            Set(ui, "aetheryteView", 2);
            Set(ui, "aetheryteBrowseTerritory", 101u);
            Set(ui, "selectAetheryteZoneTab", true);
        }
        if (name is "aetheryte-all" or "aetheryte-start-all" or "aetheryte-virtual") { Set(ui, "aetheryteView", 1); Set(ui, "selectAetheryteZoneTab", true); }
    }
    if (name.StartsWith("job-switcher-", StringComparison.Ordinal))
    {
        Set(config, "CharacterJobSwitcherEnabled", enabled);
        uiType.GetProperty("JobSwitchStatus", flags)!.SetValue(ui, (Func<string>)(() =>
            "Current equipment is preserved. Waiting for the game to confirm the requested job and soul crystal before saving its new gearset."));
        uiType.GetProperty("JobSwitcherWarning", flags)!.SetValue(ui, (Func<string?>)(() => name == "job-switcher-conflict"
            ? "Simple Tweaks / HaselTweaks is loaded. Disable its Character Window Job Switcher / Character Class Switcher tweak before confirming below. OmniKit cannot inspect those tweak settings."
            : null));
    }
    if (name.StartsWith("nomnom-", StringComparison.Ordinal))
    {
        Set(config, "NomNomEnabled", enabled);
        Set(config, "NomNomFoodId", name is "nomnom-empty" or "nomnom-off" or "nomnom-auto" or "nomnom-auto-menu" ? 0u : name == "nomnom-missing" ? 999u : 1001u);
        Set(config, "NomNomPreferHighQuality", name == "nomnom-on");
        var foodType = Type("OmniKit.Automation.NomNomFoodOption");
        var foods = Array.CreateInstance(foodType, name == "nomnom-empty" ? 0 : 3);
        if (foods.Length > 0)
        {
            foods.SetValue(Activator.CreateInstance(foodType, 1001u, "Synthetic food with a deliberately long name to verify the chooser stays within its visible gutter", 0u, 12, 4, true), 0);
            foods.SetValue(Activator.CreateInstance(foodType, 1002u, "Fixture boiled egg", 0u, 7, 0, true), 1);
            foods.SetValue(Activator.CreateInstance(foodType, 1003u, "Fixture special meal", 0u, 0, 2, false), 2);
        }
        var foodProperty = uiType.GetProperty("NomNomFoods", flags)!;
        foodProperty.SetValue(ui, System.Linq.Expressions.Expression.Lambda(foodProperty.PropertyType,
            System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.Constant(foods),
                foodProperty.PropertyType.GetMethod("Invoke")!.ReturnType)).Compile());
        uiType.GetProperty("NomNomAutomaticFoodId", flags)!.SetValue(ui, (Func<uint>)(() => name == "nomnom-empty" ? 0u : 1001u));
        uiType.GetProperty("NomNomStatus", flags)!.SetValue(ui, (Func<string>)(() => name switch
        {
            "nomnom-off" => "NomNom is off.",
            "nomnom-empty" => "No food detected in personal bags.",
            "nomnom-missing" => "Selected food is not in your personal bags.",
            "nomnom-error" => "The game rejected the food request. Synthetic detail remains readable on hover without expanding this compact settings pane or hiding its controls.",
            _ => "Food buff active. Waiting until it needs refreshing."
        }));
    }
    Set(ui, "providerEndpoint", config.GetType().GetField("AiEndpoint")!.GetValue(config));
    Set(ui, "settingsOpen", true);
    if(name is "status-error" or "aetheryte-footer-error") Set(ui,"status","Could not save settings. Synthetic failure details remain available here instead of being erased by a routine Saved message.");
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
    var scene13 = name.StartsWith("scene13-", StringComparison.Ordinal);
    var appearanceSection = name.Contains("seasonal", StringComparison.Ordinal) || name is "wardrobe-costume-menu" or "wardrobe-nib" ? 4
        : name.Contains("home", StringComparison.Ordinal) || name.Contains("styles-", StringComparison.Ordinal) && name != "appearance15-styles-random" ? 3
        : name.Contains("hand", StringComparison.Ordinal) || name == "appearance15-styles-random" || name.StartsWith("scene13-scene", StringComparison.Ordinal) ? 1
        : name.Contains("motion", StringComparison.Ordinal) || name.StartsWith("frame-", StringComparison.Ordinal) || name == "pose-menu"
          || name.Contains("grounded", StringComparison.Ordinal) || name.Contains("hop-off", StringComparison.Ordinal) ? 2 : 0;
    Set(ui, "companionAppearanceSection", appearanceSection);
    if (scene13)
    {
        Set(config, "AvatarEnabled", true);
        Set(config, "CompanionFrameInteractionEnabled", name is "scene13-motion-frame" or "scene13-motion-help");
        Set(config, "CompanionHomeEnabled", name != "scene13-home-off");
        Set(config, "CompanionGrabHandEnabled", name != "scene13-scene-hand-off");
        Set(config, "CompanionAutoRotateEnabled", name != "scene13-seasonal-off");
        Set(config, "CompanionSeasonalEnabled", name != "scene13-seasonal-off");
        Set(config, "CompanionHolidayBodiesEnabled", name == "scene13-seasonal-holiday");
        Set(config, "CompanionVariant", name is "scene13-look-random" or "scene13-look-timer" ? "Random" : "Default");
        Set(config, "CompanionLookAutoRotateEnabled", name == "scene13-look-timer");
        Set(config, "CompanionNapsEnabled", name != "scene13-home-quiet");
        Set(config, "CompanionCommentsEnabled", name != "scene13-home-quiet");
        Set(config, "AnimationsEnabled", name != "scene13-motion-static");
        Set(config, "Avatar", name == "scene13-look-orbit" ? "Orbit" : name == "scene13-motion-wink" ? "Wink" : "Moonblob");
    }

    Set(ui, "hotkeys", Activator.CreateInstance(Type("OmniKit.Ui.HotkeyCapture"), nonPublic: true));
    var fixtureRoaming = RuntimeHelpers.GetUninitializedObject(Type("OmniKit.Ui.RoamingCompanion"));
    Set(fixtureRoaming, "assetDirectory", Path.GetFullPath("assets"));
    if (wardrobeCase) fixtureRoaming.GetType().GetFields(flags).Single(field => field.FieldType == config.GetType()).SetValue(fixtureRoaming, config);
    Set(ui, "roaming", fixtureRoaming);
    object? fixtureSayings = null;
    if (name.StartsWith("sayings-", StringComparison.Ordinal) || name == "settings-tree-wide-sayings")
    {
        System.Runtime.InteropServices.NativeLibrary.Load(Path.Combine(Path.GetDirectoryName(pluginPath)!, "e_sqlite3.dll"));
        var libraryType = Type("OmniKit.Companions.SayingLibrary");
        var fixtureDirectory = Path.Combine(output, "synthetic-sayings-" + Guid.NewGuid().ToString("N"));
        fixtureSayings = Activator.CreateInstance(libraryType, fixtureDirectory, null)!;
        var synthetic = Enumerable.Range(0, name == "sayings-empty" ? 0 : 24).Select(index => new
        {
            id = "fixture:" + index.ToString("00"), companion = index < 16 ? "Universal" : index < 20 ? "Moonblob" : "Orbit", context = index % 2 == 0 ? "Any" : "Walking",
            text = "Fixture saying " + index.ToString("00") + (index == 0 ? ": literal ## and ### stay visible in this synthetic row." : ": a local synthetic sentence for browsing without reading real player data."),
            enabled = index != 1 && index != 3, deleted = index == 2, builtIn = false,
        }).ToArray();
        var json = System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 1, revision = 1, entries = synthetic });
        libraryType.GetMethod("MergeImport")!.Invoke(fixtureSayings, [json]);
        uiType.GetProperty("SayingsLibrary", flags)!.SetValue(ui, fixtureSayings);
        if (name is "sayings-current" or "sayings-current-draft")
        {
            Set(ui, "sayingScope", "Current companion");
            Set(fixtureRoaming, "displayAvatar", "Orbit");
        }
        if (name == "sayings-disabled") Set(ui, "sayingState", 1);
        if (name == "sayings-removed") Set(ui, "sayingState", 2);
        if (name is "sayings-details" or "sayings-edit" or "sayings-remove" or "sayings-restore")
        {
            Set(ui, "selectedSayingId", name == "sayings-restore" ? "fixture:02" : "fixture:00");
            Set(ui, "openSayingDetails", true);
        }
        if (name.StartsWith("sayings-import-", StringComparison.Ordinal))
        {
            var incoming = new[] { new { id = "fixture:imported", companion = "Universal", context = "Idle", text = "One synthetic imported saying.", enabled = true, deleted = false, builtIn = false } };
            Set(ui, "sayingJson", System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 1, revision = 1, entries = incoming }));
        }
    }
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
    var navigationCase = name is "companion-navigation" or "settings-tree-aetheryte" or "sayings-tree";
    var groupNavigationCase = name == "settings-group-navigation";
    var providerSwitch = name == "ai-provider-switch";
    var entriesField = uiType.GetField("entries", flags)!;
    entriesField.SetValue(ui, Array.CreateInstance(entriesField.FieldType.GetElementType()!, 0));
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO();
        var logicalWidth = name == "settings-tree-wide-720" ? 720 : name == "settings-tree-wide-960" ? 960
            : name.StartsWith("settings-tree-wide", StringComparison.Ordinal) ? 860 : 480;
        io.DisplaySize = new Vector2(logicalWidth + 80, 410) * scale;
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
            "job-switcher-help" => "Job switcher help",
            "nomnom-menu" or "nomnom-auto-menu" => "##nomnom-food", "nomnom-help" => "NomNom help",
            "launcher-appearance" => "More options", "hud-position" => "Fine-tune position",
            "interact-people" => "People & places", "interact-objects" => "Objects", "data-export-options" => "More export options",
            "settings-tree-menu" => "Settings", "aetheryte-start" or "aetheryte-start-all" => "Start unlock", "aetheryte-stop" => "Stop",
            "aetheryte-area-menu" or "aetheryte-area-jump" => "##aetheryte-area-picker",
            "hud-effects" or "hud-effects-static" => "Effect settings", "interact-gathering" => "Gathering outfits", "interact-fine" => "Fine-tune targeting",
            "hud-effect-menu" or "hud-effect-bottom-menu" => "Text effect",
            "home" or "home-only" or "home-off" or "home-wink" or "home-wink-off" => null,
            "seasonal" => null,
            "interact-enemy-help" => "Enemy behavior", "frame-help" or "scene13-motion-help" => "Frame behavior", "voice-help" => "Voice shortcut help",
            "ai-cli-help" => "CLI privacy & limits", "data-import" => "Import settings", "data-plugins" => "Umbra setup",
            "data-browser" => null, "about-source" => "Source details", "about-credits" => "Credits",
            "sayings-more" or "sayings-import-preview" or "sayings-import-apply" => "More options##sayings",
            "sayings-scope-menu" => "##sayings-scope", "sayings-context-menu" => "##sayings-context",
            "sayings-add" or "sayings-add-save" or "sayings-unicode-save" or "sayings-current-draft" => "Add", "sayings-edit" => "Edit", "sayings-remove" => "Remove", "sayings-restore" => "Restore",
            "sayings-select" => "##saying-fixture:00",
            "sayings-toggle" => "saying-enabled-fixture:00",
            "conversation-help" => "Connection choices",
            "conversation-busy" or "conversation-codex" or "conversation-claude" => "Ask companion", _ => null,
        };
        string? appearanceHeader = null;
        var appearanceTarget = name is "wardrobe-costume-menu" or "wardrobe-nib" ? "Painted costume" : name == "wardrobe-avatar-menu" ? "Companion"
            : !appearance15 ? null : name == "appearance15-hand-style-menu" || name == "appearance15-styles-random" ? "Hand style"
            : appearanceSection == 3 ? "Home style" : appearanceSection == 0 ? "Look" : "Preview animation";
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
                io.AddMousePosEvent((navigationCase ? 160 : 280)*scale,250*scale);
                var direction = key != null && measured[key].Max.Y < io.DisplaySize.Y * .5f ? 1 : -1;
                io.AddMouseWheelEvent(0, direction);
            }
            return false;
        }
        var clickedFrame = -1;
        var treeStep = 0;
        var treePhase = 0;
        var treePhaseFrame = 0;
        var groupStep = 0;
        var groupPhase = 0;
        var groupPhaseFrame = 0;
        var groupChoices = new[] {
            (0,"Keys","launcherTab",1),(0,"Jobs","launcherTab",2),(0,"Search","launcherTab",0),
            (1,"Targets","interactionTab",1),(1,"Gathering","interactionTab",2),(1,"Quest actions","interactionTab",3),(1,"General","interactionTab",0),
            (2,"Effects","hudTab",1),(2,"Position","hudTab",2),(2,"Text","hudTab",0),
            (6,"Item bar","questItemsTab",1),(6,"Next action","questItemsTab",0),
            (4,"Sources","dataTab",1),(4,"Browser","dataTab",2),(4,"Settings","dataTab",0),
            (5,"Links","aboutTab",1),(5,"Info","aboutTab",0),
            (3,"Voice","companionTab",1),(3,"AI","companionTab",2),(3,"Appearance","companionTab",0),
            (3,"Sayings","companionTab",3),(3,"Appearance","companionTab",0),
            (3,"Motion","companionAppearanceSection",2),(3,"Look","companionAppearanceSection",0) };
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
        var totalFrames = groupNavigationCase ? 220 : appearance15 ? 80 : providerSwitch ? 40 : navigationCase ? 180 : targetControl != null ? 60 : name == "data-browser-capture-long" ? 12 : 8;
        for (var frame = 0; frame < totalFrames; frame++)
        {
            if (name == "aetheryte-zone-change" && frame == 4) observedTerritory = 102;
            if (groupNavigationCase && groupStep < groupChoices.Length && frame >= 3)
            {
                var choice = groupChoices[groupStep];
                if (groupPhase == 0) { Set(ui, "section", choice.Item1); groupPhase = 1; groupPhaseFrame = frame; }
                else if (groupPhase == 1 && frame > groupPhaseFrame + 1 && PointAt("tab:" + choice.Item2, true)) { groupPhase = 2; groupPhaseFrame = frame; }
                else if (groupPhase == 2 && frame > groupPhaseFrame) { io.AddMouseButtonEvent(0, false); groupPhase = 3; groupPhaseFrame = frame; }
                else if (groupPhase == 3 && frame > groupPhaseFrame + 1)
                {
                    if ((int)uiType.GetField(choice.Item3, flags)!.GetValue(ui)! != choice.Item4)
                    {
                        renderer.Save(ImGui.GetDrawData(), Path.Combine(output, "group-navigation-failure.png"));
                        throw new InvalidOperationException($"Grouped tab did not select {choice.Item2}; actual {uiType.GetField(choice.Item3, flags)!.GetValue(ui)}, frame {frame}, step {groupStep}.");
                    }
                    groupStep++; groupPhase = 0;
                }
            }
            if (appearance15 && frame >= 3)
            {
                if (appearanceHeader != null && clickedFrame < 0)
                { if (PointAt(appearanceHeader, true)) clickedFrame = frame; }
                else if (appearanceHeader != null && frame == clickedFrame + 1) io.AddMouseButtonEvent(0, false);
                else if (appearanceReady < 0 && (appearanceHeader == null || frame >= clickedFrame + 3))
                { if (PointAt(appearanceTarget!, popupCase)) appearanceReady = frame; }
                else if ((popupCase) && frame == appearanceReady + 1) io.AddMouseButtonEvent(0, false);
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
                var destinations = name == "settings-tree-aetheryte" ? new[] { "Aetheryte" } : name == "sayings-tree" ? new[] { "Sayings" } : new[] { "AI", "Look", "Voice", "AI" };
                if (treeStep < destinations.Length && frame >= 3)
                {
                    if (treePhase == 0 && PointAt("Settings", true)) { treePhase = 1; treePhaseFrame = frame; }
                    else if (treePhase == 1 && frame > treePhaseFrame) { io.AddMouseButtonEvent(0, false); treePhase = 2; treePhaseFrame = frame; }
                    else if (treePhase == 2 && frame > treePhaseFrame + 1)
                    {
                        if (destinations[treeStep] == "Look" && !measured.ContainsKey("Look##settings-tree") && PointAt("tree:Appearance", true))
                        { treePhase = 5; treePhaseFrame = frame; }
                        else if (PointAt(destinations[treeStep] + "##settings-tree", true)) { treePhase = 3; treePhaseFrame = frame; }
                    }
                    else if (treePhase == 5 && frame > treePhaseFrame) { io.AddMouseButtonEvent(0, false); treePhase = 2; treePhaseFrame = frame; }
                    else if (treePhase == 3 && frame > treePhaseFrame) { io.AddMouseButtonEvent(0, false); treePhase = 4; treePhaseFrame = frame; }
                    else if (treePhase == 4 && frame > treePhaseFrame + 1)
                    {
                        var actualSection = (int)uiType.GetField("section", flags)!.GetValue(ui)!;
                        var actualTab = (int)uiType.GetField("companionTab", flags)!.GetValue(ui)!;
                        var expectedTab = destinations[treeStep] == "Sayings" ? 3 : destinations[treeStep] == "AI" ? 2 : destinations[treeStep] == "Voice" ? 1 : 0;
                        if (actualSection != (name == "settings-tree-aetheryte" ? 8 : 3) || name != "settings-tree-aetheryte" && actualTab != expectedTab)
                            throw new InvalidOperationException("Tree navigation did not select " + destinations[treeStep]);
                        treeStep++; treePhase = 0;
                    }
                }
            }
            if(targetControl != null && frame>=3)
            {
                if(clickedFrame<0 && PointAt(targetControl,true)) clickedFrame=frame;
                else if (name == "job-switcher-help" && clickedFrame < 0)
                { io.AddMousePosEvent(280 * scale, 250 * scale); io.AddMouseWheelEvent(0, -2); }
                else if(frame==clickedFrame+1) io.AddMouseButtonEvent(0,false);
                if (name == "sayings-current-draft" && frame == clickedFrame + 2) Set(fixtureRoaming, "displayAvatar", "Mochi");
                if (name is "sayings-add-save" or "sayings-edit" or "sayings-unicode-save" or "sayings-current-draft" && frame == clickedFrame + 3)
                {
                    Set(ui, "sayingEditingText", name == "sayings-unicode-save" ? string.Concat(Enumerable.Repeat("\U0001F600", 240)) : "A synthetic saying saved through the actual editor.");
                    if (!PointAt("Save saying", true)) throw new InvalidOperationException("The saying editor Save action is not reachable.");
                }
                if (name is "sayings-add-save" or "sayings-edit" or "sayings-unicode-save" or "sayings-current-draft" && frame == clickedFrame + 4) io.AddMouseButtonEvent(0, false);
                if (name.StartsWith("sayings-import-", StringComparison.Ordinal))
                {
                    if (frame == clickedFrame + 3 && !PointAt("Import JSON", true)) throw new InvalidOperationException("Import JSON is not reachable.");
                    if (frame is var importRelease && (importRelease == clickedFrame + 4 || importRelease == clickedFrame + 7 || importRelease == clickedFrame + 10)) io.AddMouseButtonEvent(0, false);
                    if (frame == clickedFrame + 6 && !PointAt("Preview", true)) throw new InvalidOperationException("Import preview is not reachable.");
                    if (name == "sayings-import-apply" && frame == clickedFrame + 9 && !PointAt("Apply", true)) throw new InvalidOperationException("Import Apply is not reachable.");
                }
                if (name == "aetheryte-area-jump" && frame == clickedFrame + 3 && !PointAt("New Gridania##pick-zone-101", true))
                    throw new InvalidOperationException("The searchable area picker did not expose New Gridania.");
                if (name == "aetheryte-area-jump" && frame == clickedFrame + 4) io.AddMouseButtonEvent(0, false);
                if(clickedFrame>=0 && frame>=clickedFrame+2 && name.EndsWith("-tooltip",StringComparison.Ordinal))
                    PointAt(name=="ai-account-tooltip" ? "##fixture-account-a" : "##fixture-model-a",false,false);
                if(name=="hud-effect-bottom-menu" && frame==clickedFrame+3 && PointAt("None",false,false))
                    io.AddMouseWheelEvent(0,-10);
                if(scroll && !popupCase && name!="ai-cli-cancel" && frame==clickedFrame+3)
                { io.AddMousePosEvent(280*scale,250*scale); io.AddMouseWheelEvent(0,-15); }
            }
            if(scroll && frame>=3 && targetControl==null && !(name == "data-browser-capture-long" && frame >= 5))
            {
                if (name == "aetheryte-virtual" && measured.TryGetValue("aetheryte-catalogue", out var catalogue))
                    io.AddMousePosEvent((catalogue.Min.X + catalogue.Max.X) / 2, (catalogue.Min.Y + catalogue.Max.Y) / 2);
                else io.AddMousePosEvent(io.DisplaySize.X/2,io.DisplaySize.Y/2);
                io.AddMouseWheelEvent(0,-15);
            }
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
            if (frame > 0) ImGui.SetWindowSize(section == -1 ? "Moon companion###OmniKit.Companion.Minimal" : "OmniKit Settings###OmniKit.Settings.Minimal", new Vector2(logicalWidth, 330) * scale);
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
        }
        observer.SetValue(null,null);
        if (groupNavigationCase && groupStep != groupChoices.Length) throw new InvalidOperationException("Grouped tabs were not all reachable.");
        if (navigationCase && treeStep != (name is "settings-tree-aetheryte" or "sayings-tree" ? 1 : 4))
        {
            renderer.Save(ImGui.GetDrawData(), Path.Combine(output, name + "-navigation-failure.png"));
            throw new InvalidOperationException($"Tree navigation did not finish every requested selection: {name}, step {treeStep}, phase {treePhase}, section {uiType.GetField("section", flags)!.GetValue(ui)}.");
        }
        if (fixtureSayings != null)
        {
            if (name == "sayings-more" && scroll)
                foreach (var label in new[] { "Comment interval", "Copy sayings JSON", "Import JSON" })
                    if (!measured.TryGetValue(label, out var control) || !control.Visible)
                        throw new InvalidOperationException("Sayings option is not reachable: " + label);
            var snapshot = ((System.Collections.IEnumerable)fixtureSayings.GetType().GetMethod("GetAll")!.Invoke(fixtureSayings, null)!).Cast<object>().ToArray();
            object? Entry(string id) => snapshot.FirstOrDefault(entry => (string)entry.GetType().GetProperty("Id")!.GetValue(entry)! == id);
            if (name == "sayings-add-save" && snapshot.Length != 25) throw new InvalidOperationException("Adding a saying did not preserve all existing entries.");
            if (name == "sayings-current" && (!measured.TryGetValue("##saying-fixture:20", out var visibleSaying) || !visibleSaying.Visible || measured.ContainsKey("##saying-fixture:16")))
                throw new InvalidOperationException("Current companion did not filter the actually visible Orbit.");
            if (name == "sayings-current-draft" && !snapshot.Any(entry => (string)entry.GetType().GetProperty("Text")!.GetValue(entry)! == "A synthetic saying saved through the actual editor."
                && (string)entry.GetType().GetProperty("Companion")!.GetValue(entry)! == "Orbit"))
                throw new InvalidOperationException("Auto-rotation moved the open editor to another companion.");
            if (name == "sayings-unicode-save" && !snapshot.Any(entry => (string)entry.GetType().GetProperty("Text")!.GetValue(entry)! == string.Concat(Enumerable.Repeat("\U0001F600", 240))))
                throw new InvalidOperationException("The editor did not preserve all 240 Unicode scalar characters.");
            if (name == "sayings-edit" && (string)Entry("fixture:00")!.GetType().GetProperty("Text")!.GetValue(Entry("fixture:00"))! != "A synthetic saying saved through the actual editor.")
                throw new InvalidOperationException("The saying edit was not saved.");
            if (name == "sayings-toggle" && (bool)Entry("fixture:00")!.GetType().GetProperty("Enabled")!.GetValue(Entry("fixture:00"))!) throw new InvalidOperationException("The saying checkbox did not disable its entry.");
            if (name == "sayings-remove" && (!(bool)Entry("fixture:00")!.GetType().GetProperty("Deleted")!.GetValue(Entry("fixture:00"))! || snapshot.Length != 24)) throw new InvalidOperationException("Remove did not preserve the soft-deleted saying.");
            if (name == "sayings-restore" && (bool)Entry("fixture:02")!.GetType().GetProperty("Deleted")!.GetValue(Entry("fixture:02"))!) throw new InvalidOperationException("Restore did not recover its saying.");
            if (name == "sayings-import-preview" && snapshot.Length != 24) throw new InvalidOperationException("Preview modified the sayings library.");
            if (name == "sayings-import-apply" && (snapshot.Length != 25 || Entry("fixture:00") == null)) throw new InvalidOperationException("Import did not merge while preserving existing sayings.");
            if (name is "sayings-add" or "sayings-details" or "sayings-select" or "sayings-import-preview")
            {
                foreach (var label in name == "sayings-add" ? new[] { "Save saying", "Cancel" } : name is "sayings-details" or "sayings-select" ? new[] { "Edit", "Remove", "Close" } : new[] { "Preview", "Apply", "Cancel" })
                    if (!measured.TryGetValue(label, out var item) || !item.Visible || item.Min.X < 8 * scale || item.Max.X > io.DisplaySize.X - 8 * scale || item.Min.Y < 8 * scale || item.Max.Y > io.DisplaySize.Y - 8 * scale)
                        throw new InvalidOperationException("Sayings dialog action escaped the viewport gutter: " + label);
            }
        }
        if (name.StartsWith("aetheryte-", StringComparison.Ordinal) || name.StartsWith("settings-tree-wide", StringComparison.Ordinal) && section == 8)
        {
            foreach (var label in new[] { "Start unlock", "Stop" })
                if (!measured.TryGetValue(label, out var item) || !item.Visible || item.Min.X < 8 * scale || item.Max.X > io.DisplaySize.X - 8 * scale)
                    throw new InvalidOperationException("Unlock action is not reachable outside the catalogue: " + label);
            if (name == "aetheryte-start" && (unlockStarts != 1 || unlockStops != 0)) throw new InvalidOperationException("Start did not queue exactly one synthetic request.");
            if (name == "aetheryte-start" && unlockScope != 100) throw new InvalidOperationException("Current zone Start did not preserve its scope.");
            if (name == "aetheryte-start-all" && (unlockStarts != 1 || unlockScope != 0)) throw new InvalidOperationException("All areas Start did not request the global scope.");
            if (name == "aetheryte-stop" && (unlockStops != 1 || unlockStarts != 0)) throw new InvalidOperationException("Stop did not queue exactly one synthetic request.");
            if (name is "aetheryte-browse" or "aetheryte-zone-change" or "aetheryte-area-jump")
                if ((uint)uiType.GetField("aetheryteBrowseTerritory", flags)!.GetValue(ui)! != 101 || (int)uiType.GetField("aetheryteView", flags)!.GetValue(ui)! != 2)
                    throw new InvalidOperationException("Manual zone browsing was not preserved.");
            if (name == "aetheryte-current-unknown" && unlockStarts != 0) throw new InvalidOperationException("Unknown current zone queued an unlock request.");
            if (name == "aetheryte-virtual" && scroll && (!measured.TryGetValue("travel-exit-18", out var exitRow) || !exitRow.Visible))
                throw new InvalidOperationException("Virtual travel exit was not reachable in the catalogue scroll.");
        }
        if (name.StartsWith("settings-tree-wide", StringComparison.Ordinal) && (float)uiType.GetField("settingsTreeWidth", flags)!.GetValue(ui)! > 220)
            throw new InvalidOperationException("Tree resizing did not preserve the minimum content width.");
        if (name.StartsWith("nomnom-", StringComparison.Ordinal))
        {
            foreach (var label in scroll ? new[] { "Refresh food", "NomNom help" }
                         : name is "nomnom-menu" or "nomnom-auto-menu" ? Array.Empty<string>() : new[] { "##nomnom-food" })
                if (!measured.TryGetValue(label, out var item) || !item.Visible)
                    throw new InvalidOperationException("Nom Nom control is not reachable: " + label);
            if (name is "nomnom-menu" or "nomnom-auto-menu")
            {
                if (!measured.TryGetValue("Automatic: best +EXP food", out var automaticOption) || !automaticOption.Visible)
                    throw new InvalidOperationException("Automatic food choice is not reachable.");
                foreach (var foodOptionId in new[] { "##food-1001", "##food-1002", "##food-1003" })
                {
                    var option = measured.FirstOrDefault(pair => pair.Key.EndsWith(foodOptionId, StringComparison.Ordinal));
                    if (!option.Value.Visible || option.Value.Min.X < 8 * scale || option.Value.Max.X > io.DisplaySize.X - 8 * scale)
                        throw new InvalidOperationException("Nom Nom food option escaped the visible gutter: " + foodOptionId);
                }
            }
            var expectedFood = name is "nomnom-empty" or "nomnom-off" or "nomnom-auto" or "nomnom-auto-menu" ? 0u : name == "nomnom-missing" ? 999u : 1001u;
            if (!Equals(config.GetType().GetField("NomNomFoodId")!.GetValue(config), expectedFood))
                throw new InvalidOperationException("Rendering Nom Nom changed the selected food.");
        }
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
                foreach (var label in new[] { "Open browser", "Browser controls" })
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
        else if (name is not ("sayings-add" or "sayings-details" or "sayings-select" or "sayings-import-preview")
            && (bounds.Left < 2 || bounds.Top < 2 || bounds.Right >= io.DisplaySize.X - 2 || bounds.Bottom >= io.DisplaySize.Y - 2))
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
