# HUD font and visibility checks

From the repository root, after building the plugin:

```powershell
dotnet build src/OmniKit/OmniKit.csproj -c Release --no-restore
dotnet run --project tests/HudUiSmoke/HudUiSmoke.csproj
dotnet run --project tests/StorageTests/StorageTests.csproj
```

The HUD harness loads the compiled plugin and the installed Dalamud ImGui library. It renders offscreen at the minimum 480×330 settings size and UI scales 1, 1.5 and 2. It clicks the font slider, opens the effect menu, checks the scrolled controls and Modify scroll reset, and measures native text vertex heights at 50%, 100% and 200%. It checks independent outlines, all four effects and readable shimmer colors. Images are written under `.local/visualharness/hud-font`.

Synthetic condition services and a locally allocated addon structure check that active world interactions suppress both live HUD and editing previews, then restore the HUD when the interaction ends. The installed native addon attributes verify these window names: Talk, CutSceneSelectString, Shop, InclusionShop, FreeShop, JournalAccept, JournalResult, RetainerList, RetainerTaskAsk, RetainerTaskResult, Gathering and GatheringMasterpiece. Lifecycle flags cover OccupiedInEvent, OccupiedInQuestEvent, OccupiedSummoningBell, Gathering and ExecutingGatheringAction, plus the three cutscene flags.

Hidden Talk windows, generic Occupied/Casting flags, player chat, inventory and system settings alone do not suppress it. These checks never call game interaction, camera, targeting or observation code. A live game check is still needed to confirm the game's actual interaction lifecycle.

Storage checks cover schema 1–4 migration to schema 5, preservation of existing fields and AI model choices, default shadow appearance, independent outline/effect round trips, invalid values and transaction rollback.
