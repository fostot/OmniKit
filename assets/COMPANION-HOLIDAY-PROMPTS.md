# Holiday companion body artwork provenance

Created with the built-in `image_gen.imagegen` tool and transparent background enabled. These eight original fictional companion atlases are byte-identical copies of accepted raw generation outputs, including their generated content-authenticity metadata. No offline painting, background removal, or resampling was applied. Existing base and ordinary variant PNGs are unchanged.

Each sheet contains nine seasonal body designs with eight complete poses per design. Themes, top to bottom: All Saints' Wake, Starlight, Heavensturn, Valentione, Little Ladies, Hatching Tide, Make It Rain, Moonfire Faire, The Rising. Pose columns: Neutral, MoveA, MoveB, ClosedBlink, CarrySquirmA, CarrySquirmB, EnterBed, Wake. Orbit cores are ringless; Wink has one eye; all bodies remain a single connected character without a separate limb rig. Seasonal distinction is in the body material and markings, with separate optional runtime accessories.

`companion-holiday-bodies.frames.json` records measured exclusive pixel crop bounds, body bounds, baseline anchors, carry grips, crowns, common head widths/source scale per row, and the nine Wink eye ellipses. Generated spacing is uneven, so rendering uses these measured rectangles instead of a nominal grid. UV crops may trim faint peripheral glow outside the main body; source pixels remain untouched.

Native offscreen previews were inspected at 96 and 192 pixels. The source-linked harness checks every pose's aspect and baseline, the nine Wink eyelids, and each accepted PNG hash. These are rendering checks, not live FFXIV validation. Normal motion uses a stable body texture with the existing movement transforms; authored MoveA/MoveB remain pose references, avoiding rapid whole-body texture flips.

Prompt text below preserves the submitted wording; line endings and trailing whitespace are normalized.

## Accepted raw assets

| Companion | File | Dimensions | SHA-256 |
| --- | --- | --- | --- |
| Moonblob | `moonblob-holiday-bodies.png` | 1182 × 1330 | `be9d358f4482bc737dc86010481a03ee56898e7cd23d74d807de54f215eb7703` |
| Wink | `wink-holiday-bodies.png` | 1182 × 1330 | `b332b6ea1bfea660c7c5b9dd392e6eb9cf577e7f7c58f820206ee7df233b6a8b` |
| Nova | `nova-holiday-bodies.png` | 1182 × 1330 | `7e56cbf18c48685467b1213b57bd197c3410a67725aa250e4006272e0e93d3b7` |
| Mochi | `mochi-holiday-bodies.png` | 1182 × 1330 | `d15dc1f90d0f29579d37cdc1d2a2614d976aaa4002d0129fd1fbeda5820d40fe` |
| Puff | `puff-holiday-bodies.png` | 1182 × 1330 | `093a910453ca9bf1acc7ec4507f0279b4d4763c4581d4e841e0968e682125b3e` |
| Orbit | `orbit-holiday-bodies.png` | 1182 × 1330 | `44977d175eed7c59838dbca7d19068550a1ba6ba227ba27c9549e1c668347a3a` |
| Ember | `ember-holiday-bodies.png` | 1182 × 1330 | `5368fe7e068f032bdfdc7fd22f79d4e77379f7367ccd2463779c79095b4b1846` |
| Prism | `prism-holiday-bodies.png` | 1182 × 1330 | `d6c58eed950e85baa4519c3f44645dd82b2d8744c28a166da33f33e5563a70c7` |

## Moonblob

Identity/style references: `assets/moonblob-variants.png`.

