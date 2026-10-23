# Quest inventory and preview checks

```powershell
dotnet run --project tests/QuestInventoryTests/QuestInventoryTests.csproj -c Release
dotnet build tests/QuestInventoryTests/NativeBindings/NativeBindings.csproj -c Release
```

The first command links the production policies and preview constructors. Its
synthetic cases cover matching action IDs, blocked preview priority, exact target
identity, stale session/slot rejection, tracker eligibility, current counts and
zero-or-one native callback attempts. It never loads a game host.

The second compiles the actual production item, emote and inventory services,
including sheet metadata and no-queue item calls, with the installed API15
libraries. `DalamudLibPath` may override the library directory. A minimal switch
stub replaces Configuration; no services or live game data are initialized.
Compilation is not proof of native inventory or item-use behavior in FFXIV.
