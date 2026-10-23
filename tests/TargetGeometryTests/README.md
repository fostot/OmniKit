# Camera target geometry

```powershell
dotnet run --project tests/TargetGeometryTests -c Release
```

The linked production helper is tested with a synthetic perspective camera and a target 30 yalms ahead. Fixtures cover small pitch changes that failed the former circular screen-center test, wider pitch while the target remains visible, three viewport aspect ratios, offset viewports, offscreen/behind-camera points, side targets, saved range and limited incumbent hysteresis.

Observed result: **102 synthetic geometry checks passed**. The expanded interaction input/category/action harness also passed its 137 combined checks, and the plugin compiled without warnings or errors.

The helper ranks horizontal alignment first; vertical position and distance break ties. It requires a successful projection and a center inside the viewport. It does not provide visibility or permission to act. `FindCandidate` still checks native targetability and `IsObjectInViewRange`, the configured hitbox-edge range, category toggles, active world interaction handlers and the camera collision ray. The press path and delayed gathering callback rerun detection before acting. The separate target-before-action policy now selects and confirms the object before its one requested action. Native interaction range and HUD rendering remain unchanged.

The [public native binding](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/Control/TargetSystem.cs) does not document `IsObjectInViewRange` as a configurable distance test, so that guard is retained. Synthetic tests do not establish live camera projection, collision, game interaction reach or acceptance. Objects with an offscreen center remain excluded even if part of a large model is visible.
