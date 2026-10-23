# Optional holiday companion artwork

Run from the repository root:

```powershell
dotnet run --project tests/CompanionHolidayTests/CompanionHolidayTests.csproj -c Release --no-restore
```

The synthetic-only default verifies the complete 72-look/8-file contract, canonical lookup, missing-file fallback, PNG dimensions, malformed/invalid metadata, shared frame and eye validation, and source aspect/baseline at 96/192 pixels facing both ways. All fixtures are generated beneath ignored `.local/companion-holiday-tests/`; no personal configuration, game, network, or provider calls are used.

For the final reviewed PNGs and metadata:

```powershell
dotnet run --project tests/CompanionHolidayTests/CompanionHolidayTests.csproj -c Release --no-restore -- --art-dir assets
```

That adds native ImGui rendering of all 72 holiday looks and their eight source poses at 96/192 pixels. An optional repeated `--draft path/to/frames.json` checks an art agent's partial document through the same production frame parser before the complete set is assembled. Partial documents never become available in the production holiday library.

The loader accepts `companion-holiday-bodies.frames.json` version1 with exactly eight known `{avatar}-holiday-bodies.png` sheets and 72 unique `(Avatar,Theme)` looks. Each look has the existing eight poses, common source scale/head width, measured anchor/grip/crown, and a per-look Wink eye ellipse when applicable. Orbit must be ringless. `FindHoliday` returns the existing immutable `CompanionVariantLook` with choice `Holiday` plus its canonical theme name.

Native previews are written under the ignored test output directory. They establish offscreen rendering behavior, not live FFXIV validation.
