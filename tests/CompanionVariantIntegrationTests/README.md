# Compiled companion draw checks

Build the plugin separately before running this harness. It loads the actual Release DLL without rebuilding it. From the repository root:

```powershell
dotnet run --project tests/CompanionVariantIntegrationTests/CompanionVariantIntegrationTests.csproj -c Release --no-restore -- --carry-only
dotnet run --project tests/CompanionVariantIntegrationTests/CompanionVariantIntegrationTests.csproj -c Release --no-restore -- --seasonal-only
dotnet run --project tests/CompanionVariantIntegrationTests/CompanionVariantIntegrationTests.csproj -c Release --no-restore -- --holiday-only
```

Omit the final option for the combined walk/carry/nap/seasonal matrix. The harness supplies synthetic configuration and texture services backed by the public PNG assets, then invokes the production draw paths using native ImGui. Framework access throws; no game, account, network, or personal configuration is used.

Carry checks compare the actual drawn source grip, the companion's visual grip, and the hand contact point. They cover pickup easing, both carry poses, all 32 looks at 96/192 pixels in both directions, and the original ringless Orbit core with its separate ring assembly. Seasonal checks compare the actual fitted accessory corners against the selected look's measured crown/head width and rigid crown translation. The full matrix also verifies the selected look persists through entering, sleeping, and waking.

Diagnostic PNGs are written under `.local/companion-variant-integration/`. Native offscreen rendering is not live FFXIV validation.

The separate `--holiday-only` mode loads the complete optional 72-look holiday library and writes `.local/companion-holiday-integration/`. It exercises all holiday Walk/Carry draws, eight representative enter/sleep/wake sequences, matching seasonal accessories, and the actual `ResolveLook` gate. That verifies selected-look fallback when disabled/missing, no-theme behavior, preservation while switching is locked, and accessory suppression inside the house. It does not rerun the ordinary 32-look matrix.
