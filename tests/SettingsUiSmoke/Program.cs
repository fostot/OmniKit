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
if (args.Contains("--companions"))
{
    RenderCompanions("animated", animated: true, time: .75f);
    RenderCompanions("animated-later", animated: true, time: 2.1f);
    var first = RenderCompanions("static", animated: false, time: .75f);
    var second = RenderCompanions("static-later", animated: false, time: 6.2f);
    if (!File.ReadAllBytes(first).AsSpan().SequenceEqual(File.ReadAllBytes(second)))
        throw new InvalidOperationException("Disabled animations must remain pixel-identical as time changes.");
    Console.WriteLine("PASS: procedural companions rendered in six palettes; disabled animation stays static and disabled effects emit no geometry.");
    return;
}
var cases = new[]
{
    (Name: "launcher", Section: 0, Tab: 0, Enabled: false, Scroll: true),
    (Name: "interact", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "hud", Section: 2, Tab: 0, Enabled: true, Scroll: true),
    (Name: "hud-long", Section: 2, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "companion-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "outfit-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "palette-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "pose-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "voice-off", Section: 3, Tab: 1, Enabled: false, Scroll: false),
    (Name: "voice-on", Section: 3, Tab: 1, Enabled: true, Scroll: true),
    (Name: "ai-off", Section: 3, Tab: 2, Enabled: false, Scroll: false),
    (Name: "ai-on", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-middle", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "data", Section: 4, Tab: 0, Enabled: false, Scroll: true),
    (Name: "about", Section: 5, Tab: 0, Enabled: false, Scroll: true),
    (Name: "conversation-local", Section: -1, Tab: 0, Enabled: false, Scroll: false),
    (Name: "conversation-ai", Section: -1, Tab: 2, Enabled: true, Scroll: false),
    (Name: "conversation-setup", Section: -1, Tab: 0, Enabled: false, Scroll: true),
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
    if (section == -1)
    {
        Set(config, "Avatar", "Moonblob");
        Set(config, "SetupCompleted", name != "conversation-setup");
        Set(config, "VoiceEnabled", name == "conversation-ai");
    }
    var ui = RuntimeHelpers.GetUninitializedObject(uiType);
    foreach (var field in uiType.GetFields(flags).Where(field => field.FieldType == typeof(string))) field.SetValue(ui, "");
    Set(ui, "config", config);
    Set(ui, "settingsOpen", true);
    Set(ui, "companionOpen", section == -1);
    Set(ui, "section", section); Set(ui, "companionTab", tab); Set(ui, "shownCompanionTab", -1);
    Set(ui, "selectSettingsTab", true);
    Set(ui, "hotkeys", Activator.CreateInstance(Type("OmniKit.Ui.HotkeyCapture"), nonPublic: true));
    Set(ui, "roaming", RuntimeHelpers.GetUninitializedObject(Type("OmniKit.Ui.RoamingCompanion")));
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
    var popupCase = name.EndsWith("-menu", StringComparison.Ordinal);
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
        for (var frame = 0; frame < (popupCase ? 11 : 6); frame++)
        {
            if (popupCase)
            {
                // Targets are grounded in the actual minimum-size screenshots. No choice is selected.
                if (frame == 3 && name is "palette-menu" or "pose-menu")
                {
                    io.AddMousePosEvent(200 * scale, 210 * scale);
                    io.AddMouseWheelEvent(0, name == "palette-menu" ? -1 : -15);
                }
                if (frame == 6)
                {
                    var y = name switch { "companion-menu" => 237, "outfit-menu" => 267, "palette-menu" => 212, _ => 176 };
                    io.AddMousePosEvent(200 * scale, y * scale);
                    io.AddMouseButtonEvent(0, true);
                }
                if (frame == 7) io.AddMouseButtonEvent(0, false);
            }
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
        }
        var id = $"{name}-{(scroll ? "bottom" : "top")}-scale{scale:0.0}";
        if (popupCase)
        {
            if (!ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel))
                throw new InvalidOperationException(id + " did not open a native dropdown.");
            foreach (var (field, value) in new[] { ("Avatar", "Bimble"), ("CompanionOutfit", "Navigator"), ("CompanionPalette", "Moonlight") })
                if (!Equals(config.GetType().GetField(field)!.GetValue(config), value))
                    throw new InvalidOperationException(id + " unexpectedly selected a different setting.");
        }
        var bounds = renderer.Save(ImGui.GetDrawData(), Path.Combine(output, id + ".png"));
        if (bounds.Left < 2 || bounds.Top < 2 || bounds.Right >= io.DisplaySize.X - 2 || bounds.Bottom >= io.DisplaySize.Y - 2)
            throw new InvalidOperationException(id + " reaches the viewport edge.");
        renders++;
    }
    finally { ImGui.DestroyContext(context); }
}

Type Type(string name) => assembly.GetType(name, throwOnError: true)!;
void Set(object target, string name, object? value) => target.GetType().GetField(name, flags)!.SetValue(target, value);

unsafe string RenderCompanions(string name, bool animated, float time)
{
    var helper = Type("OmniKit.Ui.ProceduralCompanion");
    var renderBody = helper.GetMethod("Draw", BindingFlags.Static | BindingFlags.Public)!;
    var renderEffects = helper.GetMethod("DrawEffects", BindingFlags.Static | BindingFlags.Public)!;
    var pose = Enum.Parse(Type("OmniKit.Ui.CompanionPose"), "Walk");
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(840, 510); io.DeltaTime = 1f / 60;
        io.IniFilename = null; io.LogFilename = null;
        io.Fonts.AddFontDefault(); io.Fonts.Build();
        var renderer = new SoftwareRenderer();
        ImGui.NewFrame();
        var list = ImGui.GetForegroundDrawList();
        var avatars = new[] { "Moonblob", "Wink", "Nova" };
        var palettes = new[] { "Moonlight", "Aurora", "Nebula", "Rose", "Ember", "Starlight" };
        for (var row = 0; row < avatars.Length; row++)
        for (var column = 0; column < palettes.Length; column++)
        {
            var config = Activator.CreateInstance(Type("OmniKit.Configuration"))!;
            Set(config, "Avatar", avatars[row]); Set(config, "CompanionPalette", palettes[column]); Set(config, "AnimationsEnabled", animated);
            var position = new Vector2(column * 140 + 10, row * 170 + 15);
            var size = new Vector2(120);
            object[] parameters = [list, position, size, config, pose, time, false];
            renderBody.Invoke(null, parameters);
            renderEffects.Invoke(null, parameters);
            Set(config, "CompanionMagicEnabled", false);
            var vertices = list.VtxBuffer.Size;
            renderEffects.Invoke(null, parameters);
            if (list.VtxBuffer.Size != vertices) throw new InvalidOperationException("Disabled magic must emit no geometry.");
            list.AddText(position + new Vector2(0, 125), 0xFFF0F1F4, avatars[row]);
            list.AddText(position + new Vector2(0, 142), 0xFFA0B3BA, palettes[column]);
        }
        ImGui.Render();
        var path = Path.Combine(output, "procedural-" + name + ".png");
        renderer.Save(ImGui.GetDrawData(), path);
        return path;
    }
    finally { ImGui.DestroyContext(context); }
}
