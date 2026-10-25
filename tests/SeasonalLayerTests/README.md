# Seasonal accessory overlays

Run from the repository root:

```powershell
rtk proxy dotnet run --project tests/SeasonalLayerTests -c Release
```

This source-linked test uses the production renderer and installed native ImGui bindings without game services. Five bow themes each draw exactly one complete front quad; all rear passes and all four painted-hat overlay passes emit no geometry. At 96 and 192 pixels, both facings and rotated fits preserve the original positions, UV rectangle, tint, and fitted bounds. Metadata contains no polygon partitions, and the approved original accessory PNG hash is checked before and after rendering.

A compact native preview is written to ignored `.local/seasonal-layers/whole-front-bows.png`. This is an offscreen draw-list check, not live-game testing.
