# Companion entrance and nap artwork

The Moonblob/Wink/Nova nap sheet was generated on 2026-10-22 with the built-in OpenAI image-generation tool, with transparent background enabled. Its first pass was accepted. The shipping PNG is byte-identical to the generated original; no offline pixel editing, recoloring, resampling or alpha replacement was applied.

| Asset | Dimensions | SHA-256 |
| --- | --- | --- |
| `companion-naps-abstract.png` | 1448 × 1086 RGBA | `2310c44d513f7083807a4fe2cb079d4812141752ac96b0772d8ebb1794e077c9` |

This sheet has three rows: Moonblob, Wink and Nova. Each row has four columns in order: Enter, Settle, Sleep Inhale, Sleep Exhale. No house, doorway, bedding, sleep letters or floor is baked into these sprites. Runtime supplies doorway clipping and sleep indicators independently.

References were `moonblob-limbless.png`, `wink-motion.png` and `nova-motion.png` for the three identities. Existing source artwork remains unchanged. The five additional companions use the final row of their own whole-body pose atlases; see their [exact prompts, hashes and provenance](WHOLE-BODY-COMPANION-PROMPTS.md).

## Measured layout

`companion-naps.frames.json` supplies the measured layout for all eight companions. Generated rows and columns are slightly uneven, so consumers must not assume equal-grid UVs.

- `Version`: 1.
- `Atlases`: eight character rows, each with `Avatar`, `Outfit`, `File`, `Width`, `Height`, `ScaleReference` and four `Frames`.
- `Bounds`: absolute source pixels `[left, top, right, bottom]`, right/bottom exclusive; alpha at least 16 bounds plus up to five pixels, constrained by neighboring-frame separators.
- `Anchor`: absolute source-pixel `[centerX, baselineY]`, measured from alpha at least 96. Place this anchor at logical `[0.5, 0.9]`.
- `ScaleReference`: fixed source pixels per logical square, shared across every pose of that avatar. Scale by `logicalSize / ScaleReference`; do not stretch a crop or scale each pose independently.

The existing sheet retains source scales 317 for Moonblob, 353 for Wink and 345 for Nova. Its 12 poses were visually inspected on charcoal: Moonblob has no limbs, Wink closes its single eye, and Nova retains soft rounded rays. The sleeping pairs retain closed eyelids with subtle breathing differences. Source alpha ranges from 0 to 255. Diagnostic crops and composites affect only ignored local files. This is artwork/layout evidence, not live-game animation validation.

## Exact abstract prompt

Use case: stylized-concept. Asset type: polished transparent game-companion entrance and nap sprite atlas.
References are identity guides in exact order: 1 Moonblob, 2 Wink, 3 Nova. Preserve the approved friendly pearly storybook JRPG shading and distinctive species. Generate NEW sleep poses with the same polish and scale.
EXACT layout: 4 equal columns x 3 equal rows, 12 isolated complete characters, clear transparent alpha background, generous empty gutters >=12% of each cell, no crossing cell boundaries. The three rows have equal height and four columns equal width; use a 4:3 canvas. Each pose centered in its cell on a consistent baseline about84% down. Same character source scale per row: sleepy/squashed forms become physically lower, never enlarged to fill cell.
Row1 MOONBLOB: smooth organic translucent pearly jelly orb, two glossy amber eyes, tiny mouth, blush, internal little sparkles. It is COMPLETELY LIMBLESS in EVERY pose: NO feet, toes, hands, arms, fins, ears, nubs or tentacles. Continuous smooth rounded contour, slight contact squash. Preserve spherical jelly identity.
Row2 WINK: one single large friendly blue cosmic eye in a pearl wisp orb, tiny smiling mouth, little soft curl, restrained orbital halo. Only ONE eye. Closed sleep eye is a gentle curved eyelid; no eyeball showing when asleep. No extra body/arms/legs.
Row3 NOVA: adorable pearly starlight puff with rounded soft rays/whisps, two glossy blue eyes and tiny smiling mouth. Preserve rounded cloud-star identity, not flat sharp spikes. Quiet dim blue/lilac glow for sleep, no flash.
Columns left to right EVERY row:
1 ENTER: awake, leaning right and squashing/crouching slightly to slip into a doorway; nothing depicting the doorway.
2 SETTLE: gently curling/sinking down, eyelids becoming drowsy, relaxed content tiny mouth.
3 SLEEP INHALE: completely closed sleepy eyes, peaceful curled/squashed pose, subtly fuller body.
4 SLEEP EXHALE: exactly matching column3 position/species/closed eyelids, subtly lower body (only around3percent) for a slow breathing loop.
No change of identity, face placement or scale between sleep3and4. Keep muted pearly/cream base shading suitable for runtime tints. Highlights remain soft and glossy. Do not invent accessories. Highly polished handpainted fantasy mascot art, clear at96px.
NO houses, doors, pillows, blankets, ground shadows, floor, props, Z symbols, text, grid lines, numbers, labels, logo, watermark, checkerboard or colored background. Genuine transparent RGBA with clean alpha gutters.

