# OmniKit mascot prompt provenance

Created 2026-10-22 with the built-in `image_gen.imagegen` tool using `transparent_background: true`. One generation per asset; no CLI/API fallback or image-editing pass. Source PNG alpha was preserved through byte-for-byte file copies.

## Bimble (`bimble.png`)

Default moon bunny mascot. No reference image.

```text
Use case: stylized-concept.
Asset type: final transparent PNG mascot avatar for a fantasy game companion UI, readable at 64–96 pixels.
Primary request: Bimble, an original lovable moon bunny mascot. One character only, cream ivory fur, two very prominent long rabbit ears fully inside the image, warm expressive dark eyes with small clear highlights, a friendly tiny smile, soft rounded cheeks. A little midnight-blue lunar traveling outfit with a simple capelet collar and one understated silver crescent clasp, a few restrained antique-gold accents. Fantasy storybook JRPG charm, hand-painted illustration with soft gouache shading, precise clean silhouette and simplified large readable shapes. Original character, not a reproduction of any named game NPC.
Scene/backdrop: genuinely transparent background, no scenery, no background shadow, no badge frame.
Composition: square image, centered three-quarter bust portrait turned slightly to the viewer's left while eyes engage the viewer, head and ears dominant, upper torso visible, all ear tips and silhouette contained with a small transparent margin. Crisp face at tiny icon sizes; no tiny accessories or clutter. Clean isolated cutout with natural antialiased edges and preserved alpha.
Palette: warm cream, soft blush inner ears, midnight blue, subtle silver and antique gold.
Constraints: no text, no letters, no logos, no watermark, no other characters, no props, no full-body pose, no goblin features, no checkerboard baked into image.
```

## Moon bear (`moonbear.png`)

Optional alternate mascot. The generated Bimble image was supplied as a style reference, not an edit target.

```text
Use case: stylized-concept.
Asset type: final transparent PNG alternate mascot avatar for the same fantasy game companion UI as the reference, readable at 64–96 pixels.
Input image: reference image only for matching hand-painted style, cream fur, expressive face, midnight-blue outfit, lunar trim, lighting, and palette. Create a new bear character, not a rabbit edit and not a second bunny.
Primary request: one original lovable moon bear mascot, rounded bear ears clearly visible, a pleasantly chubby small muzzle and snout with a little dark bear nose, cream ivory fur, warm expressive dark eyes with small clear highlights, a friendly smile, soft rounded cheeks. Matching little midnight-blue lunar traveling outfit with simple capelet collar, understated silver crescent clasp, restrained antique-gold accents. Fantasy storybook JRPG charm, soft hand-painted gouache shading, precise clean silhouette and simplified large readable shapes. Original mascot, not a reproduction of any named game NPC.
Scene/backdrop: genuinely transparent background, no scenery, no background shadow, no badge frame.
Composition: square image, centered three-quarter bust portrait turned slightly to the viewer's left while eyes engage the viewer, head dominant and upper torso visible. Entire ear tips, fur and silhouette inside canvas with at least 6 percent transparent margin on all sides. Crisp expressive face at tiny icon sizes, no tiny accessories or clutter. Clean isolated cutout with natural antialiased edges and preserved alpha.
Palette: warm cream, gentle warm blush, midnight blue, subtle silver and antique gold.
Constraints: no text, letters, logos or watermark; no other characters, props, full-body pose or goblin features; no rabbit ears, no checkerboard baked into image.
```

## Inspection

Both generation previews were visually inspected for species, cream fur, matching midnight-blue lunar outfits, friendly faces, bust composition, and absence of text/logos/background scenery. Both local files were passed to `view_image`; that tool's RGBA preview rendered unexpectedly dark in this session, although sampled original PNG pixels confirmed transparent exterior pixels and opaque colored subject pixels. Check 64–96 px readability in the consuming UI. Bimble's tallest ear has a tight top margin, so preserve its full square with contain sizing and avoid circular cropping.

## Walking atlas (`companions-walk.png`)

Created with the built-in image generation tool, using the two portraits as
character references. Transparent alpha is preserved. The final atlas is copied
unchanged into the project; the renderer selects four columns and two rows with
UV coordinates. Bunny frames occupy the first row, bear frames the second.
The initial atlas was rejected for tight/crossing cell bounds. One targeted
repair produced the included full-body atlas with visible stride/passing poses.
This is a four-frame 2D cycle, not a 3D model or an acoustic/AI animation system.

