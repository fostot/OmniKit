using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal static class Program
{
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    [DllImport("user32.dll")] static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] static extern int GetWindowRgn(nint window, nint region);
    [DllImport("gdi32.dll")] static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] static extern bool PtInRegion(nint region, int x, int y);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(nint value);
    [STAThread] static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
        if (args.Contains("--lifecycle")) { BrowserLifecycle.Run(); return; }
        var baseline = args.Contains("--baseline");
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ClientSize = new Size(900, 650) };
        host.Shown += async (_, _) =>
        {
            try
            {
                var assembly = Assembly.Load("OmniKit.Browser"); var type = assembly.GetType("OmniKit.Browser.BrowserForm")!;
                var profile = Path.Combine(Path.GetTempPath(), "OmniKit-browser-synthetic-" + Guid.NewGuid().ToString("N"));
                using var ownership = (IDisposable)Activator.CreateInstance(assembly.GetType("OmniKit.Browser.OwnedProfile")!, Members, null, [profile], null)!;
                using var form = (Form)Activator.CreateInstance(type, Members, null, [host.Handle, Environment.ProcessId, profile], null)!;
                object Field(string name) => type.GetField(name, Members)!.GetValue(form)!;
                form.Show(); await ((Task)type.GetProperty("Initialized", Members)!.GetValue(form)!).WaitAsync(TimeSpan.FromSeconds(25));
                var web = (WebView2)Field("browser"); var core = web.CoreWebView2;
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
                // Every test resource is served in-process, even if production filtering fails.
                var blockedResources = 0; var passedAdResources = 0; var navigations = 0;
                core.WebResourceRequested += (_, e) =>
                {
                    var uri = new Uri(e.Request.Uri);
                    if (uri.Host != "fixture.invalid")
                    {
                        if (e.Response?.StatusCode == 204) { ++blockedResources; return; }
                        ++passedAdResources;
                        e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes("window.syntheticAdExecuted=true;")), 200, "OK", "Content-Type: text/javascript\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-store");
                        return;
                    }
                    var page = """
                        <html><body style="background:#cdd8df;font:20px sans-serif;margin:30px">
                        <h1 id="fixture">Synthetic page</h1><a id="link" href="https://fixture.invalid/next" target="_blank">Open a user-clicked link</a>
                        <script src="https://ads.doubleclick.net/script.js"></script><img src="https://pubmatic.com/image.png">
                        <iframe src="https://adsrvr.org/frame"></iframe><script>fetch('https://google-analytics.com/collect').catch(()=>{});</script>
                        </body></html>
                        """;
                    e.Response ??= core.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(page)), 200, "OK", "Content-Type: text/html\r\nCache-Control: no-store");
                };
                var loaded = new TaskCompletionSource(); core.NavigationCompleted += (_, e) => { if (e.IsSuccess) { ++navigations; loaded.TrySetResult(); } else Console.WriteLine($"Synthetic navigation failure: {e.WebErrorStatus}."); };
                core.Navigate("https://fixture.invalid/"); await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Task.Delay(200);
                var menu = (Control)Field("menu"); var overlap = Rectangle.Intersect(menu.Bounds, web.Bounds);
                var covered = !overlap.IsEmpty && (web.Region == null || web.Region.IsVisible(overlap.X - web.Left + 1, overlap.Y - web.Top + 1));
                var layoutChanges = 0; web.SizeChanged += (_, _) => layoutChanges++;
                for (var i = 0; i < 20; i++) { SendMessage(menu.Handle, 0x200, 0, (nint)(8 | 8 << 16)); await Task.Delay(10); SendMessage(menu.Handle, 0x2A3, 0, 0); await Task.Delay(10); }
                var stable = await core.ExecuteScriptAsync("document.querySelector('#fixture').textContent") == "\"Synthetic page\"";
                Console.WriteLine($"Native hover baseline: overlapping browser pixels={covered}; browser resize events={layoutChanges}; page stable={stable}.");
                if (!baseline && covered) throw new InvalidOperationException("Floating chrome overlaps the native website paint region.");
                if (!baseline)
                {
                    if (blockedResources < 4 || passedAdResources != 0 || await core.ExecuteScriptAsync("!!window.syntheticAdExecuted") != "false") throw new InvalidOperationException("Native request protection did not block the synthetic ad resources.");
                    if (await core.ExecuteScriptAsync("window.open('https://fixture.invalid/popup')===null") != "true" || !core.Source.EndsWith('/')) throw new InvalidOperationException("Scripted popup was not suppressed.");
                    var region = CreateRectRgn(0, 0, 0, 0);
                    try { if (GetWindowRgn(web.Handle, region) == 0 || PtInRegion(region, menu.Left - web.Left + 15, menu.Top - web.Top + 14)) throw new InvalidOperationException("Native browser HWND does not exclude the floating menu."); }
                    finally { DeleteObject(region); }
                    var close = (Button)Field("closeBrowser");
                    var output = Path.Combine(Environment.CurrentDirectory, ".local", "browser-behavior-renders"); Directory.CreateDirectory(output);
                    async Task Capture(string name)
                    {
                        using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                        using var bytes = new MemoryStream(); await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, bytes); bytes.Position = 0; using var page = Image.FromStream(bytes);
                        using var graphics = Graphics.FromImage(image); graphics.DrawImage(page, web.Bounds);
                        foreach (var key in new[] { "controls", "notice", "extensionPanel", "menu", "closeBrowser", "resize" })
                        {
                            var control = (Control)Field(key); if (!control.Visible) continue;
                            using var part = new Bitmap(control.Width, control.Height); control.DrawToBitmap(part, new Rectangle(Point.Empty, control.Size)); graphics.DrawImage(part, control.Bounds);
                        }
                        image.Save(Path.Combine(output, name + ".png"));
                    }
                    foreach (var size in new[] { new Size(760, 500), new Size(360, 240) })
                    {
                        form.Size = size; type.GetMethod("LayoutChrome", Members)!.Invoke(form, null);
                        foreach (var open in new[] { false, true })
                        {
                            ((Control)Field("controls")).Visible = open; type.GetMethod("LayoutChrome", Members)!.Invoke(form, null);
                            if (!close.Visible || close.Right > form.ClientSize.Width || close.Bottom > form.ClientSize.Height || close.Bounds.IntersectsWith(menu.Bounds)) throw new InvalidOperationException("Persistent close overlaps or leaves the browser viewport.");
                            await Capture($"{size.Width}-{(open ? "controls" : "website")}");
                            if (size.Width == 760 && open && ((ToolStripItem)Field("protection")).IsOnOverflow) throw new InvalidOperationException("Protection toggle is hidden at the default browser size.");
                            if (size.Width == 360 && open)
                            {
                                var bar = (ToolStrip)Field("navigation"); bar.OverflowButton.ShowDropDown();
                                var dropdown = bar.OverflowButton.DropDown;
                                if (!dropdown.Visible || !((ToolStripItem)Field("protection")).Available) throw new InvalidOperationException("Narrow toolbar cannot reveal protection.");
                                using var popup = new Bitmap(dropdown.Width, dropdown.Height); dropdown.DrawToBitmap(popup, new Rectangle(Point.Empty, dropdown.Size)); popup.Save(Path.Combine(output, "360-overflow.png"));
                                dropdown.Close();
                            }
                        }
                    }
                    type.GetMethod("Error", Members)!.Invoke(form, ["A synthetic browser error can still be closed."]);
                    ((Control)Field("controls")).Visible = false; type.GetMethod("LayoutChrome", Members)!.Invoke(form, null); await Capture("360-error");
                    if (!close.Visible) throw new InvalidOperationException("Close disappeared on a browser error.");
                    // CDP injects a trusted input event only into this synthetic WebView, without moving the desktop cursor.
                    await core.ExecuteScriptAsync("document.querySelector('#link').scrollIntoView({block:'center'});");
                    var newWindows = 0; var initiated = false; var beforeLink = navigations;
                    core.NewWindowRequested += (_, e) => { newWindows++; initiated = e.IsUserInitiated; };
                    using var rect = JsonDocument.Parse(await core.ExecuteScriptAsync("(()=>{const r=document.querySelector('#link').getBoundingClientRect();return {x:r.x+5,y:r.y+5};})()"));
                    var x = rect.RootElement.GetProperty("x").GetDouble(); var y = rect.RootElement.GetProperty("y").GetDouble();
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new { type="mousePressed", x, y, button="left", clickCount=1 }));
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new { type="mouseReleased", x, y, button="left", clickCount=1 }));
                    await Until(() => core.Source.EndsWith("/next") && navigations > beforeLink, "A user-clicked target-blank link did not load in the same view.");
                    if (newWindows != 1 || !initiated) throw new InvalidOperationException("Synthetic link did not use the native user-initiated path.");
                    var previous = navigations; ((ToolStripButton)Field("protection")).PerformClick();
                    await Until(() => navigations > previous, "Protection toggle did not reload."); await Task.Delay(150);
                    if (passedAdResources == 0 || await core.ExecuteScriptAsync("!!window.syntheticAdExecuted") != "true") throw new InvalidOperationException("Session opt-out did not disable filtering.");
                    using var webProcess = Process.GetProcessById((int)core.BrowserProcessId); _ = webProcess.Handle;
                    var closed = new TaskCompletionSource(); form.FormClosed += (_, _) => closed.TrySetResult();
                    close.PerformClick(); await closed.Task.WaitAsync(TimeSpan.FromSeconds(6));
                    if (!webProcess.HasExited) throw new InvalidOperationException("WebView2 process survived the persistent close button.");
                    Console.WriteLine("PASS: native clipping, hover, 6 narrow/default/overflow renders, persistent close, 4 blocked request types, scripted popup suppression, trusted same-view link, protection opt-out, owned WebView2 process exit. All test page requests were fulfilled in-process.");
                }
                else form.Close();
            }
            catch (Exception error) { Console.WriteLine("FAIL: " + error.GetType().Name + ": " + error.Message); Environment.ExitCode = 1; }
            finally { host.Close(); }
        };
        Application.Run(host);
    }

    private static async Task Until(Func<bool> predicate, string reason)
    {
        var watch = Stopwatch.StartNew();
        while (!predicate()) { if (watch.Elapsed > TimeSpan.FromSeconds(8)) throw new InvalidOperationException(reason); await Task.Delay(20); }
    }
}
