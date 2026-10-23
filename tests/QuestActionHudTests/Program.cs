using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;

var pluginPath = Path.GetFullPath(args.FirstOrDefault(x => !x.StartsWith("--", StringComparison.Ordinal))
    ?? "src/OmniKit/bin/Release/OmniKit.dll");
var output = Path.GetFullPath(".local/visualharness/quest-actions");
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
var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
var failures = new List<string>();
var checks = 0;
var renders = 0;
var texture = Type("OmniKit.Ui.ActionButtonRenderer").GetProperty("TextureForTest", BindingFlags.Static | BindingFlags.NonPublic)!;
texture.SetValue(null, (Func<uint, ImTextureID>)(_ => new ImTextureID(2UL)));
foreach (var scale in new[] { 1f, 1.5f, 2f })
foreach (var width in new[] { 320, 480 })
{
    foreach (var locked in new[] { true, false })
    {
        Run($"next-{(locked ? "locked" : "unlocked")}", true, false, 0, locked, true, scale, new(width, 360));
        foreach (var count in new[] { 0, 1, 8, 11 })
            Run($"items-{count}-{(locked ? "locked" : "unlocked")}", false, true, count, locked, true, scale, new(width, 360));
    }
    Run("next-disabled", true, false, 0, false, false, scale, new(width, 360));
    Run("items-disabled", false, true, 8, false, false, scale, new(width, 360));
    Run("combined-default", true, true, 8, true, true, scale, new(width, 720));
    Run("edges", true, true, 8, false, true, scale, new(width, 360), edge: true);
}
foreach (var scale in new[] { 1f, 2f })
{
    Run("next-input", true, false, 0, true, true, scale, new(480, 360), input: true);
    Run("items-input", false, true, 11, true, true, scale, new(480, 360), input: true);
    Run("disabled-input", true, false, 0, true, false, scale, new(480, 360), input: true);
}
texture.SetValue(null, null);
foreach (var failure in failures) Console.WriteLine("FAIL: " + failure);
Console.WriteLine($"Actual HUD: {checks} checks, {renders} renders, {failures.Count} failures. No framework update or game service was invoked.");
Environment.ExitCode = failures.Count == 0 ? 0 : 1;

Type Type(string name) => assembly.GetType(name, throwOnError: true)!;
void Set(object instance, string name, object? value) => instance.GetType().GetField(name, flags)!.SetValue(instance, value);
object? Get(object instance, string name) => instance.GetType().GetField(name, flags)!.GetValue(instance);
void Check(bool value, string message) { ++checks; if (!value) failures.Add(message); }

