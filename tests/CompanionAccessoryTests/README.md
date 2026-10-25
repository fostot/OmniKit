# Accessory placement checks

Run from the repository root after the integration build:

```powershell
dotnet run --project tests/CompanionAccessoryTests/CompanionAccessoryTests.csproj -c Release -- src/OmniKit/bin/Release/OmniKit.dll
```

This harness loads the actual built plugin. It exercises head-width offsets, crown-pivot scaling, inverse mouse deltas, translations, 96/192 sizes, mirroring, rolling and shear. A small native ImGui render checks the production draw vertices and writes a synthetic textured-quad diagnostic under ignored `.local/visualharness/companion-accessories`.

Integration checks use synthetic roamer state and throwing service proxies to prove that editing freezes motion, gait, physics, nap state and render clocks. Actual appearance decisions and the roamer's look resolver apply manual everyday/holiday changes during sleep without moving or waking the pet, while automatic event changes wait for the safe-switch gate.

It does not read personal settings or profiles, invoke game/framework methods, save configuration, or use a network provider. The diagnostic demonstrates accessory geometry rather than the visual fit of any specific mascot artwork.

Pass `--editor` to also render the actual fitting window with approved project artwork at minimum 480×330 logical size and scales 1, 1.5 and 2. This checks selector menus, stationary neutral body geometry during moving/resizing, vertical corner-resize input, control and artwork bounds, and Save/Reset/Cancel plus simulated-save-failure behavior. Persistence is replaced with an inert synthetic callback; no settings file is written.

Pass `--blink-wiring` to run only the bounded universal-blink integration proof. It invokes actual `RoamingCompanion.DrawBlink` for one reviewed everyday look per avatar at 96/192 and both facings, including the existing deformation on Ember/Mochi/Puff. Open eyes and disabled blink/animation/editor modes emit no geometry or texture request. Actual Idle/Walk `TryDrawLook` calls at zero/full closure retain identical body/ring vertices and UVs, with eyelids appended. Texture handles are synthetic and limited to hash-verified public asset paths; all game services throw. This checks runtime wiring without repeating the separate full artwork/profile visual matrix.
