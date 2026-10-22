namespace OmniKit.Interaction;

// Pure transition logic: no game services, native pointers, clock, or side effects.
// The service must revalidate state immediately before executing the returned action.
public enum GatheringDecision { None, Interact, Restore, Complete, Cancel }

public readonly record struct GatheringObservation(int Gearset, uint Job, ulong Equipment,
    bool TargetEquipmentMatches, bool OriginalEquipmentMatches, bool Gathering, bool CanAct,
    bool Interrupted, bool TargetExists, bool OriginalSetUnchanged, bool RestoreEnabled);

public sealed class GatheringTransition(int originalSet, uint originalJob, int targetSet,
    uint targetJob, long startedAt, bool restore)
{
    private enum Phase { Equipping, AwaitGathering, Gathering, Restoring, Ended }
    private Phase phase;
    private long deadline = startedAt + 5000;
    private long stableSince = -1;
    private long endedSince = -1;
    private ulong equipment;

    public int OriginalSet => originalSet;
    public int TargetSet => targetSet;
    public bool Restored { get; private set; }
    public string Reason { get; private set; } = "Gathering assistance cancelled.";

    public GatheringDecision Advance(GatheringObservation observation, long now)
    {
        if (phase == Phase.Ended)
            return GatheringDecision.None;
        if (observation.Interrupted)
            return Stop("The saved gearset changed; automatic changes cancelled.");

        if (phase == Phase.Equipping)
        {
            if (!observation.TargetExists || now > deadline)
                return Stop("Gear change timed out or the node disappeared; no retry was made.");
            if ((observation.Gearset != originalSet && observation.Gearset != targetSet)
                || (observation.Job != originalJob && observation.Job != targetJob))
                return Stop("Manual job or gearset change detected; automatic changes cancelled.");
            if (observation.Gearset != targetSet || observation.Job != targetJob
                || !observation.TargetEquipmentMatches)
            {
                stableSince = -1;
                return GatheringDecision.None;
            }
            if (stableSince < 0 || equipment != observation.Equipment)
            {
                stableSince = now;
                equipment = observation.Equipment;
            }
            if (now - stableSince < 250 || !observation.CanAct)
                return GatheringDecision.None;
            phase = Phase.AwaitGathering;
            deadline = now + 8000;
            return GatheringDecision.Interact;
        }

        if (phase == Phase.Restoring)
        {
            if (observation.Gearset == originalSet && observation.Job == originalJob
                && observation.OriginalEquipmentMatches)
            {
                phase = Phase.Ended;
                Restored = true;
                return GatheringDecision.Complete;
            }
            if (now > deadline || (observation.Gearset != targetSet && observation.Gearset != originalSet)
                || (observation.Job != targetJob && observation.Job != originalJob))
                return Stop("Restoration could not be verified; no retry was made.");
            return GatheringDecision.None;
        }

        if (observation.Gearset != targetSet || observation.Job != targetJob || observation.Equipment != equipment)
            return Stop("Manual equipment, job, or gearset change detected; restoration cancelled.");
        if (observation.Gathering)
        {
            phase = Phase.Gathering;
            endedSince = -1;
            return GatheringDecision.None;
        }
        if (phase == Phase.AwaitGathering)
            return now > deadline ? Stop("Gathering did not start; automatic restoration cancelled.") : GatheringDecision.None;

        if (endedSince < 0)
            endedSince = now;
        if (now - endedSince < 500)
            return GatheringDecision.None;
        if (!restore || !observation.RestoreEnabled)
        {
            phase = Phase.Ended;
            return GatheringDecision.Complete;
        }
        if (!observation.OriginalSetUnchanged || now - endedSince > 5000)
            return Stop("Restoration cancelled because the previous set changed or the game remained busy.");
        if (!observation.CanAct)
            return GatheringDecision.None;
        phase = Phase.Restoring;
        deadline = now + 5000;
        return GatheringDecision.Restore;
    }

    private GatheringDecision Stop(string reason)
    {
        phase = Phase.Ended;
        Reason = reason;
        return GatheringDecision.Cancel;
    }
}
