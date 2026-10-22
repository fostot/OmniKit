using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using System.Security.Cryptography;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace OmniKit.Search;

/// <summary>Searchable saved gearsets. Every activation revalidates state and identity.</summary>
internal static unsafe class GearsetActions
{
    private static readonly ConditionFlag[] BlockingConditions =
    [
        ConditionFlag.Unconscious, ConditionFlag.InCombat, ConditionFlag.Casting, ConditionFlag.Casting87,
        ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51, ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78, ConditionFlag.Occupied, ConditionFlag.Occupied30,
        ConditionFlag.OccupiedInEvent, ConditionFlag.OccupiedInQuestEvent, ConditionFlag.Occupied33,
        ConditionFlag.OccupiedInCutSceneEvent, ConditionFlag.Occupied38, ConditionFlag.Occupied39,
        ConditionFlag.OccupiedSummoningBell, ConditionFlag.Gathering, ConditionFlag.ExecutingGatheringAction,
        ConditionFlag.Mounted, ConditionFlag.InFlight, ConditionFlag.RidingPillion,
        ConditionFlag.Jumping, ConditionFlag.Jumping61,
    ];

    public static string Status { get; private set; } = "Choose a saved gearset to equip.";

    public static IReadOnlyList<SearchEntry> GetEntries()
    {
        if (!Services.ClientState.IsLoggedIn || Services.Objects.LocalPlayer == null)
            return [];
        var module = RaptureGearsetModule.Instance();
        if (module == null)
            return [];

        var jobs = Services.Data.GetExcelSheet<ClassJob>(ClientLanguage.English);
        var localJobs = Services.Data.GetExcelSheet<ClassJob>();
        var entries = new List<SearchEntry>();
        for (var index = 0; index < module->Entries.Length; index++)
        {
            if (!module->IsValidGearset(index))
                continue;
            var set = module->GetGearset(index);
            if (set == null || (set->Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0)
                continue;

            var name = set->NameString;
            var job = (uint)set->ClassJob;
            var hasJob = jobs.TryGetRow(job, out var row);
            var englishName = hasJob ? row.NameEnglish.ExtractText() : "";
            var abbreviation = hasJob ? row.Abbreviation.ExtractText() : "";
            var localName = localJobs.TryGetRow(job, out var localRow) ? localRow.Name.ExtractText() : englishName;
            var title = string.IsNullOrWhiteSpace(name) ? $"Gearset {index + 1}" : name;
            var selectedIndex = index;
            var playerId = Services.Objects.LocalPlayer.GameObjectId;
            var keywords = $"job class change switch equip gearset gear set {index + 1} {name} {englishName} {localName} {abbreviation}";
            // A saved phrase must stop matching when a slot is renamed or reused for
            // another job. A bare slot number would silently retarget that binding.
            var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{job}:{name}")))[..16];
            entries.Add(new SearchEntry($"gearset:{index}:{identity}", $"{title} · {abbreviation}",
                $"Equip saved gearset {index + 1} ({localName})", keywords, "Gearsets",
                () => _ = Services.Framework.RunOnFrameworkThread(() => TryEquip(selectedIndex, name, job, playerId))));
        }
        return entries;
    }

    public static IEnumerable<string> GetPhrases() => GetEntries().SelectMany(entry =>
        new[] { entry.Title.Split(" · ")[0], "gearset " + entry.Title.Split(" · ")[0] })
        .Concat(Services.Data.GetExcelSheet<ClassJob>(ClientLanguage.English)
            .Where(row => row.RowId != 0).Select(row => "job " + row.NameEnglish.ExtractText()))
        .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Must be invoked on the framework thread, never from recognition callbacks.</summary>
    public static bool TryEquip(int index) => TryEquip(index, null, null, null);

    private static bool TryEquip(int index, string? expectedName, uint? expectedJob, ulong? expectedPlayer)
    {
        if (!Services.Framework.IsInFrameworkUpdateThread || !CanEquip())
        {
            Status = "Gear changes are paused while the game is busy or unfocused.";
            return false;
        }
        var module = RaptureGearsetModule.Instance();
        if (module == null || index < 0 || index >= module->Entries.Length || !module->IsValidGearset(index))
        {
            Status = "That saved gearset is no longer available.";
            return false;
        }
        var set = module->GetGearset(index);
        if (set == null || (set->Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0
            || (expectedName != null && set->NameString != expectedName)
            || (expectedJob != null && set->ClassJob != expectedJob)
            || (expectedPlayer != null && Services.Objects.LocalPlayer?.GameObjectId != expectedPlayer))
        {
            Status = "The character or gearset changed. Search again before equipping.";
            return false;
        }
        if ((set->Flags & RaptureGearsetModule.GearsetFlag.MainHandMissing) != 0)
        {
            Status = "The saved gearset's main-hand item is missing.";
            return false;
        }
        foreach (var item in set->Items)
            if ((item.Flags & RaptureGearsetModule.GearsetItemFlag.ItemMissing) != 0)
            {
                Status = "An item in that saved gearset is missing.";
                return false;
            }

        // Invoke once, retaining the native client's validation and linked glamour behavior.
        var equipped = module->EquipGearset(index) == 0;
        Status = equipped ? "Gearset equip request accepted." : "The game could not equip that gearset.";
        return equipped;
    }

    private static bool CanEquip()
    {
        if (!Services.ClientState.IsLoggedIn || Services.ClientState.IsGPosing
            || Services.GameGui.GameUiHidden || Services.Objects.LocalPlayer == null)
            return false;
        foreach (var condition in BlockingConditions)
            if (Services.Conditions[condition])
                return false;
        var framework = NativeFramework.Instance();
        var ui = RaptureAtkModule.Instance();
        return framework != null && !framework->WindowInactive && !framework->IsExiting
            && ui != null && !ui->IsTextInputActive() && ui->AtkUnitManager != null
            && (ui->AtkUnitManager->FocusedAddon == null || !ui->AtkUnitManager->FocusedAddon->IsVisible);
    }
}
