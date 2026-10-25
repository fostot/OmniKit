using System.Numerics;
namespace OmniKit
{
    internal static class Configuration
    {
        public static readonly string[] AvatarNames = ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism"];
    }
}
namespace OmniKit.Ui
{
    internal enum SeasonalCompanionTheme { Heavensturn, Valentione, LittleLadies, HatchingTide, MakeItRain, MoonfireFaire, TheRising, AllSaintsWake, Starlight }
    internal readonly record struct CompanionFrame(Vector2 Position, Vector2 Size);
}
