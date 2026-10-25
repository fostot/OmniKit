# Painted companion wardrobe provenance

All thirteen PNGs are unchanged raw outputs from the built-in image_gen tool,
with real RGBA transparency. No source pixels were painted, erased, resampled,
or repacked offline. Complete bodies and hats were generated together. Opaque
sprite packing failures were repaired through one targeted built-in edit per
affected sheet; rejected raw originals remain preserved locally and are excluded
from the plugin and source publication.

Each sheet contains five rows (bare, witch, Starlight cap, top hat, straw hat) and
eight complete pose columns (neutral, move A/B, closed blink, carry A/B, enter bed,
wake). The metadata measures actual body bounds separately from the full costume
so hats may extend above the body without shrinking it. All thirteen bare rows
support removing painted headwear for naps; everyday original artwork remains
unchanged. Existing bows remain separate editable accessories.

Only public character/accessory assets were used as references. No game captures,
profiles, account content or credentials were supplied. PNG provenance metadata
contains public tool/software identifiers, hashes and signing data. Measurement
and native rendering are separate from live FFXIV appearance verification.

## Accepted raw artwork

| Companion | File | Dimensions | SHA-256 |
| --- | --- | --- | --- |
| Moonblob | `moonblob-wardrobe.png` | 1586 x 992 | `5034aa0e69c650a232396e4b8f3b33fcd4d88f4359c9026ea0464f18ac0dea9d` |
| Wink | `wink-wardrobe.png` | 1586 x 992 | `f2eb6e8690b514086a00454909e2a5e52bf3d3a3ea80b2c7a15819269377b18d` |
| Nova | `nova-wardrobe.png` | 1587 x 991 | `87f154ab31f99bf13ac9597bf59e70f35881829beedf87169cc9d50852ea52e2` |
| Mochi | `mochi-wardrobe.png` | 1586 x 992 | `eb09ea7407895440eb79c3aa80bcba6f1526ce5215615acd4ea7a95d15810ceb` |
| Puff | `puff-wardrobe.png` | 1585 x 992 | `974a2dc17f805fc6ad64e178292c4cbe0f04da183e00b037e918d5f95cc2a540` |
| Orbit | `orbit-wardrobe.png` | 1585 x 992 | `d8964ce8814408e4bcc1165c5699bd409170c28503b0d90806ebf26375896735` |
| Ember | `ember-wardrobe.png` | 1586 x 992 | `a72589c7dc26a6c0d6c1db3fd427215ead1a7d8eefef90a4427e4516bcd1ee1f` |
| Prism | `prism-wardrobe.png` | 1586 x 992 | `94e71d2f0b516bb824208c6bd8a643dad57ee95d65765feca34890426474f9ad` |
| Nib | `nib-wardrobe.png` | 1586 x 992 | `caafe770f54bf49cc8c46a9ec07508bbd13fae496661f4f7d5433cafdb8a2bcf` |
| Echo | `echo-wardrobe.png` | 1585 x 992 | `f1f495208c2340d23c20621349816f1c0bc3882e2caca9ffaa54694cd34336f8` |
| Fizz | `fizz-wardrobe.png` | 1585 x 992 | `91d2c531d867fd41f56ad5e31501d5aabd05740a79884dc41f0aab2a05be0c99` |
| Glim | `glim-wardrobe.png` | 1585 x 992 | `d2a175b5ea18ebe68776aa61e26bc7e10bf30b110ea64e0e36f2b4d56c622019` |
| Rune | `rune-wardrobe.png` | 1585 x 992 | `1774620b23b1805a05c4d6257a98c5de9273a632da6819a263db6c7023212c3b` |

## Moonblob

