using System.Numerics;
using System.Text.Json.Nodes;
using OmniKit;
using OmniKit.Ui;

internal static class PolicyChecks
{
    public static void Run()
    {
        var count=0;
        void Check(bool value,string description) { count++; if(!value) throw new Exception(description); }
        var policy=new SeasonalCompanionPolicy();
        var october=new DateTimeOffset(2026,10,22,12,0,0,TimeSpan.Zero);
        SeasonalCompanionSnapshot Resolve(ushort[] world,ushort[]? ui=null,bool available=true,string selection="Auto",bool enabled=true,bool loggedIn=true,DateTimeOffset? time=null)
            =>policy.Resolve(enabled,selection,loggedIn,available,world.Select(id=>new SeasonalFestival(id,0)),
                (ui??[]).Select(id=>new SeasonalFestival(id,0)),time??october);
        ushort[] ids=[157,163,169,172,173,174,175,176,258];
        foreach(var pair in Enum.GetValues<SeasonalCompanionTheme>().Zip(ids))
        {
            var result=Resolve([pair.Second,pair.Second]);
            Check(result.CurrentTheme==pair.First && result.EligibleThemes.Length==1,"All nine native families with phase zero and duplicate slots.");
            Check(Resolve([],selection:pair.First.ToString()).CurrentTheme==pair.First,"Every manual theme.");
        }
        Check(Resolve([0]).CurrentTheme==null,"Confirmed empty native state never uses current date.");
        Check(Resolve([65000]).Evidence==SeasonalEvidence.Native && Resolve([65000]).CurrentTheme==null,"Unknown native state is not fallback.");
        foreach(var id in new ushort[]{11,22}) Check(Resolve([id]).CurrentTheme==null,"Ambiguous old Moonfire/Rising IDs.");
        foreach(var id in new ushort[]{113,145}) Check(Resolve([id]).EligibleThemes.Length==2 && Resolve([id]).CurrentTheme==null,"Combined festival offers both without arbitrary selection.");
        Check(Resolve([176,65000]).CurrentTheme==SeasonalCompanionTheme.AllSaintsWake,"Known and unknown evidence coexist.");
        Check(Resolve([176,11]).CurrentTheme==SeasonalCompanionTheme.AllSaintsWake,"Unresolved unrelated slot cannot suppress a known theme.");
        Check(Resolve([], [176]).Status.Contains("Native UI"),"UI-only evidence labelled.");
        Check(Resolve([176],[176]).Status.Contains("world + UI"),"Both sources retained.");
        Check(Resolve([176],[258]).CurrentTheme==null,"Cross-source disagreement not hidden.");
        Check(Resolve([3,6,7,30,39,84,147,148,257]).CurrentTheme==null,"Collaborations excluded.");
        Check(Resolve([176],selection:"0").CurrentTheme==null,"Numeric enum string rejected.");
        Check(Resolve([176],selection:"Moonfire").CurrentTheme==null,"Unrecognized configuration fails closed.");
        Check(Resolve([176],selection:"Starlight").CurrentTheme==SeasonalCompanionTheme.Starlight,"Manual wins explicitly.");
        Check(Resolve([176],enabled:false).Evidence==SeasonalEvidence.Disabled,"Off clears native theme.");
        Check(Resolve([176],selection:"Starlight",loggedIn:false).CurrentTheme==null,"Logout also clears manual theme.");
        var scheduled=Resolve([],available:false);
        Check(scheduled.CurrentTheme==SeasonalCompanionTheme.AllSaintsWake && scheduled.Evidence==SeasonalEvidence.OfficialSchedule
            && scheduled.OfficialSource?.StartsWith("https://na.finalfantasyxiv.com/")==true,"Explicit date fallback labelled and cited.");
        foreach(var window in SeasonalCompanionPolicy.OfficialWindows)
        {
            Check(Resolve([],available:false,time:window.Start).CurrentTheme==window.Theme,"Schedule start inclusive.");
            Check(Resolve([],available:false,time:window.End.AddTicks(-1)).CurrentTheme==window.Theme,"Schedule last instant included.");
            Check(Resolve([],available:false,time:window.Start.AddTicks(-1)).CurrentTheme!=window.Theme,"No early schedule.");
            Check(Resolve([],available:false,time:window.End).CurrentTheme!=window.Theme,"Schedule end exclusive.");
            Check(Resolve([],available:false,time:window.Start.AddYears(1)).CurrentTheme!=window.Theme,"No automatic annual recurrence.");
            Check(Resolve([258],time:window.Start).CurrentTheme==SeasonalCompanionTheme.Starlight,"Native beats different date.");
        }
        Check(Resolve([],available:false,time:new DateTimeOffset(2026,12,20,0,0,0,TimeSpan.Zero)).CurrentTheme==null,"No unverified Starlight schedule.");
        var future=new SeasonalCompanionPolicy([new(7000,248),new(7001,246),new(7001,247),new(7002,252),new(7003,241)]);
        SeasonalCompanionSnapshot Local(ushort id)=>future.Resolve(true,"Auto",true,true,[new(id,99)],[],october);
        Check(Local(7000).CurrentTheme==SeasonalCompanionTheme.AllSaintsWake,"New local static festival relation works without guessing numeric order.");
        Check(Local(7001).CurrentTheme==null,"Conflicting new relation stays ambiguous.");
        Check(Local(7002).CurrentTheme==null,"New collaboration excluded.");
        Check(Local(7003).EligibleThemes.Length==2,"Explicit combined local category recognized.");
        var service=new SeasonalCompanionService(new Configuration());
        service.Clear(); Check(service.CurrentTheme==null && service.Snapshot.Evidence==SeasonalEvidence.LoggedOut,"Clear requires no native call.");

        var path=Path.GetFullPath(".local/seasonal-companion-tests/invalid-metadata");Directory.CreateDirectory(path);
        var text=File.ReadAllText("assets/seasonal-accessories.frames.json");
        void Invalid(Action<JsonObject> mutate,string description)
        {
            var node=JsonNode.Parse(text)!.AsObject();mutate(node);
            File.WriteAllText(Path.Combine(path,"seasonal-accessories.frames.json"),node.ToJsonString());
            Check(!new SeasonalCompanionRenderer(path).IsAvailable,description);
        }
        Invalid(n=>n["File"]="../external.png","Metadata cannot select paths.");
        Invalid(n=>n["Themes"]![0]!["Bounds"]![0]=-1,"Negative crop rejected.");
        Invalid(n=>n["Themes"]![0]!["Id"]="0","Numeric theme rejected.");
        Invalid(n=>n["Profiles"]![0]!["NormalSourceAnchors"]![0]![0]=9000,"Out-of-source anchor rejected.");
        Invalid(n=>n["Profiles"]![1]!["Avatar"]="Moonblob","Duplicate avatar rejected.");
        var art=new SeasonalCompanionRenderer("assets");
        Check(!art.TryGetQuad(SeasonalCompanionTheme.Starlight,"Moonblob",0,false,default,out _),"Degenerate transform rejected.");
        Check(!art.TryGetQuad(SeasonalCompanionTheme.Starlight,"Moonblob",99,false,new(new Vector4(0,0,1000,1000),300,Vector2.Zero,Vector2.UnitX,Vector2.UnitY),out _),"Invalid pose rejected.");
        Console.WriteLine($"PASS: {count} seasonal policy/metadata checks; service native bindings compile only, no game or network calls.");
    }
}
