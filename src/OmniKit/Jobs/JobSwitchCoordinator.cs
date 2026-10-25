namespace OmniKit.Jobs;

/// <summary>Transient observations. Commands never imply completion without a later observation.</summary>
internal readonly record struct JobSwitchObservation(bool SameCharacter, bool CanAct, bool InventoryBusy,
    bool EquipmentAvailable, bool OriginalReady, bool ExpectedEquipment, bool RestoreUnchanged,
    bool TargetUnchanged, bool WeaponEquipped, bool SoulEquipped, bool SoulEmpty, bool TargetReady,
    bool RecommendationReady = false, bool RecommendedComplete = false);

internal interface IJobSwitchRuntime
{
    JobSwitchObservation Observe();
    int CreateSnapshot(NewJobGearset snapshot);
    bool EquipGearset(int index);
    bool MoveWeapon();
    bool MoveSoul();
    bool RemoveSoul();
    int ProvisionalCreate(NewJobGearset target);
    bool BeginRecommended();
    bool ApplyRecommended();
    bool FinalizeTarget(NewJobGearset target);
}

/// <summary>A bounded one-click protocol. Every state issues at most one command, without retries.</summary>
internal sealed class JobSwitchCoordinator(JobSwitchPlan plan, IJobSwitchRuntime runtime, long now)
{
    private enum Stage { Snapshot, Switch, WeaponWait, Soul, SoulWait, ReserveTarget, RecommendStart,
        RecommendReady, RecommendEquip, RecommendWait, Save, ExistingWait, RestoreWait, Done }
    private Stage stage;
    private long deadline = now + 25000;
    private int restore = plan.RestoreGearsetIndex ?? -1;
    private bool changed;
    private bool targetCreated;
    private string failure = "";
    public bool Busy => stage != Stage.Done;
    public bool HasChanged => changed;
    public bool Succeeded { get; private set; }
    public string Status { get; private set; } = plan.Reason;

