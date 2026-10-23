namespace OmniKit.Ui;

/// <summary>Cosmetic random choices are event-driven, never redrawn per frame.</summary>
internal sealed class CompanionStyleSelection(CompanionHomeStyleLibrary library, int? seed = null)
{
    private readonly Random random = seed.HasValue ? new(seed.Value) : new();
    private string? lastHomeSelection;
    private string? lastTheme;
    private bool lastHolidayEnabled;
    private CompanionHomeStyle? home;
    public CompanionHandStyle CurrentHand { get; private set; } = library.GetHand("Default");

    public CompanionHomeStyle ResolveHome(string selection, bool holidayEnabled, string? resolvedTheme, bool canChange = true)
    {
        // A nap may keep its old geometry until the visitor is outside. Do not consume
        // a pending setting/theme change or random choice while that gate is closed.
        if (home != null && (!canChange || lastHomeSelection == selection && lastTheme == resolvedTheme && lastHolidayEnabled == holidayEnabled)) return home;
        lastHomeSelection = selection; lastTheme = resolvedTheme; lastHolidayEnabled = holidayEnabled;
        var seasonalHome = holidayEnabled && resolvedTheme != null
            && CompanionHomeStyleLibrary.HomeIds.Skip(5).Contains(resolvedTheme);
        home = seasonalHome ? library.GetHome(resolvedTheme)
            : selection == "Random" ? library.GetHome(Pick(CompanionHomeStyleLibrary.HomeIds)) : library.GetHome(selection);
        return home;
    }

    // Call on the pickup edge only. Keep CurrentHand through the release animation.
    public CompanionHandStyle BeginPickup(string selection)
        => CurrentHand = selection == "Random" ? library.GetHand(Pick(CompanionHomeStyleLibrary.HandIds)) : library.GetHand(selection);
    public CompanionHandStyle ReadyHand(string selection) => selection == "Random" ? CurrentHand : library.GetHand(selection);

    private string Pick(System.Collections.Immutable.ImmutableArray<string> ids)
    {
        var index = random.Next(ids.Length - 1);
        foreach (var id in ids) if (id != "Random" && index-- == 0) return id;
        return "Default";
    }
}
