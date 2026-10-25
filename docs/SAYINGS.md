# Companion sayings

Open **Companion → Sayings → More options**. Turn **Local comments** on for occasional local speech
bubbles, then choose the interval. This does not enable AI, voice recording or
synthesized speech and does not spend provider tokens.

The starter library contains **418 original lines**: 80 Universal and 26 for
each of the thirteen companions. A line can match Any, Idle, Walking, Perched,
Returning, Home, Held or Waking. Selection combines Universal and the visible
companion's enabled lines for the current activity, avoiding the last five IDs
and texts where the available pool permits. Sleeping and accessory fitting stay
quiet. A bubble lasts seven seconds and can be dismissed by clicking it.

## Edit your library

Use the companion/context filters and search to browse. Select a line to edit,
disable or remove it; **Add** creates a line. **More options** reveals disabled
and removed entries plus JSON transfer controls. Removed lines can be restored.
An editor opened for the current companion keeps that scope even if automatic
companion rotation happens before Save.

Text is a single plain line, up to 240 Unicode characters. It cannot execute
commands or request AI. Long bubbles fit the available screen; hover over a
shortened line to read the full text. Disabling/removing/editing an active line
or switching companions dismisses stale text.

## Storage and transfer

Sayings use separate tables in the local `omnikit.db`, with their own schema
version. They do not change the settings schema. Edits, disabled states and
removed built-in entries survive plugin reloads and seed-pack updates. The
packaged starter file contains only the public authored library, never your
local edits.

**Sayings JSON** is separate from settings JSON. Export includes the whole
library, including disabled and removed entries. Treat it as private text if
you have added anything personal. Preview an import before merging it; merge
updates matching IDs and adds new IDs without deleting entries absent from the
file. Validation and writes are atomic. Limits are 2,000 entries and one MiB of
JSON. Invalid imports preserve the existing library. Unknown future library
schemas are preserved and leave this feature unavailable.

Synthetic tests use temporary databases. In-game bubble appearance and timing
still require the [game checklist](IN-GAME-CHECKLIST.md).