```text
Use case: stylized-concept.
Create ONE new production sprite atlas of nine holiday body appearances for MOONBLOB. The attached image is identity/style reference only; leave it unchanged. Moonblob is an adorable smooth rounded jelly moon-pet, one complete continuous limbless body, large glossy TWO eyes, warm blush and tiny mouth. NO feet, nubs, arms, hands, legs, wings, ears, horns, tail or separate ornaments. Preserve the polished hand-painted storybook JRPG dimensional quality and attractive pearly translucent material. Detail must read clearly in a 96-pixel miniature.

Layout: exactly EIGHT columns and NINE rows, 72 whole-body sprites total, on ONE genuinely transparent PNG. Large high-resolution near-square portrait canvas in an 8:9 width:height ratio. Equal-sized cells; center each whole body in its cell with the same floor baseline and source scale within its row. Maximum character dimensions about 65% cell width and 65% cell height, wide clear alpha gutters between EVERY body. Nothing touches a neighboring cell or outer canvas edge. No grid lines, text, labels, watermarks, background, floor, ground shadows, scenery, detached sparkles or props.

Each row is a DISTINCT richly authored seasonal material/embedded marking scheme, beyond a flat recolor. No hats, headwear, head accessories or objects perched on top: optional accessories will be a separate runtime layer. These nine rows MUST appear top to bottom in this exact order:
ROW 1 ALL SAINTS WAKE: translucent velvety violet jelly with warm pumpkin-amber inner swirls and tiny crescent/bat-shaped markings embedded beneath the surface, creamy readable face, plum eyes. Playfully cozy Halloween mood, never scary.
ROW 2 STARLIGHT: opalescent snowy ivory jelly with deep cranberry-red and evergreen ribbon-like marbling, tiny gold star flecks and snowflake markings embedded in the body, cozy winter warmth, sapphire eyes.
ROW 3 HEAVENSTURN: pearl-white jelly with rich vermilion and warm golden flowing wave patterns like festive lacquer, delicate gold fan-shaped embedded motifs, luminous warm face and midnight eyes. No animal transformation or extra limbs.
ROW 4 VALENTIONE: rose-quartz pink jelly with creamy heart-shaped swirls and small ruby heart flecks inside, rose-gold highlights, glossy plum eyes and soft blush.
ROW 5 LITTLE LADIES: soft blossom-pink and lilac opalescent jelly, delicate cherry blossom shapes embedded in its sides, ivory face and pastel gold detailing, deep violet eyes. No separate petals outside the body.
ROW 6 HATCHING TIDE: pistachio/cream jelly with pastel lavender, peach and robin-egg-blue ovals and delicate decorative zigzag bands painted across the curved lower body, bright teal-blue eyes. Still Moonblob, not an egg with feet, no rabbit ears.
ROW 7 MAKE IT RAIN: translucent amber/champagne jelly with tiny gold coin-like circular flecks and lucky star/diamond patterns INSIDE the body, ivory readable face and deep teal eyes. Rich playful gold-saucer festival material without text/cards/props.
ROW 8 MOONFIRE FAIRE: clear aquamarine and coral-sunset jelly with flowing tropical wave ribbons and small warm-gold firework-like motifs embedded in the lower body, bright cream face, sapphire eyes. No external flowers, sunglasses, beach props or flame appendages.
ROW 9 THE RISING: deep luminous midnight-blue and violet jelly with rose-gold constellation ribbons, a warm golden sunrise glow inside the lower body and delicate celebratory starlight, ivory readable face and glossy blue eyes. Bright friendly face, not dark or ominous.

Exact COLUMN order in EVERY row:
1 NEUTRAL: relaxed upright rounded body, both eyes fully open, tiny closed smile.
2 MOVE A: modest jelly ripple/compression toward left, open eyes glance slightly left, playful smile. Same body scale and cell center.
3 MOVE B: complementary modest jelly ripple toward right, eyes glance slightly right, same scale and center. No face rotation; runtime handles rolling.
4 CLOSED BLINK: same upright body and exact face position as neutral; BOTH eyes fully peacefully closed. Quiet sleep-ready expression.
5 CARRY SQUIRM A: held gently from invisible fixed upper-center grip; lower body bends left and compresses, cute determined closed-eye squint and tiny pout. No hand drawn, no pain or fear.
6 CARRY SQUIRM B: complementary rightward lower-body squirm about the SAME upper-center invisible grip, one playful wink/side glance and tiny protesting mouth. Same source scale and grip point as column5.
7 ENTER BED: sleepy low/wide body compression with half-lidded eyes and tiny sleepy mouth. No bed, blanket or scenery.
8 WAKE: gentle taller round-body stretch, both eyes happily wide open and tiny cheerful yawn. No limbs.

All eight poses in one row preserve exactly the same painted holiday identity, camera, lighting, face placement, colors and motifs. Modest distinct poses, no identity drift. Maintain stable scale and baseline, complete smooth silhouettes and genuinely empty transparent gutters. Do not introduce head accessories or limb nubs.
```

## Wink

Identity/style references: `assets/wink-variants.png`.

