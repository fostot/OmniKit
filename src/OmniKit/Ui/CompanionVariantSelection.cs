namespace OmniKit.Ui;

/// <summary>Caller-driven choice only. No timers or background work; random includes the original base look.</summary>
internal sealed class CompanionVariantSelection(CompanionVariantLibrary library, Func<int, int>? choose = null)
{
    private readonly Func<int, int> choose = choose ?? Random.Shared.Next;
    private string choice = "";
    private double selectedAt;
    public string Avatar { get; private set; } = "";
    public CompanionVariantLook? Current { get; private set; }

    public CompanionVariantLook? Resolve(string avatar, string requestedChoice, bool autoRotate, float intervalSeconds,
        double nowSeconds, bool canSwitch)
    {
        if (!double.IsFinite(nowSeconds) || !canSwitch) return Current;
        requestedChoice = requestedChoice is "Default" or "Random" or "Look1" or "Look2" or "Look3" or "Look4" ? requestedChoice : "Default";
        intervalSeconds = float.IsFinite(intervalSeconds) ? Math.Clamp(intervalSeconds, 30, 3600) : 300;
        var changed = Avatar != avatar || choice != requestedChoice;
        if (nowSeconds < selectedAt) selectedAt = nowSeconds;
        var due = !changed && requestedChoice == "Random" && autoRotate && nowSeconds - selectedAt >= intervalSeconds;
        if (!changed && !due) return Current;
        var previous = Avatar == avatar && Current != null ? int.Parse(Current.Choice.AsSpan(4)) : 0;
        Avatar = avatar; choice = requestedChoice; selectedAt = nowSeconds;
        if (requestedChoice == "Random")
        {
            // Due rotations choose one of the other four looks; the original base is index zero.
            var index = due ? (previous + 1 + Math.Clamp(choose(4), 0, 3)) % 5 : Math.Clamp(choose(5), 0, 4);
            Current = index == 0 ? null : library.Find(avatar, "Look" + index);
        }
        else Current = requestedChoice == "Default" ? null : library.Find(avatar, requestedChoice);
        return Current;
    }
    public void Reset() { Avatar = ""; choice = ""; Current = null; selectedAt = 0; }
}
