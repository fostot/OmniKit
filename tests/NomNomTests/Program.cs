using OmniKit.Automation;
var checks=0;
void Check(bool ok,string message) { checks++; if(!ok) throw new Exception(message); }
var settings=new NomNomSettings(true,100,false,true);
var food=new NomNomCandidate(100,100,0,0,10,false,"Food",true);
var o=new NomNomObservation(new(1,2,3),true,false,10,food,true);
(NomNomController,List<NomNomCandidate>) New() => (new(),[]);
void Step(NomNomController c,List<NomNomCandidate> calls,long time,NomNomObservation? obs=null,NomNomSettings? cfg=null,bool result=true)
    => c.Update(cfg??settings,obs??o,time,item=>{calls.Add(item);return result;});
{
 var(c,calls)=New(); Step(c,calls,0); Check(calls.Count==0,"enable has brief safety delay"); Step(c,calls,2000);
 Check(c.Busy && calls.Count==1,"one native request when missing Well Fed");
 for(var i=0;i<100;i++) Step(c,calls,2001+i); Check(calls.Count==1,"waiting never retries per frame");
 Step(c,calls,3000,o with{WellFed=true,SelectedCount=9}); Check(!c.Busy && calls.Count==1,"observed Well Fed confirms food");
 Step(c,calls,8000,o with{WellFed=true}); Check(calls.Count==1,"active Well Fed never refreshed prematurely");
 Step(c,calls,10000); Check(c.Busy && calls.Count==2,"new missing buff episode can eat once");
}
{
 var(c,calls)=New(); Step(c,calls,0); Step(c,calls,2000,result:false);
 for(var i=0;i<100;i++) Step(c,calls,5000+i*1000); Check(calls.Count==1 && !c.Busy,"native rejection latches indefinitely");
 Step(c,calls,110000,o with{SelectedCount=9}); Step(c,calls,120000,o with{SelectedCount=9});
 Check(calls.Count==1,"delayed own decrement after rejection does not re-arm");
 Step(c,calls,130000,o with{SelectedCount=20}); Step(c,calls,136000,o with{SelectedCount=20});
 Check(calls.Count==2,"meaningful restock re-arms after backoff");
}
{
 var(c,calls)=New(); Step(c,calls,0); Step(c,calls,2000); Step(c,calls,3000,o with{SelectedCount=9});
 Step(c,calls,11000,o with{SelectedCount=9}); Check(!c.Busy && calls.Count==1,"consumed but no Well Fed times out");
 Step(c,calls,50000,o with{SelectedCount=9}); Check(calls.Count==1,"own consumption never creates eating loop");
}
{
 var(c,calls)=New(); Step(c,calls,0); Step(c,calls,2000); Step(c,calls,11000);
 Step(c,calls,12000,o with{SelectedCount=9}); Step(c,calls,40000,o with{SelectedCount=9});
 Check(calls.Count==1,"late inventory scan after timeout still cannot re-arm own decrement");
}
foreach(var cfg in new[]{settings with{Enabled=false},settings with{RequireExperience=false}})
{
 var(c,calls)=New(); Step(c,calls,0,cfg:cfg); Step(c,calls,10000,cfg:cfg); Check(calls.Count==0,"disabled/EXP switch off performs no action");
}
foreach(var obs in new[]{o with{Safe=false},o with{NativeUsable=false},o with{WellFed=true},o with{Candidate=null,SelectedCount=0},o with{Identity=default},o with{Candidate=food with{GrantsExperience=false}}})
{
 var(c,calls)=New(); Step(c,calls,0,obs); Step(c,calls,10000,obs); Check(calls.Count==0,"unsafe/unusable/fed/missing/logout/nonEXP performs no action");
}
{
 var(c,calls)=New(); Step(c,calls,0); Step(c,calls,2000); Step(c,calls,2500,o with{Safe=false});
 Step(c,calls,20000); Check(!c.Busy && calls.Count==1,"unsafe interruption does not queue later retry");
}
{
 var(c,calls)=New(); Step(c,calls,0); Step(c,calls,2000); Step(c,calls,2500,cfg:settings with{Enabled=false});
 Check(!c.Busy && calls.Count==1,"disable stops observation and cannot consume");
 Step(c,calls,3000); Step(c,calls,6000); Check(calls.Count==2,"explicit enable change deliberately re-arms");
}
{
 var(c,calls)=New(); Step(c,calls,0); Step(c,calls,2000); c.Cancel(); Check(!c.Busy,"dispose cancels pending observation");
 Step(c,calls,50000,o with{SelectedCount=20}); Check(calls.Count==1,"cancel is terminal even after stock changes");
}
{
 var hq=food with{UseId=1000100,HighQuality=true,Slot=1}; var other=food with{ItemId=101,Slot=2};
 Check(NomNomFoodPolicy.SelectCandidate(new[]{hq,food,other},100,false)==food,"default prefers normal quality");
 Check(NomNomFoodPolicy.SelectCandidate(new[]{hq,food},100,true)==hq,"HQ preference chooses exact HQ stack");
 Check(NomNomFoodPolicy.SelectCandidate(new[]{hq},100,false)==hq,"normal preference falls back to owned HQ");
 Check(NomNomFoodPolicy.SelectCandidate(new[]{food},100,true)==food,"HQ preference falls back to owned normal");
 Check(NomNomFoodPolicy.SelectCandidate(new[]{other},100,false)==null,"never substitute another food");
 Check(NomNomFoodPolicy.SelectCandidate(new[]{food with{Count=0}},100,false)==null,"empty stacks never selected");
 Check(NomNomFoodPolicy.UseId(100,true)==1000100,"HQ use ID encoded exactly once");
}
Check(NomNomFoodPolicy.IsSupportedFood(844,48,true,false),"food844 WellFed classifier");
Check(NomNomFoodPolicy.IsSupportedFood(845,48,true,false),"drink845 WellFed classifier");
Check(!NomNomFoodPolicy.IsSupportedFood(846,49,true,false),"medicine excluded");
Check(!NomNomFoodPolicy.IsSupportedFood(847,48,true,false),"incidental data48 on other action not treated as food");
Check(!NomNomFoodPolicy.IsSupportedFood(844,49,true,false),"wrong status excluded");
Check(!NomNomFoodPolicy.IsSupportedFood(844,48,false,false),"missing food row excluded");
Check(!NomNomFoodPolicy.IsSupportedFood(844,48,true,true),"area action excluded");
{
 var(c,calls)=New(); Step(c,calls,0); Step(c,calls,2000); Step(c,calls,2500,o with{Identity=new(7,8,9)});
 Check(!c.Busy && calls.Count==1,"character or area change cancels old pending observation");
 Step(c,calls,4000,o with{Identity=new(7,8,9)}); Check(calls.Count==1,"new character must settle before any attempt");
}
{
 var(c,calls)=New(); var no=o with{Candidate=food with{ItemId=101}}; Step(c,calls,0,no); Step(c,calls,10000,no);
 Check(calls.Count==0,"mismatched candidate cannot consume substituted food");
}{
 var nq=food with {ExperienceBonus=3,ItemLevel=100};
 var hq=nq with {HighQuality=true,UseId=1000100,Slot=1};
 var stronger=nq with {ItemId=101,UseId=101,ExperienceBonus=6,ItemLevel=1};
 var higher=stronger with {ItemId=102,UseId=102,ItemLevel=200};
 Check(NomNomFoodPolicy.SelectCandidate(new[]{nq,stronger},0,false)==stronger,"automatic ranks EXP bonus before item level");
 Check(NomNomFoodPolicy.SelectCandidate(new[]{higher,stronger},0,false)==higher,"automatic item level breaks EXP ties");
 Check(NomNomFoodPolicy.SelectCandidate(new[]{hq,nq},0,false)==nq,"automatic defaults to NQ when bonuses and item level tie");
 Check(NomNomFoodPolicy.SelectCandidate(new[]{nq,hq},0,true)==hq,"automatic honors HQ preference on ties");
 Check(NomNomFoodPolicy.SelectCandidate(new[]{higher,nq},100,false)==nq,"manual selection remains exact despite stronger automatic option");
 Check(NomNomFoodPolicy.SelectCandidate(new[]{nq with{Count=0},nq with{GrantsExperience=false}},0,false)==null,"automatic excludes empty and nonEXP food");
 Check(NomNomFoodPolicy.SelectCandidate(new[]{nq with{ItemId=0}},0,false)==null,"automatic rejects empty item identity");
 Check(NomNomFoodPolicy.RankCandidates(new[]{nq,higher,stronger},0,false).SequenceEqual(new[]{higher,stronger,nq}),"native usable fallback retains best-first order");
 var stable=nq with{ItemId=99,UseId=99};
 Check(NomNomFoodPolicy.SelectCandidate(new[]{nq,stable},0,false)==stable,"automatic tie has stable item identity");
}
{
 var auto=settings with{FoodId=0}; var(c,calls)=New();
 Step(c,calls,0,o with{SelectedCount=2},auto); Step(c,calls,2000,o with{SelectedCount=2},auto);
 Check(c.Busy && calls.Count==1,"zero default selects automatic food rather than pausing");
 var next=o with{SelectedCount=1,Candidate=food with{ItemId=101,UseId=101}};
 Step(c,calls,11000,next,auto); Step(c,calls,50000,next,auto);
 Check(!c.Busy && calls.Count==1,"automatic exhausting one food cannot drain the next after missing buff confirmation");
 Step(c,calls,51000,next with{WellFed=true},auto); Step(c,calls,55000,next,auto);
 Check(c.Busy && calls.Count==2 && calls.Last().ItemId==101,"next automatic kind is allowed after observed Well Fed then expiry");
}
{
 var auto=settings with{FoodId=0}; var(c,calls)=New();
 Step(c,calls,0,o with{Candidate=null,SelectedCount=0},auto);
 Step(c,calls,10000,o with{Candidate=null,SelectedCount=0},auto);
 Check(calls.Count==0 && c.Status.Contains("No eligible"),"empty automatic inventory stays idle with useful status");
 Step(c,calls,11000,o with{Candidate=food with{GrantsExperience=false}},auto);
 Check(calls.Count==0,"automatic never consumes a nonEXP candidate");
}Console.WriteLine($"Passed {checks} Nom Nom policy checks.");

