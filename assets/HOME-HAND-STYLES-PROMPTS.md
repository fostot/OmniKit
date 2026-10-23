# Companion home and hand styles

Original transparent artwork generated with the built-in image-generation tool.
The existing `companion-house.png` and `companion-grab-hand.png` were inspected
and supplied as style references. No game screenshot, player data, account
material or private browser/profile content was supplied. The original default
assets remain unchanged.

| Asset | Dimensions | SHA-256 |
| --- | --- | --- |
| `homes-standard.png` | 2172 × 724 | `95bd641067b41085e1acdf63a3340330674af12742f622d168be6e91bac6a3b4` |
| `homes-holiday.png` | 1254 × 1254 | `b5e19afc81a46af001da20983b7970a4b78e7d49c75482bcbc23094ef9eeed05` |
| `hands-styles.png` | 1254 × 1254 | `882707fd4c78425dffff8f24144480a8215b2ddf9dcaeecfa8aa2c2effc2f3be` |

All three PNGs are byte-identical to the tool outputs. No source-image retouching,
resizing, recoloring or recompression was performed. Diagnostic composites are
separate ignored files. `companion-styles.frames.json` records individually
measured crops, doorway polygons, beds, thresholds, chimneys and hand contacts;
atlas cells are not assumed to be perfectly uniform. UVs use exclusive pixel
crop bounds divided by the full atlas size.

The nine holiday designs express annual event families, not event dates or IDs.
The default house and hand remain available alongside the new styles. The hand
atlas rows are IvoryChibi, StarlightGlove, MagicalPaw and HologramMitt; columns are
Ready, OpenReach, ClosedHold and Release. Every new pose uses the same source
scale of 360 pixels per logical cell and its measured grip. Closed hands are not
independently enlarged to fill their cells.

Source-component and 96/192 px composites were visually inspected. The native
ImGui style renderer was also checked with all home variants, both facing
directions and existing Wink/Moonblob sleeping art, plus every hand pose.
Doorway masks are based on visually measured approximate polygons, not painted
alpha masks. The parent runtime's entry/wake transitions and carried-object
interaction still require integration and live-game checks.

## Exact generation prompts

### Everyday homes

```text
Use case: stylized-concept. Asset type: original transparent 2D pet-home sprite atlas for a polished FFXIV-inspired overlay. Input image 1 is a STYLE AND FUNCTION reference only: the cozy painterly dimensional miniature, clean strong silhouette, visible open doorway and empty cushion. Do not reproduce the reference house.
Create EXACTLY THREE distinct miniature homes, arranged left-to-right in three equal cells on a wide 3:1 atlas, completely isolated with generous transparent gutters. No grid, borders, lettering or labels. LEFT CrystalPod: a pearly translucent lavender crystal shell with curved faceted blue roof, small silver star fittings and a soft violet bed. CENTER CloudCushion: a soft ivory cloud-shaped enclosed canopy home with blue-lilac edging and a plush peach cushion inside; it is clearly a small shelter with a roof and open doorway, not just a loose cushion. RIGHT LanternHut: a rounded warm amber paper-lantern-and-timber cottage with navy curved roof, subtle gold trim and soft moss-green bed.
All three match the same three-quarter front perspective and apparent size. Each entire object fits inside its cell. EACH has one wide permanently OPEN arched doorway on the front-right side, whose center is approximately 66 percent across the house silhouette. The doorway is tall and unmistakable, with its empty plush bed visible through it. Bed bottom/threshold near 87 percent of the house height. No door panel blocking the opening. Give every house a small visible chimney or capped vent on the upper-left roof so animation can emit particles above it later; do not paint smoke. At 96 pixels wide the opening and soft bed must remain clear. Cute premium painterly JRPG game materials, restrained detail, softly rounded forms, soft upper-left light, no hard black outlines. Genuine transparent RGBA background, clean alpha edges. No occupant, animal, character, face, hand, text, Z letters, particles, outside shadow, ground plane, scene, logo or watermark. Produce one atlas only.
```

### Holiday homes

