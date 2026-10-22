# OmniKit

Your everyday toolkit for XIV, with Bimble the moon bunny (or a moon bear).

**Early preview: 0.1.0-dev.1 · Dalamud API 15 · Windows x64.**
Build and isolated tests are separate from game validation. This preview has not
yet been observed running inside FFXIV. See [feature status](docs/FEATURES.md)
for the full scope and [game checklist](docs/IN-GAME-CHECKLIST.md) for acceptance.

## Install

1. Open Dalamud settings with `/xlsettings`, then **Experimental**.
2. Add this custom plugin repository and save:

   ```text
   https://raw.githubusercontent.com/fostot/OmniKit/main/repo.json
   ```

3. Open `/xlplugins`, search **OmniKit**, and install it.
4. Press **Alt + Space**, or type `/omnikit`. First run explains the local features
   and lets you choose **Use without AI** or **Set up AI**.

Open settings with **Ctrl + F10** or `/omnikit settings`. Both shortcuts are
changeable. A desktop launcher can also own Alt + Space; change one shortcut if
they overlap. Soft interaction, voice and AI start off until you enable them.

## What this preview includes

- Search game windows, loaded plugins, community recruitment and saved gearsets.
  Try `guild`, `community`, `free company`, `inventory` or `job paladin`.
- Optional camera-directed interaction: look toward an eligible nearby object
  and press the configured key. The optional HUD names the candidate. No valid
  target means the key is left alone. One press requests one native interaction.
- Optional gathering gear changes and restoration after an observed gathering
  session, with cancellation when you change gear yourself. Save your gearsets in
  FFXIV first. This does not reveal hidden nodes or choose gathering items.
- Local Windows speech input, typed commands, saved phrase-to-action bindings and
  a practice button. Speech starts only when requested, for one utterance.
- Optional Responses-compatible AI text chat with endpoint-bound encrypted key
  storage. AI receives the submitted question, not game state or plugin files.
  Replies cannot execute commands. The local matcher does not need AI.
- SQLite preferences, JSON export/import, selectable avatars and optional motion.
- Native Umbra menu integration, an optional Umbra toolbar widget, and optional
  in-game wiki navigation through Browsingway.

## Umbra

OmniKit automatically appears in Umbra's **Plugins** menu through its standard
Dalamud main/configuration windows.

For a dedicated toolbar button, download `OmniKit.Umbra.dll` from the release,
keep it in a permanent local folder, and load it through **Umbra Settings →
Plugins**. Then add the **OmniKit** toolbar widget. It uses Umbra's own styling and
icons. See the [companion instructions](src/OmniKit.Umbra/README.md).

## Wiki inside the game

Install [Browsingway](https://github.com/Styr1x/Browsingway), then use `/bw config`
to create an overlay named `omnikit`. Keep it visible and enabled; turn off Click
Through, Type Through and ACT/IINACT optimizations for this overlay. In OmniKit's
**Data** settings, enable **Open wiki searches inside Browsingway**. OmniKit sends
the search to that existing overlay; it cannot confirm page rendering. Otherwise
wiki searches use your system browser. The wiki is community-run.

## Local brain and optional AI

The local brain matches names, synonyms, small spelling mistakes and saved
phrases to a known action catalogue. A unique spoken `job <name>` can equip its
saved set; ambiguous results stay in the menu for you to choose. This is useful
without a language model and makes no provider requests with AI disabled.

AI adds conversational answers and explanations. This preview supports an API
key or a compatible local Responses server. Provider API use may incur charges.
ChatGPT OAuth and Codex/Claude CLI bridges are tracked work, not implemented
sign-in choices. A subscription is not an API key. Character instructions guide
the assistant, but are not a guarantee against every off-topic answer or prompt
injection; the execution boundary is the fixed local action catalogue.

Preferences live in `omnikit.db` under Dalamud's OmniKit configuration directory.
Keys use separate Windows DPAPI storage, bound to their provider endpoint.
Exports never include keys, recordings or conversations. Exports can contain
your custom provider address and voice phrases, so review them before sharing.
Import always disables AI in the interface until you explicitly enable it again.

## Build and test

Install .NET SDK 10 and Dalamud API 15 references in the normal XIVLauncher dev
directory. Umbra's companion requires Umbra 3.1.18.0 references. Override the
`DalamudLibPath` / `UmbraLibPath` build properties for a different local location.

```powershell
dotnet build src/OmniKit/OmniKit.csproj -c Release
dotnet build src/OmniKit.Umbra/OmniKit.Umbra.csproj -c Release
dotnet run --project tests/SearchTests/SearchTests.csproj -c Release
dotnet run --project tests/GatheringStateTests.csproj -c Release
dotnet run --project tests/StorageTests/StorageTests.csproj -c Release
pwsh scripts/Package.ps1
```

Release packages contain only reviewed runtime files and assets. Local databases,
credentials, build intermediates and user exports are not source or backup content.
See [IPC integration](docs/INTEGRATION.md) for exposing additional plugin actions.

## References and artwork

The soft-interaction concept was informed by the [awesome_wotlk fork](https://github.com/srazdokunebil/awesome_wotlk).
OmniKit's implementation is original and uses XIV's current object, camera and
interaction APIs; it includes no WoW client patch code, addresses or loaders.
The [ChokoLoader reference](https://github.com/POKOch/ChokoLoader-Awesome-for-Warmane)
was inspected as background only. [Artwork prompts](assets/PROMPTS.md) document
the generated moon companions. Dependency notices are in
[THIRD-PARTY-NOTICES](docs/THIRD-PARTY-NOTICES.md).
