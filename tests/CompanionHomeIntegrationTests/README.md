# Compiled home integration proof

This executable loads the existing Release `OmniKit.dll`; it does not build the plugin. Coordinate with other work before running so the DLL is not rebuilt while loaded.

```powershell
dotnet run --project tests/CompanionHomeIntegrationTests -c Release
```

`--navigation-only` and `--draw-only` restrict execution to the changed path. An optional first positional argument selects another previously built plugin DLL.

`--exit-only` runs the focused compiled departure proof. It seeds the sleeping boundary, then uses actual `BeginLeaving`, `Update`, `PrepareHomeNavigation` and `NavigateHome` through the entire departure and return. Default plus all twelve additional houses run in both directions at 96/192 companion size. The visitor remains at the smaller `.558` scale through the doorway and a `.35`-cell outward step, then grows at that fixed outside point. Nap art covers these three legs; ordinary movement art resumes afterward. Each case must finish and return to its original ground point.

The proof invokes the compiled `RoamingCompanion.DrawNap`, including its authored-look branch, through a synthetic texture provider. Draw commands verify that walls are redrawn over the visitor only during the doorway leg; the outward step and growth paint in front of the house. Both paths suppress accessories indoors and during the outward step, then restore them during growth only when the effective accessory setting is enabled. Four sheets show 208 native samples of the small door exit, outward step, foreground growth and end of growth. Another 624 clipped draws check the selected-look branch and disabled-accessory setting without duplicating contact sheets. This proof passed 48,691 assertions. Source-linked base and styled nap logic also passed 2,751 and 10,480 focused checks. It does not repeat the unrelated full navigation suite.

After the foreground-entry and measured Default-arch correction, the focused
compiled exit proof passed 48,683 checks. Its direct painter invocation now
reproduces the caller's ImGui channel split/merge and uses the retained Valentione
bow for attachment checks; complete painted hats are covered by the wardrobe
path. This keeps the fixture aligned with the current rendering contract.

The navigation proof constructs synthetic configuration, layouts and surfaces, then binds the actual private `RoamingCompanion.PrepareHomeNavigation` and `NavigateHome` methods to the actual `CompanionHomeRoutine.Update` navigator. Default, Cloud Cushion, Lantern Hut and All Saints' Wake houses exercise sixteen complete approach/sleep/wake/return routes: ordinary placement and homes flush against the left or right and bottom screen edges, in both facing directions. Two more All Saints' Wake routes include the active seasonal padding. An unreachable elevated porch must abort without doorway art and retain the actual physics position. Positions are checked for finite values and viewport containment. This catches integration errors that a synthetic navigator cannot, including selecting a return jump approach through the house. The integrated proof passed 50,843 assertions after the physical porch-height and clear-landing corrections.

The drawing proof invokes the compiled `DrawHome`, `DrawGrabHand` and `UpdateHomeStyle` methods with only a synthetic texture-provider stub. Native ImGui draw lists are rasterized at 96 and 192 logical pixels. Checks cover all thirteen houses in both directions, all five hand styles and four poses, exact crop/texture routing, measured finger contacts, fixed source scaling, Random stability during pickup/release and preserving an occupied house until the visitor is outside. Colored circles in hand sheets mark synthetic body/house centers; they are diagnostic markers, not shipping artwork.

Output is confined to the ignored `.local/visualharness/companion-home-integration` directory: drawing sheets, an `exits` subdirectory and a JSON trace of synthetic navigation. The harness does not construct the plugin, call its update/draw loop, read player data or configuration, enumerate real windows, access the network, or invoke game services/native game functions. Its texture provider serves only local public artwork. In-game movement, texture loading and input still require live validation.
