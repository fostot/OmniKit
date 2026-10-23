using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace OmniKit.Interaction;

/// <summary>
/// Framework-thread scanner and manual item executor. The caller must retain its
/// global fresh-click, combat/busy and text-input guards before calling TryUse.
/// No scan or preview executes an action or changes the target.
/// </summary>
internal sealed unsafe class QuestInventoryService
{
    private static readonly QuestInventoryContainer[] Containers =
        [QuestInventoryContainer.KeyItems, QuestInventoryContainer.Inventory1, QuestInventoryContainer.Inventory2,
            QuestInventoryContainer.Inventory3, QuestInventoryContainer.Inventory4];
    private IReadOnlyList<QuestInventoryEntry> snapshot = Array.Empty<QuestInventoryEntry>();
    private bool enabled;
    private uint territory;
    private long generation;
    private long nextScan;
    private string status = string.Empty;

    public IReadOnlyList<QuestInventoryEntry> Snapshot => Volatile.Read(ref snapshot);
    public string Status => Volatile.Read(ref status);

    public void Update(bool isEnabled)
    {
        enabled = isEnabled;
        var currentTerritory = Services.ClientState.TerritoryType;
        if (!enabled || !Services.ClientState.IsLoggedIn || ChangingAreas() || currentTerritory == 0)
        {
            Clear();
            return;
        }
        if (territory != currentTerritory)
        {
            Clear();
            territory = currentTerritory;
        }
        var now = Stopwatch.GetTimestamp();
        if (now < nextScan) return;
        nextScan = now + (long)(Stopwatch.Frequency * .333);
        var inventory = InventoryManager.Instance();
        var actions = ActionManager.Instance();
        if (inventory == null || actions == null)
        {
            Volatile.Write(ref snapshot, Array.Empty<QuestInventoryEntry>());
            return;
        }
        var target = CaptureTarget();
        var tracked = ReadTrackedItems();
        var entries = new List<QuestInventoryEntry>();
        foreach (var type in Containers)
        {
            var container = inventory->GetInventoryContainer(NativeContainer(type));
            if (!ValidContainer(container)) continue;
            for (var i = 0; i < container->Size; ++i)
            {
                var slot = container->Items + i;
                if (slot->IsSymbolic || slot->Quantity <= 0 || slot->ItemId == 0) continue;
                var kind = type == QuestInventoryContainer.KeyItems ? QuestInventoryKind.EventItem : QuestInventoryKind.Item;
                var baseId = slot->GetBaseItemId();
                var useId = slot->GetItemId();
                var isTracked = tracked.Contains(baseId) || tracked.Contains(useId);
                if (kind == QuestInventoryKind.Item && !isTracked) continue;
                var metadata = ReadMetadata(kind, baseId);
                if (!QuestInventoryPolicy.CanList(kind, type, baseId, useId, slot->Quantity, metadata.Allowed, isTracked)) continue;
                var entry = new QuestInventoryEntry(kind, baseId, useId, type, (ushort)i,
                    metadata.Name, metadata.IconId, slot->Quantity, false, string.Empty, isTracked, target, generation);
                var context = new QuestInventoryUseContext(true, true, false, generation, true, target, true,
                    slot->Quantity, metadata.Allowed, isTracked, actions->ActionQueued || actions->AnimationLock > 0,
                    target.IsValid ? actions->GetActionStatus(NativeKind(kind), useId, target.Id) : uint.MaxValue);
                var reason = QuestInventoryPolicy.BlockReason(entry, context);
                entries.Add(entry with { CanUse = reason.Length == 0, Reason = reason });
            }
        }
        // Deterministic ordering preserves button locations between refreshes. Each
        // entry retains its exact stack instead of silently substituting another slot.
        var ordered = entries.OrderByDescending(x => x.TrackedQuest).ThenBy(x => x.Kind)
            .ThenBy(x => x.BaseId).ThenBy(x => x.Container).ThenBy(x => x.Slot).ToArray();
        Volatile.Write(ref snapshot, Array.AsReadOnly(ordered));
    }

