# Launcher UI rendering check

This executable links the production `CompactLauncher.cs`, `LauncherAppearanceUi.cs`, `UiText.cs` and `SearchEngine.cs` and uses the locally installed Dalamud ImGui binding and native `cimgui` library. A minimal five-field Configuration fixture replaces persisted settings for the appearance controls. It creates an isolated ImGui context and renders its actual draw lists to PNG with a small software renderer. It opens no window, loads no game services, captures no microphone input and sends no network requests.

Run from the repository root:

```powershell
dotnet run --project tests/LauncherUiSmoke/LauncherUiSmoke.csproj -c Release
```

`DalamudLibPath` can be supplied as an MSBuild property when the development libraries are elsewhere. Host libraries are copied into the ignored test output solely to run locally; do not package or redistribute those copies.

Outputs go to ignored `.local/visualharness/launcher` by default. An optional first application argument selects another output directory. The harness uses locally installed Segoe UI at 17 pixels scaled with the UI, or the bundled ImGui default font if Segoe UI is unavailable. Font files are neither copied into the repository nor redistributed.

The 42 baseline PNGs cover blank input, whitespace, community results, long titles with scrolling, no-match fallback, clearing a populated query and an expanded status message, each at scales 1, 1.5 and 2 on 1280×720 and 640×480 viewports. The 78 appearance PNGs cover minimum/maximum width, text scale, visible row count and edge positions on 1280×720, 320×360 and 480×330 viewports, plus actual appearance controls, all three revealed hints and Reset behavior at all three scales. Automated checks verify viewport gutters, outer-window clipping, visible result expansion and equal collapsed heights for blank/whitespace/cleared inputs. Short viewports retain a complete single-line result; its description remains available in the existing tooltip. Inspect the PNGs for text truncation, overlap and visual alignment; the automated checks do not replace that review.

Keyboard checks verify that Down then Enter activates the second current result and that a three-row launcher can scroll to and activate result sixteen. Reset uses real ImGui mouse input and must restore all five defaults with one save callback. Pass `--keyboard` after `--` for keyboard checks only, `--baseline` for the original layouts, `--appearance` for the new options and controls, or `--tooltips` for the two narrow, enlarged tooltip cases and their collapsed baselines.

The binding's native initializer needs an on-disk calling assembly location. Running it through a dynamic reflection host can fail before creating a context. This ordinary executable places `cimgui.dll` beside its assemblies, matching the upstream standalone testbed.

The PNGs verify layout and clipping of rendered ImGui geometry. Software rasterization and the harness font can differ from the game's renderer. Screenshots do not establish live gameplay, input routing, plugin loading or graphics-driver compatibility. Unsupported custom textures or draw callbacks fail explicitly.