Accepted generation or edit:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
SUBJECT: MOONBLOB is a perfectly LIMBLESS soft spherical moon-jelly, translucent pearly ivory glass with faint lavender-blue internal stardust, warm golden crescent-shaped light inclusions, TWO large dark sparkling eyes and a tiny smiling mouth with pink cheeks. Its coherent smooth ROUND blob outline has no feet, paws, arms, ears or other appendages. Match the reference's soft lunar glow and recognisable face. Row 1 is this plain Moonblob, upright rather than rolling. Hats are painted around the rounded crown in the other four rows.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Each complete body AND hat occupies at MOST 72% of each equal grid cell in width and height. At least 14% of cell width and height is empty transparent padding on EVERY side. Clear wide transparent gutters separate all cells; outermost cells also have full padding. No cropped brim tips. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. REFERENCE ROLES: Image 1 is the identity reference ONLY: preserve this subject's face, body material and anatomy; do not copy its background or sheet layout. Image 2 is the accepted Mochi wardrobe sheet ONLY for the five-by-eight layout, painted-on hat integration and shading quality; DO NOT copy Mochi's body, two-eye anatomy or forehead crescent onto a different character. Image 3 is ONLY a hat-style reference for the witch hat, Santa cap, navy top hat and straw sunhat; do not render its detached accessories. Create the described subject alone in all 40 complete cells, on true transparent alpha.
```


## Wink

Accepted generation or edit:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
SUBJECT: WINK is a LIMBLESS pearly ivory moon-wisp with a softly curled ghost-like silhouette and EXACTLY ONE large CENTRAL sapphire-blue eye with a dark upper lid, a tiny cute mouth below, rosy cheeks, subtle gold/lilac luminosity. EVERY sprite has precisely ONE eye or ONE closed eyelid arc, never two eyes. No arms, legs, feet, paws or ears. Do not include the reference's detached orbital lights or floating particles. Keep the central eye readable beneath all complete front brims; hats are painted fitted around its upper crown.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Each complete body AND hat occupies at MOST 72% of each equal grid cell in width and height. At least 14% of cell width and height is empty transparent padding on EVERY side. Clear wide transparent gutters separate all cells; outermost cells also have full padding. No cropped brim tips. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. REFERENCE ROLES: Image 1 is the identity reference ONLY: preserve this subject's face, body material and anatomy; do not copy its background or sheet layout. Image 2 is the accepted Mochi wardrobe sheet ONLY for the five-by-eight layout, painted-on hat integration and shading quality; DO NOT copy Mochi's body, two-eye anatomy or forehead crescent onto a different character. Image 3 is ONLY a hat-style reference for the witch hat, Santa cap, navy top hat and straw sunhat; do not render its detached accessories. Create the described subject alone in all 40 complete cells, on true transparent alpha.
```


## Nova

Accepted generation or edit:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
SUBJECT: NOVA is a tiny LIMBLESS soft five-point star-wisp, pearly ivory with pastel lavender-blue edges, internal golden stardust, TWO large sapphire-blue eyes, a tiny happy mouth and pink cheeks. Five BROAD rounded points are the connected body silhouette, not separate arms or legs; the top point softly curls. Match the recognisable soft star shape and luminous face of the identity reference. No limbs, feet, paws, ears or detached particles. Each hat is painted fitted around the top star point without hiding the face.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Each complete body AND hat occupies at MOST 72% of each equal grid cell in width and height. At least 14% of cell width and height is empty transparent padding on EVERY side. Clear wide transparent gutters separate all cells; outermost cells also have full padding. No cropped brim tips. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. REFERENCE ROLES: Image 1 is the identity reference ONLY: preserve this subject's face, body material and anatomy; do not copy its background or sheet layout. Image 2 is the accepted Mochi wardrobe sheet ONLY for the five-by-eight layout, painted-on hat integration and shading quality; DO NOT copy Mochi's body, two-eye anatomy or forehead crescent onto a different character. Image 3 is ONLY a hat-style reference for the witch hat, Santa cap, navy top hat and straw sunhat; do not render its detached accessories. Create the described subject alone in all 40 complete cells, on true transparent alpha.
```


## Mochi

Accepted generation or edit:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
Mochi: the reference is its identity/style. An adorable soft pale ivory mochi dumpling, rounded squat single limbless body, two large glossy navy-violet eyes, rosy cheeks and tiny friendly mouth. NO arms, legs, ears, rabbit/bear parts or separate appendages. Keep its creamy mochi skin and recognizable cute face in every outfit.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Clear transparent gutters between all cells. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. Image 1 is Mochi's identity reference, not an atlas layout to preserve. Image 2 is a reference only for the four hat designs; draw the hats correctly ON Mochi, not as separate accessory sprites. Do not reproduce the bows-only accessories.
```

