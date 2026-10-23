using OmniKit.Interaction;

var checks = 0;
void Check(bool value, string message) { ++checks; if (!value) throw new InvalidOperationException(message); }
var target = new QuestInventoryTarget(42, (nint)128, 135);
var item = new QuestInventoryEntry(QuestInventoryKind.EventItem, 2000221, 2000221,
    QuestInventoryContainer.KeyItems, 1, "Synthetic item", 100, 3, true, "", true, target, 5);
var good = new QuestInventoryUseContext(true, true, false, 5, true, target, true, 3, true, true, false, 0);
Check(QuestInventoryPolicy.BlockReason(item, good) == "", "A fresh, matching entry is usable.");
var calls = 0;
Check(QuestInventoryPolicy.TryUse(item, good, () => { ++calls; return true; }) == QuestItemUseResult.Used && calls == 1,
    "One accepted click executes once.");
calls = 0;
Check(QuestInventoryPolicy.TryUse(item, good, () => { ++calls; return false; }) == QuestItemUseResult.Blocked && calls == 1,
    "A rejected native action cannot retry or fall through.");
Check(QuestInventoryPolicy.TryUse(item, good with { Enabled = false }, () => throw new Exception("Must not act"))
    == QuestItemUseResult.NotApplicable, "A disabled bar never acts.");
foreach (var bad in new[]
{
    good with { LoggedIn = false }, good with { ChangingAreas = true }, good with { Generation = 6 },
    good with { PresentInSnapshot = false }, good with { Target = target with { Id = 43 } },
    good with { Target = target with { Address = (nint)129 } }, good with { Target = target with { TerritoryId = 134 } },
    good with { Target = default }, good with { SlotMatches = false }, good with { Count = 0 },
    good with { Count = -1 }, good with { MetadataAllowed = false }, good with { ActionBusy = true },
    good with { ActionStatus = 1 }, good with { ActionStatus = uint.MaxValue },
})
{
    Check(QuestInventoryPolicy.BlockReason(item, bad).Length > 0, "Every failed guard explains why the click is blocked.");
    Check(QuestInventoryPolicy.TryUse(item, bad, () => throw new Exception("Must not act")) == QuestItemUseResult.Blocked,
        "Stale/invalid advice cannot dispatch.");
}
Check(QuestInventoryPolicy.BlockReason(item with { Target = default }, good).Contains("Select a target"),
    "No automatic self target is selected.");
Check(QuestInventoryPolicy.TryUse(item with { CanUse = false, Reason = "Old advice" }, good,
    () => true) == QuestItemUseResult.Used, "An old native availability estimate is not an execution token.");
Check(QuestInventoryPolicy.TryUse(item, good with { Count = 2 }, () => true) == QuestItemUseResult.Used,
    "A still-owned same slot accepts a click after count refresh, using the fresh quantity.");
Check(QuestInventoryPolicy.BlockReason(item, good with { TrackedQuest = false }) == "",
    "Usable key items can be selected manually without inventing a target-specific quest mapping.");
var normal = item with { Kind = QuestInventoryKind.Item, Container = QuestInventoryContainer.Inventory1,
    BaseId = 7, UseId = 1000007 };
Check(QuestInventoryPolicy.BlockReason(normal, good) == "", "A currently tracked normal item preserves its native use ID.");
Check(QuestInventoryPolicy.TryUse(normal, good with { TrackedQuest = false }, () => throw new Exception("Must not act"))
    == QuestItemUseResult.Blocked, "An expired normal-item quest link cannot dispatch.");
foreach (var kind in Enum.GetValues<QuestInventoryKind>())
foreach (var container in Enum.GetValues<QuestInventoryContainer>())
{
    var expected = kind == QuestInventoryKind.EventItem
        ? container == QuestInventoryContainer.KeyItems : container != QuestInventoryContainer.KeyItems;
    Check(QuestInventoryPolicy.CanList(kind, container, 1, 1, 1, true, true) == expected,
        "Only the explicit kind/container pair is allowed.");
}
Check(!QuestInventoryPolicy.CanList(QuestInventoryKind.Item, QuestInventoryContainer.Inventory1, 1, 1, 1, true, false),
    "Normal items must be in a current accepted native tracker entry.");
