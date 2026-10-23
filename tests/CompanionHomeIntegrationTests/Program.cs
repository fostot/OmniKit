using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;

var plugin = Path.GetFullPath(args.FirstOrDefault(x => !x.StartsWith("--", StringComparison.Ordinal)) ?? "src/OmniKit/bin/Release/OmniKit.dll");
var host = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (var directory in new[] { Path.GetDirectoryName(plugin)!, host })
    {
        var path = Path.Combine(directory, name.Name + ".dll");
        if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
    }
    return null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(plugin);
var fixture = new IntegrationFixture(assembly);
if (args.Contains("--exit-only"))
{
    NativeExitChecks.Run(fixture);
    Console.WriteLine($"Compiled home exit: {fixture.Checks} checks passed. No game services/native game functions called.");
    return;
}
if (!args.Contains("--draw-only")) fixture.RunNavigation();
if (!args.Contains("--navigation-only")) StyleDrawChecks.Run(fixture);
Console.WriteLine($"Compiled home integration: {fixture.Checks} checks passed. No game/native services called.");

internal sealed class IntegrationFixture(Assembly assembly)
{
    internal const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    internal const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    public int Checks { get; private set; }
    internal Type Type(string name) => assembly.GetType("OmniKit." + name, true)!;
    internal object New(string name, params object?[] args) => Activator.CreateInstance(Type(name), args)!;
    internal static object Get(object obj, string name) => obj.GetType().GetProperty(name, Instance)!.GetValue(obj)!;
    internal static object Field(object obj, string name) => obj.GetType().GetField(name, Instance)!.GetValue(obj)!;
    internal static void Set(object obj, string name, object? value) => (obj.GetType().GetField(name, Instance)
        ?? obj.GetType().GetField("<" + name + ">P", Instance))!.SetValue(obj, value);
    internal static object? Call(object obj, string name, params object?[] args) => obj.GetType().GetMethod(name, Instance)!.Invoke(obj, args);
    internal void Check(bool value, string reason) { Checks++; if (!value) throw new InvalidOperationException(reason); }
    internal object Companion(object config, object library, int seed = 1)
    {
        var companion = RuntimeHelpers.GetUninitializedObject(Type("Ui.RoamingCompanion"));
        Set(companion, "config", config);
        Set(companion, "assetDirectory", Path.GetFullPath("assets"));
        Set(companion, "homeRoutine", New("Ui.CompanionHomeRoutine", seed));
        Set(companion, "napPhysics", New("Ui.CompanionPhysics"));
        Set(companion, "napSurfaces", Activator.CreateInstance(typeof(List<>).MakeGenericType(Type("Ui.CompanionSurface")))!);
        Set(companion, "styles", library);
        Set(companion, "styleSelection", New("Ui.CompanionStyleSelection", library, (int?)seed));
        return companion;
    }

    internal void RunNavigation()
    {
        var library = New("Ui.CompanionHomeStyleLibrary", Path.GetFullPath("assets"));
        var results = new List<object>();
        var failures = new List<string>();
        foreach (var id in new[] { "Default", "CloudCushion", "LanternHut", "AllSaintsWake" })
        foreach (var edge in new[] { false, true })
        foreach (var mirrored in new[] { false, true })
        foreach (var inset in id == "AllSaintsWake" && !edge ? new[] { false, true } : new[] { false })
        {
            var label = $"{id}/{(edge ? "edge" : "normal")}/{(mirrored ? "left" : "right")}" + (inset ? "/seasonal-inset" : "");
            var config = New("Configuration");
            Set(config, "CompanionHomeStyle", id); Set(config, "CompanionHolidayHomeEnabled", false);
            Set(config, "CompanionHomeX", edge ? mirrored ? 1f : 0f : mirrored ? .835f : .165f);
            Set(config, "CompanionHomeY", edge ? 1f : .92f);
            var companion = Companion(config, library);
            Set(companion, "houseMirrored", mirrored);
            var size = new Vector2(96); var viewport = new Vector2(1920, 1080);
            var layoutMinimum = new Vector2(mirrored ? 1382.4f : 96, 594);
            var layoutMaximum = new Vector2(mirrored ? 1824 : 537.6f, 993.6f);
            if (inset)
            {
                var room = layoutMaximum - layoutMinimum;
                size *= Math.Clamp(Math.Min(room.X / (size.X * 2.2f), room.Y / (size.Y * 2.2f)), 0, 1);
                layoutMinimum += size * .6f; layoutMaximum -= size * .6f;
            }
            var layout = New("Ui.CompanionLayout", layoutMinimum, layoutMaximum, size);
            var bounds = Type("Ui.CompanionHomeGeometry").GetMethod("Bounds", Static)!.Invoke(null,
                [layout, Vector2.Zero, viewport, new Vector2((float)Field(config, "CompanionHomeX"), (float)Field(config, "CompanionHomeY"))])!;
            Call(companion, "UpdateHomeStyle", bounds, Vector2.Zero, viewport);
            var frame = Field(companion, "homeStyleFrame");
            if (edge)
            {
                var minimum = (Vector2)Get(frame, "Minimum"); var maximum = (Vector2)Get(frame, "Maximum");
                Check(Math.Abs(maximum.Y - viewport.Y) < .001f && (mirrored ? Math.Abs(maximum.X - viewport.X) < .001f : minimum.X == 0),
                    label + " uses the actual aspect-fitted house clamped flush to the viewport.");
            }
            var house = New("Ui.CompanionHomeRect", Get(frame, "Minimum"), Get(frame, "Maximum"));
            var surfaces = Array.CreateInstance(Type("Ui.CompanionSurface"), 0);
            var options = New("Ui.CompanionPhysicsOptions", true, false, true, true, true);
            Call(companion, "PrepareHomeNavigation", surfaces, layout, house, Vector2.Zero, viewport, options);
            var routine = Field(companion, "homeRoutine");
            var physics = Field(companion, "napPhysics");
            var delegateType = typeof(Func<,,,,>).MakeGenericType(typeof(Vector2), typeof(Vector2), typeof(float), typeof(bool), Type("Ui.CompanionHomeTravel"));
            var navigate = companion.GetType().GetMethod("NavigateHome", Instance)!.CreateDelegate(delegateType, companion);
            Call(routine, "Reset", 15f); Set(routine, "awakeRemaining", .01f);
            // Start on the actual saved roaming ground, on the outward side of the house.
            var current = new Vector2(mirrored ? layoutMinimum.X : layoutMaximum.X - size.X, layoutMaximum.Y - size.Y);
            var initial = current; var seen = new HashSet<string>(); var positions = new List<object>(); var active = false;
            for (var tick = 0; tick < 9000; tick++)
            {
                var prior = Get(routine, "State").ToString()!;
                Call(routine, "Update", 1f / 60, true, false, current, mirrored, layout, house, mirrored,
                    15f, 5f, Vector2.Zero, viewport, true, frame, navigate);
                var state = Get(routine, "State").ToString()!;
                current = (Vector2)Get(routine, "Position");
                if (seen.Add(state)) positions.Add(new { state, tick, x = current.X, y = current.Y });
                Check(float.IsFinite(current.X) && float.IsFinite(current.Y), label + " stays finite.");
                Check(current.X >= -.01f && current.Y >= -.01f && current.X + size.X <= viewport.X + .01f
                    && current.Y + size.Y <= viewport.Y + .01f, label + " stays in the viewport.");
                active |= (bool)Get(routine, "Active");
                if (active && state == "Roaming") break;
                if (prior == "Returning" && tick % 60 == 0)
                    positions.Add(new { state = "return-progress", tick, x = current.X, y = current.Y });
            }
            var slept = new[] { "Entering", "Settling", "Sleeping", "Waking", "Leaving", "Returning" }.All(seen.Contains);
            var returned = slept && Get(routine, "State").ToString() == "Roaming" && Vector2.Distance(current, initial) < .1f;
            if (!slept || !returned) failures.Add(label + $": slept={slept}, returned={returned}, final={current}, nav={Get(physics, "NavigationStatus")}");
            results.Add(new { label, slept, returned, positions });
        }
        var output = Path.GetFullPath(".local/visualharness/companion-home-integration");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "navigation.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        foreach (var failure in failures) Console.WriteLine(failure);
        Check(failures.Count == 0, "Compiled navigation has " + failures.Count + " failed home cycles (synthetic details above).");
        CheckBlockedRoute(library);
    }

