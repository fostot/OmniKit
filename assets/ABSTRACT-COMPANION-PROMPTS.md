# Abstract companion artwork provenance

Created for OmniKit on 2026-10-22 using the built-in `image_gen.imagegen` tool with `transparent_background: true`. Three generation calls produced the three accepted atlases; no repair calls were needed. No API-key CLI, outside artist assets, or reference images were used. These are original fictional companions.

The accepted PNG files are unchanged copies of the built-in tool outputs. No background removal, repainting, resampling, or other offline edits were applied to the source assets. Inspection-only composites do not replace the source PNGs.

## Atlas contract and inspection

Each 1024 × 1536 RGBA atlas contains 24 complete poses in four columns and six rows. Pose order: 0–7 movement, 8–11 idle/blink, 12–15 mouth preview, 16–17 carried/dangling, 18–19 falling, 20–21 landing, 22–23 edge pushing. Mouth poses are an explicit animation preview; they do not provide synthesized speech or lip synchronization.

All three sheets were visually inspected for species consistency, expressive faces, blink/mouth changes, motion poses, and separated complete silhouettes. Alpha-channel inspection confirmed real transparency. Generated row spacing varies; use the explicit pixel rectangles and anchors in `abstract-companions.frames.json`, not a uniform-grid assumption. Wink's wide landing frame has a wider region so its aura is not clipped.

The JSON records original PNG SHA-256 hashes, dimensions, alpha statistics, crop rectangles, body bounds, and baseline anchors. Rectangle right/bottom coordinates are exclusive. Apply one common pixel scale to all poses of a character and translate around its anchor; never resize each frame independently to fill a rectangle. Crops retain five source pixels around the inspected alpha bounds. Common source scales are 281 pixels for Moonblob, 290 for Wink, and 293 for Nova, with destination anchor (0.5, 0.9); every full draw rectangle fits inside its logical character box. All crop edges have alpha at most 7/255 and at least five source pixels separate the crop edge from alpha values of 16/255 or greater. A 96-logical-pixel contact sheet was inspected for clipping and readability; live game animation is a separate validation step.

The pearl/silver body shading supports renderer tinting into the existing six palettes. Highlights, eyes, blush, and aura retain painted color detail. The supplied pixels already contain compact glow; a renderer's magic-effect toggle controls additional effects, not removal of that painted lighting.

## Final prompts

## Moonblob

Output: `moonblob-motion.png`.

```text
Use case: stylized-concept. Asset type: production 2D game companion animation sprite atlas.
Create ONE polished transparent PNG sprite sheet of MOONBLOB, an original very cute pearly jelly moon pet. Exactly 24 isolated complete full-body sprites, arranged as exactly 4 columns and 6 rows. Portrait canvas 1024 x 1536 preferred, six equal-height 256px rows and four equal-width 256px columns, no grid lines.
Character: a small plump translucent marshmallow-jelly moon creature, warm creamy pearl body with dimensional velvety plush softness, two huge glossy dark eyes with luminous reflections, tiny happy expressive mouth, warm rosy cheek blush, tiny soft flipper feet, a little moonlit sparkle visible within its jelly body. Adorable sophisticated hand-painted storybook JRPG quality, rich soft shading and beautiful rim light; not a flat vector, not a logo, not a generic emoji. Mostly pearl white and silver neutral shading, restrained blush, allowing runtime palette tint. A consistent single character design in all 24 cells.
Layout is crucial: one entire character per cell, same apparent scale and three-quarter front camera, face readable at 96px, centered horizontally with normal resting body baseline at 82 percent of its cell height, generous empty alpha gutters on every side. Character including glow fits inside the inner 70 percent of each cell. No neighboring sprites touch. No objects, scene, ground, labels, numbers, text, border, watermark, or cast shadow. Genuine transparent alpha background; no white canvas or checkerboard drawn into pixels.
Animation order left-to-right, row-by-row:
Row 1 four walking/rolling phases 1-4: soft lean left; left foot forward/body slightly tall; soft center squash; lean right.
Row 2 four walking/rolling phases 5-8: right foot forward/body slightly tall; center bounce; left-forward recovery; neutral cycle recovery. Deliberate small body deformation and alternate feet, face stays attached and readable.
Row 3 four idle/blink phases: relaxed eyes open; gentle breath slightly fuller body; soft half-closed eyelids; full happy closed-eye blink.
Row 4 four distinct mouth/talking preview phases: small closed smile; small rounded O mouth; cheerful open oval mouth; wide delighted open smile. Eyes open, identity unchanged. No letters or speech bubbles.
Row 5 four physics reaction poses: picked up vertically elongated dangling with feet down; second dangling slight side lean; falling rounded body with tiny feet tucked and surprised face; second falling elongated with feet downward. No hand or grabber shown.
Row 6 four physics reaction poses: landing broad squash; landing rebound tall; gently pushing an invisible edge to the left; second strained cute edge-push squish toward left. No wall or props shown.
Exactly four cells per row and six rows. Prioritize gorgeous cute consistent character art, actual changing animation poses, complete separated silhouettes, and correct transparent atlas layout.
```

## Wink

Output: `wink-motion.png`.

