# Actual quest-action HUD proof

```powershell
dotnet run --project tests/QuestActionHudTests/QuestActionHudTests.csproj -c Release
```

An optional first argument selects an already-built OmniKit DLL. The harness
loads the actual `QuestActionHud.Draw`, action-button renderer and request/input
methods from that binary. It supplies synthetic inventory entries, target and
next-action records, an inert settings-save counter and a generated test texture.
It never calls framework Update, native action methods or live game services.
No account, profile, settings file or game inventory is read.

Native ImGui draw lists are rasterized with the existing SoftwareRenderer into
ignored `.local/visualharness/quest-actions` PNGs. Cases cover locked and unlocked
panels, empty/single/eight/overflow inventories, disabled states, viewport edges,
320/480px widths and scale 1/1.5/2. Input cases perform genuine ImGui mouse events
for hold/release, lock/unlock, dragging and pagination. Requests are inspected and
cleared without executing them. The input-capture proof verifies that owning an
action button does not block that same click, while text input still blocks it.

Bounds checks include panel containment, unexpected scrolling, heading-to-lock
gutters, overlapping default panels and tooltip containment. A failing run keeps
its rendered evidence and exits nonzero. Visual review is still required; these
offscreen tests do not establish live game rendering or native action behavior.
