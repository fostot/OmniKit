# Companion frame and grounded movement checks

Run `dotnet run --project tests/CompanionPhysicsTests -c Release` for the source-linked physics checks, or append `--grounded-only` to isolate the grounded movement branch.

Synthetic checks cover horizontal saved-area roaming, physical floor return, short hops to observed frame tops, closed/moved targets, retained flight velocity, disabled hops, viewport bounds and house collision. Navigation goals must lie on an observed support or floor. An unreachable or blocked goal reports `Blocked` at the actual location; it never invents a diagonal walk or teleport. The fixture also checks higher and lower nap-route goals and preserves existing drop, carry, ledge, containment and falling checks.

These checks run without Dalamud, a game process, network access or user profiles. They establish geometry and state behavior, not in-game visual acceptance.