Spacing edit:

```text
Use case: precise-object-edit. Edit this production transparent RGBA sprite atlas. Keep EXACT same Mochi character, all five outfits, all eight poses, the layout of FIVE rows and EIGHT columns, the painted hats on the bodies, the faces/materials and polished art style. Change ONLY layout spacing and sprite size: shrink EVERY complete sprite, including hat, to about 72% of its current width/height inside its OWN unchanged equal grid cell, and center it in that cell. Each cell MUST have at least 15% of its cell width and height as completely transparent empty padding on EVERY edge. Large clear transparent gaps between every neighboring sprite and every row. Absolutely every hat tip, brim edge, pompom, flower and body must fit completely, including the leftmost/rightmost columns and top/bottom rows; restore any cropped-off brim tips so complete. No sprite may touch another sprite or the outer image border. Preserve the same apparent BODY mass across all cells; shrink all cells uniformly including the hats rather than different scaling per pose. Preserve genuine transparency, no checkerboard painted in, no grid lines, captions, labels, shadows, backdrop or added objects. Prefer 2400x1500 or larger, 8:5 aspect, exactly40 sprites.
```


## Puff

Initial generation:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
Subject: PUFF, the existing adorable soft moon-cloud spirit from reference 2. One connected pillowy cloud body with scalloped cloud lobes, ivory-pearl cream and soft lavender-blue shadows, little golden crescent on forehead, two large glossy blue-violet eyes, blush cheeks and tiny sweet mouth. No arms, legs, hands or feet. Preserve Puff's cloud silhouette and cloud surface, not Mochi's square rice-cake form.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Each complete body AND hat occupies at MOST 72% of each equal grid cell in width and height. At least 14% of cell width and height is empty transparent padding on EVERY side. Clear wide transparent gutters separate all cells; outermost cells also have full padding. No cropped brim tips. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. REFERENCE IMAGE ROLES: Image 1 mochi-wardrobe is ONLY the accepted 5-row/8-column composition, pose ordering, rendering quality and hat fitting reference; do NOT copy Mochi's character identity. Image 2 is the sole identity/material reference for this subject. Image 3 seasonal-accessories is ONLY reference for the four requested hat materials and designs; DO NOT output detached hats or accessory pieces. The output must consist solely of forty complete Puff sprites.
```

Accepted generation or edit:

```text
Edit the supplied Puff complete-character wardrobe atlas ONLY to repair insufficient transparent spacing. Preserve the same 40 complete painted character illustrations, character identity, palette, facial expressions, costume materials, hats, FULL uncut front brims, exactly FIVE rows and EIGHT columns and the exact existing pose order. IMPORTANT: make EVERY entire body-plus-hat sprite EXACTLY HALF (50%) its current width and half its current height relative to its grid cell. This is a substantial spacing reduction, NOT a subtle shrink. Keep each sprite centered in its own corresponding cell and keep the same consistent baseline within each row. Do not shrink the body relative to its hat: scale the COMPLETE painted sprite uniformly together. Return a transparent RGBA atlas in the same landscape 8:5 composition, preferably3072x1920. Every complete sprite including the longest brim, ribbon, pompom and glow must fit inside the CENTRAL50% of its cell width and height, leaving AT LEAST25% entirely empty transparent margin on EACH side. Every neighboring sprite must be separated by a broad clean transparent gutter. No contact, no overlap, no residual colored pixels in the gutters, no cropped or missing brim pieces, no ghost copies, no extra objects, no words or grid lines. Preserve all forty poses and both eyes per face, including column4 fully closed eyes. Do not change the 5 outfit rows: plain, purple witch, red Santa, navy/gold top hat, straw/teal flower sunhat. Do not redesign or reinterpret the subject. Only uniformly reduce and recenter each complete sprite within the five-by-eight grid, preserving all painted hat-body details. Output actual alpha transparency.
```


## Orbit

Accepted generation or edit:

```text
Use case: precise-object-edit. Edit ONLY the spacing and scale of the 40 existing Orbit sprites in this transparent RGBA atlas. Preserve exactly FIVE rows by EIGHT columns, the same row order and all eight expressions/poses, the same rounded blue/ivory RINGLESS planet identity, the same four correctly painted fitted hats and continuous complete front brims. Row1 plain, row2 purple witch hat, row3 red Santa cap, row4 navy gold top hat, row5 teal ribbon/hibiscus straw sunhat. CRITICAL REPAIR: the current bottom-row straw brims touch each other. Make EVERY entire sprite, including its whole hat and every brim tip, exactly HALF of its CURRENT visual width and height. Keep each sprite centered within its original fixed grid cell. Do not tighten or rearrange the grid to compensate. Vast empty transparent gutters are intentional and required: each complete body+hat must occupy at most 45% of its equal cell width and height; at least 25% clear alpha-zero margin on EVERY side. All 40 must be clearly separated individual components. Keep the same common body scale across every row and pose; tall hats add height, never shrink only the body. No cropping, no missing or bitten front brims, no merged shapes, no rings, no new characters, no extra eyes, no text/labels/grid lines/background/shadows. The ENTIRE canvas is transparent between cells. Preserve full bodies and the existing hat materials, illumination and cute face styling; this is a spacing correction, not a redesign. Prefer a clean landscape 8:5 sheet at high resolution.
```


## Ember

Accepted generation or edit:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
Subject: EMBER, the existing adorable rounded baby star-flame from reference 2. One connected plump warm golden-orange flame body with a curling flame crown, pale buttery luminous face, two large glossy purple-blue eyes, blush cheeks and tiny sweet mouth. No arms, legs, hands or feet. Whole flame shape squishes and softly curls in movement; preserve rounded flame identity beneath fitted hats. Do not turn Ember into a cream rice cake or cloud.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Each complete body AND hat occupies at MOST 72% of each equal grid cell in width and height. At least 14% of cell width and height is empty transparent padding on EVERY side. Clear wide transparent gutters separate all cells; outermost cells also have full padding. No cropped brim tips. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. REFERENCE IMAGE ROLES: Image 1 mochi-wardrobe is ONLY the accepted 5-row/8-column composition, pose ordering, rendering quality and hat fitting reference; do NOT copy Mochi's character identity. Image 2 is the sole identity/material reference for this subject. Image 3 seasonal-accessories is ONLY reference for the four requested hat materials and designs; DO NOT output detached hats or accessory pieces. The output must consist solely of forty complete Ember sprites.
```