unsafe void Run(string name, bool next, bool items, int count, bool locked, bool enabled, float scale,
    Vector2 viewport, bool edge = false, bool input = false)
{
    var label = $"{name}-{viewport.X:0}-scale{scale:0.0}";
    var config = Activator.CreateInstance(Type("OmniKit.Configuration"))!;
    Set(config, "SoftInteractEnabled", true);
    Set(config, "NextActionEnabled", next); Set(config, "QuestItemsBarEnabled", items);
    Set(config, "NextActionLocked", locked); Set(config, "QuestItemsBarLocked", locked);
    if (edge)
    {
        Set(config, "NextActionX", 0f); Set(config, "NextActionY", 1f);
        Set(config, "QuestItemsBarX", 1f); Set(config, "QuestItemsBarY", 0f);
    }
    var saves = new Counter();
    var persistType = typeof(Action<>).MakeGenericType(config.GetType());
    var persist = Expression.Lambda(persistType,
        Expression.Call(Expression.Constant(saves), typeof(Counter).GetMethod(nameof(Counter.Add))!),
        Expression.Parameter(config.GetType())).Compile();
    Set(config, "Persist", persist);
    var interaction = RuntimeHelpers.GetUninitializedObject(Type("OmniKit.Interaction.SoftInteractionService"));
    Set(interaction, "<ActionPaused>k__BackingField", false);
    Set(interaction, "<InventoryActionPaused>k__BackingField", false);
    var candidateType = interaction.GetType().GetNestedType("Candidate", BindingFlags.NonPublic)!;
    var candidate = Activator.CreateInstance(candidateType, [42UL, (nint)128, "Synthetic target",
        Enum.Parse(Type("OmniKit.Interaction.InteractionTargets"), "QuestObjects")]);
    Set(interaction, "candidate", candidate);
    var preview = Activator.CreateInstance(Type("OmniKit.Interaction.InteractionActionPreview"),
        [42UL, (nint)128, "Synthetic quest item", "Use quest item", 100u, enabled,
            enabled ? "" : "The item is not usable on this target right now. Check your range and state."]);
    Set(interaction, "nextAction", preview);
    var inventory = Activator.CreateInstance(Type("OmniKit.Interaction.QuestInventoryService"))!;
    var entries = Array.CreateInstance(Type("OmniKit.Interaction.QuestInventoryEntry"), count);
    var target = Activator.CreateInstance(Type("OmniKit.Interaction.QuestInventoryTarget"), [42UL, (nint)128, 135u]);
    for (var i = 0; i < count; ++i)
        entries.SetValue(Activator.CreateInstance(entries.GetType().GetElementType()!,
            [Enum.Parse(Type("OmniKit.Interaction.QuestInventoryKind"), "EventItem"), 2000000u + (uint)i, 2000000u + (uint)i,
                Enum.Parse(Type("OmniKit.Interaction.QuestInventoryContainer"), "KeyItems"), (ushort)i,
                $"Synthetic quest item {i + 1}", 100u + (uint)i, i + 2, enabled,
                enabled ? "" : "The item is not usable on this target right now.", true, target!, 1L]), i);
    Set(inventory, "snapshot", entries);
    Set(interaction, "<Inventory>k__BackingField", inventory);
    var hud = Activator.CreateInstance(Type("OmniKit.Ui.QuestActionHud"), [config, interaction])!;
    var draw = hud.GetType().GetMethod("Draw", flags)!;
    var capture = interaction.GetType().GetMethod("CaptureUiInput", flags)!;
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO(); io.DisplaySize = viewport; io.DeltaTime = 1f / 60;
        io.IniFilename = null; io.LogFilename = null; io.FontGlobalScale = scale;
        io.Fonts.AddFontDefault(); io.Fonts.Build(); ImGui.StyleColorsDark();
        var renderer = new SoftwareRenderer();
        var pixels = new byte[32 * 32 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        { pixels[i] = (byte)(80 + i / 4 % 32 * 4); pixels[i + 1] = 165; pixels[i + 2] = 190; pixels[i + 3] = 255; }
        renderer.AddTexture(2, pixels, 32, 32);
        var windowName = next ? "##OmniKitNextAction" : "##OmniKitQuestItems";
        void Frame(Vector2? mouse = null, bool? down = null)
        {
            if (mouse.HasValue) io.AddMousePosEvent(mouse.Value.X, mouse.Value.Y);
            if (down.HasValue) io.AddMouseButtonEvent(0, down.Value);
            ImGui.NewFrame(); draw.Invoke(hud, [scale]);
            capture.Invoke(interaction, [hud.GetType().GetProperty("OwnsActionInput")!.GetValue(hud)]);
            ImGui.Render();
        }
        for (var frame = 0; frame < 4; frame++) Frame(new(-100, -100));
        void Save(string suffix)
        {
            renderer.Save(ImGui.GetDrawData(), Path.Combine(output, label + suffix + ".png")); ++renders;
        }
        Save("");
        foreach (var id in new[] { "##OmniKitNextAction", "##OmniKitQuestItems" })
        {
            var window = ImGuiP.FindWindowByName(id);
            if (window.Handle == null || !window.Active || window.Hidden) continue;
            Check(window.Pos.X >= 0 && window.Pos.Y >= 0 && window.Pos.X + window.Size.X <= viewport.X + .1f
                && window.Pos.Y + window.Size.Y <= viewport.Y + .1f, label + ": " + id + " leaves viewport bounds.");
            Check(window.ScrollMax.X == 0 && window.ScrollMax.Y == 0,
                label + ": " + id + " unexpectedly scrolls/clips its own controls.");
            if (!locked)
            {
                var heading = id.EndsWith("NextAction", StringComparison.Ordinal) ? "NEXT ACTION" : "QUEST ITEMS";
                var headingWidth = ImGui.CalcTextSize(heading).X / ImGui.GetFontSize() * 11 * scale;
                Check(7 * scale + headingWidth <= window.Size.X - 25 * scale,
                    label + ": " + heading + " heading lacks a gutter before its lock control.");
            }
        }
        if (next && items && !edge)
        {
            var a = ImGuiP.FindWindowByName("##OmniKitNextAction"); var b = ImGuiP.FindWindowByName("##OmniKitQuestItems");
            var overlap = a.Pos.X < b.Pos.X + b.Size.X && b.Pos.X < a.Pos.X + a.Size.X
                && a.Pos.Y < b.Pos.Y + b.Size.Y && b.Pos.Y < a.Pos.Y + a.Size.Y;
            Check(!overlap, label + ": default next-action and inventory windows overlap.");
        }
        if (!input) return;
        var panel = ImGuiP.FindWindowByName(windowName);
        var center = panel.Pos + new Vector2(15 * scale, panel.Size.Y / 2);
        Frame(center); Frame(center, true);
        var own = (bool)hud.GetType().GetProperty("OwnsActionInput")!.GetValue(hud)!;
        if (enabled) Check(own && !(bool)Get(interaction, "uiCapturesInput")!, label + ": own held action blocks itself.");
        Check(Get(interaction, "requestedAction") == null, label + ": action was requested on mouse-down.");
        for (var held = 0; held < 5; ++held) Frame(center, true);
        Check(Get(interaction, "requestedAction") == null, label + ": holding requested an action.");
        Frame(center, false);
        Check((Get(interaction, "requestedAction") != null) == enabled, label + ": release request did not match enabled state.");
        if (enabled)
        {
            Check((bool)hud.GetType().GetProperty("OwnsActionInput")!.GetValue(hud)!
                && !(bool)Get(interaction, "uiCapturesInput")!, label + ": released action self-blocked after tooltip draw.");
            Set(interaction, "requestedAction", null);
            Frame(center, false); Frame(center, false);
            Check(Get(interaction, "requestedAction") == null, label + ": one release repeated its request.");
        }
        Save("-tooltip");
        var windows = ImGui.GetCurrentContext().Windows;
        var tooltipCount = 0;
        for (var i = 0; i < windows.Size; ++i)
        {
            var tooltip = windows[i];
            if (!tooltip.Active || tooltip.Hidden || (tooltip.Flags & ImGuiWindowFlags.Tooltip) == 0) continue;
            tooltipCount++;
            Check(tooltip.Pos.X >= 0 && tooltip.Pos.Y >= 0 && tooltip.Pos.X + tooltip.Size.X <= viewport.X
                && tooltip.Pos.Y + tooltip.Size.Y <= viewport.Y, label + ": tooltip extends beyond the viewport.");
        }
        Check(tooltipCount > 0, label + ": hovered action must display its tooltip.");
        io.WantTextInput = true; capture.Invoke(interaction, [true]);
        Check((bool)Get(interaction, "uiCapturesInput")!, label + ": text input did not override own-action bypass.");
        io.WantTextInput = false;
        var lockAt = panel.Pos + new Vector2(panel.Size.X - 9 * scale, 8 * scale);
        Frame(lockAt); Frame(lockAt, true); Frame(lockAt, false); Frame(lockAt);
        var lockedField = next ? "NextActionLocked" : "QuestItemsBarLocked";
        Check(!(bool)Get(config, lockedField)!, label + ": locked top-right icon cannot independently unlock.");
        Check(Get(interaction, "requestedAction") == null, label + ": lock click requested an action.");
        Check(saves.Value == 1, label + ": unlock did not save exactly once.");
        Save("-unlocked");
        if ((bool)Get(config, lockedField)!) return;
        panel = ImGuiP.FindWindowByName(windowName);
        var start = panel.Pos + new Vector2(8 * scale, 8 * scale);
        Frame(start); Frame(start, true);
        Frame(start + new Vector2(40, -80), true); Frame(start + new Vector2(60, -100), true);
        Check(saves.Value == 1, label + ": drag persisted while still held.");
        Frame(start + new Vector2(60, -100), false); Frame();
        Check(saves.Value == 2, label + ": drag release did not save exactly once.");
        Save("-dragged");
        panel = ImGuiP.FindWindowByName(windowName);
        lockAt = panel.Pos + new Vector2(panel.Size.X - 13 * scale, 13 * scale);
        Frame(lockAt); Frame(lockAt, true); Frame(lockAt, false); Frame();
        Check((bool)Get(config, lockedField)!, label + ": unlocked top-right icon cannot relock.");
        Check(Get(interaction, "requestedAction") == null, label + ": relock requested an action.");
        if (items && count > 8)
        {
            panel = ImGuiP.FindWindowByName(windowName);
            var arrow = panel.Pos + new Vector2(panel.Size.X - 10 * scale, panel.Size.Y - 11 * scale);
            Frame(arrow); Frame(arrow, true); Frame(arrow, false); Frame();
            Check((int)Get(hud, "page")! == 1, label + ": next-page arrow did not change page.");
            Check(Get(interaction, "requestedAction") == null, label + ": paging requested an item action.");
            Save("-page2");
        }
    }
    finally { ImGui.DestroyContext(context); }
}

internal sealed class Counter { public int Value; public void Add() => ++Value; }
