# Companion home pose artwork

These seventeen atlases were generated with the built-in ImageGen tool using transparent-background requests. The selected RGBA PNG bytes and alpha channels are preserved unchanged. The manifest stores measured source crops, body bounds, bottom-center anchors, and one shared scale reference per appearance. Runtime controls travel, rotation, and house occlusion.

Coverage: thirteen default identities, thirty-two everyday appearances, and seventy-two holiday appearances; 117 appearances with four full-body poses each. Original eight identities use two seven-row atlases each; five newer defaults share one five-row atlas. Pose order: TurnBack, RearCrouch, RearClimbA, RearClimbB.

The following prompts use repository asset basenames only. Input images are identity and style references, not edit targets. All source images were visually inspected before generation. No pixel cutouts, recoloring, remapping, or limb rigs were used. Two selected atlases required a fresh whole-atlas generation to correct concrete packing or palette issues.

Atlas dimensions in prompts are requests; actual dimensions and SHA-256 hashes are authoritative in companion-homeposes.frames.json. Texture loading should remain lazy.

## companion-homeposes-moonblob-a.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset type: painted transparent full-body sprite animation atlas, identity preservation proof for an existing game UI.
References: moonblob-limbless.png is the DEFAULT pearly cream/lavender jelly sphere identity. moonblob-variants.png has FOUR everyday identities top-to-bottom: cyan OceanOpal, pink BerryNebula, pale mint/yellow HoneydewMoon, purple LavenderGeode. moonblob-holiday-bodies.png shows holiday shapes; use ONLY top two rows, orange PumpkinGel and icyblue Snowglobe. References are style/identity sources; do not copy faces onto backs.
Generate ONE PORTRAIT atlas ideally 1024x1792, with EXACTLY FOUR evenly spaced columns and SEVEN evenly spaced rows, twenty-eight separate sprites. Genuine RGBA transparent background, clean transparent gutters; no lines, labels, text, checkerboard, floor, bedding, house, shadows or scattered particles. Preserve original generated alpha. Each cell about256x256, character about180x170, centered at same x and footbaseline in cell, at least25px clear empty gutters. Entire character inside cell, no cut off.
ROWS IN THIS EXACT ORDER:
1 DEFAULT: pearly creamy ivory translucent round jelly with subtle lavender shading, golden tiny stars/crescents and warm pearlescent rim, matching moonblob-limbless.png. Absolutely not cyan.
2 OCEAN OPAL: aqua/cyan galaxy jelly, pale yellow tiny stars and crescent motifs matching reference top row.
3 BERRY NEBULA: saturated rose-pink/lilac galaxy jelly, tiny golden stars, matching reference second row.
4 HONEYDEW MOON: pastel mint-green/pale lime jelly, yellow crescent moons, matching reference third row.
5 LAVENDER GEODE: lavender/periwinkle opalescent jelly, tiny blue/cream flecks and stars, matching reference fourth row.
6 PUMPKIN GEL: orange/golden ribbed pumpkin-shaped transparent jelly body, round squat lobes and distinct vertical pumpkin grooves, from holiday reference top row. Grooves and pumpkin silhouette persist on back. No separate hat, no face.
7 SNOWGLOBE: icy pale blue rounded glassy jelly body with snowflake motifs and an embedded snowy white scalloped bottom rim, from holiday reference second row. Snowflakes and white snow band wrap around back. No separate hat, no face.
FOUR COLUMN POSES repeated identically in purpose for EVERY row:
1 three-quarter BACK turn to the RIGHT, back dominates, tiny side glimpse of face at far RIGHT edge only; do not add front eyes in middle.
2 fully REAR-facing anticipatory CROUCH: broad low body squash, no eyes/face/mouth.
3 fully REAR-facing climb STRETCH: body elongates up and leans away, mass raised and slight left haunch wiggle, NO eyes/face/mouth.
4 fully REAR-facing climb SETTLE: forward-compressed round body and right haunch wiggle, clearly lower/squatter than3, NO eyes/face/mouth.
Every sprite is one connected complete LIMBLESS body, no independent arms, legs, feet, tail or detached parts. Authored pose changes must alter actual whole-body silhouette and surface pattern perspective. Keep each row's material, color and motifs consistent across all four frames. All rows remain the SAME Moonblob species; do not invent animals. No cream FRONT face patch on rear; only full material/back patterns.
Style: high-quality hand-painted glossy soft storybook companion art, subtle volume and exquisite tiny internal light patterns consistent with references. Clear attractive silhouettes readable at96pixels. Keep deep orange distinct from pink; cyan distinct from snowy pale blue; lavender distinct from pearly default; don't randomly change palette across poses. Do NOT substitute faces for backs. Transparent everywhere outside sprites.
```

## companion-homeposes-moonblob-b.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columns×7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
SPECIES: Moonblob rounded translucent celestial jelly. Reference has9holidayrows. Use ONLY reference rows3 through9 in exactorder (skipfirst2).
REFERENCES in inputorder: moonblob-holiday-bodies.png.
ROWS EXACTORDER:
1. Lucky Lantern (moonblob-heavensturn), fromholidayreference row3
2. Love Jelly (moonblob-valentione), fromholidayreference row4
3. Sakura Dumpling (moonblob-littleladies), fromholidayreference row5
4. Hatchling Opal (moonblob-hatchingtide), fromholidayreference row6
5. Golden Treasure (moonblob-makeitrain), fromholidayreference row7
6. Tidal Jelly (moonblob-moonfirefaire), fromholidayreference row8
7. Celestial Ember (moonblob-therising), fromholidayreference row9
Follow each reference's specific palette AND actual fullbodyshape/pattern. Only backs replace frontviews, never colors. Text names are instructionsonly: NOtext onatlas.
```

