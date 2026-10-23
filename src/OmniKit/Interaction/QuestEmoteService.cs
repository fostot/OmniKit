using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using NativeEventHandler = FFXIVClientStructs.FFXIV.Client.Game.Event.EventHandler;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace OmniKit.Interaction;

/// <summary>
/// A Bridge Too Full's two soothing targets only. Framework-thread callers keep
/// fresh-press, combat/busy, distance, visibility and confirmed-target guards.
/// Observation never executes an emote or queries its immediate usability.
/// </summary>
internal sealed unsafe class QuestEmoteService(Configuration config)
{
    public string Status { get; private set; } = string.Empty;

    public bool CanOfferTarget(IGameObject target) => config.QuestEmoteUseEnabled
        && QuestEmotePolicy.MatchesTarget(target.BaseId)
        && QuestEmotePolicy.CanOffer(Capture(target, checkEmoteState: false));

    public QuestEmoteUseResult TryUse(IGameObject target)
    {
        Status = string.Empty;
        if (!config.QuestEmoteUseEnabled || !QuestEmotePolicy.MatchesTarget(target.BaseId))
            return QuestEmoteUseResult.NotApplicable;
        var context = Capture(target);
        var id = target.GameObjectId;
        var address = target.Address;
        var result = QuestEmotePolicy.TryUse(config.QuestEmoteUseEnabled, context, () =>
        {
            var current = Services.Objects.SearchById(id);
            var hardTarget = Services.Targets.Target;
            var manager = EmoteManager.Instance();
            if (current == null || !current.IsValid() || !current.IsTargetable || current.Address != address
                || current.BaseId != context.TargetDataId || hardTarget == null || !hardTarget.IsValid()
                || hardTarget.GameObjectId != id || hardTarget.Address != address || manager == null
                || EmoteController.PlayEmoteOption.StaticVirtualTablePointer == null) return false;
            var native = (NativeGameObject*)address;
            if (native == null || !native->GetIsTargetable()) return false;
            var options = new EmoteController.PlayEmoteOption
            {
                VirtualTable = EmoteController.PlayEmoteOption.StaticVirtualTablePointer,
                TargetId = native->GetGameObjectId(),
            };
            return manager->ExecuteEmote(QuestEmotePolicy.EmoteId, &options);
        });
        if (result == QuestEmoteUseResult.Blocked)
        {
            Status = QuestEmotePolicy.BlockReason(context);
            if (Status.Length == 0) Status = "The game rejected the emote action.";
        }
        return result;
    }

    private static QuestEmoteContext Capture(IGameObject target, bool checkEmoteState = true)
    {
        var value = new QuestEmoteContext(TargetDataId: target.BaseId,
            TerritoryId: Services.ClientState.TerritoryType, Targetable: false, QuestAccepted: false,
            Sequence: 0, ActiveQuestHandlers: 0, OtherActiveQuestHandlers: 0,
            MetadataMatches: false, EmoteUnlocked: false, EmoteAllowed: false);
        if (!QuestEmotePolicy.MatchesTarget(target.BaseId) || value.TerritoryId != QuestEmotePolicy.TerritoryId
            || target.ObjectKind != ObjectKind.EventNpc || !target.IsValid() || !target.IsTargetable
            || target.Address == 0) return value;
        var native = (NativeGameObject*)target.Address;
        if (!native->GetIsTargetable()) return value;
        value = value with { Targetable = true };
        var quests = QuestManager.Instance();
        if (quests == null || !quests->IsQuestAccepted(QuestEmotePolicy.QuestId)) return value;
        var quest = quests->GetQuestById(QuestEmotePolicy.QuestId);
        if (quest == null) return value;
        value = value with { QuestAccepted = true, Sequence = quest->Sequence };
        if (value.Sequence != QuestEmotePolicy.Sequence) return value;

        var handlers = stackalloc NativeEventHandler*[32];
        var count = native->GetEventHandlersImpl(handlers);
        if (count is < 1 or > 32) return value;
        var matching = 0;
        var others = 0;
        for (var i = 0; i < count; ++i)
        {
            var handler = handlers[i];
            if (handler == null || handler->Info.EventId.ContentId != EventHandlerContent.Quest
                || !handler->IsActive(native)) continue;
            if (handler->Info.EventId.Id == QuestEmotePolicy.QuestRowId
                && ((QuestEventHandler*)handler)->QuestId == QuestEmotePolicy.QuestId) ++matching;
            else ++others;
        }
        value = value with { ActiveQuestHandlers = matching, OtherActiveQuestHandlers = others };
        if (matching != 1 || others != 0) return value;

        var row = Services.Data.GetExcelSheet<Emote>().GetRowOrDefault(QuestEmotePolicy.EmoteId);
        if (row is not { } emote || emote.TextCommand.RowId != QuestEmotePolicy.TextCommandId || emote.UnlockLink != 0)
            return value;
        value = value with { MetadataMatches = true };
        if (!checkEmoteState) return value;
        var state = UIState.Instance();
        var manager = EmoteManager.Instance();
        return value with { EmoteUnlocked = state != null && state->IsEmoteUnlocked(QuestEmotePolicy.EmoteId),
            EmoteAllowed = manager != null && manager->CanExecuteEmote(QuestEmotePolicy.EmoteId) };
    }
}
