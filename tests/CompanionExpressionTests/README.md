# Companion expression rendering checks

Run `dotnet run --project tests/CompanionExpressionTests -c Release` from the repository root. This standalone Windows harness source-links the production body mesh, Orbit ring placement/renderer, and reviewed art loaders. It uses the installed Dalamud ImGui bindings and the existing software draw-list renderer; it does not build the plugin or access a running game, user settings, profiles, or network services.

Checks cover 60 and 144 Hz elapsed-time equivalence, both facings, 48/96/192 px sizes, all sampled stride phases, disabled state, mesh fold/seam prevention, exactly one connected body mesh, actual native vertex extents, and unclipped rendered body ink. Orbit uses the production placement method and a single ringless core drawn between the back and front ring passes. The harness advertises native vertex offsets to support contact sheets containing more than 65,535 vertices.

Contact sheets are written only under ignored `.local/visualharness/companion-expressions`. They are diagnostic renders of original assets; source PNGs are never modified. Carry grip offsets are measurements for renderer integration, not a change to physics. These results do not establish in-game rendering or interaction acceptance.