Initial prompt:

```text
Create a production 2D animation SPRITE ATLAS from these two character references, preserving their original identity, cream fur, friendly dark eyes, midnight-blue lunar capelets and restrained gold/silver crescent details. References define designs, but output must show FULL BODY including legs and feet. One transparent PNG with EXACTLY 4 equal-width columns and 2 equal-height rows, precisely aligned invisible regular grid. Top row: Bimble bunny in FOUR sequential frames of a looping side-view walk toward the RIGHT: left foot forward contact, passing pose, right foot forward contact, opposite passing pose. Bottom row: moon bear in the SAME four walk-cycle phases facing RIGHT. Each cell contains ONLY ONE complete isolated character. Same scale and same character position within every cell, identical foot baseline at 86% cell height; character center at50% cell width, silhouette entirely within central70%cell width and central80%height, generous transparent gutters. Bunny has long ears with all tips fully contained. Both are small cute upright biped companions, legs clearly visible below short cape, arms swinging softly with opposite legs, slight cape/ear follow-through; four distinct coherent walking poses, consistent proportions, no duplicates. Crisp clean readable simplified hand-painted storybook JRPG character sprites at96px displayedsize, matching references without over-fine fur or busy accessories. Absolutely transparent background, no shadows on ground, no floor, no landscape, no panels, no visible grid lines, no text, labels, numbers, logos, borders, badges, extra characters or cropped body parts. This output is a usable sprite atlas, NOT a concept sheet or portrait collage.
```

Repair prompt:

```text
Repair this sprite atlas for use in a real 4-column x 2-row animation grid. Preserve both character designs/style. CRITICAL FIX: every single sprite MUST fit entirely INSIDE its own equal rectangular cell, with at least15%EMPTY TRANSPARENT MARGIN on ALL FOUR sides including ear tips and feet. Reduce character size by30% inside eachcell. No part of any sprite may touch image edges or cross cellboundaries. Eachcellcharactersamephysicalsize/anchor/baseline. Toprowbunny4walkframes; bottomrowbear4walkframes. Improve the WALK CYCLE: frame1 clearly leftfootextendedforwardrightfootback, frame2 feetpassingundertorsoonelegbent, frame3 clearly rightfootforwardleftfootback and oppositearmswing, frame4 oppositepassingpose. Both faceRIGHT, completefullbodies, feetandlegsvisible; earsfollowthroughslightly. Genuine alternation, NOT fournearidenticalforwardstrides. Exactly8charactersinvisibleregulargrid, NO extras/no labels/no floor/noborders. Trulytransparentbackground. Mostimportant allsilhouettesentirelycontainedwithconsistentgutter.
```

## Extended motion and outfit atlases

Created with the built-in image generation tool. Four original RGBA PNGs, each 1254 by 1254 pixels, are preserved unchanged: `bimble-motion.png`, `moonbear-motion.png`, `bimble-stargazer.png`, and `moonbear-stargazer.png`. The first pair wears Navigator navy/gold; the second pair wears Stargazer violet/silver.

Each sheet contains 16 complete separated full-body poses, in reading order: eight walking poses, four idle/blink poses, and four speaking mouth shapes (closed, small open, wide open, round O). Walking is an illustrated loop, not skeletal animation. Talking is an explicit visual preview, not speech synthesis or audio/phoneme synchronization.

One targeted repair per base sheet corrected the initial composition/species problems. The accepted figures are separated but their row spacing is not an exact quarter-height grid. `CompanionAtlas` therefore stores inspected, padded per-frame UV rectangles, shared between each species' two outfits, and renders them at a common source-pixel scale and foot baseline. All figures, ear tips, and feet stay inside the rendered logical cell. No raster cropping, recoloring, or background removal was applied to the accepted PNGs.

All four sheets were visually inspected and composited onto charcoal for validation. Their alpha backgrounds are genuinely transparent; colored fringe artifacts in the raw RGBA preview do not appear in the composited renders. The optional magical accents and palette controls are drawn separately by the plugin, without changing these source assets. In-game animation review remains separate from this asset and source validation.


### Bimble Navigator: initial prompt

