using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace OmniKit.Jobs;

/// <summary>One explicit request, finite observation windows, and no automatic retries.</summary>
internal sealed unsafe class JobSwitchService(Configuration config) : IDisposable, IJobSwitchRuntime
{
    private sealed record ItemIdentity(uint Id, string Detail, string Instance);
    private sealed record SetIdentity(uint Job, string Name, string Equipment, string ItemIds, byte GlamourLink);
    private sealed class Request
    {
        internal required JobSwitchPlan Plan;
        internal required ulong Player;
        internal required ulong ContentId;
        internal required uint Territory;
        internal required uint OriginalJob;
        internal required uint TargetJob;
        internal required ItemIdentity[] Original;
        internal required Dictionary<(int, int), ItemIdentity> Candidates;
        internal required Dictionary<(int, int), ItemIdentity> Inventory;
        internal int SelectionPrior;
        internal int SelectionIntended;
        internal int Restore = -1;
        internal SetIdentity? RestoreIdentity;
        internal SetIdentity? TargetIdentity;
        internal int EmptySoulSlot = -1;
    }
    private Request? pending;
    private JobSwitchCoordinator? coordinator;
    private long nextRequest;
    public bool Busy => coordinator?.Busy == true;
    public Action<string,bool>? Feedback { get; set; }
    public Func<bool>? ExternalBusy { get; set; }
    public string Status { get; private set; } = "Click a class or job to switch.";

