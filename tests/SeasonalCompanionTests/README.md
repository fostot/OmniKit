# Seasonal companion checks

From the repository root, with the installed API15 assemblies available:

```powershell
dotnet run --project tests/SeasonalCompanionTests/SeasonalCompanionTests.csproj -c Release --no-restore
```

The test source-links the production policy, native observer and accessory renderer. Its service references deliberately throw if accessed: no running game, profile, login, network request or native singleton is used. Native API members are compile-checked only. Initial restore needs only the installed .NET framework and a local empty source; there are no package dependencies.

Verified: **97 policy/metadata checks** and **13,824 geometry checks**. Cases include all nine native families, phase zero, duplicate slots, separate world/UI evidence, conflicting and combined festivals, unknown/collaboration IDs, manual/off/logout behavior, future local sheet relationships, exact official date boundaries, no annual recurrence, and invalid art metadata. The continuous full-circle Moonblob bound is checked analytically against the declared 0.23 padding.

The native offscreen ImGui painter renders the real PNG textures at 48/96/192 pixels, both reaction/nap facings and shared motion phases. Output goes under ignored `.local/seasonal-companion-tests/native`. The renderer is the same source used by OmniKit; fixture types do not replace its geometry. The harness checks art attachment and festival policy, not the final roaming integration or live server festival state.

No performance, frame-rate or live FFXIV claims follow from these checks. Changes to source art, anchors, transforms or bounds require the affected checks again; unrelated UI changes do not.
