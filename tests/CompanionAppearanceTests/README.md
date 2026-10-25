# Appearance selection and signature effects

Run from the repository root:

```powershell
dotnet run --project tests/CompanionAppearanceTests/CompanionAppearanceTests.csproj -c Release --no-restore
```

Source-linked selection checks cover immediate manual choices during sleep/carry, independent named holiday bodies, preservation of everyday looks, and deferred automatic rotation/event changes. The native ImGui harness checks all eight companions with nine holiday motifs at96/192 in both facings, bounded geometry, deterministic animation-off output, magic-off and editor suppression, and actual-texture composites.

Screenshots go to ignored `.local/companion-appearance-tests/`. These are offscreen renderer checks, not live FFXIV validation. No game updates, network requests, credentials, or user profiles are used.

For the isolated automatic-swap poof checks:

```powershell
dotnet run --project tests/CompanionAppearanceTests/CompanionAppearanceTests.csproj -c Release --no-restore -- --poof-only
```

These check one captured-avatar handoff at full coverage, retained unsafe requests, interruption/reset/disable, bounded timing, all-frame geometry at96/192 with tall hats/wide bounds, and pixel comparisons proving opaque coverage over different synthetic body colors. The poof painter owns no movement, configuration, or game actions.
