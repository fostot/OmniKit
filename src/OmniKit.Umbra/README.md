# OmniKit for Umbra

This optional companion adds an actual Umbra toolbar widget with a native popup,
Moon icon, search button, and settings button. Umbra owns its appearance, spacing,
icon selection, tooltip, popup placement, and saved widget configuration.

The main OmniKit plugin also appears automatically in Umbra's existing Plugins
menu when loaded. That integration does not require this companion.

## Load the native widget

1. Install and enable the main OmniKit Dalamud plugin.
2. Open Umbra settings, then **Plugins**. Enable custom plugins if needed.
3. Add the companion **OmniKit.Umbra.dll** as a local plugin through Umbra's own
   plugin picker. Restart Umbra when its interface requests it.
4. In Umbra's toolbar widget settings, add the **OmniKit** widget to the toolbar
   and position it as desired.

This DLL is an Umbra extension, not a second Dalamud plugin. Do not register it
with Dalamud's developer-plugin loader. Installing the core Dalamud plugin does
not silently change Umbra's settings or place widgets on an existing toolbar.

## Build

Requires .NET SDK 10, Dalamud API 15 reference assemblies, and Umbra 3.1.18.0
reference assemblies. The project defaults to the standard XIVLauncher locations
under the current user's `APPDATA`. Override `UmbraLibPath`, `UmbraVersion`, or
`DalamudLibPath` as MSBuild properties when building elsewhere.

```powershell
dotnet build src/OmniKit.Umbra/OmniKit.Umbra.csproj -c Release
```

Distribute only `OmniKit.Umbra.dll`; Umbra provides the referenced framework
assemblies. Keep the companion at a stable local path so Umbra can load it again.
It may need recompilation when Umbra changes its extension API.

## Integration references

The implementation uses the documented public API, with original source code:

- [Official Umbra plugin example](https://github.com/una-xiv/Umbra.SamplePlugin)
- [StandardToolbarWidget](https://github.com/una-xiv/umbra/blob/main/Umbra/src/Toolbar/Widgets/System/Types/StandardToolbarWidget.cs)
- [MenuPopup](https://github.com/una-xiv/umbra/blob/main/Umbra/src/Toolbar/Widgets/System/Popup/MenuPopup.Components.cs)
- [Umbra's Dalamud plugin menu](https://github.com/una-xiv/umbra/blob/main/Umbra/src/Toolbar/Widgets/Library/PluginList/PluginListWidget.cs)
