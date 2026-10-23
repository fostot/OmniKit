using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using OmniKit.Assistant;

if (args.Length > 0 && args[0] == "--write-probe-args")
{
    Console.WriteLine(JsonSerializer.Serialize(CliChatBackend.BuildChatArguments(AiConnectionKind.CodexCli,
        Path.GetFullPath(args[1]), "You are a synthetic transport-test companion. Reply only with the requested fixture text.", "omnikit-probe")));
    return;
}
if (args.Length > 0) { await Fixture(args); return; }

var root = new DirectoryInfo(AppContext.BaseDirectory);
while (!File.Exists(Path.Combine(root.FullName, "src/OmniKit/Assistant/CliChatBackend.cs"))) root = root.Parent ?? throw new Exception("Project root not found.");
var scratch = Path.Combine(root.FullName, ".local", "cli-chat-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
var self = Environment.ProcessPath!;
Check(self.EndsWith(".exe", StringComparison.OrdinalIgnoreCase), "Run tests through their native apphost.");
string Alias(string name)
{
    var path = Path.Combine(AppContext.BaseDirectory, name + ".exe");
    File.Copy(self, path, true);
    return path;
}
var codex = Alias("fixture-codex");
var claude = Alias("fixture-claude");
var unsupported = Alias("unsupported-codex");
var badAuth = Alias("bad-auth-claude");
var badHelp = Alias("bad-help-claude");
CliChatBackend Backend(string name, string executable, int timeoutMs = 5000) => new(new DirectoryInfo(Path.Combine(scratch, name)),
    _ => [executable], TimeSpan.FromMilliseconds(timeoutMs), TimeSpan.FromSeconds(5));

var discovered = 0;
using (var paused = new CliChatBackend(new DirectoryInfo(Path.Combine(scratch, "paused")),
    _ => { Interlocked.Increment(ref discovered); throw new Exception("Paused chat must not discover or launch a CLI."); },
    TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)))
{
    foreach (var kind in new[] { AiConnectionKind.CodexCli, AiConnectionKind.ClaudeCli })
        Check(await paused.AskAsync(kind, "Synthetic instructions", "Synthetic question", "", default)
            == CliChatBackend.SingleRequestLimitNotice, "Production CLI chat must fail closed.");
    Check(discovered == 0 && !Directory.Exists(Path.Combine(scratch, "paused")), "Paused chat performs no discovery, process launch or workspace creation.");
}

