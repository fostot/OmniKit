# OmniKit feature scope

This tracks the requested product, including unfinished work. Source implementation,
a successful build, installation, and observed in-game behavior are separate states.
Items marked implemented still require the integration and live checks described
in the release notes before they can be called verified in game.

| Requested capability | Current state |
| --- | --- |
| Hotkey launcher with natural synonyms such as guild, community and free company | Search/catalogue and launcher UI code implemented; game validation pending. |
| Open game panels and installed plugins | Catalogue reads the game's main commands and Dalamud's exposed plugin UIs; live behavior pending. |
| Find community recruitment | Official Community Finder links are present; currently open through the system browser. |
| Camera-directed interaction | Eight world-object categories can be toggled independently. World-object interaction preserves the hard target. Separate hostile/neutral enemy switches start off; enemy actions require an explicit choice of Target only or Target + start auto-attack. Only a fresh key press acts. Enemy classification uses the game's current hostility flag and can change. Live enemy behavior requires validation. |
| Independent visible-target HUD and configurable key | Styled text without a frame; no candidate means no gameplay text. Active NPC/object interactions and cutscenes hide it. Unrelated menus pause actions while detection continues. Font size, optional outline and four text effects are in HUD; Modify opens the controls at the top with an explicit editing sample. Shift-right-click opens Lock/Unlock, Move, Modify and Hide. Full Ctrl/Alt/Shift chords are recorded by pressing keys. Live behavior still needs verification. |
| Gathering gearset switching and restoring prior equipment | Gathering service implemented; saved opt-in, restoration preference and miner/botanist choices are in the settings schema. Behavior requires live verification. |
| Native Umbra plugin-menu entry | Standard main/config UI hooks are the integration contract; enabled by the main plugin wiring. |
| Native Umbra toolbar widget and popup with matching icons | Optional sidecar implemented using actual Umbra widgets, menus and Font Awesome icons. Add its DLL through Umbra's own plugin interface; it does not silently edit a toolbar. |
| Bimble, Moonbear, Moonblob, Wink and Nova companions | Bimble/Moonbear use articulated 2D walking parts in both outfits, with distance-driven alternating stance/swing and fixed body proportions. Moonblob is limbless and rolls a stable image continuously; Wink/Nova float from stable images. Atlas artwork remains for idle/blink, mouth and reactions. Six aura palettes remain available. Moving previews and geometry checks are separate from live game appearance. |
| Pick up the companion and interact with supported frames | Optional Frame interaction under Companion → Appearance. Read-only surface snapshots cover visible rectangular native addons and compatible windows/bars in the same ImGui context. Perching, falling, contained walking and pushing have isolated state tests. Hidden/unsupported surfaces return to the saved roaming area. Native game surface behavior still needs live validation. |
| Explicit mouth-animation preview | Implemented through Preview animation. Synthesized speech and audio-driven lip sync are not implemented. |
| Minimal launcher and organized settings | Idle launcher is only a search bar and gear. Typing expands results; clearing collapses. Frameless settings use teal controls, 2 px rounding, a 540 × 400 default and 480 × 330 minimum, six native tabs and Appearance/Voice/AI subtabs. The footer shows the complete compiled version and by Zero; About includes a copyable full commit. Offscreen native checks are separate from pending game appearance. |
| Turn off avatar, animation, magic accents, frame interaction, voice and AI independently | Configuration, interface controls and local storage implemented. Frame pickup, perching, containment and falling have separate switches. |
| Voice requests and configurable phrases bound to launcher actions | Local Windows one-phrase recognition, UI, voice bindings and storage implemented; recognition quality and action dispatch require live checks. Microphone capture is explicit and time-limited. |
| Optional AI-provider text chat | Explicit API, ChatGPT, Codex CLI and Claude CLI choices. Fixed XIV/selected-companion instructions, bounded replies and no executable game actions. Provider connection tests are distinct from real account sign-in and inference. See AI-CONNECTIONS.md. |
| Use the tool fully without AI | Core launcher and interaction do not need an AI connection. First-run selection is recorded locally. |
| Sign in with ChatGPT using OAuth | OmniKit's own public-client registration, browser consent, PKCE/state/nonce, signed-token verification, separate DPAPI account storage, serialized rotating refresh and bounded streaming. Real sign-in and plan inference require user acceptance. |
| Codex/Claude CLI integration | Native CLI processes use the CLI's existing login. Fixed argument lists, stdin questions, empty working directories, tool/connector restrictions, bounded output and cancellation. No shell command interpolation or token copying. Codex versions require an isolation compatibility check. |
| Claude sign-in | Delegated to Claude CLI's own supported browser login. There is no separate generic Claude web-session or cookie bridge. |
| Keep the character and topic focused; resist instruction override | AI instructions implemented, with hard action limits in code. Perfect resistance to prompt injection is not promised. |
| Reuse other plugins' data and actions | Versioned OmniKit action-registration IPC implemented. Providers must explicitly integrate; arbitrary private plugin files are not read. Read-only AI knowledge adapters remain planned. |
| In-game browser | Optional navigation through Browsingway's registered command is implemented. A user-created visible overlay is required; the command confirms dispatch, not rendering. Otherwise wiki links use the system browser. |
| Local database for settings | SQLite schema v5, with atomic migration from v1–v4, preserved preferences and keyboard chords, and explicit validation. Conversation/audio storage is not enabled. |
| JSON settings export/import | Implemented through an explicit whitelist. Entire imports validate before changes; credentials and callbacks never appear in exports. Imported preferences cannot grant new AI transmission consent. |
| GitHub repository, packaged releases and custom Dalamud repository | Public fostot/OmniKit repository, immutable preview releases and repo.json feed. Public download integrity is verified at publication; successful loading in each user's game is separate. |

## Local data and imports

`omnikit.db` lives in the plugin's configuration directory. It stores settings,
selected avatar, shortcut choices, gathering preferences, AI connection preferences
and voice-to-action bindings. API keys remain in a separate endpoint-bound Windows
DPAPI credential store. ChatGPT registrations and tokens use a different protected
store, outside the settings DTO, database and exports. CLI credentials remain
under the selected CLI's control.

Exports use schema version 5 and include only explicitly listed preferences.
Versions 1–4 migrate atomically to version 5, preserving previous fields and
keyboard chords while adding safe defaults and the previous shadow appearance.
Older plugins cannot open a version 5 database. Exports
contain no credential values, conversation history or audio. Custom provider URLs
and voice phrases can still reveal personal choices; exported files are local user
data, not repository source or release assets.

Imports reject unsupported schemas, unknown/missing/duplicate fields, invalid
types or ranges, unsafe endpoint URLs, colliding shortcuts and duplicate voice
phrases. The maximum JSON size is 256 KiB. An imported profile keeps AI enabled
only if it was already enabled locally and the endpoint and connection are unchanged; otherwise
the user must enable it explicitly in settings. The import interface always disables AI. Restoring one's own local database
retains the previous local AI choice.

## References

- [Umbra custom-plugin example](https://github.com/una-xiv/Umbra.SamplePlugin)
- [ChatGPT public-client sign-in](https://developers.openai.com/siwc/token-sharing-open-source/sign-in)
- [ChatGPT plan inference limitations](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations)
