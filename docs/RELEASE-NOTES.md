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
