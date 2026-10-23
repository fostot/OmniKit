using System.Collections.Immutable;

namespace OmniKit.Ui;

internal enum SeasonalCompanionTheme
{
    Heavensturn, Valentione, LittleLadies, HatchingTide, MakeItRain,
    MoonfireFaire, TheRising, AllSaintsWake, Starlight,
}

internal enum SeasonalEvidence { Disabled, LoggedOut, Manual, Native, OfficialSchedule, Unavailable }
internal readonly record struct SeasonalFestival(ushort Id, ushort Phase);
internal readonly record struct SeasonalQuestRelation(uint Festival, uint Genre);
internal sealed record SeasonalCompanionSnapshot(SeasonalCompanionTheme? CurrentTheme, SeasonalEvidence Evidence,
    string Status, ImmutableArray<SeasonalCompanionTheme> EligibleThemes, string? OfficialSource = null);

/// <summary>Pure appearance selection. Festival phase is not a year; phase zero is valid.</summary>
internal sealed class SeasonalCompanionPolicy
{
    private readonly Dictionary<ushort, HashSet<SeasonalCompanionTheme>> catalog = [];
    private readonly HashSet<ushort> explicitlyCombined = [113, 145];

    public SeasonalCompanionPolicy(IEnumerable<SeasonalQuestRelation>? localRelations = null)
    {
        // Public Quest.Festival -> JournalGenre relationships, not localized names or guessed offsets.
        Add(238, [5,16,32,44,49,60,78,96,109,128,134,149,157]);
        Add(240, [8,19,33,45,64,82,100,122,135,144,154,163]);
        Add(242, [9,20,34,46,65,83,103,123,138,155,169]);
        Add(244, [10,21,35,47,66,85,104,114,124,136,156,172]);
        Add(245, [36,48,70,86,105,115,125,139,146,161,173]);
        Add(246, [11,22,37,53,71,89,106,116,126,140,150,159,174]);
        Add(247, [11,22,38,54,72,90,107,117,127,141,151,162,175]);
        Add(248, [2,14,28,42,58,76,91,118,132,142,152,164,176]);
        Add(249, [15,31,43,59,77,95,108,121,133,143,153,165,258]);
        Add(241, [113]); Add(243, [145]);
        if (localRelations == null) return;
        foreach (var relation in localRelations)
        {
            if (relation.Festival is 0 or > ushort.MaxValue) continue;
            Add(relation.Genre, [(ushort)relation.Festival]);
        }
    }

    public SeasonalCompanionSnapshot Resolve(bool enabled, string? selection, bool loggedIn, bool nativeAvailable,
        IEnumerable<SeasonalFestival> world, IEnumerable<SeasonalFestival> ui, DateTimeOffset now)
    {
        if (!loggedIn) return Empty(SeasonalEvidence.LoggedOut, "Seasonal looks resume after login.");
        if (!enabled) return Empty(SeasonalEvidence.Disabled, "Seasonal looks off.");
        if (Enum.TryParse<SeasonalCompanionTheme>(selection, out var manual)
            && Enum.IsDefined(manual) && selection == manual.ToString())
            return new(manual, SeasonalEvidence.Manual, "Manual: " + Label(manual), [manual]);
        if (!string.Equals(selection, "Auto", StringComparison.Ordinal))
            return Empty(SeasonalEvidence.Unavailable, "Choose Auto or a seasonal look.");

        if (nativeAvailable)
        {
            var eligible = new HashSet<SeasonalCompanionTheme>();
            var ambiguous = false; var unknown = false;
            var worldThemes = new HashSet<SeasonalCompanionTheme>(); var uiThemes = new HashSet<SeasonalCompanionTheme>();
            Read(world, worldThemes); Read(ui, uiThemes);
            eligible.UnionWith(worldThemes); eligible.UnionWith(uiThemes);
            var ordered = eligible.Order().ToImmutableArray();
            if (ordered.Length > 1)
                return new(null, SeasonalEvidence.Native, "Multiple native festivals; choose a seasonal look.", ordered);
            if (ordered.Length == 1)
            {
                var source = worldThemes.Count > 0 && uiThemes.Count > 0 ? "world + UI" : worldThemes.Count > 0 ? "world" : "UI";
                return new(ordered[0], SeasonalEvidence.Native, $"Native {source}: {Label(ordered[0])}"
                    + (ambiguous || unknown ? " (other festival IDs unresolved)." : "."), ordered);
            }
            return Empty(SeasonalEvidence.Native, ambiguous ? "Native festival is ambiguous; choose a look."
                : unknown ? "Native festival has no supported seasonal look." : "No native seasonal festival.");

            void Read(IEnumerable<SeasonalFestival> slots, HashSet<SeasonalCompanionTheme> destination)
            {
                foreach (var slot in slots)
                {
                    if (slot.Id == 0) continue;
                    if (!catalog.TryGetValue(slot.Id, out var values)) { unknown = true; continue; }
                    // Old Moonfire/Rising shared IDs and any conflicting new relationships remain ambiguous.
                    if (slot.Id is 11 or 22 || (values.Count != 1 && !explicitlyCombined.Contains(slot.Id))) { ambiguous = true; continue; }
                    destination.UnionWith(values);
                }
            }
        }

        // Explicit verified one-year windows only. Never replace a confirmed empty/unknown native state.
        foreach (var window in OfficialWindows)
            if (now >= window.Start && now < window.End)
                return new(window.Theme, SeasonalEvidence.OfficialSchedule,
                    $"Official 2026 schedule: {Label(window.Theme)} (native unavailable).", [window.Theme], window.Source);
        return Empty(SeasonalEvidence.Unavailable, "Native festival unavailable; no verified schedule is active.");
    }

