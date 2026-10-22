using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
using NativeGatheringPoint = FFXIVClientStructs.FFXIV.Client.Game.Object.GatheringPointObject;
using NativeGatheringType = FFXIVClientStructs.FFXIV.Client.Game.Event.GatheringType;

namespace OmniKit.Interaction;

public sealed record GearsetOption(int Index, string Name, uint ClassJobId);

/// <summary>Equips saved gearsets only. Never edits gearsets or performs gathering actions.</summary>
internal sealed unsafe class GatheringGearService(Configuration config) : IDisposable
{
    private Pending? pending;
    private long generation;
    private bool inputBlocked = true;
    private IReadOnlyList<GearsetOption> gearsets = [];
    private long gearsetsReadAt;
    private long lastObservation;

    public string Status { get; private set; } = "Automatic gathering gear is off.";

    public IReadOnlyList<GearsetOption> GetGearsets()
    {
        if (!Services.ClientState.IsLoggedIn)
            return [];
        if (Environment.TickCount64 - gearsetsReadAt < 1000)
            return gearsets;

        var module = RaptureGearsetModule.Instance();
        if (module == null)
            return [];

        var result = new List<GearsetOption>();
        for (var index = 0; index < module->Entries.Length; index++)
        {
            var entry = module->GetGearset(index);
            if (entry != null && module->IsValidGearset(index)
                && (entry->Flags & RaptureGearsetModule.GearsetFlag.Exists) != 0)
                result.Add(new GearsetOption(index, entry->NameString, entry->ClassJob));
        }
        gearsets = result;
        gearsetsReadAt = Environment.TickCount64;
        return gearsets;
    }

    // true means this service consumed the request, including a safe refusal.
    public bool TryPrepare(IGameObject target, Func<bool> interact)
    {
        if (!config.GatheringAutoSwitch || target.ObjectKind != ObjectKind.GatheringPoint)
            return false;
        if (pending != null)
        {
            Status = "A gathering gear change is already in progress.";
            return true;
        }
        if (IsInterrupted() || !CanAct())
        {
            Status = "Gathering gear change paused while the game is busy.";
            return true;
        }

        var player = Services.Objects.LocalPlayer;
        var module = RaptureGearsetModule.Instance();
        if (player == null || module == null || !TryGetRequiredJob(target, out var job, out var configuredSet))
        {
            Status = "This node's gathering job could not be identified.";
            return true;
        }

        // With automatic selection, leave an already-correct job's equipment alone.
        if (configuredSet < 0 && player.ClassJob.RowId == job)
            return false;

        var desired = configuredSet >= 0
            ? GetGearsets().FirstOrDefault(set => set.Index == configuredSet && set.ClassJobId == job)
            : GetGearsets().FirstOrDefault(set => set.ClassJobId == job);
        if (desired == null || !TryReadSet(module, desired.Index, out var desiredSet) || desiredSet.Job != job)
        {
            Status = "Create or select a valid saved gearset for this gathering job.";
            return true;
        }
        if (module->CurrentGearsetIndex == desired.Index && player.ClassJob.RowId == job)
            return false;

        if (!TryReadEquipment(out var equipped))
        {
            Status = "Equipment is not ready; no gear was changed.";
            return true;
        }

        var originalIndex = module->CurrentGearsetIndex;
        var originalValid = TryReadSet(module, originalIndex, out var original);
        var canRestore = config.GatheringRestore && originalValid
            && original.Job == player.ClassJob.RowId && original.Full == equipped.Full;
        // Preserve the actual original loadout: an unsaved manual gear change must
        // never be silently replaced by an older saved set when gathering ends.
        var state = new GatheringTransition(originalIndex, player.ClassJob.RowId,
            desired.Index, job, Environment.TickCount64, canRestore);
        var request = new Pending(++generation, Services.ClientState.TerritoryType,
            player.GameObjectId, target.GameObjectId, target.Address, target.BaseId,
            target.Position, original, desiredSet, interact, state);
        pending = request;

        // Exactly one attempt; the public native method retains its own equip checks.
        if (module->EquipGearset(desired.Index) != 0)
        {
            Cancel("The game declined the gearset change; no retry was made.");
            return true;
        }
        Status = canRestore ? "Changing gathering gear; previous set will be restored after gathering."
            : "Changing gathering gear; automatic restoration is unavailable or disabled.";
        return true;
    }

