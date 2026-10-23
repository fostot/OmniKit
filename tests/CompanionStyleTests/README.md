# Companion home and hand styles

Run from the repository root:

```powershell
dotnet run --project tests/CompanionStyleTests/CompanionStyleTests.csproj -c Release
dotnet run --project tests/CompanionStyleTests/CompanionStyleTests.csproj -c Release -- --render
```

The project links the production style library, selection state and renderer.
It verifies all 13 homes and five hand styles including Default, geometry at
48/96/192 px, reflected anchors and UVs, source aspect, the exact accepted Default
bed/opening/crop values, and fixed contact across the four hand poses. Wall quad
area plus doorway area must exactly cover the home without missing sections.
Metadata checks reject unsafe file names, mismatched artwork hashes, duplicate
styles/poses, invalid crops/anchors and inconsistent per-pose scaling.

Selection checks keep Random stable between change events, honor the current
seasonal override, keep an active nap on its old home until switching is allowed,
and keep a picked hand through hold/release. No setting is written by a helper.

`--render` uses the installed native ImGui binding and the existing software
rasterizer. It calls the actual production helper methods with the reviewed
textures and existing nap artwork, making ten contact sheets under ignored
`.local/visualharness/companion-styles`: all homes with Wink/Moonblob at 96/192 px
and both facings, plus all hand poses at 96/192 px. These are isolated draw lists,
not game screenshots; no game, profile, inventory, microphone or provider is read.
The source atlas bytes are never edited. Malformed metadata fixtures also remain
under that ignored output directory.

This proves the painter/geometry boundary. The parent runtime still owns home
state transitions, selection timing, shared texture retrieval, viewport clipping,
input and persistence; integration and live game behavior need separate checks.
