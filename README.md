# OmniKit

Your everyday toolkit for XIV by Zero, with Bimble the moon bunny (or a moon bear).

**Early preview · Dalamud API 15 · Windows x64.**
Build labels use `0.1.0.NNNN (shortSHA)`, where `NNNN` is the source commit's
reachable Git commit count, padded to at least four digits. See
[versioning](docs/VERSIONING.md) for the exact format and release workflow.

Build and isolated tests are separate from game validation. See [feature status](docs/FEATURES.md)
for the full scope and [game checklist](docs/IN-GAME-CHECKLIST.md) for acceptance.

## Install

1. Open Dalamud settings with `/xlsettings`, then **Experimental**.
2. Add this custom plugin repository and save:

   ```text
   https://raw.githubusercontent.com/fostot/OmniKit/main/repo.json
   ```

3. Open `/xlplugins`, search **OmniKit**, and install it.
4. Press **Alt + Space**, or type `/omnikit`. The launcher opens as a search field
   and settings gear. Type to see matching results; clear the query to collapse it.

Open settings with the gear, **Ctrl + F10** or `/omnikit settings`. From settings,
open the separate companion window for welcome guidance, voice controls and
optional AI chat. Both shortcuts are changeable. A desktop launcher can also own
Alt + Space; change one shortcut if they overlap. Soft interaction, voice and AI
start off until you enable them.

Settings have six tabs: **Launcher**, **Interact**, **HUD**, **Companion**, **Data**
and **About**. About shows the complete version, author and copyable full source
commit. Companion groups **Appearance**, **Voice** and **AI**. Click a shortcut
button to record it: hold Ctrl, Alt and/or Shift, press the main key, then release
all keys. Escape cancels recording. The compact settings panel has teal controls,
small corner rounding and a footer showing the built version and **by Zero**.

## What this preview includes

- Search game windows, loaded plugins, community recruitment and saved gearsets.
  Try `guild`, `community`, `free company`, `inventory` or `job paladin`.
- Optional camera-directed interaction: look toward an eligible nearby object
  and press the configured chord. The optional text HUD names the candidate,
  hiding during active NPC/object interactions, cutscenes and other paused states.
  An explicit Modify preview is the exception, with dialogue/cutscene suppression
  still taking precedence. Passive bags allow interaction; text input pauses it.
  Shift + right-click it for
  Move, Lock/Unlock, Hide or Settings. No valid
  target means the key is left alone. One press requests one native interaction.
  **Interact → Detection distance** adjusts detection from 1–30 yalms. The default
  for new settings is 6; existing saved distances stay unchanged. This does not
  extend the game's allowed interaction reach.
- **HUD** puts font size first, with an independent outline toggle and eleven
  text effects, including rainbow, neon pulse, chromatic glitch, hologram,
  flame, frost and light sweep. Intensity, speed and animation are adjustable. Modify opens this tab at the
  top. An editing sample lets you see changes even without a target or with the
  gameplay HUD disabled; active interactions still hide that sample.
- **Interact → Target types** controls NPCs, quest objects, loot, aetherytes,
  gathering nodes, retainers, housing and other usable objects. Hostile and
  neutral enemies have separate switches, both off initially. Before enabling
  enemy actions, choose **Target only** or **Target + start auto-attack**.
  Looking never starts an action. Target only does not start auto-attack; if it
  was already active, the game's existing attack state remains active.
- Optional gathering gear changes and restoration after an observed gathering
  session, with cancellation when you change gear yourself. Save your gearsets in
  FFXIV first. This does not reveal hidden nodes or choose gathering items.
- **Interact → Use verified quest items** adds the first checked item rule:
  Tiller's Shovel on the cumbersome rocks in **Rocks in a Hard Place**. It starts
  off, selects/confirms the target, and attempts one native item action only when
  quest, tracker, inventory and usability checks agree. Debris uses ordinary
  interaction. Other quest items still require manual use. See [scope and sources](docs/QUEST-ITEMS.md).
- Local Windows speech input, typed commands, saved phrase-to-action bindings and
  a practice button. Speech starts only when requested, for one utterance.
- Optional text chat through **Continue with ChatGPT** or a Responses-compatible
  API endpoint. Codex/Claude CLI connection checks and sign-in remain available,
  but CLI generation is paused because a single inference cannot be enforced. Connections
  are selected explicitly. Replies cannot execute game commands. The local
  matcher does not need AI. See [AI connections](docs/AI-CONNECTIONS.md).
