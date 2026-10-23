# Bimble shared-body preview

Run from the repository root:

```powershell
dotnet run --project tests/BimbleRigSmoke -c Release
dotnet run --project tests/CompanionMotionTests -c Release
```

The first check loads the unmodified shipping PNGs, evaluates the production rig and emits actual native ImGui image quads. The shared offscreen renderer rasterizes registered RGBA textures into ignored `.local/visualharness/bimble-v2` comparisons. Each image shows the settled idle beside four walking phases at96/192pixels, for both outfits and facing directions. It does not initialize Dalamud services or a game process.

The motion checks cover all travel headings, grounded settled paws, reachable joints and bounded intermediate geometry. They also verify identical source UVs throughout idle/walk transitions, phase-independent settled poses and the unchanged full-walk trajectories. Disabling animation chooses the static grounded pose. This is isolated rendering/geometry evidence, not live game acceptance.
