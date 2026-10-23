using System.Collections;
using System.Reflection;
using System.Runtime.Loader;

var plugin = Path.GetFullPath(args.ElementAtOrDefault(0) ?? "src/OmniKit/bin/Release/OmniKit.dll");
var host = Environment.GetEnvironmentVariable("DALAMUD_LIB_PATH") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
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
var type = assembly.GetType("OmniKit.Integration.WikiBrowser", true)!;
var parse = type.GetMethod("TryWebUrl", BindingFlags.Static | BindingFlags.NonPublic)!;
var checks = 0;
void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); checks++; }
foreach (var text in new[] { "https://example.test/wiki?search=quest", "http://127.0.0.1:1234/fixture", "https://example.test/a%20b#chapter" })
{
    object?[] values = [text, null];
    Check((bool)parse.Invoke(null, values)!, "Normal web URLs remain available in game.");
    Check(values[1] is string result && new Uri(result).Scheme is "https" or "http", "Routing returns only normalized HTTP(S) destinations.");
}
foreach (var text in new[] { "", "   ", "javascript:alert(1)", "file:///fixture", "data:text/html,fixture", "mailto:fixture@example.test", "https://user:synthetic@example.test/", "https://example.test/\nunsafe", "not-a-url", new string('x', 4097) })
{
    object?[] values = [text, null];
    Check(!(bool)parse.Invoke(null, values)!, "Non-web, credential-bearing, oversized and control-character destinations are rejected.");
}
var configuration = Activator.CreateInstance(assembly.GetType("OmniKit.Configuration", true)!)!;
using var browser = (IDisposable)Activator.CreateInstance(type, configuration)!;
type.GetMethod("Open")!.Invoke(browser, [""]);
type.GetMethod("OpenUrl")!.Invoke(browser, ["javascript:fixture"]);
Check(type.GetField("helper", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(browser) == null,
    "Rejected requests never start a process or read a browser profile.");
var seen = new List<string>();
Action<string> open = url => seen.Add(url);
var catalogue = assembly.GetType("OmniKit.Search.ActionCatalog", true)!;
var webEntries = (IEnumerable)catalogue.GetMethod("GetWebEntries", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [open])!;
foreach (var entry in webEntries)
    ((Action)entry!.GetType().GetProperty("Activate")!.GetValue(entry)!)();
Check(seen.Count == 2 && seen.All(url => url.StartsWith("https://na.finalfantasyxiv.com/lodestone/community_finder/", StringComparison.Ordinal)),
    "Both community commands use the injected in-game browser route.");
Check(seen.Any(url => url.EndsWith("type=fc", StringComparison.Ordinal)) && seen.Any(url => url.EndsWith("type=ls", StringComparison.Ordinal)),
    "Free company and linkshell destinations stay distinct.");
Console.WriteLine($"PASS: {checks} web-routing checks; no browser, game, profile or network was opened.");
