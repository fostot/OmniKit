# Compiled wardrobe integration

Build the main plugin first, then run from the repository root:

```powershell
dotnet run --project tests/CompanionWardrobeIntegrationTests/CompanionWardrobeIntegrationTests.csproj -c Release
```

This loads the actual built plugin DLL and original public PNGs. It calls production look selection, walking/carry rendering, generalized blinking and bedtime rendering with synthetic configuration and texture services. Framework/game access throws. It does not load a real profile or contact a service.

Checks cover all13 plain selections,52 named costumes,65 rendered bare/costume bodies, pinned carry grips, Orbit back/core/front order and one body quad or continuous mesh. Entry, sleeping and waking use each character's bare row; leaving and returning restore the painted costume without changing selection. Exact source UVs are checked at96/192 in both facings. Missing optional wardrobe data preserves the independent original packs. Full closure uses the same wardrobe face profile, with silver contrast only for Nib.

Native galleries are written under ignored `.local/companion-wardrobe-integration`. These are offscreen integration checks; they do not establish live-game behavior.
