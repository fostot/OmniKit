# Companion variant artwork provenance

Created with the built-in `image_gen.imagegen` tool, transparent background enabled. These are original fictional companions. The eleven accepted PNGs are byte-identical copies of raw tool outputs; no offline painting, background removal or resampling was applied. The existing eight base companions remain unchanged.

There are four authored looks per companion. Six landscape sheets have four rows and eight columns. Puff and Ember each use two four-by-four sheets: the first supplies Neutral, ClosedBlink, CarrySquirmA, CarrySquirmB; the motion sheet supplies MoveA, MoveB, EnterBed, Wake. The original Orbit ringless core is a separate four-by-four edit preserving its sixteen original expressions/reactions. Existing ringed Orbit art is retained.

`companion-variants.frames.json` contains exclusive pixel crop bounds, opaque body bounds, source anchors, upper carry grips, accessory anchors, per-look common source scale, per-frame sheet dimensions, and the four measured Wink eye ellipses. One uniform 1.15 source-unit conversion applies to the entire repaired Puff motion sheet; no pose is fitted independently. Rectangle crops preserve the full alpha>=96 body plus antialias padding and exclude every neighboring opaque body. A few very faint peripheral glow fringes are trimmed by UV crops; source pixels remain untouched.

Native offscreen ImGui previews were inspected at 96 and 192 logical pixels. These are rendering checks, not live FFXIV validation. Normal movement should retain the Neutral texture and use the existing continuous motion/mesh transform; MoveA/MoveB are authored references, not a fast full-body flip or ghost crossfade. Carry alternates around the measured grip; closed artwork supplies the same look during sleep. Mouth expressions do not provide speech or lip sync.

## Accepted raw assets

| File | Dimensions | SHA-256 |
| --- | --- | --- |
| `moonblob-variants.png` | 1774 × 887 | `f772b76deb9eb14b4f13418a7bae92097e17317a6166b0cb4390ccad00ff6331` |
| `wink-variants.png` | 1774 × 887 | `31d718baddf1c31d53e1dddaf97be1d7fdc0ef89dfb0f8827676aeadf6641dc4` |
| `nova-variants.png` | 1774 × 887 | `e5f33ca4366b9a1578bc0be970cb9be887953dc0ef7071070b9b887da42c9cc6` |
| `mochi-variants.png` | 1774 × 887 | `f5961be4fe5172687cff8dea34e732775b4b16e7a86cbf046c5d329404cb899a` |
| `puff-variants.png` | 1254 × 1254 | `a2c9a9446049ef016168051a1785f022a6035b514ac8fad8d824a45dac80f946` |
| `puff-variants-motion.png` | 1254 × 1254 | `57e0b505e66a20999397d252eb961f04966629192548d3133eda19fb07fd3e71` |
| `orbit-variants.png` | 1774 × 887 | `5f388691f99328713f68ddf5ed3b6e2abe2b9895108289787f3f22c5bbf6fd65` |
| `ember-variants.png` | 1254 × 1254 | `881c0d927b4a41b6542e40bf102daab21f1088add944cc69be76dd4060fd2eb4` |
| `ember-variants-motion.png` | 1254 × 1254 | `a08078ef1b3343cdd4df9ef758e7d15398d2263890f8e5d0cee7ceba0c39382e` |
| `prism-variants.png` | 1774 × 887 | `5ccc033161ac4a37b11e208b396647d9e9714e8ee68f6e540da9bbbaed2c23cf` |
| `orbit-core.png` | 1254 × 1254 | `c26c2f078331e56fe4c8c5cc8fa84b018d804f84e1fdebce38d7cab8ecb4b22d` |

## Reference and repair record

Existing project companion atlases supplied identity/style references. Each variant prompt below specifies a new authored look, preserving the separate default. Puff and Ember motion extensions used their new variant sheets as references. Wink received one targeted single-eye squint repair; Puff motion received one targeted spacing repair. Their first passes remain local diagnostics and are not runtime files. The original Orbit core edit uses the existing Orbit atlas as its reference and removes the baked ring without changing the source atlas.

## Exact submitted prompts

### puff

