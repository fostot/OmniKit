using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using NativeEventHandler = FFXIVClientStructs.FFXIV.Client.Game.Event.EventHandler;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace OmniKit.Interaction;

/// <summary>
/// The first verified item/target rule only. Framework-thread callers retain
/// fresh-press, combat/busy, range, visibility and confirmed-target guards.
/// Looking may query eligibility; only TryUse can issue an item action.
/// </summary>
internal sealed unsafe class QuestItemService(Configuration config)
{
    public string Status { get; private set; } = string.Empty;

    public bool CanOfferTarget(IGameObject target) => config.QuestItemUseEnabled
        && QuestItemPolicy.MatchesTarget(target.BaseId) && QuestItemPolicy.CanOffer(Capture(target, checkActionStatus: false));

    public QuestItemUseResult TryUse(IGameObject target)
    {
        Status = string.Empty;
        if (!config.QuestItemUseEnabled || !QuestItemPolicy.MatchesTarget(target.BaseId))
            return QuestItemUseResult.NotApplicable;
        var context = Capture(target);
        var id = target.GameObjectId;
        var address = target.Address;
        var result = QuestItemPolicy.TryUse(config.QuestItemUseEnabled, context, () =>
        {
            var current = Services.Objects.SearchById(id);
            var hardTarget = Services.Targets.Target;
            var manager = ActionManager.Instance();
            if (current == null || !current.IsValid() || !current.IsTargetable || current.Address != address
                || hardTarget == null || hardTarget.GameObjectId != id || hardTarget.Address != address
                || manager == null) return false;
            return manager->UseAction(ActionType.EventItem, QuestItemPolicy.ItemId, id, 0xFFFF);
        });
        if (result == QuestItemUseResult.Blocked)
        {
            Status = QuestItemPolicy.BlockReason(context);
            if (Status.Length == 0) Status = "The game rejected the item action.";
        }
        return result;
    }

    private static QuestItemContext Capture(IGameObject target, bool checkActionStatus = true)
    {
        var value = new QuestItemContext(TargetDataId: target.BaseId,
            TerritoryId: Services.ClientState.TerritoryType, Targetable: false, QuestAccepted: false,
            Sequence: 0, ActiveQuestHandlers: 0, OtherActiveQuestHandlers: 0,
            TrackedQuestMatches: 0, TrackedItemMatches: 0, OwnedKeyItems: 0,
            MetadataMatches: false, GroundTargeted: true, ActionStatus: uint.MaxValue);
        if (!QuestItemPolicy.MatchesTarget(target.BaseId) || value.TerritoryId != QuestItemPolicy.TerritoryId
            || target.ObjectKind != ObjectKind.EventObj || !target.IsValid() || !target.IsTargetable
            || target.Address == 0) return value;
        var native = (NativeGameObject*)target.Address;
        if (!native->GetIsTargetable()) return value;
        value = value with { Targetable = true };

        var quests = QuestManager.Instance();
        var state = UIState.Instance();
        var inventory = InventoryManager.Instance();
        var manager = ActionManager.Instance();
        if (quests == null || state == null || inventory == null || manager == null
            || !quests->IsQuestAccepted(QuestItemPolicy.QuestId)) return value;
        var quest = quests->GetQuestById(QuestItemPolicy.QuestId);
        if (quest == null) return value;
        value = value with { QuestAccepted = true, Sequence = quest->Sequence };
        if (value.Sequence != QuestItemPolicy.Sequence) return value;

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
            if (handler->Info.EventId.Id == QuestItemPolicy.QuestRowId
                && ((QuestEventHandler*)handler)->QuestId == QuestItemPolicy.QuestId) ++matching;
            else ++others;
        }
        value = value with { ActiveQuestHandlers = matching, OtherActiveQuestHandlers = others };
        if (matching != 1 || others != 0) return value;

        ref var tracked = ref state->QuestTodoList.Todo.TrackedQuests;
        if (tracked.LongCount is < 0 or > 30) return value;
        var trackedQuests = 0;
        var trackedItems = 0;
        for (var i = 0; i < tracked.Count; ++i)
        {
            ref var entry = ref tracked[i];
            if (entry.QuestId != QuestItemPolicy.QuestId || entry.Sequence != QuestItemPolicy.Sequence) continue;
            ++trackedQuests;
            if (entry.ItemCount is < 0 or > 8) return value;
            foreach (var item in entry.ItemIds[..entry.ItemCount])
                if (item == QuestItemPolicy.ItemId) ++trackedItems;
        }
        value = value with { TrackedQuestMatches = trackedQuests, TrackedItemMatches = trackedItems,
            OwnedKeyItems = inventory->GetItemCountInContainer(QuestItemPolicy.ItemId, InventoryType.KeyItems) };
        if (trackedQuests != 1 || trackedItems != 1 || value.OwnedKeyItems <= 0) return value;

        var itemRow = Services.Data.GetExcelSheet<EventItem>().GetRowOrDefault(QuestItemPolicy.ItemId);
        if (itemRow is not { } itemData || itemData.Quest.RowId != QuestItemPolicy.QuestRowId
            || itemData.Action.RowId != QuestItemPolicy.ActionId || itemData.Action.ValueNullable is not { } action)
            return value;
        return value with { MetadataMatches = true, GroundTargeted = action.TargetArea,
            ActionStatus = checkActionStatus ? manager->GetActionStatus(ActionType.EventItem, QuestItemPolicy.ItemId, target.GameObjectId) : uint.MaxValue };
    }
}
