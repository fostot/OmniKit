using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace OmniKit.Assistant;

/// <summary>Runs the user's native CLI without reading or copying its credentials.</summary>
public sealed class CliChatBackend : IDisposable
{
    // Recheck the synthetic tool-manifest probe before extending this allowlist.
    internal const string SupportedCodexVersion = "0.163.0-alpha.11.1";
    private const int MaximumOutputCharacters = 262144;
    private const int MaximumAnswerCharacters = 6000;
    public const string CodexContextNotice = "Codex CLI also adds its own global instructions. OmniKit disables action tools and connectors; CLI chat does not receive game state.";
    public const string ClaudeContextNotice = "Claude CLI uses its own login in restricted mode with tools and MCP connections disabled. Managed CLI policies may still apply.";
    public const string SingleRequestLimitNotice = "CLI chat paused: the single-request limit cannot be verified. Use ChatGPT or API.";

    private static readonly string[] CodexDisabledFeatures =
    [
        "shell_tool", "view_image", "multi_agent", "plugins", "hooks", "memories",
        "tool_suggest", "goals", "sleep_tool", "shell_snapshot",
    ];
    private static readonly string[] EnvironmentNames =
    [
        "SystemRoot", "WINDIR", "PATH", "PATHEXT", "USERPROFILE", "HOME", "LOCALAPPDATA",
        "APPDATA", "ProgramData", "ProgramFiles", "ProgramFiles(x86)", "TEMP", "TMP",
        "CODEX_HOME", "CLAUDE_CONFIG_DIR",
    ];

    private readonly string workspaceRoot;
    private readonly Func<AiConnectionKind, IEnumerable<string>> findExecutables;
    private readonly TimeSpan requestTimeout;
    private readonly TimeSpan signInTimeout;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object processLock = new();
    private readonly HashSet<Process> activeProcesses = [];
    private string status = "Choose a CLI and check its connection.";
    private int busy;
    private volatile bool disposed;

    public CliChatBackend(DirectoryInfo configDirectory)
        : this(configDirectory, FindExecutables, TimeSpan.FromSeconds(90), TimeSpan.FromMinutes(3)) { }

    internal CliChatBackend(DirectoryInfo configDirectory, Func<AiConnectionKind, IEnumerable<string>> findExecutables,
        TimeSpan requestTimeout, TimeSpan signInTimeout)
    {
        workspaceRoot = Path.Combine(configDirectory.FullName, "cli-chat");
        this.findExecutables = findExecutables;
        this.requestTimeout = requestTimeout;
        this.signInTimeout = signInTimeout;
    }

    public string Status => Volatile.Read(ref status);

    public Task<string> ProbeAsync(AiConnectionKind kind, CancellationToken ct) => ExecuteAsync(kind, async (exe, cwd, token) =>
    {
        var authenticated = await CheckAuthenticationAsync(kind, exe, cwd, token).ConfigureAwait(false);
        return authenticated ? ConnectedMessage(kind) : SignInMessage(kind);
    }, ct);

    public Task<string> SignInAsync(AiConnectionKind kind, CancellationToken ct) => ExecuteAsync(kind, async (exe, cwd, token) =>
    {
        SetStatus("Complete sign-in in the browser opened by the CLI. OmniKit does not receive the login token.");
        string[] args = kind == AiConnectionKind.CodexCli ? ["login"] : ["auth", "login", "--claudeai"];
        var result = await RunAsync(exe, args, cwd, null, signInTimeout, token).ConfigureAwait(false);
        if (result.ExitCode != 0) return "CLI sign-in did not complete. Try its sign-in button again when ready.";
        return await CheckAuthenticationAsync(kind, exe, cwd, token).ConfigureAwait(false)
            ? ConnectedMessage(kind) : SignInMessage(kind);
    }, ct);

    public Task<string> AskAsync(AiConnectionKind kind, string instructions, string question, string model, CancellationToken ct)
    {
        // One CLI process can perform multiple model requests, retry internally or
        // continue after a resource helper. The user's single-inference rule wins
        // over CLI chat availability until a supported request limit is verified.
        // Authentication and metadata probes remain available and never call this.
        return Task.FromResult(SetStatus(SingleRequestLimitNotice));
    }