## companion-homeposes-wink-a.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columns×7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
SPECIES: Wink is a softly swirling curled crescent/whorl-shaped creamy celestial spirit, one frontcyclopseye only glimpsed onfar-rightturnededge, fullbackhasNOeye. No separateorbitdecorations.
REFERENCES in inputorder: wink-motion.png, wink-variants.png, wink-holiday-bodies.png.
ROWS EXACTORDER:
1. Default fromfirstreference (Default), defaultfirstreference
2. Tide Glass (TideGlass), everydayvariantreference row1
3. Twilight Velvet (TwilightVelvet), everydayvariantreference row2
4. Roselight (Roselight), everydayvariantreference row3
5. Jade Lantern (JadeLantern), everydayvariantreference row4
6. Haunted Moon (wink-allsaintswake), holidayreference row1
7. Ice Iris (wink-starlight), holidayreference row2
Follow each reference's specific palette AND actual fullbodyshape/pattern. Only backs replace frontviews, never colors. Text names are instructionsonly: NOtext onatlas.
```

## companion-homeposes-wink-b.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columns×7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
SPECIES: Wink softly curled crescent/whorl singleconnected spirit, no rearcyclopseye. Reference9holidayrows, use rows3..9 only. Keep lanternredgold,heartpink,flowerpink,opalegg,goldcoin,cyanwave,purpleastral motifs and actualshapes.
REFERENCES ininputorder: wink-holiday-bodies.png.
ROWS EXACTORDER:
1. Lantern Eye (wink-heavensturn), holidayreference row3
2. Heart Sight (wink-valentione), holidayreference row4
3. Sakura Iris (wink-littleladies), holidayreference row5
4. Opal Hatchling (wink-hatchingtide), holidayreference row6
5. Golden Luck (wink-makeitrain), holidayreference row7
6. Tidal Eye (wink-moonfirefaire), holidayreference row8
7. Astral Gaze (wink-therising), holidayreference row9
```

## companion-homeposes-nova-a.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columns×7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
SPECIES: Nova connected SOFT FIVE-LOBED STAR with curled topstarpoint. Five connectedpoints are bodylobes, NOT separate arms/legs. Maintain overall starsilhouette inrear, even crouching. Defaultcreamy paleopal, everyday4palettes, first2holiday fireorange+iceblue.
REFERENCES ininputorder: nova-motion.png, nova-variants.png, nova-holiday-bodies.png.
ROWS EXACTORDER:
1. Default fromfirstreference (Default), defaultfirstreference
2. Sunrise Silk (SunriseSilk), everydayreference row1
3. Polar Aurora (PolarAurora), everydayreference row2
4. Rose Comet (RoseComet), everydayreference row3
5. Midnight Opal (MidnightOpal), everydayreference row4
6. Haunted Lantern (nova-allsaintswake), holidayreference row1
7. Snowstar (nova-starlight), holidayreference row2
```

## companion-homeposes-nova-b.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columns×7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
SPECIES: Nova soft connected FIVE-LOBED STAR, fivepoints unifiedbody notseparatelimbs; starsilhouettepersistsinback. Matchholidayreference rows3..9 exactly: redgoldlanternstar,pinkheartstar,sakuraflowerstar,pastelopalshellstar,goldenstar,coralbluepinkstar,purplecelestialstar.
REFERENCES in input order: nova-holiday-bodies.png.
ROWS EXACT ORDER:
1. Fortune Comet (nova-heavensturn), holiday reference row 3
2. Heartstar (nova-valentione), holiday reference row 4
3. Sakura Star (nova-littleladies), holiday reference row 5
4. Eggshell Star (nova-hatchingtide), holiday reference row 6
5. Golden Nova (nova-makeitrain), holiday reference row 7
6. Coral Star (nova-moonfirefaire), holiday reference row 8
7. Rising Star (nova-therising), holiday reference row 9
```