```text
Use case: stylized-concept.
Create a NEW production sprite atlas of FOUR distinct collectible looks for PUFF, the cute limbless moon-cloud spirit shown in the reference image. The reference is identity/style guidance only. Do not alter that original atlas.
One genuinely transparent square PNG, exactly FOUR columns and FOUR rows, sixteen independent whole-body sprites. No drawn grid, labels, letters, watermark, background, ground shadow or scenery. Equal cell layout with generous empty gutters; each complete sprite fits well inside its cell and does not touch a neighbor. Maximum body size about 68% of cell width and 65% of cell height.
Premium hand-painted storybook JRPG quality, appealing chubby cloud silhouette, big glossy midnight eyes, tiny cute mouth, warm blush, dimensional soft light and rich material detail readable at 96 pixels. It is always ONE unified cloud body: no arms, legs, feet, hands, wings, tails, hats or separate pieces. Embedded markings are part of the body.
Each ROW is a different authored look. Within each row, all four poses keep identical colors, motifs, body identity, source scale and center. Do not just apply a flat tint: paint the following distinctive cloud materials and motifs.
ROW 1: PEACH PARFAIT. Cream-and-peach confection cloud with softly marbled apricot swirls, pearly warm highlights, a little luminous crescent embedded in forehead, deep plum glossy eyes. Fluffy rounded cloud lobes, no food props.
ROW 2: AURORA DREAM. Deep periwinkle/lavender night-cloud with luminous mint-and-rose aurora ribbons embedded beneath its soft translucent surface, sparse tiny starlight freckles in the body, pale silver crescent, glossy blue eyes. Keep the face readable and sweet, not dark.
ROW 3: RAIN PEARL. Soft silvery-blue opalescent rain-cloud with rounded water-drop-shaped pearlescent markings embedded in its sides, pale cream face center, warm blush, silver crescent and glossy indigo eyes. No raindrops outside the body, no tears.
ROW 4: MINT MALLOW. Plush pistachio-mint cloud with cream scalloped marbling, delicate warm-gold dusting embedded on upper lobes, small golden crescent and glossy forest-blue eyes. Distinct frosted velvety surface, not the same colors as row 2.
Exact COLUMN order in EVERY row:
Column 1 NEUTRAL OPEN: relaxed upright cloud, eyes fully open and tiny closed smile.
Column 2 CLOSED BLINK: exactly the same upright cloud pose and body as column 1, both eyes peacefully fully closed, tiny serene smile. This should also work as a quiet sleep image; no Z text or props.
Column 3 HELD SQUIRM A: the same limbless cloud gently dangled from a fixed invisible grip point at upper-center; body below that point bends slightly left and compresses, eyes adorably determined/squinting, tiny pouting mouth. No fear, tears, pain or distress. No hand or grip hardware visible.
Column 4 HELD SQUIRM B: complementary gentle rightward whole-body squirm about the SAME upper-center invisible grip point, slightly stretched lower cloud, a playful annoyed expression. The upper-center grab location and overall body scale MUST align with column 3. This is a cute two-pose wiggle, not harm.
The two held poses must visibly differ in body compression and expression at miniature size, while the grip location stays fixed and no limbs appear. All four source frames in a row use the same camera and complete body scale. Preserve generous transparent margins, clean alpha and consistent lighting.
```

### ember

```text
Use case: stylized-concept.
Create a NEW production sprite atlas of FOUR distinct collectible looks for EMBER, the adorable limbless baby star-flame in the reference. The reference is identity/style guidance only; do not edit or replace the original.
One genuinely transparent square PNG, exactly FOUR columns and FOUR rows, sixteen separate whole-body sprites. No drawn grid, labels, letters, watermark, background, ground shadow, props or scenery. Equal-sized cells, generous transparent gutters; each complete sprite comfortably inside its cell. Body occupies no more than 68% of cell width and 65% of cell height.
Match the polished storybook JRPG painted miniature quality of the reference: round chubby glowing body with one short softly curled flame tip, big glossy eyes, tiny sweet mouth, warm rosy cheeks, soft luminous shading and rich internal detail readable at 96 pixels. Always ONE connected body; no arms, legs, feet, hands, wings, hats, accessories or detached sparks. Each variant must remain unmistakably Ember. No aggressive sharp fire or frightening expression.
Each ROW is one distinct painted appearance, not a simple flat recolor:
ROW 1 MOONFIRE: pearly icy-blue and silver flame, soft pale-cyan inner swirls, rounded luminous cream face, an embedded small silver crescent beneath the flame tip, blue-violet glossy eyes. A gentle magical moon flame.
ROW 2 ROSE CINDER: warm rose-pink and soft cream flame, rose-gold ribbons embedded under the luminous skin, tiny heart-shaped warm glow inside its forehead, plum glossy eyes. Soft petals suggested in the internal painted swirls, no separate petals.
ROW 3 HONEY STAR: translucent golden-honey flame with rounded amber-caramel inner swirls, a few tiny star flecks suspended inside the body, warm ivory face center and glossy deep teal eyes. Rich honey-glass material, no honey pot or external stars.
ROW 4 VIOLET WISP: soft lavender/violet flame edged with pearly turquoise, dreamlike layered mist swirls within the body, a few small opal freckles, cream face and glossy midnight-blue eyes. Clearly a rounded baby flame, not a sharp crystal or ghost with limbs.
Exact COLUMN order for EVERY row:
1 NEUTRAL OPEN: upright relaxed original round body, fully open eyes and a tiny closed smile.
2 CLOSED BLINK: exactly the same upright body shape and markings as column1, both eyes peacefully closed, tiny contented mouth. Suitable as quiet sleep image; no text.
3 HELD SQUIRM A: body suspended from an invisible fixed upper-center grip point near the base of its top flame curl; the lower body visibly bends/compresses left, eyes cute determined squint, tiny pout. Gentle playful protest, no pain or distress. No hand visible.
4 HELD SQUIRM B: complementary rightward lower-body bend/stretch around the SAME fixed upper-center grip point; a cute sideways glance or wink and tiny open complaining mouth. Both held images maintain the identical grip location and source scale while lower silhouette and expression clearly change.
Keep all four poses in each row the same character, lighting and complete body scale. The two held poses must read as a distinct gentle two-frame wiggle at 96 pixels, with no independent moving pieces. Keep the top grip anchor fixed while the lower body squishes beneath it. No hands, tears, injury or scary flames. True alpha transparency and wide clear gutters, no cropped tips or inter-cell overlap.
```

