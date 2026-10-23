# OmniKit feature scope

This tracks the requested product, including unfinished work. Source implementation,
a successful build, installation, and observed in-game behavior are separate states.
Items marked implemented still require the integration and live checks described
in the release notes before they can be called verified in game.

| Requested capability | Current state |
| --- | --- |
| Hotkey launcher with natural synonyms such as guild, community and free company | Search/catalogue and launcher UI implemented. Width, text size, visible result count, horizontal position and top position are adjustable; reset restores the compact defaults. Additional results remain reachable by scrolling/arrow keys. Game validation pending. |
| Open game panels and installed plugins | Catalogue reads the game's main commands and Dalamud's exposed plugin UIs; live behavior pending. |
| Find community recruitment | Official Community Finder links open in OmniKit's in-game browser. Live rendering inside FFXIV remains unverified. |
| Camera-directed interaction | Eight world-object categories can be toggled independently. Horizontal aim tolerates vertical camera pitch while a candidate center remains visible. A fresh press selects and confirms targetable objects before interacting; looking alone never changes the hard target. Separate hostile/neutral enemy switches start off; enemy actions require an explicit choice of Target only or Target + start auto-attack. Only a fresh key press acts. Enemy classification uses the game's current hostility flag and can change. Live enemy behavior requires validation. |
| Independent visible-target HUD and configurable key | Styled text without a frame; no candidate means no gameplay text. Active NPC/object interactions and cutscenes hide it. Paused actions hide the normal HUD while detection continues; only explicit HUD editing shows a paused sample. Passive player bags remain actionable unless text input or another blocking state is active. Font size, optional outline and 11 text-effect choices are in HUD, with intensity, animation and speed controls. Outline thickness accepts 0.5–4, default 1, and follows the primary effect glyphs. Modify opens these controls with an explicit editing sample. Shift-right-click opens Lock/Unlock, Move, Modify and Hide. Full Ctrl/Alt/Shift chords are recorded by pressing keys. Live behavior still needs verification. |
| Gathering gearset switching and restoring prior equipment | Gathering service implemented; saved opt-in, restoration preference and miner/botanist choices are in the settings schema. Behavior requires live verification. |
| Verified quest-item use | Off by default. Two mapped cases: Tiller's Shovel on cumbersome rocks in Rocks in a Hard Place, and Fetid Dodo Flesh on suspicious mounds in Forbidden Island. Quest/tracker/inventory/confirmed-target/native-usability guards precede one item request per press. Debris stays ordinary interaction. Generic quest solving and third-party adapters are not implemented; native game execution remains unverified. |
| Verified quest-emote use | Off by default. The first mapped case uses `/soothe` on the irate coachmen in A Bridge Too Full. A fresh press confirms the target before one emote request; a blocked matched rule never falls back to talking. Native game execution remains unverified. See [scope and sources](QUEST-EMOTES.md). |
| Next Action icon | The exact resolver used by Interact supplies the preview and blocked reason. Locked mode shows one icon; unlocked mode adds the NEXT ACTION heading and drag area. Its separate lock control does not execute an action. Clicking submits one guarded request; observation alone never acts. Live behavior pending. |
| Manual quest item bar | The Quest Items tab controls a paged bar with up to eight visible items. It reads usable non-ground KeyItems and ordinary bag items explicitly tracked for an accepted quest's current sequence. It excludes arbitrary potion inventory. A manual click rechecks the existing target, exact stack and native usability, with no auto-target, queue, retry or claim that the item belongs to that target. Snapshot data is not persisted, logged or sent to AI. See [item coverage](QUEST-ITEMS.md). |
| Native Umbra plugin-menu entry | Standard main/config UI hooks are the integration contract; enabled by the main plugin wiring. |
| Native Umbra toolbar widget and popup with matching icons | Optional sidecar implemented using actual Umbra widgets, menus and Font Awesome icons. Add its DLL through Umbra's own plugin interface; it does not silently edit a toolbar. |
| Eight whole-body companions | Moonblob is the default, joined by Wink, Nova, Mochi, Puff, Orbit, Ember and Prism. Each uses one complete limbless body; Moonblob rolls and the others float from stable images. Atlas artwork supplies blink, mouth and reaction states. Six aura palettes remain available. Wink is the plugin icon. Artwork and geometry checks are separate from live game appearance. |
| Seasonal accessories and optional companion rotation | Nine shared accessory themes cover All Saints' Wake, Starlight, Heavensturn, Valentione, Little Ladies' Day, Hatching-tide, Make It Rain, Moonfire Faire and The Rising. Auto reads supported native festivals; unknown/conflicting states do not guess by month. Only explicit official date windows may substitute when native data is unavailable. Manual selection remains available. Optional local rotation chooses another companion every 30–3600 seconds, retaining the saved manual choice. These are accessories, not a new full atlas for every pet/event. |
| Pick up the companion and interact with supported frames | Optional Frame interaction under Companion → Appearance. Read-only surface snapshots cover visible rectangular native addons and compatible windows/bars in the same ImGui context, including compatible NoMouseInputs windows. Full-screen canvases are not universally supported. Walking off a supported thin HUD strip falls toward a lower frame or the screen floor before returning to the roaming area. The optional pickup hand centers the companion/house beneath its grip and has scale 0.2–1, default 0.4. Native game surfaces still need live validation. |
| Independent companion house and naps | Dragging the house changes only its own screen position. Normal free roaming stays in the drawn area and routes around the house; nap trips can leave it at about twice normal speed, enter through the front doorway, settle, sleep, wake and return continuously. All eight companions have inspected entrance/sleep artwork. Defaults are about 45 seconds awake and 20 asleep, adjustable to 15–300 and 5–120 seconds. Naps and chimney sleep symbols have separate switches. Pause freezes a trip; disabling naps, hiding or moving the house returns without teleporting. Source-linked state checks and offscreen rendering are separate from live appearance. |
| Companion comments and blinking | Home visibility, grab hand, local text comments and Wink blinking have separate controls. Comment interval is 30–600 seconds; Wink blink interval is 1–10 seconds and also requires animation enabled. An optional Fourier-smoothed blink affects Wink only. Comments do not use AI, the microphone or synthesized speech. |
| Explicit mouth-animation preview | Implemented through Preview animation. Synthesized speech and audio-driven lip sync are not implemented. |
| Minimal launcher and organized settings | Idle launcher is only a search bar and gear. Typing expands results; clearing collapses. Frameless settings use teal controls, 2 px rounding, a 540 × 400 default and 480 × 330 minimum, seven native tabs including Quest Items, and Appearance/Voice/AI subtabs. Compact status lines distinguish normal, warning and error states; long details remain available on hover, and optional explanations expand in place. The footer shows the complete compiled version and by Zero; About includes a copyable full commit. Offscreen native checks are separate from pending game appearance. |
| Turn off avatar, animation, magic accents, frame interaction, voice and AI independently | Configuration, interface controls and local storage implemented. Frame pickup, perching, containment and falling have separate switches. |
| Voice requests and configurable phrases bound to launcher actions | Local Windows one-phrase recognition, UI, voice bindings and storage implemented; recognition quality and action dispatch require live checks. Microphone capture is explicit and time-limited. |
| Optional AI-provider text chat | Explicit API, ChatGPT, Codex CLI and Claude CLI choices. Each accepted Ask permits at most one inference request; concurrent submissions are rejected, with no queue, automatic retry or provider/model fallback. Fixed XIV/selected-companion instructions, bounded replies and no executable game actions. CLI generation is paused; API/ChatGPT chat remains available. Provider connection tests are distinct from real account sign-in and inference. See AI-CONNECTIONS.md. |
| Use the tool fully without AI | Core launcher and interaction do not need an AI connection. First-run selection is recorded locally. |
| Sign in with ChatGPT using OAuth | OmniKit's own public-client registration, browser consent, PKCE/state/nonce, signed-token verification, separate DPAPI account storage, serialized rotating refresh and bounded streaming. Real sign-in and plan inference require user acceptance. |
| Codex/Claude CLI integration | Generation is paused because the CLI cannot establish OmniKit's strict one-Ask/one-inference limit. CLI availability/probe and supported sign-in flows remain available through the CLI's own login. No token copying or generic web-session bridge. |
| Claude sign-in | Delegated to Claude CLI's own supported browser login. There is no separate generic Claude web-session or cookie bridge. |
| Keep the character and topic focused; resist instruction override | AI instructions implemented, with hard action limits in code. Perfect resistance to prompt injection is not promised. |
| Reuse other plugins' data and actions | Versioned OmniKit action-registration IPC implemented. Providers must explicitly integrate; arbitrary private plugin files are not read. Read-only AI knowledge adapters remain planned. |
| In-game browser | Wiki and web actions use an out-of-process WebView2 child window with a dedicated profile. A menu icon reveals controls; the close X stays visible. Close/hide stops the helper and engine, with no prewarm. Per-session protection defaults on and blocks script popups and a limited original list of 60 known ad/tracking hosts, not all advertising. Controls offer a session protection switch and unpacked-extension management. Native-UI hover cutouts mitigate overlap; actual FFXIV flicker, focus, DPI and fullscreen behavior remain unverified. Windowed/borderless mode is intended. No Browsingway dependency or silent external-browser fallback. |
| Optional capture of other game-process web links | Off by default, including migrations. Standard HTTP(S) ShellExecute calls from the same game process can start the browser on demand; merely enabling capture does not prewarm it. Authentication links, non-web schemes, custom process launches and other processes are excluded. This does not intercept every way a plugin can launch a browser. Live behavior requires validation. |
| Local database for settings | SQLite schema v8 adds 19 fields and atomically migrates v1–v7, preserving preferences, keyboard chords and local AI consent with explicit validation. Legacy house coordinates derive from the previous roaming box. All migrated wiki/web navigation uses the in-game browser. Conversation/audio storage is not enabled. |
| JSON settings export/import | Implemented through an explicit whitelist. Entire imports validate before changes; credentials and callbacks never appear in exports. Imported preferences cannot grant new AI transmission consent. |
| GitHub repository, packaged releases and custom Dalamud repository | Public fostot/OmniKit repository, immutable preview releases and repo.json feed. Public download integrity is verified at publication; successful loading in each user's game is separate. |

