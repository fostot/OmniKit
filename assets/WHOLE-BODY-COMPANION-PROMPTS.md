# Whole-body companion artwork and Wink icon

Generated on 2026-10-22 with the built-in OpenAI image-generation tool, with transparent background enabled. Five companion atlases and one standalone Wink icon were each accepted on their first pass: six calls total, no repairs. All shipping PNGs are byte-identical copies of the generated originals; no offline pixel editing, recoloring, resampling or alpha replacement was applied. The embedded generation provenance is preserved.

The five new atlas prompts used text descriptions. Existing abstract artwork was inspected for its established style, without attaching it to those five generation calls. The Wink icon used `wink-motion.png` as an inspected identity reference. These are original fictional characters.

| Asset | Dimensions | Common source scale | SHA-256 |
| --- | --- | --- | --- |
| `mochi-poses.png` | 1254 × 1254 RGBA | 335 | `767c01195f021a9a936b8fd943d9d9e8256b4170741bf6cfce462d13a25f1422` |
| `puff-poses.png` | 1254 × 1254 RGBA | 336 | `cf1d6bb3b6da7210e026c8265361f1b605ee503cc1b6ef5712ab20e25864193d` |
| `orbit-poses.png` | 1254 × 1254 RGBA | 325 | `2129f6bb1d1fd232ca7a9bcfd5206114207f33f196dc4e0ff8f9bfc0d8aecb31` |
| `ember-poses.png` | 1254 × 1254 RGBA | 353 | `b5a116c4ec4876a10455e64150f20eda9423257e6153cd0386251615113d106b` |
| `prism-poses.png` | 1254 × 1254 RGBA | 338 | `3af4203d247ba27608514a9a482af7d18f9d6242b0357e6f5aa0139256fc82a3` |
| `wink-icon.png` | 1254 × 1254 RGBA | Standalone icon | `6e983d651fb0a00776521b47a5158d6f6d2fc40af3016a656925e898ae817051` |

## Measured layout and state mapping

Each new companion atlas contains 16 whole-body poses in four rows of four: open/half-blink/closed-blink/smile; closed/small-A/round-O/happy mouth; carry/fall/landing/push; nap entrance/settle/sleep inhale/sleep exhale. They have no arms, legs, feet or detached accessories. Orbit’s ring is part of the same whole sprite. No house, bedding or sleep letters are baked into the sprites.

`abstract-companions.frames.json` maps the artwork to the existing 24 runtime slots: walk 0–7 repeats the stable open-body image; idle 8–11 maps the four expression poses; talk 12–15 maps the mouth poses; each pair in carry 16–17, fall 18–19, land 20–21 and push 22–23 repeats its corresponding authored reaction. Movement uses the stable whole-body image rather than an articulated rig. `companion-naps.frames.json` maps source cells 12–15 to its four nap states.

Generated spacing is slightly uneven. Use the measured source rectangles, never equal quarter UVs. Rectangles use absolute source pixels with right/bottom exclusive, containing alpha at least 16 plus up to five pixels of padding constrained by the separator. Anchors are absolute source `[centerX, baselineY]` from alpha at least 96, placed at logical `[0.5, 0.9]`. Each avatar uses one common source scale for all normal and nap poses. Scale uniformly by logical size divided by the source scale; do not stretch or independently fit each crop.

All 80 poses were inspected at 96 pixels, with selected open/blink/landing/sleep poses also inspected at 192 pixels. All PNGs have genuine alpha from 0 to 255; no measured separator or canvas boundary cuts alpha at least 16. Orbit’s outer-right edge reaches only alpha 7. All measured padded rectangles fit the logical square. Mouth differences remain subtle at miniature scale. The icon was inspected at 32, 64 and 96 pixels; its visible bounds at alpha 16 are `[57, 131, 1213, 1085]`, with canvas-border alpha no greater than 1. These are artwork/layout checks, not live-game validation.

The original Moonblob/Wink/Nova nap sheet has its own [prompt and provenance](COMPANION-NAPS-PROMPTS.md).

## Exact Mochi prompt

