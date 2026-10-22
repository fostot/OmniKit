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