## Prism

Initial generation:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
Subject: PRISM, the existing adorable rounded crystal spirit from reference 2. One connected plump softly rounded gem body with gently pointed crown, pearly pastel rainbow crystalline facets, warm ivory central face, two large glossy blue-violet eyes, blush cheeks and tiny sweet mouth. No limbs, hands, feet or separate shards. Preserve its translucent opal-crystal material and rounded faceted silhouette beneath hats, not Mochi's rice-cake form.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Each complete body AND hat occupies at MOST 72% of each equal grid cell in width and height. At least 14% of cell width and height is empty transparent padding on EVERY side. Clear wide transparent gutters separate all cells; outermost cells also have full padding. No cropped brim tips. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. REFERENCE IMAGE ROLES: Image 1 mochi-wardrobe is ONLY the accepted 5-row/8-column composition, pose ordering, rendering quality and hat fitting reference; do NOT copy Mochi's character identity. Image 2 is the sole identity/material reference for this subject. Image 3 seasonal-accessories is ONLY reference for the four requested hat materials and designs; DO NOT output detached hats or accessory pieces. The output must consist solely of forty complete Prism sprites.
```

Accepted generation or edit:

```text
Edit the supplied Prism complete-character wardrobe atlas ONLY to repair insufficient transparent spacing. Preserve the same 40 complete painted character illustrations, character identity, palette, facial expressions, costume materials, hats, FULL uncut front brims, exactly FIVE rows and EIGHT columns and the exact existing pose order. IMPORTANT: make EVERY entire body-plus-hat sprite EXACTLY HALF (50%) its current width and half its current height relative to its grid cell. This is a substantial spacing reduction, NOT a subtle shrink. Keep each sprite centered in its own corresponding cell and keep the same consistent baseline within each row. Do not shrink the body relative to its hat: scale the COMPLETE painted sprite uniformly together. Return a transparent RGBA atlas in the same landscape 8:5 composition, preferably3072x1920. Every complete sprite including the longest brim, ribbon, pompom and glow must fit inside the CENTRAL50% of its cell width and height, leaving AT LEAST25% entirely empty transparent margin on EACH side. Every neighboring sprite must be separated by a broad clean transparent gutter. No contact, no overlap, no residual colored pixels in the gutters, no cropped or missing brim pieces, no ghost copies, no extra objects, no words or grid lines. Preserve all forty poses and both eyes per face, including column4 fully closed eyes. Do not change the 5 outfit rows: plain, purple witch, red Santa, navy/gold top hat, straw/teal flower sunhat. Do not redesign or reinterpret the subject. Only uniformly reduce and recenter each complete sprite within the five-by-eight grid, preserving all painted hat-body details. Output actual alpha transparency.
```


## Nib

Initial generation:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
Subject: NIB, a NEW original cute plump black-indigo INKDROP spirit. One connected pear/teardrop-shaped body, glossy rich black-indigo liquid ink with a gentle curled droplet crown and subtle iridescent violet-blue-silver ink edges. Two clearly separated large friendly glossy white-silver eyes with dark pupils and tiny cute mouth, a faint warm blush. No arms, legs, hands, feet, wings or appendages. Nib must remain visibly dark ink in all five rows, never cream, cloud, gem or Mochi; preserve both eyes even when fitted hats are worn.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Each complete body AND hat occupies at MOST 72% of each equal grid cell in width and height. At least 14% of cell width and height is empty transparent padding on EVERY side. Clear wide transparent gutters separate all cells; outermost cells also have full padding. No cropped brim tips. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. REFERENCE IMAGE ROLES: Image 1 mochi-wardrobe is ONLY the accepted 5-row/8-column composition, pose ordering, rendering quality and hat fitting reference; do NOT copy Mochi's character identity. Image 2 seasonal-accessories is ONLY reference for the four requested hat materials and designs; DO NOT output detached hats or accessory pieces. Nib is the new dark inkdrop described above, and the output must consist solely of forty complete Nib sprites.
```

