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