    public void Update(long now)
    {
        if (!Busy) return;
        var o = runtime.Observe();
        if (!o.SameCharacter) { Done("Class change cancelled because the character or area changed."); return; }
        if (!o.CanAct) { Fail("Class change stopped while the game became busy.",false,o,now); return; }
        if (now > deadline) { Fail("The game did not confirm the class change in time.",stage!=Stage.RestoreWait,o,now); return; }
        if (o.InventoryBusy) return;
        if (!o.EquipmentAvailable) { Fail("Equipment is unavailable.",false,o,now); return; }
        if(stage!=Stage.RestoreWait && !o.ExpectedEquipment)
        { Fail("Equipment changed outside this class request.",false,o,now); return; }
        if(targetCreated && stage!=Stage.RestoreWait && !o.TargetUnchanged)
        { Fail("The new gearset changed outside this request.",false,o,now); return; }
        switch(stage)
        {
            case Stage.Snapshot:
                if(!o.OriginalReady) { Fail("Equipment changed before the snapshot.",false,o,now); return; }
                if(plan.CurrentSnapshot is { } snapshot)
                {
                    var created=runtime.CreateSnapshot(snapshot);
                    if(created!=snapshot.Index) { Fail("Could not preserve the current equipment.",false,o,now); return; }
                    restore=created;
                }
                stage=Stage.Switch;
                return;
            case Stage.Switch:
                if(restore>=0 && !o.RestoreUnchanged) { Fail("The original gearset changed.",false,o,now); return; }
                if(plan.ExistingGearsetIndex is int existing)
                {
                    if(!o.TargetUnchanged) { Fail("The target gearset changed.",false,o,now); return; }
                    changed=true; stage=Stage.ExistingWait;
                    if(!runtime.EquipGearset(existing)) Fail("The game rejected that gearset.",true,o,now);
                    return;
                }
                if(plan.Weapon==null) { Done(plan.CurrentSnapshot!=null ? "Current class gearset saved." : "The requested class is already equipped.",true); return; }
                if(!runtime.MoveWeapon()) { Fail("The selected weapon moved or became unavailable.",false,o,now); return; }
                changed=true; stage=Stage.WeaponWait;
                return;
            case Stage.WeaponWait:
                if(o.WeaponEquipped) stage=Stage.Soul;
                return;
            case Stage.Soul:
                if(plan.SoulCrystal!=null)
                {
                    if(o.SoulEquipped) { stage=Stage.SoulWait; return; }
                    if(!runtime.MoveSoul()) { Fail("The required soul crystal moved or became unavailable.",true,o,now); return; }
                }
                else if(!o.SoulEmpty && !runtime.RemoveSoul())
                { Fail("The soul crystal cannot be safely removed.",true,o,now); return; }
                stage=Stage.SoulWait;
                return;
            case Stage.SoulWait:
                if(o.TargetReady) stage=Stage.ReserveTarget;
                return;
            case Stage.ReserveTarget:
                if(!o.TargetReady) { Fail("The requested class changed before creating its gearset.",true,o,now); return; }
                if(plan.TargetSnapshot is not { } provisional) { Fail("The target gearset plan is unavailable.",true,o,now); return; }
                if(runtime.ProvisionalCreate(provisional)!=provisional.Index)
                { Fail("Could not create the requested class gearset.",true,o,now); return; }
                targetCreated=true; stage=Stage.RecommendStart;
                return;
            case Stage.RecommendStart:
                if(!runtime.BeginRecommended()) { Fail("Recommended Gear could not be prepared.",true,o,now); return; }
                stage=Stage.RecommendReady;
                return;
            case Stage.RecommendReady:
                if(o.RecommendationReady) stage=Stage.RecommendEquip;
                return;
            case Stage.RecommendEquip:
                if(!o.RecommendationReady || !runtime.ApplyRecommended())
                { Fail("Recommended Gear changed or became unavailable.",true,o,now); return; }
                stage=Stage.RecommendWait;
                return;
            case Stage.RecommendWait:
                if(o.RecommendedComplete) stage=Stage.Save;
                return;
            case Stage.Save:
                if(!o.TargetReady || !o.RecommendedComplete) { Fail("The recommended equipment changed before reassignment.",true,o,now); return; }
                if(plan.TargetSnapshot is not { } target) { Fail("The target gearset plan is unavailable.",true,o,now); return; }
                if(!runtime.FinalizeTarget(target)) { Fail("The class changed, but its new gearset could not be reassigned.",true,o,now); return; }
                Done("Recommended gear equipped and new gearset reassigned.",true);
                return;
            case Stage.ExistingWait:
                if(!o.TargetUnchanged) { Fail("The target gearset changed during the request.",true,o,now); return; }
                if(o.TargetReady) Done("Saved gearset equipped.",true);
                return;
            case Stage.RestoreWait:
                if(o.OriginalReady) Done(failure+" Original equipment restored.");
                return;
        }
    }
    private void Fail(string reason,bool canRestore,JobSwitchObservation o,long now)
    {
        // Never restore over manual interference or wait for an unsafe condition to become safe.
        if(canRestore && changed && stage!=Stage.RestoreWait && o.CanAct && o.SameCharacter
            && !o.InventoryBusy && o.EquipmentAvailable && o.ExpectedEquipment && o.RestoreUnchanged && restore>=0)
        {
            failure=reason; stage=Stage.RestoreWait; deadline=now+4000;
            if(runtime.EquipGearset(restore)) { Status=reason+" Restoring original equipment."; return; }
        }
        Done(reason+(changed ? restore>=0 ? $" Restore gearset {restore+1} manually when safe." : " Check your equipment manually when safe." : ""));
    }
    public void Cancel() { if(Busy) Done("Class change cancelled. Any saved snapshot remains available."); }
    private void Done(string message,bool succeeded=false) { Status=message; Succeeded=succeeded; stage=Stage.Done; }
}


