using System.Numerics;

namespace OmniKit
{
    internal static class Configuration
    {
        internal static string[] AvatarNames => ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism"];
        internal static string[] OutfitNames => ["Navigator", "Stargazer"];
    }
}
namespace OmniKit.Ui
{
    internal readonly record struct CompanionFrame(Vector2 UvStart, Vector2 UvEnd, Vector2 Position, Vector2 Size);
}