## companion-homeposes-mochi-a.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columns×7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
SPECIES: Mochi is a creamy puffy ROUNDED SQUARE ricecake, squirclebody with softcorners and FLATBOTTOM; not sphere/notflame. Defaultivory pearly lavender,4everydayrows pinksakura,greentea,graysesame,yuzuyellow; first2holiday orange ribbedpumpkinmochi and redwhite peppermintSWIRLstripes. Frontforeheadcrescent ONLY front: rearhasNOcrescent except materialpatterns matchingreference. Roundedcubelikeback staysconnected.
REFERENCES in input order: mochi-poses.png, mochi-variants.png, mochi-holiday-bodies.png.
ROWS EXACT ORDER:
1. Default from first reference (Default), default first reference
2. Sakura Cream (SakuraCream), everyday reference row 1
3. Matcha Moon (MatchaMoon), everyday reference row 2
4. Black Sesame (BlackSesame), everyday reference row 3
5. Yuzu Glow (YuzuGlow), everyday reference row 4
6. Pumpkin Mochi (mochi-allsaintswake), holiday reference row 1
7. Peppermint Mochi (mochi-starlight), holiday reference row 2
```

## companion-homeposes-mochi-b.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columns×7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
SPECIES: Mochi softconnected ricecake. Holidayreference9rows, useONLYrows3..9 exactorder: redbottomcreamtop pleatedluckydumpling; pinkheartdessertwithcreamrim; pinksakuraflower wrappedgreenleafbottom; pastelspottedshell cream egg; goldeningotbody; sandycreamdrop oncyanwhitewavedbottom; purpledropgoldstars. These are intrinsicfullbodyshapes andmaterial, not clothing. BackhasNOface; retainwrappedleaves,zigzagshell andwave patterns.
REFERENCES in input order: mochi-holiday-bodies.png.
ROWS EXACT ORDER:
1. Lucky Dumpling (mochi-heavensturn), holiday reference row 3
2. Sweetheart Mochi (mochi-valentione), holiday reference row 4
3. Sakura Mochi (mochi-littleladies), holiday reference row 5
4. Egg Mochi (mochi-hatchingtide), holiday reference row 6
5. Golden Daifuku (mochi-makeitrain), holiday reference row 7
6. Beach Mochi (mochi-moonfirefaire), holiday reference row 8
7. Stardust Mochi (mochi-therising), holiday reference row 9
```

## companion-homeposes-puff-a.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columns×7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
SPECIES: Puff oneconnected billowingplush CLOUD with severalroundcloudlobes and curledlavendertop. No separatehands/feet. Default cream/lavender;4everydayvariantrows peachpink,saturated violet/periwinkle with vivid cyan mint aurora ribbons,pearly bluegray with embedded shiny raindrops,mintgreen; first2holiday creamy paleghostcloud with lavender scalloped bottom band and icywhiteblue snowcloud. Frontforeheadcrescent absentonrear. Cloudlobesstaycloudy inallposes, notflame.
REFERENCES in input order: puff-poses.png, puff-variants.png, puff-holiday-bodies.png.
ROWS EXACT ORDER:
1. Default from first reference (Default), default first reference
2. Peach Parfait (PeachParfait), everyday reference row 1
3. Aurora Dream (AuroraDream), everyday reference row 2
4. Rain Pearl (RainPearl), everyday reference row 3
5. Mint Mallow (MintMallow), everyday reference row 4
6. Ghostcloud (puff-allsaintswake), holiday reference row 1
7. Snowcloud (puff-starlight), holiday reference row 2
CRITICAL PACKING CORRECTION: previous sprites nearly touched causing neighbor pixels in rectangle crops. This time make EVERY sprite much SMALLER, only60percent of eachcellwidth, max150px at1024canvaswidth. Wide EMPTY TRANSPARENT gutters40px minimum allaround eachsprite. Preserve SAME7identities exactorder and4poses. No bodypartorouterglowmayapproach any neighbor. This safetyspacing is MOREIMPORTANT than fillingcanvas.
```

## companion-homeposes-puff-b.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columns×7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
SPECIES: Puff connected fluffy CLOUDlobes, holidayreference9rows useONLYrows3..9. Row1 redorangegold lanternCLOUD, row2 pale pinkheartCLOUD, row3 pinksakurafiveflowerCLOUD, row4 pastelyellowlavenderCLOUDinopalzigzagshell, row5 goldbillowingCLOUD, row6 cyanseafoamwhitewaveCLOUD, row7 deepvioletblueauroraCLOUDgoldstarlights. Matchactualshapes/palettes/patterns, notallflametips. No detachedlimbs; no rearfaces.
REFERENCES in input order: puff-holiday-bodies.png.
ROWS EXACT ORDER:
1. Lantern Cloud (puff-heavensturn), holiday reference row 3
2. Heartcloud (puff-valentione), holiday reference row 4
3. Sakura Cloud (puff-littleladies), holiday reference row 5
4. Eggcloud (puff-hatchingtide), holiday reference row 6
5. Golden Raincloud (puff-makeitrain), holiday reference row 7
6. Seafoam Cloud (puff-moonfirefaire), holiday reference row 8
7. Aurora Cloud (puff-therising), holiday reference row 9
```

