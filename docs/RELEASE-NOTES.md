# 0.1.0.0013: action icons, launcher layout and seasonal accessories

Next Action previews the same guarded resolver used by Interact. Locked, it is
one icon; unlocking shows the NEXT ACTION heading and drag area. A separate lock
click never executes the action. The new Quest Items settings tab also controls
a paged manual bar with up to eight visible buttons. It reads usable non-ground
KeyItems and ordinary bag items explicitly listed by the current native quest
tracker, not arbitrary potion inventory. A selected item is a manual choice,
not proof that it matches the target. Each click rechecks the existing target,
exact stack and native usability before at most one unqueued attempt.

Verified Interact mappings remain limited to Tiller's Shovel in Rocks in a Hard
Place, Fetid Dodo Flesh in Forbidden Island and `/soothe` in A Bridge Too Full.
Their opt-ins remain separate. A blocked matched rule never falls through to
another action; no generic quest solver, automatic target guessing or retries
were added. See [items](QUEST-ITEMS.md) and [emotes](QUEST-EMOTES.md).

Launcher settings now adjust width, text size, visible result count, horizontal
position and distance from the top, with one reset button. The empty launcher
stays input plus gear; typing expands it within the viewport. Results beyond the
visible count remain reachable by scrolling or arrow keys.

Nine shared accessory themes cover All Saints' Wake, Starlight, Heavensturn,
Valentione, Little Ladies' Day, Hatching-tide, Make It Rain, Moonfire Faire and The
Rising. Auto reads supported native festival state, with no month-based guessing.
Only explicit official date windows may substitute when native data is unavailable;
unknown/conflicting native states remain visible as status with manual selection
available. Optional local companion rotation accepts 30–3600 seconds and preserves
the saved manual companion. Wink gains an optional Fourier-smoothed blink.

The pickup hand centers the carried companion or house beneath its grip. Nap
entry uses the front doorway, and walking off supported thin HUD strips falls
toward a lower support or the screen floor. These changes preserve the existing
eight whole-body companions and their motion.

Schema 8 adds 19 explicitly whitelisted fields and migrates versions 1–7 atomically,
preserving prior preferences, chords and local AI consent. Import still cannot
grant new AI transmission consent. The base remains `0.1.0`; this batch targets
source count 13 and label `0.1.0.0013`, with its finalized source hash in the build.
Earlier published artifacts remain unchanged. Isolated checks do not establish
live inventory use, festival detection, input focus or motion inside FFXIV; those
remain on the [acceptance checklist](IN-GAME-CHECKLIST.md).

The larger per-companion appearance collection, selectable everyday/holiday homes,
alternative hands, grounded hopping and rings are pending a later batch and are
not included here.

# 0.1.0.0011: independent homes, naps and browser controls

The active companions are Moonblob (default), Wink, Nova, Mochi, Puff, Orbit,
Ember and Prism. All eight use complete limbless bodies; Moonblob rolls and the
others float. Wink replaces the plugin icon. The earlier rabbit/bear art and rigs
were retired locally and are excluded from current source files and packages;
older published releases and history remain unchanged.

The companion house now has its own saved position. Dragging it leaves the drawn
roaming area unchanged; normal free roaming stays inside that area. All eight
companions have entrance and sleep artwork. Nap trips walk to the house
at about twice normal speed, settle, sleep, wake and return continuously. Defaults
are about 45 seconds awake and 20 asleep, adjustable to 15–300 and 5–120 seconds.
Naps and chimney sleep symbols can be disabled separately. Animation pause freezes
the trip; disabling naps, hiding or moving the house returns without teleporting.
The pickup hand is smaller by default (0.4), with scale adjustable from 0.2–1.
Falls can land on supported frames below. Read-only surface observation includes
compatible NoMouseInputs windows, not every full-screen canvas.

The browser has an always-visible close X. Close/hide stops its helper and engine;
there is no background prewarm. Explicit opens and captured ordinary web links
start it on demand. Session protection starts on, blocking script popups and
requests to an original embedded list of 60 known advertising/tracking hosts.
The session switch allows compatibility exceptions; this is not comprehensive ad
blocking. Native game-UI hover cutouts mitigate overlap, but the reported flicker
still needs verification inside FFXIV.

HUD outline thickness is adjustable from 0.5–4 (default 1) and follows the primary
effect glyphs. Schema 7 atomically migrates versions 1–6 while preserving existing
preferences, chords and local AI consent. Existing house coordinates derive from
the former roaming box. Imports still cannot grant new AI consent.

Use verified quest items now supports Fetid Dodo Flesh on suspicious mounds in
Forbidden Island, alongside Tiller's Shovel in Rocks in a Hard Place. Both are
limited verified mappings with one guarded item request per fresh press; they
do not fight encounters or solve later objectives. See [item scope](QUEST-ITEMS.md).

