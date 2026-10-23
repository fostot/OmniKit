# Companion behavior checks

After building the core plugin, run:

`dotnet run --project tests/CompanionBehaviorTests/CompanionBehaviorTests.csproj -c Release`

This creates an isolated native ImGui context with synthetic logged-out services and texture handles. It does not open FFXIV, access a game process, use real configuration, or contact a network service. Checks exercise the real frame reader, comment placement, house mouse dragging and one-save-on-release behavior, inward house UVs, hand pose selection/clipping, and Wink's independent eyelid geometry.

Comment screenshots and native triangle snapshots go to the ignored `.local/visualharness/companion-behavior` directory. With Python, Pillow, and NumPy available, `python tests/CompanionBehaviorTests/render-preview.py` renders those triangles against the unchanged approved PNGs for inspection. The script writes diagnostic images only; it never modifies source assets.

For the optional Fourier blink, pass `--fourier-blink-only` after the plugin/output arguments. This renders the real eyelid painter over the unchanged Wink frame-0 texture at 96/192px, both facings, and idle/floating phases. It checks unchanged body quads and hit bounds, finite contained lid geometry, disabled behavior, and the unchanged default blink. The contact sheets cover closing, the full-closure hold, and reopening. Pure cached-curve tests run with `dotnet run --project tests/CompanionMotionTests -c Release -- --blink-only`.

Run `tests/CompanionPhysicsTests` separately for pure drop/perch/containment, screen-bottom falling, continuous return, and home-area bounds. These are offscreen checks, not an in-game acceptance test.
