namespace OmniKit;
internal sealed class Configuration { }
internal static class BuildVersion { public const string Display = "synthetic"; }
internal static class Services { public static TestPluginInterface PluginInterface => throw new InvalidOperationException("No host or profile access is permitted in this fixture."); }
internal sealed class TestPluginInterface
{
    public FileInfo AssemblyLocation => throw new InvalidOperationException();
    public DirectoryInfo ConfigDirectory => throw new InvalidOperationException();
}