- Bimble and Moonbear with **Navigator** (navy/gold) and **Stargazer**
  (violet/silver) outfit atlases, plus illustrated **Moonblob**, **Wink**
  and **Nova**. Six aura palettes are available: Moonlight, Aurora,
  Nebula, Rose, Ember and Starlight. In **Companion → Appearance**, draw a
  roaming area, preview an animation, or turn off magic accents, animation or
  the companion. Clicking the companion opens its conversation. Bimble and
  Moonbear walk with articulated painted parts: alternating planted feet,
  swinging legs and opposing arms keep the body consistent. Bimble has bounded
  ear follow-through. Moonblob is completely limbless and rolls continuously
  around its body circle; Wink and Nova float without changing shape each frame.
  Movement cadence follows distance travelled, including on moving frames.
  Diagonal travel has directional steps and stable facing near vertical paths;
  movement uses a high-resolution timer for high-refresh displays.
  Bimble's rebuilt idle and walk share one body and limb renderer, with a short
  settle transition that grounds both paws. Reaction and mouth previews retain
  atlas poses; Moonbear also retains its separate idle artwork. Mouth movement
  is an explicit preview; synthesized speech and lip sync are not implemented.
  Moonbear retains approximately the same idle/walk height, with a remaining
  silhouette difference between its separate artwork.
- Optional **Companion → Appearance → Frame interaction** lets you pick up the
  companion and drop it onto a supported visible frame. It can perch on a top
  edge, turn and walk off, fall to the screen floor, then walk home; or stay
  inside a frame and push against its edges.
  Supported surfaces include rectangular FFXIV addons and compatible plugin
  windows and Umbra bars in Dalamud's ImGui context. Closed or unsupported
  frames make it fall and return home. Nothing edits those windows. An optional
  draggable moon house moves its saved area and faces inward. Optional hand cues
  show pickup/release. Wink blinks while floating. Occasional comments are local
  text only, with no model request or microphone use.
- SQLite preferences and JSON export/import, including saved chords, HUD position
  and the companion area. Older settings migrate to the new format.
- Native Umbra menu integration, an optional Umbra toolbar widget, and an owned
  in-game Chromium browser for OmniKit website and wiki actions.

## Umbra

OmniKit automatically appears in Umbra's **Plugins** menu through its standard
Dalamud main/configuration windows.

For a dedicated toolbar button, download the release asset named
`OmniKit.Umbra-<version>-<shortSHA>.dll`, keep it in a permanent local folder,
and load that file through **Umbra Settings →
Plugins**. Then add the **OmniKit** toolbar widget. It uses Umbra's own styling and
icons. Earlier dev.1/dev.2 releases used the filename `OmniKit.Umbra.dll`.
See the [companion instructions](src/OmniKit.Umbra/README.md).

## Wiki inside the game

Wiki searches, Community Finder links and OmniKit website actions open in its
own in-game WebView2 window. The page stays visible; the menu icon reveals the
address bar, navigation, movement and extension controls. `/omnikit browser`
opens it, `/omnikit browser controls` toggles controls, and `/omnikit browser hide`
hides it. `/omnikit wiki <search>` searches the community-run wiki. Browser
startup failures are reported without silently opening your desktop browser.

The helper uses the installed Microsoft Edge WebView2 Evergreen Runtime and its
own local profile, never another browser's profile. It supports unpacked Chromium
extensions through WebView2; Chrome Web Store installation and universal extension
compatibility are not promised. Ordinary page downloads and external application
schemes are blocked. Supported account sign-in still uses the provider's external
consent flow. See the [browser implementation](src/OmniKit.Browser/README.md).

**Data → In-game browser → Capture game & plugin web links** optionally routes
standard HTTP/HTTPS ShellExecute opens from the game process into OmniKit.
It starts off and opens the browser when enabled. Capture requires a ready
browser with queue space. Recognized authentication and non-web links pass through. Custom launchers,
direct process starts and opens from another process are outside this hook's
coverage. Capture failure leaves the original open method in charge; OmniKit's
own web commands still report browser errors without a desktop fallback.

## Local brain and optional AI

The local brain matches names, synonyms, small spelling mistakes and saved
phrases to a known action catalogue. A unique spoken `job <name>` can equip its
saved set; ambiguous results stay in the menu for you to choose. This is useful
without a language model and makes no provider requests with AI disabled.

AI adds conversational answers and explanations. ChatGPT sign-in uses the
supported browser consent flow and keeps its tokens in separate Windows
encrypted storage. CLI sign-in and connection checks use each CLI's own login
without copying its credentials; CLI generation is currently paused. Each explicit
Ask permits at most one inference request. There is no automatic retry, fallback,
continuation or queued duplicate. Companion comments are local, never model calls.
This limits request count, not the provider's exact token usage or charge.
API mode supports an API key or a compatible local Responses server;
provider API use may incur charges. Character instructions guide
the assistant, but are not a guarantee against every off-topic answer or prompt
injection; the execution boundary is the fixed local action catalogue.

