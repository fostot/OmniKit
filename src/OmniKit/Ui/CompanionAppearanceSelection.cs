namespace OmniKit.Ui;

/// <summary>Only explicit settings belong here. Resolved event themes and timed avatar/look choices are separate inputs.</summary>
internal readonly record struct CompanionAppearanceRequest(string Avatar, string EverydayChoice, bool HolidayEnabled,
    string HolidayBodySelection, string SeasonalThemeSelection)
{
    public string CostumeSelection { get; init; } = "Auto";
}

internal readonly record struct CompanionAppearanceDecision(bool CanApplyBody, bool ManualChange,
    SeasonalCompanionTheme? HolidayTheme);

/// <summary>Manual body choices apply during naps/carry; automatic changes keep the caller's safe transition gate.</summary>
internal sealed class CompanionAppearanceSelection
{
    private CompanionAppearanceRequest? previous;

    // Call once per frame before resolving the everyday look. This does not change
    // motion, nap timing, the saved everyday choice, home art, or accessory choice.
    public CompanionAppearanceDecision Update(CompanionAppearanceRequest request,
        SeasonalCompanionTheme? activeTheme, bool canSwitch)
    {
        var changed = previous is { } old && old != request;
        var apply = previous == null || changed || canSwitch;
        previous = request;
        return new(apply, changed, ResolveHoliday(request.HolidayEnabled, request.HolidayBodySelection, activeTheme));
    }

    public static SeasonalCompanionTheme? ResolveHoliday(bool enabled, string selection, SeasonalCompanionTheme? activeTheme)
    {
        if (!enabled) return null;
        if (selection == "Auto") return activeTheme is { } theme && Enum.IsDefined(theme) ? theme : null;
        return Enum.TryParse<SeasonalCompanionTheme>(selection, out var specific) && Enum.IsDefined(specific)
            && selection == specific.ToString() ? specific : null;
    }

    public void Reset() => previous = null;
}
