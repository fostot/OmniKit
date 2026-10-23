# Seasonal accessory fit checks

From the repository root:

```powershell
dotnet run --project tests/SeasonalFitTests/SeasonalFitTests.csproj -c Release --no-restore
```

This harness links the production metadata reader, accessory placement, sprite transforms, and body attachment warp. It uses installed native ImGui and actual public PNG assets, without loading the game or a personal configuration.

The native galleries cover all eight base creatures and all 32 authored looks in normal, carried, and sleeping poses, at 96 and 192 pixels, facing both directions. Orbit uses its new ringless core; the separate production ring painter is covered by the compiled integration harness. Geometry checks cover all nine accessories, every source pose, an exact complete rolling-circle bound, and sampled full-strength crown movement. A deforming body translates the entire hat from its crown without stretching the hat. Body geometry and UVs remain unchanged when a hat is added. Invalid fit inputs fail closed.

Outputs are written beneath the ignored `.local/seasonal-fit/native/` directory. These are offscreen rendering checks, not live FFXIV validation.
