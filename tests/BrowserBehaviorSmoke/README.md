# Native browser behavior checks

Run from the repository root on Windows with the installed .NET 10 Desktop and WebView2 runtimes:

```powershell
dotnet run --project tests/BrowserBehaviorSmoke/BrowserBehaviorSmoke.csproj -c Release
dotnet run --project tests/BrowserBehaviorSmoke/BrowserBehaviorSmoke.csproj -c Release -- --lifecycle
```

The first command instantiates the actual production browser form in an offscreen synthetic native host. All test page requests, including deliberately named advertising hosts, are fulfilled in-process: the test never needs an ad network or a signed-in website. It checks four resource kinds (script, image, iframe and fetch), scripted popup suppression, a trusted DevTools input click on a new-window anchor, successful same-view navigation, protection opt-out, native window-region clipping and 20 hover enter/leave cycles. Six real control/page composites are written to ignored `.local/browser-behavior-renders` for review at 760 × 500 and 360 × 240, including error and overflow states. The persistent × closes the actual control and its captured WebView2 browser process.

`--lifecycle` starts the actual Release helper executable as a native child of the synthetic host. Three sequential launches reuse one newly created dedicated temporary profile and load only a loopback HTTP fixture. Each of `hide`, `shutdown` and stdin EOF must exit the helper, destroy its window and terminate every captured descendant process. The test holds process handles to avoid confusing a reused PID with the owned process, checks idle afterward, and emits no browsing URLs, profile paths or process details. The observed run captured 18 WebView2 child-process handles across its three launches. Existing browser profiles, game windows, credentials and account sessions are never used.

The earlier `--baseline` probe found overlapping menu/browser native paint regions, zero browser resize events and stable page content during hover. It did not reproduce the game's full-page flicker. The current probe verifies that native regions no longer overlap; live FFXIV hover/composition, focus, mixed DPI and exclusive fullscreen remain unverified.

Temporary synthetic profiles are intentionally distinct from the plugin profile. Test source links/references the production helper; no browser implementation is duplicated. `BrowserFacadeTests` separately covers bounded queue/startup cancellation, abandoned-start cleanup, closing during writes, no idle relaunch and immediate reopen waiting for the prior profile lock to release. `BrowserProtectionTests` checks domain boundaries and false positives without a browser or network.

The facade suite additionally delays completion after a successful kill request, verifies that an immediate reopen waits for actual clean exit, and makes exit ignore cancellation to prove the bounded failure path rejects profile reuse. Reserved helper exit code 73 and unexpected nonzero exit both prevent new launches. These are synthetic process-boundary checks, not a claim that a real WebView2 hang was reproduced.
