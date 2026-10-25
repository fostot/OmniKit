# vnavmesh IPC contract verification

Primary repository inspected through the connected GitHub tool: `awgil/ffxiv_navmesh`, master commit `6fc80725eb8290472eee433fc4be7ee06ec79357` (2026-08-31). This adapter independently implements public IPC contract calls; it does not copy the provider's implementation.

- [IPC registrations](https://github.com/awgil/ffxiv_navmesh/blob/6fc80725eb8290472eee433fc4be7ee06ec79357/vnavmesh/IPCProvider.cs)
- [Combined simple request semantics](https://github.com/awgil/ffxiv_navmesh/blob/6fc80725eb8290472eee433fc4be7ee06ec79357/vnavmesh/AsyncMoveRequest.cs)
- [Path lifecycle and global stop](https://github.com/awgil/ffxiv_navmesh/blob/6fc80725eb8290472eee433fc4be7ee06ec79357/vnavmesh/Movement/FollowPath.cs)
- [Current-scene mesh and cancellable queries](https://github.com/awgil/ffxiv_navmesh/blob/6fc80725eb8290472eee433fc4be7ee06ec79357/vnavmesh/NavmeshManager.cs)

## Exact public contracts

All names have the `vnavmesh.` prefix.

| Name | Arguments | Return |
| --- | --- | --- |
| Nav.IsReady | none | bool |
| Nav.PathfindCancelable | Vector3 from, Vector3 to, bool fly, CancellationToken | Task of List of Vector3 |
| Nav.PathfindInProgress | none | bool |
| Nav.PathfindNumQueued | none | int |
| SimpleMove.PathfindInProgress | none | bool |
| Query.Mesh.PointOnFloor | Vector3 point, bool allowUnlandable, float halfExtentXZ | nullable Vector3 |
| Path.MoveTo | List of Vector3, bool fly | void action |
| Path.IsRunning | none | bool |
| Path.ListWaypoints | none | List of Vector3 |
| Path.Stop | none | void action |

The combined `SimpleMove.PathfindAndMoveTo(Vector3,bool)->bool` is deliberately not used: its pending query is global and `Path.Stop` does not cancel it. The bridge owns a cancellation token for its own `Nav.PathfindCancelable` query, ignores late results, and sends one ground `Path.MoveTo` only after that query completes and global navigation remains idle.

## Ownership and scope limits

There is no caller token or owner ID in public path IPC. Active ownership can only be inferred from the remaining waypoints being an exact suffix of the route dispatched by this bridge, with no other pending queries. A different path causes ownership loss without a global stop. Another caller replacing the path with exactly the same remaining waypoints is indistinguishable. Other navigation automations must remain idle during an OmniKit route; this is a practical cooperation requirement, not an immutable technical lock.

The mesh belongs to the current scene/territory. Public IPC has no target territory or boat/transport interaction contract. FollowPath contains limited client-path handling and a TODO for warp/aetheryte teleport; this does not supply a cross-zone travel planner. The cancellable basic query returns positions without typed waypoint area metadata, so this ground-only bridge does not promise special transport-link behavior. Arrival and successful attunement require independent observations by the caller; `PathEnded` means only that navigation stopped.

`dotnet run --project tests/VnavmeshContractTests/VnavmeshContractTests.csproj` runs fake transport tests. `dotnet build tests/VnavmeshContractTests/NativeCompile/NativeCompile.csproj` proves IPC subscriber signatures compile against the installed SDK. Neither command invokes live IPC or native game functions.
