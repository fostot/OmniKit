# Verified quest emotes

**Use verified quest emotes** is off by default. Its first supported case is **A Bridge Too Full**: perform Soothe on either of the two irate coachmen during sequence 2 in Lower La Noscea. Other actors, including Wyrstmaga and Doesrael, retain ordinary interaction. There is no generic emote inference, quest solver, AI lookup or third-party quest catalog.

The caller must already have a fresh Interact press, an eligible visible target within the configured detection distance, the existing combat/busy guards, and a confirmed hard target. The service requires the accepted quest and exact sequence, a valid targetable event NPC, one matching active native quest handler with no competing active quest handler, and matching local Emote metadata. On the press it also requires the native unlock and current emote-availability checks. It then rechecks the target identity/address and makes at most one `EmoteManager.ExecuteEmote` call with an explicit target in `PlayEmoteOption`. It does not queue, retry, advance dialogue or fall through to ordinary interaction after a matched failure. Looking only queries eligibility; it never executes an emote or polls its immediate usability.

Blocked attempts show fixed concise feedback in Interact settings. Success or an unrelated target clears that feedback. No quest names, player data, raw native status values or diagnostics are logged by this service. With this option enabled, a recognized coachman whose required step is no longer active is blocked rather than receiving an automatic replacement interaction.

The mapping was independently checked against public extracted game data:

| Fact | Verified value | Source |
| --- | --- | --- |
| Quest | short ID 407; Quest sheet row 65943; `SubSea110_00407` | [Quest data](https://v2.xivapi.com/api/sheet/Quest/65943?fields=Name,Id,QuestParams,TodoParams) |
| Required step | sequence 2; objective index 1 requires Soothe | [Extracted game quest text](https://github.com/xivapi/ffxiv-datamining/blob/master/csv/en/quest/004/SubSea110_00407.csv) |
| Targets | ENpcBase 1002449 and 1002450, both irate coachmen | [Quest objective locations](https://v2.xivapi.com/api/sheet/Quest/65943?fields=TodoParams), [NPC names](https://v2.xivapi.com/api/sheet/ENpcResident?rows=1002449,1002450&fields=Singular) |
| Territory | 135, from both objective locations | [Quest objective locations](https://v2.xivapi.com/api/sheet/Quest/65943?fields=TodoParams) |
| Emote | Emote 35, Soothe; TextCommand 436; UnlockLink 0 | [Emote data](https://v2.xivapi.com/api/sheet/Emote/35?fields=Name,TextCommand,UnlockLink), [command data](https://v2.xivapi.com/api/sheet/TextCommand/436?fields=Command,ShortCommand) |

These are original narrow matching rules using public game facts. No Questionable implementation, AGPL quest file or bulk quest mapping was copied.

The native interfaces are documented in upstream [EmoteManager](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/Control/EmoteManager.cs), [EmoteController.PlayEmoteOption](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/Control/EmoteController.cs), and [UIState.IsEmoteUnlocked](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/UIState.cs). The option uses the resolved native virtual table and the freshly confirmed object's native ID. The service uses the player-facing EmoteManager, not another character's animation controller.

`CanExecuteEmote` establishes local emote availability; it does not prove server-side quest credit or target-specific quest range. The game decides whether the emote advances the objective. No undocumented per-coachman completion variables are inferred, so another deliberate press can repeat Soothe while the quest remains on this step. The mapping, native binding compilation and 72 pure policy checks were verified without a running game. Actual FFXIV execution and quest credit remain unverified.

```powershell
dotnet run --project tests/QuestEmoteTests/QuestEmoteTests.csproj -c Release
dotnet run --project tests/QuestEmoteTests/NativeBindings/NativeBindings.csproj -c Release
```

The first command tests exact targets, unrelated-actor fallback, disabled state, missing/ambiguous context, unlock/state rejection, fixed feedback and zero-or-one execution attempts. The second compiles the specific calls against installed API15 bindings and runs only a confirmation message. It invokes no game function and reads no active quest, character, profile or credentials. `DalamudLibPath` can override the development-library location at build time.
