using System.Numerics;
namespace OmniKit
{
    internal sealed class Configuration
    {
        public bool CompanionSeasonalEnabled = true;
        public string CompanionSeasonalTheme = "Auto";
        public static readonly string[] AvatarNames = ["Moonblob","Wink","Nova","Mochi","Puff","Orbit","Ember","Prism"];
        public static readonly string[] OutfitNames = ["Default"];
    }
    // Type-only references: native observation is compiled but never invoked by this executable.
    internal static class Services
    {
        internal static Dalamud.Plugin.Services.IFramework Framework => throw new InvalidOperationException("No game calls in synthetic tests.");
        internal static Dalamud.Plugin.Services.IClientState ClientState => throw new InvalidOperationException("No game calls in synthetic tests.");
        internal static Dalamud.Plugin.Services.IDataManager Data => throw new InvalidOperationException("No game calls in synthetic tests.");
    }
}
namespace OmniKit.Ui
{
    // Type-only fixtures; actual atlas and nap transform implementations are source-linked.
    internal readonly record struct CompanionFrame(Vector2 UvStart,Vector2 UvEnd,Vector2 Position,Vector2 Size);
    internal static class CompanionGait { public const float DefaultCycleDistance = .45f; }
}