Use case: stylized-concept.
Asset type: production whole-body companion sprite atlas for an original fantasy game companion.
Canvas: square PNG with a genuinely transparent background. Exactly FOUR columns and FOUR rows, sixteen independent sprites, no drawn grid, no cell labels, no text, no watermark.
Style: exceptionally polished storybook JRPG fantasy creature illustration, hand-painted dimensional shading, creamy luminous highlights, velvety material, gentle blush, large glossy expressive eyes, appealing readable silhouette at 96 pixels. Cute and comforting, never creepy. This is a single unified limbless body, not a parts rig.
Layout: equal-sized cells. Each complete character is centered at the same horizontal cell center with its bottom on the same baseline, consistent source scale and identity across all sixteen cells. Normal body occupies about 68% of cell width and 65% of height; preserve generous transparent gutters on all sides and between every cell. No sprite, glow or ornament crosses a cell boundary. No floor, cast ground shadow, backdrop, scenery, house, bed, stars floating outside the body, or Z letters. No arms, legs, feet, hands, tails, detached pieces or accessories. Expressions and gentle compression are the only state changes; maintain the same viewpoint and identifying markings.
Exact pose order, read left to right:
Row 1: open eyes with a closed tiny mouth; half-blink; fully closed blink; pleased open-eye smile.
Row 2: neutral closed mouth; small open A mouth; round wide O mouth; happy laughing mouth. Keep the body otherwise identical.
Row 3: gently vertically stretched picked-up body with a surprised face (no hand visible); startled falling expression with a slight whole-body stretch; softly squashed landing body; slightly compressed pushing body with squinting determined face (no object visible). Limbless in ALL poses.
Row 4: sleepy crouched entrance pose; curling/settling by compressing the WHOLE body (no limbs); asleep inhale with closed eyes and tiny contented mouth; asleep exhale, only subtly smaller than inhale. All four keep the same body and markings; no bedding or house.
Subject: MOCHI, a softly rounded-square moon rice-cake spirit. One chunky pillowy squircle of pale rice-cream, gently translucent rounded edges, delicate powdery mochi surface and pearlescent lunar shading, little rosy cheeks, two enormous deep midnight-blue glossy eyes, tiny sweet mouth. Distinctive tiny crescent of warm silver light embedded in its forehead, not a separate prop. It is visibly a rounded square rather than a sphere. Bottom and side corners smoothly rounded; absolutely no nub-like feet or arms. Preserve its rounded-square proportions in every state.

## Exact Puff prompt

Use case: stylized-concept.
Asset type: production whole-body companion sprite atlas for an original fantasy game companion.
Canvas: square PNG with a genuinely transparent background. Exactly FOUR columns and FOUR rows, sixteen independent sprites, no drawn grid, no cell labels, no text, no watermark.
Style: exceptionally polished storybook JRPG fantasy creature illustration, hand-painted dimensional shading, creamy luminous highlights, velvety material, gentle blush, large glossy expressive eyes, appealing readable silhouette at 96 pixels. Cute and comforting, never creepy. This is a single unified limbless body, not a parts rig.
Layout: equal-sized cells. Each complete character is centered at the same horizontal cell center with its bottom on the same baseline, consistent source scale and identity across all sixteen cells. Normal body occupies about 68% of cell width and 65% of height; preserve generous transparent gutters on all sides and between every cell. No sprite, glow or ornament crosses a cell boundary. No floor, cast ground shadow, backdrop, scenery, house, bed, stars floating outside the body, or Z letters. No arms, legs, feet, hands, tails, detached pieces or accessories. Expressions and gentle compression are the only state changes; maintain the same viewpoint and identifying markings.
Exact pose order, read left to right:
Row 1: open eyes with a closed tiny mouth; half-blink; fully closed blink; pleased open-eye smile.
Row 2: neutral closed mouth; small open A mouth; round wide O mouth; happy laughing mouth. Keep the body otherwise identical.
Row 3: gently vertically stretched picked-up body with a surprised face (no hand visible); startled falling expression with a slight whole-body stretch; softly squashed landing body; slightly compressed pushing body with squinting determined face (no object visible). Limbless in ALL poses.
Row 4: sleepy crouched entrance pose; curling/settling by compressing the WHOLE body (no limbs); asleep inhale with closed eyes and tiny contented mouth; asleep exhale, only subtly smaller than inhale. All four keep the same body and markings; no bedding or house.
Subject: PUFF, a soft mooncloud spirit. One compact cloud-shaped whole body made of softly merged rounded cloud lobes, luminous pearl-cream with moon-blue/lavender shading, downy mist-soft surface with crisp readable outer silhouette, rosy cheeks, two enormous midnight-blue glossy eyes, tiny sweet mouth. A gentle pearly crescent highlight is embedded within its forehead. All lobes are integrated into the cloud body, never limbs or detached puffs. Keep the cloud's recognizable softly scalloped outline and same lobe arrangement in all states.

## Exact Orbit prompt

