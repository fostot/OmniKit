# Aetherytes and aethernet shards

1. Open **Settings → Game tools → Unlocks → Aetheryte**.
2. Choose **Current zone**, a zone tab, or **All areas**. **Find an area** jumps to a zone.
3. Press **Start unlock** with vnavmesh enabled. **Stop**, **Esc**, or `/omnikit unlock stop` ends the run.

Check marks come from the game's attunement flags. Both major aetherytes and physical shards appear, including those already unlocked. Virtual aethernet exits say **Travel exit** and have no attunement box. Current-zone objects refresh their known locations; manual browsing stays on the selected zone across area changes.

## Initial route coverage

The runner visits the nearest eligible current-zone destination first and recalculates after each confirmed attunement. **All areas** can continue through verified walking exits. A named zone limits the desired unlocks to that zone, including supported walking travel to reach it.

Installed sheets and layout objects provide names and real world coordinates. The inspected data supplied 107 major aetherytes, 90 physical shards and 31 virtual exits; future sheet contents are read dynamically. Ambiguous/missing positions stay visible but cannot become route targets. Known quest prerequisites must be complete.

**Travel coverage is incomplete.** This version has 10 unconditional directed walking connections in the inspected layouts. Conditional layers, ferries, airships, paid travel, flying and special transport links are not automated. There is no complete world unlock tour. Unsupported travel stops with a reason and never counts as completed. Boats require separately verified NPC/menu/fare/access/arrival mappings before they can be added.

## Control and completion

Only explicit Start begins movement. Runs never resume after logout, reload or a character change. Game attunement flags remain authoritative; the catalogue and route state stay in memory and are not exported or sent to AI. No AI requests are made.

The runner checks object kind, base ID, address, range and confirmed hard target before one native interaction. It never advances dialogue or chooses unknown menu options. Path completion alone proves neither arrival nor attunement: the expected territory or native unlock flag must be observed. Failed targets/connections are excluded for the remainder of the run; a new Start is an explicit fresh attempt.

Combat, death, crafting, gathering, mounting, lost focus, text input or manual movement stop automatic control. Expected loading and mesh warmup have bounded waits. NomNom, soft-interaction actions and Character-window job switching do not compete with an active unlock run.

## vnavmesh ownership

Other navigation automations must remain idle. Public IPC has no caller ownership token. OmniKit owns its cancellable query, ignores late results and dispatches one ground path only when navigation remains idle. It checks the remaining waypoint suffix before stopping a path. A different path causes ownership loss without a global stop; another caller replacing a path with exactly the same remaining waypoints is indistinguishable. This is a cooperation limit, not an absolute ownership guarantee.

The combined SimpleMove pathfind-and-move request is not used because global Path.Stop does not cancel its pending query. Exact contracts and synthetic checks are in the [IPC verification notes](../tests/VnavmeshContractTests/README.md).

## Verification

Synthetic tests cover directed route ordering, gated/virtual exclusions, cancellation, ownership loss, late results, finite loading/attunement waits and one interaction per attempt. Public-data checks inspect sheets/layouts only. Offscreen UI checks cover navigation, areas, scopes, tabs, counts and fixed Start/Stop controls.

Actual movement, zone transitions and attunement still need the [in-game checks](IN-GAME-CHECKLIST.md). Compilation, synthetic tests and offscreen rendering are separate from live game validation.