// This reflection-only fixture exercises suspended transport against our own fake
// executable. The public generation entry point above must remain unavailable.
static Task<string> AskSuspendedFixture(CliChatBackend backend, AiConnectionKind kind,
    string instructions, string question, string model, CancellationToken ct) =>
    (Task<string>)typeof(CliChatBackend).GetMethod("AskCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(backend, [kind, instructions, question, model, ct])!;

using (var backend = Backend("codex", codex))
{
    var status = await backend.ProbeAsync(AiConnectionKind.CodexCli, default);
    Check(status.Contains("is signed in") && status.Contains("global instructions"), "Codex discloses ambient instructions.");
    Check(!status.Contains("PRIVATE_FIXTURE_DETAIL"), "Authentication output must remain private.");
    const string question = "--model injected\n\"; Write-Output 'must stay text'; #\n$(not-a-command) `literal` Ω おはよう";
    Environment.SetEnvironmentVariable("OMNIKIT_UNSAFE_INHERIT_FIXTURE", "fixture-only");
    string answer;
    try { answer = await AskSuspendedFixture(backend, AiConnectionKind.CodexCli, "Trusted companion instructions.", question, "gpt-test", default); }
    finally { Environment.SetEnvironmentVariable("OMNIKIT_UNSAFE_INHERIT_FIXTURE", null); }
    Check(answer == question, "Standard input must preserve Unicode, newlines, and shell-like text literally.");
    var recordPath = Directory.GetFiles(Path.Combine(scratch, "codex"), "fixture.json", SearchOption.AllDirectories).Single();
    using var record = JsonDocument.Parse(File.ReadAllText(recordPath));
    var argv = record.RootElement.GetProperty("args").EnumerateArray().Select(x => x.GetString()!).ToArray();
    Check(!argv.Any(x => x.Contains(question, StringComparison.Ordinal)), "The question must never appear in process arguments.");
    Check(record.RootElement.GetProperty("stdin").GetString() == question, "Only explicitly supplied text enters stdin.");
    Check(!record.RootElement.GetProperty("inheritedFixtureEnvironment").GetBoolean(), "Unapproved process environment is not inherited.");
    Check(!argv.Any(x => x.Contains("dangerously")), "No bypass flags may be used.");
    Check(argv.Contains("--ignore-user-config") && argv.Contains("mcp_servers={}") && argv.Contains("apps._default.enabled=false"), "Codex must disable configured connectors.");
    Check(!backend.Status.Contains(question), "Status must not copy assistant conversation text.");
    Check((await AskSuspendedFixture(backend, AiConnectionKind.CodexCli, "Rules", "fixture:malformed", "", default)).Contains("valid final text"), "Malformed final events must be rejected.");
    Check((await AskSuspendedFixture(backend, AiConnectionKind.CodexCli, "Rules", "fixture:failed", "", default)).Contains("could not complete"), "Nonzero exit must return a fixed error.");
    Check(!backend.Status.Contains("PRIVATE_FIXTURE_DETAIL"), "Failed stderr must never be exposed.");
    Check((await AskSuspendedFixture(backend, AiConnectionKind.CodexCli, "Rules", "fixture:output-limit", "", default)).Contains("too much data"), "Combined stdout/stderr must be bounded and terminate the process.");
    Check((await AskSuspendedFixture(backend, AiConnectionKind.CodexCli, "Rules", "hello", "--unsafe", default)).Contains("valid CLI model"), "Option-shaped model names must fail validation.");
}

using (var backend = Backend("claude", claude))
{
    Check((await backend.ProbeAsync(AiConnectionKind.ClaudeCli, default)).Contains("is signed in"), "Claude auth JSON can establish login without exposing account fields.");
    Check(await AskSuspendedFixture(backend, AiConnectionKind.ClaudeCli, "Trusted persona", "hello Claude", "", default) == "hello Claude", "Claude returns only the result text.");
    var path = Directory.GetFiles(Path.Combine(scratch, "claude"), "fixture.json", SearchOption.AllDirectories).Single();
    using var record = JsonDocument.Parse(File.ReadAllText(path));
    var argv = record.RootElement.GetProperty("args").EnumerateArray().Select(x => x.GetString()!).ToArray();
    Check(argv[Array.IndexOf(argv, "--tools") + 1] == "" && argv[Array.IndexOf(argv, "--setting-sources") + 1] == "", "Empty arguments must survive native process transport.");
    Check(argv.Contains("--restricted") && argv.Contains("--safe-mode") && argv.Contains("--strict-mcp-config") && !argv.Contains("--bare"), "Claude keeps OAuth while isolating tools/customizations.");
    Check((await backend.SignInAsync(AiConnectionKind.ClaudeCli, default)).Contains("is signed in"), "Explicit sign-in uses the CLI login command and then verifies status.");
}
using (var backend = Backend("unsupported", unsupported))
    Check((await backend.ProbeAsync(AiConnectionKind.CodexCli, default)).Contains("has not passed"), "An unverified Codex version fails closed.");
using (var backend = Backend("bad-help", badHelp))
    Check((await backend.ProbeAsync(AiConnectionKind.ClaudeCli, default)).Contains("required restricted"), "Missing isolation options fail closed.");
using (var backend = Backend("bad-auth", badAuth))
{
    var response = await backend.ProbeAsync(AiConnectionKind.ClaudeCli, default);
    Check(response.Contains("sign-in button") && !response.Contains("PRIVATE_FIXTURE_DETAIL"), "Malformed auth status is sanitized.");
}
using (var backend = new CliChatBackend(new DirectoryInfo(Path.Combine(scratch, "late-path")),
    _ => Enumerable.Range(0, 20).Select(index => Path.Combine(scratch, "missing-" + index + ".exe")).Append(codex),
    TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)))
    Check((await backend.ProbeAsync(AiConnectionKind.CodexCli, default)).Contains("is signed in"),
        "Missing discovery candidates must not hide a later installed executable.");

async Task TreeTest(string name, bool cancel, bool unload, bool orphan)
{
    using var backend = Backend(name, codex, cancel || unload ? 5000 : 800);
    using var cts = new CancellationTokenSource();
    var task = AskSuspendedFixture(backend, AiConnectionKind.CodexCli, "Rules", orphan ? "fixture:orphan" : "fixture:hang", "", cts.Token);
    string? pidPath = null;
    var watch = Stopwatch.StartNew();
    while (watch.Elapsed < TimeSpan.FromSeconds(5))
    {
        var directory = Path.Combine(scratch, name);
        if (Directory.Exists(directory)) pidPath = Directory.GetFiles(directory, "child.pid", SearchOption.AllDirectories).FirstOrDefault();
        if (pidPath is not null && new FileInfo(pidPath).Length > 0) break;
        await Task.Delay(20);
    }
    Check(pidPath is not null, "Fixture child starts before shutdown test.");
    var pid = int.Parse(File.ReadAllText(pidPath!));
    if (cancel) cts.Cancel();
    if (unload) backend.Dispose();
    var result = await task.WaitAsync(TimeSpan.FromSeconds(8));
    Check(result.Contains("canceled") || result.Contains("too long"), "Cancellation/unload/timeout has a safe result.");
    try
    {
        using var child = Process.GetProcessById(pid);
        Check(child.WaitForExit(2000), "No descendant survives canceled, timed-out, or unloaded requests.");
    }
    catch (ArgumentException) { }
}
await TreeTest("cancel-tree", true, false, false);
await TreeTest("timeout-tree", false, false, false);
await TreeTest("unload-tree", false, true, false);
await TreeTest("orphan-tree", false, false, true);