```text
Use case: stylized-concept.
Asset type: production transparent 2D sprite animation sheet, a precisely regular 4-column by 4-row grid, 16 full-body frames total. Prefer a square 2048 x 2048 canvas.
Input image 1: Bimble portrait, character identity/style reference only. Input image 2: existing eight-frame companion atlas, full-body design reference only; use ONLY its cream bunny, never its bear. Create a NEW sheet, preserve originals.
Subject: Bimble, original cream moon bunny with two long rabbit ears, warm dark eyes, small pink nose, short midnight-navy lunar capelet/outfit, silver crescent clasp and restrained antique gold trim. Cute upright biped, hand-painted storybook JRPG style matching references. Full body with legs, feet and every ear tip visible. Three-quarter profile facing RIGHT in every cell.
Layout is functional animation data: exactly 4 equal columns and 4 equal rows, no visible grid or labels. EACH cell contains exactly ONE complete bunny, identically scaled, center at 50% of cell width, foot baseline at 85% of cell height. Every silhouette stays within x=18%..82% and y=15%..85% of its OWN cell; transparent gutters on all four sides. Do not crop ears or feet or put feet on a cell boundary. Same camera, clothes, proportions and head position throughout, with only intentional animation changes.
Frames read left-to-right, top-to-bottom:
ROW 1, four distinct walking phases: left-leg-forward contact; left support/down pose with body slightly lower; left support passing pose with right knee lifted; left support up pose approaching right contact.
ROW 2, opposite half of the SAME coherent walk loop: right-leg-forward contact; right support/down pose; right support passing pose with left knee lifted; right support up pose leading back to first frame. Clearly alternate feet and opposing arm swing; subtle cape/ear follow-through. These eight poses must NOT be identical copied strides.
ROW 3, four stationary idle/blink frames: same neutral upright resting body and head anchor in all four cells, eyes fully open; eyes half closed; eyes fully closed; eyes opening. Keep mouth closed friendly smile and feet planted.
ROW 4, four speaking mouth shapes: identical neutral upright full-body pose, identical head/muzzle alignment, eyes open in ALL four. Change ONLY mouth and small jaw shape: CLOSED smile; SMALL OPEN mouth with visible dark interior; WIDE OPEN speaking mouth; clearly ROUND O mouth. The mouth differences must be readable at small sprite size. No whole-head bobbing as a substitute for mouth animation.
Genuinely transparent RGBA background, no baked checkerboard, scenery, floor, cast shadow, frame, panels, visible lines, letters, labels, numbers, watermark or extra characters. Consistent generous gutters are more important than filling the canvas. A usable 16-frame sprite atlas, not a concept collage.
```


### Moonbear Navigator: initial prompt

```text
Use case: stylized-concept.
Asset type: production transparent 2D sprite animation sheet, a precisely regular 4-column by 4-row grid, 16 full-body frames total. Prefer a square 2048 x 2048 canvas.
Input image 1: Moonbear portrait, character identity/style reference only. Input image 2: existing eight-frame companion atlas, full-body design reference only; use ONLY its cream bear, never its rabbit. Create a NEW sheet, preserve originals.
Subject: Moonbear, original pleasantly round cream moon bear with small rounded bear ears, warm dark eyes, short bear muzzle and dark bear nose, short midnight-navy lunar capelet/outfit, silver crescent clasp and restrained antique gold trim. Cute upright biped, hand-painted storybook JRPG style matching references. Full body with legs, paws and both rounded ear tips visible. Three-quarter profile facing RIGHT in every cell.
Layout is functional animation data: exactly 4 equal columns and 4 equal rows, no visible grid or labels. EACH cell contains exactly ONE complete bear, identically scaled, center at 50% of cell width, foot baseline at 85% of cell height. Every silhouette stays within x=18%..82% and y=15%..85% of its OWN cell; transparent gutters on all four sides. Do not crop ears or feet or put feet on a cell boundary. Same camera, clothes, proportions and head position throughout, with only intentional animation changes.
Frames read left-to-right, top-to-bottom:
ROW 1, four distinct walking phases: left-leg-forward contact; left support/down pose with body slightly lower; left support passing pose with right knee lifted; left support up pose approaching right contact.
ROW 2, opposite half of the SAME coherent walk loop: right-leg-forward contact; right support/down pose; right support passing pose with left knee lifted; right support up pose leading back to first frame. Clearly alternate feet and opposing arm swing; subtle cape follow-through. These eight poses must NOT be identical copied strides.
ROW 3, four stationary idle/blink frames: same neutral upright resting body and head anchor in all four cells, eyes fully open; eyes half closed; eyes fully closed; eyes opening. Keep mouth closed friendly smile and feet planted.
ROW 4, four speaking mouth shapes: identical neutral upright full-body pose, identical head/muzzle alignment, eyes open in ALL four. Change ONLY mouth and small jaw shape: CLOSED smile; SMALL OPEN mouth with visible dark interior; WIDE OPEN speaking mouth; clearly ROUND O mouth. The mouth differences must be readable at small sprite size. No whole-head bobbing as a substitute for mouth animation.
Genuinely transparent RGBA background, no baked checkerboard, scenery, floor, cast shadow, frame, panels, visible lines, letters, labels, numbers, watermark or extra characters. No rabbit ears. Consistent generous gutters are more important than filling the canvas. A usable 16-frame sprite atlas, not a concept collage.
```


