using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using NativeFramework=FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace OmniKit.Automation;

/// <summary>Local personal-bag food maintenance. No networking, provider calls or persistent inventory snapshots.</summary>
internal sealed unsafe class NomNomService(Configuration config) : IDisposable
{
    private readonly NomNomController controller=new();
    private IReadOnlyList<NomNomFoodOption> foods=[];
    private List<NomNomCandidate> candidates=[];
    private long nextScan;
    private long nextUpdate;
    private bool disposed;
    public bool Busy=>controller.Busy;
    public string Status=>controller.Status;
    public uint AutomaticFoodId { get; private set; }
    public IReadOnlyList<NomNomFoodOption> GetFoodOptions()=>foods;
    public void RefreshFoods()=>nextScan=0;
    public void Update(bool inputBlocked=false)
    {
        if(disposed || !Services.Framework.IsInFrameworkUpdateThread) return;
        var now=Environment.TickCount64;
        if(now<nextUpdate) return;
        nextUpdate=now+250;
        var player=Services.Objects.LocalPlayer;
        var native=PlayerState.Instance();
        var loggedIn=Services.ClientState.IsLoggedIn && player!=null && native!=null && native->IsLoaded;
        if(!loggedIn) { foods=[]; candidates=[]; nextScan=0; }
        else if(now>=nextScan) { ScanFoods(); nextScan=now+1000; }
        var identity=loggedIn?new NomNomIdentity(native->ContentId,player!.GameObjectId,Services.ClientState.TerritoryType):default;
        var wellFed=loggedIn && player!.StatusList.Any(status=>status.StatusId==48);
        var safe=loggedIn && !inputBlocked && CanAct();
        var manager=ActionManager.Instance();
        var ready=config.NomNomEnabled && config.NomNomKeepExpBuff && safe && !wellFed
            && manager!=null && !manager->ActionQueued && manager->AnimationLock<=0;
        var ranked=NomNomFoodPolicy.RankCandidates(candidates,config.NomNomFoodId,config.NomNomPreferHighQuality);
        var selected=ranked.FirstOrDefault();
        if(ready && config.NomNomFoodId==0)
            foreach(var item in ranked)
                if(manager->GetActionStatus(ActionType.Item,item.UseId,player!.GameObjectId)==0) { selected=item; break; }
        AutomaticFoodId=(config.NomNomFoodId==0 ? selected : NomNomFoodPolicy.SelectCandidate(candidates,0,config.NomNomPreferHighQuality))?.ItemId ?? 0;
        // Automatic stock spans eligible foods, so using the last item of one kind
        // cannot silently re-arm a failed attempt against the next kind.
        var count=candidates.Where(item=>config.NomNomFoodId==0 ? item.GrantsExperience : item.ItemId==config.NomNomFoodId).Sum(item=>item.Count);
        var usable=ready && selected!=null
            && manager->GetActionStatus(ActionType.Item,selected.UseId,player!.GameObjectId)==0;
        controller.Update(new(config.NomNomEnabled,config.NomNomFoodId,config.NomNomPreferHighQuality,config.NomNomKeepExpBuff),
            new(identity,safe,wellFed,count,selected,usable),now,item=>TryEat(item,identity,inputBlocked));
    }
    private void ScanFoods()
    {
        var inventory=InventoryManager.Instance();
        var found=new List<NomNomCandidate>();
        if(inventory==null) { foods=[]; candidates=[]; return; }
        foreach(var type in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4})
        {
            var bag=inventory->GetInventoryContainer(type);
            if(bag==null || !bag->IsLoaded || bag->Items==null || bag->Size is <0 or >256) continue;
            for(ushort slot=0;slot<bag->Size;slot++)
            {
                var item=bag->GetInventorySlot(slot);
                if(item==null || item->IsSymbolic || item->ItemId==0 || item->Quantity<=0
                    || !Services.Data.GetExcelSheet<Item>().TryGetRow(item->ItemId,out var row)) continue;
                var hq=(item->Flags & InventoryItem.ItemFlags.HighQuality)!=0;
                if(!FoodMetadata(row,hq,out var experience)) continue;
                found.Add(new(row.RowId,NomNomFoodPolicy.UseId(row.RowId,hq),(int)type,slot,item->Quantity,hq,row.Name.ExtractText(),experience>0,
                    experience,(int)row.LevelItem.RowId));
            }
        }
        candidates=found;
        foods=found.GroupBy(item=>item.ItemId).Select(group=>
        {
            var first=group.First();
            var icon=Services.Data.GetExcelSheet<Item>().GetRow(first.ItemId).Icon;
            return new NomNomFoodOption(first.ItemId,first.Name,icon,group.Where(item=>!item.HighQuality).Sum(item=>item.Count),
                group.Where(item=>item.HighQuality).Sum(item=>item.Count),group.Any(item=>item.GrantsExperience));
        }).OrderBy(item=>item.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    private static bool FoodMetadata(Item row,bool hq,out int experience)
    {
        experience=0;
        if(!row.ItemAction.IsValid || row.ItemAction.RowId==0) return false;
        var action=row.ItemAction.Value;
        // Food/drink actions use Well Fed in argument zero and the ItemFood row in argument one.
        if(!action.Action.IsValid) return false;
        var data=hq?action.DataHQ:action.Data;
        if(!Services.Data.GetExcelSheet<ItemFood>().TryGetRow(data[1],out var food)
            || !NomNomFoodPolicy.IsSupportedFood(action.Action.RowId,data[0],true,action.Action.Value.TargetArea)) return false;
        experience=food.EXPBonusPercent;
        return true;
    }
    private bool TryEat(NomNomCandidate selected,NomNomIdentity expected,bool inputBlocked)
    {
        if(disposed || !config.NomNomEnabled || !config.NomNomKeepExpBuff
            || (config.NomNomFoodId!=0 && config.NomNomFoodId!=selected.ItemId) || inputBlocked || !CanAct()) return false;
        var player=Services.Objects.LocalPlayer;
        var native=PlayerState.Instance();
        var inventory=InventoryManager.Instance();
        var manager=ActionManager.Instance();
        if(player==null || native==null || inventory==null || manager==null || manager->ActionQueued || manager->AnimationLock>0
            || new NomNomIdentity(native->ContentId,player.GameObjectId,Services.ClientState.TerritoryType)!=expected
            || player.StatusList.Any(status=>status.StatusId==48)) return false;
        foreach(var op in inventory->PendingOperations) if(!op.IsEmpty) return false;
        var bag=inventory->GetInventoryContainer((InventoryType)selected.Container);
        if(bag==null || !bag->IsLoaded || selected.Slot>=bag->Size) return false;
        var item=bag->GetInventorySlot(selected.Slot);
        if(item==null || item->IsSymbolic || item->ItemId!=selected.ItemId || item->Quantity!=selected.Count
            || ((item->Flags & InventoryItem.ItemFlags.HighQuality)!=0)!=selected.HighQuality
            || !Services.Data.GetExcelSheet<Item>().TryGetRow(item->ItemId,out var row)
            || !FoodMetadata(row,selected.HighQuality,out var experience) || config.NomNomKeepExpBuff && experience<=0
            || manager->GetActionStatus(ActionType.Item,selected.UseId,player.GameObjectId)!=0) return false;
        var extra=((uint)selected.Container<<8)|selected.Slot;
        // Macro mode rejects queuing. Exact bag/slot and self-target are revalidated above.
        return manager->UseAction(ActionType.Item,selected.UseId,player.GameObjectId,extra,ActionManager.UseActionMode.Macro);
    }
    private static bool CanAct()
    {
        if(!Services.ClientState.IsLoggedIn || Services.ClientState.IsGPosing || Services.Objects.LocalPlayer==null
            || Services.GameGui.GameUiHidden) return false;
        foreach(var flag in Blockers) if(Services.Conditions[flag]) return false;
        var framework=NativeFramework.Instance(); var ui=RaptureAtkModule.Instance();
        return framework!=null && !framework->WindowInactive && !framework->IsExiting && ui!=null && !ui->IsTextInputActive();
    }
    private static readonly ConditionFlag[] Blockers=[ConditionFlag.Unconscious,ConditionFlag.InCombat,ConditionFlag.Casting,
        ConditionFlag.Casting87,ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51,ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78,ConditionFlag.Occupied,ConditionFlag.Occupied30,ConditionFlag.Occupied33,
        ConditionFlag.Occupied38,ConditionFlag.Occupied39,ConditionFlag.OccupiedInEvent,ConditionFlag.OccupiedInQuestEvent,
        ConditionFlag.OccupiedInCutSceneEvent,ConditionFlag.OccupiedSummoningBell,ConditionFlag.Mounted,ConditionFlag.Mounting,
        ConditionFlag.Mounting71,ConditionFlag.MountOrOrnamentTransition,ConditionFlag.InFlight,ConditionFlag.RidingPillion,
        ConditionFlag.Jumping,ConditionFlag.Jumping61,ConditionFlag.Crafting,ConditionFlag.ExecutingCraftingAction,
        ConditionFlag.PreparingToCraft,ConditionFlag.Gathering,ConditionFlag.ExecutingGatheringAction];
    public void Dispose() { disposed=true; controller.Cancel(); foods=[]; candidates=[]; }
}



