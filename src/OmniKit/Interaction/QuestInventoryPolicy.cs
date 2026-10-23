namespace OmniKit.Interaction;

internal enum QuestInventoryKind { EventItem, Item }
internal enum QuestInventoryContainer { KeyItems, Inventory1, Inventory2, Inventory3, Inventory4 }

internal readonly record struct QuestInventoryTarget(ulong Id, nint Address, uint TerritoryId)
{
    public bool IsValid => Id is not (0 or 0xE0000000) && Address != 0 && TerritoryId != 0;
}

/// <summary>Transient immutable inventory advice. Never persisted or sent to an AI provider.</summary>
internal readonly record struct QuestInventoryEntry(QuestInventoryKind Kind, uint BaseId, uint UseId,
    QuestInventoryContainer Container, ushort Slot, string Name, uint IconId, int Count,
    bool CanUse, string Reason, bool TrackedQuest, QuestInventoryTarget Target, long Generation);

internal readonly record struct QuestInventoryUseContext(bool Enabled, bool LoggedIn, bool ChangingAreas,
    long Generation, bool PresentInSnapshot, QuestInventoryTarget Target, bool SlotMatches, int Count,
    bool MetadataAllowed, bool TrackedQuest, bool ActionBusy, uint ActionStatus);

internal static class QuestInventoryPolicy
{
    public static bool CanList(QuestInventoryKind kind, QuestInventoryContainer container,
        uint baseId, uint useId, int count, bool metadataAllowed, bool trackedQuest)
        => baseId != 0 && useId != 0 && count > 0 && metadataAllowed && (kind switch
        {
            QuestInventoryKind.EventItem => container == QuestInventoryContainer.KeyItems && baseId == useId,
            QuestInventoryKind.Item => container is >= QuestInventoryContainer.Inventory1
                and <= QuestInventoryContainer.Inventory4 && trackedQuest,
            _ => false,
        });

    public static string BlockReason(QuestInventoryEntry entry, QuestInventoryUseContext current)
    {
        if (!current.Enabled) return "The quest item bar is disabled.";
        if (!current.LoggedIn || current.ChangingAreas) return "Wait until the area is ready.";
        if (entry.Generation != current.Generation || !current.PresentInSnapshot)
            return "The item list changed. Choose the item again.";
        if (!entry.Target.IsValid) return "Select a target first.";
        if (!current.Target.IsValid || entry.Target != current.Target)
            return "The target changed. Choose the item again.";
        if (!current.SlotMatches || current.Count <= 0) return "The inventory slot changed. Choose the item again.";
        if (!CanList(entry.Kind, entry.Container, entry.BaseId, entry.UseId,
                current.Count, current.MetadataAllowed, current.TrackedQuest))
            return "This item is no longer a supported quest item.";
        if (current.ActionBusy) return "Wait for the current action to finish.";
        if (current.ActionStatus != 0) return "The item is not usable on this target right now. Check your range and state.";
        return string.Empty;
    }

    public static QuestItemUseResult TryUse(QuestInventoryEntry entry, QuestInventoryUseContext current, Func<bool> use)
    {
        if (!current.Enabled) return QuestItemUseResult.NotApplicable;
        if (BlockReason(entry, current).Length != 0) return QuestItemUseResult.Blocked;
        return use() ? QuestItemUseResult.Used : QuestItemUseResult.Blocked;
    }
}