## companion-homeposes-orbit-a.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columnsÃ—7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.

References orbit-poses.png and orbit-core.png are identity/style references; orbit-variants.png lists four variant rows and orbit-holiday-bodies.png lists nine holiday rows. Use references only; create the four NEW whole-body rear/climb poses described, never reproduce their front-facing expressions.
EXACT ROWS top to bottom:
1. Default (Default)
2. OceanMarble (Ocean Marble)
3. PeachGiant (Peach Giant)
4. JadeWorld (Jade World)
5. AmethystSky (Amethyst Sky)
6. orbit-allsaintswake (Pumpkin Planet)
7. orbit-starlight (Snow Planet)
Species-specific: Orbit is a ringless planetary core. Smooth single planet body only. Absolutely no Saturn rings, orbit trails, detached motifs or face patches on back. Base pearl blue, variants ocean cyan/peach gold/jade green/amethyst lavender. Match holiday texture and silhouette from reference rows.
```

## companion-homeposes-orbit-b.png

Actual source: 949 by 1658 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columnsÃ—7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.

References orbit-poses.png and orbit-core.png are identity/style references; orbit-variants.png lists four variant rows and orbit-holiday-bodies.png lists nine holiday rows. Use references only; create the four NEW whole-body rear/climb poses described, never reproduce their front-facing expressions.
EXACT ROWS top to bottom:
1. orbit-heavensturn (Lantern Planet)
2. orbit-valentione (Heart Planet)
3. orbit-littleladies (Flower Planet)
4. orbit-hatchingtide (Egg Planet)
5. orbit-makeitrain (Treasure Planet)
6. orbit-moonfirefaire (Water Planet)
7. orbit-therising (Astral Planet)
Species-specific: Orbit is a ringless planetary core. Smooth single planet body only. Absolutely no Saturn rings, orbit trails, detached motifs or face patches on back. Base pearl blue, variants ocean cyan/peach gold/jade green/amethyst lavender. Match holiday texture and silhouette from reference rows.
CRITICAL EXACT SOURCE ROW MAPPING: The FIRST output row is Lantern Planet from holiday reference row THREE: solid Chinese-lantern RED lacquer with GOLD vertical ribs and small connected gold crown; absolutely NO purple volcanic chunks, no molten orange pumpkin. Output row2 source holidayrow4 pink heart planet. Outputrow3 sourceholidayrow5 pink flower-lobed planet. Outputrow4 sourceholidayrow6 palegold/turquoise cracked egg planet. Outputrow5 sourceholidayrow7 golden embossed treasure planet. Outputrow6 sourceholidayrow8 turquoise seawater/foam planet. Outputrow7 sourceholidayrow9 midnight navy faceted planet with gold starbursts/cracks. All are whole connected bodies seen from rear; preserve these seven exact forms and palettes across four distinct rear poses.
```