### puff-motion

```text
Use case: stylized-concept.
Create one NEW matching movement/reaction atlas for the four PUFF looks in the supplied reference atlas. The reference is the approved identity and exact appearance for each row; preserve its colors, markings, face, source body size and material. Keep the original reference file unchanged.
True transparent square PNG with FOUR columns and FOUR rows, sixteen independent sprites, no grid/text/background/shadow. Row order MUST match the reference: Peach Parfait peach/cream marbled cloud; Aurora Dream periwinkle with mint/rose aurora; Rain Pearl silver-blue with embedded drop markings; Mint Mallow pistachio/cream with golden dust. Same original cute single cloud body and crescent, no limbs or accessories.
The four columns are NEW states, not copies of the reference columns:
1 MOVE A / BILLOW: the cloud gently swells upward and slightly left as if taking a soft breath, lower cloud gently compresses, open glossy eyes peek slightly left and tiny happy mouth. Whole-body silhouette visibly billows; no detached pieces.
2 MOVE B / PEEK: complementary gentle low/wide billow, upper cloud rolls slightly right, glossy eyes glance slightly right with a cheeky small smile. This is a visible two-frame breathing/billowing movement, not just recolored identical pixels. Keep source scale and center equal to column1; expression and soft compression change.
3 ENTER BED: sleepy whole-body crouch/compression with half-lidded eyes and tiny sleepy mouth, still same connected limbless cloud.
4 WAKE: slightly taller whole-body stretch with delighted bright eyes and a tiny yawn/smile, same body and materials, no arms. Cute and comfortable.
Paint at exactly the same polish as reference: dimensional storybook JRPG miniature, pearly material, soft bright readable face, rich body markings. Body about70% cell width, centered at the same horizontal cell center and floor baseline, one source scale across the row. Generous empty transparent gutters, nothing crosses a cell edge. No hands, feet, legs, arms, floating props, letters, Zs, house, bed or floor. Keep movements modest enough to loop smoothly between the two authored movement states without changing the character identity.
```

### ember-motion

```text
Use case: stylized-concept.
Create a NEW matching movement/reaction atlas for the four EMBER collectible looks in the supplied reference. Preserve exact look identity, colors, embedded motifs, round cute face and polished hand-painted JRPG quality. The reference is identity guidance and must remain unchanged.
A genuinely transparent square PNG, exactly FOUR columns and FOUR rows, sixteen independent complete sprites. Equal cells with generous transparent gutters, each body no more than68% of its cell width/height; no cropped flame tips or neighboring overlap. No text/grid/background/shadow.
Row1 MOONFIRE: icy pearly-blue/silver baby flame with embedded crescent, cream face, blue-violet eyes.
Row2 ROSE CINDER: rose/cream flame with embedded rose-gold swirls and a small heart, plum eyes.
Row3 HONEY STAR: golden-honey/amber glassy flame with embedded star flecks and forehead star, deep-teal eyes.
Row4 VIOLET WISP: lavender/violet/turquoise flame with opal freckles, cream face, midnight-blue eyes.
Keep each one a single round connected limbless baby-flame body, no arms, legs, feet, hands or detached sparks.
Exact columns:
1 MOVE A / FLICKER UP: modest taller whole-body stretch, upper flame curl sways left, bright open eyes glance slightly left, small pleased mouth. Clearly alive with a visible smooth silhouette change but still round and chubby.
2 MOVE B / FLICKER LOW: complementary modest squat/wider whole body, upper flame curl sways right, open eyes glance right, tiny playful smile. Paint different curved internal flame streaks consistent with this deformation. The whole body flickers together, no separate pieces or scary spikes.
3 ENTER BED: sleepy body crouch, flame curl relaxed lower, half-lidded eyes and a tiny drowsy mouth.
4 WAKE: cheerful whole-body stretch, flame curl gently upright, delighted face with a small yawn/smile; no limb gestures.
Source scale, camera, body center and ground baseline are consistent across each row. Keep movements restrained and loopable; source art should provide expression and fluid silhouette variation, not an abrupt redesign. Preserve exact colored markings from each reference row. No hands, props, beds, houses, Z letters, tears, pain, injury, detached ornaments or ground effects.
```

### moonblob

