# Seasonal companion accessories

Original art generated with the built-in Codex image generation tool on 2026-10-22. One generation, no repair call, no external image API and no pixel editing of the shipped PNG. The file is a byte-identical copy of the returned original. The artwork contains original fictional accessories, no official game logos or character designs.

- File: `seasonal-accessories.png`
- Size: 1254 × 1254, RGBA with genuine transparency.
- SHA-256: `236b61c902a0cef852ef46d861d5e4d778c3755cff429969ec87a5a2377d0058`
- Layout: nine items in three columns and three rows, in the exact prompt order below.
- Measured separators: X = 0, 427, 828, 1254; Y = 0, 445, 832, 1254. These are measured crops, not assumed equal-grid UVs.
- [Attachment metadata](seasonal-accessories.frames.json) stores each alpha-bound crop plus five pixels of sampling space, its pivot, width fraction and all eight companions' 24 normal / four nap source anchors.

The runtime keeps the existing whole-body art and animation. It maps each accessory through the body's actual source-to-screen transform, including mirroring and rolling, with no independent accessory animation. Attachments are a front layer and use the same clipping and house-wall occlusion as the body. The renderer never tints or shrinks the body to fit an accessory. Accent colors in metadata are available for optional accessory/aura presentation only.

Each companion keeps its reviewed source scale. For an accessory point P, source pivot V, body attachment A and body source scale S, the body-source point is `A + (P - V) * S * WidthFraction / accessoryWidth`; the body's affine transform places it on screen. Source anchors are measured per pose, so sleeping and compressed bodies keep the same attachment relationship.

Reserve 0.23 logical-cell size on all four sides when accounting for total visual bounds. The exact full-circle Moonblob corner bound reaches 0.223823; quarter-phase samples alone only reached 0.173 and are insufficient. This padding preserves the full configured body size.

All nine themes on all eight companions were rendered using the production renderer and native offscreen ImGui textures at 48, 96 and 192 logical pixels. Both facings of carry/fall/land/push/entrance/sleep, plus shared motion phases, were inspected. The art reads best at the intended 96-pixel size; tiny decorative detail is naturally reduced at 48 pixels. This is offscreen rendering evidence, not live FFXIV validation.

## Exact generation prompt

```text
Use case: stylized-concept.
Asset type: one reusable seasonal ACCESSORY atlas for a family of eight original limbless moon-spirit game companions. These are accessories only, no characters or body duplicates.
Create a square genuinely transparent PNG with exactly THREE columns and THREE rows, nine separately isolated compact wearable clips/miniature headpieces. Equal cells with at least 18% empty transparent gutters around every item. No drawn grid, labels, letters, text, numbers, watermark, background or floor shadow. Every accessory is a single coherent attached cluster, not a collection of detached components. Each has its visual attachment point at its lower center so it can rest on the upper side of an existing character. Slight frontal three-quarter view, matching viewpoint and light for the entire collection.
Style: premium hand-painted storybook JRPG fantasy miniature, softly rounded plush fabric, pearlescent enamel, delicate gold/silver trim, polished painterly dimensional shading, rich but restrained seasonal color. Warm, adorable and readable as a 28–40px accessory on a 96px character. Strong silhouettes and clear large motifs; omit tiny fussy details that disappear at miniature scale. No logos, official game symbols or borrowed character designs.
Exact item order:
ROW 1 LEFT, Heavensturn: compact red silk lucky-knot clip with a small rounded ivory folded-paper fan, subtle warm gold edge and a tiny pine-green center. No writing, no kanji, no zodiac animal.
ROW 1 CENTER, Valentione: blush-rose velvet bow with one large glossy heart-shaped chocolate/enamel center, gold edge. No letters.
ROW 1 RIGHT, Little Ladies: two peach-pink blossoms on one graceful ivory-pink ribbon bow with a small pearl center; petals soft and generously rounded.
ROW 2 LEFT, Hatching-tide: a small rounded pastel decorated egg nestled in one mint/lilac ribbon crown, pale-yellow accents. No rabbit ears, animals, arms or feet.
ROW 2 CENTER, Make It Rain: a tiny plush midnight-blue show top-hat clip with a broad gold ribbon and two overlapping round gold coin medallions attached at the front, coin surfaces unlettered and unbranded.
ROW 2 RIGHT, Moonfire Faire: a miniature warm straw sunhat clip, turquoise ribbon and a single coral hibiscus attached at one side. Compact, readable, soft brim.
ROW 3 LEFT, The Rising: an opalescent silver-gold rounded star crest centered on a deep twilight-blue celebration bow, subtle warm gold rim. No detached confetti.
ROW 3 CENTER, All Saints' Wake: a charming short floppy violet witch-cap clip with a broad cinnamon-orange band and one round pumpkin brooch; softly curled tip and brim, not tall/sharp, no bat wings, no face or limbs.
ROW 3 RIGHT, Starlight Celebration: a small cozy cranberry-red stocking-cap clip with thick cream plush brim, attached cream pompom and a warm golden star brooch. Whole cap compact and rounded.
Keep the nine themes visually distinct yet from the same handmade fantasy wardrobe. Accessories must be fully visible, isolated with generous true-alpha padding, uncut, and not touch any neighboring cell. No people, no creatures, no eyes, no mascot heads, no hands, no shoulders, no body silhouettes, no pedestals.
```

## Event identification

These accessories are independent original designs themed to the nine annual event families. Native recognition uses read-only `GameMain.ActiveFestivals` and `PlayerState.ActiveFestivalIds`/`ActiveFestivalPhases`, then static `Quest.Festival` → `JournalGenre` relationships. Festival IDs 11 and 22 are ambiguous; combined or conflicting families do not silently select one. Native appearance evidence does not establish seasonal quest availability.

Primary API references: [GameMain](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/GameMain.cs), [PlayerState](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/PlayerState.cs), [ZoneInitPacket](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Network/ZoneInitPacket.cs), [Quest schema](https://raw.githubusercontent.com/xivdev/EXDSchema/latest/Quest.yml).

When native state is unavailable, the policy may use an explicitly labelled, dated official 2026 schedule. It never guesses by month, repeats an old date in a future year, or replaces a confirmed empty or unrecognized native state. There is no verified Starlight 2026 date fallback. Exact supported windows and official source URLs are in `SeasonalCompanionPolicy.cs`; a scheduled result exposes its source URL. Manual appearance selection is separate from event detection.
