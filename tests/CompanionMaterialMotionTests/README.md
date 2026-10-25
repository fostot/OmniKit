# Material motion checks

Build the main plugin after its material-motion implementation is ready, then run from the repository root:

```powershell
dotnet run --project tests/CompanionMaterialMotionTests/CompanionMaterialMotionTests.csproj -c Release --no-restore
```

This focused harness loads the actual plugin DLL and public artwork. It reuses the native ImGui software painter, supplies a synthetic texture service and rejects framework access. It never reads a player profile, starts a game, contacts a provider or changes source artwork.

The matrix covers the 32 everyday looks, 72 holiday bodies and 52 painted costumes at 96/192 pixels in both facings, plus the thirteen species-default profile resets. It deliberately seeds the wrong material before calling the actual runtime to prove that the current authored look ID is selected.

Checks cover distinct material deformation, rigid point-pair distances within 0.1%, soft width changes, connected mesh orientation, total textured-mesh area within 8% of the unchanged source quad, finite bounds, unchanged neutral texture/UVs during blinking, exact material-aware eyelid integration, phase seams, idle time changes and continuous stopping. Orbit's actual body, painted hat and both ring passes must use one shared material-aware rigid matrix. Existing ring palette/facing tests remain in OrbitPoseTests and are not repeated here.

Representative native diagnostic frames are written to ignored `.local/companion-material-motion-tests`. Any assembled preview loop has illustrative playback timing; this is offscreen evidence, not live-game validation.