### Bimble: targeted repair

```text
Repair this animation sprite atlas for a real 4-column x 4-row UV grid. First image is the defective sheet to correct; second image is Bimble's identity reference. TWO SPECIFIC ERRORS MUST BE FIXED. 1: EVERY ONE OF THE 16 SPRITES must be the SAME BUNNY Bimble with long rabbit ears, including all idle and talking cells in rows 3 and 4. There must be ZERO BEARS, zero round-eared characters, on this sheet. Use reference 2 to restore the bunny identity. 2: Current upper-row feet cross their cell boundaries. Reduce EACH full-body sprite's size by 30% inside its own cell and align every anchor to the exact regular 4x4 grid. Cell boundaries are at 25%,50%,75% of total canvas width and height. Every sprite including BOTH EAR TIPS and FEET must stay inside the middle 65% of its own equal cell, with at least 15% transparent margin on EVERY side. Match uniform size and baseline at 82% of each cell height; no cropping or row overlap.
Keep 16 distinct poses: first two rows = eight sequential walking phases facing RIGHT with alternating feet/arms and passing/up/down poses; third row = neutral standing bunny, eyes open/half closed/closed/opening; fourth row = IDENTICALLY ALIGNED neutral standing bunny, eyes open, with only four clearly different mouth shapes closed/small open/wide open/round O. Preserve cream fur, warm dark eyes, pink rabbit nose, midnight-navy short lunar outfit and cape, silver crescent clasp, restrained gold trim. Full bodies, consistent camera/proportions/style. All 16 are BUNNIES. Exact invisible regular 4x4 grid. No labels, text, frames, scenery, floors, shadows, extra figures. Genuinely transparent alpha background.
```


### Moonbear: targeted repair

```text
Repair only the production layout and pose clarity of this Moonbear animation sprite atlas. It must be usable in an EXACT 4-column x 4-row UV grid. Current upper-row feet cross their cell boundaries. Reduce EACH full-body bear sprite by 30% INSIDE ITS OWN CELL and align every anchor to the exact regular 4x4 grid. Cell boundaries are at 25%,50%,75% of total canvas width and height. Every sprite including EAR TIPS and FEET stays inside the middle 65% of its own equal cell, at least 15% genuinely transparent margin on EVERY side. Uniform physical size and baseline at 82% of each cell height; no cropping, no boundary crossing, no row overlap. All 16 characters are the SAME cream Moonbear from this input, with the SAME navy lunar clothes and gold/silver trim.
Keep the functional poses: rows 1 and 2 form eight distinct sequential walking phases facing RIGHT, alternating feet/arms with contact/down/passing/up positions on each side; all walking eyes open. Row 3 has four identical neutral standing bodies and aligned heads, eyes open/half closed/closed/opening. Row 4 has IDENTICAL neutral standing bodies and aligned heads with open eyes, only mouth changes: closed smile/small open/wide open/round O. Preserve mouth distinctions, friendly bear face, rounded bear ears, dark bear nose, short midnight-blue lunar outfit/cape, crescent clasp, illustration style. Full bodies only, no portrait crops, no rabbit ears. No labels, text, frames, scenery, floors, cast shadows, extra figures. Genuinely transparent alpha background. Grid correctness and generous transparent cell gutters are the priorities.
```


