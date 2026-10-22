# Plugin actions

OmniKit discovers loaded Dalamud plugins' exposed main/configuration windows.
Additional actions require the source plugin to opt in. OmniKit does not read
arbitrary plugin files or send provider descriptions to AI.

All calls below use Dalamud's public call gates. Subscribe to availability before
checking the function, so a reload does not lose registration.

| Call gate | Signature / behavior |
| --- | --- |
| `OmniKit.IsAvailable.v1` | `GetIpcSubscriber<bool>().InvokeFunc()` |
| `OmniKit.Available.v1` | `GetIpcSubscriber<object>()`, no-argument availability message |
| `OmniKit.RegisterActions.v1` | `GetIpcSubscriber<string,string,bool>().InvokeFunc(pluginInternalName,json)` |
| `OmniKit.UnregisterActions.v1` | `GetIpcSubscriber<string,bool>().InvokeFunc(pluginInternalName)` |
| `OmniKit.ActionSelected.v1` | `GetIpcSubscriber<string,string,object>()`, `(pluginInternalName,actionId)` message |

Register a JSON array with `Id`, `Title`, `Description`, and `Keywords`. The latter
two may be null. The source must be a currently loaded plugin. Limits are 100
actions, 128,000 JSON characters, unique IDs of 1–100 characters, titles of 1–120,
descriptions up to 300 and keywords up to 500. Register again to replace a set.

```json
[{"Id":"example.panel","Title":"Example Panel","Description":"Open the panel","Keywords":"example options settings"}]
```

Subscribe to selected messages, filter by your exact internal name, revalidate
the current game state, and execute only recognized IDs. An unavailable plugin
loses its entries. Unregister when unloading; register again after OmniKit reloads.
IPC is cooperative communication between already trusted in-process plugins,
not an authentication boundary. Call gates cannot prevent another plugin from
claiming a loaded plugin's name.

Structured read-only knowledge sources for AI are planned separately. Registration
does not authorize background data collection or provider transmission.
