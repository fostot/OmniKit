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
content scrolls independently and the footer reserves its own space for actionable status,
the compiled assembly's version label and the author Zero.
About also shows the full source commit and a copyable build-details summary.
Controls reveal feature-specific options only when enabled.
Routine instructions live in tooltips and disclosures. Repeated Saved messages
are omitted; semantic colors and subtle text outlines emphasize actual state.

Shortcut controls are clickable recorders. Hold Ctrl, Alt and/or Shift, press
one main key, then release every key before the new chord is committed. Escape
cancels. Capture feedback must remain visible, fit the available width, and stop
when its control is hidden or the settings window closes.

The interaction HUD is styled text without a window panel. It hides during NPC
dialogue, active NPC/object interactions and cutscenes. Other paused actions hide
normal target text; explicit HUD editing may display its labelled sample.
Passive player inventory does not pause interaction unless text input or another
blocking state is active. Shift +
right-click exposes Move, Lock/Unlock, Hide and Settings. Modify opens the HUD tab
at the top, with font size, independent outline and readable text effects first.
A clearly labelled sample appears only during editing, including with the
gameplay HUD disabled or no candidate. Interaction suppression wins over editing.
Eleven effects retain an opaque readable text core. Advanced intensity, speed
and animation controls are collapsed initially; outline is independent.

Bimble and Moonbear use separate Navigator (navy/gold) and Stargazer
(violet/silver) painted parts for continuous joint-driven walking. One foot
stays planted while the other swings; the same body image keeps its proportions.
Bimble's upper ear bands have restrained delayed motion. Moonblob has no limbs
and rolls continuously from one stable image. Wink and Nova use stable floating
images while moving. Cadence follows travelled distance, not a free-running frame
counter. Bimble's rebuilt idle and walk share the same artwork and renderer,
with a bounded 180ms grounded settle. Other idle and reaction poses retain
their illustrated atlas artwork. Six palettes
control the aura and softly tint these three companions: Moonlight, Aurora,
Nebula, Rose, Ember and Starlight. The saved screen rectangle remains the default
roaming area. Optional Frame interaction lets the user pick up a companion and
place it on a supported visible frame's top or inside it. Its physics follow
the frame, handle edges and recover safely when the frame disappears. A click
opens its conversation; a drag does not. Mouth movement is an explicit preview, without
synthesized speech or audio-driven lip sync.
An optional house anchors the saved area, faces inward, and moves the entire
area when dragged. Falling reaches the screen floor before walking home.
Optional open/closing/hold/release hand cues belong to pets and the house.
Comments are occasional local text, never a background AI request. Wink's
independent eyelid closes while the approved floating body stays stable.

Magic accents, animation and the companion can be disabled independently.
Disabling animation leaves the companion still. The idle search field remains
free of companion art.

The AI subtab must always allow returning to Appearance and Voice with actual
mouse clicks. Native tab state and programmatic tab selection must agree.
Connection-specific options occupy the scrolling content area; the version
footer remains separate. ChatGPT browser sign-in, CLI checks and model loading
run asynchronously with visible progress and cancellation.
Ask is disabled while an inference is active. CLI chat is visibly paused until
its internal request count can be constrained; connection checks remain usable.

The browser shows the website with one menu icon. Navigation, address, move,
resize and extension controls appear only when requested. It has an owned
profile and parented game window, with no silent external-browser fallback.
Optional same-process link capture is a separate default-off Data setting;
its status and tooltip explain supported opens and authentication exceptions.

Growing content uses scrollable regions; launcher height follows its
query/results state. Keep visible gutters from borders and dividers, and check
bounds at the smallest supported size and across expanded states at Dalamud's
UI scale.

Umbra's widget uses Umbra's own theme, icons, menu and dimensions. Runtime game
screenshots, Escape handling and return of keyboard focus remain part of the
acceptance checklist.