```text
Use case: stylized-concept.
Create ONE new production sprite atlas of nine holiday body appearances for WINK. The attached image is identity/style reference only; leave it unchanged. Wink is the lovable floating cosmic-eye familiar in the reference: one smooth body with softly flowing contour ripples, exactly ONE very large friendly glossy eye horizontally centered, tiny cute mouth below it and warm blush. ONE eye only, never a pair, including squint expressions. No detached orbiting objects. The eye opening occupies the same facial location in each pose. NO feet, nubs, arms, hands, legs, wings, ears, horns, tail or separate ornaments. Preserve the polished hand-painted storybook JRPG dimensional quality and attractive rich dimensional magical material. Detail must read clearly in a 96-pixel miniature.

Layout: exactly EIGHT columns and NINE rows, 72 whole-body sprites total, on ONE genuinely transparent PNG. Large high-resolution near-square portrait canvas in an 8:9 width:height ratio. Equal-sized cells; center each whole body in its cell with the same floor baseline and source scale within its row. Maximum character dimensions about 65% cell width and 65% cell height, wide clear alpha gutters between EVERY body. Nothing touches a neighboring cell or outer canvas edge. No grid lines, text, labels, watermarks, background, floor, ground shadows, scenery, detached sparkles or props.

Each row is a DISTINCT richly authored seasonal material/embedded marking scheme, beyond a flat recolor. No hats, headwear, head accessories or objects perched on top: optional accessories will be a separate runtime layer. These nine rows MUST appear top to bottom in this exact order:
ROW 1 ALL SAINTS WAKE: translucent velvety violet body with warm pumpkin-amber inner swirls and tiny crescent/bat-shaped markings embedded beneath the surface, creamy readable face, plum eye. Playfully cozy Halloween mood, never scary.
ROW 2 STARLIGHT: opalescent snowy ivory body with deep cranberry-red and evergreen ribbon-like marbling, tiny gold star flecks and snowflake markings embedded in the body, cozy winter warmth, sapphire eye.
ROW 3 HEAVENSTURN: pearl-white body with rich vermilion and warm golden flowing wave patterns like festive lacquer, delicate gold fan-shaped embedded motifs, luminous warm face and midnight eye. No animal transformation or extra limbs.
ROW 4 VALENTIONE: rose-quartz pink body with creamy heart-shaped swirls and small ruby heart flecks inside, rose-gold highlights, glossy plum eye and soft blush.
ROW 5 LITTLE LADIES: soft blossom-pink and lilac opalescent body, delicate cherry blossom shapes embedded in its sides, ivory face and pastel gold detailing, deep violet eye. No separate petals outside the body.
ROW 6 HATCHING TIDE: pistachio/cream body with pastel lavender, peach and robin-egg-blue ovals and delicate decorative zigzag bands painted across the curved lower body, bright teal-blue eye. Still Wink, not an egg with feet, no rabbit ears.
ROW 7 MAKE IT RAIN: translucent amber/champagne body with tiny gold coin-like circular flecks and lucky star/diamond patterns INSIDE the body, ivory readable face and deep teal eye. Rich playful gold-saucer festival material without text/cards/props.
ROW 8 MOONFIRE FAIRE: clear aquamarine and coral-sunset body with flowing tropical wave ribbons and small warm-gold firework-like motifs embedded in the lower body, bright cream face, sapphire eye. No external flowers, sunglasses, beach props or flame appendages.
ROW 9 THE RISING: deep luminous midnight-blue and violet body with rose-gold constellation ribbons, a warm golden sunrise glow inside the lower body and delicate celebratory starlight, ivory readable face and glossy blue eye. Bright friendly face, not dark or ominous.

Exact COLUMN order in EVERY row:
1 NEUTRAL: relaxed upright body, one eye fully open, tiny closed smile.
2 MOVE A: modest body ripple/compression toward left, open eye glance slightly left, playful smile. Same body scale and cell center.
3 MOVE B: complementary modest body ripple toward right, eye glance slightly right, same scale and center. No whole-face rotation.
4 CLOSED BLINK: same upright body and exact face position as neutral; the single eye fully peacefully closed. Quiet sleep-ready expression.
5 CARRY SQUIRM A: held gently from invisible fixed upper-center grip; lower body bends left and compresses, cute determined ONE continuous closed-eyelid squint and tiny pout. No hand drawn, no pain or fear.
6 CARRY SQUIRM B: complementary rightward lower-body squirm about the SAME upper-center invisible grip, one partially open eye with a playful side glance and tiny protesting mouth. Same source scale and grip point as column5.
7 ENTER BED: sleepy low/wide body compression with half-lidded eye and tiny sleepy mouth. No bed, blanket or scenery.
8 WAKE: gentle taller round-body stretch, one eye happily wide open and tiny cheerful yawn. No limbs.

All eight poses in one row preserve exactly the same painted holiday identity, camera, lighting, face placement, colors and motifs. Modest distinct poses, no identity drift. Maintain stable scale and baseline, complete distinct silhouettes and genuinely empty transparent gutters. Do not introduce head accessories or limb nubs.
SUBJECT-SPECIFIC ANATOMY AND POSES: Wink has only ONE eye. ClosedBlink is ONE single continuous curved eyelid line across its centered eye opening. CarrySquirmA is that same ONE closed lid curved into a playful squint, never two eyelids or a two-eye face; CarrySquirmB is one partially open eye glancing sideways. Neutral/MoveA/MoveB use the same one-eye outline while the iris glances. Preserve this anatomy in all72cells.
```

## Nova

Identity/style references: `assets/nova-variants.png`.