    public static string Label(SeasonalCompanionTheme theme) => theme switch
    {
        SeasonalCompanionTheme.Valentione => "Valentione's Day", SeasonalCompanionTheme.LittleLadies => "Little Ladies' Day",
        SeasonalCompanionTheme.HatchingTide => "Hatching-tide", SeasonalCompanionTheme.MakeItRain => "Make It Rain",
        SeasonalCompanionTheme.MoonfireFaire => "Moonfire Faire", SeasonalCompanionTheme.TheRising => "The Rising",
        SeasonalCompanionTheme.AllSaintsWake => "All Saints' Wake", SeasonalCompanionTheme.Starlight => "Starlight Celebration",
        _ => "Heavensturn",
    };

    private static SeasonalCompanionSnapshot Empty(SeasonalEvidence evidence, string status) => new(null, evidence, status, []);
    private void Add(uint genre, ushort[] ids)
    {
        SeasonalCompanionTheme[] themes = genre switch
        {
            238 => [SeasonalCompanionTheme.Heavensturn], 240 => [SeasonalCompanionTheme.Valentione],
            242 => [SeasonalCompanionTheme.LittleLadies], 244 => [SeasonalCompanionTheme.HatchingTide],
            245 => [SeasonalCompanionTheme.MakeItRain], 246 => [SeasonalCompanionTheme.MoonfireFaire],
            247 => [SeasonalCompanionTheme.TheRising], 248 => [SeasonalCompanionTheme.AllSaintsWake],
            249 => [SeasonalCompanionTheme.Starlight],
            241 => [SeasonalCompanionTheme.Valentione, SeasonalCompanionTheme.LittleLadies],
            243 => [SeasonalCompanionTheme.LittleLadies, SeasonalCompanionTheme.HatchingTide], _ => [],
        };
        if (themes.Length == 0) return;
        foreach (var id in ids)
        {
            if (!catalog.TryGetValue(id, out var values)) catalog[id] = values = [];
            values.UnionWith(themes);
            if (genre is 241 or 243) explicitlyCombined.Add(id);
        }
    }

    internal readonly record struct Window(SeasonalCompanionTheme Theme, DateTimeOffset Start, DateTimeOffset End, string Source);
    // Official final minute is included by storing the next minute as exclusive. No unverified Starlight date.
    internal static readonly ImmutableArray<Window> OfficialWindows =
    [
        new(SeasonalCompanionTheme.Heavensturn, Utc(2025,12,31,15), Utc(2026,1,15,15), "https://na.finalfantasyxiv.com/lodestone/special/2026/Heavensturn/xqnas3t3m1"),
        new(SeasonalCompanionTheme.Valentione, Utc(2026,2,2,8), Utc(2026,2,16,15), "https://eu.finalfantasyxiv.com/lodestone/special/2026/Valentiones_Day/wenbvnlh6s"),
        new(SeasonalCompanionTheme.LittleLadies, Utc(2026,2,26,8), Utc(2026,3,12,15), "https://na.finalfantasyxiv.com/lodestone/special/2026/Little_Ladies_Day/ivf88rc2gm"),
        new(SeasonalCompanionTheme.HatchingTide, Utc(2026,3,23,8), Utc(2026,4,6,15), "https://na.finalfantasyxiv.com/lodestone/special/2026/Hatching_tide/wgtbl3kgc5"),
        new(SeasonalCompanionTheme.MakeItRain, Utc(2026,5,29,8), Utc(2026,6,24,15), "https://na.finalfantasyxiv.com/lodestone/special/2026/the_make_it_rain_campaign/NX0yR0viwE"),
        new(SeasonalCompanionTheme.MoonfireFaire, Utc(2026,8,12,8), Utc(2026,8,26,15), "https://na.finalfantasyxiv.com/blog/003876.html"),
        new(SeasonalCompanionTheme.TheRising, Utc(2026,8,27,7), Utc(2026,9,10,15), "https://na.finalfantasyxiv.com/blog/003893.html"),
        new(SeasonalCompanionTheme.AllSaintsWake, Utc(2026,10,15,8), Utc(2026,11,2,15), "https://na.finalfantasyxiv.com/lodestone/special/2026/All_Saints_Wake/tfrx2rkv52"),
    ];
    private static DateTimeOffset Utc(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0, TimeSpan.Zero);
}
