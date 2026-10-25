using System.Numerics;

namespace OmniKit.Unlocks;

internal enum VnavmeshState { Idle, FindingPath, Moving, PathEnded, Failed, Cancelled, OwnershipLost }

internal interface IVnavmeshTransport
{
    bool Available { get; }
    bool CanInvoke { get; }
    bool Ready();
    bool Running();
    bool SimpleQueryRunning();
    bool QueryRunning();
    int QueuedQueries();
    List<Vector3> Waypoints();
    Vector3? Floor(Vector3 point);
    Task<List<Vector3>> Find(Vector3 from,Vector3 to,CancellationToken token);
    void Move(List<Vector3> path);
    void Stop();
}

/// <summary>
/// Uses an owned cancellable query, then dispatches a single ground path. vnavmesh has no IPC ownership token:
/// path ownership is best-effort suffix verification and requires other navigation automations to remain idle.
/// </summary>
internal sealed class VnavmeshBridge : IDisposable
{
    private readonly IVnavmeshTransport transport;
    private Task<List<Vector3>>? query;
    private CancellationTokenSource? cancellation;
    private Vector3[] owned=[];
    private uint territory;
    private Vector3 destination;
    private long deadline;
    private bool disposed;
    public VnavmeshBridge() : this(new DalamudVnavmeshTransport()) { }
    internal VnavmeshBridge(IVnavmeshTransport transport)=>this.transport=transport;
    public VnavmeshState State { get; private set; }
    public string Status { get; private set; }="vnavmesh is idle.";
    public bool OwnedActive=>State is VnavmeshState.FindingPath or VnavmeshState.Moving;
    public bool BlockedByOtherNavigation { get; private set; }
    public bool DependencyAvailable=>!disposed && transport.Available;
    public bool IsReady=>Read(transport.Ready);
    public bool IsRunning=>Read(transport.Running);
    public bool IsIdle
    {
        get
        {
            if(!DependencyAvailable || !transport.CanInvoke) return false;
            try
            {
                if(!transport.Ready()) return false;
                if(transport.Running() || transport.SimpleQueryRunning() || transport.QueryRunning() || transport.QueuedQueries()!=0)
                { Status="Another navigation operation is active. Stop it before continuing this route."; return false; }
                return true;
            }
            catch { Status="The navigation state could not be verified."; return false; }
        }
    }
    public Vector3? PointOnFloor(Vector3 point)
    {
        if(!Valid(point) || !DependencyAvailable || !transport.CanInvoke) return null;
        try { return transport.Floor(point) is { } result && Valid(result)?result:null; }
        catch { Status="vnavmesh floor query is unavailable."; return null; }
    }
    public bool TryBeginPath(Vector3 start,Vector3 end,uint currentTerritory)
        =>TryBeginPath(start,end,currentTerritory,Environment.TickCount64);
    internal bool TryBeginPath(Vector3 start,Vector3 end,uint currentTerritory,long now)
    {
        if(disposed || OwnedActive) return false;
        BlockedByOtherNavigation=false;
        if(currentTerritory==0 || !Valid(start) || !Valid(end)) return Failure("The path endpoints or area are unavailable.");
        if(!DependencyAvailable || !transport.CanInvoke) return Failure("Enable vnavmesh before starting a route.");
        try
        {
            if(!transport.Ready()) return Failure("Wait for the current area's navigation mesh.");
            if(transport.Running() || transport.SimpleQueryRunning() || transport.QueryRunning() || transport.QueuedQueries()!=0)
            { BlockedByOtherNavigation=true; return Failure("Another navigation operation is active. Stop it before starting this route."); }
            cancellation=new(); territory=currentTerritory; destination=end; owned=[];
            query=transport.Find(start,end,cancellation.Token);
            deadline=now+15000; State=VnavmeshState.FindingPath; Status="Finding a ground path.";
            return true;
        }
        catch { ReleaseQuery(); return Failure("vnavmesh rejected the path query."); }
    }
    public VnavmeshState Poll(uint currentTerritory)=>Poll(currentTerritory,Environment.TickCount64);
    internal VnavmeshState Poll(uint currentTerritory,long now)
    {
        if(!OwnedActive) return State;
        if(disposed || currentTerritory!=territory || !DependencyAvailable || !transport.CanInvoke)
        { CancelWithoutGlobalStop("The area or navigation dependency changed."); return State; }
        try
        {
            if(!transport.Ready()) { CancelWithoutGlobalStop("The navigation mesh changed."); return State; }
            if(now>deadline)
            {
                Stop();
                if(State!=VnavmeshState.OwnershipLost) { State=VnavmeshState.Failed; Status="The navigation operation timed out."; }
                return State;
            }
            if(State==VnavmeshState.FindingPath)
            {
                if(query==null) { Failure("The owned query is unavailable."); return State; }
                if(!query.IsCompleted) return State;
                if(query.IsCanceled || query.IsFaulted)
                { _=query.Exception; ReleaseQuery(); Failure("No usable ground path was found."); return State; }
                var result=query.GetAwaiter().GetResult(); ReleaseQuery();
                if(result.Count is <1 or >4096 || result.Any(point=>!Valid(point)) || Vector3.Distance(result[^1],destination)>2)
                { Failure("The path does not reach the requested destination."); return State; }
                if(transport.Running() || transport.SimpleQueryRunning() || transport.QueryRunning() || transport.QueuedQueries()!=0)
                { LoseOwnership("Another navigation operation started while the path was being calculated."); return State; }
                owned=result.ToArray();
                transport.Move(result.ToList());
                State=VnavmeshState.Moving; deadline=now+180000; Status="Following the ground path.";
                return State;
            }
            if(!transport.Running())
            { owned=[]; State=VnavmeshState.PathEnded; Status="The path ended. Verify arrival before continuing."; return State; }
            if(transport.SimpleQueryRunning() || transport.QueryRunning() || transport.QueuedQueries()!=0
                || !MatchesOwnedSuffix(owned,transport.Waypoints()))
                LoseOwnership("Navigation changed outside this route; automatic control ended.");
        }
        catch { LoseOwnership("The navigation state could not be verified; automatic control ended."); }
        return State;
    }
    /// <returns>True when an owned query was cancelled or a matching active path was stopped.</returns>
    public bool Stop()
    {
        if(State==VnavmeshState.FindingPath)
        { CancelWithoutGlobalStop("The owned path query was cancelled."); return true; }
        if(State!=VnavmeshState.Moving) return false;
        try
        {
            if(!DependencyAvailable || !transport.CanInvoke || transport.SimpleQueryRunning() || transport.QueryRunning() || transport.QueuedQueries()!=0
                || !transport.Running() || !MatchesOwnedSuffix(owned,transport.Waypoints()))
            { LoseOwnership("The active path no longer matches this route; no global stop was sent."); return false; }
            transport.Stop(); owned=[]; State=VnavmeshState.Cancelled; Status="The route path was stopped.";
            return true;
        }
        catch { LoseOwnership("Could not verify the active path; no further stop was attempted."); return false; }
    }
    private bool Read(Func<bool> read)
    {
        if(!DependencyAvailable || !transport.CanInvoke) return false;
        try { return read(); } catch { Status="vnavmesh IPC is unavailable."; return false; }
    }
    internal static bool MatchesOwnedSuffix(IReadOnlyList<Vector3> expected,IReadOnlyList<Vector3> observed)
    {
        if(observed.Count==0 || observed.Count>expected.Count) return false;
        var offset=expected.Count-observed.Count;
        for(var i=0;i<observed.Count;i++) if(observed[i]!=expected[offset+i]) return false;
        return true;
    }
    private static bool Valid(Vector3 point)=>float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);
    private bool Failure(string reason) { State=VnavmeshState.Failed; Status=reason; return false; }
    private void LoseOwnership(string reason) { ReleaseQuery(); owned=[]; State=VnavmeshState.OwnershipLost; Status=reason; }
    private void CancelWithoutGlobalStop(string reason) { ReleaseQuery(); owned=[]; State=VnavmeshState.Cancelled; Status=reason; }
    private void ReleaseQuery()
    {
        var source=cancellation; cancellation=null;
        try { source?.Cancel(); } catch(AggregateException) { /* Late query results are ignored regardless of provider callbacks. */ }
        finally { source?.Dispose(); }
        if(query is { IsFaulted:true }) _=query.Exception;
        query=null;
    }
    public void Dispose() { if(disposed) return; Stop(); disposed=true; ReleaseQuery(); }
}