```text
Use case: stylized-concept.
Create a NEW production companion variant atlas using the attached existing character only as identity/style reference. Do not modify the original.
A wide landscape genuinely transparent PNG with exactly EIGHT columns and FOUR rows:32 independent complete whole-body sprites. Canvas aspect about2:1. No grid, labels, text, watermark, background, floor shadow or scenery. Each sprite is centered in its own equal cell at consistent size with generous clear alpha gutters, never touching a neighbor. Body occupies about65% cell width and65% cell height. Keep high-quality readable silhouette and facial detail at96px.
Style: premium hand-painted storybook JRPG fantasy miniature, polished dimensional shading, soft luminous material, warm blush, friendly expressive face, adorable rather than creepy. A single unified body, no arms/legs/hands/feet/wings/tails/props, no detachable body pieces. Author the complete body in every cell.
Each ROW is a distinct painted look described below. Within a row, preserve the exact same colors, motifs, face identity, viewpoint, source scale and body center. New materials/embedded patterns must make these authored looks beyond flat palette swaps.
EXACT COLUMN order for every row:
1 Neutral: fully open eyes, tiny closed smile, relaxed upright body.
2 MoveA: the specified first gentle whole-body movement personality with visible silhouette/reflection change, open eyes glancing slightly left.
3 MoveB: complementary second movement pose, open eyes glancing right, a tiny cheeky expression. Same body scale and center; no screen-space translation between cells.
4 ClosedBlink/Sleep: precisely the neutral upright body with eye(s) fully and peacefully closed. Same center, surface and face position.
5 CarrySquirmA: gently held from invisible fixed upper-center grip; lower body bends/compresses left, cute determined squint/pout, no fear/pain. No hand drawn.
6 CarrySquirmB: complementary rightward lower-body squirm around exactly the SAME upper-center grip, cute wink or sideways glance/tiny protesting mouth. Same source scale, grip point and center as5.
7 EnterBed: sleepy soft whole-body crouch/compression, half-lidded eyes, tiny sleepy mouth.
8 Wake: cheerful whole-body stretch with happy alert expression or tiny yawn, no limbs.
No Z letters, bed, house, objects, detached sparkles or independent limbs. Coherent modest movement variation, no extreme stretching or cropped edges. The original existing artwork remains a separate unchanged default.
SUBJECT: MOONBLOB, an original smooth rounded pearly jelly moon-pet, completely limbless. Preserve the reference's large glossy two eyes, blush, tiny mouth, rounded jelly contour and magical sparkle embedded in its body. No feet/nubs, no moon-cloud lobes, no separate ornaments.
ROW1 OCEAN OPAL: clear turquoise/seafoam jelly, opalescent cream face, flowing pale blue wave ribbons embedded inside, tiny pearl bubbles, deep sapphire eyes.
ROW2 BERRY NEBULA: translucent strawberry/rose jelly with mauve nebula swirls and tiny gold starlight inside, ivory face center, glossy plum eyes.
ROW3 HONEYDEW MOON: pale melon-green pearly jelly, delicate creamy crescent-shaped swirls and a few gold motes suspended within, soft warm face, dark teal eyes.
ROW4 LAVENDER GEODE: transparent pale lavender jelly with tiny softly glowing opal/crystal flecks suspended INSIDE its smooth round surface, no exterior hard facets or spikes, ivory face center, violet-blue eyes.
Movement personality for columns2/3: soft whole-body jelly ripple; one diagonal compression with a small wave traveling down the surface followed by the complementary opposite ripple. The face stays recognizable while eyes glance playfully. Do not paint an entirely different facing or rotate the face upside down; runtime will handle rolling the whole sprite. Keep all eight cells in each row the same material and scale. Closed blink fully shuts BOTH eyes.
```

### wink