Use case: stylized-concept.
Asset type: production whole-body companion sprite atlas for an original fantasy game companion.
Canvas: square PNG with a genuinely transparent background. Exactly FOUR columns and FOUR rows, sixteen independent sprites, no drawn grid, no cell labels, no text, no watermark.
Style: exceptionally polished storybook JRPG fantasy creature illustration, hand-painted dimensional shading, creamy luminous highlights, velvety material, gentle blush, large glossy expressive eyes, appealing readable silhouette at 96 pixels. Cute and comforting, never creepy. This is a single unified limbless body, not a parts rig.
Layout: equal-sized cells. Each complete character is centered at the same horizontal cell center with its bottom on the same baseline, consistent source scale and identity across all sixteen cells. Normal body occupies about 68% of cell width and 65% of height; preserve generous transparent gutters on all sides and between every cell. No sprite, glow or ornament crosses a cell boundary. No floor, cast ground shadow, backdrop, scenery, house, bed, stars floating outside the body, or Z letters. No arms, legs, feet, hands, tails, detached pieces or accessories. Expressions and gentle compression are the only state changes; maintain the same viewpoint and identifying markings.
Exact pose order, read left to right:
Row 1: open eyes with a closed tiny mouth; half-blink; fully closed blink; pleased open-eye smile.
Row 2: neutral closed mouth; small open A mouth; round wide O mouth; happy laughing mouth. Keep the body otherwise identical.
Row 3: gently vertically stretched picked-up body with a surprised face (no hand visible); startled falling expression with a slight whole-body stretch; softly squashed landing body; slightly compressed pushing body with squinting determined face (no object visible). Limbless in ALL poses.
Row 4: sleepy crouched entrance pose; curling/settling by compressing the WHOLE body (no limbs); asleep inhale with closed eyes and tiny contented mouth; asleep exhale, only subtly smaller than inhale. All four keep the same body and markings; no bedding or house.
Subject: ORBIT, a tiny ringed-planet spirit. One plump pearly pale moon-blue planet sphere with subtle creamy latitude bands and a few embedded warm-silver speckles, two enormous midnight-blue glossy eyes, rosy cheeks, tiny sweet mouth. One elegant thick rounded opalescent planetary ring encircles the body diagonally and is rendered as an inseparable rigid part of the WHOLE sprite: same angle, diameter and perspective in every frame, ring behind sphere at the back and across the lower foreground. No moons or separate satellites. Ring must not cover the eyes/mouth. The planet/ring whole fits comfortably within every cell. The ring never detaches, flexes into arms or grows new parts.

## Exact Ember prompt

Use case: stylized-concept.
Asset type: production whole-body companion sprite atlas for an original fantasy game companion.
Canvas: square PNG with a genuinely transparent background. Exactly FOUR columns and FOUR rows, sixteen independent sprites, no drawn grid, no cell labels, no text, no watermark.
Style: exceptionally polished storybook JRPG fantasy creature illustration, hand-painted dimensional shading, creamy luminous highlights, velvety material, gentle blush, large glossy expressive eyes, appealing readable silhouette at 96 pixels. Cute and comforting, never creepy. This is a single unified limbless body, not a parts rig.
Layout: equal-sized cells. Each complete character is centered at the same horizontal cell center with its bottom on the same baseline, consistent source scale and identity across all sixteen cells. Normal body occupies about 68% of cell width and 65% of height; preserve generous transparent gutters on all sides and between every cell. No sprite, glow or ornament crosses a cell boundary. No floor, cast ground shadow, backdrop, scenery, house, bed, stars floating outside the body, or Z letters. No arms, legs, feet, hands, tails, detached pieces or accessories. Expressions and gentle compression are the only state changes; maintain the same viewpoint and identifying markings.
Exact pose order, read left to right:
Row 1: open eyes with a closed tiny mouth; half-blink; fully closed blink; pleased open-eye smile.
Row 2: neutral closed mouth; small open A mouth; round wide O mouth; happy laughing mouth. Keep the body otherwise identical.
Row 3: gently vertically stretched picked-up body with a surprised face (no hand visible); startled falling expression with a slight whole-body stretch; softly squashed landing body; slightly compressed pushing body with squinting determined face (no object visible). Limbless in ALL poses.
Row 4: sleepy crouched entrance pose; curling/settling by compressing the WHOLE body (no limbs); asleep inhale with closed eyes and tiny contented mouth; asleep exhale, only subtly smaller than inhale. All four keep the same body and markings; no bedding or house.
Subject: EMBER, a round baby starflame spirit. One plump rounded flame-shaped body, warm pearl-cream heart glowing into gentle peach/apricot and muted gold edges, a single short soft teardrop curl at the top integrated into the body, velvety luminous translucency, two enormous deep violet-blue glossy eyes, warm rosy cheeks, tiny sweet mouth. Soft and cuddly like a small warm star; no sharp tongues of fire, aggressive flames, separate sparks or hard glare. The one rounded flame silhouette stays consistent, with all glow contained close to the body.