    public bool TryRequest(uint targetJob)
    {
        if (Busy) { Status = "A class change is already in progress."; return false; }
        if (ExternalBusy?.Invoke()==true) return Reject("Wait for the current item action to finish before changing classes.");
        if (Environment.TickCount64 < nextRequest || !CanAct()) return Reject("Job changes are paused while the game is busy or unfocused.");
        var player = Services.Objects.LocalPlayer;
        var native = PlayerState.Instance();
        var sets = RaptureGearsetModule.Instance();
        if (player == null || native == null || !native->IsLoaded || sets == null || InventoryBusy() || !ReadEquipment(out var original)
            || !Services.Data.GetExcelSheet<ClassJob>().TryGetRow(targetJob, out var target)
            || !Services.Data.GetExcelSheet<ClassJob>().TryGetRow(player.ClassJob.RowId, out var current))
            return Reject("The character or class data is unavailable.");
        var level = native->GetClassJobLevel((int)targetJob, false);
        var unlocked = level > 0 && (target.UnlockQuest.RowId == 0 || QuestManager.IsQuestComplete(target.UnlockQuest.RowId));
        var foundSets = new List<JobGearset>();
        var slotFlags = new byte[sets->Entries.Length];
        for (var i = 0; i < sets->Entries.Length; i++)
        {
            var set = sets->Entries[i];
            slotFlags[i]=(byte)set.Flags;
            if ((set.Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0) continue;
            var identity = ReadSet(i);
            // A linked plate could alter the outfit while restoring; preserve a fresh snapshot instead.
            foundSets.Add(new(i, set.ClassJob, identity != null, set.GlamourSetLink == 0
                && identity?.Equipment == EquipmentKey(original)));
        }
        var free=JobGearsetCodec.FreeSlots(slotFlags,InventoryManager.Instance()->GetPermittedGearsetCount(),sets->FirstEmptyGearsetSlot());
        var candidates = new Dictionary<(int,int), ItemIdentity>();
        var items = new List<OwnedJobItem>();
        foreach (var type in new[]{InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3,
                     InventoryType.Inventory4, InventoryType.ArmoryMainHand, InventoryType.ArmorySoulCrystal, InventoryType.EquippedItems})
        {
            var bag = InventoryManager.Instance()->GetInventoryContainer(type);
            if (bag == null || !bag->IsLoaded) continue;
            for (var slot = 0; slot < bag->Size; slot++)
            {
                if (type == InventoryType.EquippedItems && slot != 0 && slot != 13) continue;
                var item = bag->GetInventorySlot(slot);
                if (item == null || item->IsSymbolic || item->ItemId == 0 || item->Quantity != 1
                    || !Services.Data.GetExcelSheet<Item>().TryGetRow(item->ItemId, out var row)
                    || !row.EquipSlotCategory.IsValid) continue;
                var category = row.EquipSlotCategory.Value;
                if (category.MainHand != 1 && category.SoulCrystal != 1) continue;
                var valid = InventoryManager.CanEquip(row.RowId, native->Race, native->Sex, level,
                    (byte)targetJob, native->GrandCompany, 0, null) == 0;
                var key = ((int)type, slot);
                candidates[key] = ReadItem(item);
                items.Add(new(row.RowId, (int)type, slot, row.LevelEquip, (int)row.LevelItem.RowId,
                    (item->Flags & InventoryItem.ItemFlags.HighQuality) != 0, valid, category.SoulCrystal == 1));
            }
        }
        var plan = JobSwitchPlan.Build(new(current.RowId, current.Name.ExtractText(), targetJob, target.Name.ExtractText(),
            true, unlocked, level, target.ItemSoulCrystal.RowId, foundSets, free, items));
        if (!plan.Allowed) return Reject(plan.Reason);
        var request = new Request { Plan = plan, Player = player.GameObjectId, ContentId = native->ContentId, Territory = Services.ClientState.TerritoryType,
            OriginalJob = current.RowId, TargetJob = targetJob, Original = original, Candidates = candidates,
            Inventory = ReadOwnedInventory(), SelectionPrior = sets->CurrentGearsetIndex, SelectionIntended = sets->CurrentGearsetIndex,
            Restore = plan.RestoreGearsetIndex ?? -1 };
        if (plan.CurrentSnapshot == null) request.RestoreIdentity = ReadSet(request.Restore);
        if (plan.ExistingGearsetIndex is int existing) request.TargetIdentity = ReadSet(existing);
        if (plan.TargetSnapshot != null && target.ItemSoulCrystal.RowId == 0 && original[13].Id != 0)
        {
            request.EmptySoulSlot = FindEmptySoulSlot();
            if (request.EmptySoulSlot < 0) return Reject("Make room in the soul crystal Armoury Chest before switching classes.");
        }
        pending = request;
        coordinator = new JobSwitchCoordinator(plan,this,Environment.TickCount64);
        Status = plan.Reason;
        return true;
    }

    public void Update()
    {
        if(coordinator==null || !coordinator.Busy) return;
        coordinator.Update(Environment.TickCount64);
        Status=coordinator.Status;
        if(!coordinator.Busy)
        { pending=null; nextRequest=Environment.TickCount64+750; NotifyFeedback(!coordinator.Succeeded); }
    }
    JobSwitchObservation IJobSwitchRuntime.Observe()
    {
        var r=pending!;
        var same=SameCharacter(r);
        var can=same && CanAct();
        if(!same || !can) return new(same,can,false,false,false,false,false,false,false,false,false,false);
        var busy=InventoryBusy();
        if(busy) return new(true,true,true,false,false,false,false,false,false,false,false,false);
        var available=ReadEquipment(out var equipped);
        if(!available) return new(true,true,busy,false,false,false,false,false,false,false,false,false);
        var original=Services.Objects.LocalPlayer!.ClassJob.RowId==r.OriginalJob && EquipmentKey(equipped)==EquipmentKey(r.Original);
        var module=RaptureGearsetModule.Instance();
        var expected=module!=null && JobGearsetCodec.SelectionTransitionAllowed(module->CurrentGearsetIndex,r.SelectionPrior,r.SelectionIntended)
            && ExpectedEquipment(r,equipped) && ExpectedInventory(r);
        var restore=r.RestoreIdentity!=null && ReadSet(r.Restore)==r.RestoreIdentity;
        var targetUnchanged=r.Plan.ExistingGearsetIndex is not int existing || ReadSet(existing)==r.TargetIdentity;
        var weapon=r.Plan.Weapon is { } w && equipped[0]==r.Candidates[(w.Container,w.Slot)];
        var soul=r.Plan.SoulCrystal is { } stone && equipped[13]==r.Candidates[(stone.Container,stone.Slot)];
        var ready=r.Plan.ExistingGearsetIndex!=null
            ? Services.Objects.LocalPlayer!.ClassJob.RowId==r.TargetJob && r.TargetIdentity!=null
                && (r.TargetIdentity.GlamourLink==0 ? EquipmentKey(equipped)==r.TargetIdentity.Equipment
                    : string.Join('|',equipped.Select(item=>item.Id))==r.TargetIdentity.ItemIds)
            : r.Plan.Weapon!=null && TargetReady(r,equipped);
        return new(true,true,busy,true,original,expected,restore,targetUnchanged,weapon,soul,equipped[13].Id==0,ready);
    }
    int IJobSwitchRuntime.CreateSnapshot(NewJobGearset snapshot)
    {
        var r=pending!;
        var module=RaptureGearsetModule.Instance();
        if(!CanAct() || module==null || !JobGearsetCodec.SelectionTransitionAllowed(module->CurrentGearsetIndex,r.SelectionPrior,r.SelectionIntended)
            || !ReadEquipment(out var equipment)) return 255;
        var prior=module->CurrentGearsetIndex;
        if(!CreateSnapshot(snapshot,equipment,out var created)
            || !JobGearsetCodec.SelectionTransitionAllowed(module->CurrentGearsetIndex,prior,created)) return 255;
        r.SelectionPrior=r.SelectionIntended=module->CurrentGearsetIndex;
        if(snapshot==pending!.Plan.CurrentSnapshot)
        { pending.Restore=created; pending.RestoreIdentity=ReadSet(created); }
        return created;
    }
    bool IJobSwitchRuntime.EquipGearset(int index)
    {
        var r=pending!;
        var module=RaptureGearsetModule.Instance();
        if(!CanAct() || module==null || !JobGearsetCodec.SelectionTransitionAllowed(module->CurrentGearsetIndex,r.SelectionPrior,r.SelectionIntended)) return false;
        r.SelectionPrior=module->CurrentGearsetIndex;
        r.SelectionIntended=index;
        return module->EquipGearset(index)==0;
    }
    bool IJobSwitchRuntime.MoveWeapon()=>CanAct() && pending?.Plan.Weapon is { } weapon && MoveCandidate(pending,weapon,0);
    bool IJobSwitchRuntime.MoveSoul()=>CanAct() && pending?.Plan.SoulCrystal is { } soul && MoveCandidate(pending,soul,13);
    bool IJobSwitchRuntime.RemoveSoul()
    {
        var r=pending!;
        if(!CanAct() || r.EmptySoulSlot<0 || !ReadEquipment(out var equipped) || equipped[13]!=r.Original[13]) return false;
        var inventory=InventoryManager.Instance();
        var destination=inventory->GetInventorySlot(InventoryType.ArmorySoulCrystal,r.EmptySoulSlot);
        if(destination==null || destination->ItemId!=0) return false;
        inventory->MoveItemSlot(InventoryType.EquippedItems,13,InventoryType.ArmorySoulCrystal,(ushort)r.EmptySoulSlot,true);
        return true;
    }
    private bool CanAct()
    {
        if (!config.CharacterJobSwitcherEnabled || !Services.Framework.IsInFrameworkUpdateThread || !Services.ClientState.IsLoggedIn
            || Services.ClientState.IsGPosing || Services.GameGui.GameUiHidden || Services.Objects.LocalPlayer == null) return false;
        foreach (var flag in Blockers) if (Services.Conditions[flag]) return false;
        var framework = NativeFramework.Instance();
        var ui = RaptureAtkModule.Instance();
        return framework != null && !framework->WindowInactive && !framework->IsExiting && ui != null && !ui->IsTextInputActive();
    }
    private static readonly ConditionFlag[] Blockers = [ConditionFlag.Unconscious,ConditionFlag.InCombat,
        ConditionFlag.Casting,ConditionFlag.Casting87,ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51,
        ConditionFlag.WatchingCutscene,ConditionFlag.WatchingCutscene78,ConditionFlag.Occupied,ConditionFlag.Occupied30,
        ConditionFlag.Occupied33,ConditionFlag.Occupied38,ConditionFlag.Occupied39,ConditionFlag.OccupiedInEvent,
        ConditionFlag.OccupiedInQuestEvent,ConditionFlag.OccupiedInCutSceneEvent,ConditionFlag.OccupiedSummoningBell,
        ConditionFlag.Mounted,ConditionFlag.Mounting,ConditionFlag.Mounting71,ConditionFlag.MountOrOrnamentTransition,
        ConditionFlag.InFlight,ConditionFlag.RidingPillion,ConditionFlag.Jumping,ConditionFlag.Jumping61,
        ConditionFlag.Crafting,ConditionFlag.ExecutingCraftingAction,ConditionFlag.PreparingToCraft,
        ConditionFlag.Gathering,ConditionFlag.ExecutingGatheringAction];
    private static bool SameCharacter(Request r) => Services.ClientState.IsLoggedIn
        && Services.Objects.LocalPlayer?.GameObjectId == r.Player && PlayerState.Instance()!=null
        && PlayerState.Instance()->ContentId == r.ContentId
        && Services.ClientState.TerritoryType == r.Territory;
    private static bool InventoryBusy()
    {
        var inventory = InventoryManager.Instance();
        if (inventory == null) return true;
        foreach (var operation in inventory->PendingOperations) if (!operation.IsEmpty) return true;
        return false;
    }
    private static bool MoveCandidate(Request r, OwnedJobItem candidate, ushort slot)
    {
        var manager = InventoryManager.Instance();
        var item = manager->GetInventorySlot((InventoryType)candidate.Container,candidate.Slot);
        if (item == null || item->IsSymbolic || ReadItem(item) != r.Candidates[(candidate.Container,candidate.Slot)]) return false;
        var player=PlayerState.Instance();
        if(player==null || !player->IsLoaded || !Services.Data.GetExcelSheet<ClassJob>().TryGetRow(r.TargetJob,out var target)
            || target.UnlockQuest.RowId!=0 && !QuestManager.IsQuestComplete(target.UnlockQuest.RowId)
            || InventoryManager.CanEquip(item->ItemId,player->Race,player->Sex,player->GetClassJobLevel((int)r.TargetJob,false),
                (byte)r.TargetJob,player->GrandCompany,0,null)!=0) return false;
        if (candidate.Container == (int)InventoryType.EquippedItems && candidate.Slot == slot) return true;
        // One request; completion is determined only by physical equipment observations.
        manager->MoveItemSlot((InventoryType)candidate.Container,(ushort)candidate.Slot,InventoryType.EquippedItems,slot,true);
        return true;
    }
    private static Dictionary<(int,int),ItemIdentity> ReadOwnedInventory()
    {
        var result=new Dictionary<(int,int),ItemIdentity>();
        var manager=InventoryManager.Instance();
        foreach(var type in Enum.GetValues<InventoryType>().Where(type=>type is InventoryType.Inventory1 or InventoryType.Inventory2
            or InventoryType.Inventory3 or InventoryType.Inventory4 || type.ToString().StartsWith("Armory",StringComparison.Ordinal)))
        {
            var bag=manager->GetInventoryContainer(type);
            if(bag==null || !bag->IsLoaded) continue;
            for(var i=0;i<bag->Size;i++) { var item=bag->GetInventorySlot(i); if(item!=null && !item->IsSymbolic) result[((int)type,i)]=ReadItem(item); }
        }
        return result;
    }
    private bool ExpectedInventory(Request r)
    {
        var now=ReadOwnedInventory();
        if(now.Count!=r.Inventory.Count) return false;
        foreach(var (key,previous) in r.Inventory)
        {
            if(!now.TryGetValue(key,out var current)) return false;
            if(previous==current) continue;
            if(coordinator?.HasChanged!=true) return false;
            // Only selected sources and vacated slots receiving original equipment may change.
            var selected=r.Plan.Weapon is { } weapon && key==(weapon.Container,weapon.Slot)
                || r.Plan.SoulCrystal is { } soul && key==(soul.Container,soul.Slot);
            if(r.Plan.ExistingGearsetIndex!=null)
            {
                // Existing native gearsets may replace every equipment slot.
                selected=r.TargetIdentity!.Equipment.Split('|').Contains(previous.Detail,StringComparer.Ordinal);
            }
            if(!selected && previous.Id!=0) return false;
            if(current.Id!=0 && !r.Original.Contains(current)) return false;
        }
        return true;
    }
    private bool ExpectedEquipment(Request r, ItemIdentity[] equipped)
    {
        if (coordinator?.HasChanged!=true) return EquipmentKey(equipped) == EquipmentKey(r.Original);
        if (r.Plan.ExistingGearsetIndex != null)
            return r.TargetIdentity!=null && JobGearsetCodec.EquipmentTransitionAllowed(
                r.Original.Select(item=>item.Detail).ToArray(),r.TargetIdentity.Equipment.Split('|'),equipped.Select(item=>item.Detail).ToArray());
        for (var i=0;i<14;i++)
        {
            if (equipped[i] == r.Original[i]) continue;
            if (i==0 && r.Plan.Weapon is { } weapon && equipped[i]==r.Candidates[(weapon.Container,weapon.Slot)]) continue;
            if (i==13 && r.Plan.SoulCrystal is { } soul && equipped[i]==r.Candidates[(soul.Container,soul.Slot)]) continue;
            // A weapon change can remove incompatible equipment and its old soul.
            if (i!=0 && equipped[i].Id==0) continue;
            return false;
        }
        return true;
    }
    private static bool TargetReady(Request r, ItemIdentity[] equipped) => Services.Objects.LocalPlayer!.ClassJob.RowId == r.TargetJob
        && equipped[0] == r.Candidates[(r.Plan.Weapon!.Container,r.Plan.Weapon.Slot)]
        && (r.Plan.SoulCrystal is { } soul ? equipped[13] == r.Candidates[(soul.Container,soul.Slot)] : equipped[13].Id == 0);
    private static int FindEmptySoulSlot()
    {
        var bag = InventoryManager.Instance()->GetInventoryContainer(InventoryType.ArmorySoulCrystal);
        if (bag == null || !bag->IsLoaded) return -1;
        for(var i=0;i<bag->Size;i++) { var item=bag->GetInventorySlot(i); if(item != null && item->ItemId==0) return i; }
        return -1;
    }
    private static bool CreateSnapshot(NewJobGearset snapshot, ItemIdentity[] equipped, out int created)
    {
        created=-1;
        var sets=RaptureGearsetModule.Instance();
        var agent=AgentGearSet.Instance();
        if (sets==null || agent==null || Services.Objects.LocalPlayer!.ClassJob.RowId != snapshot.ClassJobId
            || snapshot.Index<0 || snapshot.Index>=sets->Entries.Length) return false;

        var flags=new byte[sets->Entries.Length];
        flags[snapshot.Index]=(byte)sets->Entries[snapshot.Index].Flags;
        if(!JobGearsetCodec.CanCreateAt(snapshot.Index,sets->FirstEmptyGearsetSlot(),flags)) return false;
        var index=sets->CreateGearset();
        if(index!=snapshot.Index) return false;
        created=index;
        if(!agent->RenameGearset(index,snapshot.Name)) return false;
        sets->SaveFile(true);
        var saved=ReadSet(index);
        return saved != null && saved.Job==snapshot.ClassJobId && saved.Name==snapshot.Name && saved.Equipment==EquipmentKey(equipped);
    }
    private static bool SetUsable(RaptureGearsetModule.GearsetEntry* set)
    {
        if((set->Flags & RaptureGearsetModule.GearsetFlag.MainHandMissing)!=0) return false;
        foreach(var item in set->Items) if((item.Flags & RaptureGearsetModule.GearsetItemFlag.ItemMissing)!=0) return false;
        return true;
    }
    private static SetIdentity? ReadSet(int index)
    {
        var sets=RaptureGearsetModule.Instance();
        if(sets==null || index<0 || index>=sets->Entries.Length || !sets->IsValidGearset(index)) return null;
        var set=sets->GetGearset(index);
        if(set==null || (set->Flags & RaptureGearsetModule.GearsetFlag.Exists)==0 || !SetUsable(set)) return null;
        var keys=new List<string>();
        foreach(var item in set->Items) keys.Add(Key(item.ItemId,item.GlamourId,item.Stain0Id,item.Stain1Id,item.Materia.ToArray(),item.MateriaGrades.ToArray()));
        return new(set->ClassJob,set->NameString,string.Join('|',keys),
            string.Join('|',set->Items.ToArray().Select(item=>item.ItemId)),set->GlamourSetLink);
    }
    private static bool ReadEquipment(out ItemIdentity[] equipment)
    {
        equipment=[];
        var manager=InventoryManager.Instance();
        var bag=manager==null?null:manager->GetInventoryContainer(InventoryType.EquippedItems);
        if(bag==null || !bag->IsLoaded || bag->Size<14) return false;
        var result=new ItemIdentity[14];
        for(var i=0;i<14;i++) { var item=bag->GetInventorySlot(i); if(item==null || item->IsSymbolic) return false; result[i]=ReadItem(item); }
        equipment=result; return true;
    }
    private static ItemIdentity ReadItem(InventoryItem* item)
    {
        var materia=new ushort[5]; var grades=new byte[5];
        for(byte i=0;i<5;i++) { materia[i]=item->GetMateriaId(i); grades[i]=item->GetMateriaGrade(i); }
        var id=JobGearsetCodec.GearsetItemId(item->ItemId,(item->Flags & InventoryItem.ItemFlags.HighQuality)!=0);
        return new(id,Key(id,item->GetGlamourId(),item->GetStain(0),item->GetStain(1),materia,grades),$"{item->Quantity}:{item->Condition}:{(byte)item->Flags}:{item->SpiritbondOrCollectability}");
    }
    private static string Key(uint id,uint glamour,byte stain0,byte stain1,ushort[] materia,byte[] grades)
        => $"{id}:{glamour}:{stain0}:{stain1}:{string.Join(',',materia)}:{string.Join(',',grades)}";
    private static string EquipmentKey(ItemIdentity[] equipment)=>string.Join('|',equipment.Select(item=>item.Detail));
    private bool Reject(string reason) { Status=reason; NotifyFeedback(true); return false; }
    private void NotifyFeedback(bool error)
    {
        try { Feedback?.Invoke(Status,error); }
        catch(Exception exception) { Services.Log.Warning(exception,"Could not show job switch feedback."); }
    }
    public void Dispose() { coordinator?.Cancel(); if(coordinator!=null) Status=coordinator.Status; pending=null; }
}









