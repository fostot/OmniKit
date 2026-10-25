# Settings UI rendering check

This executable renders the actual compiled `PluginUi.DrawSettings` and its production theme through the installed Dalamud ImGui binding and native `cimgui`. It reuses the small software rasterizer in `LauncherUiSmoke`; screenshots are actual ImGui geometry, not an HTML recreation.

Build the core first, then run from the repository root:

```powershell
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release
```

The first two optional positional arguments are the core DLL and screenshot directory. Defaults are `src/OmniKit/bin/Release/OmniKit.dll` and ignored `.local/visualharness/settings`. `DalamudLibPath` selects the binding libraries at build time; `DALAMUD_LIB_PATH` selects host dependency resolution at runtime when using a nonstandard installation. No machine-specific absolute path is committed.

The harness bypasses the plugin UI constructor and injects in-memory preferences and the minimum inert objects needed for drawing. It does not initialize the plugin, load a game context, start voice recognition, issue a network request, launch a CLI or sign in. AI credential status checks use a unique, empty path underneath the screenshot directory; no real credential store is read. Account/model/status displays use synthetic in-memory fixtures, and the settings persistence callback does nothing. It never saves preferences, opens a file dialog, or reads the user's plugin configuration. Locally installed Segoe UI is used without copying or redistributing the font.

The default render set covers the native Launcher, Interact, HUD, Companion Appearance/Voice/AI, Data and About tabs plus companion conversation and first-run setup windows at their minimum logical size of 480×330, scaled to 1, 1.5 and 2. Conversation windows use Moonblob so the real header renders without loading a texture service. Long forms also render after scrolling to the bottom. The screenshot viewport is slightly larger than the window. Assertions check viewport gutters; inspect images for clipping, overlap, control alignment and footer separation. Expanded game-dependent selectors and plugin callbacks are outside this isolated test.

Native click checks use the actual rendered control rectangles reported by the UI's optional, normally unset measurement callback. This keeps clicks tied to labels as spacing changes. Additional cases reveal each help/disclosure section and its scrolled bottom, the eleven-effect HUD menu at both ends, effect intensity/animation/speed controls, meaningful error status, and the centered import confirmation. The modal's viewport-wide dim backdrop is intentional; its two buttons are checked for visible gutters. Browser status is synthetic and neither browser button is pressed. Home/personality cases cover the expanded house/comment controls and Wink's blink controls, including disabled timing states. No roaming or animation service is started.

Synthetic browser status transitions also verify that a completed request replaces its opening message, a delayed failure reveals the browser section without replacing unrelated status, and dismissing that failure does not repeatedly reopen settings. The failure section itself is rendered at each scale. No browser helper is launched.

Browser link-capture cases expand the browser section with capture enabled, disabled, failed, and a deliberately long synthetic status. A test-only status callback is queried only while the preference is enabled. The bottom view checks that both browser buttons and the following setup button remain reachable; the long status is hovered to reveal its complete wrapped tooltip. These cases use no interception hook or real browser operation.

Focused checks avoid repeating the full set:

```powershell
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=ai-on,hud --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=about --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=interact,interact-types,interact-enemy-choose,interact-enemy-target,interact-enemy-attack,interact-none --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=frame-on,frame-static --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=companion-navigation --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=ai-off,ai-on,ai-chatgpt,ai-chatgpt-connected,ai-chatgpt-middle,ai-codex,ai-claude,ai-cli-busy,ai-cli-error --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=ai-provider-menu,ai-account-menu,ai-model-menu,ai-account-tooltip,ai-model-tooltip,ai-endpoint,ai-cli-cancel,ai-provider-switch --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=ai-model-discovery,ai-model-manual,ai-model-empty,ai-model-failure --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=ai-codex,ai-claude,conversation-ai,conversation-codex,conversation-claude,conversation-busy
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=companion-menu,palette-menu,pose-menu,hud-outline --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=hud-effects,hud-effects-static,hud-effect-menu,hud-effect-bottom-menu,data-confirm,data-browser
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=data-browser,data-browser-error,data-browser-capture,data-browser-capture-off,data-browser-capture-error,data-browser-capture-long
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=home,home-off,home-wink,home-wink-off
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=interact-quest-on,home-only
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --measure-hotkeys
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --companions
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --companion-poses
```