    // Suspended transport retained for focused synthetic process-isolation tests.
    // No production entry point calls it. Re-enabling requires a new request-count proof.
    private Task<string> AskCoreAsync(AiConnectionKind kind, string instructions, string question, string model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > 4000)
            return Task.FromResult("Ask a question of 1–4,000 characters.");
        if (string.IsNullOrWhiteSpace(instructions) || instructions.Length > 16000)
            return Task.FromResult("The companion instructions are unavailable.");
        model = model?.Trim() ?? "";
        if (model.Length > 200 || model.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-' and not '_' and not ':' and not '/' and not '+')
            || (model.Length > 0 && !char.IsAsciiLetterOrDigit(model[0])))
            return Task.FromResult("Choose a valid CLI model name first.");
        return ExecuteAsync(kind, async (exe, cwd, token) =>
        {
            if (!await CheckAuthenticationAsync(kind, exe, cwd, token).ConfigureAwait(false)) return SetStatus(SignInMessage(kind));
            SetStatus("Waiting for the CLI reply. " + ContextNotice(kind));
            var result = await RunAsync(exe, BuildChatArguments(kind, cwd, instructions, model), cwd,
                question, requestTimeout, token).ConfigureAwait(false);
            if (result.ExitCode != 0) return SetStatus("The CLI could not complete this request. Check its login, selected model, and account usage.");
            var answer = ReadAnswer(kind, result.StandardOutput);
            SetStatus(ConnectedMessage(kind));
            return answer;
        }, ct, updateStatusWithResult: false);
    }

    private async Task<string> ExecuteAsync(AiConnectionKind kind,
        Func<string, string, CancellationToken, Task<string>> action, CancellationToken ct, bool updateStatusWithResult = true)
    {
        if (disposed) return "CLI chat stopped because OmniKit is unloading.";
        if (kind is not AiConnectionKind.CodexCli and not AiConnectionKind.ClaudeCli) return "Choose Codex CLI or Claude CLI.";
        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) return "A CLI request is already running.";
        string? cwd = null;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
            linked.Token.ThrowIfCancellationRequested();
            // A new empty working directory prevents loading a game/project checkout.
            // CLI-owned global instructions and managed policy are disclosed separately.
            cwd = Path.Combine(workspaceRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cwd);
            SetStatus("Checking the native CLI and its isolation options.");
            var exe = await FindSupportedExecutableAsync(kind, cwd, linked.Token).ConfigureAwait(false);
            var result = await action(exe, cwd, linked.Token).ConfigureAwait(false);
            if (updateStatusWithResult) SetStatus(result);
            return result;
        }
        catch (UnsupportedCliException) { return SetStatus(UnsupportedMessage(kind)); }
        catch (MissingCliException) { return SetStatus($"{DisplayName(kind)} was not found. Install its official native CLI, then check again."); }
        catch (OutputLimitException) { return SetStatus("The CLI returned too much data and was stopped."); }
        catch (OperationCanceledException)
        {
            return SetStatus(ct.IsCancellationRequested || lifetime.IsCancellationRequested
                ? "CLI request canceled." : "The CLI took too long and was stopped.");
        }
        catch (JsonException) { return SetStatus("The CLI did not return a valid final text reply."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return SetStatus("The CLI could not be started or safely read. Check its installation and try again.");
        }
        finally
        {
            // Remove only our empty per-operation directory, never recursively erase CLI output.
            if (cwd is not null)
                try { Directory.Delete(cwd, recursive: false); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            Interlocked.Exchange(ref busy, 0);
        }
    }

    private async Task<string> FindSupportedExecutableAsync(AiConnectionKind kind, string cwd, CancellationToken token)
    {
        var found = false;
        foreach (var exe in findExecutables(kind).Distinct(StringComparer.OrdinalIgnoreCase)
                     .Where(path => Path.IsPathFullyQualified(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path)).Take(16))
        {
            found = true;
            var version = await RunAsync(exe, ["--version"], cwd, null, TimeSpan.FromSeconds(10), token).ConfigureAwait(false);
            if (version.ExitCode != 0) continue;
            if (kind == AiConnectionKind.CodexCli)
            {
                if (version.StandardOutput.Trim() != "codex-cli " + SupportedCodexVersion) continue;
                var help = await RunAsync(exe, ["exec", "--help"], cwd, null, TimeSpan.FromSeconds(10), token).ConfigureAwait(false);
                if (help.ExitCode != 0 || !ContainsAll(help.StandardOutput, "--ignore-user-config", "--ephemeral", "--skip-git-repo-check", "--sandbox", "--json", "--config")) continue;
                return exe;
            }
            var number = version.StandardOutput.Split(' ', '\r', '\n')[0];
            if (!Version.TryParse(number, out var parsed) || parsed < new Version(2, 1, 304)) continue;
            var claudeHelp = await RunAsync(exe, ["--help"], cwd, null, TimeSpan.FromSeconds(10), token).ConfigureAwait(false);
            if (claudeHelp.ExitCode == 0 && ContainsAll(claudeHelp.StandardOutput, "--restricted", "--safe-mode", "--tools", "--strict-mcp-config",
                    "--mcp-config", "--setting-sources", "--disable-slash-commands", "--no-session-persistence", "--system-prompt", "--output-format")) return exe;
        }
        if (!found) throw new MissingCliException();
        throw new UnsupportedCliException();
    }

    private async Task<bool> CheckAuthenticationAsync(AiConnectionKind kind, string exe, string cwd, CancellationToken token)
    {
        string[] args = kind == AiConnectionKind.CodexCli ? ["login", "status"] : ["auth", "status", "--json"];
        var result = await RunAsync(exe, args, cwd, null, TimeSpan.FromSeconds(10), token).ConfigureAwait(false);
        if (result.ExitCode != 0) return false;
        if (kind == AiConnectionKind.CodexCli)
            return (result.StandardOutput + "\n" + result.StandardError).Split('\n')
                .Any(line => line.Trim().Equals("Logged in using ChatGPT", StringComparison.OrdinalIgnoreCase));
        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput, new JsonDocumentOptions { MaxDepth = 16 });
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("loggedIn", out var loggedIn) && loggedIn.ValueKind == JsonValueKind.True;
        }
        catch (JsonException) { return false; }
    }

    internal static List<string> BuildChatArguments(AiConnectionKind kind, string cwd, string instructions, string model)
    {
        List<string> args;
        if (kind == AiConnectionKind.CodexCli)
        {
            args = ["exec", "--ignore-user-config", "--ephemeral", "--skip-git-repo-check", "--sandbox", "read-only", "--json", "--color", "never", "--cd", cwd];
            foreach (var feature in CodexDisabledFeatures) AddConfig(args, "features." + feature, "false");
            AddConfig(args, "tools.view_image", "false");
            AddConfig(args, "project_doc_max_bytes", "0");
            AddConfig(args, "web_search", "\"disabled\"");
            AddConfig(args, "mcp_servers", "{}");
            AddConfig(args, "apps._default.enabled", "false");
            AddConfig(args, "developer_instructions", JsonSerializer.Serialize(instructions));
            if (model.Length > 0) args.AddRange(["--model", model]);
            args.Add("-"); // User text is supplied only on standard input.
        }
        else
        {
            args = ["--print", "--restricted", "--safe-mode", "--tools", "", "--strict-mcp-config", "--mcp-config", "{\"mcpServers\":{}}",
                "--setting-sources", "", "--disable-slash-commands", "--no-session-persistence", "--input-format", "text", "--output-format", "json", "--system-prompt", instructions];
            if (model.Length > 0) args.AddRange(["--model", model]);
        }
        return args;
    }

    private static void AddConfig(List<string> args, string key, string value) => args.AddRange(["-c", key + "=" + value]);
    private static bool ContainsAll(string text, params string[] names) => names.All(name => text.Contains(name, StringComparison.Ordinal));

    private async Task<ProcessResult> RunAsync(string exe, IEnumerable<string> arguments, string cwd, string? input, TimeSpan timeout, CancellationToken token)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        cancellation.CancelAfter(timeout);
        var info = new ProcessStartInfo(exe)
        {
            WorkingDirectory = cwd, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        info.Environment.Clear();
        foreach (var name in EnvironmentNames)
            if (Environment.GetEnvironmentVariable(name) is { } value) info.Environment[name] = value;
        info.Environment["NO_COLOR"] = "1";
        using var process = new Process { StartInfo = info };
        cancellation.Token.ThrowIfCancellationRequested();
        if (!process.Start()) throw new InvalidOperationException();
        lock (processLock) activeProcesses.Add(process);
        using var stop = cancellation.Token.Register(() => Kill(process));
        var outputCount = 0;
        var overLimit = 0;
        async Task<string> ReadAsync(StreamReader reader)
        {
            var text = new StringBuilder();
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellation.Token).ConfigureAwait(false)) != 0)
            {
                if (Interlocked.Add(ref outputCount, count) > MaximumOutputCharacters)
                {
                    Interlocked.Exchange(ref overLimit, 1);
                    cancellation.Cancel();
                    throw new OutputLimitException();
                }
                text.Append(buffer, 0, count);
            }
            return text.ToString();
        }
        try
        {
            // Closing this job also kills descendants whose parent exited before cancellation.
            using var job = ProcessJob.Attach(process);
            var stdout = ReadAsync(process.StandardOutput);
            var stderr = ReadAsync(process.StandardError);
            try
            {
                if (input is not null) await process.StandardInput.WriteAsync(input.AsMemory(), cancellation.Token).ConfigureAwait(false);
                process.StandardInput.Close();
                await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(cancellation.Token)).ConfigureAwait(false);
            }
            catch
            {
                cancellation.Cancel();
                Kill(process);
                // Observe both read tasks; their reads use the canceled token.
                try { await Task.WhenAll(stdout, stderr).ConfigureAwait(false); } catch { }
                if (Volatile.Read(ref overLimit) != 0) throw new OutputLimitException();
                if (cancellation.IsCancellationRequested) throw new OperationCanceledException(cancellation.Token);
                throw;
            }
            return new ProcessResult(process.ExitCode, stdout.Result, stderr.Result);
        }
        finally
        {
            Kill(process);
            lock (processLock) activeProcesses.Remove(process);
        }
    }

    internal static string ReadAnswer(AiConnectionKind kind, string output)
    {
        string? answer = null;
        if (kind == AiConnectionKind.ClaudeCli)
        {
            using var document = JsonDocument.Parse(output, new JsonDocumentOptions { MaxDepth = 32 });
            var root = document.RootElement;
            if (StringProperty(root, "type") != "result" || StringProperty(root, "subtype") != "success"
                || !root.TryGetProperty("is_error", out var error) || error.ValueKind != JsonValueKind.False) throw new JsonException();
            answer = StringProperty(root, "result");
        }
        else
        {
            var completed = false;
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                using var document = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 32 });
                var root = document.RootElement;
                var type = StringProperty(root, "type");
                if (type is "error" or "turn.failed") throw new JsonException();
                if (type == "turn.completed") completed = true;
                if (type == "item.completed" && root.TryGetProperty("item", out var item)
                    && StringProperty(item, "type") == "agent_message" && StringProperty(item, "phase") != "commentary")
                    answer = StringProperty(item, "text"); // Only the last completed assistant message is the answer.
            }
            if (!completed) throw new JsonException();
        }
        if (string.IsNullOrWhiteSpace(answer)) throw new JsonException();
        answer = answer.Trim();
        return answer.Length <= MaximumAnswerCharacters ? answer : answer[..MaximumAnswerCharacters] + "\n[Answer shortened]";
    }

    private static string? StringProperty(JsonElement root, string name) => root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private string SetStatus(string message) { Volatile.Write(ref status, message); return message; }
    private static string DisplayName(AiConnectionKind kind) => kind == AiConnectionKind.CodexCli ? "Codex CLI" : "Claude CLI";
    private static string ContextNotice(AiConnectionKind kind) => kind == AiConnectionKind.CodexCli ? CodexContextNotice : ClaudeContextNotice;
    private static string ConnectedMessage(AiConnectionKind kind) => DisplayName(kind) + " is signed in. " + ContextNotice(kind);
    private static string SignInMessage(AiConnectionKind kind) => "Use the sign-in button for " + DisplayName(kind) + " first. " + ContextNotice(kind);
    private static string UnsupportedMessage(AiConnectionKind kind) => kind == AiConnectionKind.CodexCli
        ? "This Codex CLI version has not passed OmniKit's tool-isolation check. Supported version: " + SupportedCodexVersion + ". Update OmniKit support before using another version."
        : "This Claude CLI does not support OmniKit's required restricted chat options. Use Claude CLI 2.1.304 or newer with all required isolation flags.";

    private static IEnumerable<string> FindExecutables(AiConnectionKind kind)
    {
        var candidates = new List<string>();
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (kind == AiConnectionKind.CodexCli)
        {
            var bundled = Path.Combine(local, "OpenAI", "Codex", "bin");
            if (Directory.Exists(bundled))
                candidates.AddRange(Directory.EnumerateDirectories(bundled).Select(dir => Path.Combine(dir, "codex.exe"))
                    .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc));
        }
        else candidates.Add(Path.Combine(profile, ".local", "bin", "claude.exe"));
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim().Trim('"')).Where(Path.IsPathFullyQualified).Append(Path.Combine(roaming, "npm"));
        foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(directory, kind == AiConnectionKind.CodexCli ? "codex.exe" : "claude.exe"));
            if (kind != AiConnectionKind.CodexCli) continue;
            var package = Path.Combine(directory, "node_modules", "@openai");
            const string vendor = "vendor/x86_64-pc-windows-msvc/codex/codex.exe";
            candidates.Add(Path.Combine(package, "codex", vendor));
            candidates.Add(Path.Combine(package, "codex-win32-x64", vendor));
            candidates.Add(Path.Combine(package, "codex", "node_modules", "@openai", "codex-win32-x64", vendor));
        }
        return candidates;
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        lock (processLock) foreach (var process in activeProcesses) Kill(process);
        // In-flight continuations still use this token source; do not dispose it underneath them.
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
    private sealed class UnsupportedCliException : Exception;
    private sealed class MissingCliException : Exception;
    private sealed class OutputLimitException : Exception;

    private sealed class ProcessJob : IDisposable
    {
        private readonly SafeFileHandle handle;
        private ProcessJob(SafeFileHandle handle) => this.handle = handle;

        public static ProcessJob Attach(Process process)
        {
            if (!OperatingSystem.IsWindows()) throw new NotSupportedException();
            var job = CreateJobObjectW(IntPtr.Zero, null);
            if (job.IsInvalid) { job.Dispose(); throw new Win32Exception(); }
            var limits = new ExtendedLimits { Basic = new BasicLimits { Flags = 0x2000 } }; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>())
                || !AssignProcessToJobObject(job, process.Handle))
            {
                // Tiny version/help processes can exit before assignment; no children are retained.
                var exited = process.HasExited;
                job.Dispose();
                if (!exited) throw new Win32Exception();
            }
            return new ProcessJob(job);
        }

        public void Dispose() => handle.Dispose();
        [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
        {
            public long ProcessTime, JobTime;
            public uint Flags;
            public nuint MinimumWorkingSet, MaximumWorkingSet;
            public uint ActiveProcessLimit;
            public nuint Affinity;
            public uint Priority, Scheduling;
        }
        [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
        {
            public BasicLimits Basic;
            public IoCounters Io;
            public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObjectW(IntPtr attributes, string? name);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref ExtendedLimits info, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    }
}
