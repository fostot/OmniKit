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