## Local data and imports

The earlier rabbit/bear art and rigs were retired locally and excluded from current
source files and packages. Previous published releases and history remain unchanged.
Source 13 adds the action surfaces, seasonal accessories, optional avatar timer
and Wink blink smoothing described above. Larger per-avatar looks, selectable
everyday/holiday homes, alternative hands, grounded hopping and rings remain
pending; they are not included in `0.1.0.0013`.

`omnikit.db` lives in the plugin's configuration directory. It stores settings,
selected avatar, shortcut choices, gathering preferences, AI connection preferences
and voice-to-action bindings. API keys remain in a separate endpoint-bound Windows
DPAPI credential store. ChatGPT registrations and tokens use a different protected
store, outside the settings DTO, database and exports. CLI credentials remain
under the selected CLI's control.

Exports use schema version 8 and include only explicitly listed preferences.
Versions 1–7 migrate atomically to version 8, preserving previous fields, keyboard
chords and local consent. The 19 new fields cover nine action-bar preferences,
five launcher layout controls, seasonal enable/theme, optional avatar rotation
enable/interval and optional Fourier blink. Older house coordinates derive from
the old roaming box's midpoint/bottom; its later movements do not change the box.
Quest-item use and capture of other game-process web links both default off.
Existing effect IDs and appearance choices are preserved. In-game web navigation
is enabled for every migrated profile. Older plugins cannot open a version 8 database. Exports
contain no credential values, conversation history or audio. Custom provider URLs
and voice phrases can still reveal personal choices; exported files are local user
data, not repository source or release assets.

Imports reject unsupported schemas, unknown/missing/duplicate fields, invalid
types or ranges, unsafe endpoint URLs, colliding shortcuts and duplicate voice
phrases. The maximum JSON size is 256 KiB. An imported profile keeps AI enabled
only if it was already enabled locally and the endpoint and connection are unchanged; otherwise
the user must enable it explicitly in settings. The import interface always disables AI. Restoring one's own local database
retains the previous local AI choice.

HUD effects are None, Shadow, Soft glow, Gentle shimmer, Rainbow, Neon pulse,
Chromatic glitch, Hologram, Flame, Frost and Light sweep. Intensity accepts 0–2,
speed accepts 0.1–3, and effect animation can be disabled independently of the
companion. Outline thickness is 0.5–4, default 1, and follows the primary effect
glyphs. These are text styling controls, not changes to game effects.

## References

- [Umbra custom-plugin example](https://github.com/una-xiv/Umbra.SamplePlugin)
- [ChatGPT public-client sign-in](https://developers.openai.com/siwc/token-sharing-open-source/sign-in)
- [ChatGPT plan inference limitations](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations)