```text
Use case: stylized-concept.
Create ONE new production sprite atlas of nine holiday body appearances for NOVA. The attached image is identity/style reference only; leave it unchanged. Nova is the adorable plump rounded-star starlight puff in the reference: ONE unified soft body with five softly rounded luminous rays flowing from its center, two large friendly glossy eyes, warm blush and tiny mouth. The rounded rays are part of the same body, never arms, feet, legs or separate rig pieces. No sharp spikes, harsh flashes or detached rays. NO feet, nubs, arms, hands, legs, wings, ears, horns, tail or separate ornaments. Preserve the polished hand-painted storybook JRPG dimensional quality and attractive rich dimensional magical material. Detail must read clearly in a 96-pixel miniature.

Layout: exactly EIGHT columns and NINE rows, 72 whole-body sprites total, on ONE genuinely transparent PNG. Large high-resolution near-square portrait canvas in an 8:9 width:height ratio. Equal-sized cells; center each whole body in its cell with the same floor baseline and source scale within its row. Maximum character dimensions about 65% cell width and 65% cell height, wide clear alpha gutters between EVERY body. Nothing touches a neighboring cell or outer canvas edge. No grid lines, text, labels, watermarks, background, floor, ground shadows, scenery, detached sparkles or props.

Each row is a DISTINCT richly authored seasonal material/embedded marking scheme, beyond a flat recolor. No hats, headwear, head accessories or objects perched on top: optional accessories will be a separate runtime layer. These nine rows MUST appear top to bottom in this exact order:
ROW 1 ALL SAINTS WAKE: translucent velvety violet body with warm pumpkin-amber inner swirls and tiny crescent/bat-shaped markings embedded beneath the surface, creamy readable face, plum eyes. Playfully cozy Halloween mood, never scary.
ROW 2 STARLIGHT: opalescent snowy ivory body with deep cranberry-red and evergreen ribbon-like marbling, tiny gold star flecks and snowflake markings embedded in the body, cozy winter warmth, sapphire eyes.
ROW 3 HEAVENSTURN: pearl-white body with rich vermilion and warm golden flowing wave patterns like festive lacquer, delicate gold fan-shaped embedded motifs, luminous warm face and midnight eyes. No animal transformation or extra limbs.
ROW 4 VALENTIONE: rose-quartz pink body with creamy heart-shaped swirls and small ruby heart flecks inside, rose-gold highlights, glossy plum eyes and soft blush.
ROW 5 LITTLE LADIES: soft blossom-pink and lilac opalescent body, delicate cherry blossom shapes embedded in its sides, ivory face and pastel gold detailing, deep violet eyes. No separate petals outside the body.
ROW 6 HATCHING TIDE: pistachio/cream body with pastel lavender, peach and robin-egg-blue ovals and delicate decorative zigzag bands painted across the curved lower body, bright teal-blue eyes. Still Nova, not an egg with feet, no rabbit ears.
ROW 7 MAKE IT RAIN: translucent amber/champagne body with tiny gold coin-like circular flecks and lucky star/diamond patterns INSIDE the body, ivory readable face and deep teal eyes. Rich playful gold-saucer festival material without text/cards/props.
ROW 8 MOONFIRE FAIRE: clear aquamarine and coral-sunset body with flowing tropical wave ribbons and small warm-gold firework-like motifs embedded in the lower body, bright cream face, sapphire eyes. No external flowers, sunglasses, beach props or flame appendages.
ROW 9 THE RISING: deep luminous midnight-blue and violet body with rose-gold constellation ribbons, a warm golden sunrise glow inside the lower body and delicate celebratory starlight, ivory readable face and glossy blue eyes. Bright friendly face, not dark or ominous.

Exact COLUMN order in EVERY row:
1 NEUTRAL: relaxed upright body, both eyes fully open, tiny closed smile.
2 MOVE A: modest body ripple/compression toward left, open eyes glance slightly left, playful smile. Same body scale and cell center.
3 MOVE B: complementary modest body ripple toward right, eyes glance slightly right, same scale and center. No whole-face rotation.
4 CLOSED BLINK: same upright body and exact face position as neutral; BOTH eyes fully peacefully closed. Quiet sleep-ready expression.
5 CARRY SQUIRM A: held gently from invisible fixed upper-center grip; lower body bends left and compresses, cute determined closed-eye squint and tiny pout. No hand drawn, no pain or fear.
6 CARRY SQUIRM B: complementary rightward lower-body squirm about the SAME upper-center invisible grip, one playful wink/side glance and tiny protesting mouth. Same source scale and grip point as column5.
7 ENTER BED: sleepy low/wide body compression with half-lidded eyes and tiny sleepy mouth. No bed, blanket or scenery.
8 WAKE: gentle taller round-body stretch, both eyes happily wide open and tiny cheerful yawn. No limbs.

All eight poses in one row preserve exactly the same painted holiday identity, camera, lighting, face placement, colors and motifs. Modest distinct poses, no identity drift. Maintain stable scale and baseline, complete distinct silhouettes and genuinely empty transparent gutters. Do not introduce head accessories or limb nubs.
SUBJECT-SPECIFIC ANATOMY AND POSES: Preserve Nova's distinctive five softly rounded connected star rays and chubby center, including its softly curled upper tip. MoveA softly folds the rounded rays inward; MoveB gently opens them wider like a slow breath. Bed pose softly compresses the unified star; Wake gently stretches it. Do not turn Nova into a generic sphere or humanoid.
```

