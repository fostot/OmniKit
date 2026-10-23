# HUD font and visibility checks

From the repository root, after building the plugin:

```powershell
dotnet build src/OmniKit/OmniKit.csproj -c Release --no-restore
dotnet run --project tests/HudUiSmoke/HudUiSmoke.csproj
dotnet run --project tests/StorageTests/StorageTests.csproj
```

The HUD harness loads the compiled plugin and the installed Dalamud ImGui library. It renders offscreen at the minimum 480×330 settings size and UI scales 1, 1.5 and 2. It clicks the font slider, opens the effect menu, checks the scrolled controls and Modify scroll reset, and measures native text vertex heights at 50%, 100% and 200%. It checks independent outlines, the original four effects and readable shimmer colors. `HudEffectTests` checks the full eleven-effect renderer; `SettingsUiSmoke` checks its menu and expanded adjustment controls. Images are written under `.local/visualharness/hud-font`.

Synthetic condition services and a locally allocated addon structure check that active world interactions suppress both live HUD and editing previews, then restore the HUD when the interaction ends. The installed native addon attributes verify these window names: Talk, CutSceneSelectString, Shop, InclusionShop, FreeShop, JournalAccept, JournalResult, RetainerList, RetainerTaskAsk, RetainerTaskResult, Gathering and GatheringMasterpiece. Lifecycle flags cover OccupiedInEvent, OccupiedInQuestEvent, OccupiedSummoningBell, Gathering and ExecutingGatheringAction, plus the three cutscene flags.

Hidden Talk windows do not count as conversations. Ordinary action pauses, text input and unrelated focused menus hide the normal HUD; explicit modification may still show its sample. Passive player bags may remain actionable and display the configured key hint. These checks never call game interaction, camera, targeting or observation code. A live game check is still needed to confirm the game's actual interaction lifecycle.

The focused bags regression runs with `dotnet run --project tests/HudUiSmoke -- src/OmniKit/bin/Release/OmniKit.dll .local/visualharness/hud-bags --bags-only`. It verifies native inventory addon names, displays `[E]` when ready with bags open, hides the normal HUD during synthetic action pauses and keyboard capture, and keeps explicit Modify, context-menu and drag states visible. World interactions still hide every state. `InteractionInputTests` separately checks the exact passive-bag focus allowlist and that filters, chat, vendors, retainers and unknown menus remain blocked. These are synthetic checks, not live game interaction evidence.

The same focused run sends real ImGui Shift and right-click events to the last ready HUD bounds after actions become paused. It verifies that this opens the keyless editor, that releasing Shift retains the popup, that Shift outside those bounds leaves the HUD hidden, and that dialogue clears the remembered bounds. It never changes the action-paused state or sends operating-system input.

Storage checks cover migration from earlier schemas, preservation of existing fields and AI model choices, default shadow appearance, independent outline/effect round trips, invalid values and transaction rollback.
