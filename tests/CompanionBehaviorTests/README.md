# Companion behavior checks

After building the core plugin, run:

`dotnet run --project tests/CompanionBehaviorTests/CompanionBehaviorTests.csproj -c Release`

This creates an isolated native ImGui context with synthetic logged-out services and texture handles. It does not open FFXIV, access a game process, use real configuration, or contact a network service. Checks exercise the real frame reader, comment placement, house mouse dragging and one-save-on-release behavior, inward house UVs, hand pose selection/clipping, and Wink's independent eyelid geometry.

Comment screenshots and native triangle snapshots go to the ignored `.local/visualharness/companion-behavior` directory. With Python, Pillow, and NumPy available, `python tests/CompanionBehaviorTests/render-preview.py` renders those triangles against the unchanged approved PNGs for inspection. The script writes diagnostic images only; it never modifies source assets.

Run `tests/CompanionPhysicsTests` separately for pure drop/perch/containment, screen-bottom falling, continuous return, and home-area bounds. These are offscreen checks, not an in-game acceptance test.
