using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;

namespace OmniKit.Integration;

/// <summary>Optional interception of this process's standard Unicode shell web launches only.</summary>
internal sealed unsafe class WebLinkInterceptor : IDisposable
{
    private readonly IGameInteropProvider interop;
    private readonly Func<string, bool> tryQueue;
    private readonly object lifecycle = new();
    private Hook<Execute>? execute;
    private Hook<ExecuteEx>? executeEx;
    private int enabled, disposed;
    private long captured, passedThrough, failures;
    private string status = "Standard web link capture is off.";
    private static readonly AsyncLocal<int> bypassDepth = new();
    [ThreadStatic] private static int forwardingDepth;

    /// <param name="tryQueue">Must quickly accept a bounded in-memory queue; never open UI or wait here.
    /// Return false if the browser cannot accept the link, so Windows handles the original request.</param>
    public WebLinkInterceptor(IGameInteropProvider interop, Func<string, bool> tryQueue)
    { this.interop = interop; this.tryQueue = tryQueue; }

    public bool IsEnabled => Volatile.Read(ref enabled) != 0;
    public string Status => Volatile.Read(ref status);
    public long CapturedCount => Interlocked.Read(ref captured);
    public long PassedThroughCount => Interlocked.Read(ref passedThrough);
    public long FailureCount => Interlocked.Read(ref failures);

    /// <summary>Use around explicit external authentication launches. Flows to .NET's shell thread.</summary>
    public static IDisposable Bypass() => new BypassScope();