```text
Use case: stylized-concept. Asset type: original transparent 3x3 sprite atlas of NINE seasonal miniature companion homes for a polished FFXIV-inspired game overlay. Input image 1 is a STYLE AND FUNCTION reference only: cozy painterly dimensional miniature, clear silhouette, visible open doorway and empty cushion. Design new seasonal homes rather than recoloring or duplicating the reference.
EXACTLY NINE isolated complete homes in a strict 3-column by 3-row grid, one per equal square cell, same scale, same three-quarter front perspective, generous transparent gutters. No grid lines, captions, lettering or labels. Read left-to-right, top-to-bottom:
1 All Saints' Wake: cozy pumpkin and violet witch-roof cottage, subtle bat-shaped roof trim, orange warm doorway, plum bed.
2 Starlight Celebration: snowy deep-red-and-evergreen cottage, restrained star ornaments and festive ribbon, cream bed.
3 Heavensturn: red lacquer and ivory paper miniature shrine-style home with gold rope crest and tiny bell, blue bed; no specific zodiac animal or writing.
4 Valentione's Day: blush pink and chocolate-brown heart-roof cottage, small rose decorations, velvet rose bed.
5 Little Ladies' Day: peach-blossom ivory cottage with a curved pale-pink roof, delicate fan-shaped roof detail and lilac bed.
6 Hatching-tide: pastel speckled egg-shell pod home, pale mint roof and spring ribbon, soft yellow bed.
7 Make It Rain: tiny teal-and-gold show pavilion home with a rounded gold roof, coin-shaped and diamond-shaped trim, burgundy bed; no signs or text.
8 Moonfire Faire: bamboo beach-lantern hut, turquoise and coral hibiscus trim, a soft palm-leaf roof, pale aqua bed.
9 The Rising: indigo and silver star-roof cottage with elegant celebratory comet-embossed trim, soft blue bed.
EACH house MUST have one large permanently OPEN front-right doorway at about 66 percent of its silhouette width, an unmistakably visible empty plush bed inside, and a distinct threshold about 87 percent of silhouette height. Doorway aperture must occupy a substantial lower-right part of the home; do not make it a tiny dark hole, window or closed door. EACH has a small visible chimney/capped roof vent at upper left for later animation; paint no smoke. Keep houses readable at 96 pixels wide: simplified clear shapes, cozy rounded proportions, premium softly painted JRPG materials, soft upper-left light. Preserve genuine transparent RGBA background. No occupant, animal, character, hand, face, sleep letters, numbers, Zs, particles outside silhouette, scene, floor, cast shadow, watermark or logo. One atlas only.
```

### Hand styles and poses

```text
Use case: stylized-concept. Asset type: transparent original 4x4 grab-hand sprite atlas for a polished cute JRPG overlay. Input image 1 is a style/function reference only for a descending plush glove, short cuff and gentle EMPTY pinch. Create four visually distinct styles, not a copy of the source.
EXACTLY SIXTEEN isolated complete hand poses on a strict FOUR-COLUMN by FOUR-ROW atlas. Every equal square cell has generous transparent margins. NO grid, labels, numbering, text or props. Wrist at top-center; same downward orientation, upper-left lighting, size and grip contact point in EVERY cell. Grip center is at 50 percent cell width and 77 percent cell height; keep that point visually consistent as fingers open and close.
Rows are styles:
Row 1: Ivory chibi glove: soft pearly ivory plush rounded fingers, tiny peach shading, pale teal cuff with a small gold crescent.
Row 2: Starlight glove: cozy deep-navy velvet glove with restrained gold star embroidery, ivory fluffy cuff and a tiny gold clasp.
Row 3: Magical paw: an adorable lavender plush PAW-SHAPED glove, rounded padded digits including an opposing thumb capable of a gentle pinch, pale pink pads, violet cuff with a small silver charm. No animal arm, body or face.
Row 4: Hologram mitt: a solid readable translucent cyan-blue rounded cartoon glove with a soft luminous rim, sparse geometric seams and small violet cuff. Not a wireframe; maintain clear fingers and a consistent silhouette.
Columns are poses, repeated for every row:
Column1 READY: relaxed open rounded fingers descending, thumb apart, awaiting a grab.
Column2 OPEN REACH: fingers extended downward around an invisible object below the palm, clear open pinch gap centered at the stated contact point.
Column3 CLOSED HOLD: gentle thumb/index pinch closing at that exact contact point, other fingers tucked naturally, empty grip; not a punching fist.
Column4 RELEASE: fingers gently reopening around that same contact point after release, wrist/cuff unchanged.
Premium painterly game-asset finish, clear clean silhouette readable at 64-96px, cute and dimensional, soft restrained material highlights. Genuine transparent RGBA background and generous alpha gutters around each cell. No person, body, extended forearm, pet, house, held object, floating prop, actual star particles, glow outside cell, cast shadow, ground, text, watermark or extra hands. One atlas only.
```
