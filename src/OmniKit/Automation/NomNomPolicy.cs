namespace OmniKit.Automation;

internal sealed record NomNomFoodOption(uint ItemId,string Name,uint IconId,int NormalCount,int HighQualityCount,bool GrantsExperience);
internal sealed record NomNomCandidate(uint ItemId,uint UseId,int Container,ushort Slot,int Count,bool HighQuality,string Name,bool GrantsExperience,
    int ExperienceBonus = 0,int ItemLevel = 0);
internal readonly record struct NomNomSettings(bool Enabled,uint FoodId,bool PreferHighQuality,bool RequireExperience);
internal readonly record struct NomNomIdentity(ulong Character,ulong Object,uint Territory);
internal readonly record struct NomNomObservation(NomNomIdentity Identity,bool Safe,bool WellFed,int SelectedCount,
    NomNomCandidate? Candidate,bool NativeUsable);

/// <summary>One attempt per missing-buff episode. Failed attempts latch until deliberate settings or stock changes.</summary>
internal sealed class NomNomController
{
    private NomNomSettings? settings;
    private NomNomIdentity identity;
    private bool failed;
    private int failedCount;
    private long deadline;
    private long nextAttempt;
    private bool consumed;
    private int countBefore;
    private bool attempted;
    private bool cancelled;
    public bool Busy { get; private set; }
    public string Status { get; private set; }="Automatic food selection is ready.";
    public void Update(NomNomSettings selected,NomNomObservation o,long now,Func<NomNomCandidate,bool> use)
    {
        if(cancelled) return;
        if(settings!=selected || identity!=o.Identity)
        {
            settings=selected; identity=o.Identity; Busy=false; failed=false; consumed=false; attempted=false; nextAttempt=now+2000;
        }
        if(!selected.Enabled) { Busy=false; Status="Nom Nom is off."; return; }
        if(!selected.RequireExperience) { Busy=false; Status="EXP food maintenance is off."; return; }
        if(o.Identity.Character==0 || o.Identity.Object==0) { Busy=false; Status="Waiting for your character."; return; }
        if(o.WellFed)
        {
            Busy=false; failed=false; consumed=false; attempted=false; nextAttempt=Math.Max(nextAttempt,now+2000);
            Status="Well Fed is active. No food needed."; return;
        }
        if(Busy)
        {
            if(!o.Safe) { Latch(o.SelectedCount,"Eating paused; change the food, toggle Nom Nom, or change its stock to try again."); return; }
            if(o.SelectedCount<countBefore) consumed=true;
            if(now>deadline)
                Latch(o.SelectedCount,consumed?"Food was consumed, but Well Fed was not confirmed. Automatic eating is paused.":"The game did not confirm eating. Automatic eating is paused.");
            return;
        }
        if(failed)
        {
            // A delayed scan observing our own single consumed item must not re-arm a failed attempt.
            if(o.SelectedCount==failedCount || attempted && o.SelectedCount==countBefore-1)
            { failedCount=o.SelectedCount; return; }
            failed=false; nextAttempt=Math.Max(nextAttempt,now+5000);
        }
        if(o.Candidate==null || o.SelectedCount<=0)
        { Status=selected.FoodId==0 ? "No eligible EXP food is available in your personal bags." : "The selected food is not available in your personal bags."; return; }
        if(selected.FoodId!=0 && o.Candidate.ItemId!=selected.FoodId) { Latch(o.SelectedCount,"The selected food changed. Choose it again."); return; }
        if(!o.Candidate.GrantsExperience)
        { Status="The selected food has no EXP bonus. Choose another food."; return; }
        if(!o.Safe) { Status="Eating is paused while the game is busy or unfocused."; return; }
        if(!o.NativeUsable) { Status="The selected food is not usable right now."; return; }
        if(now<nextAttempt) { Status="Waiting briefly before checking food."; return; }
        countBefore=o.SelectedCount; consumed=false; attempted=true; nextAttempt=now+5000;
        if(!use(o.Candidate)) { Latch(o.SelectedCount,"The game rejected eating. Automatic eating is paused."); return; }
        Busy=true; deadline=now+8000; Status="Waiting for Well Fed after eating.";
    }
    private void Latch(int count,string reason) { Busy=false; failed=true; failedCount=count; Status=reason; }
    public void Cancel() { cancelled=true; Busy=false; failed=true; Status="Automatic eating stopped."; }
}



internal static class NomNomFoodPolicy
{
    internal static bool IsSupportedFood(uint actionId,uint statusId,bool validFoodRow,bool targetArea)
        => actionId is 844 or 845 && statusId==48 && validFoodRow && !targetArea;
    internal static NomNomCandidate? SelectCandidate(IEnumerable<NomNomCandidate> items,uint selected,bool preferHq)
        => RankCandidates(items,selected,preferHq).FirstOrDefault();
    internal static IEnumerable<NomNomCandidate> RankCandidates(IEnumerable<NomNomCandidate> items,uint selected,bool preferHq)
    {
        var available=items.Where(item=>item.ItemId!=0 && item.Count>0);
        if(selected==0)
            return available.Where(item=>item.GrantsExperience)
                .OrderByDescending(item=>item.ExperienceBonus).ThenByDescending(item=>item.ItemLevel)
                .ThenBy(item=>item.HighQuality==preferHq?0:1).ThenBy(item=>item.ItemId)
                .ThenBy(item=>item.Container).ThenBy(item=>item.Slot);
        return available.Where(item=>item.ItemId==selected)
            .OrderBy(item=>item.HighQuality==preferHq?0:1).ThenBy(item=>item.Container).ThenBy(item=>item.Slot);
    }
    internal static uint UseId(uint baseId,bool hq)=>baseId+(hq?1_000_000u:0u);
}