The new Use verified quest emotes setting starts off. Its first mapped case is
`/soothe` on the irate coachmen in A Bridge Too Full. A fresh press confirms the
target before one emote request, with no talking fallback when the matched rule
is blocked. The mapping is verified; native game execution remains unverified.
See [scope and sources](QUEST-EMOTES.md). This release includes only these two item
cases and one emote case, not a general quest solver.

Focused state/storage checks and native offscreen nap rendering cover the new
behavior. Browser lifecycle/protection checks use a synthetic host. These are
separate from pending FFXIV focus, flicker, surface, fullscreen and visual-motion
acceptance. The base remains 0.1.0; this artifact uses source commit count 11.
Earlier releases remain unchanged.

# 0.1.0.0009: in-game browser, companion homes and expressive HUD

OmniKit's wiki, Community Finder and website actions now open in its own
parented WebView2 browser. The website stays visible while its address/navigation
controls stay hidden behind a menu icon. Unpacked Chromium extensions can be
managed in that menu. The installed WebView2 Evergreen Runtime and .NET Desktop
Runtime are required. Browser errors never silently open the system browser;
official account consent still uses the external provider sign-in flow.
An optional, default-off link interceptor captures standard HTTP/HTTPS
ShellExecute opens made by the same game process while the browser is ready and
has queue space. Recognized auth/non-web links pass through;
custom process launches and links opened by another process are not covered.

Settings now emphasize choices and actionable status, with routine instructions
in tooltips/disclosures, semantic colors and readable outlined labels. The HUD
has eleven styles, including rainbow, neon pulse, chromatic glitch, hologram,
flame, frost and light sweep, with intensity/speed/freeze controls and an
independent outline. Active NPC/object interactions still suppress both target
text and the editing preview.
Other paused states now hide the normal HUD too; only deliberate HUD editing
shows a paused sample. Passive player bags remain usable, with the normal key
label, while text input and unknown focused menus still pause interaction.

Camera-directed detection now uses horizontal aim with vertical screen tolerance,
so a visible target is not lost merely because camera pitch moves it away from
the exact screen center. Range and native visibility/collision checks remain.
On a fresh Interact press, targetable objects are selected and confirmed before
the one native interaction; observation alone never changes the hard target.
Optional **Use verified quest items** starts with Tiller's Shovel on the three
cumbersome rocks in Rocks in a Hard Place. It requires the current accepted
quest/sequence, native tracker item, inventory ownership, unambiguous handler
context and native usability. Debris remains ordinary interaction. Other quests
still require manual item use; the current public Questionable/Tsukimichi IPC
does not expose a complete generic required-item/target mapping.

Companions can fall to the screen floor and walk continuously back to their
saved area. An optional draggable moon house moves that area and faces inward.
A four-pose hand signals hovering, pickup and release; the supported ImGui hand
cursor is used over draggable objects. Optional occasional comments are local
text only. Wink now closes its eye while floating. Bimble and Moonbear's body
fit, separate sleeve/hip attachments and lower-limb fullness are corrected.
Arms sit under the cape; paws retain the source artwork's aspect. Bimble was
then rebuilt with fresh Navigator/Stargazer part artwork and a shared idle/walk
renderer: a bounded 180ms transition settles both paws without changing his
body or resetting walked-distance phase. Moonbear retains a silhouette
difference between its separate idle/walk art. Gait timing and sole paths are unchanged. Other
approved companion walking/rolling/floating motion is preserved.

Each explicit Ask permits at most one inference request, with no queued duplicate,
automatic retry, provider fallback or tool continuation. Overlapping requests are
rejected. Codex/Claude CLI sign-in and connection checks remain available, but CLI
generation is paused because their internal inference/retry count cannot be
verified. Direct ChatGPT and Responses-compatible API chat remain available.
This bounds request count, not exact provider token usage or charges.

Schema v6 migrates v1–v5 settings atomically and adds effect, home, hand, comment
and blink preferences. Browser preferences migrate to the built-in route.

Validation includes an actual out-of-process WebView2 child on a synthetic
host, navigation/protocol/lifecycle checks, and a real synthetic MV3 extension
install/disable/enable/remove cycle. Native minimum-size UI/effect renders,
motion/physics checks, storage migrations and synthetic counting HTTP tests
passed. None establishes actual FFXIV focus, fullscreen, mixed-DPI or live frame
compatibility; those remain on the game acceptance checklist. No paid inference
or real account credential was needed for these tests. The base remains 0.1.0.

# 0.1.0.0007: HUD editing, coherent movement and ChatGPT models