    public QuestItemUseResult TryUse(QuestInventoryEntry selected)
    {
        Volatile.Write(ref status, string.Empty);
        if (!enabled) return QuestItemUseResult.NotApplicable;
        var loggedIn = Services.ClientState.IsLoggedIn;
        var changing = ChangingAreas();
        var target = loggedIn && !changing ? CaptureTarget() : default;
        var present = Snapshot.Any(x => x.Generation == selected.Generation && x.Kind == selected.Kind
            && x.BaseId == selected.BaseId && x.UseId == selected.UseId && x.Container == selected.Container
            && x.Slot == selected.Slot && x.Target == selected.Target);
        var value = new QuestInventoryUseContext(enabled, loggedIn, changing, generation, present, target,
            false, 0, false, false, true, uint.MaxValue);
        if (!loggedIn || changing || !present || selected.Generation != generation || !target.IsValid || target != selected.Target)
            return Block(selected, value);
        var inventory = InventoryManager.Instance();
        var actions = ActionManager.Instance();
        if (inventory == null || actions == null || !Enum.IsDefined(selected.Container)) return Block(selected, value);
        var container = inventory->GetInventoryContainer(NativeContainer(selected.Container));
        if (!ValidContainer(container) || selected.Slot >= container->Size) return Block(selected, value);
        var slot = container->Items + selected.Slot;
        if (slot->IsSymbolic || slot->Quantity <= 0 || slot->GetBaseItemId() != selected.BaseId || slot->GetItemId() != selected.UseId)
            return Block(selected, value);
        var metadata = ReadMetadata(selected.Kind, selected.BaseId);
        var tracked = ReadTrackedItems();
        value = value with { SlotMatches = true, Count = slot->Quantity, MetadataAllowed = metadata.Allowed,
            TrackedQuest = tracked.Contains(selected.BaseId) || tracked.Contains(selected.UseId),
            ActionBusy = actions->ActionQueued || actions->AnimationLock > 0,
            ActionStatus = metadata.Allowed ? actions->GetActionStatus(NativeKind(selected.Kind), selected.UseId, target.Id) : uint.MaxValue };
        var result = QuestInventoryPolicy.TryUse(selected, value, () =>
        {
            if (CaptureTarget() != target) return false;
            var extra = selected.Kind == QuestInventoryKind.EventItem ? 0xFFFFu
                : ((uint)NativeContainer(selected.Container) << 8) | selected.Slot;
            // Macro mode explicitly refuses the normal action queue. No location
            // action, self-target substitution, retry or fallback is attempted.
            return actions->UseAction(NativeKind(selected.Kind), selected.UseId, target.Id, extra, ActionManager.UseActionMode.Macro);
        });
        if (result == QuestItemUseResult.Blocked)
        {
            var reason = QuestInventoryPolicy.BlockReason(selected, value);
            Volatile.Write(ref status, reason.Length == 0 ? "The game rejected the item action." : reason);
        }
        return result;
    }

    private QuestItemUseResult Block(QuestInventoryEntry entry, QuestInventoryUseContext context)
    {
        Volatile.Write(ref status, QuestInventoryPolicy.BlockReason(entry, context));
        return QuestItemUseResult.Blocked;
    }

    private void Clear()
    {
        if (territory != 0 || Snapshot.Count != 0) ++generation;
        territory = 0;
        nextScan = 0;
        Volatile.Write(ref snapshot, Array.Empty<QuestInventoryEntry>());
        Volatile.Write(ref status, string.Empty);
    }

    private static bool ChangingAreas() => Services.Conditions[ConditionFlag.BetweenAreas]
        || Services.Conditions[ConditionFlag.BetweenAreas51];

    private static QuestInventoryTarget CaptureTarget()
    {
        var target = Services.Targets.Target;
        var territoryId = Services.ClientState.TerritoryType;
        if (target == null || !target.IsValid() || !target.IsTargetable || target.Address == 0
            || !((NativeGameObject*)target.Address)->GetIsTargetable()) return new(0, 0, territoryId);
        return new(target.GameObjectId, target.Address, territoryId);
    }

    private static bool ValidContainer(InventoryContainer* value) => value != null && value->IsLoaded
        && value->Items != null && value->Size is >= 0 and <= 256;

    private static HashSet<uint> ReadTrackedItems()
    {
        var result = new HashSet<uint>();
        var state = UIState.Instance();
        var quests = QuestManager.Instance();
        if (state == null || quests == null) return result;
        ref var tracked = ref state->QuestTodoList.Todo.TrackedQuests;
        if (tracked.LongCount is < 0 or > 30) return result;
        for (var i = 0; i < tracked.Count; ++i)
        {
            ref var entry = ref tracked[i];
            if (entry.QuestId == 0 || entry.ItemCount is < 0 or > 8 || !quests->IsQuestAccepted(entry.QuestId)) continue;
            var quest = quests->GetQuestById(entry.QuestId);
            if (quest == null || quest->Sequence != entry.Sequence) continue;
            foreach (var id in entry.ItemIds[..entry.ItemCount]) if (id != 0) result.Add(id);
        }
        return result;
    }

    private readonly record struct ItemMetadata(bool Allowed, string Name, uint IconId);

    private static ItemMetadata ReadMetadata(QuestInventoryKind kind, uint id)
    {
        if (kind == QuestInventoryKind.EventItem)
        {
            var item = Services.Data.GetExcelSheet<EventItem>().GetRowOrDefault(id);
            if (item is { } row && row.Action.RowId != 0 && row.Action.ValueNullable is { } action && !action.TargetArea)
                return new(true, row.Name.ToString(), row.Icon);
        }
        else if (kind == QuestInventoryKind.Item)
        {
            var item = Services.Data.GetExcelSheet<Item>().GetRowOrDefault(id);
            if (item is { } row && row.ItemAction.RowId != 0 && row.ItemAction.ValueNullable is { } itemAction
                && itemAction.Action.RowId != 0 && itemAction.Action.ValueNullable is { } action && !action.TargetArea)
                return new(true, row.Name.ToString(), row.Icon);
        }
        return new(false, string.Empty, 0);
    }

    private static ActionType NativeKind(QuestInventoryKind kind) => kind == QuestInventoryKind.EventItem
        ? ActionType.EventItem : ActionType.Item;

    private static InventoryType NativeContainer(QuestInventoryContainer container) => container switch
    {
        QuestInventoryContainer.KeyItems => InventoryType.KeyItems,
        QuestInventoryContainer.Inventory1 => InventoryType.Inventory1,
        QuestInventoryContainer.Inventory2 => InventoryType.Inventory2,
        QuestInventoryContainer.Inventory3 => InventoryType.Inventory3,
        QuestInventoryContainer.Inventory4 => InventoryType.Inventory4,
        _ => throw new ArgumentOutOfRangeException(nameof(container)),
    };
}
