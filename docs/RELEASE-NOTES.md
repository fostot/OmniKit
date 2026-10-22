# 0.1.0.0003: compact settings, companion choices and commit-derived versioning

The settings and companion panels now use a compact frameless layout with teal
controls and 2 px corner rounding. They open at 540 × 400 logical pixels and
support a minimum of 480 × 330. Settings have six native tabs, including About,
and the Appearance, Voice and AI companion subtabs. The footer shows the complete
version label from the compiled assembly and identifies the author as Zero.
About includes the full source commit and a Copy build details button.

Detection distance is now directly visible in Interact, adjustable from 1–30
yalms. New settings default to 6; existing saved distances are preserved. This
changes target detection, while native game checks still determine usable reach.

Appearance controls support Bimble and Moonbear in Navigator (navy/gold) and
Stargazer (violet/silver) outfits, backed by separate atlases. Each atlas has
16 poses: 8 walking, 4 idle/blink and 4 mouth poses. The vector companions
Moonblob, Wink and Nova offer six aura/shape palettes: Moonlight, Aurora,
Nebula, Rose, Ember and Starlight. Magic accents and animation have independent
off switches. Mouth movement is available through explicit animation preview;
this release does not synthesize speech or provide audio-driven lip sync.

Settings schema v3 adds outfit, palette and magic preferences. Existing v1 and
v2 JSON and SQLite snapshots migrate with their saved preferences intact,
including v2 keyboard chords. Migration converts legacy v1 modifier codes only
once, then adds defaults for the new appearance fields.

All four atlases passed transparency, frame-bounds and composited appearance
inspection. Motion/layout checks passed, as did offscreen vector rendering,
disabled-animation checks and native settings/dropdown/About layout checks.
The user reported that basic soft interaction works in game at close range.
The updated appearance and distance controls still require in-game acceptance;
isolated checks do not establish live keyboard focus or gameplay behavior.

New artifacts use `major.minor.patch.commitCount`, displayed with a short source
hash, such as the illustrative `0.1.0.0003 (abc1234)`. The base remains `0.1.0`.
The last component counts commits reachable from the artifact's source commit;
it is padded to at least four digits and does not increase when that same source
is rebuilt. .NET and Dalamud use the unpadded numeric equivalent, such as
`0.1.0.3`.

Builds require a full Git checkout. The release tag points to the source commit
used to build the artifact, even if publishing its repository feed creates a
later metadata commit. The final count and hash are determined only after the
source commit is finalized. See [versioning](VERSIONING.md) for the workflow.

The shared MSBuild targets apply the public author `Zero` and derive metadata
for both plugin assemblies. Release assets now include their padded version and
short source hash: `OmniKit-<version>-<shortSHA>.zip` and
`OmniKit.Umbra-<version>-<shortSHA>.dll`.

Integration checks passed using the actual targets: a first-commit build, an
incremental second-commit build after a metadata-only change, and rejection of
missing or shallow Git history. The checks inspect emitted JSON, DLL versions
and embedded source attributes using synthetic local repositories.

The prior `0.1.0-dev.1` and `0.1.0-dev.2` release identities and artifacts remain
unchanged. This metadata change does not replace the pending in-game acceptance
checks recorded below.

# 0.1.0-dev.2

The launcher now opens as a compact search field and settings gear. Results
appear after you type. The idle launcher has no title, avatar, tabs, footer or
suggested entries. Welcome guidance, the moon companion, voice controls and
optional AI chat live in a separate companion window, available from settings.

Settings use five native tabs: Launcher, Interact, HUD, Companion and Data.
Companion contains Appearance, Voice and AI tabs. Click a shortcut to record it,
hold any combination of Ctrl, Alt and Shift, press the main key, then release
all keys to save the complete chord. Escape cancels capture. Runtime shortcuts
require the exact modifiers and a fresh main-key press.

The target HUD uses styled text without a panel. It continues to show the
camera candidate as a preview while interaction is paused by menus or text
input. Shift + right-click opens its Move, Lock/Unlock, Hide and Settings menu.
Position and text scale are saved separately.

Bimble and Moonbear now have generated 2D walking sprites. Draw a roaming area
in Companion → Appearance to keep the selected companion inside that box.
Click the companion to open its conversation; turning animation off keeps it
still. The runtime package includes the `companions-walk.png` sprite atlas.

Settings schema v2 adds full shortcut chords, HUD positioning and the companion
area. Existing v1 JSON and SQLite settings migrate with their prior modifiers
and preferences intact. Invalid imports and failed database migrations preserve
the previous snapshot. Hotkey and storage tests cover release ordering, exact
modifier matching, migration defaults and database-error rollback.

The base version remains 0.1.0. The core numeric assembly/file version is
0.1.0.1 so Dalamud can detect the update. The unchanged Umbra toolbar companion
retains its 0.1.0.0 assembly version. The published 0.1.0-dev.1 release remains
available unchanged.

Packaging checks the project, DLL and generated manifest versions before
creating artifacts, uses the manifest assembly version in the repository feed,
and refuses to overwrite an existing distribution revision.

Native ImGui offscreen launcher checks passed across 42 states. Focused tooltip
reruns and visual review passed after bounds fixes. In-game acceptance remains pending. The compact layout, keyboard
focus, game-key suppression, HUD behavior and companion motion still need
observation in FFXIV; offscreen checks do not establish that behavior.

The actual compiled settings panel was also rendered across 42 tab, expanded
form, scroll-position and UI-scale states. Focused key-capture, interaction-key,
motion-boundary and schema-migration checks passed. These checks do not open a
microphone or execute gameplay actions.

# 0.1.0-dev.1

First installable preview for Windows x64 and Dalamud API 15. The numeric plugin
assembly version is 0.1.0.0; the separate development revision is 0.1.0-dev.1.

Includes the searchable launcher, typed/job/gearset actions, optional camera soft
interaction and target HUD, optional gathering gear switching/restoration, lunar
avatars, one-shot local speech, custom voice phrases, optional text-only AI,
SQLite settings and JSON import/export. Umbra's Plugins menu is supported, with
a separate native toolbar companion. Browsingway can host wiki searches when
the user configures its named overlay.

The build and focused search, storage and gathering-transition tests pass.
Packaged SQLite, Windows DPAPI and System.Speech dependency checks pass without
starting the microphone or game. Native game behavior, visual layout, recognition
quality and page rendering have not yet been verified in FFXIV.

ChatGPT/Claude account sign-in, CLI bridges, advanced character animation,
acoustic voice-model training, and read-only AI knowledge adapters for other
plugins are unfinished. The initial voice practice feature stores phrase/action
bindings; it does not train a speech model. The full scope remains in FEATURES.md.

Install using the custom repository in README.md. Soft interaction, voice and AI
are opt-in. The Umbra companion is a separate extension loaded by Umbra itself.
