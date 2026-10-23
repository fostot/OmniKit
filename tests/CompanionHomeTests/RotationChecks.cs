using OmniKit;
using OmniKit.Ui;

internal static class RotationChecks
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool passed, string reason) { if (!passed) throw new InvalidOperationException(reason); checks++; }
        var config = new Configuration { Avatar = "Wink" };
        var rotation = new CompanionRotation(17);
        Check(rotation.Update(config.Avatar, false, 30, 0, true) == "Wink", "Disabled rotation shows the configured companion.");
        Check(rotation.Update(config.Avatar, true, 30, 10, true) == "Wink"
            && rotation.Update(config.Avatar, true, 30, 30009, true) == "Wink", "Enabling starts a complete interval without an immediate switch.");
        var next = rotation.Update(config.Avatar, true, 30, 30010, true);
        Check(next != "Wink" && Configuration.AvatarNames.Contains(next) && config.Avatar == "Wink",
            "A due rotation chooses another supported companion without overwriting the preference.");
        Check(rotation.Update(config.Avatar, true, 30, 60010, false) == next
            && rotation.Update(config.Avatar, true, 30, 180010, false) == next, "Due changes wait while carry, naps or preview prevent switching.");
        var resumed = rotation.Update(config.Avatar, true, 30, 180011, true);
        Check(resumed != next && rotation.Update(config.Avatar, true, 30, 180012, true) == resumed,
            "Unblocking performs one change and starts a fresh timer, without a catch-up burst.");
        Check(rotation.Update(config.Avatar, false, 30, 180013, false) == "Wink", "Disabling immediately restores the chosen companion, even while blocked.");
        rotation.Update(config.Avatar, true, 30, 180014, true);
        var prior = rotation.Update(config.Avatar, true, 30, 210014, true);
        Check(rotation.Update(config.Avatar, true, 60, 210015, true) == prior
            && rotation.Update(config.Avatar, true, 60, 270014, true) == prior,
            "Changing the interval retains the current display and restarts its full timer.");
        Check(rotation.Update(config.Avatar, true, 60, 270015, true) != prior, "The updated interval expires at its new deadline.");
        Check(rotation.Update("Nova", true, 60, 270016, false) == "Nova"
            && rotation.Update("Nova", true, 60, 330015, true) == "Nova", "An explicit manual avatar change applies immediately and resets the timer.");

        foreach (var (input, milliseconds) in new[] { (0f, 30000L), (4000f, 3600000L),
            (float.NaN, 300000L), (float.PositiveInfinity, 300000L), (float.NegativeInfinity, 300000L) })
        {
            var bounded = new CompanionRotation(9);
            bounded.Update("Moonblob", true, input, 0, true);
            Check(bounded.Update("Moonblob", true, input, milliseconds - 1, true) == "Moonblob"
                && bounded.Update("Moonblob", true, input, milliseconds, true) != "Moonblob", "Invalid or out-of-range intervals normalize to the supported timing.");
        }
        var rollback = new CompanionRotation(19);
        rollback.Update("Wink", true, 30, 10000, true);
        rollback.Update("Wink", true, 30, 5000, true);
        Check(rollback.Update("Wink", true, 30, 34999, true) == "Wink"
            && rollback.Update("Wink", true, 30, 35000, true) != "Wink", "Clock rollback restarts a full interval instead of underflowing.");
        var nearLimit = new CompanionRotation(19);
        nearLimit.Update("Wink", true, 30, long.MaxValue - 50000, true);
        Check(nearLimit.Update("Wink", true, 30, long.MaxValue - 20001, true) == "Wink"
            && nearLimit.Update("Wink", true, 30, long.MaxValue - 20000, true) != "Wink", "Large timestamps preserve exact elapsed milliseconds.");
        var a = new CompanionRotation(23); var b = new CompanionRotation(23);
        var current = a.Update("Mochi", true, 30, 0, true); b.Update("Mochi", true, 30, 0, true);
        var visited = new HashSet<string> { current };
        for (var i = 1; i <= 64; i++)
        {
            var chosen = a.Update("Mochi", true, 30, i * 30000L, true);
            Check(chosen != current && Configuration.AvatarNames.Contains(chosen)
                && chosen == b.Update("Mochi", true, 30, i * 30000L, true), "A deterministic fixture covers valid random selection with no immediate repeat.");
            visited.Add(chosen); current = chosen;
        }
        Check(visited.SetEquals(Configuration.AvatarNames), "The fixed fixture visits all eight supported companions.");
        Check(new CompanionRotation(1).Update("Unknown", false, 30, 0, true) == Configuration.AvatarNames[0],
            "A malformed caller selection falls back to the first supported companion.");
        return checks;
    }
}
