# Companion home and nap checks

The cosmetic rotation helper can be checked independently with `dotnet run --project tests/CompanionHomeTests -c Release -- --rotation-only`. Synthetic clocks cover deferred changes during carry/nap/preview, manual selection and interval resets, disabled restoration, bounded intervals, clock rollback, no catch-up bursts and all eight choices. It never writes preferences or calls the game.

Run from the repository root:

```powershell
dotnet run --project tests/CompanionHomeTests -c Release
```

Use `-- --logic-only` to skip native rendering. The harness links production house geometry, nap state machine, obstacle routing, roaming, nap metadata reader and home painter. It uses synthetic viewport coordinates and the checked-in artwork; it never starts a game, loads a user profile, queries a provider or persists plugin settings.

Bounded cases cover house placement at every screen edge, independent roaming bounds, house collision avoidance, faster nap travel, the complete entrance/sleep/return cycle, pause and cancellation without teleporting, and all eight companions at 96/192 pixels in both directions. Native ImGui renders the actual textures through `CompanionHomeRenderer`: 128 nap samples plus sleep-symbol vertices at four viewport corners. It also checks every active companion's complete vector fallback stays visible and bounded. The shared software renderer writes five diagnostic images under ignored `.local/visualharness/companion-home`.

These checks establish isolated geometry, state-transition and rendering evidence. They do not establish live FFXIV behavior or the appearance of motion in the game renderer.