Accepted generation or edit:

```text
Edit the supplied Nib complete-character wardrobe atlas ONLY to repair insufficient transparent spacing. Preserve the same 40 complete painted character illustrations, character identity, palette, facial expressions, costume materials, hats, FULL uncut front brims, exactly FIVE rows and EIGHT columns and the exact existing pose order. IMPORTANT: make EVERY entire body-plus-hat sprite EXACTLY HALF (50%) its current width and half its current height relative to its grid cell. This is a substantial spacing reduction, NOT a subtle shrink. Keep each sprite centered in its own corresponding cell and keep the same consistent baseline within each row. Do not shrink the body relative to its hat: scale the COMPLETE painted sprite uniformly together. Return a transparent RGBA atlas in the same landscape 8:5 composition, preferably3072x1920. Every complete sprite including the longest brim, ribbon, pompom and glow must fit inside the CENTRAL50% of its cell width and height, leaving AT LEAST25% entirely empty transparent margin on EACH side. Every neighboring sprite must be separated by a broad clean transparent gutter. No contact, no overlap, no residual colored pixels in the gutters, no cropped or missing brim pieces, no ghost copies, no extra objects, no words or grid lines. Preserve all forty poses and both eyes per face, including column4 fully closed eyes. Do not change the 5 outfit rows: plain, purple witch, red Santa, navy/gold top hat, straw/teal flower sunhat. Do not redesign or reinterpret the subject. Only uniformly reduce and recenter each complete sprite within the five-by-eight grid, preserving all painted hat-body details. Output actual alpha transparency.
```


## Echo