## companion-homeposes-ember-a.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columns×7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
SPECIES: Ember connected rounded flamebody with curved topflametip. Defaultwarmgold/orange. Four everydayrows: Moonfireicybluewhitecrescent; RoseCinderpinkcreamheart; HoneyStarwarmgoldstars; VioletWisp purplewhitecyan. First2holiday orangePUMPKINflamewithGREENtip, and pearlywhitecandleflamewithgoldtip. Keepactualreferencebodypalettes acrosswholebacks, nofrontfacepatch orforeheadicon onrear. Intrinsicpatternsremain, doNOTdefaultallrowstogold.
REFERENCES in input order: ember-poses.png, ember-variants.png, ember-holiday-bodies.png.
ROWS EXACT ORDER:
1. Default from first reference (Default), default first reference
2. Moonfire (Moonfire), everyday reference row 1
3. Rose Cinder (RoseCinder), everyday reference row 2
4. Honey Star (HoneyStar), everyday reference row 3
5. Violet Wisp (VioletWisp), everyday reference row 4
6. Jack-o-Flame (ember-allsaintswake), holiday reference row 1
7. Candle Soul (ember-starlight), holiday reference row 2
```

## companion-homeposes-ember-b.png

Actual source: 948 by 1660 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columns×7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
SPECIES: Ember connected flame. Reference9holidayrows useONLYrows3..9 exactorder: redorangegold LanternFlame withgoldflametip; vividpinkyellow Heartflame; lightpink SakuraFlame inintrinsicpetalrim; goldtip OpalFlamewithinpastelzigzagshell; goldwhole GoldenFlame; cyanwhite SeafoamSoul withwhitewavetexture; redorangePhoenixSpark withintenseyellowcore. Theseintrinsicpatterns/shapes followrearfullbody; nothats, notseparateparts. NOrearface.
REFERENCES in input order: ember-holiday-bodies.png.
ROWS EXACT ORDER:
1. Lantern Flame (ember-heavensturn), holiday reference row 3
2. Heartflame (ember-valentione), holiday reference row 4
3. Sakura Flame (ember-littleladies), holiday reference row 5
4. Opal Flame (ember-hatchingtide), holiday reference row 6
5. Golden Flame (ember-makeitrain), holiday reference row 7
6. Seafoam Soul (ember-moonfirefaire), holiday reference row 8
7. Phoenix Spark (ember-therising), holiday reference row 9
```

## companion-homeposes-prism-a.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columnsÃ—7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.

References prism-poses.png are identity/style references; prism-variants.png lists four variant rows and prism-holiday-bodies.png lists nine holiday rows. Use references only; create the four NEW whole-body rear/climb poses described, never reproduce their front-facing expressions.
EXACT ROWS top to bottom:
1. Default (Default)
2. RoseQuartz (Rose Quartz)
3. Aquamarine (Aquamarine)
4. Sunstone (Sunstone)
5. AmethystDream (Amethyst Dream)
6. prism-allsaintswake (Haunted Amethyst)
7. prism-starlight (Ice Crystal)
Species-specific: Prism is a single faceted crystal body. Preserve the exact distinct holiday silhouette (star, heart, flower, egg, diamond, curled wave) and faceting. No detached facets, no new appendages, no flat front face patch on its back. Rear is entirely faceted material.
EXACT ROW MAPPING: row1 original default iridescent pearl faceted teardrop gem; row2 variants row1 RoseQuartz blushpink faceted teardrop; row3 variants row2 Aquamarine cyan mint faceted teardrop; row4 variants row3 Sunstone orange golden faceted teardrop; row5 variants row4 AmethystDream violetpurple teardrop; row6 holidayrow1 HauntedAmethyst purple squat rounded gemstone with pointed crown, row7 holidayrow2 IceCrystal angular sixpoint blue icy crystal star. Preserve silhouette topology, no petal shapes on teardrop rows.
```

## companion-homeposes-prism-b.png

Actual source: 948 by 1659 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1792, EXACTLY4columnsÃ—7rows (28sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.

References prism-poses.png are identity/style references; prism-variants.png lists four variant rows and prism-holiday-bodies.png lists nine holiday rows. Use references only; create the four NEW whole-body rear/climb poses described, never reproduce their front-facing expressions.
EXACT ROWS top to bottom:
1. prism-heavensturn (Fortune Ruby)
2. prism-valentione (Rose Quartz)
3. prism-littleladies (Sakura Crystal)
4. prism-hatchingtide (Egg Opal)
5. prism-makeitrain (Golden Citrine)
6. prism-moonfirefaire (Ocean Crystal)
7. prism-therising (Astral Diamond)
Species-specific: Prism is a single faceted crystal body. Preserve the exact distinct holiday silhouette (star, heart, flower, egg, diamond, curled wave) and faceting. No detached facets, no new appendages, no flat front face patch on its back. Rear is entirely faceted material.
CRITICAL EXACT SOURCE ROW MAPPING: FIRST outputrow FortuneRuby from holiday reference row THREE: rich ruby red faceted rounded gemstone with gold facets near base, no purple. Outputrow2 sourceholidayrow4 RoseQuartz pink heart shaped faceted gemstone. Outputrow3 sourceholidayrow5 SakuraCrystal pink five-lobed flower shaped faceted gemstone. Outputrow4 sourceholidayrow6 EggOpal paleiridescentfacetedegg. Outputrow5 sourceholidayrow7 GoldenCitrine goldenpointedfacetedgem. Outputrow6 sourceholidayrow8 OceanCrystal curledturquoisewave silhouette, facetedwaterbody. Outputrow7 sourceholidayrow9 AstralDiamond darkmidnightblue diamond with goldstarburstfacets. Keep EACH holiday distinct shape fromreference. No face patches, all rears rendered as fully wrapping gemmaterial.
```

