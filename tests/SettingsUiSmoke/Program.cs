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
    (Name: "launcher", Section: 0, Tab: 0, Enabled: false, Scroll: true),
    (Name: "interact", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-types", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-enemy-choose", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "interact-enemy-target", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-enemy-attack", Section: 1, Tab: 0, Enabled: true, Scroll: false),
    (Name: "interact-none", Section: 1, Tab: 0, Enabled: true, Scroll: true),
    (Name: "hud", Section: 2, Tab: 0, Enabled: true, Scroll: true),
    (Name: "hud-long", Section: 2, Tab: 0, Enabled: true, Scroll: false),
    (Name: "appearance", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "companion-navigation", Section: 3, Tab: 0, Enabled: false, Scroll: false),
    (Name: "frame-on", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "frame-static", Section: 3, Tab: 0, Enabled: true, Scroll: true),
    (Name: "companion-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
    (Name: "outfit-menu", Section: 3, Tab: 0, Enabled: true, Scroll: false),
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
    (Name: "ai-endpoint", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-codex", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-claude", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-cli-busy", Section: 3, Tab: 2, Enabled: true, Scroll: true),
    (Name: "ai-cli-cancel", Section: 3, Tab: 2, Enabled: true, Scroll: false),
    (Name: "ai-cli-error", Section: 3, Tab: 2, Enabled: true, Scroll: true),
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
    var cliCase = name is "ai-codex" or "ai-claude" or "ai-cli-busy" or "ai-cli-error" or "ai-cli-cancel";
    if (chatGptCase || cliCase)
        Set(config, "AiConnection", Enum.Parse(Type("OmniKit.Assistant.AiConnectionKind"),
            chatGptCase ? "ChatGpt" : name == "ai-claude" ? "ClaudeCli" : "CodexCli"));
    if (name.StartsWith("frame-", StringComparison.Ordinal))
    {
        Set(config, "CompanionFrameInteractionEnabled", true);
        Set(config, "AnimationsEnabled", name != "frame-static");
    }
    if (name.StartsWith("interact-enemy-", StringComparison.Ordinal))
    {
        Set(config, "InteractionTargets", Enum.Parse(Type("OmniKit.Interaction.InteractionTargets"), "All"));
        Set(config, "EnemyInteractMode", Enum.Parse(Type("OmniKit.Interaction.EnemyInteractMode"),
            name.EndsWith("target", StringComparison.Ordinal) ? "TargetOnly" : name.EndsWith("attack", StringComparison.Ordinal) ? "StartAttack" : "Choose"));
    }
    if (name == "interact-none") Set(config, "InteractionTargets", Enum.Parse(Type("OmniKit.Interaction.InteractionTargets"), "None"));
    if (section == -1)
    {
        Set(config, "Avatar", "Moonblob");
        Set(config, "SetupCompleted", name != "conversation-setup");
        Set(config, "VoiceEnabled", name == "conversation-ai");
    }
    var ui = RuntimeHelpers.GetUninitializedObject(uiType);
    foreach (var field in uiType.GetFields(flags).Where(field => field.FieldType == typeof(string))) field.SetValue(ui, "");
    Set(ui, "config", config);
    Set(ui, "providerEndpoint", config.GetType().GetField("AiEndpoint")!.GetValue(config));
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
            "A deliberately very long visible target name used to check that every word wraps inside the settings content area without covering the scrollbar",
            Enum.Parse(Type("OmniKit.Interaction.InteractionTargets"), "Npcs")));
    }
    Set(ui, "interaction", interaction);
    Set(ui, "voice", Activator.CreateInstance(Type("OmniKit.Assistant.VoiceInput"), nonPublic: true));
    // HasApiKey inspects only an empty harness path, never the user's credential store.
    var chat = RuntimeHelpers.GetUninitializedObject(Type("OmniKit.Assistant.ChatService"));
    Set(chat, "config", config);
    var emptyCredentials = new DirectoryInfo(Path.Combine(output, "empty-credentials-" + Guid.NewGuid().ToString("N")));
    Set(chat, "credentials", Activator.CreateInstance(Type("OmniKit.Assistant.CredentialStore"), emptyCredentials));
    using var chatGpt = (IDisposable)Activator.CreateInstance(Type("OmniKit.Assistant.ChatGptConnection"), emptyCredentials)!;
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
        var extraInteraction = popupCase || name is "ai-endpoint" or "ai-cli-cancel";
        for (var frame = 0; frame < (providerSwitch ? 40 : navigationCase ? 28 : extraInteraction ? 11 : 6); frame++)
        {
            if (providerSwitch)
            {
                if (frame is 3 or 12 or 21 or 30)
                {
                    Set(config, "AiEnabled", true);
                    io.AddMousePosEvent(200 * scale, 247 * scale); io.AddMouseButtonEvent(0, true);
                }
                if (frame is 4 or 13 or 22 or 31 or 8 or 17 or 26 or 35) io.AddMouseButtonEvent(0, false);
                if (frame is 7 or 16 or 25 or 34)
                {
                    var y = frame switch { 7 => 320, 16 => 342, 25 => 298, _ => 276 };
                    io.AddMousePosEvent(200 * scale, y * scale); io.AddMouseButtonEvent(0, true);
                }
            }
            if (navigationCase)
            {
                if (frame is 3 or 7 or 11 or 15)
                {
                    var x = frame == 7 ? 92 : frame == 11 ? 155 : 195;
                    io.AddMousePosEvent(x * scale, 145 * scale);
                    io.AddMouseButtonEvent(0, true);
                }
                if (frame is 4 or 8 or 12 or 16) io.AddMouseButtonEvent(0, false);
                if (frame is 19 or 23) Set(ui, "companionTab", frame == 19 ? 0 : 2);
            }
            if (extraInteraction)
            {
                // Targets are grounded in the actual minimum-size screenshots. No choice is selected.
                if (frame == 3 && name is "palette-menu" or "pose-menu" or "ai-account-menu" or "ai-model-menu" or "ai-account-tooltip" or "ai-model-tooltip" or "ai-endpoint" or "ai-cli-cancel")
                {
                    io.AddMousePosEvent(200 * scale, 210 * scale);
                    io.AddMouseWheelEvent(0, name == "palette-menu" ? -1 : name == "pose-menu" ? -2 : -30);
                }
                if (frame == 6)
                {
                    var y = name switch { "companion-menu" => 237, "outfit-menu" => 267, "palette-menu" => 212,
                        "ai-provider-menu" => 247, "ai-account-menu" or "ai-account-tooltip" => 141, "ai-model-menu" or "ai-model-tooltip" => 261,
                        "ai-endpoint" or "ai-cli-cancel" => 260, _ => 218 };
                    io.AddMousePosEvent((name == "ai-cli-cancel" ? 110 : 200) * scale, y * scale);
                    io.AddMouseButtonEvent(0, true);
                }
                if (frame == 7) io.AddMouseButtonEvent(0, false);
                if (name == "ai-endpoint" && frame == 8) io.AddMouseWheelEvent(0, -30);
                if (frame == 8 && name.EndsWith("-tooltip", StringComparison.Ordinal))
                    io.AddMousePosEvent(180 * scale, (name == "ai-account-tooltip" ? 170 : 291) * scale);
            }
            if (scroll && frame >= 3)
            {
                io.AddMousePosEvent(io.DisplaySize.X / 2, io.DisplaySize.Y / 2);
                io.AddMouseWheelEvent(0, -15);
            }
            else if (name is "ai-middle" or "ai-chatgpt-middle" && frame == 3)
            {
                io.AddMousePosEvent(io.DisplaySize.X / 2, io.DisplaySize.Y / 2);
                io.AddMouseWheelEvent(0, name == "ai-chatgpt-middle" ? -3 : -1);
            }
            else if (section == 1 && frame == 3 && name != "interact")
            {
                io.AddMousePosEvent(200 * scale, 210 * scale);
                io.AddMouseWheelEvent(0, name.StartsWith("interact-enemy-", StringComparison.Ordinal) ? -5 : -2);
            }
            else if (name.StartsWith("frame-", StringComparison.Ordinal) && frame == 3)
            {
                io.AddMousePosEvent(200 * scale, 210 * scale);
                io.AddMouseWheelEvent(0, -4);
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
        var id = $"{name}-{(scroll ? "bottom" : "top")}-scale{scale:0.0}";
        if (name == "ai-cli-cancel" && (!pendingCancellation.IsCancellationRequested || uiType.GetField("pendingConnection", flags)!.GetValue(ui) != null))
        {
            renderer.Save(ImGui.GetDrawData(), Path.Combine(output, id + "-failure.png"));
            throw new InvalidOperationException("Cancel connection did not cancel and clear the synthetic pending operation.");
        }
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
