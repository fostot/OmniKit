namespace OmniKit.Interaction;

internal enum QuestItemUseResult { NotApplicable, Used, Blocked }
internal readonly record struct QuestItemRule(ushort QuestId, uint QuestRowId, byte Sequence,
    uint TerritoryId, uint ItemId, uint ActionId);

/// <summary>Facts captured together immediately before a user-requested item action.</summary>
internal readonly record struct QuestItemContext(
    uint TargetDataId, uint TerritoryId, bool Targetable, bool QuestAccepted, byte Sequence,
    int ActiveQuestHandlers, int OtherActiveQuestHandlers, int TrackedQuestMatches,
    int TrackedItemMatches, int OwnedKeyItems, bool MetadataMatches, bool GroundTargeted,
    uint ActionStatus);

internal static class QuestItemPolicy
{
    // Independent game-data provenance and deliberate scope: docs/QUEST-ITEMS.md.
    private static readonly QuestItemRule Shovel = new(471, 66007, 2, 134, 2000350, 1);
    private static readonly QuestItemRule DodoFlesh = new(416, 65952, 1, 135, 2000221, 1);

    public static QuestItemRule? RuleForTarget(uint dataId) => dataId switch
    {
        2001243 or 2001244 or 2001245 => Shovel,
        2000772 or 2000773 or 2000774 => DodoFlesh,
        _ => null,
    };

    public static bool MatchesTarget(uint dataId) => RuleForTarget(dataId).HasValue;

    public static bool CanOffer(QuestItemContext value) => RuleForTarget(value.TargetDataId) is { } rule
        && value.TerritoryId == rule.TerritoryId && value.Targetable && value.QuestAccepted
        && value.Sequence == rule.Sequence && value.ActiveQuestHandlers == 1
        && value.OtherActiveQuestHandlers == 0 && value.TrackedQuestMatches == 1
        && value.TrackedItemMatches == 1 && value.OwnedKeyItems > 0
        && value.MetadataMatches && !value.GroundTargeted;

    public static bool CanUse(QuestItemContext value) => CanOffer(value) && value.ActionStatus == 0;

    public static string BlockReason(QuestItemContext value)
    {
        if (RuleForTarget(value.TargetDataId) is not { } rule || value.TerritoryId != rule.TerritoryId || !value.Targetable)
            return "The target is no longer available for this item action.";
        if (!value.QuestAccepted || value.Sequence != rule.Sequence)
            return "The required quest step is not active.";
        if (value.ActiveQuestHandlers != 1 || value.OtherActiveQuestHandlers != 0)
            return "The target's quest context is missing or ambiguous.";
        if (value.TrackedQuestMatches != 1)
            return "The active quest must appear once in the quest tracker.";
        if (value.TrackedItemMatches != 1)
            return "The quest tracker does not show a single matching item.";
        if (value.OwnedKeyItems <= 0)
            return "The required key item is not in your inventory.";
        if (!value.MetadataMatches)
            return "The item mapping could not be verified.";
        if (value.GroundTargeted)
            return "Ground-targeted quest items are not supported.";
        if (value.ActionStatus != 0)
            return "The item is not usable on this target right now. Check your range and state.";
        return string.Empty;
    }

    public static QuestItemUseResult TryUse(bool enabled, QuestItemContext value, Func<QuestItemRule, bool> use)
    {
        if (!enabled || RuleForTarget(value.TargetDataId) is not { } rule) return QuestItemUseResult.NotApplicable;
        // A matched rule owns this press even when validation or the native call
        // fails. The caller must not follow it with a second, ordinary interaction.
        if (!CanUse(value)) return QuestItemUseResult.Blocked;
        return use(rule) ? QuestItemUseResult.Used : QuestItemUseResult.Blocked;
    }
}
