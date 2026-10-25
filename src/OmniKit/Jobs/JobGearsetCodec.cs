namespace OmniKit.Jobs;

/// <summary>Pure native-table projection and gearset ID encoding used by the adapter.</summary>
internal static class JobGearsetCodec
{
    internal static int[] FreeSlots(ReadOnlySpan<byte> flags, int permittedCount, int firstEmpty)
    {
        var limit=Math.Min(flags.Length,Math.Max(0,permittedCount));
        if(firstEmpty<0 || firstEmpty>=limit || firstEmpty==255 || (flags[firstEmpty]&1)!=0) return [];
        var result=new List<int>();
        for(var i=firstEmpty;i<limit;i++) if((flags[i]&1)==0) result.Add(i);
        return result.ToArray();
    }
    internal static bool CanCreateAt(int planned, int firstEmpty, ReadOnlySpan<byte> flags)
        => planned>=0 && planned<flags.Length && firstEmpty!=255 && planned==firstEmpty && (flags[planned]&1)==0;

    // InventoryItem.ItemId is the physical base ID. Gearset entries encode HQ in the ID.
    internal static uint GearsetItemId(uint baseItemId, bool highQuality)
        => baseItemId==0 ? 0 : baseItemId+(highQuality?1_000_000u:0u);
    internal static bool EquipmentTransitionAllowed(ReadOnlySpan<string> original, ReadOnlySpan<string> target,
        ReadOnlySpan<string> current)
    {
        if(original.Length!=14 || target.Length!=14 || current.Length!=14) return false;
        for(var i=0;i<14;i++)
            if(current[i]!=original[i] && current[i]!=target[i]) return false;
        return true;
    }
    internal static bool SelectionTransitionAllowed(int current, int prior, int intended)
        => current==prior || current==intended;}

