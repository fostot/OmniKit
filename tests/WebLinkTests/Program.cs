using System.Diagnostics;
using System.Runtime.InteropServices;
using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;

internal static unsafe class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate int ExecuteEx(Info* info);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate nint Execute(nint window, nint verb, nint file, nint parameters, nint directory, int show);
    [StructLayout(LayoutKind.Sequential)] internal struct Info
    {
        public uint Size, Mask;
        public nint Window, Verb, File, Parameters, Directory;
        public int Show;
        public nint Instance, IdList, Class, ClassKey;
        public uint HotKey;
        public nint IconOrMonitor, Process;
    }
    private static readonly AsyncLocal<int> Bypass = new();
    private static int checks;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }

    private static void Main()
    {
        Check(Environment.Is64BitProcess && OperatingSystem.IsWindows(), "Windows x64 required");
        Check(sizeof(Info) == 112, "SHELLEXECUTEINFOW x64 size");
        Check(Marshal.OffsetOf<Info>(nameof(Info.Instance)) == 56 && Marshal.OffsetOf<Info>(nameof(Info.Process)) == 104, "ABI offsets");
        // These bottom hooks never call Windows. No test launches a browser or another process.
        var module = NativeLibrary.Load("shell32.dll");
        var exAddress = NativeLibrary.GetExport(module, "ShellExecuteExW");
        var wAddress = NativeLibrary.GetExport(module, "ShellExecuteW");
        var exGuardCalls = 0; var wGuardCalls = 0; var bypassSeen = false;
        ExecuteEx exGuard = info => { exGuardCalls++; bypassSeen |= Bypass.Value == 1; info->Instance = 33; info->Process = 0; return 1; };
        Execute wGuard = (_, _, _, _, _, _) => { wGuardCalls++; return 33; };
        var bottomEx = ReloadedHooks.Instance.CreateHook(exGuard, exAddress.ToInt64());
        var bottomW = ReloadedHooks.Instance.CreateHook(wGuard, wAddress.ToInt64());
        bottomEx.Activate(); bottomW.Activate();
        var invokeEx = Marshal.GetDelegateForFunctionPointer<ExecuteEx>(exAddress);
        var invokeW = Marshal.GetDelegateForFunctionPointer<Execute>(wAddress);
        var info = new Info { Size = (uint)sizeof(Info) };
        Check(invokeEx(&info) == 1 && exGuardCalls == 1 && info.Instance == 33 && info.Process == 0, "Ex guard armed");
        Check(invokeW(0,0,0,0,0,1) == 33 && wGuardCalls == 1, "W guard armed");

        IHook<ExecuteEx>? topEx = null; IHook<Execute>? topW = null;
        var outerExCalls = 0; var outerWCalls = 0;
        ExecuteEx exOuter = value => { outerExCalls++; return topEx!.OriginalFunction(value); };
        Execute wOuter = (a,b,c,d,e,f) => { outerWCalls++; return topW!.OriginalFunction(a,b,c,d,e,f); };
        topEx = ReloadedHooks.Instance.CreateHook(exOuter, exAddress.ToInt64());
        topW = ReloadedHooks.Instance.CreateHook(wOuter, wAddress.ToInt64());
        topEx.Activate(); topW.Activate();
        Check(invokeEx(&info) == 1 && outerExCalls == 1 && exGuardCalls == 2, "Ex chain");
        Check(invokeW(0,0,0,0,0,1) == 33 && outerWCalls == 1 && wGuardCalls == 2, "W chain");

        Bypass.Value = 1;
        // Only reached after both installed export guards are independently verified.
        using var process = Process.Start(new ProcessStartInfo("https://example.invalid/synthetic-proof") { UseShellExecute = true });
        Check(process == null && outerExCalls == 2 && exGuardCalls == 3, "Process.Start accepts handled launch without process");
        Check(bypassSeen, "AsyncLocal bypass flows to shell launch thread");
        Bypass.Value = 0;
        topEx.Disable(); topW.Disable();
        Check(invokeEx(&info) == 1 && outerExCalls == 2 && exGuardCalls == 4, "Ex disable preserves lower hook");
        Check(invokeW(0,0,0,0,0,1) == 33 && outerWCalls == 1 && wGuardCalls == 3, "W disable preserves lower hook");
        topEx.Enable(); topW.Enable();
        Check(invokeEx(&info) == 1 && outerExCalls == 3 && exGuardCalls == 5, "Ex reenable");
        Check(invokeW(0,0,0,0,0,1) == 33 && outerWCalls == 2 && wGuardCalls == 4, "W reenable");
        topEx.Disable(); topW.Disable();
        // Dalamud's Reloaded backend Dispose disables its hook; trampolines stay allocated.
        ProductionChecks(invokeEx, invokeW, () => exGuardCalls, () => wGuardCalls);
        GC.KeepAlive(bottomEx); GC.KeepAlive(bottomW); GC.KeepAlive(topEx); GC.KeepAlive(topW);
        Console.WriteLine($"{checks} synthetic shell hook checks passed; no external launch.");
    }

    private static void ProductionChecks(ExecuteEx invokeEx, Execute invokeW, Func<int> exGuardCount, Func<int> wGuardCount)
    {
        var accepted = 0; var accept = true; var shouldThrow = false;
        var provider = new Dalamud.Plugin.Services.TestInterop();
        using var capture = new OmniKit.Integration.WebLinkInterceptor(provider, _ =>
        {
            if (shouldThrow) throw new IOException("Synthetic failure");
            if (accept) accepted++;
            return accept;
        });
        Check(!capture.IsEnabled && provider.Created == 0, "Default off does not install hooks");
        Check(capture.SetEnabled(true) && capture.IsEnabled && provider.Created == 2, "Explicit enable installs both hooks");
        var file = Marshal.StringToHGlobalUni("https://example.invalid/wiki/Test#Heading");
        var verb = Marshal.StringToHGlobalUni("open");
        try
        {
            var info = new Info { Size = (uint)sizeof(Info), File = file, Verb = verb, Show = 1, Mask = 0x540 };
            var beforeEx = exGuardCount(); var beforeW = wGuardCount();
            Check(invokeEx(&info) == 1 && info.Instance == 33 && info.Process == 0 && accepted == 1 && exGuardCount() == beforeEx, "Capture Ex without downstream launch");
            Check(invokeW(0,verb,file,0,0,1) == 33 && accepted == 2 && wGuardCount() == beforeW, "Capture W without downstream launch");
            using (var process = Process.Start(new ProcessStartInfo("https://example.invalid/wiki/Test") { UseShellExecute = true }))
                Check(process == null && accepted == 3 && exGuardCount() == beforeEx, "Production Process.Start captured");
            using (OmniKit.Integration.WebLinkInterceptor.Bypass())
            {
                using var process = Process.Start(new ProcessStartInfo("https://example.invalid/wiki/Test") { UseShellExecute = true });
                Check(process == null && accepted == 3 && exGuardCount() == beforeEx + 1, "Explicit auth bypass flows through shell thread");
                using (OmniKit.Integration.WebLinkInterceptor.Bypass())
                    Check(invokeW(0,verb,file,0,0,1) == 33 && accepted == 3, "Nested bypass");
                Check(invokeW(0,verb,file,0,0,1) == 33 && accepted == 3, "Outer bypass survives inner disposal");
            }
            Check(invokeW(0,verb,file,0,0,1) == 33 && accepted == 4, "Scope restores capture");
            accept = false; beforeEx = exGuardCount();
            Check(invokeEx(&info) == 1 && accepted == 4 && exGuardCount() == beforeEx + 1, "Full/unavailable queue falls through");
            shouldThrow = true; beforeW = wGuardCount();
            Check(invokeW(0,verb,file,0,0,1) == 33 && wGuardCount() == beforeW + 1 && capture.FailureCount == 1, "Queue exception falls through");
            shouldThrow = false; accept = true;
            info.Mask |= 0x8000000; beforeEx = exGuardCount();
            Check(invokeEx(&info) == 1 && exGuardCount() == beforeEx + 1 && accepted == 4, "Special shell flags preserve original");
            info.Mask = 0; info.Size = 1; beforeEx = exGuardCount();
            Check(invokeEx(&info) == 1 && exGuardCount() == beforeEx + 1 && accepted == 4, "Unknown structure preserves original");
            info.Size = (uint)sizeof(Info); info.IdList = 1; beforeEx = exGuardCount();
            Check(invokeEx(&info) == 1 && exGuardCount() == beforeEx + 1 && accepted == 4, "ID list preserves original");
            info.IdList = 0; beforeW = wGuardCount();
            Check(invokeW(0,verb,file,0,0,0) == 33 && wGuardCount() == beforeW + 1 && accepted == 4, "Hidden launch preserves original");
            foreach (var otherVerb in new[] { "print", "runas", "edit" })
            {
                var other = Marshal.StringToHGlobalUni(otherVerb);
                try { beforeW = wGuardCount(); Check(invokeW(0,other,file,0,0,1) == 33 && wGuardCount() == beforeW + 1 && accepted == 4, "Non-open verb preserved"); }
                finally { Marshal.FreeHGlobal(other); }
            }
            beforeW = wGuardCount();
            Check(invokeW(0,verb,file,verb,0,1) == 33 && wGuardCount() == beforeW + 1, "Parameters preserved");
            Check(capture.SetEnabled(false) && !capture.IsEnabled, "Disable");
            beforeEx = exGuardCount();
            Check(invokeEx(&info) == 1 && exGuardCount() == beforeEx + 1 && accepted == 4, "Disable preserves downstream behavior");
            Check(capture.SetEnabled(true) && provider.Created == 2, "Reenable reuses hooks");
            capture.Dispose(); capture.Dispose(); beforeEx = exGuardCount();
            Check(invokeEx(&info) == 1 && exGuardCount() == beforeEx + 1 && accepted == 4, "Disposal preserves downstream behavior");
            Check(!capture.SetEnabled(true), "Disposed cannot reenable");
        }
        finally { Marshal.FreeHGlobal(file); Marshal.FreeHGlobal(verb); }
        using var failed = new OmniKit.Integration.WebLinkInterceptor(new Dalamud.Plugin.Services.TestInterop { FailCreation = true }, _ => true);
        Check(!failed.SetEnabled(true) && !failed.IsEnabled && failed.FailureCount == 1, "Creation failure leaves capture off");
        foreach (var good in new[] { "https://example.invalid/wiki/Title", "HTTP://example.invalid/", "https://example.invalid/wiki/雪?search=NPC#Lore" })
            Check(OmniKit.Integration.WebLinkInterceptor.TryWebDestination(good, out var clean) && clean.Length != 0, "Ordinary website accepted");
        foreach (var bad in new[] { "file:///test", "mailto:test@example.invalid", "steam://open", "javascript:alert(1)", "https://example.invalid/\n", "https://user:password@example.invalid/", "http://localhost/", "http://127.0.0.1/", "http://[::1]/", "https://auth.example.invalid/", "https://example.invalid/oauth/authorize", "https://example.invalid/login", "https://example.invalid/%6cogin", "https://example.invalid/?client_id=synthetic", "https://example.invalid/#access_token=synthetic", "https://example.invalid/?%63ode=synthetic", "https://example.invalid/?STATE=synthetic", "https://example.invalid/?SAMLResponse=synthetic", new string('x',4097) })
            Check(!OmniKit.Integration.WebLinkInterceptor.TryWebDestination(bad, out var clean) && clean.Length == 0, "Nonweb or authentication destination bypassed");
    }
}