    public bool SetEnabled(bool value)
    {
        lock (lifecycle)
        {
            if (disposed != 0) return false;
            if (!value)
            {
                Volatile.Write(ref enabled, 0);
                DisableHooks();
                Volatile.Write(ref status, "Standard web link capture is off.");
                return true;
            }
            if (IsEnabled) return true;
            try
            {
                if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) throw new PlatformNotSupportedException();
                execute ??= interop.HookFromSymbol<Execute>("shell32.dll", "ShellExecuteW", ExecuteDetour);
                executeEx ??= interop.HookFromSymbol<ExecuteEx>("shell32.dll", "ShellExecuteExW", ExecuteExDetour);
                execute.Enable(); executeEx.Enable();
                Volatile.Write(ref enabled, 1);
                Volatile.Write(ref status, "Capturing standard web links from the game and plugins.");
                return true;
            }
            catch (Exception)
            {
                Volatile.Write(ref enabled, 0); DisableHooks();
                Interlocked.Increment(ref failures);
                Volatile.Write(ref status, "Web link capture is unavailable; normal link handling is preserved.");
                return false;
            }
        }
    }

    private nint ExecuteDetour(nint window, nint verb, nint file, nint parameters, nint directory, int show)
    {
        if (TryCapture(verb, file, parameters, directory, show)) return 33;
        Interlocked.Increment(ref passedThrough);
        forwardingDepth++;
        try { return execute!.OriginalDisposeSafe(window, verb, file, parameters, directory, show); }
        finally { forwardingDepth--; }
    }

    private int ExecuteExDetour(ShellExecuteInfo* info)
    {
        // Preserve ID lists, classes, handles, verbs and uncommon shell launch semantics.
        const uint supportedFlags = 0x40 | 0x100 | 0x400 | 0x4000 | 0x8000;
        if (info != null && info->Size == sizeof(ShellExecuteInfo) && (info->Mask & ~supportedFlags) == 0
            && info->IdList == 0 && info->Class == 0 && info->ClassKey == 0 && info->HotKey == 0 && info->IconOrMonitor == 0
            && TryCapture(info->Verb, info->File, info->Parameters, info->Directory, info->Show))
        {
            // Documented successful shell launches can have no process handle (for example DDE).
            info->Instance = 33; info->Process = 0;
            return 1;
        }
        Interlocked.Increment(ref passedThrough);
        forwardingDepth++;
        try { return executeEx!.OriginalDisposeSafe(info); }
        finally { forwardingDepth--; }
    }

    private bool TryCapture(nint verb, nint file, nint parameters, nint directory, int show)
    {
        if (!IsEnabled || Volatile.Read(ref disposed) != 0 || bypassDepth.Value != 0 || forwardingDepth != 0) return false;
        try
        {
            // A hidden/minimized launch must not unexpectedly open a visible in-game panel.
            if (show is not (1 or 3 or 5 or 9 or 10)
                || (parameters != 0 && *(char*)parameters != '\0') || (directory != 0 && *(char*)directory != '\0')) return false;
            var operation = ReadString(verb, 16);
            if (operation.Length != 0 && !operation.Equals("open", StringComparison.OrdinalIgnoreCase)) return false;
            if (!TryWebDestination(ReadString(file, 4096), out var destination)) return false;
            // Even a mistakenly reentrant callback must not recursively capture its own shell launch.
            forwardingDepth++;
            try { if (!tryQueue(destination)) return false; }
            finally { forwardingDepth--; }
            Interlocked.Increment(ref captured);
            return true;
        }
        catch (Exception)
        {
            // No URL, launch arguments or exception messages are logged or exposed.
            Interlocked.Increment(ref failures);
            return false;
        }
    }

    internal static bool TryWebDestination(string text, out string destination)
    {
        destination = "";
        if (text.Length is < 1 or > 4096 || text.Any(char.IsControl)
            || !Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.Host.Length == 0 || uri.UserInfo.Length != 0 || uri.IsLoopback) return false;
        // Conservative recognition, not a universal authentication detector. Our own sign-in uses Bypass.
        foreach (var part in uri.IdnHost.Split('.').Concat(Uri.UnescapeDataString(uri.AbsolutePath).Split('/', StringSplitOptions.RemoveEmptyEntries)))
            if (AuthSegments.Contains(part)) return false;
        if (HasAuthParameters(uri.Query) || HasAuthParameters(uri.Fragment)) return false;
        destination = uri.AbsoluteUri;
        return true;
    }

    private static readonly HashSet<string> AuthSegments = new(StringComparer.OrdinalIgnoreCase)
    { "auth", "oauth", "oauth2", "authorize", "authorization", "login", "log-in", "signin", "sign-in", "signon", "sign-on", "sso", "account", "accounts", "identity", "callback", "token", "connect", "device" };
    private static readonly HashSet<string> AuthParameters = new(StringComparer.OrdinalIgnoreCase)
    { "code", "state", "token", "access_token", "id_token", "refresh_token", "client_id", "client_secret", "response_type", "redirect_uri", "scope", "code_challenge", "code_verifier", "session", "sessionid", "ticket", "samlrequest", "samlresponse", "relaystate", "assertion" };
    private static bool HasAuthParameters(string text)
    {
        foreach (var parameter in text.TrimStart('?', '#').Split('&', ';'))
        {
            var equal = parameter.IndexOf('=');
            var key = Uri.UnescapeDataString(equal < 0 ? parameter : parameter[..equal]);
            if (AuthParameters.Contains(key)) return true;
        }
        return false;
    }

    private static string ReadString(nint pointer, int limit)
    {
        if (pointer == 0) return "";
        var chars = (char*)pointer;
        var length = 0;
        while (length <= limit && chars[length] != '\0') length++;
        if (length > limit) throw new ArgumentException();
        return new string(chars, 0, length);
    }

    private void DisableHooks()
    {
        try { execute?.Disable(); } catch (Exception) { Interlocked.Increment(ref failures); }
        try { executeEx?.Disable(); } catch (Exception) { Interlocked.Increment(ref failures); }
    }
    public void Dispose()
    {
        lock (lifecycle)
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            Volatile.Write(ref enabled, 0); DisableHooks();
            try { execute?.Dispose(); } catch (Exception) { Interlocked.Increment(ref failures); }
            try { executeEx?.Dispose(); } catch (Exception) { Interlocked.Increment(ref failures); }
            Volatile.Write(ref status, "Standard web link capture is off.");
        }
    }
    private sealed class BypassScope : IDisposable
    {
        private readonly int previous = bypassDepth.Value;
        private bool disposed;
        public BypassScope() => bypassDepth.Value = previous + 1;
        public void Dispose() { if (disposed) return; disposed = true; bypassDepth.Value = previous; }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate nint Execute(nint window, nint verb, nint file, nint parameters, nint directory, int show);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate int ExecuteEx(ShellExecuteInfo* info);
    [StructLayout(LayoutKind.Sequential)] internal struct ShellExecuteInfo
    {
        public uint Size, Mask;
        public nint Window, Verb, File, Parameters, Directory;
        public int Show;
        public nint Instance, IdList, Class, ClassKey;
        public uint HotKey;
        public nint IconOrMonitor, Process;
    }
}
