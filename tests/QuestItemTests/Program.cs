using OmniKit.Interaction;

var count = 0;
void Check(bool value, string message) { ++count; if (!value) throw new InvalidOperationException(message); }
var good = new QuestItemContext(2001243, 134, true, true, 2, 1, 0, 1, 1, 1, true, false, 0);
foreach (var rock in new uint[] { 2001243, 2001244, 2001245 })
{
    var calls = 0;
    var current = good with { TargetDataId = rock };
    Check(QuestItemPolicy.CanUse(current), "Each verified rock can qualify.");
    Check(QuestItemPolicy.TryUse(true, current, () => { ++calls; return true; }) == QuestItemUseResult.Used && calls == 1,
        "One press can issue exactly one action.");
}
foreach (var other in new uint[] { 0, 2001272, 2001246, 2001247, 2001248, 1002633, uint.MaxValue })
{
    var current = good with { TargetDataId = other };
    Check(!QuestItemPolicy.CanUse(current), "Debris and unknown targets cannot use the shovel rule.");
    Check(QuestItemPolicy.TryUse(true, current, () => throw new Exception("Must not act")) == QuestItemUseResult.NotApplicable,
        "Unmapped objects retain ordinary interaction.");
}
Check(QuestItemPolicy.TryUse(false, good, () => throw new Exception("Must not act")) == QuestItemUseResult.NotApplicable,
    "Disabled feature never consumes an item action.");
var blocked = new[]
{
    good with { TerritoryId = 0 }, good with { TerritoryId = 135 }, good with { Targetable = false },
    good with { QuestAccepted = false }, good with { Sequence = 0 }, good with { Sequence = 1 },
    good with { Sequence = 3 }, good with { Sequence = 255 },
    good with { ActiveQuestHandlers = 0 }, good with { ActiveQuestHandlers = 2 },
    good with { OtherActiveQuestHandlers = 1 }, good with { TrackedQuestMatches = 0 },
    good with { TrackedQuestMatches = 2 }, good with { TrackedItemMatches = 0 },
    good with { TrackedItemMatches = 2 }, good with { OwnedKeyItems = 0 },
    good with { OwnedKeyItems = -1 }, good with { MetadataMatches = false },
    good with { GroundTargeted = true }, good with { ActionStatus = 1 }, good with { ActionStatus = uint.MaxValue },
};
foreach (var current in blocked)
{
    Check(!QuestItemPolicy.CanUse(current), "Every missing or ambiguous proof blocks eligibility.");
    Check(QuestItemPolicy.BlockReason(current).Length > 0, "Every failed guard must explain the blocked item action.");
    Check(QuestItemPolicy.TryUse(true, current, () => throw new Exception("Must not act")) == QuestItemUseResult.Blocked,
        "Matched but blocked rules must not fall through to an ordinary interaction.");
}
var rejectedCalls = 0;
Check(QuestItemPolicy.CanOffer(good with { ActionStatus = 1 })
    && !QuestItemPolicy.CanUse(good with { ActionStatus = 1 }),
    "Detection is independent of item action reach; the actual press still requires native usability.");
Check(QuestItemPolicy.TryUse(true, good, () => { ++rejectedCalls; return false; }) == QuestItemUseResult.Blocked
    && rejectedCalls == 1, "Native rejection is a single attempt, without retry or fallback.");
Check(QuestItemPolicy.BlockReason(good) == string.Empty, "Usable actions do not show routine success status.");
Check(QuestItemPolicy.BlockReason(good with { Sequence = 255, OwnedKeyItems = 0 }) == "The required quest step is not active.",
    "A completed step takes precedence over a missing item.");
Check(QuestItemPolicy.BlockReason(good with { ActiveQuestHandlers = 2 }).Contains("ambiguous", StringComparison.Ordinal),
    "Competing quest context has a distinct explanation.");
Check(QuestItemPolicy.BlockReason(good with { TrackedQuestMatches = 0 }) != QuestItemPolicy.BlockReason(good with { OwnedKeyItems = 0 }),
    "An untracked quest and a missing key item give different guidance.");
Check(QuestItemPolicy.BlockReason(good with { ActionStatus = 1 }) == QuestItemPolicy.BlockReason(good with { ActionStatus = uint.MaxValue }),
    "Native status values must not leak raw diagnostics.");
Console.WriteLine($"Passed {count} quest-item policy checks. No game calls or inventory were used.");