```text
Use case: stylized-concept.
Create a NEW production companion variant atlas using the attached existing character only as identity/style reference. Do not modify the original.
A wide landscape genuinely transparent PNG with exactly EIGHT columns and FOUR rows:32 independent complete whole-body sprites. Canvas aspect about2:1. No grid, labels, text, watermark, background, floor shadow or scenery. Each sprite is centered in its own equal cell at consistent size with generous clear alpha gutters, never touching a neighbor. Body occupies about65% cell width and65% cell height. Keep high-quality readable silhouette and facial detail at96px.
Style: premium hand-painted storybook JRPG fantasy miniature, polished dimensional shading, soft luminous material, warm blush, friendly expressive face, adorable rather than creepy. A single unified body, no arms/legs/hands/feet/wings/tails/props, no detachable body pieces. Author the complete body in every cell.
Each ROW is a distinct painted look described below. Within a row, preserve the exact same colors, motifs, face identity, viewpoint, source scale and body center. New materials/embedded patterns must make these authored looks beyond flat palette swaps.
EXACT COLUMN order for every row:
1 Neutral: fully open eyes, tiny closed smile, relaxed upright body.
2 MoveA: the specified first gentle whole-body movement personality with visible silhouette/reflection change, open eyes glancing slightly left.
3 MoveB: complementary second movement pose, open eyes glancing right, a tiny cheeky expression. Same body scale and center; no screen-space translation between cells.
4 ClosedBlink/Sleep: precisely the neutral upright body with eye(s) fully and peacefully closed. Same center, surface and face position.
5 CarrySquirmA: gently held from invisible fixed upper-center grip; lower body bends/compresses left, cute determined squint/pout, no fear/pain. No hand drawn.
6 CarrySquirmB: complementary rightward lower-body squirm around exactly the SAME upper-center grip, cute wink or sideways glance/tiny protesting mouth. Same source scale, grip point and center as5.
7 EnterBed: sleepy soft whole-body crouch/compression, half-lidded eyes, tiny sleepy mouth.
8 Wake: cheerful whole-body stretch with happy alert expression or tiny yawn, no limbs.
No Z letters, bed, house, objects, detached sparkles or independent limbs. Coherent modest movement variation, no extreme stretching or cropped edges. The original existing artwork remains a separate unchanged default.
SUBJECT: WINK, the lovable round floating cosmic-eye familiar in the reference: ONE very large friendly glossy eye horizontally centered on the body, one tiny cute mouth below it, blush, smooth rounded single body with small flowing contour ripples. Absolutely ONE eye, never a pair, no creepy anatomy. Eye opening centered on the same facial point in every pose. No detached orbiting stars or props; sparkles remain embedded in body.
ROW1 TIDE GLASS: pearly turquoise and pale seafoam glassy body with softly flowing wave patterns inside, warm ivory face around the single eye, brilliant sapphire iris.
ROW2 TWILIGHT VELVET: soft plush lavender/periwinkle body with small embedded silver star freckles, a pale-lilac eye surround and glowing amber/gold iris. Keep face bright and friendly, no dark threatening eye.
ROW3 ROSELIGHT: translucent rose-and-cream body with luminous gentle rose-gold swirls, warm cream eye surround, a large teal-blue iris.
ROW4 JADE LANTERN: pale celadon/jade body with creamy cloud-like marbling and a little warm-gold light inside, pale mint eye surround, glossy violet iris.
Movement personality: a small whole-body organic ripple paired with a clearly leftward iris glance inMoveA and rightward iris glance inMoveB. The single eye's opening/outline stays centered while iris glances. NO independent eyes or detached parts. For ClosedBlink/Sleep, exactly the same neutral body and face position, single eye fully shut with one soft curved lid line and a contented tiny mouth.
Important production constraint: neutral and closed-eye cells in each row have the same body palette, angle, eye-region size and eye center, so the eye region can blend continuously. Never replace the colored eye surround with the old reference's cream body. CarrySquirmA gently closes/squints the single eye; CarrySquirmB opens it in an adorably determined sideways glance. No second eye ever. 4rows×8columns exact.
```

### wink-repair

```text
Edit the supplied32-sprite atlas with ONE narrowly targeted anatomy correction. Preserve all colors, materials, body shapes, layout, transparency,32cells and every other pose exactly.
ONLY column5 (the CarrySquirmA pose) in ALL FOUR rows must change: WINK is a one-eyed familiar, but these four cells currently depict paired squint marks resembling two eyes. Replace those paired marks with ONE SINGLE large closed/squinting eye at the same central facial eye location used in column1. Use one continuous soft curved eyelid line, slightly squeezed in a cute playful way, and keep the tiny pouting mouth below it. Absolutely no left-and-right eye pair, no two separate chevrons, no second eye. It must read as one huge eye closed, like column4 but with a subtly determined single-lid expression. Preserve the upper-center held grip, body silhouette and all embedded markings.
All other columns1,2,3,4,6,7,8 in every row must remain unchanged. True transparent background, no labels or extra art.
```

### nova

```text
Use case: stylized-concept.
Create a NEW production companion variant atlas using the attached existing character only as identity/style reference. Do not modify the original.
A wide landscape genuinely transparent PNG with exactly EIGHT columns and FOUR rows:32 independent complete whole-body sprites. Canvas aspect about2:1. No grid, labels, text, watermark, background, floor shadow or scenery. Each sprite is centered in its own equal cell at consistent size with generous clear alpha gutters, never touching a neighbor. Body occupies about65% cell width and65% cell height. Keep high-quality readable silhouette and facial detail at96px.
Style: premium hand-painted storybook JRPG fantasy miniature, polished dimensional shading, soft luminous material, warm blush, friendly expressive face, adorable rather than creepy. A single unified body, no arms/legs/hands/feet/wings/tails/props, no detachable body pieces. Author the complete body in every cell.
Each ROW is a distinct painted look described below. Within a row, preserve the exact same colors, motifs, face identity, viewpoint, source scale and body center. New materials/embedded patterns must make these authored looks beyond flat palette swaps.
EXACT COLUMN order for every row:
1 Neutral: fully open eyes, tiny closed smile, relaxed upright body.
2 MoveA: the specified first gentle whole-body movement personality with visible silhouette/reflection change, open eyes glancing slightly left.
3 MoveB: complementary second movement pose, open eyes glancing right, a tiny cheeky expression. Same body scale and center; no screen-space translation between cells.
4 ClosedBlink/Sleep: precisely the neutral upright body with eye(s) fully and peacefully closed. Same center, surface and face position.
5 CarrySquirmA: gently held from invisible fixed upper-center grip; lower body bends/compresses left, cute determined squint/pout, no fear/pain. No hand drawn.
6 CarrySquirmB: complementary rightward lower-body squirm around exactly the SAME upper-center grip, cute wink or sideways glance/tiny protesting mouth. Same source scale, grip point and center as5.
7 EnterBed: sleepy soft whole-body crouch/compression, half-lidded eyes, tiny sleepy mouth.
8 Wake: cheerful whole-body stretch with happy alert expression or tiny yawn, no limbs.
No Z letters, bed, house, objects, detached sparkles or independent limbs. Coherent modest movement variation, no extreme stretching or cropped edges. The original existing artwork remains a separate unchanged default.
SUBJECT: NOVA, the original adorable neon starlight puff in reference. A unified plump rounded-star/cloud-like body with softly rounded luminous rays flowing from it, large friendly glossy two eyes and tiny mouth, blush. Rays are part of the SAME body, never arms/legs/feet or separate rig pieces. No sharp spikes, no harsh flash.
ROW1 SUNRISE SILK: peach/apricot/cream pearlescent starlight body with tiny gold star freckles embedded within, soft rose tips and deep violet eyes.
ROW2 POLAR AURORA: pale icy-aqua body with flowing mint/lilac aurora bands, gently silver-glowing rounded tips and deep sapphire eyes.
ROW3 ROSE COMET: rosy-pink/pale plum body with satin-like comet ribbons embedded in the surface, pearl highlights, warm blush and glossy teal eyes.
ROW4 MIDNIGHT OPAL: luminous periwinkle/navy-opal body with bright creamy face and internal iridescent opal speckles, softly pale-blue rounded tips and warm amber eyes. Keep face friendly/readable, not dark or sinister.
Movement personality: column2 softly folds the rounded rays closer to its plump center, column3 gently opens/flares the rays wider like a slow friendly breath. The face glances and smiles; the rays flex as one continuous soft body. No sudden flashes, detached rays, limbs, new accessories or particle background. Keep eight states clearly separated and all four looks consistent.
```