Check(CliChatBackend.ReadAnswer(AiConnectionKind.CodexCli,
    "{\"type\":\"item.completed\",\"item\":{\"type\":\"reasoning\",\"text\":\"PRIVATE_FIXTURE_DETAIL\"}}\n" +
    "{\"type\":\"item.completed\",\"item\":{\"type\":\"agent_message\",\"phase\":\"commentary\",\"text\":\"intermediate\"}}\n" +
    "{\"type\":\"item.completed\",\"item\":{\"type\":\"agent_message\",\"text\":\"final only\"}}\n{\"type\":\"turn.completed\"}") == "final only", "Reasoning and commentary never become final answers.");
Console.WriteLine("PASS: production CLI inference paused with zero process launches; suspended synthetic transport stdin/argument isolation, auth/error sanitizing, required flags/version gates, JSON final-only parsing, concurrent output bound, cancellation/timeout/unload/orphan process-tree shutdown.");

static void Check(bool passed, string message) { if (!passed) throw new Exception(message); }

static async Task Fixture(string[] argv)
{
    Console.InputEncoding = new System.Text.UTF8Encoding(false);
    Console.OutputEncoding = new System.Text.UTF8Encoding(false);
    var name = Path.GetFileName(Environment.ProcessPath!);
    var claude = name.Contains("claude", StringComparison.Ordinal);
    if (argv[0] == "fixture-child") { await Task.Delay(Timeout.Infinite); return; }
    if (argv.Contains("--version"))
    {
        Console.WriteLine(claude ? "2.1.304 (Claude Code)" : name.Contains("unsupported") ? "codex-cli 0.999.0" : "codex-cli " + CliChatBackend.SupportedCodexVersion);
        return;
    }
    if (argv.Contains("--help"))
    {
        Console.WriteLine(name.Contains("bad-help") ? "--print" : "--config --ignore-user-config --ephemeral --skip-git-repo-check --sandbox --json --restricted --safe-mode --tools --strict-mcp-config --mcp-config --setting-sources --disable-slash-commands --no-session-persistence --system-prompt --output-format");
        return;
    }
    if (argv.Contains("status"))
    {
        if (name.Contains("bad-auth")) Console.WriteLine("malformed PRIVATE_FIXTURE_DETAIL");
        else if (claude) Console.WriteLine("{\"loggedIn\":true,\"accountDetail\":\"PRIVATE_FIXTURE_DETAIL\"}");
        else { Console.Error.WriteLine("Logged in using ChatGPT"); Console.Error.WriteLine("PRIVATE_FIXTURE_DETAIL"); }
        return;
    }
    if (argv.Contains("login")) { Check(!claude || argv.Contains("--claudeai"), "Claude sign-in selects the subscription flow."); return; }
    var input = await Console.In.ReadToEndAsync();
    File.WriteAllText("fixture.json", JsonSerializer.Serialize(new { args = argv, stdin = input,
        inheritedFixtureEnvironment = Environment.GetEnvironmentVariable("OMNIKIT_UNSAFE_INHERIT_FIXTURE") is not null }));
    if (input is "fixture:hang" or "fixture:orphan")
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("fixture-child");
        using var child = Process.Start(info)!;
        File.WriteAllText("child.pid", child.Id.ToString());
        if (input == "fixture:orphan") return;
        await Task.Delay(Timeout.Infinite);
    }
    if (input == "fixture:output-limit")
    {
        await Task.WhenAll(Task.Run(() => Console.Out.Write(new string('x', 200000))), Task.Run(() => Console.Error.Write(new string('y', 200000))));
        await Task.Delay(Timeout.Infinite);
    }
    if (input == "fixture:failed") { Console.Error.WriteLine("PRIVATE_FIXTURE_DETAIL"); Environment.ExitCode = 7; return; }
    if (input == "fixture:malformed") { Console.WriteLine("PRIVATE_FIXTURE_DETAIL invalid JSON"); return; }
    if (claude) Console.WriteLine(JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = input, accountDetail = "PRIVATE_FIXTURE_DETAIL" }));
    else
    {
        Console.WriteLine(JsonSerializer.Serialize(new { type = "item.completed", item = new { type = "agent_message", text = input } }));
        Console.WriteLine("{\"type\":\"turn.completed\"}");
    }
}
