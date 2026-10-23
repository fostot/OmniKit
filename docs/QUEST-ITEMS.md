# Quest item interaction

The optional **Use verified quest items** setting is off by default. The first supported case is **Rocks in a Hard Place**: use the Tiller's Shovel on its three cumbersome rocks during sequence 2. Strewn debris keeps ordinary interaction. This is a deliberately limited mapping, not a general quest solver.

A fresh Interact press must pass the existing visibility, range, combat/busy and confirmed-target guards. The item service additionally requires the accepted quest and sequence, one active matching native quest handler with no competing active quest handler, one matching tracked quest containing the shovel, an owned KeyItems entry, matching non-ground item/action metadata, and native action status zero. A matched but blocked attempt does not fall through to another interaction. There is no polling use, retry, dialogue advancement or AI identification. Native execution still needs in-game verification.

Observation does not call native action-status validation. It can offer an
otherwise eligible target at the configured detection distance; actual item use
still requires the native action's range and usability on the deliberate press.

If a matched item action is blocked, Interact settings show a brief reason, such as a missing tracked item or native usability failure. Success and unrelated targets clear that feedback. No raw native status values or player data are displayed.

The mapping uses independently checked public game facts, not copied Questionable code or quest JSON:

| Fact | Value | Source |
| --- | --- | --- |
| Quest | short ID 471, sheet row 66007, `SubSea059_00471` | [Quest game data](https://v2.xivapi.com/api/sheet/Quest/66007?fields=Name,Id,QuestParams,QuestListenerParams,TodoParams) |
| Current step | sequence 2 separates removing rocks with the shovel from picking up debris | [Extracted game quest text](https://github.com/xivapi/ffxiv-datamining/blob/master/csv/en/quest/004/SubSea059_00471.csv) |
| Tiller's Shovel | EventItem 2000350, Quest 66007, Action 1, non-ground | [EventItem game data](https://v2.xivapi.com/api/sheet/EventItem/2000350?fields=Name,Quest,Action) |
| Cumbersome rocks | EObj 2001243, 2001244, 2001245; quest 66007, territory 134 | [Quest script parameters](https://v2.xivapi.com/api/sheet/Quest/66007?fields=QuestParams,QuestListenerParams), [object name](https://v2.xivapi.com/api/sheet/EObjName/2001243?fields=Singular) |
| Debris excluded | EObj 2001246, 2001247, 2001248 | [Debris game data](https://v2.xivapi.com/api/sheet/EObjName/2001246?fields=Singular) |

The native tracker exposes `QuestId`, `Sequence`, `ItemIds` and `ItemCount` through [QuestTodoList.TrackedQuest](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/QuestTodoList.cs). These fields link an item to a quest, not necessarily to each objective target. Both rock removal and debris pickup share this quest and current sequence. `EventHandler.GetEventItemId` has no documented target-specific meaning, and the shovel uses the generic Key Item action. Consequently, quest association or an item tooltip alone is not used to infer additional mappings.

Questionable's current public step IPC does not expose the required item or target ID. Its installed QuestPaths assembly contains the schema but no embedded quest JSON; the current upstream catalog getter is empty. Current quest definitions come from a separately downloaded bundle. Tsukimichi's item-to-quest query describes reward sources, not required-use items. No generic adapter or bundled third-party quest catalog is implemented.

`tests/QuestItemTests` exercises the pure eligibility policy, ambiguity and missing-data rejection, debris/unknown fallback, disabled state, and zero-or-one action attempt. These synthetic tests do not establish live native execution.
