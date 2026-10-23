namespace OmniKit.Ui;

/// <summary>Optional cosmetic rotation. The user's configured choice is never changed.</summary>
internal sealed class CompanionRotation(int? seed = null)
{
    private readonly Random random = seed.HasValue ? new Random(seed.Value) : new Random();
    private string? configured;
    private string actual = Configuration.AvatarNames[0];
    private float interval;
    private long startedAt;
    private long lastTime;
    private bool wasEnabled;

    // now is a monotonic millisecond timestamp, independent of animation/gait time.
    public string Update(string configuredAvatar, bool enabled, float intervalSeconds, long now, bool canSwitch)
    {
        if (!Configuration.AvatarNames.Contains(configuredAvatar)) configuredAvatar = Configuration.AvatarNames[0];
        intervalSeconds = Math.Clamp(float.IsFinite(intervalSeconds) ? intervalSeconds : 300, 30, 3600);
        if (configured != configuredAvatar)
        {
            configured = actual = configuredAvatar;
            startedAt = now;
        }
        else if (interval != intervalSeconds || now < lastTime || enabled && !wasEnabled)
            startedAt = now;
        interval = intervalSeconds;
        lastTime = now;
        wasEnabled = enabled;
        if (!enabled)
        {
            startedAt = now;
            return actual = configuredAvatar;
        }
        // Unsigned subtraction avoids signed overflow over long-lived clocks.
        // A blocked, due change waits without consuming its turn or catching up.
        if (!canSwitch || unchecked((ulong)now - (ulong)startedAt) < (double)interval * 1000)
            return actual;
        var choices = Configuration.AvatarNames;
        var selected = random.Next(choices.Count - 1);
        foreach (var choice in choices)
        {
            if (choice == actual) continue;
            if (selected-- != 0) continue;
            actual = choice;
            break;
        }
        startedAt = now;
        return actual;
    }
}