## Mochi

Identity/style references: `assets/mochi-variants.png`.

```text
Use case: stylized-concept.
Create ONE new production sprite atlas of nine holiday body appearances for MOCHI. The attached image is identity/style reference only; leave it unchanged. Mochi is the adorable rounded-SQUARE moon rice-cake spirit in the reference: one thick pillowy squircle with four softly rounded corners, slightly translucent pearly edges, a powdery soft surface, two big glossy eyes, rosy cheeks, tiny mouth and a small luminous crescent embedded in the forehead. Preserve the square silhouette, no nubs or limbs, no food props, plates or leaves. NO feet, nubs, arms, hands, legs, wings, ears, horns, tail or separate ornaments. Preserve the polished hand-painted storybook JRPG dimensional quality and attractive rich dimensional magical material. Detail must read clearly in a 96-pixel miniature.

Layout: exactly EIGHT columns and NINE rows, 72 whole-body sprites total, on ONE genuinely transparent PNG. Large high-resolution near-square portrait canvas in an 8:9 width:height ratio. Equal-sized cells; center each whole body in its cell with the same floor baseline and source scale within its row. Maximum character dimensions about 65% cell width and 65% cell height, wide clear alpha gutters between EVERY body. Nothing touches a neighboring cell or outer canvas edge. No grid lines, text, labels, watermarks, background, floor, ground shadows, scenery, detached sparkles or props.

Each row is a DISTINCT richly authored seasonal material/embedded marking scheme, beyond a flat recolor. No hats, headwear, head accessories or objects perched on top: optional accessories will be a separate runtime layer. These nine rows MUST appear top to bottom in this exact order:
ROW 1 ALL SAINTS WAKE: translucent velvety violet body with warm pumpkin-amber inner swirls and tiny crescent/bat-shaped markings embedded beneath the surface, creamy readable face, plum eyes. Playfully cozy Halloween mood, never scary.
ROW 2 STARLIGHT: opalescent snowy ivory body with deep cranberry-red and evergreen ribbon-like marbling, tiny gold star flecks and snowflake markings embedded in the body, cozy winter warmth, sapphire eyes.
ROW 3 HEAVENSTURN: pearl-white body with rich vermilion and warm golden flowing wave patterns like festive lacquer, delicate gold fan-shaped embedded motifs, luminous warm face and midnight eyes. No animal transformation or extra limbs.
ROW 4 VALENTIONE: rose-quartz pink body with creamy heart-shaped swirls and small ruby heart flecks inside, rose-gold highlights, glossy plum eyes and soft blush.
ROW 5 LITTLE LADIES: soft blossom-pink and lilac opalescent body, delicate cherry blossom shapes embedded in its sides, ivory face and pastel gold detailing, deep violet eyes. No separate petals outside the body.
ROW 6 HATCHING TIDE: pistachio/cream body with pastel lavender, peach and robin-egg-blue ovals and delicate decorative zigzag bands painted across the curved lower body, bright teal-blue eyes. Still Mochi, not an egg with feet, no rabbit ears.
ROW 7 MAKE IT RAIN: translucent amber/champagne body with tiny gold coin-like circular flecks and lucky star/diamond patterns INSIDE the body, ivory readable face and deep teal eyes. Rich playful gold-saucer festival material without text/cards/props.
ROW 8 MOONFIRE FAIRE: clear aquamarine and coral-sunset body with flowing tropical wave ribbons and small warm-gold firework-like motifs embedded in the lower body, bright cream face, sapphire eyes. No external flowers, sunglasses, beach props or flame appendages.
ROW 9 THE RISING: deep luminous midnight-blue and violet body with rose-gold constellation ribbons, a warm golden sunrise glow inside the lower body and delicate celebratory starlight, ivory readable face and glossy blue eyes. Bright friendly face, not dark or ominous.

Exact COLUMN order in EVERY row:
1 NEUTRAL: relaxed upright body, both eyes fully open, tiny closed smile.
2 MOVE A: modest body ripple/compression toward left, open eyes glance slightly left, playful smile. Same body scale and cell center.
3 MOVE B: complementary modest body ripple toward right, eyes glance slightly right, same scale and center. No whole-face rotation.
4 CLOSED BLINK: same upright body and exact face position as neutral; BOTH eyes fully peacefully closed. Quiet sleep-ready expression.
5 CARRY SQUIRM A: held gently from invisible fixed upper-center grip; lower body bends left and compresses, cute determined closed-eye squint and tiny pout. No hand drawn, no pain or fear.
6 CARRY SQUIRM B: complementary rightward lower-body squirm about the SAME upper-center invisible grip, one playful wink/side glance and tiny protesting mouth. Same source scale and grip point as column5.
7 ENTER BED: sleepy low/wide body compression with half-lidded eyes and tiny sleepy mouth. No bed, blanket or scenery.
8 WAKE: gentle taller round-body stretch, both eyes happily wide open and tiny cheerful yawn. No limbs.

All eight poses in one row preserve exactly the same painted holiday identity, camera, lighting, face placement, colors and motifs. Modest distinct poses, no identity drift. Maintain stable scale and baseline, complete distinct silhouettes and genuinely empty transparent gutters. Do not introduce head accessories or limb nubs.
SUBJECT-SPECIFIC ANATOMY AND POSES: Mochi must remain a thick rounded-square pillow, not a jelly sphere. MoveA gently leans/squishes left; MoveB softly rebounds right; its four corners deform together. Carry poses wiggle the lower squircle beneath an identical fixed invisible upper-center grip. Keep the small embedded forehead crescent in every holiday body; do not use head ornaments.
```

