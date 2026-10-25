# Orbit facing and pose checks

Build the main plugin, then run from the repository root:

```powershell
dotnet run --project tests/OrbitPoseTests/OrbitPoseTests.csproj -c Release --no-restore
```

This loads the actual compiled plugin and public artwork. It uses synthetic configuration, a local texture provider and native ImGui without a game or graphics device. Any framework access throws. It does not read a player profile or contact a service.

The focused proof covers:

- All160 original, everyday, holiday and wardrobe Orbit source poses at96/192 in both facings. Ring centers must match the painted planet's measured Body center reconstructed from its actual frame UVs; radius and normal placement stay unchanged.
- Exact horizontal reflection of band, outlines, colors and motifs for both depth passes, nine holiday themes, decoration toggles and two existing clock orientations. Motifs retain the same Z clipping and ring radius. Disabled animation remains static.
- Four established native fingerprints for the original unmirrored ring output.
- Prior and intervening vertex sentinels around consecutive ring passes, unchanged draw flags and bounds measured from the reflected vertices.
- Actual runtime rendering of all four painted hats, both facings and early/settled carry poses. Rear ring, one painted core and front ring use the same carried body center and facing. Screen-center reconstruction allows only .001px float roundoff at wide gallery coordinates.
- Rigid movement matrices remain finite, periodic, coupled to facing and identity when inactive or animation is disabled. The production activity decay settles continuously. `DrawPosed` transforms complete native ring passes exactly, retaining sentinels, colors, UVs and depth membership.
- Active walking of the original core and all18 everyday/holiday/wardrobe bodies uses the same matrix for painted hat/body, eye closure and both ring passes at96/192. These cases explicitly warm production activity above.99 so an identity-only fixture cannot pass them.

Native `painted-orbit-96.png`, `painted-orbit-192.png` and eight `sway-<size>-<step>.png` stride frames are written under ignored `.local/orbit-pose-tests`. This verifies offscreen rendering and integration, not live-game behavior. It does not infer an extra tilt from painted poses; the existing animated ring plane is preserved and the complete assembly receives the shared travel sway.
