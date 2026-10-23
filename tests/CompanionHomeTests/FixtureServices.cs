namespace OmniKit
{
    internal static class Services { internal static FixturePluginInterface PluginInterface { get; } = new(); }
    internal sealed class FixturePluginInterface
    {
        public void SavePluginConfig(Configuration configuration) => throw new InvalidOperationException("This synthetic harness cannot save game settings.");
    }
}

namespace OmniKit.Storage
{
    public sealed record VoiceBinding(string Phrase, string ActionId);
}
