namespace OmniKit;

internal static class Services
{
    internal static TestPluginInterface PluginInterface { get; } = new();
}

internal sealed class TestPluginInterface
{
    public void SavePluginConfig(Configuration configuration) =>
        throw new InvalidOperationException("Storage tests must not call Dalamud persistence.");
}
