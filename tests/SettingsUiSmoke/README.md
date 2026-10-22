# Settings UI rendering check

This executable renders the actual compiled `PluginUi.DrawSettings` and its production theme through the installed Dalamud ImGui binding and native `cimgui`. It reuses the small software rasterizer in `LauncherUiSmoke`; screenshots are actual ImGui geometry, not an HTML recreation.

Build the core first, then run from the repository root:

```powershell
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release
```

The first two optional positional arguments are the core DLL and screenshot directory. Defaults are `src/OmniKit/bin/Release/OmniKit.dll` and ignored `.local/visualharness/settings`. `DalamudLibPath` selects the binding libraries at build time; `DALAMUD_LIB_PATH` selects host dependency resolution at runtime when using a nonstandard installation. No machine-specific absolute path is committed.

The harness bypasses the plugin UI constructor and injects in-memory preferences and the minimum inert objects needed for drawing. It does not initialize the plugin, load a game context, start voice recognition, issue a network request, or press action buttons. AI credential status checks use a unique, empty path underneath the screenshot directory; no real credential store is read. It never saves preferences, opens a file dialog, or reads the user's plugin configuration. Locally installed Segoe UI is used without copying or redistributing the font.

The default render set covers the native Launcher, Interact, HUD, Companion Appearance/Voice/AI and Data tabs at their minimum logical size of 520×360, scaled to 1, 1.5 and 2. Long forms also render after scrolling to the bottom. The screenshot viewport is slightly larger than the window. Assertions check viewport gutters; inspect images for clipping, overlap, control alignment and footer separation. Expanded game-dependent selectors and plugin callbacks are outside this isolated test.

Focused checks avoid repeating the full set:

```powershell
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --case=ai-on,hud --scale=2
dotnet run --project tests/SettingsUiSmoke/SettingsUiSmoke.csproj -c Release -- --measure-hotkeys
```

The measurement check compares the longest capture row and instruction against the minimum content width with a scrollbar. It does not activate key capture or poll physical keyboard input. Software rendering and the harness font can differ from the game renderer. These checks establish isolated layout evidence, not live Dalamud plugin loading, gameplay or backend behavior. Host DLLs copied into ignored build output are for local execution only and must not be redistributed.