Preferences live in `omnikit.db` under Dalamud's OmniKit configuration directory.
API keys use separate Windows DPAPI storage, bound to their provider endpoint.
ChatGPT registrations and tokens also stay outside the database and exports.
Exports never include credentials, recordings or conversations. Exports can contain
your custom provider address and voice phrases, so review them before sharing.
Import always disables AI in the interface until you explicitly enable it again.
Schema v6 adds effect controls and companion home, hand, comment and blink
preferences, plus default-off link capture and verified quest-item use. It migrates v1–v5 settings while preserving target types, the explicit enemy action choice,
frame interaction, AI connection preferences and saved chords. Legacy
v1 Ctrl/Alt/Shift virtual-key values are converted to chord masks. Failed
database migrations keep the previous snapshot.

## Build and test

Install .NET SDK 10 and Dalamud API 15 references in the normal XIVLauncher dev
directory. Umbra's companion requires Umbra 3.1.18.0 references. Override the
`DalamudLibPath` / `UmbraLibPath` build properties for a different local location.
Build from a full Git checkout with Git available. Shallow history and source
archives without Git metadata are unsupported; there is no manual version
override. For release artifacts, commit the reviewed source before building so
the embedded count and hash identify the files actually packaged.
`Directory.Build.props` owns the shared base version and author;
`Directory.Build.targets` derives versions and source metadata for both plugin
assemblies. Keep their project files free of duplicate hardcoded version fields.

```powershell
dotnet build src/OmniKit/OmniKit.csproj -c Release
dotnet build src/OmniKit.Umbra/OmniKit.Umbra.csproj -c Release
dotnet run --project tests/SearchTests/SearchTests.csproj -c Release
dotnet run --project tests/GatheringStateTests.csproj -c Release
dotnet run --project tests/StorageTests/StorageTests.csproj -c Release
dotnet run --project tests/HotkeyTests/HotkeyTests.csproj -c Release
dotnet run --project tests/InteractionInputTests/InteractionInputTests.csproj -c Release
dotnet run --project tests/CompanionPhysicsTests/CompanionPhysicsTests.csproj -c Release
dotnet run --project tests/CompanionMotionTests/CompanionMotionTests.csproj -c Release
dotnet run --project tests/CliChatTests/CliChatTests.csproj -c Release
dotnet run --project tests/ChatGptTests/ChatGptTests.csproj -c Release
dotnet run --project tests/ChatCostGuardTests/ChatCostGuardTests.csproj -c Release
dotnet run --project tests/WebRoutingTests/WebRoutingTests.csproj -c Release
pwsh -NoProfile -File tests/VersioningTests.ps1
pwsh scripts/Package.ps1
```

Release packages contain only reviewed runtime files and assets. Local databases,
credentials, build intermediates and user exports are not source or backup content.
The base remains `0.1.0`. Release labels use a padded commit count, while .NET
and Dalamud use its numeric form: illustrative label `0.1.0.0003 (abc1234)` maps
to numeric version `0.1.0.3`. Rebuilding the same source commit does not increment
the version. A later repository-feed commit does not change the earlier
artifact's identity: its release tag must still point to the source commit.
The prior `0.1.0-dev.1` and `0.1.0-dev.2` releases remain unchanged. See the
[versioning workflow](docs/VERSIONING.md) before packaging or publishing.
New release assets are `OmniKit-<version>-<shortSHA>.zip` and
`OmniKit.Umbra-<version>-<shortSHA>.dll`. The core ZIP still contains the normal
`OmniKit.dll` and its build metadata. The versioning integration test uses only
synthetic repositories under ignored `.local` and retains them for inspection.
See [IPC integration](docs/INTEGRATION.md) for exposing additional plugin actions.

## References and artwork

The soft-interaction concept was informed by the [awesome_wotlk fork](https://github.com/srazdokunebil/awesome_wotlk).
OmniKit's implementation is original and uses XIV's current object, camera and
interaction APIs; it includes no WoW client patch code, addresses or loaders.
The [ChokoLoader reference](https://github.com/POKOch/ChokoLoader-Awesome-for-Warmane)
was inspected as background only. [Artwork prompts](assets/PROMPTS.md) document
the generated moon companions; the [house](assets/HOUSE-PROMPT.md) and
[grab hand](assets/GRAB-HAND-PROMPT.md) and
[rebuilt Bimble](assets/BIMBLE-V2-PROMPTS.md) have their own prompt records. Dependency notices are in
[THIRD-PARTY-NOTICES](docs/THIRD-PARTY-NOTICES.md).
