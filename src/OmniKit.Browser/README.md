# OmniKit browser helper

A separate .NET 10 Windows Forms process hosts WebView2 inside a native child window. It has no Dalamud references and shares no game CLR. The x64 helper uses the installed WebView2 Runtime and the pinned 1.0.3719.77 SDK.

Start with `--parent-window <decimal HWND> --parent-pid <process ID> --profile <dedicated absolute directory>`. The window must belong to the supplied process. An empty profile directory receives an OmniKit ownership marker; a nonempty unmarked directory is rejected. Existing Chrome or Edge profiles are never imported. Standard input is the only command channel. No listening network endpoint or page-to-native message bridge is created.

Send newline-delimited JSON, at most 8192 characters per line:

```json
{"kind":"navigate","url":"https://ffxiv.consolegameswiki.com/wiki/FF14_Wiki"}
{"kind":"toggle-controls"}
{"kind":"show"}
{"kind":"hide"}
{"kind":"shutdown"}
```

Standard output contains JSON events with `kind` and a fixed safe `message`. Event kinds are `ready`, `loading`, `navigated`, `shown`, `hidden`, `error`, `closing` and `closed`. `ready` additionally carries the numeric child `window` handle. Events never contain URLs, page titles, profile paths, raw exceptions or browsing data. EOF, host-window destruction or parent-process exit closes the helper.

The default surface is 760 × 500 with a controls icon and a persistent × close button. Opening controls reveals navigation, address entry, home, extensions, protection, a draggable heading and a resize grip. Narrow layouts put trailing toolbar actions in the native overflow menu. The surface stays within its parent client rectangle. A smaller parent can constrain its usual 360 × 240 minimum. Top-level navigation accepts HTTP/HTTPS only; subframes also permit internal blank/srcdoc pages and HTTP-origin blob documents. External protocols, downloads, developer tools, native host objects, web messages, automatic password saving/autofill and permission requests are disabled.

Close, `hide`, and `shutdown` all dispose WebView2 and exit the helper. The helper waits for its captured browser process to exit, then terminates that owned process tree if needed. The plugin bounds shutdown separately, discards queued navigation, and reclaims late startup results. It waits for old shutdown before a new explicit open reuses the profile. There is no browser preload or hidden idle browser: enabling link capture changes the hook only, and an accepted web link starts the browser on demand. The helper's temporary hidden state during teardown is not a retained browsing session.

Both layers perform a bounded exit wait after requesting forced termination; returning from `Kill` alone never establishes profile release. The helper returns exit code 73 if its owned WebView2 exit cannot be confirmed. The facade blocks reopening after an unconfirmed helper exit or any nonzero exit, including abnormal termination, because it cannot confirm descendant cleanup. It discards pending navigation and asks the user to restart OmniKit; the same plugin instance never clears this failure simply because another open or hide request arrives.

Protection is on for every new helper session. An embedded, manually maintained list of 60 advertising and tracking hostnames matches exact names and their subdomains through WebView2's [request interception API](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/webresourcerequested), including requests from child frames and workers. Blocked requests receive an empty local response. The list in `blocked-hosts.txt` is original OmniKit source covered by this repository's MIT license; no external filter list or extension was copied. Rules are updated with the plugin, without a filter download service. This is bounded host blocking, not a complete ad blocker: first-party ads, new hosts and cosmetic page elements can remain, and some site features may break. The toolbar's Protection control disables both network and popup protection for the current session and reloads the page.

With protection on, a document-start script suppresses JavaScript `window.open`, while native non-user-initiated new-window requests are cancelled. A user-clicked HTTP(S) link that requests another window opens in the same browser after its new-window event completes. No separate native popup or external browser is launched. Sites that depend on scripted windows may need the session protection toggle. Disabling protection permits those requests to navigate in the same view; it still does not create separate windows or allow external protocols.

Extensions are explicitly selected unpacked folders. The manager uses WebView2's [profile extension APIs](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2profile) to add and list extensions, and [extension APIs](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2browserextension) to enable, disable and remove them. The runtime may list its own built-in extensions. Web Store installation and universal Chrome-extension compatibility are not promised. Extensions can read or change pages in this dedicated browser, as disclosed before folder selection.

The maintained checks are `tests/BrowserFacadeTests`, `tests/BrowserProtectionTests` and `tests/BrowserBehaviorSmoke`. The last exercises the actual production WinForms/WebView2 control, local request interception, trusted same-view link input, popup suppression, session opt-out, persistent close and real helper/child-process teardown. Its WinForms controls and WebView2 captures are composited at their actual bounds for 760 × 500 and 360 × 240 review, including narrow toolbar overflow. Prior local extension checks exercised installation, content scripts, listing, enabling, disabling and removal using an unpacked MV3 fixture.

The hover probe found overlapping native website/chrome paint regions in the prior implementation, but did not reproduce the reported whole-page flicker. The website HWND now excludes the floating buttons' rectangles, and synthetic hover leaves its size and page unchanged. This is evidence for the native paint-boundary fix, not confirmation that FFXIV's reported flicker is resolved. Game focus, mixed DPI, GPU composition and exclusive fullscreen still require live-game validation. Windowed or borderless mode is the intended native-child integration path.

Package the helper executable, DLL, `.deps.json`, `.runtimeconfig.json`, `.build.json`, both WebView2 managed DLLs and x64 `WebView2Loader.dll`. The SDK XML documentation is not required at runtime. Version and author metadata come from the repository's shared build targets.
