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
| Camera-directed interaction without clicking or changing the hard target | Interaction service implemented, including eligibility checks, camera selection, input guards and a per-press action. Actual NPCs, vendors, treasure and quest objects require live validation. |
| Independent visible-target HUD and configurable key | Styled text without a frame; nothing appears without a candidate. Detection continues through menus while actions pause. Shift-right-click opens Lock/Unlock, Move, Modify and Hide. Full Ctrl/Alt/Shift chords are recorded by pressing keys. Live behavior still needs verification. |
| Gathering gearset switching and restoring prior equipment | Gathering service implemented; saved opt-in, restoration preference and miner/botanist choices are in the settings schema. Behavior requires live verification. |
| Native Umbra plugin-menu entry | Standard main/config UI hooks are the integration contract; enabled by the main plugin wiring. |
| Native Umbra toolbar widget and popup with matching icons | Optional sidecar implemented using actual Umbra widgets, menus and Font Awesome icons. Add its DLL through Umbra's own plugin interface; it does not silently edit a toolbar. |
| Bimble, Moonbear, Moonblob, Wink and Nova companions | Roaming renderer, click-to-chat, bounded area and appearance controls implemented. Bimble/Moonbear have separate Navigator navy/gold and Stargazer violet/silver outfit atlases, each with 8 walking, 4 idle/blink and 4 mouth poses. Atlas transparency, frame bounds and composited appearance checks passed. The three vector companions have six rendered aura/shape palettes; live game appearance remains pending. |
| Explicit mouth-animation preview | Implemented through Preview animation. Synthesized speech and audio-driven lip sync are not implemented. |
| Minimal launcher and organized settings | Idle launcher is only a search bar and gear. Typing expands results; clearing collapses. Frameless settings use teal controls, 2 px rounding, a 540 × 400 default and 480 × 330 minimum, six native tabs and Appearance/Voice/AI subtabs. The footer shows the complete compiled version and by Zero; About includes a copyable full commit. Offscreen native checks are separate from pending game appearance. |
| Turn off avatar, animation, magic accents, voice and AI independently | Configuration, interface controls and local storage implemented. Disabled-animation checks pass for the vector companions. |
| Voice requests and configurable phrases bound to launcher actions | Local Windows one-phrase recognition, UI, voice bindings and storage implemented; recognition quality and action dispatch require live checks. Microphone capture is explicit and time-limited. |
| Optional AI-provider text chat | Responses API service implemented with fixed XIV/Bimble instructions, bounded requests and no executable tools. Requires explicit enablement, a configured model, and provider authentication. No live provider call verified. |
| Use the tool fully without AI | Core launcher and interaction do not need an AI connection. First-run selection is recorded locally. |
| Sign in with ChatGPT using OAuth | Supported public-client flow researched; not implemented. Requires OmniKit's own registration, browser consent, PKCE, token verification, protected storage, refresh and streaming support. |
| Codex/Claude CLI integration | Requested; not implemented. Must use an explicitly selected provider's supported authenticated interface with no arbitrary shell execution or copying another application's credentials. |
| Claude/provider OAuth choices | Requested; provider-specific supported access and terms must be verified before implementation. No generic OAuth login is claimed. |
| Keep the character and topic focused; resist instruction override | AI instructions implemented, with hard action limits in code. Perfect resistance to prompt injection is not promised. |
| Reuse other plugins' data and actions | Versioned OmniKit action-registration IPC implemented. Providers must explicitly integrate; arbitrary private plugin files are not read. Read-only AI knowledge adapters remain planned. |
| In-game browser | Optional navigation through Browsingway's registered command is implemented. A user-created visible overlay is required; the command confirms dispatch, not rendering. Otherwise wiki links use the system browser. |
| Local database for settings | SQLite schema v3 implemented, with atomic migration from v1/v2, preserved preferences and keyboard chords, and explicit validation. Conversation/audio storage is not enabled. |
| JSON settings export/import | Implemented through an explicit whitelist. Entire imports validate before changes; credentials and callbacks never appear in exports. Imported preferences cannot grant new AI transmission consent. |
| GitHub repository, packaged releases and custom Dalamud repository | Public fostot/OmniKit repository, immutable preview releases and repo.json feed. Public download integrity is verified at publication; successful loading in each user's game is separate. |

## Local data and imports

`omnikit.db` lives in the plugin's configuration directory. It stores settings,
selected avatar, shortcut choices, gathering preferences, AI connection preferences
and voice-to-action bindings. API keys remain in a separate endpoint-bound Windows
DPAPI credential store. OAuth credentials, if implemented, also need protected
storage; they must not be added to the settings DTO or database.

Exports use schema version 3 and include only explicitly listed preferences.
Versions 1 and 2 migrate atomically to version 3, preserving previous fields and
keyboard chords while adding outfit, palette and magic defaults. Older plugins
that support only schema 1 or 2 cannot open a version 3 database. Exports
contain no credential values, conversation history or audio. Custom provider URLs
and voice phrases can still reveal personal choices; exported files are local user
data, not repository source or release assets.

Imports reject unsupported schemas, unknown/missing/duplicate fields, invalid
types or ranges, unsafe endpoint URLs, colliding shortcuts and duplicate voice
phrases. The maximum JSON size is 256 KiB. An imported profile keeps AI enabled
only if it was already enabled locally and the endpoint is unchanged; otherwise
the user must enable it explicitly in settings. The import interface always disables AI. Restoring one's own local database
retains the previous local AI choice.

## References

- [Umbra custom-plugin example](https://github.com/una-xiv/Umbra.SamplePlugin)
- [ChatGPT public-client sign-in](https://developers.openai.com/siwc/token-sharing-open-source/sign-in)
- [ChatGPT plan inference limitations](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations)