### mochi

```text
Use case: stylized-concept.
Create a NEW production companion variant atlas using the attached existing character only as identity/style reference. Do not modify the original.
A wide landscape genuinely transparent PNG with exactly EIGHT columns and FOUR rows:32 independent complete whole-body sprites. Canvas aspect about2:1. No grid, labels, text, watermark, background, floor shadow or scenery. Each sprite is centered in its own equal cell at consistent size with generous clear alpha gutters, never touching a neighbor. Body occupies about65% cell width and65% cell height. Keep high-quality readable silhouette and facial detail at96px.
Style: premium hand-painted storybook JRPG fantasy miniature, polished dimensional shading, soft luminous material, warm blush, friendly expressive face, adorable rather than creepy. A single unified body, no arms/legs/hands/feet/wings/tails/props, no detachable body pieces. Author the complete body in every cell.
Each ROW is a distinct painted look described below. Within a row, preserve the exact same colors, motifs, face identity, viewpoint, source scale and body center. New materials/embedded patterns must make these authored looks beyond flat palette swaps.
EXACT COLUMN order for every row:
1 Neutral: fully open eyes, tiny closed smile, relaxed upright body.
2 MoveA: the specified first gentle whole-body movement personality with visible silhouette/reflection change, open eyes glancing slightly left.
3 MoveB: complementary second movement pose, open eyes glancing right, a tiny cheeky expression. Same body scale and center; no screen-space translation between cells.
4 ClosedBlink/Sleep: precisely the neutral upright body with eye(s) fully and peacefully closed. Same center, surface and face position.
5 CarrySquirmA: gently held from invisible fixed upper-center grip; lower body bends/compresses left, cute determined squint/pout, no fear/pain. No hand drawn.
6 CarrySquirmB: complementary rightward lower-body squirm around exactly the SAME upper-center grip, cute wink or sideways glance/tiny protesting mouth. Same source scale, grip point and center as5.
7 EnterBed: sleepy soft whole-body crouch/compression, half-lidded eyes, tiny sleepy mouth.
8 Wake: cheerful whole-body stretch with happy alert expression or tiny yawn, no limbs.
No Z letters, bed, house, objects, detached sparkles or independent limbs. Coherent modest movement variation, no extreme stretching or cropped edges. The original existing artwork remains a separate unchanged default.
SUBJECT: MOCHI, the original rounded-square moon rice-cake spirit from reference. One thick pillowy squircle with softly rounded corners, slightly translucent pearly edges, powdery soft surface, two big glossy eyes, rosy cheeks, tiny cute mouth and small luminous crescent embedded on forehead. Preserve the square silhouette; never turn it into a sphere or give it nubs/limbs. No food props, plates or leaves.
ROW1 SAKURA CREAM: blush-pink and ivory mochi with faint petal-shaped rose marbling embedded in its surface, pale golden crescent and glossy violet eyes.
ROW2 MATCHA MOON: pistachio/matcha-green marbled mochi with a bright cream face center, fine gold dusting in the upper surface, warm golden crescent and deep-blue eyes.
ROW3 BLACK SESAME: softly luminous pearl-grey mochi with delicate charcoal/silver sesame-like speckles embedded across its body, bright ivory face, silver crescent, glossy amber eyes. Cute, light and comforting, never blackened/burnt.
ROW4 YUZU GLOW: warm pale citrus-yellow and cream mochi with translucent honey-citrus swirls and tiny gold light motes inside, cream crescent and teal-blue eyes. No external lemon or fruit pieces.
Movement personality: MoveA is a gentle left-leaning whole-body squish while remaining rounded-square; MoveB is a complementary right-leaning soft rebound/stretch. Four corners deform together like pillowy mochi, no limbs. The face glances playfully. Held poses squeeze and wiggle lower squircle below a stable invisible upper-center grip. ClosedBlink/Sleep must shut both eyes fully with continuous soft eyelid lines. Preserve source scale and fixed center; authored silhouette motion not screen translation.
```