## Puff

Identity/style references: `assets/puff-variants.png`, `assets/puff-poses.png`.

```text
Create a production transparent RGBA game sprite atlas for OmniKit's cute companion Puff. The two attached sheets are IDENTITY AND RENDERING STYLE REFERENCES ONLY, not edit targets: Puff is a single soft rounded cloud creature with fluffy curled lobes, huge glossy friendly eyes, tiny mouth, small crescent marking, no limbs. Match that plush polished painterly game-art quality. Create NEW complete holiday body designs, not a sheet of accessories.

Canvas: high resolution 2048 by 2304 portrait if possible, EXACTLY 8 equally spaced columns and 9 equally spaced rows, 72 separate complete sprites. Every invisible cell has generous transparent gutter; no sprite overlaps another cell; no outlines for cells, no labels, no text, no watermark, no background or ground shadow. Actual transparent alpha. Each cell centers its creature at x=50%, body base y=84%, whole body width about 74% of cell and height about 66%. Common body scale and identity across the entire sheet. Do not crop any puff lobe. Uniform front camera and light.

Rows from TOP to BOTTOM, each a full-body seasonal surface/material design consistently repeated across all 8 poses:
1 All Saints' Wake: toasted pumpkin-orange and deep plum cloud lobes with subtle golden spiderweb markings embedded on the body.
2 Starlight: snowy pearl cloud with evergreen/red candy-stripe BODY bands and tiny integrated gold star markings.
3 Heavensturn: vermilion-and-ivory porcelain cloud with gold flowing wave patterns.
4 Valentione: rose-pink cream cloud with gentle red heart-shaped markings.
5 Little Ladies: pale sakura-pink and ivory satin cloud with delicate cherry-blossom prints.
6 Hatching Tide: pastel mint/lilac cloud with colorful speckled egg-shell-like spots, still a cloud silhouette.
7 Make It Rain: rich teal-and-gold cloud with embossed round golden coin-shaped body markings, no loose coins.
8 Moonfire Faire: warm coral/sunset orange cloud grading into tropical ocean-blue lower lobes, white seafoam body patterns.
9 The Rising: midnight-violet and indigo cloud with small luminous gold firework/starburst patterns within the body.

Columns LEFT to RIGHT within EVERY row:
1 Neutral: balanced upright, both eyes open, tiny smile.
2 MoveA: gentle 5% squash, soft lobes lean slightly left, eyes open.
3 MoveB: gentle 5% stretch, soft lobes lean slightly right, eyes open.
4 ClosedBlink: same neutral whole body, BOTH eyes fully closed, calm smile.
5 CarrySquirmA: soft slight left tilt/squish, open startled-cute eyes.
6 CarrySquirmB: mirrored slight right tilt/squish, open startled-cute eyes.
7 EnterBed: relaxed slightly flattened cloud, sleepy half-closed eyes, no bed depicted.
8 Wake: upright cheerful little stretch, bright open eyes and happy mouth.

CRITICAL: NO hats, antlers, hair ornaments, crowns, halos, floating props, loose sparkles, objects, hands, beds, scenery, baked accessory layers or external effects anywhere. Holiday distinction is the WHOLE BODY's material, colored lobes and integrated markings. Keep the center of the face and base aligned across each row. No limbs or appendages. Exact 9-row by 8-column arrangement; do not substitute 4 or 6 columns.
```

