using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OmniKit.Integration;

/// <summary>One owned browser helper, embedded as a child of the game. Never opens desktop web links.</summary>
internal sealed class WikiBrowser : IDisposable
{
    private const string SearchUrl = "https://ffxiv.consolegameswiki.com/mediawiki/index.php?search=";
    private const string ShutdownBlockedStatus = "Browser shutdown could not be confirmed. Restart OmniKit before reopening.";
    private readonly object lifecycle = new();
    private readonly Queue<(string Command, long Generation)> commands = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Func<HelperProcess> startProcess;
    private readonly Func<nint, int, bool> validWindow;
    private readonly TimeSpan startTimeout, pipeTimeout, shutdownTimeout;
    private Session? helper;
    private Task<HelperProcess>? starting;
    private Task cleanup = Task.CompletedTask;
    private CancellationTokenSource? opening;
    private long generation;
    private int pendingCount;
    private bool workerRunning, disposed, shutdownUnconfirmed;
    private string status = "In-game browser";
    private bool hasError;
    private long statusRevision;
    public WikiBrowser(Configuration config) : this(StartProcess, ValidateWindow) { }
    internal WikiBrowser(Func<HelperProcess> startProcess, Func<nint, int, bool> validWindow,
        TimeSpan? startTimeout = null, TimeSpan? pipeTimeout = null, TimeSpan? shutdownTimeout = null)
    {
        this.startProcess = startProcess; this.validWindow = validWindow;
        this.startTimeout = startTimeout ?? TimeSpan.FromSeconds(10);
        this.pipeTimeout = pipeTimeout ?? TimeSpan.FromSeconds(5);
        this.shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(3);
    }
    public string Status => Volatile.Read(ref status);
    public bool HasError => Volatile.Read(ref hasError);
    public long StatusRevision => Interlocked.Read(ref statusRevision);
    public bool IsAvailable => File.Exists(HelperPath);
    // Hook-thread reads use tracked readiness only: no filesystem/process queries or UI calls.
    public bool IsReady { get { lock (lifecycle) return !disposed && helper is { VerifiedReady: true }; } }
    internal int PendingCount { get { lock (lifecycle) return pendingCount; } }
    private static string HelperPath => Path.Combine(Services.PluginInterface?.AssemblyLocation.DirectoryName ?? "", "browser", "OmniKit.Browser.exe");

    public bool CapturesInput
    {
        get
        {
            nint window;
            // A failed navigation can still leave an editable browser window focused.
            lock (lifecycle) window = !disposed && helper is { } current ? current.Window : 0;
            if (window == 0) return false;
            var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
            return GetGUIThreadInfo(0, ref info) && info.Focus != 0 && (info.Focus == window || IsChild(window, info.Focus));
        }
    }

    public void Open(string query)
    {
        var search = query?.Trim() ?? "";
        if (search.Length is < 1 or > 500 || search.Any(char.IsControl))
        { SetStatus("Enter a wiki search (1–500 characters).", true); return; }
        OpenUrl(SearchUrl + Uri.EscapeDataString(search));
    }
    public void OpenUrl(string url) => TryOpenUrl(url);
    public bool TryOpenUrl(string url, bool requireReady = false)
    {
        if (!TryWebUrl(url, out var destination))
        {
            if (!requireReady) SetStatus("Use an HTTP or HTTPS website address.", true);
            return false;
        }
        return TryQueue(JsonSerializer.Serialize(new { kind = "navigate", url = destination }), requireReady);
    }
    public void Show() => TryQueue("{\"kind\":\"show\"}");
    public void ToggleControls() => TryQueue("{\"kind\":\"toggle-controls\"}");
    public void Hide()
    {
        lock (lifecycle)
        {
            if (disposed) return;
            generation++;
            pendingCount -= commands.Count; commands.Clear();
            opening?.Cancel();
            var session = helper; helper = null;
            if (session != null) cleanup = StopAsync(session, true);
            SetStatus(shutdownUnconfirmed ? ShutdownBlockedStatus : "Browser closed.", shutdownUnconfirmed);
        }
    }

