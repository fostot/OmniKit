# Launcher UI rendering check

This executable links the production `CompactLauncher.cs` and `SearchEngine.cs` and uses the locally installed Dalamud ImGui binding and native `cimgui` library. It creates an isolated ImGui context and renders its actual draw lists to PNG with a small software renderer. It opens no window, loads no game services, captures no microphone input and sends no network requests.

Run from the repository root:

```powershell
dotnet run --project tests/LauncherUiSmoke/LauncherUiSmoke.csproj -c Release
```

`DalamudLibPath` can be supplied as an MSBuild property when the development libraries are elsewhere. Host libraries are copied into the ignored test output solely to run locally; do not package or redistribute those copies.

Outputs go to ignored `.local/visualharness/launcher` by default. An optional first application argument selects another output directory. The harness uses locally installed Segoe UI at 17 pixels scaled with the UI, or the bundled ImGui default font if Segoe UI is unavailable. Font files are neither copied into the repository nor redistributed.

The 42 PNGs cover blank input, whitespace, community results, long titles with scrolling, no-match fallback, clearing a populated query and an expanded status message, each at scales 1, 1.5 and 2 on 1280×720 and 640×480 viewports. Automated checks verify a viewport gutter, visible result expansion and equal collapsed heights for blank/whitespace/cleared inputs. Inspect the PNGs for text truncation, overlap and visual alignment; the automated checks do not replace that review.

A focused keyboard check also verifies that Down then Enter activates the second current result while the search field has focus. Pass `--keyboard` after `--` to run only that check, or `--tooltips` to rerender only the two narrow, enlarged tooltip cases and their collapsed baselines.

The binding's native initializer needs an on-disk calling assembly location. Running it through a dynamic reflection host can fail before creating a context. This ordinary executable places `cimgui.dll` beside its assemblies, matching the upstream standalone testbed.

The PNGs verify layout and clipping of rendered ImGui geometry. Software rasterization and the harness font can differ from the game's renderer. Screenshots do not establish live gameplay, input routing, plugin loading or graphics-driver compatibility. Unsupported custom textures or draw callbacks fail explicitly.