Accepted generation or edit:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
NEW companion ECHO: an adorable soft lavender translucent ghost-wisp. One connected rounded pear-shaped body flowing into a short gently curled lower tip, two large glossy violet-blue eyes, rosy cheeks and tiny smiling mouth. Mint-white glow within soft lavender translucent skin. No arms, hands, feet, wings or separate floating parts. Identity distinct from Mochi, a fluid ghost silhouette not a cream dumpling. Hats sit correctly on its rounded upper crown.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Each complete body AND hat occupies at MOST 72% of each equal grid cell in width and height. At least 14% of cell width and height is empty transparent padding on EVERY side. Clear wide transparent gutters separate all cells; outermost cells also have full padding. No cropped brim tips. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. Image 1 is ONLY the layout and painterly sprite-rendering style reference of Mochi; DO NOT copy its body, face shape or cream mochi material. Create the NEW subject specified above with a distinct shape and material. Image 2 is ONLY the four hat designs. Paint the hats directly and correctly ON the subject in every costume pose. Keep EXACT FIVE rows by EIGHT columns with wide transparent gaps.
```

Spacing edit:

```text
Use case: precise-object-edit. Correct the packing of this EXACT production sprite sheet, preserving the character's identity, face, material, every complete painted hat design and the EXACT existing FIVE rows/EIGHT columns/40 poses. CRITICAL: adjacent brim tips currently touch, making sprites unusable. Make EACH COMPLETE body-plus-hat sprite VERY MUCH SMALLER: HALF of its present width AND HALF its present height, centered independently inside its SAME fixed grid cell. The sprites should look MINIATURE with HUGE TRANSPARENT EMPTY GAPS. Each complete sprite can occupy no more than 55% of its cell width or height, leaving at least22% of each cell empty on EVERY edge. Do not enlarge them to fill space. All40sprites must be completely separate with a broad transparent channel between every pair of neighbors and every row, plus transparent outside margins. Keep the full curved brim of every hat, the full pompom/cap, and all body parts fully visible; no overlap, no clipping, no cutouts, no missing hat pixels. EXACT same FIVE rows and EIGHT columns, 40miniature sprites, transparent RGBA background. Keep the SAME scale reduction uniformly across all40 cells, keeping body mass consistent across poses and outfits; no per-pose scale changes. No extra characters, no captions, no labels, no grid, no checkerboard, no props/ground shadows. This edit ONLY fixes spacing and shrinks the complete sprites; no redesign. Prefer very high resolution 3172x1984 so these small well-spaced sprites retain crisp detail.
```


## Fizz

Accepted generation or edit:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
NEW companion FIZZ: an adorable living soap bubble. Perfectly rounded thin transparent glassy bubble body, delicate rainbow-iridescent rim with cyan/pink/pearl highlights, clear airy inner chamber, two large glossy midnight-blue eyes, rosy cheeks and tiny smile visibly on front film. The rainbow rim is part of the single bubble, no extra ring orbiting outside. No arms, legs, ears, tentacles or separate appendages. Airy soap-film appearance rather than solid mochi/jelly; keep cute readable face.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Each complete body AND hat occupies at MOST 72% of each equal grid cell in width and height. At least 14% of cell width and height is empty transparent padding on EVERY side. Clear wide transparent gutters separate all cells; outermost cells also have full padding. No cropped brim tips. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. Image 1 is ONLY the layout and painterly sprite-rendering style reference of Mochi; DO NOT copy its body, face shape or cream mochi material. Create the NEW subject specified above with a distinct shape and material. Image 2 is ONLY the four hat designs. Paint the hats directly and correctly ON the subject in every costume pose. Keep EXACT FIVE rows by EIGHT columns with wide transparent gaps.
```

Spacing edit:

```text
Use case: precise-object-edit. Correct the packing of this EXACT production sprite sheet, preserving the character's identity, face, material, every complete painted hat design and the EXACT existing FIVE rows/EIGHT columns/40 poses. CRITICAL: adjacent brim tips currently touch, making sprites unusable. Make EACH COMPLETE body-plus-hat sprite VERY MUCH SMALLER: HALF of its present width AND HALF its present height, centered independently inside its SAME fixed grid cell. The sprites should look MINIATURE with HUGE TRANSPARENT EMPTY GAPS. Each complete sprite can occupy no more than 55% of its cell width or height, leaving at least22% of each cell empty on EVERY edge. Do not enlarge them to fill space. All40sprites must be completely separate with a broad transparent channel between every pair of neighbors and every row, plus transparent outside margins. Keep the full curved brim of every hat, the full pompom/cap, and all body parts fully visible; no overlap, no clipping, no cutouts, no missing hat pixels. EXACT same FIVE rows and EIGHT columns, 40miniature sprites, transparent RGBA background. Keep the SAME scale reduction uniformly across all40 cells, keeping body mass consistent across poses and outfits; no per-pose scale changes. No extra characters, no captions, no labels, no grid, no checkerboard, no props/ground shadows. This edit ONLY fixes spacing and shrinks the complete sprites; no redesign. Prefer very high resolution 3172x1984 so these small well-spaced sprites retain crisp detail.
```


