# HUD effect checks

Run `dotnet run --project tests/HudEffectTests/HudEffectTests.csproj -c Release` with the installed Dalamud development libraries in the usual XIVLauncher development directory. For another location, pass `-p:DalamudLibPath=<directory>`.

This standalone harness renders native ImGui font-atlas draw lists in memory. It does not start FFXIV or read game state, settings, accounts, credentials, or microphone input. Synthetic contact sheets are written to the ignored `.local/visualharness/hud-effects` directory, or the explicit first argument.

Checks cover native Unicode/kerning measurement, scalar-safe elision, fixed effect bounds at three font sizes and intensity levels, independent outlines, deterministic animation-off behavior, zero-intensity behavior, actual animated vertex/color changes, and a bounded draw-pass budget. Eleven styles are rendered at several sample times for visual review. The unchanged opaque text core remains readable beneath the effect-specific halo, split-color echo, moving scan band, rising flame geometry, ice sparkles, or moving light.

This is offscreen rendering evidence, not an in-game test.

For the focused adjustable-outline checks, run `dotnet run --project tests/HudEffectTests -c Release -- .local/visualharness/hud-outline --outline-only`. This checks 0.5–4 thickness, exact native prefix/name/suffix glyph alignment, a stable opaque foreground under every effect, independent toggling, finite fallback, frozen effects, screen-corner bounds and the 32-pass maximum. The contact sheets compare four outline widths at two animation times. Their renderer enables vertex-offset support because a comparison sheet contains many complete HUDs.