```text
Use case: stylized-concept. Asset type: production 2D game companion animation sprite atlas.
Create ONE polished transparent PNG sprite sheet of WINK, an original extremely cute little cosmic eye familiar. Exactly 24 isolated complete full-body sprites, exactly 4 columns and 6 rows, portrait canvas 1024 x 1536 preferred. Every 256 x 256 cell contains exactly one sprite, centered at x=128,384,640,896; six row centers y=128,384,640,896,1152,1408. Keep generous transparent padding and do not draw grid lines.
Wink is a tiny floating spherical pearl-cream moon orb, velvety softly translucent jelly/plush material, with ONE very large friendly glossy midnight-blue iris/eye on its face, huge sparkling eye reflections, cute soft upper and lower eyelids, tiny sweet expressive mouth BELOW the eye, subtle warm blush, and a gentle compact luminous silver cosmic aura hugging the body. One eye per creature, never two eyes. Soft dimensional body, not a disembodied realistic anatomical eyeball: no veins, no flesh, no creepiness. A tiny magical friend full of personality. Beautiful polished hand-painted storybook JRPG quality with rounded volumes, soft light, rich painterly shading, delicate pearl highlights. Mostly neutral pearl/silver shading for runtime palette tint; midnight blue eye, small pink blush. Not flat vector, not an emoji, not simple circles.
Same recognizable character, same scale and camera in every cell, full body plus aura inside inner 70 percent of each cell, readable at 96 pixels. No text, labels, numbers, watermark, frame, scene, ground, shadow, background texture, extra characters, hands or props. Genuine transparent alpha background.
Read left-to-right then top-to-bottom:
Row 1: four distinct floating movement frames 1-4, orb tilts left, gently stretches up, softly squashes, tilts right; eyelid/eye and aura follow the body.
Row 2: movement frames 5-8, right-leaning upward bob, low bob, leftward recovery, neutral return. Clear but gentle floating animation with varied eyelid/eye expression, no trails crossing cells.
Row 3 idle/blink: relaxed open eye; contented slightly narrowed eye and breathing expansion; half-closed soft eyelid; fully closed happy curved eyelid with tiny smile.
Row 4 talking mouth poses, eye open: tiny closed smile; tiny O mouth; cheerful open oval mouth; broad adorable open smile. Mouth clearly BELOW eye, stays separate.
Row 5: picked-up dangling stretch; second gentle dangling lean; falling rounded with eye wide and mouth surprised; falling elongated downward. No grabber shown.
Row 6: broad soft landing squash; tall rebound; gentle pushing an invisible edge left with squished left side and determined cute eye; second edge-push strain pose. No wall shown.
Exactly 24 separated sprites, four in every row, six rows. Prioritize adorable highly polished character artwork, real pose/expression changes, stable character identity, precise separated cells and transparent alpha.
```

## Nova

Output: `nova-motion.png`.

```text
Use case: stylized-concept. Asset type: production 2D game companion animation sprite atlas.
Create ONE polished transparent PNG sprite sheet of NOVA, an original extremely adorable magical starlight puff companion. Exactly 24 isolated complete full-body sprites, exactly 4 columns and 6 rows, portrait canvas 1024 x 1536 preferred. Equal 256 x 256 cells, centers x128,384,640,896 and y128,384,640,896,1152,1408. No drawn grid.
Nova is a little softly rounded luminous star-cloud creature with a plump pearl-cream body and five or six soft rounded starlight rays/curled wisps forming its silhouette, like an impossibly cute glowing plush star puff. TWO large glossy midnight-blue eyes, luminous eye reflections, tiny friendly expressive mouth, warm blush, soft fluffy/velvety texture and pearlescent rim light. Gentle opalescent neon glow hugs the character; restrained pale cyan and lilac light accents over mostly pearl-white/silver body so runtime tint works. Radiant and magical, beautifully dimensional hand-painted storybook JRPG quality; rich painterly shading and soft sculpted volumes. Not a flat star symbol, not hard sharp spikes, no harsh neon flash, not an emoji. Consistent species, face and ray count in all cells.
Each entire character plus compact glow must fit in inner 68 percent of its cell with generous empty transparent alpha gutters. Same scale and three-quarter front view, face readable at 96px. No long trails, no extraneous background stars far from body, no text, labels, watermark, shadows, ground, scenery, border or props. Genuine transparent alpha background, no painted backdrop.
Read left to right then top to bottom:
Row1 four floating/walking phases1-4: soft lean left; left lower ray stepping with taller body; little center squash; lean right.
Row2 four movement phases5-8: right lower ray stepping; low floating bob; leftward recovery; neutral cycle return. Clear changing lower rays and body squish, rounded rays stay attached, gentle motion.
Row3 idle/blink: relaxed eyes open; gentle breathing fuller body; eyes half closed; happy closed eye blink.
Row4 talking mouth preview: tiny closed smile; small round O; open oval mouth; wide delighted smile, eyes open throughout, mouth stays below eyes.
Row5 physics reactions: picked-up vertically stretched dangling star; second dangling slight sideways lean; falling tucked rounded star with surprised face; second falling elongated downward. No hands/strings/grabbers shown.
Row6 physics reactions: landing broad squash with rays spread softly; tall rebound; pushing an invisible edge left with cute determined face and left ray flattened; second gentle edge-push strained pose. No wall shown.
Exactly 24 complete separated sprites, four per row, six rows. Prioritize exceptionally cute polished fantasy pet art, a stable soft STAR-PUFF silhouette, real animation pose differences, generous alpha gutters, and transparent alpha.
```
