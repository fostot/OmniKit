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

    private sealed class FakeHelper : WikiBrowser.HelperProcess
    {
        private readonly Channel<string> events = Channel.CreateUnbounded<string>();
        private readonly TaskCompletionSource exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource write = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int active, maximum, kills, disposed;
        public FakeHelper(bool ready = false) { if (ready) Emit("{\"kind\":\"ready\",\"window\":123}"); }
        public bool StallWrites, IgnoreWriteCancellation;
        public ConcurrentQueue<string> Commands { get; } = new();
        public int ActiveWrites => Volatile.Read(ref active);
        public int MaximumWrites => Volatile.Read(ref maximum);
        public int Kills => Volatile.Read(ref kills);
        public bool Disposed => Volatile.Read(ref disposed) != 0;
        public override int Id => 7;
        public override bool HasExited => exit.Task.IsCompleted;
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
                if (command.Contains("shutdown")) Exit();
            }
            finally { Interlocked.Decrement(ref active); }
        }
        public override async ValueTask<string?> ReadEventAsync(CancellationToken ct)
        { try { return await events.Reader.ReadAsync(ct); } catch (ChannelClosedException) { return null; } }
        public override async ValueTask<string?> ReadErrorAsync(CancellationToken ct) { await Task.Delay(Timeout.Infinite, ct); return null; }
        public override Task WaitForExitAsync(CancellationToken ct) => exit.Task.WaitAsync(ct);
        public override void Kill() { Interlocked.Increment(ref kills); Exit(); }
        public override void Dispose() => Interlocked.Exchange(ref disposed, 1);
    }
    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        { int previous; do { previous = Volatile.Read(ref target); if (previous >= value) return; } while (Interlocked.CompareExchange(ref target, value, previous) != previous); }
    }
}
