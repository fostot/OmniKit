using OmniKit.Jobs;
var count=0;
void Check(bool valid,string message) { count++; if(!valid) throw new Exception(message); }
JobSwitchPlan Plan(bool soul=true)=>new(true,"test",CurrentSnapshot:new(1,8,"Carpenter"),TargetSnapshot:new(2,19,"Paladin"),
 Weapon:new(10,3500,0,1,10,false,true,false),SoulCrystal:soul?new(20,3501,0,1,0,false,true,true):null,RestoreGearsetIndex:1);
(Fake,JobSwitchCoordinator) Start(JobSwitchPlan? plan=null) { var f=new Fake(); return(f,new(plan??Plan(),f,0)); }
void Advance(JobSwitchCoordinator c,int steps,long start=0) { for(var i=0;i<steps;i++) c.Update(start+i); }
void ToTarget(Fake f,JobSwitchCoordinator c,bool soul=true)
{
 Advance(c,2); f.O=f.O with{WeaponEquipped=true}; Advance(c,2,10);
 f.O=f.O with{SoulEquipped=soul,SoulEmpty=!soul,TargetReady=true};
}
void ToProvisional(Fake f,JobSwitchCoordinator c) { ToTarget(f,c); Advance(c,2,20); }
void ToRecommended(Fake f,JobSwitchCoordinator c)
{
 ToProvisional(f,c); c.Update(30); f.O=f.O with{RecommendationReady=true}; Advance(c,2,40);
 f.O=f.O with{RecommendedComplete=true};
}
{
 var(f,c)=Start(); Advance(c,2); Check(f.Events.SequenceEqual(new[]{"create:1","weapon"}),"current save precedes weapon");
 Advance(c,100,10); Check(f.Events.Count==2,"weapon confirmation waits without retry");
 f.O=f.O with{WeaponEquipped=true}; Advance(c,2,200); Check(f.Events.Last()=="soul","soul follows actual weapon");
 f.O=f.O with{SoulEquipped=true}; Advance(c,10,300); Check(!f.Events.Contains("provisional:2"),"wrong actual job cannot create target");
 f.O=f.O with{TargetReady=true}; Advance(c,2,400);
 Check(f.Events.Last()=="provisional:2","plus creates target only after weapon and crystal");
 c.Update(410); Advance(c,100,420); Check(f.Events.Last()=="recommend-setup" && f.Events.Count==5,"asynchronous calculation never repeats or equips early");
 f.O=f.O with{RecommendationReady=true}; Advance(c,2,600); Check(f.Events.Last()=="recommend-equip","recommended gear applied once after calculation");
 Advance(c,100,700); Check(!f.Events.Contains("finalize:2"),"reassignment waits for physical recommended equipment");
 f.O=f.O with{RecommendedComplete=true}; Advance(c,2,900);
 Check(c.Succeeded && !c.Busy && f.Events.SequenceEqual(new[]{"create:1","weapon","soul","provisional:2","recommend-setup","recommend-equip","finalize:2"}),"complete native operation order");
 Advance(c,100,1000); Check(f.Events.Count==7,"completed flow never repeats"); c.Cancel(); Check(c.Succeeded,"dispose preserves completed success");
}
{
 var(f,c)=Start(); f.CreateResult=255; Advance(c,10); Check(!c.Busy && f.Events.SequenceEqual(new[]{"create:1"}),"native create failure stops before equipment");
}
{
 var(f,c)=Start(); f.O=f.O with{OriginalReady=false}; Advance(c,10); Check(f.Events.Count==0 && !c.Busy,"manual initial gear cannot be snapshotted");
}
{
 var(f,c)=Start(); Advance(c,1); f.O=f.O with{RestoreUnchanged=false}; Advance(c,10); Check(f.Events.Count==1 && !c.Busy,"changed return set blocks weapon");
}
{
 var(f,c)=Start(); f.MoveResult=false; Advance(c,10); Check(f.Events.SequenceEqual(new[]{"create:1","weapon"}),"failed weapon request cannot advance or retry");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{OriginalReady=false}; c.Update(25001); Advance(c,100,25002);
 Check(f.Events.Count(x=>x=="equip:1")==1 && c.Busy,"timeout requests at most one safe restore");
 f.O=f.O with{OriginalReady=true}; c.Update(26000); Check(!c.Busy && !c.Succeeded && c.Status.Contains("restored"),"restore requires observed original");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{OriginalReady=false}; c.Update(25001); c.Update(29002);
 Check(!c.Busy && c.Status.Contains("manually") && f.Events.Count(x=>x=="equip:1")==1,"restore deadline ends without retry");
}
foreach(var change in new[]{0,1,2,3})
{
 var(f,c)=Start(); Advance(c,2); f.O=change switch{0=>f.O with{CanAct=false},1=>f.O with{SameCharacter=false},2=>f.O with{ExpectedEquipment=false},_=>f.O with{EquipmentAvailable=false}};
 c.Update(3); Advance(c,10,100); Check(!c.Busy && f.Events.Count==2,"unsafe character/equipment state never queues later action");
}
{
 var(f,c)=Start(); Advance(c,2); c.Cancel(); Advance(c,10,100); Check(!c.Busy && f.Events.Count==2 && !c.Succeeded,"cancel is terminal");
}
{
 var(f,c)=Start(); Advance(c,2); f.O=f.O with{InventoryBusy=true}; c.Update(25001); Check(!c.Busy && f.Events.Count==2,"busy inventory timeout cannot restore");
}
{
 var(f,c)=Start(Plan(false)); ToTarget(f,c,false); Check(f.Events.Last()=="remove-soul","base class removes old crystal");
 Advance(c,2,20); Check(f.Events.Last()=="provisional:2","base class creates only after empty crystal observed");
}
{
 var(f,c)=Start(); ToTarget(f,c); f.ProvisionalResult=255; Advance(c,2,20);
 Check(f.Events.TakeLast(2).SequenceEqual(new[]{"provisional:2","equip:1"}) && !f.Events.Contains("recommend-setup"),"failed plus cannot calculate or reassign");
}
{
 var(f,c)=Start(); ToProvisional(f,c); f.BeginResult=false; c.Update(30);
 Check(f.Events.TakeLast(2).SequenceEqual(new[]{"recommend-setup","equip:1"}),"setup rejection attempts only safe return");
}
{
 var(f,c)=Start(); ToProvisional(f,c); c.Update(30); c.Update(25001);
 Check(!f.Events.Contains("recommend-equip") && f.Events.Count(x=>x=="recommend-setup")==1,"recommendation timeout never blind applies");
}
{
 var(f,c)=Start(); ToProvisional(f,c); c.Update(30); f.O=f.O with{RecommendationReady=true}; c.Update(40); f.ApplyResult=false; c.Update(41);
 Check(f.Events.TakeLast(2).SequenceEqual(new[]{"recommend-equip","equip:1"}) && !f.Events.Contains("finalize:2"),"apply rejection cannot reassign");
}
{
 var(f,c)=Start(); ToProvisional(f,c); f.O=f.O with{TargetUnchanged=false}; c.Update(30);
 Check(!c.Busy && f.Events.Last()=="provisional:2","edited new set cannot be recommended or overwritten");
}
{
 var(f,c)=Start(); ToRecommended(f,c); f.FinalizeResult=false; Advance(c,2,50);
 Check(f.Events.TakeLast(2).SequenceEqual(new[]{"finalize:2","equip:1"}),"reassignment rejection safely returns once");
}
{
 var(f,c)=Start(); ToRecommended(f,c); f.O=f.O with{TargetReady=false}; Advance(c,2,50);
 Check(!f.Events.Contains("finalize:2"),"wrong class immediately before reassignment stops");
}
{
 var plan=Plan() with{ExistingGearsetIndex=3,TargetSnapshot=null,Weapon=null,SoulCrystal=null}; var(f,c)=Start(plan);
 Advance(c,2); f.O=f.O with{TargetReady=true}; c.Update(3);
 Check(c.Succeeded && f.Events.SequenceEqual(new[]{"create:1","equip:3"}),"existing target never gets plus recommended or reassigned");
}
{
 var plan=Plan() with{CurrentSnapshot=null,RestoreGearsetIndex=null}; var(f,c)=Start(plan); f.O=f.O with{RestoreUnchanged=false};
 ToRecommended(f,c); Advance(c,2,50);
 Check(c.Succeeded && !f.Events.Contains("create:1"),"existing dirty current set prevents duplicate without false restore guard");
}
{
 var plan=Plan() with{CurrentSnapshot=null,RestoreGearsetIndex=null}; var(f,c)=Start(plan); f.O=f.O with{RestoreUnchanged=false};
 Advance(c,2); c.Update(25001); Check(!c.Busy && !f.Events.Any(x=>x.StartsWith("equip:")),"no matching return set means no fabricated automatic restore");
}
Check(JobGearsetCodec.DisplayName("  carpenter ")=="Carpenter","new class name is trimmed and capitalized");
Check(JobGearsetCodec.DisplayName("dark knight")=="Dark Knight","multiword class name uses display capitalization");
Check(JobGearsetCodec.DisplayName("侍")=="侍","nonLatin class name preserved");
// Adapter-facing native Flags projection: empty rows are free independently of IsValidGearset.
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
}
Console.WriteLine($"Passed {count} job switch protocol checks.");
sealed class Fake : IJobSwitchRuntime
{
 public JobSwitchObservation O=new(true,true,false,true,true,true,true,true,false,false,false,false);
 public List<string> Events=[]; public int? CreateResult,ProvisionalResult;
 public bool MoveResult=true,EquipResult=true,BeginResult=true,ApplyResult=true,FinalizeResult=true;
 public JobSwitchObservation Observe()=>O;
 public int CreateSnapshot(NewJobGearset s) { Events.Add($"create:{s.Index}"); return CreateResult??s.Index; }
 public int ProvisionalCreate(NewJobGearset s) { Events.Add($"provisional:{s.Index}"); return ProvisionalResult??s.Index; }
 public bool BeginRecommended() { Events.Add("recommend-setup"); return BeginResult; }
 public bool ApplyRecommended() { Events.Add("recommend-equip"); return ApplyResult; }
 public bool FinalizeTarget(NewJobGearset s) { Events.Add($"finalize:{s.Index}"); return FinalizeResult; }
 public bool EquipGearset(int i) { Events.Add($"equip:{i}"); return EquipResult; }
 public bool MoveWeapon() { Events.Add("weapon"); return MoveResult; }
 public bool MoveSoul() { Events.Add("soul"); return MoveResult; }
 public bool RemoveSoul() { Events.Add("remove-soul"); return MoveResult; }
}