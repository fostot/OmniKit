using System.Numerics;
using OmniKit.Unlocks;
var count=0;
void Check(bool ok,string reason) { count++; if(!ok) throw new Exception(reason); }
var start=new Vector3(0,0,0); var end=new Vector3(10,0,0); var middle=new Vector3(5,0,0);
(Fake,VnavmeshBridge) New() { var fake=new Fake(); return (fake,new(fake)); }
void Move(Fake f,VnavmeshBridge b) { b.TryBeginPath(start,end,1,0); f.Pending.SetResult([start,middle,end]); b.Poll(1,1); }
{
 var(f,b)=New(); Check(b.TryBeginPath(start,end,1,0),"idle mesh accepts one owned query");
 Check(b.State==VnavmeshState.FindingPath && f.FindCalls==1 && f.MoveCalls==0,"pathfind and move are separate");
 for(var i=0;i<100;i++) b.Poll(1,i); Check(f.FindCalls==1 && f.MoveCalls==0,"pending query never repeated or moved");
 Check(!b.TryBeginPath(start,end,1,200),"second start blocked while owned query pending");
 f.Pending.SetResult([start,middle,end]); b.Poll(1,201); Check(b.State==VnavmeshState.Moving && f.MoveCalls==1,"completed owned query moves once");
 b.Poll(1,202); Check(f.MoveCalls==1,"moving poll never restarts route");
 f.Path=[middle,end]; b.Poll(1,203); Check(b.State==VnavmeshState.Moving,"consumed waypoint suffix remains owned");
 Check(b.Stop() && f.StopCalls==1,"stop sent only for matching owned suffix");
 Check(!b.Stop() && f.StopCalls==1,"stop never repeated after cancellation");
}
foreach(var blocker in new[]{"missing","notready","running","simple","query","queued","thread"})
{
 var(f,b)=New(); switch(blocker) { case "missing":f.Available=false;break;case "notready":f.MeshReady=false;break;
 case "running":f.Active=true;break;case "simple":f.Simple=true;break;case "query":f.Query=true;break;case "queued":f.Queue=1;break;case "thread":f.CanInvoke=false;break; }
 Check(!b.TryBeginPath(start,end,1,0) && f.FindCalls==0 && f.StopCalls==0,"foreign/missing/unready activity prevents start: "+blocker);
 Check(b.BlockedByOtherNavigation==(blocker is "running" or "simple" or "query" or "queued"),"caller can stop the entire run for foreign navigation instead of retrying other destinations: "+blocker);
 Check(!b.IsIdle,"in-range interactions also require verified idle navigation: "+blocker);
}
{
 var(f,b)=New(); Check(b.IsIdle,"in-range interaction can start when dependency is ready and all navigation is idle");
 f.Active=true; Check(!b.IsIdle && f.StopCalls==0,"new foreign path blocks in-range interaction without stopping it");
}
{
 var(f,b)=New(); b.TryBeginPath(start,end,1,0); Check(b.Stop() && f.Token.IsCancellationRequested,"pending cancellation owns only CTS");
 f.Pending.SetResult([start,end]); b.Poll(1,10); Check(f.MoveCalls==0 && f.StopCalls==0,"late path completion never moves and never globally stops");
}
{
 var(f,b)=New(); b.TryBeginPath(start,end,1,0); f.Pending.SetException(new InvalidOperationException("synthetic")); b.Poll(1,1);
 Check(b.State==VnavmeshState.Failed && f.MoveCalls==0,"faulted path reports failure without moving");
}
{
 var(f,b)=New(); b.TryBeginPath(start,end,1,0); f.Pending.SetResult([]); b.Poll(1,1);
 Check(b.State==VnavmeshState.Failed && f.MoveCalls==0,"empty path never moves");
}
{
 var(f,b)=New(); b.TryBeginPath(start,end,1,0); f.Pending.SetResult([start,middle]); b.Poll(1,1);
 Check(b.State==VnavmeshState.Failed && f.MoveCalls==0,"partial path cannot pretend to reach destination");
}
{
 var(f,b)=New(); b.TryBeginPath(start,end,1,0); f.Pending.SetResult([start,end]); f.Active=true; b.Poll(1,1);
 Check(b.State==VnavmeshState.OwnershipLost && f.MoveCalls==0 && f.StopCalls==0,"foreign path before dispatch not overwritten");
}
{
 var(f,b)=New(); Move(f,b); f.Path=[new(7,0,2),end]; b.Poll(1,2);
 Check(b.State==VnavmeshState.OwnershipLost && f.StopCalls==0,"replaced path loses ownership without globalstop");
 Check(!b.Stop() && f.StopCalls==0,"cannot stop replacement path after ownership loss");
}
{
 var(f,b)=New(); Move(f,b); f.Simple=true; Check(!b.Stop() && f.StopCalls==0,"foreign SimpleMove pending query prevents global stop");
}
{
 var(f,b)=New(); Move(f,b); f.Query=true; b.Poll(1,2); Check(b.State==VnavmeshState.OwnershipLost && f.StopCalls==0,"foreign query while moving relinquishes control");
}
{
 var(f,b)=New(); b.TryBeginPath(start,end,1,0); b.Poll(2,1); f.Pending.SetResult([start,end]); b.Poll(2,2);
 Check(f.Token.IsCancellationRequested && f.MoveCalls==0 && f.StopCalls==0,"zone change cancels owned query and ignores latecompletion");
}
{
 var(f,b)=New(); b.TryBeginPath(start,end,1,0); b.Poll(1,15001); Check(b.State==VnavmeshState.Failed && f.Token.IsCancellationRequested && f.StopCalls==0,"query timeout cancels only owned CTS");
}
{
 var(f,b)=New(); Move(f,b); f.Active=false; b.Poll(1,2); Check(b.State==VnavmeshState.PathEnded && f.StopCalls==0,"path ended distinct from arrived or attuned");
}
{
 var(f,b)=New(); b.TryBeginPath(start,end,1,0); b.Dispose(); f.Pending.SetResult([start,end]); b.Poll(1,2);
 Check(f.MoveCalls==0 && f.StopCalls==0 && !b.TryBeginPath(start,end,1,4),"dispose prevents all late movement");
}
Check(!VnavmeshBridge.MatchesOwnedSuffix(new[]{start,end},Array.Empty<Vector3>()),"empty observed path is not ownership proof");
Check(!VnavmeshBridge.MatchesOwnedSuffix(new[]{start,end},new[]{end,start}),"reordered waypoints rejected");
Check(VnavmeshBridge.MatchesOwnedSuffix(new[]{start,middle,end},new[]{end}),"last remaining waypoint accepted");
Console.WriteLine($"Passed {count} vnavmesh contract checks. No live IPC invoked.");
sealed class Fake : IVnavmeshTransport
{
 public bool Available {get;set;}=true; public bool CanInvoke {get;set;}=true;
 public bool MeshReady=true,Active,Simple,Query; public int Queue,FindCalls,MoveCalls,StopCalls;
 public List<Vector3> Path=[]; public CancellationToken Token;
 public TaskCompletionSource<List<Vector3>> Pending=new();
 public bool Ready()=>MeshReady;public bool Running()=>Active;public bool SimpleQueryRunning()=>Simple;
 public bool QueryRunning()=>Query;public int QueuedQueries()=>Queue;public List<Vector3> Waypoints()=>Path.ToList();
 public Vector3? Floor(Vector3 point)=>point;
 public Task<List<Vector3>> Find(Vector3 from,Vector3 to,CancellationToken token) { FindCalls++;Token=token;return Pending.Task; }
 public void Move(List<Vector3> path) { MoveCalls++;Path=path;Active=true; }
 public void Stop() { StopCalls++;Active=false;Path=[]; }
}
