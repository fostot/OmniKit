# Settings UI rendering check

This executable renders the actual compiled `PluginUi.DrawSettings` and its production theme through the installed Dalamud ImGui binding and native `cimgui`. It reuses the small software rasterizer in `LauncherUiSmoke`; screenshots are actual ImGui geometry, not an HTML recreation.

Build the core first, then run from the repository root:

```powershell
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release
```

The first two optional positional arguments are the core DLL and screenshot directory. Defaults are `src/OmniKit/bin/Release/OmniKit.dll` and ignored `.local/visualharness/settings`. `DalamudLibPath` selects the binding libraries at build time; `DALAMUD_LIB_PATH` selects host dependency resolution at runtime when using a nonstandard installation. No machine-specific absolute path is committed.

The harness bypasses the plugin UI constructor and injects in-memory preferences and the minimum inert objects needed for drawing. It does not initialize the plugin, load a game context, start voice recognition, issue a network request, launch a CLI or sign in. AI credential status checks use a unique, empty path underneath the screenshot directory; no real credential store is read. Account/model/status displays use synthetic in-memory fixtures, and the settings persistence callback does nothing. It never saves preferences, opens a file dialog, or reads the user's plugin configuration. Locally installed Segoe UI is used without copying or redistributing the font.

The default render set covers the native Launcher, Interact, HUD, Companion Appearance/Voice/AI, Data and About tabs plus companion conversation and first-run setup windows at their minimum logical size of 480×330, scaled to 1, 1.5 and 2. Conversation windows use Moonblob so the real header renders without loading a texture service. Long forms also render after scrolling to the bottom. The screenshot viewport is slightly larger than the window. Assertions check viewport gutters; inspect images for clipping, overlap, control alignment and footer separation. Expanded game-dependent selectors and plugin callbacks are outside this isolated test.

Focused checks avoid repeating the full set:

```powershell
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=ai-on,hud --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=about --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=interact,interact-types,interact-enemy-choose,interact-enemy-target,interact-enemy-attack,interact-none --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=frame-on,frame-static --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=companion-navigation --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=ai-off,ai-on,ai-chatgpt,ai-chatgpt-connected,ai-chatgpt-middle,ai-codex,ai-claude,ai-cli-busy,ai-cli-error --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=ai-provider-menu,ai-account-menu,ai-model-menu,ai-account-tooltip,ai-model-tooltip,ai-endpoint,ai-cli-cancel,ai-provider-switch --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=companion-menu,outfit-menu,palette-menu,pose-menu --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --measure-hotkeys
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --companions
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --companion-poses
```

The measurement check compares the longest capture row and instruction against the minimum content width with a scrollbar. It does not activate key capture or poll physical keyboard input. Software rendering and the harness font can differ from the game renderer. These checks establish isolated layout evidence, not live Dalamud plugin loading, gameplay or backend behavior. Host DLLs copied into ignored build output are for local execution only and must not be redistributed.

The companion check renders the actual vector fallback helper for Moonblob, Wink and Nova in all six palettes. The pose variant renders Carry, Fall, Land and Push. Both compare static screenshots at two different times to verify that disabling animation freezes the visual, check sprite geometry bounds and verify that disabled magic emits no draw geometry. Neither loads any artwork texture nor starts the roaming service.

The four menu cases open the native Companion, Outfit, Aura / shape color and Preview animation dropdowns, scrolling to reach the latter controls. They assert that a popup opened, keep the preference values unchanged and retain the viewport gutter check. Inspect each screenshot to confirm the correct menu and readable labels and color swatches. The menus may extend beyond the settings window into the viewport, as native ImGui popups normally do. No selection or plugin action is invoked.

Interaction cases cover the expanded world category list, all categories disabled, enemy selection awaiting a choice, and both selected enemy actions. These are in-memory configuration states rendered through the real UI; no game target is selected and no attack is started.

Frame interaction cases show the master setting and each sub-option, with animations both enabled and disabled. They validate the settings layout only; pickup, perching, window containment and falling require separate runtime validation.

The companion navigation case uses real ImGui mouse events to click AI → Appearance → Voice → AI with AI enabled, checking the active tab after each transition. It also checks programmatic Appearance and AI selection. This catches a later tab reasserting its selection during the same draw loop; it does not invoke provider, microphone or game actions.

AI cases cover API/local-server settings (including the expanded endpoint field), disconnected and connected ChatGPT states, deliberately long synthetic account and model labels, their native dropdowns and wrapped tooltips, both CLI providers, and pending/error status. The cancel case clicks the real Cancel connection button against an in-memory pending task and verifies cancellation and task cleanup. The provider-switch case clicks Codex → Claude → ChatGPT → API, asserting that each switch disables AI questions and preserves all four independent model preferences. It writes a screenshot after each transition. These checks do not establish successful real sign-in, billing eligibility, provider inference, CLI isolation or token storage; those have separate tests and live validation boundaries.