### prism

```text
Use case: stylized-concept.
Create a NEW production companion variant atlas using the attached existing character only as identity/style reference. Do not modify the original.
A wide landscape genuinely transparent PNG with exactly EIGHT columns and FOUR rows:32 independent complete whole-body sprites. Canvas aspect about2:1. No grid, labels, text, watermark, background, floor shadow or scenery. Each sprite is centered in its own equal cell at consistent size with generous clear alpha gutters, never touching a neighbor. Body occupies about65% cell width and65% cell height. Keep high-quality readable silhouette and facial detail at96px.
Style: premium hand-painted storybook JRPG fantasy miniature, polished dimensional shading, soft luminous material, warm blush, friendly expressive face, adorable rather than creepy. A single unified body, no arms/legs/hands/feet/wings/tails/props, no detachable body pieces. Author the complete body in every cell.
Each ROW is a distinct painted look described below. Within a row, preserve the exact same colors, motifs, face identity, viewpoint, source scale and body center. New materials/embedded patterns must make these authored looks beyond flat palette swaps.
EXACT COLUMN order for every row:
1 Neutral: fully open eyes, tiny closed smile, relaxed upright body.
2 MoveA: the specified first gentle whole-body movement personality with visible silhouette/reflection change, open eyes glancing slightly left.
3 MoveB: complementary second movement pose, open eyes glancing right, a tiny cheeky expression. Same body scale and center; no screen-space translation between cells.
4 ClosedBlink/Sleep: precisely the neutral upright body with eye(s) fully and peacefully closed. Same center, surface and face position.
5 CarrySquirmA: gently held from invisible fixed upper-center grip; lower body bends/compresses left, cute determined squint/pout, no fear/pain. No hand drawn.
6 CarrySquirmB: complementary rightward lower-body squirm around exactly the SAME upper-center grip, cute wink or sideways glance/tiny protesting mouth. Same source scale, grip point and center as5.
7 EnterBed: sleepy soft whole-body crouch/compression, half-lidded eyes, tiny sleepy mouth.
8 Wake: cheerful whole-body stretch with happy alert expression or tiny yawn, no limbs.
No Z letters, bed, house, objects, detached sparkles or independent limbs. Coherent modest movement variation, no extreme stretching or cropped edges. The original existing artwork remains a separate unchanged default.
SUBJECT: PRISM, the original adorable rounded crystal-spirit from the reference. One chubby softly faceted pebble/gem body, two huge friendly glossy eyes, little blush and tiny cute mouth. Facets have rounded safe edges, delicate luminous refraction, no sharp spikes, limbs, separate crystals or props. Preserve a single full body with same face and proportions.
ROW1 ROSE QUARTZ: translucent rose/peach quartz with creamy opal face facets and fine rose-gold internal veins, violet eyes.
ROW2 AQUAMARINE: soft aqua/sea-blue gem with pale mint refraction and pearly ivory central facets, sapphire eyes; a few internal bubble-like light inclusions.
ROW3 SUNSTONE: warm apricot/gold crystal with amber and honey internal ribbons, bright creamy face facets and deep teal eyes.
ROW4 AMETHYST DREAM: pale lavender/periwinkle amethyst with soft violet internal facets and opalescent silver glints, luminous cream face and glossy amber eyes.
Movement personality: subtle whole-body facet flex/refraction, one gentle leftward compression and complementary rightward rebound. Paint shifting soft internal highlight paths to make the crystal feel alive without rotating the face away or detaching facets. Same stable center, body shape identity and common scale. Held lower body gently squishes despite magical gem material, no cracks, shards, damage or pain. All eight states keep the same material patterns and single cute face.
```

### orbit