## companion-homeposes-newcomers.png

Actual source: 1122 by 1402 pixels.

```text
Use case: stylized-concept. Asset: transparent painted full-body sprite animation atlas for existing cute companions.
Generate ONE PORTRAIT RGBA atlas, target1024x1280, EXACTLY4columns×5rows (20sprites). Genuine transparent background; NO text, labels, checkerboard, floor, cast shadows, house, bed, separate accessories or scattered particles. Keep original alpha. Clear empty gutters between all sprites, entire silhouette safely inside own cell with25px minimum margins. Characters~170pxwide in256pxcells; consistent row baseline and center. Match reference fullbodyidentity,palette,material,patterns,topology; polished glossy hand-painted storybook game art readable96px.
FOUR COLUMN POSES for EVERY row:
1 three-quarter BACK turn RIGHT, rounded back dominates, only tiny far-right side-eye sliver allowed.
2 FULL REAR low anticipatoryCROUCH; whole body squat, completely NOeyes/mouth/face.
3 FULL REAR climbSTRETCH, whole body taller leaning away, slight unified haunchwiggleLEFT; NOface.
4 FULL REAR climbSETTLE, lower forward-compressed body, slight unified haunchwiggleRIGHT; NOface.
Each pose one complete connected LIMBLESS body; no hands/feet/tails/independentparts. Surface motifs wrap around rear and followturn/squash; don't paint front cream facepatches onback. Keep volume/mass consistent, visiblydifferent authoredsilhouettes across4columns; no4duplicates. Allbodyshapes mustremainrecognizableoriginalspecies, no invented animals, no new hats/clothes. Remove detached orbiting rings, sparks and floating decorations. Motifs intrinsic tobody remain. No baked scene or background. Preserve exactlook colors/patterns across eachrow's4poses.
INPUTREFERENCES: five wardrobeatlases in order nib,echo,fizz,glim,rune. Use ONLY TOPROW ORIGINAL BAREBODY identity fromeachreference, absolutelyNOhats/clothing fromlowerrows.
FIVE ROWS EXACTORDER:
1 NIB: dark indigo/nearblack glossy INKY TEARDROP with curledtop and subtle purple/cyan nebula edge highlights. Wholebody remains dark, notwhite. Notaflame.
2 ECHO: creamy lavender/lilac soft GHOST/JELLY TEARDROP with curvedtopandunified roundedsideflange, gentlelavendersheen, noforeheadcrescent onrear.
3 FIZZ: round opalescent transparent SOAPBUBBLE spirit, sphere-like withiridescentpastelrim andwhitepearlescent reflections, noarms/legs/floatingbubbles.
4 GLIM: compact plump GOLDEN FIVEPOINTSTAR, connectedfivepointssilhouette unifiedbody, noindependentlyriggedarms/legs, softglossygold, noextraNova-likecurl.
5 RUNE: smooth rounded grayteal PEBBLE/RUNESTONE dome, subtle facetedstoneplanes and embedded bright CYAN DIAMOND runes aroundsurface; no appendages. Keep graybody andcyanrunepattern onrear, notpink/notpurple.
Same4columns TurnBack,RearCrouch,RearClimbA,RearClimbB. Everyidentity4poses andnoaddedcharacters. Fullbodiesconnected, mostlybackviews,noeyes/mouthon3fullrearposes. Enoughtransparentgutters thatrectangularUVcropsneverincludeanyneighbor, max60percentcellwidth ifnecessary. Intended96pxindoors.
```
