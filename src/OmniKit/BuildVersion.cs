using System.Reflection;

namespace OmniKit;

internal static class BuildVersion
{
    public static string Display { get; } = typeof(BuildVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Version unavailable";
    public static string CommitSha { get; } = Metadata("OmniKit.CommitSha");
    public static string CommitCount { get; } = Metadata("OmniKit.CommitCount");

    private static string Metadata(string key) => typeof(BuildVersion).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(attribute => attribute.Key == key)?.Value ?? "Unavailable";
}
