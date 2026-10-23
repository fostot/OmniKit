# Standard web link capture checks

Run on Windows x64 with the installed Dalamud API 15 libraries:

```powershell
dotnet run --project tests/WebLinkTests/WebLinkTests.csproj -c Release
```

This is a separate synthetic process. Bottom guards intercept **all** calls to that process's `ShellExecuteW` and `ShellExecuteExW`; they never call Windows' original functions. Each guard is verified before the first `Process.Start` test. No browser, real website, game process, account, or credential store is used. The guards remain installed until the test process exits.

The tests compile the production interceptor unchanged, adapting only the Dalamud service boundary to the installed Reloaded hook backend. They cover the 112-byte Windows x64 structure, native success results, .NET `UseShellExecute` behavior with no process handle, chained hooks, enable/disable/dispose, explicit authentication bypass across .NET's shell thread, queue refusal/errors, and conservative exclusions. The adapter mirrors Dalamud's documented Reloaded disable-on-dispose behavior; this is not a live test of the Dalamud service or arbitrary third-party hooks.

Integration requires an opt-in setting (default off), a bounded memory queue, and a callback that returns false if the queue or embedded browser is unavailable. Drain that queue on the normal UI/framework thread. Call `SetEnabled` on a setting transition, not in an automatic retry loop. Enclose explicit external sign-in launches in `using (WebLinkInterceptor.Bypass())`.

Only ordinary Unicode shell HTTP/HTTPS open/default launches are captured. Nonweb links, special shell verbs/options, hidden/minimized requests, loopback addresses, and recognized authentication paths/parameters pass through. Authentication recognition is deliberately conservative, not universal; the explicit bypass protects OmniKit's own authorization flow. Custom `CreateProcess` launches, ANSI shell APIs, and links opened by other processes are outside this feature. The interceptor stores counters and fixed status messages, never URL logs.

References: [ShellExecuteW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shellexecutew), [ShellExecuteExW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shellexecuteexw), [SHELLEXECUTEINFOW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ns-shellapi-shellexecuteinfow), and [Dalamud Reloaded hook lifecycle](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Hooking/Internal/ReloadedHook.cs).