    public void Update(bool blocked)
    {
        inputBlocked = blocked;
        if (!config.GatheringAutoSwitch || !config.SoftInteractEnabled)
        {
            Cancel("Automatic gathering gear is off.");
            return;
        }
        if (pending is not { } request)
        {
            if (Status == "Automatic gathering gear is off.")
                Status = "Ready. Press the interact key while looking at a gathering node.";
            return;
        }
        var now = Environment.TickCount64;
        if (now - lastObservation < 50)
            return;
        lastObservation = now;

        var player = Services.Objects.LocalPlayer;
        var module = RaptureGearsetModule.Instance();
        if (IsInterrupted() || player == null || module == null
            || player.GameObjectId != request.PlayerId
            || Services.ClientState.TerritoryType != request.Territory)
        {
            Cancel("Gathering assistance cancelled because the game context changed.");
            return;
        }
        if (!TryReadEquipment(out var equipment))
        {
            Cancel("Equipment could not be verified; automatic changes cancelled.");
            return;
        }

        var target = Services.Objects.SearchById(request.TargetId);
        var sameTarget = target != null && target.Address == request.TargetAddress
            && target.BaseId == request.TargetBaseId && target.ObjectKind == ObjectKind.GatheringPoint
            && Vector3.DistanceSquared(target.Position, request.TargetPosition) < 0.01f;
        var sameDesiredSet = TryReadSet(module, request.State.TargetSet, out var desired)
            && desired == request.Desired;
        var sameOriginalSet = TryReadSet(module, request.State.OriginalSet, out var original)
            && original == request.Original;
        var gathering = Services.Conditions[ConditionFlag.Gathering]
            || Services.Conditions[ConditionFlag.ExecutingGatheringAction];
        var result = request.State.Advance(new GatheringObservation(
            module->CurrentGearsetIndex, player.ClassJob.RowId, equipment.Full,
            sameDesiredSet && equipment.Items == request.Desired.Items,
            sameOriginalSet && equipment.Full == request.Original.Full,
            gathering, CanAct(), !sameDesiredSet, sameTarget,
            sameOriginalSet, config.GatheringRestore), now);

        switch (result)
        {
            case GatheringDecision.Interact:
                // The callback rechecks camera cone, range, target identity, UI and
                // occlusion. It consumes its generation even if validation declines.
                if (pending?.Generation != request.Generation || !sameTarget || !request.Interact())
                    Cancel("The node is no longer eligible; interaction cancelled.");
                else
                    Status = "Gathering gear equipped. Waiting for gathering to begin.";
                break;
            case GatheringDecision.Restore:
                if (pending?.Generation != request.Generation || !sameOriginalSet
                    || module->EquipGearset(request.State.OriginalSet) != 0)
                    Cancel("The previous gearset could not be restored; no retry was made.");
                else
                    Status = "Gathering finished. Restoring the previous saved gearset.";
                break;
            case GatheringDecision.Complete:
                pending = null;
                Status = request.State.Restored ? "Previous gearset restored."
                    : "Gathering finished; current gear was kept.";
                break;
            case GatheringDecision.Cancel:
                Cancel(request.State.Reason);
                break;
            default:
                if (gathering)
                    Status = "Gather normally. The previous set is restored only after gathering ends.";
                break;
        }
    }

    public void Dispose() => Cancel("Gathering gear assistance stopped.");

    private bool TryGetRequiredJob(IGameObject target, out uint classJob, out int configured)
    {
        classJob = 0;
        configured = -1;
        var points = Services.Data.GetExcelSheet<GatheringPoint>(ClientLanguage.English);
        var types = Services.Data.GetExcelSheet<Lumina.Excel.Sheets.GatheringType>(ClientLanguage.English);
        if (!points.TryGetRow(target.BaseId, out var point)
            || !point.GatheringPointBase.IsValid
            || !types.TryGetRow(point.GatheringPointBase.Value.GatheringType.RowId, out var type))
            return false;

        var discipline = type.Name.ExtractText() switch
        {
            "Mining" or "Quarrying" => "miner",
            "Logging" or "Harvesting" => "botanist",
            _ => null,
        };
        if (discipline == null)
            return false;
        // Cross-check the sheet chain against the live node's named native type.
        // A stale/mismatched data-row mapping must never switch to the wrong job.
        var native = (NativeGatheringPoint*)target.Address;
        if (native == null || native->Impl == null)
            return false;
        var nativeDiscipline = native->Impl->GetGatheringType() switch
        {
            NativeGatheringType.Mining or NativeGatheringType.Quarrying => "miner",
            NativeGatheringType.Logging or NativeGatheringType.Harvesting => "botanist",
            _ => null,
        };
        if (nativeDiscipline != discipline)
            return false;
        configured = discipline == "miner" ? config.MinerGearset : config.BotanistGearset;
        foreach (var job in Services.Data.GetExcelSheet<ClassJob>(ClientLanguage.English))
            if (string.Equals(job.NameEnglish.ExtractText(), discipline, StringComparison.OrdinalIgnoreCase))
            {
                classJob = job.RowId;
                return classJob != 0;
            }
        return false;
    }