## Orbit

Identity/style references: `assets/orbit-core.png`.

```text
Create a production transparent RGBA holiday game sprite atlas for OmniKit companion Orbit. Attached image is an IDENTITY AND RENDERING STYLE REFERENCE, not an edit target. Orbit is ONE round ringless planet body with glossy huge blue eyes, rosy cheeks and tiny mouth, no limbs. Its spherical circular outline stays smooth. CRITICAL: NO rings, bands outside the silhouette, halos, orbits or satellites in ANY cell; runtime draws its ring separately. Holiday designs are painted/embedded planetary material, not attached props. Match the reference's polished, soft, cute painterly game-art rendering.

EXACT LAYOUT: 8 columns by 9 rows, 72 complete separated sprites; high-resolution portrait 2048x2304 canvas if available. Keep about15% transparent gutter inside EVERY cell. No sprite/halo touches another. Actual transparent alpha, no background, no ground shadows, no text/labels/grid/watermark. Uniform front camera/light. Align cell centers x50% and baseline y84%, largest body extent74%cell, SAME scale throughout each row. Keep face alignment stable between poses.

9 rows TOP TO BOTTOM, each a full-body seasonal spherical planetary surface design repeated consistently across its 8 poses:
1 All Saints' Wake: pumpkin-orange/deep plum with delicate embedded gold web patterns.
2 Starlight: pearl snow-white with emerald and red candy-stripe body swirls, tiny integrated gold stars.
3 Heavensturn: vermilion-and-ivory with gold flowing wave patterns.
4 Valentione: rose-pink/cream with red heart-shaped body markings.
5 Little Ladies: delicate sakura-pink/ivory with cherry-blossom prints.
6 Hatching Tide: pastel mint/lilac with colorful speckled egg-shell spots.
7 Make It Rain: rich teal/gold with embossed round coin-like body markings; no loose coins.
8 Moonfire Faire: sunset coral/orange grading into ocean-blue lower material, white seafoam waves within the body.
9 The Rising: midnight-indigo/violet with tiny luminous gold starburst/firework patterns embedded within the body.

8 columns LEFT TO RIGHT in each row:
1 Neutral: upright, both eyes open, tiny smile.
2 MoveA: subtle5% squash and slight lean left, BOTH eyes open.
3 MoveB: subtle5% stretch and slight lean right, BOTH eyes open (NOT wink).
4 ClosedBlink: neutral full body, BOTH eyes fully closed.
5 CarrySquirmA: mild left tilt/squish, open startled-cute eyes.
6 CarrySquirmB: mild right tilt/squish, open startled-cute eyes.
7 EnterBed: relaxed slightly squat whole body, sleepy half-closed eyes; NO bed.
8 Wake: upright cheerful small stretch, bright open eyes and happy mouth.

Keep all holiday patterns attached to and contained within the body. NO hats/crowns/antlers/costume headpieces, floating props, hands, beds, floor, ornaments, external glow particles, other characters or accessories. Do not turn holiday patterns into a costume silhouette. Every sprite remains recognizable as Orbit. Exact9rows×8columns. EVERY ONE OF THE72 PLANETS MUST BE COMPLETELY RINGLESS.
```

## Ember

Identity/style references: `assets/ember-poses.png`.

