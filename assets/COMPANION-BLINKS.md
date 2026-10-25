# Eye-only blink measurements

`companion-blinks.frames.json` contains210 inspected eye regions for112 existing bodies:8 originals,32 everyday variants, and72 v2 holiday looks. The PNG sources are unchanged. Each entry records the exact normal/closed crop pair, one eye for Wink or two for the other companions, and a skin-only sampling rectangle from that same look's closed frame. The sheet list records dimensions and SHA-256 hashes.

Original closed frames were inspected: Moonblob/Wink/Nova use index11, Mochi/Puff/Ember/Prism index10, and the ringless original Orbit uses index2. Ordinary and holiday variants use their named `ClosedBlink` frame. The renderer paints only eyelid sectors and a closing arc onto the normal body's existing roll/float/deformation transform; it does not replace the body texture or borrow another look's color.

Measurements were derived locally from the public PNG sources and reviewed using annotated normal/closed crop galleries. Notable corrections include Ember's bright lower iris, Orbit's separated outer eye opening, and original Wink's eye-to-mouth clearance. No new images were generated and no production pixels were edited. Source-linked native96/192 render checks are separate from live FFXIV validation.

The sampled lids can retain a slight oval color boundary on strongly graded faces. This is an eye-region shading limitation; the body texture, position, and motion do not change during blinking.
