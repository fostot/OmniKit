using System.Globalization;

namespace OmniKit.Browser;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 6 || args[0] != "--parent-window" || args[2] != "--parent-pid" || args[4] != "--profile" ||
                !long.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var handle) ||
                !int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || !NativeWindow.Matches((nint)handle, pid))
            {
                BrowserProtocol.Emit("error", "The browser host could not be verified.");
                return 1;
            }
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, _) => { BrowserProtocol.Emit("error", "The browser encountered an unexpected error."); Application.Exit(); };
            using var profile = new OwnedProfile(args[5]);
            using var form = new BrowserForm((nint)handle, pid, profile.Path);
            using var cancellation = new CancellationTokenSource();
            // Console.In may synchronously block despite its async signature.
            // Keep pipe reads off the STA that owns WebView2's message pump.
            form.Shown += (_, _) => _ = Task.Run(() => ReadCommandsAsync(form, cancellation.Token));
            form.FormClosed += (_, _) => cancellation.Cancel();
            Application.Run(form);
            BrowserProtocol.Emit("closed", "The browser closed.");
            return 0;
        }
        catch (Exception)
        {
            BrowserProtocol.Emit("error", "The browser could not start. Check its runtime and dedicated profile.");
            return 1;
        }
    }

    private static async Task ReadCommandsAsync(BrowserForm form, CancellationToken cancellation)
    {
        try { await BrowserProtocol.ReadAsync(Console.In, command => form.InvokeAsync(() => form.Dispatch(command), cancellation), cancellation); }
        catch (OperationCanceledException) { return; }
        catch (Exception) { BrowserProtocol.Emit("error", "The browser command channel closed."); }
        if (!cancellation.IsCancellationRequested && !form.IsDisposed)
            try { await form.InvokeAsync(form.Close); } catch (InvalidOperationException) { }
    }
}
