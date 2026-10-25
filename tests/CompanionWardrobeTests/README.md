# Painted companion wardrobe checks

After the thirteen reviewed wardrobe PNGs and `companion-wardrobe.frames.json` are present, run from the repository root:

```powershell
dotnet run --project tests/CompanionWardrobeTests/CompanionWardrobeTests.csproj -c Release -- --native
```

The harness source-links the production loaders, frame math and painters. It checks thirteen bare bodies and four complete painted costumes for all thirteen companions: 65 looks, 520 poses and 125 measured eyes. `FindBare` supplies every bedtime body; `FindDefault` changes only the five new characters. Exact normal/closed crop identities, common body scale/anchors, source aspect and 96/192 bounds are checked in both facings. Original eight base companions, 32 everyday looks and 72 holiday looks remain independent. Painted hats use reserved visual padding without reducing body scale.

Invalid metadata and a byte-altered copy of one public PNG are written only into a unique ignored `.local/companion-wardrobe-tests` fixture. Missing, partial, future-version, duplicate, unsafe-path, mismatched hash/dimension, invalid costume/pose/anchor/eye and malformed packs must expose no partial roster. Original art is never modified. `--native` renders every pose plus open/half/full eye closure using actual ImGui, the production painters and original PNG textures, at96/192 in both facings. It verifies the lids preserve the single normal body and sample only the same look's measured closed-eye skin. Galleries remain ignored. This is offscreen evidence, not in-game validation.
