# Seasonal accessory layers

Run from the repository root:

```powershell
rtk proxy dotnet run --project tests/SeasonalLayerTests -c Release
```

This source-linked test uses the production seasonal renderer and installed ImGui bindings without game services. It checks the four complementary hat partitions, positive triangle area, exact source coverage, non-overlap, malformed metadata rejection, finite transformed vertices, explicit render gutters, and the unchanged accessory PNG hash.

The native draw-list proof renders all nine accessories on the original Mochi body at 96 and 192 pixels, comparing a single front layer with rear/body/front ordering. Both facings use a rotated, lowered fit so the occlusion is visible. Output is ignored under `.local/seasonal-layers/`.

The top hat, straw hat and witch hat place their rear-left brim or underside behind the body. The Starlight hat places its rear-right fabric fold behind while retaining its cuff and pom in front. The five ribbon decorations remain entirely in front. This is a partition of the existing 2D artwork, not newly painted rear surfaces or a 3D hat model. Saved transforms apply to the complete quad before either layer is drawn.

The current run passed 34,257 checks. The complete 96-pixel gallery and the four 192-pixel hat comparisons were inspected. No live-game behavior is claimed.