    private void CheckBlockedRoute(object library)
    {
        var config = New("Configuration");
        Set(config, "CompanionHomeY", .2f); Set(config, "CompanionHolidayHomeEnabled", false);
        var companion = Companion(config, library); Set(companion, "houseMirrored", true);
        var size = new Vector2(96); var viewport = new Vector2(1920, 1080);
        var layout = New("Ui.CompanionLayout", new Vector2(1382.4f, 594), new Vector2(1824, 993.6f), size);
        var bounds = Type("Ui.CompanionHomeGeometry").GetMethod("Bounds", Static)!.Invoke(null,
            [layout, Vector2.Zero, viewport, new Vector2(.835f, .2f)])!;
        Call(companion, "UpdateHomeStyle", bounds, Vector2.Zero, viewport);
        var frame = Field(companion, "homeStyleFrame");
        var house = New("Ui.CompanionHomeRect", Get(frame, "Minimum"), Get(frame, "Maximum"));
        Call(companion, "PrepareHomeNavigation", Array.CreateInstance(Type("Ui.CompanionSurface"), 0), layout, house,
            Vector2.Zero, viewport, New("Ui.CompanionPhysicsOptions", true, false, true, true, true));
        var routine = Field(companion, "homeRoutine"); var physics = Field(companion, "napPhysics");
        var delegateType = typeof(Func<,,,,>).MakeGenericType(typeof(Vector2), typeof(Vector2), typeof(float), typeof(bool), Type("Ui.CompanionHomeTravel"));
        var navigator = companion.GetType().GetMethod("NavigateHome", Instance)!.CreateDelegate(delegateType, companion);
        Call(routine, "Reset", 15f); Set(routine, "awakeRemaining", .01f);
        var point = new Vector2(1382.4f, 897.6f); var active = false; var aborted = false;
        for (var i = 0; i < 9000; i++)
        {
            Call(routine, "Update", 1f / 60, true, false, point, true, layout, house, true, 15f, 5f,
                Vector2.Zero, viewport, true, frame, navigator);
            point = (Vector2)Get(routine, "Position");
            Check(!(bool)Get(routine, "UsesNapArt"), "An unreachable elevated porch never starts doorway or sleep art.");
            active |= (bool)Get(routine, "Active");
            if (active && !(bool)Get(routine, "Active")) { aborted = true; break; }
            if (!(bool)Get(routine, "Active") && Get(physics, "NavigationStatus").ToString() == "Blocked") { aborted = true; break; }
        }
        Check(aborted && Get(physics, "NavigationStatus").ToString() == "Blocked", "An unreachable elevated porch aborts without unsupported travel.");
        Check(point == (Vector2)Get(physics, "Position"), "Abort retains the actual physics point for the caller's handoff.");
        var bed = (Vector2)Call(frame, "BedPosition", size)!;
        Check(Vector2.Distance(point, bed) > size.Y, "A blocked route does not teleport into the bed.");
    }
}
