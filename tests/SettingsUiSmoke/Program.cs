using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;

var measureOnly = args.Contains("--measure-hotkeys");
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
var cases = new[]
{
    (Name: "launcher", Section: 0, Tab: 0, Enabled: false, Scroll: true),
    (Name: "interact", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "hud", Section: 2, Tab: 0, Enabled: true, Scroll: true),
    (Name: "hud-long", Section: 2, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "voice-off", Section: 3, Tab: 1, Enabled: false, Scroll: false),
    (Name: "voice-on", Section: 3, Tab: 1, Enabled: true, Scroll: true),
    (Name: "ai-off", Section: 3, Tab: 2, Enabled: false, Scroll: false),
    (Name: "ai-on", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-middle", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "data", Section: 4, Tab: 0, Enabled: false, Scroll: true),
};
var renders = 0;
foreach (var scale in new[] { 1f, 1.5f, 2f })
foreach (var scenario in cases)
{
    if (measureOnly && scenario.Name != "launcher") continue;
    if (caseFilter is not null && !caseFilter.Contains(scenario.Name)) continue;
    if (scaleFilter is not null && scale.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) != scaleFilter) continue;
    Render(scenario.Name, scenario.Section, scenario.Tab, scenario.Enabled, scale, scroll: false);
    if (scenario.Scroll && !measureOnly) Render(scenario.Name, scenario.Section, scenario.Tab, scenario.Enabled, scale, scroll: true);
}
Console.WriteLine($"PASS: {renders} actual compiled settings renders completed without starting game, voice or network services.");

unsafe void Render(string name, int section, int tab, bool enabled, float scale, bool scroll)
{
    var config = Activator.CreateInstance(Type("OmniKit.Configuration"))!;
    Set(config, "SoftInteractEnabled", section == 1 && enabled);
    Set(config, "VoiceEnabled", tab == 1 && enabled);
    Set(config, "AiEnabled", tab == 2 && enabled);
    var ui = RuntimeHelpers.GetUninitializedObject(uiType);
    foreach (var field in uiType.GetFields(flags).Where(field => field.FieldType == typeof(string))) field.SetValue(ui, "");
    Set(ui, "config", config);
    Set(ui, "settingsOpen", true);
    Set(ui, "section", section); Set(ui, "companionTab", tab); Set(ui, "shownCompanionTab", -1);
    Set(ui, "selectSettingsTab", true);
    Set(ui, "hotkeys", Activator.CreateInstance(Type("OmniKit.Ui.HotkeyCapture"), nonPublic: true));
    var interactionType = Type("OmniKit.Interaction.SoftInteractionService");
    var interaction = RuntimeHelpers.GetUninitializedObject(interactionType);
    if (name == "hud-long")
    {
        var candidateType = interactionType.GetNestedType("Candidate", BindingFlags.NonPublic)!;
        Set(interaction, "candidate", Activator.CreateInstance(candidateType, 0UL, IntPtr.Zero,
            "A deliberately very long visible target name used to check that every word wraps inside the settings content area without covering the scrollbar"));
    }
    Set(ui, "interaction", interaction);
    Set(ui, "voice", Activator.CreateInstance(Type("OmniKit.Assistant.VoiceInput"), nonPublic: true));
    // HasApiKey inspects only an empty harness path, never the user's credential store.
    var chat = RuntimeHelpers.GetUninitializedObject(Type("OmniKit.Assistant.ChatService"));
    Set(chat, "config", config);
    Set(chat, "credentials", Activator.CreateInstance(Type("OmniKit.Assistant.CredentialStore"),
        new DirectoryInfo(Path.Combine(output, "empty-credentials-" + Guid.NewGuid().ToString("N")))));
    Set(ui, "chat", chat);
    var entriesField = uiType.GetField("entries", flags)!;
    entriesField.SetValue(ui, Array.CreateInstance(entriesField.FieldType.GetElementType()!, 0));
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(600, 440) * scale;
        io.FontGlobalScale = scale;
        io.DeltaTime = 1f / 60;
        io.IniFilename = null; io.LogFilename = null;
        var font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        if (File.Exists(font)) io.Fonts.AddFontFromFileTTF(font, 17);
        else io.Fonts.AddFontDefault();
        io.Fonts.Build();
        var renderer = new SoftwareRenderer();
        ImGui.StyleColorsDark();
        for (var frame = 0; frame < 6; frame++)
        {
            if (scroll && frame >= 3)
            {
                io.AddMousePosEvent(io.DisplaySize.X / 2, io.DisplaySize.Y / 2);
                io.AddMouseWheelEvent(0, -15);
            }
            else if (name == "ai-middle" && frame == 3)
            {
                io.AddMousePosEvent(io.DisplaySize.X / 2, io.DisplaySize.Y / 2);
                io.AddMouseWheelEvent(0, -1);
            }
            ImGui.NewFrame();
            if (frame > 0) ImGui.SetWindowSize("OmniKit Settings###OmniKit.Settings.Compact", new Vector2(520, 360) * scale);
            ImGui.SetNextWindowPos(new Vector2(40, 40) * scale);
            theme.Invoke(null, null);
            try
            {
                draw.Invoke(ui, null);
                if (measureOnly && frame == 5)
                {
                    var style = ImGui.GetStyle();
                    var available = 520 * scale - 2 * style.WindowPadding.X - style.ScrollbarSize;
                    var label = Math.Max(ImGui.CalcTextSize("Open settings").X, ImGui.CalcTextSize("Interact key").X);
                    var row = label + ImGui.CalcTextSize("Use one key with Ctrl, Alt or Shift").X + ImGui.CalcTextSize("Cancel").X
                        + 4 * style.FramePadding.X + 2 * style.ItemSpacing.X;
                    var instruction = ImGui.CalcTextSize("Hold modifiers, press a key, then release all keys. Escape cancels.").X;
                    Console.WriteLine($"Capture scale{scale:0.0}: available={available:0.0}px, longest row={row:0.0}px, instruction={instruction:0.0}px.");
                    if (row > available || instruction > available) Console.WriteLine("OVERFLOW: the capture row or instruction exceeds its available width.");
                }
            }
            finally { ImGui.PopStyleColor(11); ImGui.PopStyleVar(5); }
            ImGui.Render();
        }
        var id = $"{name}-{(scroll ? "bottom" : "top")}-scale{scale:0.0}";
        var bounds = renderer.Save(ImGui.GetDrawData(), Path.Combine(output, id + ".png"));
        if (bounds.Left < 2 || bounds.Top < 2 || bounds.Right >= io.DisplaySize.X - 2 || bounds.Bottom >= io.DisplaySize.Y - 2)
            throw new InvalidOperationException(id + " reaches the viewport edge.");
        renders++;
    }
    finally { ImGui.DestroyContext(context); }
}

Type Type(string name) => assembly.GetType(name, throwOnError: true)!;
void Set(object target, string name, object? value) => target.GetType().GetField(name, flags)!.SetValue(target, value);
