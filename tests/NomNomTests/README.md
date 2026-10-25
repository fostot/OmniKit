# Nom Nom verification

`dotnet run --project tests/NomNomTests/NomNomTests.csproj` executes synthetic policy tests only. `dotnet build tests/NomNomTests/NativeCompile/NativeCompile.csproj` compiles the real native adapter against the installed Dalamud SDK with a four-field configuration stub; it does not execute it or launch XIV.

Verified installed contracts: Item.ItemAction and its Action/Data/DataHQ properties; Action.TargetArea; ItemFood.EXPBonusPercent; InventoryManager.GetInventoryContainer/GetInventorySlot/PendingOperations; physical InventoryItem.ItemId/Quantity/Flags; PlayerState.ContentId; local player GameObjectId and StatusList.StatusId; ActionManager.ActionQueued/AnimationLock/GetActionStatus/UseAction with UseActionMode.Macro.

The source uses exact personal bag container and slot in `UseAction`'s extra parameter, explicit self target, normal/HQ item ID, and immediate revalidation. No AgentInventoryContext.UseItem hook, chat commands, network calls, AI/provider requests, config/profile inspection, or game/native invocation is used by the tests.

Public references:
- https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/ActionManager.cs documents the item bag/slot extra parameter and action status/use signatures.
- https://github.com/xivapi/ffxiv-datamining/blob/master/csv/en/ItemAction.csv contains 844 and 845 actions with argument zero equal to Well Fed status 48 and argument one identifying ItemFood. Filtering to those action kinds plus exact status avoids treating arbitrary consumable parameters as food.
- https://github.com/xivapi/ffxiv-datamining/blob/master/csv/en/ItemFood.csv exposes EXPBonusPercent.
- https://github.com/xivapi/ffxiv-datamining/blob/master/csv/en/Status.csv identifies row 48 as Well Fed.

These checks establish API availability and synthetic behavior. Automatic food use and its effects in the live game still need user acceptance testing with the opt-in feature.
