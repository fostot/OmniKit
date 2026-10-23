# Lunar walk rigs: provenance and validation

The four 1254 × 1254 RGBA sheets were generated with the built-in OpenAI image-generation tool. The project PNGs preserve the generated outputs byte for byte. The runtime articulates the supplied body parts; no offline image edits, recoloring, resampling or frame synthesis were applied to the shipping PNGs.

| Character | Outfit | Asset | SHA-256 |
| --- | --- | --- | --- |
| Bimble | Navigator | `bimble-walk-rig.png` | `ff260b7ec283f666f79d4c0395dc4f65d8c76fa4c398b40346b9673ec90874b7` |
| Bimble | Stargazer | `bimble-walk-rig-stargazer.png` | `9a2f18c0002943b0ac9a27e669b28c3b17e30b4c8b5765b6beccd095faad4970` |
| Moonbear | Navigator | `moonbear-walk-rig.png` | `efb8911dfa928b1fe091e7feed4391fa03cb644cc5a1d71a9b5fbc9e4b415cc6` |
| Moonbear | Stargazer | `moonbear-walk-rig-stargazer.png` | `836931d4f0fd51703bfe3ce557908c1aa1612b89e62702a5d8421589d4b00a00` |

## Sheet and motion contract

Each sheet contains nine isolated parts in row-major order: body, near arm, far arm, near thigh, far thigh, near shin, far shin, near foot and far foot. The body includes the head, ears, outfit and tail. The arm and leg parts are separate so that actual walked distance can drive continuous poses instead of exchanging independent full-body pictures.

`lunar-walk-rigs.json` records each exact source crop, normalized joint pivot, body placement, segment length and cycle distance. Source rectangles use exclusive right and bottom pixel coordinates. Crops follow alpha ≥16 bounds with up to five pixels of gutter, constrained to avoid adjacent parts. The Stargazer bear's closely spaced thigh and shin require smaller gutters. Maximum crop-edge alpha is 3/255 for both Bimble outfits, 6/255 for Navigator Moonbear and 15/255 for Stargazer Moonbear.

The production evaluator generates the far arm, two articulated legs, six adjoining body bands and near arm in drawing order. The body bands allow restrained secondary motion; Bimble's upper bands provide ear follow-through. Feet use ankle pivots, and the stance-foot trajectory accounts for the companion's world movement. This is a small 2D cutout rig, not a video or a simulated 3D skeleton.

The accepted proportions use internal limb pivots so painted fur overlaps at the joints, with shorter legs tucked under the body. Directional steps follow travel in both screen axes. Vertical travel shortens the stride to preserve reachable leg geometry; the character remains upright. Supporting-foot planting is exact at steady headings; heading transitions are bounded and continuous. Facing has a dead band near vertical travel, and runtime elapsed time comes from a high-resolution clock.

## Diagnostic evidence

The standalone diagnostic exporter linked the production `LunarWalkRig.cs` and evaluated all four metadata entries at 49 phases, including the closed loop endpoint, in both directions: 392 poses. The diagnostic rasterizer used those actual ordered quads and UV coordinates, including every body band.

The resulting local review artifacts are:

- `.local/lunar-rig-walk.gif`: a 24-frame looping comparison of all four rigs, both directions, at 96px and 192px.
- `.local/lunar-rig-walk-first.png`: the same comparison at phase zero.
- `.local/*-walk-rig*-cycle.png`: eight sampled 192px poses per outfit.
- `.local/*-walk-rig*-bounds.png`: charcoal composites with the measured crop rectangles.

The four contact strips and combined size comparison were visually inspected. Hips, knees and ankles remain joined in the sampled poses, all four outfits retain their silhouette, and mirrored poses retain the expected facing direction. Diagnostic phase-zero and phase-one pixels match exactly for all four rigs in both directions. The PNG hashes were checked against the metadata after rendering. Diagnostic cropping, resampling and compositing affect only ignored `.local` outputs.

Focused production-quad checks additionally cover all four rigs at 72 travel headings, 64 phases and both facing directions. They check reachable fixed-length legs, cell bounds and supporting-foot world positions in both axes. Separate checks cover facing stability, bounded heading transitions, stop/start phase retention and equal travel at 60/144 Hz and variable pacing. These are geometry and state checks, not observations of animation inside the game.

These checks validate source crops and assembled poses. The GIF provides moving playback for review; the available image-inspection tool exposes static images. This does not establish perceived smoothness or live rendering performance inside FFXIV.

## Generation prompt set

Two earlier full-body eight-frame walk sheets were rejected because the poses did not alternate convincingly. Neither rejected output is included in the project. The accepted method uses the following part-sheet prompts, with transparent background enabled for every call.

### Bimble Navigator

Production layered 2D rig parts sheet: nine separate components in a 3×3 transparent square sheet. Use the supplied Bimble reference for ivory fur, navy/gold clothing, moon clasp and polished soft JRPG identity. Side profile faces right. Cell 0 contains the complete head, ears, neck, torso, cape and shorts only, with no attached arms, hands, legs or feet; the tail may remain. Cell 1 is the near whole arm, shoulder at top and paw at bottom. Cell 2 is the shaded far arm. Cells 3 and 4 are near and far thigh capsules, hip at top and knee at bottom, with no feet. Cells 5 and 6 are near and far shin capsules, knee at top and ankle at bottom, with no feet. Cells 7 and 8 are flat near and far feet pointing right, with the ankle at the left/back. Use rounded overlapping joints, one part per cell and generous transparent gutters. No background, noise, ground shadow, effects, grid, text or extra characters.

One targeted repair retained the sheet layout, face, clothing and separate parts, removed the attached arm/paw and leg/thigh stubs from cell 0, ended sleeves at the shoulder and shorts at the hip, and removed stray red/yellow matte noise. It reiterated plain thigh/shin capsules without fingers or feet and flat right-facing feet. The shipping PNG is the repaired output.

### Moonbear Navigator

Use the accepted Bimble kit for the nine-part layout only and the existing Moonbear art for character identity. Make every component bear anatomy: small round ears, black nose, creamy muzzle and rounded fur, with no rabbit features. Preserve the same right-facing component order, isolated limbs, overlapping rounded joints and transparent gutters. The main body contains only head, torso, navy/gold outfit and permitted tail, without attached limbs. No extra props, characters, background, shadow, effects, text or grid.

### Stargazer outfits

Apply once to the accepted Bimble sheet and once to the accepted Moonbear sheet: change only the navy/gold clothing on the upper-left body component to a rich violet short capelet and shorts with silver trim, clasp and a small star accent. Preserve sheet dimensions, layout, component positions, all fur, head, limbs, alpha and the other eight unclothed components. Keep the main body free of attached limbs; its tail remains. Do not add a hat, staff, props, characters, background, shadow, effects, text or grid.

These record the final prompt specifications and targeted correction. The generated outputs were inspected rather than assumed to satisfy every requested pixel-preservation constraint; the metadata measures each accepted sheet independently.
