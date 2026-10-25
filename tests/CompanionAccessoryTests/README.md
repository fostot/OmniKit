# Accessory placement checks

Run from the repository root after the integration build:

```powershell
dotnet run --project tests/CompanionAccessoryTests/CompanionAccessoryTests.csproj -c Release --no-restore -- src/OmniKit/bin/Release/OmniKit.dll --editor
```

This harness loads the actual built plugin. It exercises head-width offsets, crown-pivot scaling and clockwise local rotation, inverse mouse deltas, translations, 96/192 sizes, mirroring, rolling and shear. Expected rotated corners are calculated from source artwork coordinates before the body transform; the tests check the fixed crown pivot, unchanged UV correspondence, finite/clamped angles and legacy zero-angle records. Synthetic preference serialization covers the real rotation property. Eight real body types and all nine accessories also check reserved padding through diagonal and quarter-turn body rotations. A small native ImGui render checks the production draw vertices and writes a synthetic textured-quad diagnostic under ignored `.local/visualharness/companion-accessories`.

Integration checks use synthetic roamer state and throwing service proxies to prove that editing freezes motion, gait, physics, nap state and render clocks. Actual appearance decisions and the roamer's look resolver apply manual everyday/holiday changes during sleep without moving or waking the pet, while automatic event changes wait for the safe-switch gate.

The actual roamer's accessory-layer helper is also invoked on a native split draw list. After merging, each of the five complete bows follows the single body quad, and subsequent foreground paint stays on the front channel. Rotated/mirrored cases use synthetic texture handles without service calls. Painted wardrobe hats do not use this overlay helper.

It does not read personal settings or profiles, invoke game/framework methods, save configuration, or use a network provider. The diagnostic demonstrates accessory geometry rather than the visual fit of any specific mascot artwork.

Pass `--editor` to also render the actual fitting window with approved project artwork at minimum 480×330 logical size and scales 1, 1.5 and 2. This checks selector menus, stationary neutral body geometry during moving/resizing/rotation, vertical corner-resize input, both angle endpoints, same-row Size/Angle controls with gutters, artwork bounds, and Save/Reset/Cancel plus simulated-save-failure behavior. Reopening must retain the saved angle. Persistence is replaced with an inert synthetic callback; no settings file is written. These are native offscreen checks, not live FFXIV verification.

Pass `--blink-wiring` to run only the bounded universal-blink integration proof. It invokes actual `RoamingCompanion.DrawBlink` for one reviewed everyday look per avatar at 96/192 and both facings, including the existing deformation on Ember/Mochi/Puff. Open eyes and disabled blink/animation/editor modes emit no geometry or texture request. Actual Idle/Walk `TryDrawLook` calls at zero/full closure retain identical body/ring vertices and UVs, with eyelids appended. Texture handles are synthetic and limited to hash-verified public asset paths; all game services throw. This checks runtime wiring without repeating the separate full artwork/profile visual matrix.

Pass `--editor-blink` after the wardrobe integration build to isolate the actual fitting-preview blink. All thirteen original identities plus four representative everyday/painted-costume bodies use public textures in the real editor. Native geometry compares one stable body quad against open/closed eyelids, disabled blink/animation output, texture requests and the isolated preview clock. The live companion remains behind its editing guard. Rendered previews are written under ignored `.local/visualharness/companion-accessories/editor-blink`. Ordinary editor layout/drag checks explicitly disable blinking so their body-stability assertions remain independent.
