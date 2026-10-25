namespace OmniKit.Jobs;

/// <summary>
/// A transient main-hand/tool or soul-crystal observation; never pass armor here.
/// CanEquipTarget includes the native equip and exact target class restrictions.
/// The native caller must revalidate the full item fingerprint before use.
/// </summary>
internal sealed record OwnedJobItem(uint ItemId, int Container, int Slot, int RequiredLevel,
    int ItemLevel, bool HighQuality, bool CanEquipTarget, bool IsSoulCrystal);

internal sealed record JobGearset(int Index, uint ClassJobId, bool Usable, bool MatchesEquipped = false);

internal sealed record JobSwitchContext(uint CurrentJobId, string CurrentJobName,
    uint TargetJobId, string TargetJobName, bool CanSwitch, bool TargetUnlocked, int TargetLevel,
    uint RequiredSoulCrystalId, IReadOnlyList<JobGearset> Gearsets,
    IReadOnlyList<int> FreeGearsetSlots, IReadOnlyList<OwnedJobItem> OwnedItems);

internal sealed record NewJobGearset(int Index, uint ClassJobId, string Name);

/// <summary>Complete preflight policy, with no game calls or mutation of existing sets.</summary>
internal sealed record JobSwitchPlan(bool Allowed, string Reason, int? ExistingGearsetIndex = null,
    NewJobGearset? CurrentSnapshot = null, NewJobGearset? TargetSnapshot = null,
    OwnedJobItem? Weapon = null, OwnedJobItem? SoulCrystal = null, int? RestoreGearsetIndex = null)
{
    public static JobSwitchPlan Build(JobSwitchContext context)
    {
        if (!context.CanSwitch)
            return Blocked("Job changes are paused while the game is busy or unfocused.");
        if (context.CurrentJobId == 0 || context.TargetJobId == 0
            || string.IsNullOrWhiteSpace(context.CurrentJobName)
            || string.IsNullOrWhiteSpace(context.TargetJobName))
            return Blocked("The current class or requested class is unavailable.");
        if (!context.TargetUnlocked || context.TargetLevel <= 0)
            return Blocked("That class or job has not been unlocked.");

        var existing = context.Gearsets.Where(set => set.ClassJobId == context.TargetJobId)
            .OrderByDescending(set => set.Usable).ThenBy(set => set.Index).FirstOrDefault();
        if (existing is { Usable: false })
            return Blocked("The saved gearset has missing equipment. Restore its items before switching.");

        var matchingCurrent = context.Gearsets.Where(set => set.ClassJobId == context.CurrentJobId
                && set.Usable && set.MatchesEquipped)
            .OrderBy(set => set.Index).FirstOrDefault();
        // One saved set for the active class is enough. Do not manufacture a
        // duplicate merely because equipment changed since that set was saved.
        var needsCurrent = !context.Gearsets.Any(set => set.ClassJobId == context.CurrentJobId);
        var sameJob = context.CurrentJobId == context.TargetJobId;
        var occupied = context.Gearsets.Select(set => set.Index).ToHashSet();
        var free = context.FreeGearsetSlots.Where(slot => slot >= 0 && !occupied.Contains(slot))
            .Distinct().OrderBy(slot => slot).ToArray();
        var neededSlots = (needsCurrent ? 1 : 0) + (sameJob || existing != null ? 0 : 1);
        if (free.Length < neededSlots)
            return Blocked($"This change needs {neededSlots} free gearset slots; only {free.Length} are available.");

        var nextSlot = 0;
        var current = needsCurrent
            ? new NewJobGearset(free[nextSlot++], context.CurrentJobId, context.CurrentJobName)
            : null;
        var restoreIndex = matchingCurrent?.Index ?? current?.Index;
        if (sameJob)
            return new(true, current != null ? "Save the current class as a gearset." : "The requested class is already equipped.",
                CurrentSnapshot: current, RestoreGearsetIndex: restoreIndex);
        if (existing != null)
            return new(true, current != null ? "Save the current class, then equip the existing gearset." : "Equip the existing gearset.",
                ExistingGearsetIndex: existing.Index, CurrentSnapshot: current, RestoreGearsetIndex: restoreIndex);

        var weapon = context.OwnedItems.Where(item => ValidItem(item) && !item.IsSoulCrystal
                && item.CanEquipTarget && item.RequiredLevel <= context.TargetLevel)
            .OrderByDescending(item => item.ItemLevel).ThenByDescending(item => item.HighQuality)
            .ThenBy(item => item.Container).ThenBy(item => item.Slot).FirstOrDefault();
        if (weapon == null)
            return Blocked("No owned, equippable main-hand weapon or tool is available for that class.");

        OwnedJobItem? soul = null;
        if (context.RequiredSoulCrystalId != 0)
        {
            soul = context.OwnedItems.Where(item => ValidItem(item) && item.IsSoulCrystal
                    && item.ItemId == context.RequiredSoulCrystalId && item.CanEquipTarget
                    && item.RequiredLevel <= context.TargetLevel)
                .OrderBy(item => item.Container).ThenBy(item => item.Slot).FirstOrDefault();
            if (soul == null)
                return Blocked("The required soul crystal is not available for that job.");
        }

        return new(true, current != null ? "Save the current class, equip the requested class, and save its new gearset."
                : "Equip the requested class and save its new gearset.",
            CurrentSnapshot: current,
            TargetSnapshot: new(free[nextSlot], context.TargetJobId, context.TargetJobName),
            Weapon: weapon, SoulCrystal: soul, RestoreGearsetIndex: restoreIndex);
    }

    private static bool ValidItem(OwnedJobItem item) => item.ItemId != 0
        && item.Container >= 0 && item.Slot >= 0 && item.RequiredLevel >= 0 && item.ItemLevel >= 0;

    private static JobSwitchPlan Blocked(string reason) => new(false, reason);
}
