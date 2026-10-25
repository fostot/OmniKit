# Nom Nom

Open **Automations**, choose a food from the inventory dropdown, enable **Nom Nom**
and leave **Keep +EXP buff active** on. Both switches and a selected eligible food
are required. New settings and migrated profiles start with Nom Nom off and no
food selected.

The dropdown detects supported food and drinks in the four personal inventory
bags, with separate NQ/HQ quantities. It refreshes while logged in; **Refresh food**
requests another scan. Retainers, saddlebag and storage are not searched. Inventory
observations stay in memory and are not exported, logged or sent to an AI provider.

When the character has no Well Fed status, OmniKit checks that the selected food
grants EXP and is currently usable. It prefers NQ unless **Prefer HQ** is checked,
then falls back to the other quality of the same item. It never substitutes a
different food. Any active Well Fed buff is left alone, even after changing the
selected food. It refreshes after expiry rather than continually extending the
duration. Unchecking **Keep +EXP buff active** pauses all eating.

Each attempt rechecks the character, item, exact bag slot, quantity, quality and
native usability before requesting one self-targeted item action. It waits for
Well Fed to appear. A rejection or missing confirmation pauses further attempts;
toggle Nom Nom or change the selection to retry deliberately. A meaningful stock
change can also re-enable checking. Its own delayed one-item decrease cannot
re-arm a failed attempt.

Combat, casting, death, loading, dialogue, cutscenes, crafting, gathering,
mounting, text input, lost focus and OmniKit configuration/input pause eating.
Food use and Character-window job switching exclude each other while pending;
soft interaction also pauses while food use awaits confirmation. Nom Nom makes
no AI requests and does not spend provider tokens.

## Verification and data

Food classification requires known food action 844 or 845, the Well Fed status
48, a valid ItemFood row and a non-area action. EXP eligibility comes from the
row's EXPBonusPercent field, including the selected quality's data. These fields
were checked against the maintained extracted game data:
[ItemAction](https://github.com/xivapi/ffxiv-datamining/blob/master/csv/en/ItemAction.csv),
[ItemFood](https://github.com/xivapi/ffxiv-datamining/blob/master/csv/en/ItemFood.csv)
and [Status](https://github.com/xivapi/ffxiv-datamining/blob/master/csv/en/Status.csv).
Native item-action contracts use the installed Dalamud/FFXIVClientStructs SDK and
the same guarded item-use route already used by OmniKit's manual quest-item bar.

Synthetic tests and SDK compilation are separate from actual item consumption.
The game must still confirm the behavior in the in-game acceptance checklist.
