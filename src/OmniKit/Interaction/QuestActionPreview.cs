namespace OmniKit.Interaction;

internal enum QuestActionKind { EventItem, Emote }

/// <summary>Read-only advice, never an execution token. A press captures fresh facts.</summary>
internal readonly record struct QuestActionPreview(QuestActionKind Kind, uint Id, uint IconId,
    string Name, bool CanUse, string Reason)
{
    public static QuestActionPreview? ForItem(QuestItemContext context, uint iconId, string name)
        => QuestItemPolicy.RuleForTarget(context.TargetDataId) is { } rule
            ? new(QuestActionKind.EventItem, rule.ItemId, iconId, name,
                QuestItemPolicy.CanUse(context), QuestItemPolicy.BlockReason(context))
            : null;

    public static QuestActionPreview? ForEmote(QuestEmoteContext context, uint iconId, string name)
        => QuestEmotePolicy.MatchesTarget(context.TargetDataId)
            ? new(QuestActionKind.Emote, QuestEmotePolicy.EmoteId, iconId, name,
                QuestEmotePolicy.CanUse(context), QuestEmotePolicy.BlockReason(context))
            : null;
}