## Glim

Accepted generation or edit:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
NEW companion GLIM: an adorable softly inflated FIVE-POINT STAR pillow made of warm cream-gold starlight, five short rounded points smoothly integrated into a single connected star body, two large glossy navy eyes, rosy cheeks, tiny friendly smile. Soft amber inner glow and gold edge. Not a circle/ball/dumpling, clearly a little cuddly five-point star, no limbs or separate appendages.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Each complete body AND hat occupies at MOST 72% of each equal grid cell in width and height. At least 14% of cell width and height is empty transparent padding on EVERY side. Clear wide transparent gutters separate all cells; outermost cells also have full padding. No cropped brim tips. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. Image 1 is ONLY the layout and painterly sprite-rendering style reference of Mochi; DO NOT copy its body, face shape or cream mochi material. Create the NEW subject specified above with a distinct shape and material. Image 2 is ONLY the four hat designs. Paint the hats directly and correctly ON the subject in every costume pose. Keep EXACT FIVE rows by EIGHT columns with wide transparent gaps.
```

Spacing edit:

```text
Use case: precise-object-edit. Correct the packing of this EXACT production sprite sheet, preserving the character's identity, face, material, every complete painted hat design and the EXACT existing FIVE rows/EIGHT columns/40 poses. CRITICAL: adjacent brim tips currently touch, making sprites unusable. Make EACH COMPLETE body-plus-hat sprite VERY MUCH SMALLER: HALF of its present width AND HALF its present height, centered independently inside its SAME fixed grid cell. The sprites should look MINIATURE with HUGE TRANSPARENT EMPTY GAPS. Each complete sprite can occupy no more than 55% of its cell width or height, leaving at least22% of each cell empty on EVERY edge. Do not enlarge them to fill space. All40sprites must be completely separate with a broad transparent channel between every pair of neighbors and every row, plus transparent outside margins. Keep the full curved brim of every hat, the full pompom/cap, and all body parts fully visible; no overlap, no clipping, no cutouts, no missing hat pixels. EXACT same FIVE rows and EIGHT columns, 40miniature sprites, transparent RGBA background. Keep the SAME scale reduction uniformly across all40 cells, keeping body mass consistent across poses and outfits; no per-pose scale changes. No extra characters, no captions, no labels, no grid, no checkerboard, no props/ground shadows. This edit ONLY fixes spacing and shrinks the complete sprites; no redesign. Prefer very high resolution 3172x1984 so these small well-spaced sprites retain crisp detail.
```


## Rune

Accepted generation or edit:

```text
Use case: stylized-concept. Production game companion RGBA sprite atlas, exactly FIVE equal horizontal ROWS and EIGHT equal COLUMNS, 40 complete isolated sprites. Prefer 3072x1920, landscape aspect 8:5; truly transparent background. Crisp polished cute painterly FFXIV pet art, readable at 96px.
NEW companion RUNE: an adorable squat floating rounded rectangular rune stone, one connected pillow-shaped slate-gray/teal pebble tablet with gently squared corners, smooth softly weathered stone texture and tiny teal-blue glowing engraved runes fused into surface, two large glossy sapphire eyes and tiny smiling mouth, warm peach cheek blush. Clearly a little living stone tablet with sturdy rounded-square silhouette, no hands/arms/legs/wings/extra loose stones.
Each ROW is the SAME character wearing a different complete outfit. Paint the body AND hat together as one correct connected illustration. Hat perspective, head fit, shadows and occlusion must be correct and stable: entire near/front brim runs continuously across the front of the forehead; rear brim naturally behind head. No missing chunks, no floating hat, no duplicated body, no detached limbs. Hats fit around the crown; keep face readable. Outfit and body identity remain identical across all eight poses within a row.
ROW 1: plain everyday character with NO headwear, recognizable base colors and materials.
ROW 2: All Saints' Wake witch costume, tall soft crooked purple witch hat, orange satin band/bow and small gold pumpkin clasp. FULL wide purple near/front brim with gold edge wrapped properly around head. Character retains its identity and normal face, subtly warm Halloween accents.
ROW 3: Starlight costume, red velvet Santa cap with fluffy white front cuff and white pompom, cap sits properly on head with cloth behind, ivory/ice winter accents on the same character. Full cuff visible and naturally curves around crown.
ROW 4: Make It Rain costume, dark navy silk TOP HAT, wide gold band, small round gold medallions and navy/gold ribbon; complete near/front gold-edged brim fitted properly around head. Golden festive accents on the same character.
ROW 5: Moonfire Faire costume, woven straw sunhat, teal ribbon and pink-red hibiscus flower to the side. Complete woven front brim wraps around forehead, back brim naturally behind. Subtle turquoise summer accents on the same character.
COLUMNS left to right, same for every row:
1 Neutral upright, cute open eyes and gentle smile.
2 MoveA soft body squash leaning slightly left, eyes open, keep volume.
3 MoveB complementary gentle stretch slightly right, eyes open, keep volume.
4 ClosedBlink same upright body and face placement as Neutral, all eyes completely closed as eyelid arcs.
5 CarrySquirmA cute compact held-from-upper-head tilt left, body stretches as one connected form; hat stays correctly fitted, no hand in image.
6 CarrySquirmB complementary tilt right, cute struggling face, headwear stays fitted.
7 EnterBed low sleepy body squash, half-closed eyes; complete body and fitted headwear still visible.
8 Wake cute gentle upright stretch, cheerful fully open eyes.
Keep the same apparent BODY size, volume and baseline across every cell and row; tall hats extend upward rather than shrinking the body. Each complete body AND hat occupies at MOST 72% of each equal grid cell in width and height. At least 14% of cell width and height is empty transparent padding on EVERY side. Clear wide transparent gutters separate all cells; outermost cells also have full padding. No cropped brim tips. Absolutely NO characters other than this subject, no hands, no house, no captions/labels/grids/background/ground shadows/floating decorative objects. All pixels of each complete body AND hat stay inside its cell. Preserve a single coherent connected body. Image 1 is ONLY the layout and painterly sprite-rendering style reference of Mochi; DO NOT copy its body, face shape or cream mochi material. Create the NEW subject specified above with a distinct shape and material. Image 2 is ONLY the four hat designs. Paint the hats directly and correctly ON the subject in every costume pose. Keep EXACT FIVE rows by EIGHT columns with wide transparent gaps.
```

Spacing edit:

```text
Use case: precise-object-edit. Correct the packing of this EXACT production sprite sheet, preserving the character's identity, face, material, every complete painted hat design and the EXACT existing FIVE rows/EIGHT columns/40 poses. CRITICAL: adjacent brim tips currently touch, making sprites unusable. Make EACH COMPLETE body-plus-hat sprite VERY MUCH SMALLER: HALF of its present width AND HALF its present height, centered independently inside its SAME fixed grid cell. The sprites should look MINIATURE with HUGE TRANSPARENT EMPTY GAPS. Each complete sprite can occupy no more than 55% of its cell width or height, leaving at least22% of each cell empty on EVERY edge. Do not enlarge them to fill space. All40sprites must be completely separate with a broad transparent channel between every pair of neighbors and every row, plus transparent outside margins. Keep the full curved brim of every hat, the full pompom/cap, and all body parts fully visible; no overlap, no clipping, no cutouts, no missing hat pixels. EXACT same FIVE rows and EIGHT columns, 40miniature sprites, transparent RGBA background. Keep the SAME scale reduction uniformly across all40 cells, keeping body mass consistent across poses and outfits; no per-pose scale changes. No extra characters, no captions, no labels, no grid, no checkerboard, no props/ground shadows. This edit ONLY fixes spacing and shrinks the complete sprites; no redesign. Prefer very high resolution 3172x1984 so these small well-spaced sprites retain crisp detail.
```
