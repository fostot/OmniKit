# Companion home and nap checks

The cosmetic rotation helper can be checked independently with `dotnet run --project tests/CompanionHomeTests -c Release -- --rotation-only`. Synthetic clocks cover deferred changes during carry/nap/preview, manual selection and interval resets, disabled restoration, bounded intervals, clock rollback, no catch-up bursts and all eight choices. It never writes preferences or calls the game.

Run from the repository root:

```powershell
dotnet run --project tests/CompanionHomeTests -c Release
```

Use `-- --logic-only` to skip native rendering. The harness links production house geometry, nap state machine, obstacle routing, roaming, nap metadata reader and home painter. It uses synthetic viewport coordinates and the checked-in artwork; it never starts a game, loads a user profile, queries a provider or persists plugin settings.

Bounded cases cover house placement at every screen edge, independent roaming bounds, house collision avoidance, faster nap travel, the complete entrance/sleep/return cycle, pause and cancellation without teleporting, and all eight companions at 96/192 pixels in both directions. Native ImGui renders the actual textures through `CompanionHomeRenderer`: 128 nap samples plus sleep-symbol vertices at four viewport corners. It also checks every active companion's complete vector fallback stays visible and bounded. The shared software renderer writes five diagnostic images under ignored `.local/visualharness/companion-home`.

These checks establish isolated geometry, state-transition and rendering evidence. They do not establish live FFXIV behavior or the appearance of motion in the game renderer.

## Styled homes and delegated travel

```powershell
dotnet run --project tests/CompanionHomeTests -c Release -- --styles-only --logic-only
dotnet run --project tests/CompanionHomeTests -c Release -- --styles-only
```

The focused branch passes a measured `CompanionHomeStyleFrame` into the production
nap routine. It checks every home, both facings and 96/192 px companion sizes.
The travel callback owns approach, foreground crossing and return; it must never
run during doorway entry, settling, sleep or waking. Synthetic blocked, invalid,
stationary and never-arriving routes end at their actual finite position without
silently switching back to the old free-space walker. Travel pose propagates to
the caller. The watchdog allows eight seconds without movement and 120 seconds
of total delegated travel, excluding sleep and paused time.

The render branch calls the actual `CompanionHomeRenderer` with all twelve new
homes at 96/192 px house sizes and both facings. Its 192 samples show early/late
entry, sleep and exit, with the original nap artwork, measured doorway wall masks
and chimney anchors. Four sheets are written under ignored
`.local/visualharness/companion-home/styles`. Logical viewports leave room for the
full companion on either side of a home before its nap art shrinks; clipped
diagnostic tiles are not used as physical game boundaries.

The optional navigator used here is synthetic. Real support planes, hop routing
and physics adoption belong to the runtime integration and its separate tests.
Passing no style and no callback retains the prior default-house path, exercised
by the existing `--logic-only` checks.
