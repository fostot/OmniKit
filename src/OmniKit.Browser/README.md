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

Standard output contains JSON events with `kind` and a fixed safe `message`. Event kinds are `ready`, `loading`, `navigated`, `shown`, `hidden`, `error` and `closed`. `ready` additionally carries the numeric child `window` handle. Events never contain URLs, page titles, profile paths, raw exceptions or browsing data. EOF, host-window destruction or parent-process exit closes the helper.

The default surface is 760 × 500 with a single controls icon. Opening controls reveals navigation, address entry, home, extensions, a draggable heading, hide button and resize grip. The surface stays within its parent client rectangle. A smaller parent can constrain its usual 360 × 240 minimum. Websites and new-window links navigate in the same view. Top-level navigation accepts HTTP/HTTPS only; subframes also permit internal blank/srcdoc pages and HTTP-origin blob documents. External protocols, downloads, developer tools, native host objects, web messages, automatic password saving/autofill and permission requests are disabled.

Extensions are explicitly selected unpacked folders. The manager uses WebView2's [profile extension APIs](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2profile) to add and list extensions, and [extension APIs](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2browserextension) to enable, disable and remove them. The runtime may list its own built-in extensions. Web Store installation and universal Chrome-extension compatibility are not promised. Extensions can read or change pages in this dedicated browser, as disclosed before folder selection.

The synthetic smoke tests under ignored `.local` exercise a real cross-process native child, local HTTP page loading, controls, hide/show, parent resizing, rejected navigation, sanitized output and EOF shutdown. A separate production-control smoke installs a local MV3 fixture, verifies its content script, and tests manager listing, disabling, enabling and removal. WinForms controls and WebView2 capture previews are composited at their actual bounds for 760 × 500 and 360 × 240 layout review. These checks do not establish behavior inside FFXIV: game focus, mixed DPI and exclusive fullscreen remain live-game validation items. Windowed or borderless mode is the intended native-child integration path.

Package the helper executable, DLL, `.deps.json`, `.runtimeconfig.json`, `.build.json`, both WebView2 managed DLLs and x64 `WebView2Loader.dll`. The SDK XML documentation is not required at runtime. Version and author metadata come from the repository's shared build targets.