    internal static bool TryWebUrl(string? text, out string destination)
    {
        destination = "";
        if (string.IsNullOrWhiteSpace(text) || text.Length > 4096 || text.Any(char.IsControl)
            || !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http") || uri.UserInfo.Length > 0 || uri.Host.Length == 0) return false;
        destination = uri.AbsoluteUri; return true;
    }

    private bool TryQueue(string command, bool requireReady = false)
    {
        lock (lifecycle)
        {
            if (disposed || (requireReady && helper is not { VerifiedReady: true })) return false;
            if (shutdownUnconfirmed)
            {
                if (!requireReady) SetStatus(ShutdownBlockedStatus, true);
                return false;
            }
            if (command.Length > 8192 || pendingCount >= 8)
            {
                if (!requireReady) SetStatus("The browser is busy. Try again after the current request finishes.", true);
                return false;
            }
            commands.Enqueue((command, generation)); pendingCount++;
            SetStatus("Opening in-game browser…");
            if (!workerRunning) { workerRunning = true; _ = Task.Run(DrainAsync); }
            return true;
        }
    }
    private async Task DrainAsync()
    {
        while (true)
        {
            string command;
            long requestedGeneration;
            lock (lifecycle)
            {
                if (disposed || commands.Count == 0) { workerRunning = false; return; }
                (command, requestedGeneration) = commands.Dequeue();
            }
            Session? session = null;
            try
            {
                session = await GetSessionAsync(requestedGeneration).ConfigureAwait(false);
                if (session != null)
                {
                    using var write = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, session.Stop.Token);
                    write.CancelAfter(pipeTimeout);
                    Task sending;
                    lock (session)
                    {
                        write.Token.ThrowIfCancellationRequested();
                        if (session.Stopping != null) throw new OperationCanceledException();
                        sending = session.Writing = session.Process.SendAsync(command, write.Token);
                    }
                    try { await sending.WaitAsync(write.Token).ConfigureAwait(false); }
                    finally { if (!sending.IsCompleted) _ = ObserveAsync(sending); }
                }
            }
            catch (OperationCanceledException)
            {
                lock (lifecycle) if (!disposed && generation == requestedGeneration)
                    SetStatus("Browser request timed out. Open the browser again to retry.", true);
                if (session != null) await RetireAsync(session).ConfigureAwait(false);
            }
            catch (BrowserStartException error) { lock (lifecycle) if (!disposed && generation == requestedGeneration) SetStatus(error.SafeStatus, true); }
            catch (Exception)
            {
                lock (lifecycle) if (!disposed && generation == requestedGeneration)
                    SetStatus("Browser could not start. Check the WebView2 / .NET Desktop runtimes and update OmniKit.", true);
                if (session != null) await RetireAsync(session).ConfigureAwait(false);
            }
            finally { lock (lifecycle) pendingCount--; }
        }
    }
    private async Task<Session?> GetSessionAsync(long requestedGeneration)
    {
        Task previousCleanup;
        lock (lifecycle) previousCleanup = cleanup;
        // A newly requested view reuses the same profile only after the prior
        // owned helper has finished its bounded shutdown and released its lock.
        await previousCleanup.WaitAsync(lifetime.Token).ConfigureAwait(false);
        Session? old;
        lock (lifecycle)
        {
            if (disposed || generation != requestedGeneration) return null;
            if (shutdownUnconfirmed) throw new BrowserStartException(ShutdownBlockedStatus);
            old = helper;
            if (old != null && !old.Process.HasExited && old.Window != 0) return old;
            helper = null;
        }
        if (old != null) await RetireAsync(old).ConfigureAwait(false);
        Task<HelperProcess> launch;
        lock (lifecycle)
        {
            if (disposed || generation != requestedGeneration) return null;
            if (shutdownUnconfirmed) throw new BrowserStartException(ShutdownBlockedStatus);
            // Native Process.Start cannot be cancelled. Keep one owner until it returns;
            // never accumulate additional starts or publish an abandoned result.
            if (starting != null) throw new BrowserStartException("Browser startup is still finishing. Try again shortly.");
            launch = starting = Task.Run(startProcess);
        }
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        lock (lifecycle)
        {
            if (disposed || generation != requestedGeneration) startup.Cancel();
            else opening = startup;
        }
        startup.CancelAfter(startTimeout);
        HelperProcess process;
        try { process = await launch.WaitAsync(startup.Token).ConfigureAwait(false); }
        catch { _ = CleanLateStartAsync(launch); throw; }
        finally { lock (lifecycle) if (ReferenceEquals(opening, startup)) opening = null; }
        var session = new Session(process, lifetime.Token);
        bool publish;
        lock (lifecycle)
        {
            if (ReferenceEquals(starting, launch)) starting = null;
            publish = !disposed && generation == requestedGeneration && !startup.IsCancellationRequested;
            if (publish) helper = session;
        }
        if (!publish) { await StopAsync(session, false).ConfigureAwait(false); return null; }
        _ = ReadEventsAsync(session); _ = DrainErrorsAsync(session); _ = WatchExitAsync(session);
        try
        {
            if (!await session.Ready.Task.WaitAsync(startup.Token).ConfigureAwait(false))
                throw new BrowserStartException("Browser did not become ready. Open it again to retry.");
            return session;
        }
        catch { await RetireAsync(session).ConfigureAwait(false); throw; }
    }
    private async Task CleanLateStartAsync(Task<HelperProcess> launch)
    {
        try
        {
            var process = await launch.ConfigureAwait(false);
            await StopAsync(new Session(process, lifetime.Token), false).ConfigureAwait(false);
        }
        catch (Exception) { }
        finally { lock (lifecycle) if (ReferenceEquals(starting, launch)) starting = null; }
    }
    private static HelperProcess StartProcess()
    {
        if (!File.Exists(HelperPath)) throw new BrowserStartException("Browser files missing. Update OmniKit.");
        using var game = Process.GetCurrentProcess();
        var parent = game.MainWindowHandle;
        if (parent == 0) throw new BrowserStartException("The game window is not available yet.");
        var info = FileVersionInfo.GetVersionInfo(HelperPath);
        if (info.ProductVersion != BuildVersion.Display || info.CompanyName != "Zero")
            throw new BrowserStartException("Browser version mismatch. Update OmniKit and reload it.");
        var start = new ProcessStartInfo(HelperPath)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(HelperPath)!,
        };
        start.ArgumentList.Add("--parent-window"); start.ArgumentList.Add(parent.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--parent-pid"); start.ArgumentList.Add(game.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--profile"); start.ArgumentList.Add(Path.Combine(Services.PluginInterface.ConfigDirectory.FullName, "browser-profile"));
        // Do not pass API/CLI credentials or other arbitrary environment secrets to the helper.
        start.Environment.Clear();
        foreach (var name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "LOCALAPPDATA", "APPDATA", "USERPROFILE", "ProgramFiles", "ProgramFiles(x86)", "DOTNET_ROOT" })
            if (Environment.GetEnvironmentVariable(name) is { } value) start.Environment[name] = value;
        return new NativeHelper(Process.Start(start) ?? throw new InvalidOperationException());
    }

    private async Task ReadEventsAsync(Session session)
    {
        try
        {
            while (await session.Process.ReadEventAsync(session.Stop.Token).ConfigureAwait(false) is { } line)
            {
                if (line.Length > 1024) continue;
                using var message = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 4 });
                if (!message.RootElement.TryGetProperty("kind", out var node)) continue;
                lock (lifecycle)
                {
                    if (disposed || !ReferenceEquals(helper, session)) return;
                    switch (node.GetString())
                    {
                        case "ready":
                            if (message.RootElement.TryGetProperty("window", out var handle) && handle.TryGetInt64(out var raw)
                                && raw != 0 && !session.Process.HasExited && validWindow((nint)raw, session.Process.Id))
                            {
                                session.Window = (nint)raw; session.VerifiedReady = true;
                                session.Ready.TrySetResult(true); SetStatus("In-game browser ready.");
                            }
                            else { session.Ready.TrySetResult(false); SetStatus("Browser window could not be verified.", true); }
                            break;
                        case "loading": SetStatus("Loading page…"); break;
                        case "navigated":
                        case "shown":
                            session.VerifiedReady = session.Window != 0 && !session.Process.HasExited && validWindow(session.Window, session.Process.Id);
                            SetStatus(node.GetString() == "shown" ? "In-game browser open." : "Page loaded in game."); break;
                        case "hidden": SetStatus("Browser hidden."); break;
                        case "error":
                            session.VerifiedReady = false; session.Ready.TrySetResult(false);
                            SetStatus("Browser request failed. Check its controls and the WebView2 / .NET Desktop runtimes.", true); break;
                        case "closed":
                            session.Window = 0; session.VerifiedReady = false; session.Ready.TrySetResult(false);
                            generation++; pendingCount -= commands.Count; commands.Clear(); helper = null;
                            if (!HasError) SetStatus("Browser closed.");
                            cleanup = StopAsync(session, false); break;
                        case "closing":
                            generation++; pendingCount -= commands.Count; commands.Clear(); helper = null;
                            SetStatus("Browser closed.");
                            cleanup = StopAsync(session, true); break;
                    }
                }
            }
        }
        catch (Exception) { }
        finally { MarkEnded(session); }
    }
    private async Task WatchExitAsync(Session session)
    {
        try { await session.Process.WaitForExitAsync(session.Stop.Token).ConfigureAwait(false); }
        catch (Exception) { }
        finally { MarkEnded(session); }
    }
    private void MarkEnded(Session session)
    {
        lock (lifecycle)
        {
            session.Window = 0; session.VerifiedReady = false; session.Ready.TrySetResult(false);
            if (!disposed && ReferenceEquals(helper, session) && !HasError) SetStatus("Browser closed. Open it again to retry.");
        }
    }
    private static async Task DrainErrorsAsync(Session session)
    {
        try { while (await session.Process.ReadErrorAsync(session.Stop.Token).ConfigureAwait(false) != null) { } }
        catch (Exception) { }
    }
    private Task RetireAsync(Session session)
    {
        lock (lifecycle)
        {
            if (ReferenceEquals(helper, session)) helper = null;
            return cleanup = StopAsync(session, true);
        }
    }
    private Task StopAsync(Session session, bool graceful)
    {
        lock (session)
        {
            // Never overlap a shutdown write with an outstanding pipe operation. A cancelled
            // underlying write may still be running; terminate that owned helper instead.
            var requestShutdown = graceful && session.Writing is not { IsCompleted: false };
            return session.Stopping ??= Task.Run(async () =>
            {
                session.Stop.Cancel();
                var exitConfirmed = false;
                try
                {
                    using var timeout = new CancellationTokenSource(shutdownTimeout);
                    if (requestShutdown && !session.Process.HasExited)
                    {
                        Task? sending = null;
                        try
                        {
                            sending = session.Process.SendAsync("{\"kind\":\"shutdown\"}", timeout.Token);
                            await sending.WaitAsync(timeout.Token).ConfigureAwait(false);
                        }
                        catch (Exception) { }
                        finally { if (sending is { IsCompleted: false }) _ = ObserveAsync(sending); }
                        try { await session.Process.WaitForExitAsync(timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false); }
                        catch (Exception) { }
                    }
                    if (!session.Process.HasExited)
                    {
                        session.Process.Kill();
                        // Kill requests termination; it does not establish that the
                        // process has exited and released the owned profile yet.
                        using var forcedExit = new CancellationTokenSource(shutdownTimeout);
                        await session.Process.WaitForExitAsync(forcedExit.Token).WaitAsync(forcedExit.Token).ConfigureAwait(false);
                    }
                    exitConfirmed = session.Process.HasExited && session.Process.ExitCode == 0;
                }
                catch (Exception) { }
                finally
                {
                    // Exit code 73 means the helper could not confirm WebView2
                    // teardown. Any nonzero/unknown exit is equally conservative:
                    // the facade cannot establish that all descendants released it.
                    if (!exitConfirmed)
                    {
                        try { exitConfirmed = session.Process.HasExited && session.Process.ExitCode == 0; }
                        catch (Exception) { }
                    }
                    if (!exitConfirmed)
                    {
                        lock (lifecycle)
                        {
                            shutdownUnconfirmed = true;
                            pendingCount -= commands.Count; commands.Clear();
                            if (!disposed) SetStatus(ShutdownBlockedStatus, true);
                        }
                    }
                    try { session.Process.Dispose(); } catch (Exception) { }
                    session.Stop.Dispose();
                }
            });
        }
    }
    private static async Task ObserveAsync(Task task)
    { try { await task.ConfigureAwait(false); } catch (Exception) { } }
    private void SetStatus(string text, bool error = false)
    { Volatile.Write(ref hasError, error); Volatile.Write(ref status, text); Interlocked.Increment(ref statusRevision); }

    public void Dispose()
    {
        Session? process;
        lock (lifecycle)
        {
            if (disposed) return;
            disposed = true; pendingCount -= commands.Count; commands.Clear();
            process = helper; helper = null;
        }
        lifetime.Cancel();
        if (process != null) _ = StopAsync(process, true);
        // In-flight startup stays owned by GetSessionAsync/CleanLateStartAsync until termination.
    }
    private sealed class Session(HelperProcess process, CancellationToken lifetime)
    {
        public HelperProcess Process { get; } = process;
        public CancellationTokenSource Stop { get; } = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        public TaskCompletionSource<bool> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public nint Window;
        public bool VerifiedReady;
        public Task? Writing, Stopping;
    }
    private sealed class BrowserStartException(string safeStatus) : Exception
    { public string SafeStatus { get; } = safeStatus; }
    // Synthetic tests replace only this process boundary; production uses the owned helper below.
    internal abstract class HelperProcess : IDisposable
    {
        public abstract int Id { get; }
        public abstract bool HasExited { get; }
        public virtual int ExitCode => 0;
        public abstract Task SendAsync(string command, CancellationToken ct);
        public abstract ValueTask<string?> ReadEventAsync(CancellationToken ct);
        public abstract ValueTask<string?> ReadErrorAsync(CancellationToken ct);
        public abstract Task WaitForExitAsync(CancellationToken ct);
        public abstract void Kill();
        public abstract void Dispose();
    }
    private sealed class NativeHelper(Process process) : HelperProcess
    {
        public override int Id => process.Id;
        public override bool HasExited => process.HasExited;
        public override int ExitCode => process.ExitCode;
        public override async Task SendAsync(string command, CancellationToken ct)
        {
            await process.StandardInput.WriteLineAsync(command.AsMemory(), ct).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
        }
        public override ValueTask<string?> ReadEventAsync(CancellationToken ct) => process.StandardOutput.ReadLineAsync(ct);
        public override ValueTask<string?> ReadErrorAsync(CancellationToken ct) => process.StandardError.ReadLineAsync(ct);
        public override Task WaitForExitAsync(CancellationToken ct) => process.WaitForExitAsync(ct);
        public override void Kill() => process.Kill(entireProcessTree: true);
        public override void Dispose() => process.Dispose();
    }
    private static bool ValidateWindow(nint window, int process)
    { GetWindowThreadProcessId(window, out var owner); return owner == process && IsWindow(window); }
    [StructLayout(LayoutKind.Sequential)] private struct GuiThreadInfo
    { public uint Size, Flags; public nint Active, Focus, Capture, MenuOwner, MoveSize, Caret; public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetGUIThreadInfo(uint id, ref GuiThreadInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsChild(nint parent, nint child);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
}
