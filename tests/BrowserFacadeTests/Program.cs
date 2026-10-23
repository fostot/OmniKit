using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using OmniKit.Integration;

internal static class Program
{
    private static int checks;
    private const string Url = "https://example.invalid/synthetic";
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    private static async Task Until(Func<bool> condition, string reason)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(4)) throw new Exception(reason);
            await Task.Delay(5);
        }
    }
    private static WikiBrowser Create(Func<WikiBrowser.HelperProcess> factory, int startMs = 500, int pipeMs = 200)
        => new(factory, (window, id) => window == 123 && id == 7,
            TimeSpan.FromMilliseconds(startMs), TimeSpan.FromMilliseconds(pipeMs), TimeSpan.FromMilliseconds(80));

    private static async Task Main()
    {
        var hiddenStarts = 0;
        using (var unopened = Create(() => { Interlocked.Increment(ref hiddenStarts); return new FakeHelper(); }))
        {
            unopened.Hide();
            Check(hiddenStarts == 0 && unopened.PendingCount == 0 && !unopened.IsReady, "Hide before open creates no helper or command");
        }
        await BoundAndUnload();
        await Readiness();
        await LateStartup(false);
        await LateStartup(true);
        await PipeTimeout();
        await ShutdownTimeout();
        await ReadyTimeout();
        await InvalidReady();
        await StartupFailure();
        await CloseAndReopen();
        await ImmediateReopenWaitsForProfileRelease();
        await ForcedExitMustFinishBeforeReopen();
        await UnconfirmedForcedExitBlocksReopen();
        await UncleanExitBlocksReopen(73);
        await UncleanExitBlocksReopen(1);
        await CloseDuringStartup();
        await CloseDropsQueuedNavigation();
        Console.WriteLine($"PASS: {checks} browser facade checks; synthetic helpers only, no process/profile/network access.");
    }

    private static async Task BoundAndUnload()
    {
        var starts = 0; var fake = new FakeHelper();
        using var browser = Create(() => { Interlocked.Increment(ref starts); return fake; }, pipeMs: 3000);
        Check(!browser.IsReady && !browser.TryOpenUrl(Url, true) && starts == 0 && browser.PendingCount == 0, "Capture cannot start an unverified helper");
        browser.Show();
        await Until(() => starts == 1, "Manual startup begins");
        Check(!browser.IsReady && fake.Commands.Count == 0, "No command before valid ready handshake");
        fake.Emit("{\"kind\":\"ready\",\"window\":123}");
        await Until(() => browser.IsReady && browser.PendingCount == 0, "Verified helper becomes ready");
        fake.StallWrites = true; fake.IgnoreWriteCancellation = true;
        var admissions = await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() => browser.TryOpenUrl(Url, true)))).WaitAsync(TimeSpan.FromSeconds(2));
        Check(admissions.Count(value => value) == 8 && browser.PendingCount == 8, "Exactly eight accepted commands including in-flight work under concurrent load");
        await Until(() => fake.ActiveWrites == 1, "One stalled write exists");
        Check(fake.MaximumWrites == 1 && starts == 1, "Single worker, one writer, no extra startup");
        Check(!browser.TryOpenUrl(Url, true), "Overload rejects without a waiter task");
        var watch = Stopwatch.StartNew(); browser.Dispose();
        Check(watch.Elapsed < TimeSpan.FromMilliseconds(100) && !browser.IsReady, "Dispose never waits for stalled pipe");
        Check(!browser.TryOpenUrl(Url) && browser.PendingCount <= 1, "Dispose discards queued work and rejects new work");
        await Until(() => fake.Disposed && browser.PendingCount == 0, "Stalled helper is killed and in-flight slot released");
        Check(fake.Kills == 1 && fake.Commands.Count(command => command.Contains("navigate")) == 1 && fake.MaximumWrites == 1, "Unload never overlaps shutdown with blocked writer or emits queued commands");
    }
    private static async Task Readiness()
    {
        var fake = new FakeHelper(ready: true);
        using var browser = Create(() => fake);
        browser.Show(); await Until(() => browser.IsReady && browser.PendingCount == 0, "Ready");
        fake.Emit("{\"kind\":\"error\"}"); await Until(() => !browser.IsReady && browser.HasError, "Error revokes readiness");
        Check(!browser.TryOpenUrl(Url, true), "Error routes subsequent captures back to normal handling");
        fake.Emit("{\"kind\":\"navigated\"}"); await Until(() => browser.IsReady, "Successful navigation revalidates original owned HWND");
        fake.Emit("{\"kind\":\"closed\"}"); await Until(() => !browser.IsReady, "Close revokes readiness");
        Check(!browser.TryOpenUrl(Url, true), "Closed helper cannot capture");
        browser.Dispose(); await Until(() => fake.Disposed, "Closed helper cleanup");

        var exited = new FakeHelper(ready: true);
        using var second = Create(() => exited); second.Show();
        await Until(() => second.IsReady && second.PendingCount == 0, "Second ready");
        exited.Exit(); await Until(() => !second.IsReady, "Exit signal clears readiness even without closed event");
        Check(!second.TryOpenUrl(Url, true), "Exited process cannot capture");
    }
    private static async Task LateStartup(bool timeoutFirst)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var fake = new FakeHelper(ready: true); var starts = 0;
        using var browser = Create(() => { Interlocked.Increment(ref starts); entered.TrySetResult(); release.Wait(); return fake; }, startMs: 60);
        browser.Show(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        if (timeoutFirst)
        {
            await Until(() => browser.PendingCount == 0 && browser.HasError, "Startup timeout frees pending slot");
            for (var i = 0; i < 8; i++) browser.Show();
            await Until(() => browser.PendingCount == 0, "Requests fail while timed-out startup remains owned");
            Check(starts == 1, "A stalled native start never accumulates additional starting tasks");
        }
        var watch = Stopwatch.StartNew(); browser.Dispose();
        Check(watch.Elapsed < TimeSpan.FromMilliseconds(100) && !browser.IsReady, "Unload does not wait for native startup");
        release.Set();
        await Until(() => fake.Disposed && browser.PendingCount == 0, "Late process is terminated after unload");
        Check(fake.Kills == 1 && !browser.IsReady && fake.Commands.Count == 0, "Late start never publishes or receives queued commands");
    }
    private static async Task PipeTimeout()
    {
        var fake = new FakeHelper(ready: true) { StallWrites = true, IgnoreWriteCancellation = true };
        using var browser = Create(() => fake, pipeMs: 50);
        browser.Show(); await Until(() => fake.Disposed && browser.PendingCount == 0, "Pipe and shutdown timeouts terminate a cancellation-ignoring helper");
        Check(fake.Kills == 1 && browser.HasError && !browser.IsReady, "Pipe failure revokes readiness and cleans child");
    }
    private static async Task InvalidReady()
    {
        var fake = new FakeHelper();
        using var browser = Create(() => fake);
        browser.Show(); fake.Emit("{\"kind\":\"ready\",\"window\":456}");
        await Until(() => fake.Disposed && browser.PendingCount == 0, "Invalid HWND fails startup");
        Check(!browser.IsReady && browser.HasError && fake.Commands.All(value => !value.Contains("navigate")), "Unowned HWND never enables capture");
    }
    private static async Task ShutdownTimeout()
    {
        var fake = new FakeHelper(ready: true);
        using var browser = Create(() => fake); browser.Show();
        await Until(() => browser.IsReady && browser.PendingCount == 0, "Ready before shutdown test");
        fake.StallWrites = true; fake.IgnoreWriteCancellation = true;
        var watch = Stopwatch.StartNew(); browser.Dispose();
        Check(watch.Elapsed < TimeSpan.FromMilliseconds(100), "Graceful shutdown write never blocks unload");
        await Until(() => fake.Disposed, "Shutdown pipe timeout completes cleanup");
        Check(fake.Kills == 1 && fake.MaximumWrites == 1 && !browser.IsReady, "Cancellation-ignoring shutdown is terminated once");
    }
    private static async Task ReadyTimeout()
    {
        var fake = new FakeHelper();
        using var browser = Create(() => fake, startMs: 60); browser.Show();
        await Until(() => fake.Disposed && browser.PendingCount == 0, "No ready handshake times out and cleans helper");
        Check(!browser.IsReady && browser.HasError && !browser.TryOpenUrl(Url, true), "Unready helper cannot swallow a captured link");
    }
    private static async Task StartupFailure()
    {
        using var browser = Create(() => throw new IOException("Synthetic private error text"));
        browser.Show(); await Until(() => browser.PendingCount == 0 && browser.HasError, "Startup failure resolves");
        Check(!browser.IsReady && !browser.Status.Contains("private") && !browser.TryOpenUrl(Url, true), "Startup exceptions are sanitized and cannot consume captured links");
    }

    private static async Task CloseAndReopen()
    {
        var starts = 0; var first = new FakeHelper(ready: true); var second = new FakeHelper(ready: true);
        using var browser = Create(() => Interlocked.Increment(ref starts) == 1 ? first : second);
        Check(browser.TryOpenUrl(Url), "An explicit captured link can cold-start the browser without prewarming.");
        await Until(() => browser.IsReady && browser.PendingCount == 0, "First helper ready");
        browser.Hide(); await Until(() => first.Disposed && first.HasExited, "Close releases the helper");
        await Task.Delay(40);
        Check(!browser.IsReady && starts == 1 && browser.PendingCount == 0 && !browser.HasError,
            "A closed browser remains stopped while idle.");
        Check(first.Commands.Count(c => c.Contains("shutdown")) == 1, "Close requests shutdown instead of hidden browsing.");
        Check(browser.TryOpenUrl(Url), "A later explicit link can reopen the browser.");
        await Until(() => starts == 2 && browser.IsReady && browser.PendingCount == 0, "New helper ready");
        browser.Hide(); await Until(() => second.Disposed, "Second helper closed");
    }

    private static async Task ImmediateReopenWaitsForProfileRelease()
    {
        var first = new FakeHelper(ready: true); var second = new FakeHelper(ready: true); var starts = 0;
        using var browser = new WikiBrowser(() => Interlocked.Increment(ref starts) == 1 ? first : second,
            (window, id) => window == 123 && id == 7, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        browser.Show(); await Until(() => browser.IsReady && browser.PendingCount == 0, "Initial helper is ready");
        first.StallWrites = true;
        browser.Hide(); browser.OpenUrl(Url);
        await Until(() => first.ActiveWrites == 1, "Old helper shutdown is in flight");
        Check(starts == 1, "Immediate reopen waits for the old owned profile lock to release");
        first.Exit();
        await Until(() => browser.IsReady && browser.PendingCount == 0 && starts == 2, "Queued explicit reopen starts after cleanup");
        Check(first.Disposed && second.Commands.Count(c => c.Contains("navigate")) == 1, "Reopen sends one navigation to a fresh helper after disposal");
    }

    private static async Task CloseDuringStartup()
    {
        using var release = new ManualResetEventSlim(); var entered = new TaskCompletionSource();
        var fake = new FakeHelper(ready: true); var starts = 0;
        using var browser = Create(() => { Interlocked.Increment(ref starts); entered.TrySetResult(); release.Wait(); return fake; }, startMs: 1000);
        browser.Show(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        browser.Hide(); await Until(() => browser.PendingCount == 0, "Close cancels the pending start wait");
        release.Set(); await Until(() => fake.Disposed, "Late process from a closed request is reclaimed");
        Check(!browser.IsReady && !browser.HasError && starts == 1 && fake.Commands.Count == 0,
            "Late readiness cannot reopen a closed browser or send its abandoned request.");
    }

    private static async Task ForcedExitMustFinishBeforeReopen()
    {
        var first = new FakeHelper(ready: true) { IgnoreShutdown = true, DelayKillExit = true };
        var second = new FakeHelper(ready: true); var starts = 0;
        using var browser = new WikiBrowser(() => Interlocked.Increment(ref starts) == 1 ? first : second,
            (window, id) => window == 123 && id == 7, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        browser.Show(); await Until(() => browser.IsReady && browser.PendingCount == 0, "Ready before delayed termination");
        browser.Hide(); browser.OpenUrl(Url);
        await Until(() => first.Kills == 1, "Graceful timeout requests forced termination");
        Check(!first.HasExited && !first.Disposed && starts == 1,
            "Kill returning cannot release cleanup ownership or launch a replacement while exit is delayed.");
        first.Exit();
        await Until(() => starts == 2 && browser.IsReady && browser.PendingCount == 0, "Confirmed clean exit releases the queued explicit reopen");
        Check(first.Disposed && second.Commands.Count(c => c.Contains("navigate")) == 1,
            "Delayed but confirmed clean termination releases one explicit request, without retry.");
    }

    private static async Task UnconfirmedForcedExitBlocksReopen()
    {
        var first = new FakeHelper(ready: true) { IgnoreShutdown = true, DelayKillExit = true, IgnoreExitCancellation = true };
        var starts = 0;
        using var browser = Create(() => { Interlocked.Increment(ref starts); return first; });
        browser.Show(); await Until(() => browser.IsReady && browser.PendingCount == 0, "Ready before unconfirmed termination");
        browser.Hide(); browser.OpenUrl(Url);
        await Until(() => first.Disposed && browser.PendingCount == 0, "Both shutdown waits finish despite cancellation-ignoring exit");
        Check(!first.HasExited && first.Kills == 1 && starts == 1 && browser.HasError,
            "An unconfirmed forced exit cannot reuse the still-owned profile or accumulate launches.");
        Check(!browser.TryOpenUrl(Url) && browser.Status.Contains("Restart OmniKit", StringComparison.Ordinal),
            "Further explicit requests fail closed with safe recovery guidance.");
        first.Exit();
        browser.Hide(); browser.Show();
        Check(starts == 1 && browser.PendingCount == 0 && browser.HasError,
            "Late exit and Hide cannot erase an unconfirmed shutdown latch within the same plugin instance.");
    }

    private static async Task UncleanExitBlocksReopen(int exitCode)
    {
        var first = new FakeHelper(ready: true) { SimulatedExitCode = exitCode }; var starts = 0;
        using var browser = Create(() => { Interlocked.Increment(ref starts); return first; });
        browser.Show(); await Until(() => browser.IsReady && browser.PendingCount == 0, "Ready before unsuccessful helper close");
        browser.Hide(); browser.OpenUrl(Url);
        await Until(() => first.Disposed && browser.PendingCount == 0, "Unclean exit releases local handles without accepting its profile");
        Check(first.HasExited && starts == 1 && !browser.TryOpenUrl(Url) && browser.HasError,
            "Reserved child-exit failure and unexpected nonzero exit both prevent profile reuse.");
    }

    private static async Task CloseDropsQueuedNavigation()
    {
        var fake = new FakeHelper(ready: true); var starts = 0;
        using var browser = Create(() => { Interlocked.Increment(ref starts); return fake; }, pipeMs: 3000);
        browser.Show(); await Until(() => browser.IsReady && browser.PendingCount == 0, "Ready before queued requests");
        fake.StallWrites = true; fake.IgnoreWriteCancellation = true;
        browser.OpenUrl(Url); browser.OpenUrl(Url); browser.OpenUrl(Url);
        await Until(() => fake.ActiveWrites == 1, "One request in flight");
        fake.Emit("{\"kind\":\"closing\"}");
        await Until(() => fake.Disposed && browser.PendingCount == 0, "Close button discards queued requests and releases blocked writer");
        await Task.Delay(40);
        Check(starts == 1 && !browser.IsReady && fake.Commands.Count(c => c.Contains("navigate")) == 1,
            "Queued navigation cannot restart a browser closed by its own button.");
    }

    private sealed class FakeHelper : WikiBrowser.HelperProcess
    {
        private readonly Channel<string> events = Channel.CreateUnbounded<string>();
        private readonly TaskCompletionSource exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource write = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int active, maximum, kills, disposed;
        public FakeHelper(bool ready = false) { if (ready) Emit("{\"kind\":\"ready\",\"window\":123}"); }
        public bool StallWrites, IgnoreWriteCancellation, IgnoreShutdown, DelayKillExit, IgnoreExitCancellation;
        public int SimulatedExitCode;
        public ConcurrentQueue<string> Commands { get; } = new();
        public int ActiveWrites => Volatile.Read(ref active);
        public int MaximumWrites => Volatile.Read(ref maximum);
        public int Kills => Volatile.Read(ref kills);
        public bool Disposed => Volatile.Read(ref disposed) != 0;
        public override int Id => 7;
        public override bool HasExited => exit.Task.IsCompleted;
        public override int ExitCode => SimulatedExitCode;
        public void Emit(string value) => events.Writer.TryWrite(value);
        public void Exit() { exit.TrySetResult(); events.Writer.TryComplete(); write.TrySetResult(); }
        public override async Task SendAsync(string command, CancellationToken ct)
        {
            var count = Interlocked.Increment(ref active); InterlockedExtensions.Max(ref maximum, count);
            try
            {
                Commands.Enqueue(command);
                if (StallWrites)
                {
                    if (IgnoreWriteCancellation) await write.Task;
                    else await write.Task.WaitAsync(ct);
                }
                if (command.Contains("shutdown") && !IgnoreShutdown) Exit();
            }
            finally { Interlocked.Decrement(ref active); }
        }
        public override async ValueTask<string?> ReadEventAsync(CancellationToken ct)
        { try { return await events.Reader.ReadAsync(ct); } catch (ChannelClosedException) { return null; } }
        public override async ValueTask<string?> ReadErrorAsync(CancellationToken ct) { await Task.Delay(Timeout.Infinite, ct); return null; }
        public override Task WaitForExitAsync(CancellationToken ct) => IgnoreExitCancellation ? exit.Task : exit.Task.WaitAsync(ct);
        public override void Kill() { Interlocked.Increment(ref kills); if (!DelayKillExit) Exit(); }
        public override void Dispose() => Interlocked.Exchange(ref disposed, 1);
    }
    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        { int previous; do { previous = Volatile.Read(ref target); if (previous >= value) return; } while (Interlocked.CompareExchange(ref target, value, previous) != previous); }
    }
}
