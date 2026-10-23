# OmniKit feature scope

This tracks the requested product, including unfinished work. Source implementation,
a successful build, installation, and observed in-game behavior are separate states.
Items marked implemented still require the integration and live checks described
in the release notes before they can be called verified in game.

| Requested capability | Current state |
| --- | --- |
| Hotkey launcher with natural synonyms such as guild, community and free company | Search/catalogue and launcher UI code implemented; game validation pending. |
| Open game panels and installed plugins | Catalogue reads the game's main commands and Dalamud's exposed plugin UIs; live behavior pending. |
| Find community recruitment | Official Community Finder links open in OmniKit's in-game browser. Live rendering inside FFXIV remains unverified. |
| Camera-directed interaction | Eight world-object categories can be toggled independently. Horizontal aim tolerates vertical camera pitch while a candidate center remains visible. A fresh press selects and confirms targetable objects before interacting; looking alone never changes the hard target. Separate hostile/neutral enemy switches start off; enemy actions require an explicit choice of Target only or Target + start auto-attack. Only a fresh key press acts. Enemy classification uses the game's current hostility flag and can change. Live enemy behavior requires validation. |
| Independent visible-target HUD and configurable key | Styled text without a frame; no candidate means no gameplay text. Active NPC/object interactions and cutscenes hide it. Paused actions hide the normal HUD while detection continues; only explicit HUD editing shows a paused sample. Passive player bags remain actionable unless text input or another blocking state is active. Font size, optional outline and 11 text-effect choices are in HUD, with intensity, animation and speed controls; Modify opens the controls at the top with an explicit editing sample. Shift-right-click opens Lock/Unlock, Move, Modify and Hide. Full Ctrl/Alt/Shift chords are recorded by pressing keys. Live behavior still needs verification. |
| Gathering gearset switching and restoring prior equipment | Gathering service implemented; saved opt-in, restoration preference and miner/botanist choices are in the settings schema. Behavior requires live verification. |
| Verified quest-item use | Optional first checked case: Tiller's Shovel on the cumbersome rocks in Rocks in a Hard Place, with current quest/tracker/inventory/target/usability guards. Debris stays ordinary interaction. Generic quest-item matching and third-party adapters are not implemented; native game execution remains unverified. |
| Native Umbra plugin-menu entry | Standard main/config UI hooks are the integration contract; enabled by the main plugin wiring. |
| Native Umbra toolbar widget and popup with matching icons | Optional sidecar implemented using actual Umbra widgets, menus and Font Awesome icons. Add its DLL through Umbra's own plugin interface; it does not silently edit a toolbar. |
| Bimble, Moonbear, Moonblob, Wink and Nova companions | Bimble's rebuilt idle and walk share one articulated body and limb set in both outfits, with a 180ms grounded settle when stopping. Moonbear uses articulated walking parts fitted to its separate idle artwork. Steps follow travelled distance and preserve source aspect. Moonblob is limbless and rolls a stable image continuously; Wink/Nova float from stable images. Atlas artwork remains for other idle, mouth and reaction states. Six aura palettes remain available. Moving previews and geometry checks are separate from live game appearance. |
| Pick up the companion and interact with supported frames | Optional Frame interaction under Companion → Appearance. Read-only surface snapshots cover visible rectangular native addons and compatible windows/bars in the same ImGui context. Perching, falling, contained walking and pushing have isolated state tests. Leaving or losing a supported frame falls to the screen floor and then walks home; disabling falling returns home directly. The optional grab hand accompanies manual pickup of the companion or its house. Native game surface behavior still needs live validation. |
| Companion home, comments and blinking | A local house marks the home area; dragging it moves the roaming area. Home visibility, grab hand, local text comments and Wink blinking have separate controls. Comment interval is 30–600 seconds; Wink blink interval is 1–10 seconds and also requires animation enabled. Comments do not use AI, the microphone or synthesized speech. |
| Explicit mouth-animation preview | Implemented through Preview animation. Synthesized speech and audio-driven lip sync are not implemented. |
| Minimal launcher and organized settings | Idle launcher is only a search bar and gear. Typing expands results; clearing collapses. Frameless settings use teal controls, 2 px rounding, a 540 × 400 default and 480 × 330 minimum, six native tabs and Appearance/Voice/AI subtabs. Compact status lines distinguish normal, warning and error states; long details remain available on hover, and optional explanations expand in place. The footer shows the complete compiled version and by Zero; About includes a copyable full commit. Offscreen native checks are separate from pending game appearance. |
| Turn off avatar, animation, magic accents, frame interaction, voice and AI independently | Configuration, interface controls and local storage implemented. Frame pickup, perching, containment and falling have separate switches. |
| Voice requests and configurable phrases bound to launcher actions | Local Windows one-phrase recognition, UI, voice bindings and storage implemented; recognition quality and action dispatch require live checks. Microphone capture is explicit and time-limited. |
| Optional AI-provider text chat | Explicit API, ChatGPT, Codex CLI and Claude CLI choices. Each accepted Ask permits at most one inference request; concurrent submissions are rejected, with no queue, automatic retry or provider/model fallback. Fixed XIV/selected-companion instructions, bounded replies and no executable game actions. CLI generation is paused; API/ChatGPT chat remains available. Provider connection tests are distinct from real account sign-in and inference. See AI-CONNECTIONS.md. |
| Use the tool fully without AI | Core launcher and interaction do not need an AI connection. First-run selection is recorded locally. |
| Sign in with ChatGPT using OAuth | OmniKit's own public-client registration, browser consent, PKCE/state/nonce, signed-token verification, separate DPAPI account storage, serialized rotating refresh and bounded streaming. Real sign-in and plan inference require user acceptance. |
| Codex/Claude CLI integration | Generation is paused because the CLI cannot establish OmniKit's strict one-Ask/one-inference limit. CLI availability/probe and supported sign-in flows remain available through the CLI's own login. No token copying or generic web-session bridge. |
| Claude sign-in | Delegated to Claude CLI's own supported browser login. There is no separate generic Claude web-session or cookie bridge. |
| Keep the character and topic focused; resist instruction override | AI instructions implemented, with hard action limits in code. Perfect resistance to prompt injection is not promised. |
| Reuse other plugins' data and actions | Versioned OmniKit action-registration IPC implemented. Providers must explicitly integrate; arbitrary private plugin files are not read. Read-only AI knowledge adapters remain planned. |
| In-game browser | Wiki and web actions use OmniKit's own out-of-process WebView2 child window with a dedicated profile. A single icon reveals navigation, address entry, move/resize and extension controls. Explicitly chosen unpacked extensions can be added, listed, enabled, disabled or removed; Web Store installation and universal compatibility are not promised. No Browsingway dependency or external-browser fallback. Synthetic host/extension checks passed; actual FFXIV focus, DPI and fullscreen behavior remain unverified. Windowed/borderless mode is the intended integration path. |
| Optional capture of other game-process web links | Off by default, including migrations. When enabled, captures standard HTTP(S) ShellExecute calls from the same game process for OmniKit's browser. Authentication links, non-web schemes, custom process launches and calls from other processes are excluded. This does not intercept every possible way a plugin can launch a browser. Live behavior requires validation. |
| Local database for settings | SQLite schema v6, with atomic migration from v1–v5, preserved preferences and keyboard chords, and explicit validation. All migrated wiki/web navigation uses the in-game browser. Conversation/audio storage is not enabled. |
| JSON settings export/import | Implemented through an explicit whitelist. Entire imports validate before changes; credentials and callbacks never appear in exports. Imported preferences cannot grant new AI transmission consent. |
| GitHub repository, packaged releases and custom Dalamud repository | Public fostot/OmniKit repository, immutable preview releases and repo.json feed. Public download integrity is verified at publication; successful loading in each user's game is separate. |

## Local data and imports

`omnikit.db` lives in the plugin's configuration directory. It stores settings,
selected avatar, shortcut choices, gathering preferences, AI connection preferences
and voice-to-action bindings. API keys remain in a separate endpoint-bound Windows
DPAPI credential store. ChatGPT registrations and tokens use a different protected
store, outside the settings DTO, database and exports. CLI credentials remain
under the selected CLI's control.

Exports use schema version 6 and include only explicitly listed preferences.
Versions 1–5 migrate atomically to version 6, preserving previous fields and
keyboard chords while adding HUD-effect and companion home/personality defaults.
Quest-item use and capture of other game-process web links both default off.
Existing effect IDs and appearance choices are preserved. In-game web navigation
is enabled for every migrated profile. Older plugins cannot open a version 6 database. Exports
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
companion. These are text styling controls, not changes to game effects.

## References

- [Umbra custom-plugin example](https://github.com/una-xiv/Umbra.SamplePlugin)
- [ChatGPT public-client sign-in](https://developers.openai.com/siwc/token-sharing-open-source/sign-in)
- [ChatGPT plan inference limitations](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations)
