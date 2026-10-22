# UI direction

OmniKit uses a quiet charcoal surface and cream text. Settings controls use muted
teal accents and 2 px corner rounding, with compact custom move/close controls
instead of a native title bar.

Alt + Space opens a compact launcher containing only the search field and a
settings gear. An empty query shows no results, title, header, avatar, tabs or
footer. Typing reveals the matching result list below the field; clearing the
query collapses it again. The gear opens settings.

The moon companion has a separate window, opened from settings. It contains
welcome guidance, voice controls and optional AI chat. First-run guidance
explains the local command matcher before offering optional AI.

Settings use native tabs for Launcher, Interact, HUD, Companion, Data and About.
Companion groups Appearance, Voice and AI in a second tab bar. The initial
settings size is 540 × 400 logical pixels, with a minimum of 480 × 330. Tab
content scrolls independently and the footer reserves its own space for status,
the compiled assembly's version label and the author Zero.
About also shows the full source commit and a copyable build-details summary.
Controls reveal feature-specific options only when enabled.

Shortcut controls are clickable recorders. Hold Ctrl, Alt and/or Shift, press
one main key, then release every key before the new chord is committed. Escape
cancels. Capture feedback must remain visible, fit the available width, and stop
when its control is hidden or the settings window closes.

The interaction HUD is styled text without a window panel. It keeps the camera
candidate visible as a preview when actions are paused. Shift + right-click
exposes Move, Lock/Unlock, Hide and Settings; position and scale are adjustable.

Bimble and Moonbear use separate Navigator (navy/gold) and Stargazer
(violet/silver) outfit atlases. Each atlas contains 8 walking, 4 idle/blink and
4 mouth poses. Moonblob, Wink and Nova are vector companions. Six palettes
control the aura or vector shape: Moonlight, Aurora, Nebula, Rose, Ember and
Starlight. These companions stay inside a user-drawn screen rectangle; clicking
one opens its conversation. Mouth movement is an explicit preview, without
synthesized speech or audio-driven lip sync.

Magic accents, animation and the companion can be disabled independently.
Disabling animation leaves the companion still. The idle search field remains
free of companion art.

Growing content uses scrollable regions; launcher height follows its
query/results state. Keep visible gutters from borders and dividers, and check
bounds at the smallest supported size and across expanded states at Dalamud's
UI scale.

Umbra's widget uses Umbra's own theme, icons, menu and dimensions. Runtime game
screenshots, Escape handling and return of keyboard focus remain part of the
acceptance checklist.
