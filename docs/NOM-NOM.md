# NomNom

Open **Automations**, enable **NomNom** and leave **Keep +EXP buff active** on.
**Automatic: best +EXP food** is the default choice. Both switches and an
available eligible food are required. NomNom starts off; manually selected food
IDs remain unchanged after updates. Stored food ID zero now means Automatic.

The dropdown detects supported food and drinks in the four personal inventory
bags, with separate NQ/HQ quantities. It refreshes while logged in; **Refresh food**
requests another scan. Retainers, saddlebag and storage are not searched. Inventory
observations stay in memory and are not exported, logged or sent to an AI provider.

When the character has no Well Fed status, Automatic ranks owned eligible foods
by EXP bonus percentage, then item level, then NQ/HQ preference. It skips food
the game currently rejects. Stable item ID/bag/slot tie-breaks prevent random
selection changes. This ranks EXP and item level, not job-specific combat stats
or market value. The next resolved food is shown below the dropdown.

Selecting a specific food limits consumption to that item. Manual mode prefers
NQ unless **Prefer HQ** is checked, then falls back to the other quality of that
same item. Any active Well Fed buff is left alone, even after changing the
selected food. It refreshes after expiry rather than continually extending the
duration. Unchecking **Keep +EXP buff active** pauses all eating.

Each attempt rechecks the character, item, exact bag slot, quantity, quality and
native usability before requesting one self-targeted item action. It waits for
Well Fed to appear. A rejection or missing confirmation pauses further attempts;
toggle NomNom or change the selection to retry deliberately. A meaningful stock
change can also re-enable checking. Its own delayed one-item decrease cannot
re-arm a failed attempt. In Automatic mode, the same guard spans all eligible food
stock: exhausting one kind cannot trigger repeated eating of the next kind when
the buff never appeared.

Combat, casting, death, loading, dialogue, cutscenes, crafting, gathering,
mounting, text input, lost focus and OmniKit configuration/input pause eating.
Food use and Character-window job switching exclude each other while pending;
soft interaction also pauses while food use awaits confirmation. NomNom makes
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
