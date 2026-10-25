# Character window job switcher

Enable **Launcher → Character window job switcher**, then click an unlocked class
or job in **Character → Classes/Jobs**. A controller confirm release requests one
switch; holding confirm does not continually switch. Shift-clicking a crafter
retains its native desynthesis action.

This replaces that individual feature in Simple Tweaks or HaselTweaks. Disable
their **Character Window Job Switcher / Character Class Switcher** tweaks first.
OmniKit detects whether either plugin is loaded, but cannot inspect its tweak
settings; confirm in OmniKit after disabling those tweaks. The other plugins can
remain installed. Nothing watches global error messages or responds to unrelated
gearset errors.

## Missing gearsets

- A usable gearset that exactly matches the current class and physical equipment
  can serve as the return point. A linked glamour plate is excluded from reuse.
- If any gearset already exists for the current class, OmniKit does not create
  another one, even if the currently equipped items differ or that set has missing
  items. Only a class with no set receives a new class-named snapshot. Existing
  sets are not overwritten. An unmatched current outfit has no automatic return
  snapshot; save your changes manually if you want to retain it in an existing set.
- An existing usable set for the requested job is equipped directly. An existing
  broken set is left unchanged and reported; there is no silent parent-class
  substitution.
- If the requested set is missing, OmniKit selects an owned, equippable main-hand
  weapon or tool, preferring item level, then HQ, then stable bag position. It
  requires the exact soul crystal for an advanced job. The order is weapon/soul
  crystal → confirm actual class → **+ create gearset** → **Recommended Gear** →
  observe the recommended equipment → **Reassign Gear** on that new set.
  Native class naming is retained when correct; only the request-created row may
  receive a corrected, localized display name. Its class, outfit and name are
  verified before and after saving.

Preflight requires all needed items, the unlocked job and enough free gearset
slots before changing equipment. Two missing sets need two free slots. The
inventory search covers personal bags, main-hand/soul-crystal Armoury Chest
sections and the currently equipped main hand/crystal. It does not buy items,
withdraw from retainers. Recommended Gear uses the game's own recommendations
from currently available equipment; it does not acquire missing equipment.

## Failure handling

Only the explicit class click begins a request. Combat, occupied states,
cutscenes, loading, crafting, gathering, mounting, text input and lost game focus
block it. Each native action is issued once and observed before advancing.
Character/area changes, manual equipment or unrelated gearset-selection changes
stop the request. No delayed retry is queued.

A failed request may attempt one restoration to the unchanged saved return set
when the character remains safe and the equipment still matches the expected
transition. Otherwise the local message identifies the gearset for manual
restoration when an exact saved return set exists. A new target set created before
recommendation is retained if later steps fail; the message identifies it for
review. Created snapshots are retained. A native gearset operation can have
partial effects; cancellation cannot promise that the game restores everything.
Strict equipment checks can stop when a linked glamour plate changes cosmetics.
Facewear is not independently compared by the physical-equipment guard.

## Source and verification

The behavior reference was Simple Tweaks'
[CharacterClassSwitcher](https://github.com/Caraxi/SimpleTweaksPlugin/blob/c039932e40b0c752c09005cc4fdc362b6b786da4/Tweaks/CharacterClassSwitcher.cs),
including its missing saved-gearset case. OmniKit's implementation is independent;
no AGPL tweak source or class-to-node table was copied. Native contracts come from
the public [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) and
[Dalamud](https://github.com/goatcorp/Dalamud) SDKs. The maintained
[Questionable equipment interaction](https://github.com/PunishXIV/Questionable/blob/new-main/Questionable/Controller/Steps/Interactions/EquipItem.cs)
also documents the inventory move API used for equipping items.

Synthetic tests cover planning, missing items/capacity, one-action sequencing,
interference, cancellation, timeout and bounded restoration. SDK compilation and
offscreen settings rendering do not verify native Character-window event handling
or actual equipment changes. Those require the in-game checklist.
