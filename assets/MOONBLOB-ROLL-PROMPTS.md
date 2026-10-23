# Moonblob limbless rolling atlas provenance

Tool: built-in `image_gen.imagegen`, with `transparent_background: true` for both calls.

Final asset: `assets/moonblob-limbless.png`, copied byte-for-byte from generated output `exec-2139a102-f40a-4c93-89e7-5f8969c03407.png`. SHA-256: `6c88b89d296e413a81dedb8a13c4fef94203f5ffc00707fb350fd8276b75ca15`.

Initial output: `exec-c5aaa6ff-b1b4-474c-a288-4c0036de9943.png`. Its reference was the existing `assets/moonblob-motion.png` for pearly jelly material and facial style. One targeted repair corrected the top-right rolling face from nearly vertical to a diagonal 135-degree pose. The repair used the initial output as its reference. No offline image edits were applied to either source output or the final project PNG. Diagnostic crops, contact sheets and GIF previews are separate under `.local`.

The final image contains one 24-frame sheet. Production rolling rotates stable frame 0 continuously using travelled distance; authored frames 1-7 document the intended full-body tumble but are not exchanged during that movement. Frames 8-23 supply the other expressions and physical reactions. All inspected silhouettes are limbless.

Metadata: `.local/moonblob-limbless.frames.json`. Original RGBA 1024x1536. Alpha range 0-254, 691415 completely transparent pixels. Crops use alpha>=16 bounds plus five pixels, max crop edge alpha 2. A single 305px source scale and destination anchor (0.5,0.9) fit every complete draw rectangle into [0,1]. No per-frame rescaling.

Motion diagnostic: `.local/moonblob-limbless-roll.gif`, eight looping frames at 130ms each, rendered at 192px and actual 96px with common scale and anchors. The rolling face orientations were inspected sequentially, with frame 3 repaired. Live animation playback could not be observed through the available computer-use surface because no browser or native surface was enabled; the animated GIF is provided for actual moving review. No claim of in-game validation is made.

## Initial generation prompt

Use case: identity-preserve / stylized-concept. Asset type: transparent 2D animation sprite atlas.
Use the supplied previous Moonblob sheet ONLY as a reference for its adorable pearly jelly material, expressive glossy eyes, rosy blush and polished painterly JRPG art quality. Redesign Moonblob as a COMPLETELY LIMBLESS ROLLING ORB. REMOVE every foot, flipper, arm, hand, nub, ear, tail, protruding bead, lobe and appendage in EVERY frame. Its entire silhouette must be ONE continuous smooth organic ball/oval of soft translucent pearly jelly. A face painted within the orb and a small internal crescent gleam may remain. No external limbs at all, including in carry/fall/push states.
Create exactly 24 complete isolated sprites in 4 columns × 6 rows, portrait 1024×1536 canvas preferred. Same character, scale and view. Beautiful dimensional creamy pearl/silver jelly with warm blush, big glossy dark eyes, tiny cute mouth, tiny INTERNAL sparkles. Genuine transparent alpha background; no ground, shadow, scene, grid, captions, labels, numbers or watermark. Keep entire bodies within each cell with generous transparent gutters. No floating decorations outside the silhouette.
CRITICAL: the first 8 frames are an ordered CLOCKWISE FULL BODY ROLL, not a bounce or left-right wobble. The face rotates rigidly WITH the ball through one complete revolution. Show exactly these consecutive face orientations left to right across the first TWO rows:
Frame 0: upright face at 0 degrees, eyes horizontal above mouth.
Frame 1: whole face tilted 45 degrees CLOCKWISE.
Frame 2: whole face at 90 degrees CLOCKWISE, eye line vertical, mouth toward the LEFT side of the orb.
Frame 3: whole face at 135 degrees CLOCKWISE, diagonally upside-down.
Frame 4: whole face at 180 degrees, completely UPSIDE-DOWN, mouth ABOVE the two eyes.
Frame 5: whole face at 225 degrees CLOCKWISE, upside-down diagonal.
Frame 6: whole face at 270 degrees CLOCKWISE, eye line vertical, mouth toward the RIGHT side of the orb.
Frame 7: whole face at 315 degrees CLOCKWISE, approaching upright, ready to loop cleanly to frame0.
Keep the whole round jelly body centered with a subtle flattening at the bottom contact point in rolling frames. Do not restore upright eyes between these angles. This must visibly tumble when played in order 0,1,2,3,4,5,6,7,0. A small internal crescent/light marking rotates together with the face, helping the rolling motion read.
The remaining four rows have an upright face and NO LIMBS:
Row3 frames8-11: relaxed open eyes; gentle breathing slight expansion; half-closed eyelids; fully closed happy blink. Smooth limbless orb in each.
Row4 frames12-15: tiny closed smile; small round O mouth; open oval mouth; delighted wide open smile. Eyes open, smooth limbless orb.
Row5 frames16-19: picked-up vertically stretched jelly oval; second gently tilted dangling jelly oval; falling round ball surprised; falling vertical elongated smooth oval. No hands/grabbers or nubs.
Row6 frames20-23: landing broad low jelly squash; tall smooth rebound; pushing invisible left edge by flattening the orb's LEFT BODY SURFACE with determined cute face; second leftward push squash. NO arm, foot or protrusion used for pushing. No wall shown.
Every sprite must retain one single smooth blob silhouette. Prioritize beautiful cute consistent art, strict limb removal, genuinely sequential full rolling rotation in first8, separated complete silhouettes and alpha transparency.

## Targeted repair prompt

Use case: precise-object-edit. Edit this transparent Moonblob animation sheet with ONE targeted correction only.
Preserve the complete 4-column × 6-row 24-frame atlas, same canvas dimensions, transparency, all character materials and colors, all silhouettes, all spacing, and EVERY other sprite unchanged.
CHANGE ONLY THE FACE ORIENTATION of the FOURTH SPRITE in the TOP ROW (frame3, row1 column4, upper-right orb). That rolling face currently looks almost vertical, too similar to the preceding90-degree frame. It must instead be the missing135-degree CLOCKWISE rolling pose between the90-degree third sprite and180-degree upside-down first sprite of row2.
For that fourth sprite only: place its two eyes on a CLEAR45-DEGREE DIAGONAL from UPPER RIGHT to LOWER LEFT. Put the mouth clearly on the UPPER LEFT side of the midpoint between the eyes. The face must be diagonally upside-down at135degrees, not vertical, not upright. Approximate centers within that orb: one eye in its upper-right face quadrant, the other eye in its lower-left face quadrant, mouth in its upper-left face quadrant. Rotate the eye highlights/eyelids and mouth consistently with the face. Retain both glossy eyes, tiny happy open mouth, rosy blush and the same jelly body.
Keep all24 sprites entirely LIMBLESS: a single smooth pearly jelly orb/oval silhouette, no feet/nubs/arms/hands/flippers/ears/tails/protrusions. Do not add props, marks, text, backgrounds, grids, labels, extra sprites or external sparkles.
All other23 sprites and their pose/expression/face orientations stay unchanged. Preserve genuine transparent alpha.