Check(!QuestInventoryPolicy.CanList(QuestInventoryKind.EventItem, QuestInventoryContainer.KeyItems, 1, 2, 1, true, true),
    "Key items cannot substitute a different action ID.");
Check(!QuestInventoryPolicy.CanList((QuestInventoryKind)99, QuestInventoryContainer.KeyItems, 1, 1, 1, true, true), "Unknown kind is rejected.");
Check(!QuestInventoryPolicy.CanList(QuestInventoryKind.Item, (QuestInventoryContainer)99, 1, 1, 1, true, true), "Unknown container is rejected.");
foreach (var invalid in new[] { normal with { BaseId = 0 }, normal with { UseId = 0 }, normal with { Kind = (QuestInventoryKind)99 },
    normal with { Container = (QuestInventoryContainer)99 } })
    Check(QuestInventoryPolicy.TryUse(invalid, good, () => throw new Exception("Must not act")) == QuestItemUseResult.Blocked,
        "Malformed selected identity never dispatches.");

var rocks = new QuestItemContext(2001243, 134, true, true, 2, 1, 0, 1, 1, 1, true, false, 0);
var mounds = rocks with { TargetDataId = 2000772, TerritoryId = 135, Sequence = 1 };
foreach (var facts in new[] { rocks, mounds })
{
    var preview = QuestActionPreview.ForItem(facts, 99, "Local item")!.Value;
    uint used = 0;
    QuestItemPolicy.TryUse(true, facts, rule => { used = rule.ItemId; return true; });
    Check(preview.Kind == QuestActionKind.EventItem && preview.Id == used && preview.CanUse && preview.Reason == "",
        "The preview uses the exact production rule's action identity and decision.");
    Check(preview.IconId == 99 && preview.Name == "Local item", "Sheet presentation metadata is retained.");
    foreach (var blocked in new[] { facts with { Sequence = 255 }, facts with { TrackedItemMatches = 0 },
        facts with { GroundTargeted = true }, facts with { ActionStatus = 1 } })
    {
        var advice = QuestActionPreview.ForItem(blocked, 99, "Local item");
        Check(advice is { CanUse: false } && advice.Value.Id == preview.Id
            && advice.Value.Reason == QuestItemPolicy.BlockReason(blocked),
            "A matched blocked item preview retains priority and the exact policy reason.");
    }
}
Check(QuestActionPreview.ForItem(rocks with { TargetDataId = 2001246 }, 99, "Debris") == null,
    "An unrecognized target has no invented item preview.");
var coachman = new QuestEmoteContext(1002449, 135, true, true, 2, 1, 0, true, true, true);
var emote = QuestActionPreview.ForEmote(coachman, 88, "Local emote")!.Value;
Check(emote.Kind == QuestActionKind.Emote && emote.Id == QuestEmotePolicy.EmoteId && emote.CanUse
    && emote.IconId == 88 && emote.Name == "Local emote", "Emote preview shares the execution policy.");
foreach (var blocked in new[] { coachman with { Sequence = 3 }, coachman with { ActiveQuestHandlers = 2 },
    coachman with { EmoteUnlocked = false }, coachman with { EmoteAllowed = false } })
    Check(QuestActionPreview.ForEmote(blocked, 88, "Local emote") is { CanUse: false } advice
        && advice.Reason == QuestEmotePolicy.BlockReason(blocked), "Matched unavailable emotes keep their blocked preview.");
Check(QuestActionPreview.ForEmote(coachman with { TargetDataId = 1002448 }, 88, "Other actor") == null,
    "There is no generic emote guess.");
Console.WriteLine($"Quest inventory and preview checks passed: {checks}. No game functions were called.");