The measurement check compares the longest capture row and instruction against the minimum content width with a scrollbar. It does not activate key capture or poll physical keyboard input. Software rendering and the harness font can differ from the game renderer. These checks establish isolated layout evidence, not live Dalamud plugin loading, gameplay or backend behavior. Host DLLs copied into ignored build output are for local execution only and must not be redistributed.

The companion check renders the actual vector fallback helper for Moonblob, Wink and Nova in all six palettes. The pose variant renders Carry, Fall, Land and Push. Both compare static screenshots at two different times to verify that disabling animation freezes the visual, check sprite geometry bounds and verify that disabled magic emits no draw geometry. Neither loads any artwork texture nor starts the roaming service.

The four menu cases open the native Companion, Outfit, Aura / shape color and Preview animation dropdowns, scrolling to reach the latter controls. They assert that a popup opened, keep the preference values unchanged and retain the viewport gutter check. Inspect each screenshot to confirm the correct menu and readable labels and color swatches. The menus may extend beyond the settings window into the viewport, as native ImGui popups normally do. No selection or plugin action is invoked.

Interaction cases cover the expanded world category list, all categories disabled, enemy selection awaiting a choice, and both selected enemy actions. These are in-memory configuration states rendered through the real UI; no game target is selected and no attack is started.

The quest-item case enables only the preference in memory; it never uses an item. The home-only case disables frame interaction while revealing Home & personality, checking that the shared grab-hand option remains reachable for users who only enable the house.

Frame interaction cases show the master setting and each sub-option, with animations both enabled and disabled. They validate the settings layout only; pickup, perching, window containment and falling require separate runtime validation.

The companion navigation case uses real ImGui mouse events to click AI → Appearance → Voice → AI with AI enabled, checking the active tab after each transition. It also checks programmatic Appearance and AI selection. This catches a later tab reasserting its selection during the same draw loop; it does not invoke provider, microphone or game actions.

AI cases cover API/local-server settings (including the expanded endpoint field), disconnected and connected ChatGPT states, deliberately long synthetic account and model labels, their native dropdowns and wrapped tooltips, both CLI providers, and pending/error status. The cancel case clicks the real Cancel connection button against an in-memory pending task and verifies cancellation and task cleanup. The provider-switch case clicks Codex → Claude → ChatGPT → API, asserting that each switch disables AI questions and preserves all four independent model preferences. It writes a screenshot after each transition. These checks do not establish successful real sign-in, billing eligibility, provider inference, CLI isolation or token storage; those have separate tests and live validation boundaries.

The paused CLI and busy conversation cases attempt a native click on the disabled Ask button with a nonempty synthetic question. They verify that no answer task starts and Search wiki remains visible. The fixed CLI pause notice and send tooltip are rendered at all three scales; the separate `ChatCostGuardTests` harness verifies the service boundary rather than relying on UI gating alone.

Model-refresh cases pass completed synthetic tasks through the real UI update
method. They verify selection of a returned lightweight default, preservation of
a valid manual model, a successful empty catalog, and a failed refresh retaining
the earlier choices. Error text comes from the sanitized connection status; the
fixture exception's untrusted text must never appear. The resulting form and
wrapped status are rendered at minimum size, with no network or authentication.

The focused `--case=wardrobe-costume-menu,wardrobe-avatar-menu,wardrobe-nib` set checks the six painted-costume choices and all thirteen companion choices at 480×330 logical pixels and scales 1, 1.5 and 2. Every dropdown option must become fully readable while scrolling, retain the native screen margin, and save the selected synthetic value. Nib's expanded sections omit unavailable everyday and holiday-body selectors. A separate invocation of the actual companion Header verifies one complete Nib portrait quad from the public wardrobe PNG, using a texture stub and rejecting other service access. These cases write nine settings previews and three portrait previews under the ignored output directory.

Companion appearance now uses one visible section at a time: Look, Scene, Motion, Home and Seasonal. Run `dotnet run --project tests/SettingsUiSmoke -c Release -- --scene13` for the focused section states at the minimum 480x330 logical window, including timer rotation, Orbit rings, hand visibility, Wink blink, animation disabled, expanded frame help, holiday bodies and quiet home routines. Each state renders top and bottom at 1, 1.5 and 2 scale. Scene sizes use synthetic in-memory preferences; no live settings are read or written.
