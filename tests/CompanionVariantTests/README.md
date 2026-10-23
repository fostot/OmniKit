# Companion variant checks

From the repository root:

```powershell
dotnet run --project tests/CompanionVariantTests/CompanionVariantTests.csproj -c Release --no-restore
```

The source-linked harness validates the actual optional-art parser, all 32 looks, 256 pose crops, 16 original Orbit core poses, unchanged PNG hashes, source aspect and anchors at 96/192 logical pixels, mirrored geometry, fixed carry grips, and malformed metadata rejection. Synthetic selection checks cover cached random choices, base/default fallback, specific choices, deferred switches, one rotation after unlocking, and reset.

It renders the production `CompanionVariantRenderer` with actual PNG textures using offscreen native ImGui. Sixteen galleries and two fixed-grip carry comparisons are saved under ignored `.local/companion-variant-tests`. These are local rendering and policy checks, not live FFXIV behavior or gameplay tests. No providers, credentials, game actions, or real user settings are used.

For the focused Wink eyelid check only:

```powershell
dotnet run --project tests/CompanionVariantTests/CompanionVariantTests.csproj -c Release --no-restore -- --blink-only
```

This renders closure 0, .25, .5, .75 and 1 for all four looks, both facings, at 96/192 pixels. It verifies unchanged neutral-body vertices, look-specific closed-forehead texture coordinates, local eye geometry, transparent tint, and exactly unchanged pixels outside the eye when stages render at identical coordinates. Four `wink-blink-<size>-<facing>.png` galleries contain the 80 cases. No whole-body blend or texture substitution is used.

Travel should use each look's `NormalAtlas` with the caller's existing continuous transform. `MoveA`/`MoveB` remain authored references; this helper does not alternate them or crossfade whole bodies. `ClosedBlink` supplies same-look sleep artwork. The selection lock defers both avatar and look requests; the caller should keep rendering `selection.Avatar` while locked. A `null` selected look means the unchanged base artwork.