The visible-target HUD hides during active NPC/object interactions, dialogue
and cutscenes, including its editing sample. Other paused actions no longer replace the live target with a Preview
suffix. Modify opens HUD settings at the top, with font size first. Size and
position remain editable when the gameplay HUD is disabled. An explicit editing
sample provides feedback without a target. A separate outline toggle and None,
Shadow, Soft glow and Gentle shimmer effects are saved locally. Schema v5
migrates older settings while retaining the previous shadow appearance.

Bimble and Moonbear now walk using painted 2D body/limb parts with fixed
proportions, alternating planted feet, two-joint legs and opposing arms. The
movement cycle follows distance travelled instead of a separate timer. Bimble's
ear region has restrained delayed flex. Moving a supporting window does not
count as a step. Both outfits use the same joint choreography.

Walking also follows diagonal travel in both screen axes, with shorter vertical
strides, continuous heading changes and a facing dead band near vertical paths.
The movement clock uses high-resolution elapsed time instead of millisecond
system ticks. Focused checks cover steady-heading foot planting, all-direction
geometry and equal elapsed-time travel at 60/144 Hz and variable frame pacing.

Moonblob is a completely limbless jelly creature again. Its movement rotates one
stable image continuously around its body circle, with one revolution per body
circumference. Wink and Nova float from stable images instead of cycling through
redrawn body shapes. Existing idle, mouth and physical reaction poses remain;
animation off still stops movement. Artwork originals are preserved and optional
rig failures retain the atlas fallback.

ChatGPT model discovery accepts both documented catalog formats, has a separate
bounded catalog size allowance, and distinguishes a successful empty list from
authentication, network, format, size, cancellation and provider failures. Failed
refreshes preserve cached model choices. When the previous choice is empty or
unavailable, OmniKit selects a lightweight text-chat model from the returned
catalog. A valid manual selection stays selected. The preference uses model
name tiers, not a claim of measured price; no unavailable catalog or model ID is
invented and no alternate billing connection is used.

Focused movement geometry, offline model-discovery and minimum-size settings
checks cover these changes. Rendered moving previews are separate from live game
acceptance. The exact cause of the reported live catalog failure remains
unconfirmed because no user credentials or live provider response were accessed.
The base remains 0.1.0; the artifact count/hash identify its source commit.

# 0.1.0.0005: companion motion, target choices and AI connections

Fixed Companion settings getting stuck on the AI tab. The regression check now
clicks AI → Appearance → Voice → AI through the actual native tab bar and also
checks programmatic selection at minimum window size.

Interact settings now expose eight world-object categories and separate hostile
and neutral enemy switches. Enemies start disabled. The user must explicitly
choose Target only or Target + start auto-attack before an enemy press can act.
Looking does nothing by itself. Target only preserves any already-active attack
state; starting auto-attack sets it on rather than toggling it. Classification
uses the game's current hostility flag, which can change with game state.
Native reach, camera visibility, input and busy-state guards remain enforced.

All five companions now use illustrated 24-pose atlases, including both Bimble
and Moonbear outfits. The new artwork covers carry, fall, land and edge-pushing
alongside walking, idle/blink and mouth previews. Bimble has changing ear poses;
Moonblob, Wink and Nova have new cute illustrated designs. Six aura palettes
and independent animation/magic switches remain available.

Optional Frame interaction under Companion → Appearance adds mouse pickup,
top-edge perching, falling, landing and contained walking with an edge-pushing
reaction. Supported surfaces include visible rectangular FFXIV addons and
compatible plugin windows/bars in Dalamud's ImGui context, including Umbra.
Snapshots are read-only; a closed or unsuitable supporting frame returns the
companion to its roaming area. Pickup, perching, containment and falling each
have a switch. Animation off freezes motion; the master frame switch starts off.

AI settings offer Continue with ChatGPT, Codex CLI, Claude CLI and API key
connections. ChatGPT uses OmniKit's own supported browser OAuth registration,
PKCE/state/nonce and signed identity verification, separate DPAPI token storage,
account selection, rotating refresh, model listing and bounded streaming.
Native CLI modes reuse each CLI's own login, disable action tools/connectors,
run in empty directories and stop on cancellation, timeout or unload. Codex
adds its normal global instructions; compatibility checks refuse untested Codex
versions. See [AI connections](AI-CONNECTIONS.md) for supported versions, setup,
privacy boundaries and provider limits. No AI reply executes game actions.

Schema v4 migrates v1/v2/v3 settings and adds safe target, frame and connection
defaults. JSON exports contain preferences only. Credentials stay outside the
database/export; changing a connection cannot grant AI transmission consent.

Focused input/category, storage/migration, frame physics, atlas bounds, offline
OAuth/CLI and native UI checks cover these changes. These are isolated checks. Enemy attack behavior,
native frame tracking and real provider sign-in/inference still require the
[in-game acceptance checks](IN-GAME-CHECKLIST.md). The base version remains
0.1.0, with the final build identified by its source commit count and hash.

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
