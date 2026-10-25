using static OmniKit.Ui.CompanionBodyMaterial;

namespace OmniKit.Ui;

internal enum CompanionBodyMaterial { Species, Gel, Cream, Fluid, Bubble, Cloud, Wisp, Flame, Silk, Crystal, Stone, Metal }

internal readonly record struct CompanionMotionProfile(CompanionBodyMaterial Material,
    float Tempo = 1, float Strength = 1, float Lag = 1, bool Rolls = false);

/// <summary>Reviewed body materials keyed by exact public artwork IDs, never inferred from display labels.</summary>
internal static class CompanionMotionProfiles
{
    private static readonly IReadOnlyDictionary<string, CompanionMotionProfile> Defaults =
        new Dictionary<string, CompanionMotionProfile>(StringComparer.Ordinal)
        {
            ["Moonblob"] = new(Species, Rolls: true),
            ["Wink"] = new(Species), ["Nova"] = new(Species), ["Mochi"] = new(Species),
            ["Puff"] = new(Species), ["Orbit"] = new(Species), ["Ember"] = new(Species),
            ["Prism"] = new(Species), ["Nib"] = new(Species), ["Echo"] = new(Species),
            ["Fizz"] = new(Species), ["Glim"] = new(Species), ["Rune"] = new(Species),
        };
    private static readonly IReadOnlyDictionary<(string Avatar, string LookId), CompanionMotionProfile> Profiles = CreateProfiles();

    /// <summary>The 32 everyday, 72 holiday and 52 painted-costume IDs with explicit profiles.</summary>
    public static IReadOnlyList<(string Avatar, string LookId)> MappedLooks { get; } =
        Array.AsReadOnly(Profiles.Keys.ToArray());

    public static CompanionMotionProfile Resolve(string avatar, string? lookId)
    {
        if (lookId != null && Profiles.TryGetValue((avatar, lookId), out var profile)) return profile;
        // Default, exact wardrobe-none IDs and unknown looks retain approved species motion.
        // Unknown IDs cannot borrow another companion's material or silently stop the blob rolling.
        return avatar != null && Defaults.TryGetValue(avatar, out var fallback) ? fallback : new(Species);
    }