```text
Create a production transparent RGBA holiday game sprite atlas for OmniKit companion Ember. Attached image is an IDENTITY AND RENDERING STYLE REFERENCE, not an edit target. Ember is ONE complete cute living flame with a rounded glowing face/base and a curled flame tip continuous with its body, glossy huge violet eyes, rosy cheeks and tiny mouth. Preserve the WHOLE flame from base through full tip in EVERY pose. No limbs, hats, or extra objects. Holiday designs color the entire coherent flame while keeping a warm pale face. Match the reference's polished, soft, cute painterly game-art rendering.

EXACT LAYOUT: 8 columns by 9 rows, 72 complete separated sprites; high-resolution portrait 2048x2304 canvas if available. Keep about15% transparent gutter inside EVERY cell. No sprite/halo touches another. Actual transparent alpha, no background, no ground shadows, no text/labels/grid/watermark. Uniform front camera/light. Align cell centers x50% and baseline y84%, largest body extent74%cell, SAME scale throughout each row. Keep face alignment stable between poses.

9 rows TOP TO BOTTOM, each a full-body seasonal whole glowing flame material design repeated consistently across its 8 poses:
1 All Saints' Wake: pumpkin-orange/deep plum with delicate embedded gold web patterns.
2 Starlight: pearl snow-white with emerald and red candy-stripe body swirls, tiny integrated gold stars.
3 Heavensturn: vermilion-and-ivory with gold flowing wave patterns.
4 Valentione: rose-pink/cream with red heart-shaped body markings.
5 Little Ladies: delicate sakura-pink/ivory with cherry-blossom prints.
6 Hatching Tide: pastel mint/lilac with colorful speckled egg-shell spots.
7 Make It Rain: rich teal/gold with embossed round coin-like body markings; no loose coins.
8 Moonfire Faire: sunset coral/orange grading into ocean-blue lower material, white seafoam waves within the body.
9 The Rising: midnight-indigo/violet with tiny luminous gold starburst/firework patterns embedded within the body.

8 columns LEFT TO RIGHT in each row:
1 Neutral: upright, both eyes open, tiny smile.
2 MoveA: subtle5% squash and slight lean left, BOTH eyes open.
3 MoveB: subtle5% stretch and slight lean right, BOTH eyes open (NOT wink).
4 ClosedBlink: neutral full body, BOTH eyes fully closed.
5 CarrySquirmA: mild left tilt/squish, open startled-cute eyes.
6 CarrySquirmB: mild right tilt/squish, open startled-cute eyes.
7 EnterBed: relaxed slightly squat whole body, sleepy half-closed eyes; NO bed.
8 Wake: upright cheerful small stretch, bright open eyes and happy mouth.

Keep all holiday patterns attached to and contained within the body. NO hats/crowns/antlers/costume headpieces, floating props, hands, beds, floor, ornaments, external glow particles, other characters or accessories. Do not turn holiday patterns into a costume silhouette. Every sprite remains recognizable as Ember. Exact9rows×8columns.
```

## Prism

Identity/style references: `assets/prism-poses.png`.

```text
Create a production transparent RGBA holiday game sprite atlas for OmniKit companion Prism. Attached image is an IDENTITY AND RENDERING STYLE REFERENCE, not an edit target. Prism is ONE rounded faceted crystal-gem creature with a soft pointed crown, huge glossy blue eyes, rosy cheeks and tiny mouth, no limbs. Preserve its translucent prismatic gemstone facets and entire rounded crystal silhouette. Holiday designs are embedded in the crystal's material, not attached objects. Match the reference's polished, soft, cute painterly game-art rendering.

EXACT LAYOUT: 8 columns by 9 rows, 72 complete separated sprites; high-resolution portrait 2048x2304 canvas if available. Keep about15% transparent gutter inside EVERY cell. No sprite/halo touches another. Actual transparent alpha, no background, no ground shadows, no text/labels/grid/watermark. Uniform front camera/light. Align cell centers x50% and baseline y84%, largest body extent74%cell, SAME scale throughout each row. Keep face alignment stable between poses.

9 rows TOP TO BOTTOM, each a full-body seasonal translucent faceted gemstone material design repeated consistently across its 8 poses:
1 All Saints' Wake: pumpkin-orange/deep plum with delicate embedded gold web patterns.
2 Starlight: pearl snow-white with emerald and red candy-stripe body swirls, tiny integrated gold stars.
3 Heavensturn: vermilion-and-ivory with gold flowing wave patterns.
4 Valentione: rose-pink/cream with red heart-shaped body markings.
5 Little Ladies: delicate sakura-pink/ivory with cherry-blossom prints.
6 Hatching Tide: pastel mint/lilac with colorful speckled egg-shell spots.
7 Make It Rain: rich teal/gold with embossed round coin-like body markings; no loose coins.
8 Moonfire Faire: sunset coral/orange grading into ocean-blue lower material, white seafoam waves within the body.
9 The Rising: midnight-indigo/violet with tiny luminous gold starburst/firework patterns embedded within the body.

8 columns LEFT TO RIGHT in each row:
1 Neutral: upright, both eyes open, tiny smile.
2 MoveA: subtle5% squash and slight lean left, BOTH eyes open.
3 MoveB: subtle5% stretch and slight lean right, BOTH eyes open (NOT wink).
4 ClosedBlink: neutral full body, BOTH eyes fully closed.
5 CarrySquirmA: mild left tilt/squish, open startled-cute eyes.
6 CarrySquirmB: mild right tilt/squish, open startled-cute eyes.
7 EnterBed: relaxed slightly squat whole body, sleepy half-closed eyes; NO bed.
8 Wake: upright cheerful small stretch, bright open eyes and happy mouth.

Keep all holiday patterns attached to and contained within the body. NO hats/crowns/antlers/costume headpieces, floating props, hands, beds, floor, ornaments, external glow particles, other characters or accessories. Do not turn holiday patterns into a costume silhouette. Every sprite remains recognizable as Prism. Exact9rows×8columns.
```
