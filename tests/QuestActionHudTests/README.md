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

Append `--tooltip-only` for the focused tooltip regression matrix. It hovers the
actual disabled action button with the Ripe La Noscean Orange name and the full
public range/state reason at all four viewport corners, widths 320/480 and scales
1/1.5/2. Each repeated hover checks zero scrolling, no child scroll window, the
native no-input flags, stable placement, every expected nonspace glyph and its
complete clip bounds. The unchanged lock/drag/request cases remain in the normal
full harness run.

Append `--visibility-only` for the matched-action visibility checks. Ordinary
Interact/Target/Target + attack previews, absent targets and paused states must
stay hidden even unlocked. Matched item/emote previews remain visible, including
an unusable item's blocked reason. These cases never invoke game actions.