### Stargazer clothing variation: prompt applied once to each accepted base

```text
Use case: precise-object-edit. This input is an accepted 16-frame 4-by-4 animation sprite atlas. Change ONLY the character's clothing to a Stargazer outfit: a rich violet-purple hoodless short mage robe/capelet with silver trim, silver crescent clasp and a few restrained silver star details. Keep legs and feet fully visible below the short robe so walking remains readable. Replace navy/gold fabric/trim with violet/silver throughout. No hood, hat, staff, wand, props, new effects or added objects.
Preserve EXACTLY all 16 characters, their species/face/fur/ears, each frame's mouth and eye shape, poses, existing silhouette size, pixel placement/spacing, exact image dimensions and transparent background. First 8 frames walk, next 4 blink, last 4 have closed/small-open/wide-open/round-O mouths. All frames face RIGHT. Do NOT reorder frames, reposition figures, add text or merge sprites. Clothing-only variation of the input, identical animation layout and anchors. Truly transparent PNG alpha.
```


## Companion frame interaction and reaction artwork

The accepted files `bimble-physical.png`, `bimble-physical-stargazer.png`, `moonbear-physical.png`, and `moonbear-physical-stargazer.png` each contain 24 full-body poses: eight walking, four idle/blink, four talking-mouth previews, and two each for carry, fall, land, and push. Each PNG is 1024 × 1536 with genuine RGBA transparency. Source images are preserved byte for byte. `lunar-companions.frames.json` records inspected per-frame rectangles, body anchors, shared source-pixel scales, and source hashes; the renderer does not assume a perfect quarter grid or resize every crop independently.

Six generation calls were used: two character bases, two initial outfit variations, one targeted Bimble separation repair, and a replacement Stargazer variation of that repaired base. The initial Bimble pair was excluded because adjacent bodies overlapped. The accepted poses were inspected on charcoal at logical 96-pixel display size; every crop includes the full silhouette with edge alpha at most 7/255. Ear flex is illustrated across frames; mouth poses are explicit previews, not phoneme-synchronized speech.

### Bimble physical animation base

```text
Create a new production 2D character sprite atlas, using input only as design/style reference. EXACTLY 4 columns by 6 rows = 24 separate full-body sprites on one tall transparent PNG, target aspect 2:3. Every figure fits inside its own equal cell with at least 14% empty margins including ear tips and feet. One character per cell, no grid lines or labels, no props, no floor or cast shadow. Constant physical scale and body center/foot baseline across cells; poses may squash slightly intentionally. Friendly highly polished hand-painted JRPG illustration, warm ivory fur, navy short lunar capelet with gold trim and silver crescent clasp. Face RIGHT, consistent three-quarter side view. Real animation keyframes, not duplicate images: alternating front/back legs clearly distinguish left contact, down, passing, up then opposite contact, down, passing, up. Keep torso/head volume and face stable to avoid flicker. Rows1–2: these 8 sequential walking phases. Row3: neutral standing idle poses eyes open, half-closed, fully closed, reopening. Row4: identical aligned standing body/head with eyes open and mouth closed, small open, wide open, round O. Row5: cells1–2 CARRY held up by invisible grip at upper back: shoulders raised, legs dangling, surprised but cute expression, small alternating leg wiggle; cells3–4 FALL with limbs out and cape lifting, surprised open mouth, wind-tossed fur. Row6: cells1–2 LAND compress into soft crouch then recovering standing posture, feet firmly planted; cells3–4 PUSH an invisible wall to the RIGHT: both paws extended, leaning body with feet braced, first effort then stronger effort, adorable determined face. Absolutely no drawn wall/hand/grip/ground or text. Every sprite is a complete individual with natural translucent antialiased edge on truly transparent alpha. Character is BIMBLE BUNNY, long rabbit ears, small pink rabbit nose, no bears anywhere. Most important new improvement: long ears are flexible living anatomy, not rigid identical sticks. The 8 walk frames show delayed ear follow-through: ear roots follow skull while tips bend backward/down on impact and recover with slight overshoot one phase later, the two ears respond at slightly different phases. Vary ears naturally in idle, carry, fall and landing; never cut them off. Preserve recognizably the SAME bunny face and costume.
```

