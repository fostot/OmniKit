using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

internal static class BrowserLifecycle
{
    [DllImport("user32.dll")] private static extern nint GetParent(nint child);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint child);
    [DllImport("kernel32.dll")] private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32First(nint snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32Next(nint snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ProcessEntry
    {
        public uint Size, Usage, Id; public nuint Heap; public uint Module, Threads, ParentId; public int Priority; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
    }

    internal static void Run()
    {
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ClientSize = new Size(900, 650) };
        host.Shown += async (_, _) =>
        {
            try { await CheckAsync(host); }
            catch (Exception error) { Console.WriteLine("FAIL: " + error.GetType().Name + ": " + error.Message); Environment.ExitCode = 1; }
            finally { host.Close(); }
        };
        Application.Run(host);
    }

    private static async Task CheckAsync(Form host)
    {
        var helperPath = Path.GetFullPath("src/OmniKit.Browser/bin/Release/OmniKit.Browser.exe");
        if (!File.Exists(helperPath)) throw new InvalidOperationException("Run from the repository root after building the Release helper.");
        var profile = Path.Combine(Path.GetTempPath(), "OmniKit-browser-lifecycle-" + Guid.NewGuid().ToString("N"));
        using var server = new HttpListener();
        using var port = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        port.Start(); var number = ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
        server.Prefixes.Add($"http://127.0.0.1:{number}/"); server.Start();
        var serving = Task.Run(async () =>
        {
            while (server.IsListening)
                try
                {
                    var context = await server.GetContextAsync();
                    var bytes = Encoding.UTF8.GetBytes("<html><body>Local lifecycle fixture</body></html>");
                    context.Response.ContentType = "text/html"; await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
        });
        var observedChildren = 0;
        try
        {
            foreach (var mode in new[] { "hide", "shutdown", "eof" })
            {
                var start = new ProcessStartInfo(helperPath) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                foreach (var value in new[] { "--parent-window", host.Handle.ToInt64().ToString(), "--parent-pid", Environment.ProcessId.ToString(), "--profile", profile }) start.ArgumentList.Add(value);
                using var helper = Process.Start(start) ?? throw new InvalidOperationException("Synthetic helper did not start.");
                var children = new List<Process>();
                try
                {
                    var ready = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    var read = Task.Run(async () =>
                    {
                        while (await helper.StandardOutput.ReadLineAsync() is { } line)
                        {
                            using var json = JsonDocument.Parse(line); var item = json.RootElement;
                            if (item.EnumerateObject().Any(p => p.Name is not ("kind" or "message" or "window")) || line.Contains("127.0.0.1") || line.Contains("lifecycle-")) throw new InvalidOperationException("Helper emitted unexpected browsing details.");
                            switch (item.GetProperty("kind").GetString())
                            {
                                case "ready": ready.TrySetResult(item.GetProperty("window").GetInt64()); break;
                                case "navigated": loaded.TrySetResult(); break;
                                case "error": throw new InvalidOperationException("Helper reported an error during the local lifecycle fixture.");
                            }
                        }
                    });
                    await helper.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { kind = "navigate", url = $"http://127.0.0.1:{number}/" })); await helper.StandardInput.FlushAsync();
                    var child = (nint)await ready.Task.WaitAsync(TimeSpan.FromSeconds(25));
                    await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    if (GetParent(child) != host.Handle) throw new InvalidOperationException("Browser is not a native child of the synthetic host.");
                    children.AddRange(CaptureDescendants(helper.Id)); observedChildren += children.Count;
                    if (children.Count < 2) throw new InvalidOperationException("No WebView2 child process tree was observed.");
                    if (mode == "eof") helper.StandardInput.Close();
                    else { await helper.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { kind = mode })); await helper.StandardInput.FlushAsync(); }
                    await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8)); await read.WaitAsync(TimeSpan.FromSeconds(2));
                    foreach (var process in children) await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    if (helper.ExitCode != 0 || IsWindow(child) || children.Any(p => !p.HasExited) || (await helper.StandardError.ReadToEndAsync()).Length != 0) throw new InvalidOperationException("Closing left a helper, window or owned WebView2 process running.");
                    await Task.Delay(500);
                    var idle = CaptureDescendants(helper.Id);
                    try { if (idle.Count != 0) throw new InvalidOperationException("A closed helper relaunched a child process while idle."); }
                    finally { foreach (var process in idle) process.Dispose(); }
                }
                finally
                {
                    if (!helper.HasExited) helper.Kill(entireProcessTree: true);
                    foreach (var process in children) { if (!process.HasExited) process.Kill(entireProcessTree: true); process.Dispose(); }
                }
            }
        }
        finally { server.Stop(); await serving; }
        Console.WriteLine($"PASS: 3 real helper launches reuse one synthetic profile; native parent ownership; hide/shutdown/EOF each exit helper and owned WebView2 tree ({observedChildren} child process handles observed); no idle relaunch. No game or existing browser profile accessed.");
    }

    private static List<Process> CaptureDescendants(int root)
    {
        var entries = new List<(int Id, int Parent)>();
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == -1) throw new InvalidOperationException("Could not inspect the synthetic process tree.");
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), Name = string.Empty };
            if (Process32First(snapshot, ref entry)) do { entries.Add(((int)entry.Id, (int)entry.ParentId)); } while (Process32Next(snapshot, ref entry));
        }
        finally { CloseHandle(snapshot); }
        var owned = new HashSet<int> { root }; bool changed;
        do { changed = false; foreach (var item in entries) if (owned.Contains(item.Parent) && owned.Add(item.Id)) changed = true; } while (changed);
        var result = new List<Process>();
        foreach (var id in owned.Where(id => id != root))
            try { var process = Process.GetProcessById(id); _ = process.Handle; result.Add(process); }
            catch (ArgumentException) { }
        return result;
    }
}
