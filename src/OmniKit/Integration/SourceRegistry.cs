using Dalamud.Plugin.Ipc;
using System.Text.Json;
using OmniKit.Search;

namespace OmniKit.Integration;

internal sealed class SourceRegistry : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, SourceAction[]> sources = new(StringComparer.Ordinal);
    private readonly ICallGateProvider<string, string, bool> register;
    private readonly ICallGateProvider<string, bool> unregister;
    private readonly ICallGateProvider<string, string, object> selected;
    private readonly ICallGateProvider<bool> available;

    public SourceRegistry()
    {
        var pi = Services.PluginInterface;
        register = pi.GetIpcProvider<string, string, bool>("OmniKit.RegisterActions.v1");
        unregister = pi.GetIpcProvider<string, bool>("OmniKit.UnregisterActions.v1");
        selected = pi.GetIpcProvider<string, string, object>("OmniKit.ActionSelected.v1");
        available = pi.GetIpcProvider<bool>("OmniKit.IsAvailable.v1");
        register.RegisterFunc(Register);
        unregister.RegisterFunc(Unregister);
        available.RegisterFunc(() => true);
        pi.GetIpcProvider<object>("OmniKit.Available.v1").SendMessage();
    }

    private bool Register(string pluginName, string json)
    {
        if (string.IsNullOrWhiteSpace(pluginName) || pluginName.Length > 100 || json is null || json.Length > 128_000) return false;
        if (!Services.PluginInterface.InstalledPlugins.Any(p => p.InternalName == pluginName && p.IsLoaded)) return false;
        try
        {
            var actions = JsonSerializer.Deserialize<SourceAction[]>(json);
            if (actions is null || actions.Length > 100) return false;
            if (actions.Any(a => a is null || a.Id is null || a.Title is null || a.Id.Length is < 1 or > 100 || a.Title.Length is < 1 or > 120 ||
                                 a.Description?.Length > 300 || a.Keywords?.Length > 500)) return false;
            if (actions.Select(a => a.Id).Distinct().Count() != actions.Length) return false;
            lock (gate) sources[pluginName] = actions;
            return true;
        }
        catch (JsonException) { return false; }
    }

    private bool Unregister(string pluginName) { lock (gate) return sources.Remove(pluginName); }

    public SearchEntry[] GetEntries()
    {
        var loaded = Services.PluginInterface.InstalledPlugins.Where(p => p.IsLoaded).Select(p => p.InternalName).ToHashSet();
        lock (gate)
        {
            foreach (var name in sources.Keys.Where(name => !loaded.Contains(name)).ToArray()) sources.Remove(name);
            return sources.SelectMany(source => source.Value.Select(action => new SearchEntry(
                $"source:{source.Key}:{action.Id}", action.Title, action.Description ?? "Plugin action", action.Keywords ?? "",
                source.Key, () => Invoke(source.Key, action.Id)))).ToArray();
        }
    }

    private void Invoke(string source, string id)
    {
        if (!Services.PluginInterface.InstalledPlugins.Any(p => p.InternalName == source && p.IsLoaded))
            throw new InvalidOperationException("The source plugin is no longer loaded.");
        lock (gate)
            if (!sources.TryGetValue(source, out var actions) || !actions.Any(action => action.Id == id))
                throw new InvalidOperationException("The source action is no longer registered.");
        _ = Services.Framework.RunOnFrameworkThread(() =>
        {
            if (Services.PluginInterface.InstalledPlugins.Any(p => p.InternalName == source && p.IsLoaded))
                selected.SendMessage(source, id);
        });
    }

    public void Dispose()
    {
        register.UnregisterFunc(); unregister.UnregisterFunc(); available.UnregisterFunc();
        lock (gate) sources.Clear();
    }

    private sealed record SourceAction(string Id, string Title, string? Description, string? Keywords);
}
