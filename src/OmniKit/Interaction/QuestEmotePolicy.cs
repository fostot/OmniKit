namespace OmniKit.Interaction;

internal enum QuestEmoteUseResult { NotApplicable, Used, Blocked }

internal readonly record struct QuestEmoteContext(
    uint TargetDataId, uint TerritoryId, bool Targetable, bool QuestAccepted, byte Sequence,
    int ActiveQuestHandlers, int OtherActiveQuestHandlers, bool MetadataMatches,
    bool EmoteUnlocked, bool EmoteAllowed);

internal static class QuestEmotePolicy
{
    // Independent extracted game-data facts; see docs/QUEST-EMOTES.md.
    public const ushort QuestId = 407;
    public const uint QuestRowId = 65943;
    public const byte Sequence = 2;
    public const uint TerritoryId = 135;
    public const ushort EmoteId = 35;
    public const uint TextCommandId = 436;

    public static bool MatchesTarget(uint dataId) => dataId is 1002449 or 1002450;

    public static bool CanOffer(QuestEmoteContext value) => MatchesTarget(value.TargetDataId)
        && value.TerritoryId == TerritoryId && value.Targetable && value.QuestAccepted
        && value.Sequence == Sequence && value.ActiveQuestHandlers == 1
        && value.OtherActiveQuestHandlers == 0 && value.MetadataMatches;

    public static bool CanUse(QuestEmoteContext value) => CanOffer(value) && value.EmoteUnlocked && value.EmoteAllowed;

    public static string BlockReason(QuestEmoteContext value)
    {
        if (!MatchesTarget(value.TargetDataId) || value.TerritoryId != TerritoryId || !value.Targetable)
            return "The target is no longer available for this emote.";
        if (!value.QuestAccepted || value.Sequence != Sequence)
            return "The required quest step is not active.";
        if (value.ActiveQuestHandlers != 1 || value.OtherActiveQuestHandlers != 0)
            return "The target's quest context is missing or ambiguous.";
        if (!value.MetadataMatches) return "The required emote mapping could not be verified.";
        if (!value.EmoteUnlocked) return "The required emote is not unlocked.";
        if (!value.EmoteAllowed) return "The game does not allow this emote right now.";
        return string.Empty;
    }

    public static QuestEmoteUseResult TryUse(bool enabled, QuestEmoteContext value, Func<bool> use)
    {
        if (!enabled || !MatchesTarget(value.TargetDataId)) return QuestEmoteUseResult.NotApplicable;
        // A matched request owns this press, including a rejected native action.
        // Never follow it with dialogue, a second emote, or an ordinary interaction.
        if (!CanUse(value)) return QuestEmoteUseResult.Blocked;
        return use() ? QuestEmoteUseResult.Used : QuestEmoteUseResult.Blocked;
    }
}