### Moonbear physical animation base

```text
Create a new production 2D character sprite atlas, using input only as design/style reference. EXACTLY 4 columns by 6 rows = 24 separate full-body sprites on one tall transparent PNG, target aspect 2:3. Every figure fits inside its own equal cell with at least 14% empty margins including ear tips and feet. One character per cell, no grid lines or labels, no props, no floor or cast shadow. Constant physical scale and body center/foot baseline across cells; poses may squash slightly intentionally. Friendly highly polished hand-painted JRPG illustration, warm ivory fur, navy short lunar capelet with gold trim and silver crescent clasp. Face RIGHT, consistent three-quarter side view. Real animation keyframes, not duplicate images: alternating front/back legs clearly distinguish left contact, down, passing, up then opposite contact, down, passing, up. Keep torso/head volume and face stable to avoid flicker. Rows1–2: these 8 sequential walking phases. Row3: neutral standing idle poses eyes open, half-closed, fully closed, reopening. Row4: identical aligned standing body/head with eyes open and mouth closed, small open, wide open, round O. Row5: cells1–2 CARRY held up by invisible grip at upper back: shoulders raised, legs dangling, surprised but cute expression, small alternating leg wiggle; cells3–4 FALL with limbs out and cape lifting, surprised open mouth, wind-tossed fur. Row6: cells1–2 LAND compress into soft crouch then recovering standing posture, feet firmly planted; cells3–4 PUSH an invisible wall to the RIGHT: both paws extended, leaning body with feet braced, first effort then stronger effort, adorable determined face. Absolutely no drawn wall/hand/grip/ground or text. Every sprite is a complete individual with natural translucent antialiased edge on truly transparent alpha. Character is MOONBEAR, rounded small bear ears and short bear snout with dark bear nose, no rabbits anywhere. Gentle belly, fur and cape follow-through with weight shifting, rounded ears squash slightly with expression. Preserve recognizably the SAME bear face and costume.
```

### Bimble layout repair

```text
Repair this exact BIMBLE BUNNY 24-frame sprite atlas for safe animation slicing. Concrete defect: the fourth row mouth sprites' feet touch/merge into fifth row carry/fall ears, so they cannot be separated. Re-layout ALL 24 existing poses into EXACTLY FOUR EQUAL COLUMNS by SIX EQUAL ROWS on a 1024x1536 transparent PNG. SHRINK EVERY FIGURE by 25% inside its cell. Cell center is at (50%,50%), foot baseline at82% of cellheight. Silhouette including both complete long ears, paws, cape and feet must stay within x=16%..84%, y=12%..84% of its OWN256x256cell. At least 24pixels truly empty space around every figure. ALL24sprites must be disconnected with no touching or crossing cell boundaries. Preserve Bunny identity, cream fur/pinknose/navy-gold lunar outfit, flexible varied ear poses, pose order and all original mouth shapes. First8frames walking clear alternating feet and opposing arm swing, all eyesOPEN with varied flexible ear followthrough; next4idleblink; next4mouthclosed/smallopen/wideopen/roundO; next2Carry with dangling feet; next2Fall; next2Land crouch and recovery; last2Push right with paws extended. Stable head/torso volume and physical scale for smooth animation. Do not introduce bears or other species, text, labels, grid lines, ground, cast shadows, props or background. Pure transparent alpha exterior. Layout correctness and complete isolated silhouettes take priority over filling canvas.
```

### Physical animation Stargazer clothing variation

```text
Edit this exact 24-frame sprite atlas: change ONLY every navy/gold outfit into the Stargazer clothing palette/style, rich violet-purple short hoodless lunar mage capelet/robe with silver trim, silver crescent clasp and restrained silver stars. Maintain EXACTLY the same character species, all 24 complete poses in their current order, every eye and mouth state, flexible ear shape, limb pose, physical scale, pixel placement, cell layout and dimensions. Do not change fur, anatomy, facial identity, move/crop/resize any figure, add props, change frame count, or add words. Keep short clothing above feet/legs, no hood. Genuinely transparent PNG alpha exterior preserved; NO background, no painted brown rectangle, no checkerboard, no ground shadows. This is a clothing-only alternate atlas for identical runtime coordinates.
```