    private static IReadOnlyDictionary<(string Avatar, string LookId), CompanionMotionProfile> CreateProfiles()
    {
        // Materials follow the painted body: ice/gems stay crystalline, metallic bodies stay
        // weighty, dough stays soft, and lantern/petal skins flex gently. Orbit's caller keeps
        // its body, headwear and rings rigid together regardless of the material listed here.
        var profiles = new Dictionary<(string Avatar, string LookId), CompanionMotionProfile>
        {
            [("Moonblob", "OceanOpal")] = new(Fluid, 1.08f, 1.12f, 1.18f, true),
            [("Moonblob", "BerryNebula")] = new(Gel, 0.92f, 1.18f, 1.22f, true),
            [("Moonblob", "HoneydewMoon")] = new(Gel, 1.04f, 1.08f, 1.12f, true),
            [("Moonblob", "LavenderGeode")] = new(Crystal, 0.8f, 0.72f, 0.82f),
            [("Wink", "TideGlass")] = new(Crystal, 0.9f, 0.76f, 0.86f),
            [("Wink", "TwilightVelvet")] = new(Silk, 0.84f, 1.05f, 1.2f),
            [("Wink", "Roselight")] = new(Gel, 1.04f, 1.1f, 1.12f),
            [("Wink", "JadeLantern")] = new(Crystal, 0.78f, 0.7f, 0.8f),
            [("Nova", "SunriseSilk")] = new(Silk, 1.08f, 1.14f, 1.16f),
            [("Nova", "PolarAurora")] = new(Wisp, 0.92f, 1.2f, 1.22f),
            [("Nova", "RoseComet")] = new(Gel, 1.16f, 1.16f, 1.08f),
            [("Nova", "MidnightOpal")] = new(Crystal, 0.86f, 0.78f, 0.86f),
            [("Mochi", "SakuraCream")] = new(Cream, 0.92f, 1.15f, 1.16f),
            [("Mochi", "MatchaMoon")] = new(Cream, 0.82f, 1.05f, 1.22f),
            [("Mochi", "BlackSesame")] = new(Cream, 0.76f, 0.94f, 1.25f),
            [("Mochi", "YuzuGlow")] = new(Cream, 1.1f, 1.2f, 1.02f),
            [("Puff", "PeachParfait")] = new(Cream, 0.9f, 1.1f, 1.16f),
            [("Puff", "AuroraDream")] = new(Wisp, 1.02f, 1.22f, 1.24f),
            [("Puff", "RainPearl")] = new(Bubble, 1.16f, 1.18f, 0.96f),
            [("Puff", "MintMallow")] = new(Cream, 0.82f, 1.16f, 1.24f),
            [("Orbit", "OceanMarble")] = new(Stone, 0.88f, 0.78f, 0.82f),
            [("Orbit", "PeachGiant")] = new(Cloud, 1.02f, 1.12f, 1.14f),
            [("Orbit", "JadeWorld")] = new(Stone, 0.76f, 0.68f, 0.76f),
            [("Orbit", "AmethystSky")] = new(Crystal, 0.9f, 0.84f, 0.9f),
            [("Ember", "Moonfire")] = new(Flame, 1.18f, 1.2f, 1.04f),
            [("Ember", "RoseCinder")] = new(Flame, 0.92f, 1.06f, 1.18f),
            [("Ember", "HoneyStar")] = new(Fluid, 0.82f, 1.12f, 1.26f),
            [("Ember", "VioletWisp")] = new(Wisp, 1.04f, 1.22f, 1.24f),
            [("Prism", "RoseQuartz")] = new(Crystal, 0.92f, 0.84f, 0.94f),
            [("Prism", "Aquamarine")] = new(Crystal, 1.06f, 0.92f, 0.86f),
            [("Prism", "Sunstone")] = new(Crystal, 0.82f, 0.76f, 0.8f),
            [("Prism", "AmethystDream")] = new(Crystal, 0.72f, 0.7f, 0.98f),
            [("Moonblob", "moonblob-allsaintswake")] = new(Gel, 1.02f, 1.16f, 1.2f, true),
            [("Moonblob", "moonblob-starlight")] = new(Crystal, 0.82f, 0.72f, 0.84f),
            [("Moonblob", "moonblob-heavensturn")] = new(Silk, 0.94f, 1.02f, 1.18f),
            [("Moonblob", "moonblob-valentione")] = new(Gel, 0.9f, 1.24f, 1.22f, true),
            [("Moonblob", "moonblob-littleladies")] = new(Cream, 0.86f, 1.16f, 1.2f),
            [("Moonblob", "moonblob-hatchingtide")] = new(Crystal, 0.9f, 0.8f, 0.9f),
            [("Moonblob", "moonblob-makeitrain")] = new(Metal, 0.78f, 0.68f, 0.78f),
            [("Moonblob", "moonblob-moonfirefaire")] = new(Fluid, 1.18f, 1.25f, 1.2f),
            [("Moonblob", "moonblob-therising")] = new(Wisp, 1.08f, 1.22f, 1.24f),
            [("Wink", "wink-allsaintswake")] = new(Wisp, 1.04f, 1.16f, 1.24f),
            [("Wink", "wink-starlight")] = new(Crystal, 0.8f, 0.7f, 0.82f),
            [("Wink", "wink-heavensturn")] = new(Silk, 0.9f, 1.04f, 1.18f),
            [("Wink", "wink-valentione")] = new(Gel, 0.94f, 1.18f, 1.2f),
            [("Wink", "wink-littleladies")] = new(Silk, 1.06f, 1.12f, 1.16f),
            [("Wink", "wink-hatchingtide")] = new(Crystal, 0.92f, 0.84f, 0.94f),
            [("Wink", "wink-makeitrain")] = new(Metal, 0.86f, 0.72f, 0.8f),
            [("Wink", "wink-moonfirefaire")] = new(Fluid, 1.2f, 1.22f, 1.16f),
            [("Wink", "wink-therising")] = new(Wisp, 1.12f, 1.26f, 1.22f),
            [("Nova", "nova-allsaintswake")] = new(Gel, 0.98f, 1.12f, 1.14f),
            [("Nova", "nova-starlight")] = new(Crystal, 0.84f, 0.78f, 0.86f),
            [("Nova", "nova-heavensturn")] = new(Silk, 1.08f, 1.14f, 1.16f),
            [("Nova", "nova-valentione")] = new(Gel, 1.12f, 1.24f, 1.12f),
            [("Nova", "nova-littleladies")] = new(Silk, 0.94f, 1.16f, 1.22f),
            [("Nova", "nova-hatchingtide")] = new(Crystal, 1.02f, 0.9f, 0.94f),
            [("Nova", "nova-makeitrain")] = new(Metal, 0.96f, 0.8f, 0.8f),
            [("Nova", "nova-moonfirefaire")] = new(Fluid, 1.16f, 1.2f, 1.12f),
            [("Nova", "nova-therising")] = new(Wisp, 1.28f, 1.3f, 1.14f),
            [("Mochi", "mochi-allsaintswake")] = new(Cream, 0.82f, 1.08f, 1.24f),
            [("Mochi", "mochi-starlight")] = new(Cream, 0.9f, 1.16f, 1.18f),
            [("Mochi", "mochi-heavensturn")] = new(Cream, 0.78f, 1.04f, 1.26f),
            [("Mochi", "mochi-valentione")] = new(Cream, 1.02f, 1.22f, 1.16f),
            [("Mochi", "mochi-littleladies")] = new(Cream, 0.88f, 1.2f, 1.22f),
            [("Mochi", "mochi-hatchingtide")] = new(Cream, 1.08f, 1.12f, 1.08f),
            [("Mochi", "mochi-makeitrain")] = new(Metal, 0.76f, 0.72f, 0.78f),
            [("Mochi", "mochi-moonfirefaire")] = new(Cream, 1.16f, 1.26f, 1.14f),
            [("Mochi", "mochi-therising")] = new(Cream, 0.96f, 1.14f, 1.2f),
            [("Puff", "puff-allsaintswake")] = new(Wisp, 0.9f, 1.18f, 1.26f),
            [("Puff", "puff-starlight")] = new(Cloud, 0.78f, 1.04f, 1.22f),
            [("Puff", "puff-heavensturn")] = new(Silk, 0.94f, 1.08f, 1.16f),
            [("Puff", "puff-valentione")] = new(Cloud, 1.04f, 1.18f, 1.2f),
            [("Puff", "puff-littleladies")] = new(Cloud, 0.88f, 1.14f, 1.24f),
            [("Puff", "puff-hatchingtide")] = new(Cloud, 1.1f, 1.2f, 1.08f),
            [("Puff", "puff-makeitrain")] = new(Metal, 0.84f, 0.76f, 0.84f),
            [("Puff", "puff-moonfirefaire")] = new(Fluid, 1.14f, 1.26f, 1.22f),
            [("Puff", "puff-therising")] = new(Wisp, 1.06f, 1.3f, 1.26f),
            [("Orbit", "orbit-allsaintswake")] = new(Stone, 0.86f, 0.82f, 0.84f),
            [("Orbit", "orbit-starlight")] = new(Crystal, 0.8f, 0.74f, 0.82f),
            [("Orbit", "orbit-heavensturn")] = new(Silk, 0.98f, 1.08f, 1.16f),
            [("Orbit", "orbit-valentione")] = new(Crystal, 0.94f, 0.88f, 0.98f),
            [("Orbit", "orbit-littleladies")] = new(Silk, 0.88f, 1.02f, 1.22f),
            [("Orbit", "orbit-hatchingtide")] = new(Stone, 0.9f, 0.84f, 0.94f),
            [("Orbit", "orbit-makeitrain")] = new(Metal, 0.76f, 0.68f, 0.76f),
            [("Orbit", "orbit-moonfirefaire")] = new(Fluid, 1.14f, 1.16f, 1.18f),
            [("Orbit", "orbit-therising")] = new(Stone, 0.7f, 0.72f, 0.86f),
            [("Ember", "ember-allsaintswake")] = new(Flame, 1.12f, 1.2f, 1.08f),
            [("Ember", "ember-starlight")] = new(Flame, 0.86f, 0.94f, 1.2f),
            [("Ember", "ember-heavensturn")] = new(Flame, 1.02f, 1.12f, 1.14f),
            [("Ember", "ember-valentione")] = new(Flame, 0.96f, 1.24f, 1.18f),
            [("Ember", "ember-littleladies")] = new(Crystal, 0.88f, 0.84f, 0.92f),
            [("Ember", "ember-hatchingtide")] = new(Crystal, 1.04f, 0.9f, 0.88f),
            [("Ember", "ember-makeitrain")] = new(Metal, 0.82f, 0.76f, 0.84f),
            [("Ember", "ember-moonfirefaire")] = new(Fluid, 1.16f, 1.28f, 1.22f),
            [("Ember", "ember-therising")] = new(Flame, 1.32f, 1.32f, 0.92f),
            [("Prism", "prism-allsaintswake")] = new(Crystal, 0.78f, 0.72f, 1.04f),
            [("Prism", "prism-starlight")] = new(Crystal, 0.72f, 0.64f, 0.8f),
            [("Prism", "prism-heavensturn")] = new(Crystal, 0.96f, 0.88f, 0.88f),
            [("Prism", "prism-valentione")] = new(Crystal, 0.9f, 0.82f, 0.96f),
            [("Prism", "prism-littleladies")] = new(Crystal, 1.02f, 0.9f, 1.04f),
            [("Prism", "prism-hatchingtide")] = new(Crystal, 1.08f, 0.94f, 1.02f),
            [("Prism", "prism-makeitrain")] = new(Crystal, 0.82f, 0.76f, 0.84f),
            [("Prism", "prism-moonfirefaire")] = new(Fluid, 1.12f, 1.18f, 1.2f),
            [("Prism", "prism-therising")] = new(Crystal, 0.86f, 0.86f, 0.94f),
        };
        // These four costumes add clothing to the same body. They inherit species motion;
        // only the pace and strength vary modestly. The generated keys are exact known IDs,
        // not substring parsing or guesses about the user's labels.
        var costumes = new (string Suffix, float Tempo, float Strength)[]
        {
            ("allsaintswake", .96f, 1.06f),
            ("starlight", .9f, .94f),
            ("makeitrain", 1.08f, .96f),
            ("moonfirefaire", 1.12f, 1.08f),
        };
        foreach (var (avatar, species) in Defaults)
        foreach (var costume in costumes)
            profiles.Add((avatar, avatar.ToLowerInvariant() + "-wardrobe-" + costume.Suffix),
                species with { Tempo = costume.Tempo, Strength = costume.Strength });
        return profiles;
    }
}