    private bool CanAct()
    {
        if (inputBlocked || Services.GameGui.GameUiHidden
            || Services.Conditions[ConditionFlag.Casting] || Services.Conditions[ConditionFlag.Casting87]
            || Services.Conditions[ConditionFlag.Gathering] || Services.Conditions[ConditionFlag.ExecutingGatheringAction]
            || Services.Conditions[ConditionFlag.Crafting] || Services.Conditions[ConditionFlag.ExecutingCraftingAction]
            || Services.Conditions[ConditionFlag.PreparingToCraft]
            || Services.Conditions[ConditionFlag.Occupied] || Services.Conditions[ConditionFlag.OccupiedInEvent]
            || Services.Conditions[ConditionFlag.OccupiedInQuestEvent] || Services.Conditions[ConditionFlag.Jumping]
            || Services.Conditions[ConditionFlag.Occupied30] || Services.Conditions[ConditionFlag.Occupied33]
            || Services.Conditions[ConditionFlag.Occupied38] || Services.Conditions[ConditionFlag.Occupied39]
            || Services.Conditions[ConditionFlag.OccupiedSummoningBell] || Services.Conditions[ConditionFlag.InFlight]
            || Services.Conditions[ConditionFlag.Jumping61] || Services.Conditions[ConditionFlag.MountOrOrnamentTransition]
            || Services.Conditions[ConditionFlag.Mounting] || Services.Conditions[ConditionFlag.Mounting71]
            || Services.Conditions[ConditionFlag.Mounted] || Services.Conditions[ConditionFlag.RidingPillion])
            return false;
        var ui = RaptureAtkModule.Instance();
        return ui != null && !ui->IsTextInputActive() && ui->AtkUnitManager != null
            && (ui->AtkUnitManager->FocusedAddon == null || !ui->AtkUnitManager->FocusedAddon->IsVisible);
    }

    private static bool IsInterrupted()
    {
        var framework = NativeFramework.Instance();
        return !Services.ClientState.IsLoggedIn || Services.ClientState.IsGPosing
            || framework == null || framework->WindowInactive || framework->IsExiting
            || Services.Conditions[ConditionFlag.Unconscious] || Services.Conditions[ConditionFlag.InCombat]
            || Services.Conditions[ConditionFlag.BetweenAreas] || Services.Conditions[ConditionFlag.BetweenAreas51]
            || Services.Conditions[ConditionFlag.WatchingCutscene] || Services.Conditions[ConditionFlag.WatchingCutscene78]
            || Services.Conditions[ConditionFlag.OccupiedInCutSceneEvent];
    }

    private static bool TryReadSet(RaptureGearsetModule* module, int index, out EquipmentIdentity identity)
    {
        identity = default;
        if (index < 0 || index >= module->Entries.Length || !module->IsValidGearset(index))
            return false;
        var set = module->GetGearset(index);
        if (set == null || (set->Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0
            || (set->Flags & RaptureGearsetModule.GearsetFlag.MainHandMissing) != 0)
            return false;
        ulong items = 14695981039346656037, full = items;
        foreach (var item in set->Items)
        {
            if ((item.Flags & RaptureGearsetModule.GearsetItemFlag.ItemMissing) != 0)
                return false;
            Mix(ref items, item.ItemId);
            Mix(ref full, item.ItemId);
            Mix(ref full, item.GlamourId);
            Mix(ref full, item.Stain0Id);
            Mix(ref full, item.Stain1Id);
            for (var i = 0; i < item.Materia.Length; i++)
            {
                Mix(ref full, item.Materia[i]);
                Mix(ref full, item.MateriaGrades[i]);
            }
        }
        identity = new EquipmentIdentity(items, full, set->ClassJob, set->GlamourSetLink);
        return true;
    }

    private static bool TryReadEquipment(out EquipmentIdentity identity)
    {
        identity = default;
        var inventory = InventoryManager.Instance();
        var container = inventory == null ? null : inventory->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null || !container->IsLoaded || container->Size < 14)
            return false;
        ulong items = 14695981039346656037, full = items;
        for (var slot = 0; slot < 14; slot++)
        {
            var item = container->GetInventorySlot(slot);
            if (item == null)
                return false;
            var itemId = item->GetItemId();
            Mix(ref items, itemId);
            Mix(ref full, itemId);
            Mix(ref full, item->GetGlamourId());
            Mix(ref full, item->GetStain(0));
            Mix(ref full, item->GetStain(1));
            for (byte materia = 0; materia < 5; materia++)
            {
                Mix(ref full, item->GetMateriaId(materia));
                Mix(ref full, item->GetMateriaGrade(materia));
            }
        }
        identity = new EquipmentIdentity(items, full);
        return true;
    }

    private static void Mix(ref ulong hash, uint value) => hash = unchecked((hash ^ value) * 1099511628211);

    private void Cancel(string reason)
    {
        pending = null;
        generation++;
        Status = reason;
    }

    private readonly record struct EquipmentIdentity(ulong Items, ulong Full, byte Job = 0, byte GlamourLink = 0);
    private sealed record Pending(long Generation, uint Territory, ulong PlayerId, ulong TargetId,
        nint TargetAddress, uint TargetBaseId, Vector3 TargetPosition, EquipmentIdentity Original, EquipmentIdentity Desired,
        Func<bool> Interact, GatheringTransition State);
}
