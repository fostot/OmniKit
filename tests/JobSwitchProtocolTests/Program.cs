using OmniKit.Jobs;
var count=0;
void Check(bool valid,string message) { count++; if(!valid) throw new Exception(message); }
JobSwitchPlan Plan(bool soul=true)=>new(true,"test", CurrentSnapshot:new(1,8,"Carpenter"),TargetSnapshot:new(2,19,"Paladin"),
 Weapon:new(10,3500,0,1,10,false,true,false),SoulCrystal:soul?new(20,3501,0,1,0,false,true,true):null,RestoreGearsetIndex:1);
(Fake,JobSwitchCoordinator) Start(JobSwitchPlan? plan=null) { var f=new Fake(); return (f,new(plan??Plan(),f,0)); }
void Advance(JobSwitchCoordinator c,int steps,long start=0) { for(var i=0;i<steps;i++) c.Update(start+i); }
{
 var(f,c)=Start(); Advance(c,2); Check(f.Events.SequenceEqual(new[]{"create:1","weapon"}),"snapshot must precede weapon");
 Advance(c,100,10); Check(f.Events.Count==2,"waiting weapon must not retry or save");
 f.O=f.O with {WeaponEquipped=true}; Advance(c,2,200); Check(f.Events.Last()=="soul","stone after physical weapon");
 f.O=f.O with {SoulEquipped=true}; Advance(c,3,300); Check(!f.Events.Contains("create:2"),"stone alone cannot save wrong actual job");
 f.O=f.O with {TargetReady=true}; Advance(c,2,400); Check(!c.Busy && f.Events.Last()=="create:2","actual target creates once");
 Advance(c,100,500); Check(f.Events.Count==4,"complete cannot repeat commands");
 Check(c.Succeeded,"saved new target reports success"); c.Cancel(); Check(c.Succeeded,"dispose after completion preserves success");
}
{
 var(f,c)=Start(); f.CreateResult=255; Advance(c,20); Check(!c.Busy && f.Events.SequenceEqual(new[]{"create:1"}),"native create255 stops before mutation");
 Check(!c.Succeeded,"failed native creation reports failure");
}
{
 var(f,c)=Start(); f.O=f.O with{OriginalReady=false}; Advance(c,20); Check(!c.Busy && f.Events.Count==0,"manual gear before snapshot stops all");
}
{
 var(f,c)=Start(); Advance(c,1); f.O=f.O with{RestoreUnchanged=false}; Advance(c,20); Check(!c.Busy && f.Events.Count==1,"restore slot reuse stops before moves");
}
{
 var(f,c)=Start(); f.MoveResult=false; Advance(c,20); Check(!c.Busy && f.Events.SequenceEqual(new[]{"create:1","weapon"}),"source slot reuse never retries");
}
{
 var(f,c)=Start(); Advance(c,2); c.Update(10001); Check(f.Events.Last()=="equip:1" && c.Busy,"timeout makes one rollback request");
 Advance(c,100,10002); Check(f.Events.Count(x=>x=="equip:1")==1,"rollback not retried");
 f.O=f.O with{OriginalReady=true}; c.Update(11000); Check(!c.Busy && c.Status.Contains("restored"),"rollback requires observed original");
 Check(!c.Succeeded,"restored original remains failed request");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{OriginalReady=false}; c.Update(10001); c.Update(14002);
 Check(!c.Busy && f.Events.Count(x=>x=="equip:1")==1 && c.Status.Contains("manually"),"rollback timeout ends with manual restore");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{CanAct=false}; c.Update(3); f.O=f.O with{CanAct=true}; Advance(c,20,4);
 Check(!c.Busy && f.Events.Count==2,"disable/combat stop without delayed rollback");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{SameCharacter=false}; c.Update(3); Check(!c.Busy && f.Events.Count==2,"login/area swap forbids rollback");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{ExpectedEquipment=false}; c.Update(3); Check(!c.Busy && f.Events.Count==2,"manual interference forbids restore over user");
}
{
 var(f,c)=Start(); Advance(c,2); c.Cancel(); Advance(c,20,10); Check(!c.Busy && f.Events.Count==2,"dispose cancel does not schedule later restore");
 Check(!c.Succeeded,"cancelled request does not report success");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{InventoryBusy=true}; c.Update(10001);
 Check(!c.Busy && f.Events.Count==2,"busy inventory timeout cannot issue rollback");
}
{
 var(f,c)=Start(Plan(false)); Advance(c,2); f.O=f.O with{WeaponEquipped=true}; Advance(c,2,10);
 Check(f.Events.Last()=="remove-soul","base class removes old soul once"); f.O=f.O with{SoulEmpty=true,TargetReady=true}; Advance(c,2,20);
 Check(!c.Busy && f.Events.Last()=="create:2","base class waits physical ready before save");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{WeaponEquipped=true}; Advance(c,2,10); f.O=f.O with{TargetReady=true}; c.Update(20);
 f.CreateResult=255; c.Update(21); Check(f.Events.TakeLast(2).SequenceEqual(new[]{"create:2","equip:1"}),"capacity race before target save triggers one restore");
}
{
 var plan=Plan() with{ExistingGearsetIndex=3,TargetSnapshot=null,Weapon=null,SoulCrystal=null}; var(f,c)=Start(plan);
 f.EquipResult=false; Advance(c,2); Check(f.Events.SequenceEqual(new[]{"create:1","equip:3","equip:1"}),"native equip failure restores once");
}
{
 var plan=Plan() with{ExistingGearsetIndex=3,TargetSnapshot=null,Weapon=null,SoulCrystal=null}; var(f,c)=Start(plan);
 Advance(c,2); f.O=f.O with{TargetReady=true}; c.Update(3); Check(!c.Busy && f.Events.SequenceEqual(new[]{"create:1","equip:3"}),"existing target never overwritten");
 Check(c.Succeeded,"observed existing target reports success");
}
{
 var(f,c)=Start(); f.CreateResult=9; Advance(c,20); Check(!c.Busy && f.Events.Count==1,"unexpected native slot stops before weapon");
}
{
 var(f,c)=Start(); f.O=f.O with{InventoryBusy=true}; Advance(c,20); Check(f.Events.Count==0,"pending inventory blocks initial native action");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{WeaponEquipped=true}; c.Update(3); f.MoveResult=false; c.Update(4);
 Check(f.Events.TakeLast(2).SequenceEqual(new[]{"soul","equip:1"}),"soul source reuse restores once");
}
{
 var(f,c)=Start(); Advance(c,2); f.EquipResult=false; c.Update(10001); Advance(c,20,10002);
 Check(!c.Busy && f.Events.Count(x=>x=="equip:1")==1,"native rejected rollback cannot loop");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{EquipmentAvailable=false}; c.Update(3);
 Check(!c.Busy && f.Events.Count==2,"unavailable equipment never guessed or restored");
}
{
 var plan=Plan() with{ExistingGearsetIndex=3,TargetSnapshot=null,Weapon=null,SoulCrystal=null}; var(f,c)=Start(plan);
 c.Update(0); f.O=f.O with{TargetUnchanged=false}; c.Update(1);
 Check(!c.Busy && f.Events.Count==1,"target slot reuse stops before existing equip");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{WeaponEquipped=true}; Advance(c,2,10);
 f.O=f.O with{TargetReady=true}; c.Update(20); f.O=f.O with{TargetReady=false}; c.Update(21);
 Check(!f.Events.Contains("create:2") && f.Events.Last()=="equip:1","wrong class immediately before save blocks creation");
}// Adapter-facing native Flags projection: empty rows are free independently of IsValidGearset.
{
 byte[] flags=[1,0,1,0,0];
 Check(JobGearsetCodec.FreeSlots(flags,5,1).SequenceEqual(new[]{1,3,4}),"empty native flags become free slots");
 Check(JobGearsetCodec.FreeSlots(flags,4,1).SequenceEqual(new[]{1,3}),"permitted capacity bounds empty rows");
 Check(JobGearsetCodec.FreeSlots(flags,5,3).SequenceEqual(new[]{3,4}),"native first-empty skips inaccessible preceding rows");
 Check(JobGearsetCodec.FreeSlots(flags,5,255).Length==0,"native full sentinel permits no creation");
 Check(JobGearsetCodec.FreeSlots(flags,5,0).Length==0,"occupied native first-empty fails safely");
 Check(JobGearsetCodec.CanCreateAt(1,1,flags),"empty native slot accepted without IsValid existing-row predicate");
 Check(!JobGearsetCodec.CanCreateAt(1,3,flags),"first-empty changed before creation stops");
 Check(!JobGearsetCodec.CanCreateAt(0,0,flags),"existing flags never accepted for creation");
 Check(!JobGearsetCodec.CanCreateAt(1,255,flags),"full capacity refuses create");
 Check(!JobGearsetCodec.CanCreateAt(5,5,flags),"out-of-range slot refuses create");
 Check(JobGearsetCodec.GearsetItemId(1234,false)==1234,"normal gearset ID keeps base");
 Check(JobGearsetCodec.GearsetItemId(1234,true)==1001234,"HQ physical item equals HQ gearset stored ID");
 Check(JobGearsetCodec.GearsetItemId(1234,true)!=JobGearsetCodec.GearsetItemId(1234,false),"HQ and normal fingerprint differ");
 Check(JobGearsetCodec.GearsetItemId(0,true)==0,"empty stale HQ flags remain empty");
}{
 var plan=Plan() with{CurrentSnapshot=null,TargetSnapshot=null,Weapon=null,SoulCrystal=null,ExistingGearsetIndex=null};
 var(f,c)=Start(plan); Advance(c,2); Check(c.Succeeded && !c.Busy && f.Events.Count==0,"already equipped class is successful without native mutation");
}{
 var original=Enumerable.Range(0,14).Select(i=>$"original-{i}").ToArray();
 var target=Enumerable.Range(0,14).Select(i=>$"target-{i}").ToArray();
 var partial=Enumerable.Range(0,14).Select(i=>i%2==0?original[i]:target[i]).ToArray();
 Check(JobGearsetCodec.EquipmentTransitionAllowed(original,target,original),"original native gear is valid before partial switch");
 Check(JobGearsetCodec.EquipmentTransitionAllowed(original,target,target),"complete target gear is valid");
 Check(JobGearsetCodec.EquipmentTransitionAllowed(original,target,partial),"legitimate per-slot partial transition is allowed");
 partial[3]=target[4]; Check(!JobGearsetCodec.EquipmentTransitionAllowed(original,target,partial),"target gear in wrong slot is manual interference");
 partial[3]="unrelated-manual-gear"; Check(!JobGearsetCodec.EquipmentTransitionAllowed(original,target,partial),"unrelated manual gear aborts native transition");
 Check(!JobGearsetCodec.EquipmentTransitionAllowed(original,target,original[..13]),"truncated equipment observation rejected");
 Check(JobGearsetCodec.SelectionTransitionAllowed(4,4,8),"prior selected set allowed while native request waits");
 Check(JobGearsetCodec.SelectionTransitionAllowed(8,4,8),"intended selected set allowed after request");
 Check(!JobGearsetCodec.SelectionTransitionAllowed(9,4,8),"unrelated selected set rejected even if its items match");
 Check(!JobGearsetCodec.SelectionTransitionAllowed(-1,4,8),"unexpected selection reset fails safe");
}Console.WriteLine($"Passed {count} job switch protocol checks.");
sealed class Fake : IJobSwitchRuntime
{
 public JobSwitchObservation O=new(true,true,false,true,true,true,true,true,false,false,false,false);
 public List<string> Events=[]; public int? CreateResult; public bool MoveResult=true,EquipResult=true;
 public JobSwitchObservation Observe()=>O;
 public int CreateSnapshot(NewJobGearset s) { Events.Add($"create:{s.Index}"); return CreateResult??s.Index; }
 public bool EquipGearset(int i) { Events.Add($"equip:{i}"); return EquipResult; }
 public bool MoveWeapon() { Events.Add("weapon"); return MoveResult; }
 public bool MoveSoul() { Events.Add("soul"); return MoveResult; }
 public bool RemoveSoul() { Events.Add("remove-soul"); return MoveResult; }
}