## Exact Prism prompt

Use case: stylized-concept.
Asset type: production whole-body companion sprite atlas for an original fantasy game companion.
Canvas: square PNG with a genuinely transparent background. Exactly FOUR columns and FOUR rows, sixteen independent sprites, no drawn grid, no cell labels, no text, no watermark.
Style: exceptionally polished storybook JRPG fantasy creature illustration, hand-painted dimensional shading, creamy luminous highlights, velvety material, gentle blush, large glossy expressive eyes, appealing readable silhouette at 96 pixels. Cute and comforting, never creepy. This is a single unified limbless body, not a parts rig.
Layout: equal-sized cells. Each complete character is centered at the same horizontal cell center with its bottom on the same baseline, consistent source scale and identity across all sixteen cells. Normal body occupies about 68% of cell width and 65% of height; preserve generous transparent gutters on all sides and between every cell. No sprite, glow or ornament crosses a cell boundary. No floor, cast ground shadow, backdrop, scenery, house, bed, stars floating outside the body, or Z letters. No arms, legs, feet, hands, tails, detached pieces or accessories. Expressions and gentle compression are the only state changes; maintain the same viewpoint and identifying markings.
Exact pose order, read left to right:
Row 1: open eyes with a closed tiny mouth; half-blink; fully closed blink; pleased open-eye smile.
Row 2: neutral closed mouth; small open A mouth; round wide O mouth; happy laughing mouth. Keep the body otherwise identical.
Row 3: gently vertically stretched picked-up body with a surprised face (no hand visible); startled falling expression with a slight whole-body stretch; softly squashed landing body; slightly compressed pushing body with squinting determined face (no object visible). Limbless in ALL poses.
Row 4: sleepy crouched entrance pose; curling/settling by compressing the WHOLE body (no limbs); asleep inhale with closed eyes and tiny contented mouth; asleep exhale, only subtly smaller than inhale. All four keep the same body and markings; no bedding or house.
Subject: PRISM, a rounded crystal spirit. One short chubby rounded gemstone body with broad softened facets, pearly opalescent cream and very subtle lavender/mint moonlight refraction, polished translucent dimensional surface, two enormous deep midnight-blue glossy eyes, warm pink cheeks, tiny sweet mouth. Crystal is a compact upright rounded oval/soft hexagonal pebble, not a long sharp shard, with all corners plush and smoothly rounded. Embedded inner glow stays inside the one crystal body. No floating shards, crown, horns, points or extra crystals. Preserve the exact facet layout and broad compact proportions throughout every expression.

## Exact Wink icon prompt

Use case: identity-preserve.
Asset type: one standalone transparent square application/plugin icon for OmniKit.
Input image: the supplied atlas is a CHARACTER IDENTITY REFERENCE for Wink, not the desired layout. Create ONE large centered Wink, never a grid or a sprite sheet.
Preserve Wink's existing identity: a compact pearly cream moon-spirit whole body with a soft curled upper tip, ONE enormous glossy deep-blue/violet eye, a tiny friendly happy mouth below it, gentle rosy blush, creamy iridescent dimensional highlights, and a soft blue/silver orbital ribbon close around the body with only two or three tiny warm gold glints attached to that ribbon. All elements form one clear unified icon. No arms, legs, feet, detached props, hat or other character.
Style: polished hand-painted storybook JRPG fantasy charm, adorable and inviting, exquisite softly shaded material but a simple strong silhouette readable at 32â€“96 pixels. Use the same eye shape, cream body and lunar colors as the reference. Keep the single eye open and delighted, with a small smiling mouth.
Composition: exactly one complete Wink centered, front/three-quarter view, filling about 78% of a square canvas; leave at least 10% genuinely transparent safe space around every part of the silhouette and ribbon. No cropping. No ground, cast shadow, scenery, background color, vignette or platform. Genuinely transparent PNG background.
No text, no letters, no logo lettering, no watermark, no border, no repeated characters, no atlas. This replaces an earlier mascot icon, so prioritize the recognizable face and small-size clarity.