```text
Use case: stylized-concept.
Create a NEW production companion variant atlas using the attached existing character only as identity/style reference. Do not modify the original.
A wide landscape genuinely transparent PNG with exactly EIGHT columns and FOUR rows:32 independent complete whole-body sprites. Canvas aspect about2:1. No grid, labels, text, watermark, background, floor shadow or scenery. Each sprite is centered in its own equal cell at consistent size with generous clear alpha gutters, never touching a neighbor. Body occupies about65% cell width and65% cell height. Keep high-quality readable silhouette and facial detail at96px.
Style: premium hand-painted storybook JRPG fantasy miniature, polished dimensional shading, soft luminous material, warm blush, friendly expressive face, adorable rather than creepy. A single unified body, no arms/legs/hands/feet/wings/tails/props, no detachable body pieces. Author the complete body in every cell.
Each ROW is a distinct painted look described below. Within a row, preserve the exact same colors, motifs, face identity, viewpoint, source scale and body center. New materials/embedded patterns must make these authored looks beyond flat palette swaps.
EXACT COLUMN order for every row:
1 Neutral: fully open eyes, tiny closed smile, relaxed upright body.
2 MoveA: the specified first gentle whole-body movement personality with visible silhouette/reflection change, open eyes glancing slightly left.
3 MoveB: complementary second movement pose, open eyes glancing right, a tiny cheeky expression. Same body scale and center; no screen-space translation between cells.
4 ClosedBlink/Sleep: precisely the neutral upright body with eye(s) fully and peacefully closed. Same center, surface and face position.
5 CarrySquirmA: gently held from invisible fixed upper-center grip; lower body bends/compresses left, cute determined squint/pout, no fear/pain. No hand drawn.
6 CarrySquirmB: complementary rightward lower-body squirm around exactly the SAME upper-center grip, cute wink or sideways glance/tiny protesting mouth. Same source scale, grip point and center as5.
7 EnterBed: sleepy soft whole-body crouch/compression, half-lidded eyes, tiny sleepy mouth.
8 Wake: cheerful whole-body stretch with happy alert expression or tiny yawn, no limbs.
No Z letters, bed, house, objects, detached sparkles or independent limbs. Coherent modest movement variation, no extreme stretching or cropped edges. The original existing artwork remains a separate unchanged default.
SUBJECT: ORBIT, the adorable little planet spirit in the reference. Create only the round RINGLESS PLANET CORE in every cell. A separate runtime ring will be drawn later, so ABSOLUTELY NO SATURN RING, oval band outside the body, rim, orbital line, external star or shadow of a ring anywhere in this sheet. Reconstruct the complete spherical body where the reference ring hides it. Preserve the two large glossy eyes, tiny mouth, blush and softly painted cloud bands.
ROW1 OCEAN MARBLE: pearly turquoise/blue planet with soft cream swirling atmospheric bands and a few tiny opal flecks inside, sapphire eyes.
ROW2 PEACH GIANT: apricot/rose/gold gas planet with flowing creamy atmospheric ribbons and a subtle rounded oval storm marking on its upper side, deep violet eyes. All markings ON the sphere, no outside ring.
ROW3 JADE WORLD: mint/celadon planet with creamy marble ribbons and soft warm-gold cloud streaks, deep-blue glossy eyes.
ROW4 AMETHYST SKY: pale lavender/periwinkle planet with luminous cream/silver cloud bands, sparse opal freckles and warm amber eyes.
Movement personality for MoveA/MoveB: the spherical planet center and diameter stay STEADY; the two faces make cute opposing iris glances while softly painted internal cloud currents subtly shift. Do not lean or squash the whole sphere in the movement states. The independent ring animation will provide the orbital motion later. Gentle whole-body deformation is allowed only in the authored carry/enter/wake reaction states. Maintain the same full core diameter in all neutral/open/closed/move frames. No limbs, no external ring, no props; wide transparent gutters.
```

### orbit-core

```text
Edit this original ORBIT sprite atlas into a companion RINGLESS-CORE atlas. Preserve the original atlas separately.
Keep exactly FOUR columns by FOUR rows,16cells, all original facial expressions and relative whole-planet body poses, the pearly pale-blue/lavender planet palette, soft cream atmospheric cloud bands, original glossy blue eyes, rosy cheeks and tiny mouth. Preserve polished hand-painted storybook JRPG quality.
Remove EVERY Saturn ring completely from ALL16sprites, including front band, back arc, external glow and cast shadow from that ring. Reconstruct the complete rounded planet core naturally beneath where each ring had occluded it, using matching cream-and-pale-blue cloud bands. The resulting file must contain ONLY complete ringless planet bodies, no elliptical ring outside or crossing any body. No external orbital lines, detached pieces, particles or accessories.
Neutral and blink bodies remain round and steady, original carry/landing/sleep body compression remains gentle and coherent. Do not redesign the character, change the face or add motifs. Keep the same source body scale and central body alignment across cells, allowing each original reaction's intended deformation. Generous transparent gutters, no touching neighbors or cropped edges. True transparent background, no grid, labels, text, shadow floor, bed or scenery. No arms, hands, legs, feet or nubs.
This ringless companion asset allows a separately drawn animated ring to be layered behind and in front later; do not draw any part of that ring now.
```

### puff-motion-repair

```text
Edit this matching four-look PUFF motion atlas ONLY to fix sprite spacing. Preserve exact 16 character looks, expressions, whole-body movement poses, colors, markings and row/column order.
The current columns 2/3 crowd together, causing their alpha edges to touch. Reduce EVERY complete sprite uniformly to about 80% of its current width and height, leaving it centered in its existing equal cell with a consistent baseline. Apply the SAME uniform scale reduction to all 16 sprites; no independent fitting or shape changes. Preserve the complete silhouette and all details. Leave at least 30px of completely empty transparent gap between every neighboring sprite on this 1254-square layout, with clean margins at canvas edges. No sprite or glow may touch another cell. Keep exactly 4 columns × 4 rows.
Do NOT change any character anatomy, colors, materials, eyes, mouth or movement pose. Do not add any objects. True alpha transparency, no background/grid/text. This is a spacing-only repair; the original larger raw sheet will be preserved.
```