internal sealed class DalamudVnavmeshTransport : IVnavmeshTransport
{
    public bool Available=>Services.PluginInterface.InstalledPlugins.Any(plugin=>plugin.InternalName=="vnavmesh" && plugin.IsLoaded);
    public bool CanInvoke=>Services.Framework.IsInFrameworkUpdateThread;
    public bool Ready()=>Services.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady").InvokeFunc();
    public bool Running()=>Services.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning").InvokeFunc();
    public bool SimpleQueryRunning()=>Services.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress").InvokeFunc();
    public bool QueryRunning()=>Services.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.PathfindInProgress").InvokeFunc();
    public int QueuedQueries()=>Services.PluginInterface.GetIpcSubscriber<int>("vnavmesh.Nav.PathfindNumQueued").InvokeFunc();
    public List<Vector3> Waypoints()=>Services.PluginInterface.GetIpcSubscriber<List<Vector3>>("vnavmesh.Path.ListWaypoints").InvokeFunc();
    public Vector3? Floor(Vector3 point)=>Services.PluginInterface.GetIpcSubscriber<Vector3,bool,float,Vector3?>("vnavmesh.Query.Mesh.PointOnFloor").InvokeFunc(point,false,2f);
    public Task<List<Vector3>> Find(Vector3 from,Vector3 to,CancellationToken token)
        =>Services.PluginInterface.GetIpcSubscriber<Vector3,Vector3,bool,CancellationToken,Task<List<Vector3>>>("vnavmesh.Nav.PathfindCancelable").InvokeFunc(from,to,false,token);
    public void Move(List<Vector3> path)=>Services.PluginInterface.GetIpcSubscriber<List<Vector3>,bool,object>("vnavmesh.Path.MoveTo").InvokeAction(path,false);
    public void Stop()=>Services.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction();
}
