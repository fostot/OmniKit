using OmniKit.Interaction;

var count = 0;
void Check(bool value, string message) { ++count; if (!value) throw new InvalidOperationException(message); }
var good = new QuestItemContext(2001243, 134, true, true, 2, 1, 0, 1, 1, 1, true, false, 0);
foreach (var rock in new uint[] { 2001243, 2001244, 2001245 })
{
    var calls = 0;
    var current = good with { TargetDataId = rock };
    Check(QuestItemPolicy.CanUse(current), "Each verified rock can qualify.");
    Check(QuestItemPolicy.TryUse(true, current, _ => { ++calls; return true; }) == QuestItemUseResult.Used && calls == 1,
        "One press can issue exactly one action.");
}
foreach (var other in new uint[] { 0, 2001272, 2001246, 2001247, 2001248, 1002633, uint.MaxValue })
{
    var current = good with { TargetDataId = other };
    Check(!QuestItemPolicy.CanUse(current), "Debris and unknown targets cannot use the shovel rule.");
    Check(QuestItemPolicy.TryUse(true, current, _ => throw new Exception("Must not act")) == QuestItemUseResult.NotApplicable,
        "Unmapped objects retain ordinary interaction.");
}
Check(QuestItemPolicy.TryUse(false, good, _ => throw new Exception("Must not act")) == QuestItemUseResult.NotApplicable,
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
    Check(QuestItemPolicy.TryUse(true, current, _ => throw new Exception("Must not act")) == QuestItemUseResult.Blocked,
        "Matched but blocked rules must not fall through to an ordinary interaction.");
}
var rejectedCalls = 0;
Check(QuestItemPolicy.CanOffer(good with { ActionStatus = 1 })
    && !QuestItemPolicy.CanUse(good with { ActionStatus = 1 }),
    "Detection is independent of item action reach; the actual press still requires native usability.");
Check(QuestItemPolicy.TryUse(true, good, _ => { ++rejectedCalls; return false; }) == QuestItemUseResult.Blocked
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
var flesh = good with { TargetDataId = 2000772, TerritoryId = 135, Sequence = 1 };
foreach (var mound in new uint[] { 2000772, 2000773, 2000774 })
{
    var current = flesh with { TargetDataId = mound };
    var calls = 0;
    Check(QuestItemPolicy.CanUse(current), "Each verified mound can qualify.");
    Check(QuestItemPolicy.TryUse(true, current, rule =>
        {
            ++calls;
            Check(rule.QuestId == 416 && rule.QuestRowId == 65952 && rule.Sequence == 1 && rule.TerritoryId == 135,
                "Dodo execution retains its own quest, sequence and territory.");
            Check(rule.ItemId == 2000221 && rule.ActionId == 1,
                "Execution dispatches the verified flesh ID, never a guessed or same-named item.");
            return true;
        }) == QuestItemUseResult.Used && calls == 1, "One mound press uses one flesh action.");
}
foreach (var wrong in new[]
{
    flesh with { TerritoryId = 134 }, flesh with { Sequence = 0 }, flesh with { Sequence = 2 },
    flesh with { Sequence = 255 }, flesh with { QuestAccepted = false }, flesh with { Targetable = false },
    flesh with { ActiveQuestHandlers = 0 }, flesh with { ActiveQuestHandlers = 2 },
    flesh with { OtherActiveQuestHandlers = 1 }, flesh with { TrackedQuestMatches = 0 },
    flesh with { TrackedQuestMatches = 2 }, flesh with { TrackedItemMatches = 0 },
    flesh with { TrackedItemMatches = 2 }, flesh with { OwnedKeyItems = 0 },
    flesh with { MetadataMatches = false }, flesh with { GroundTargeted = true }, flesh with { ActionStatus = 1 },
})
{
    Check(!QuestItemPolicy.CanUse(wrong) && QuestItemPolicy.BlockReason(wrong).Length > 0,
        "Blocked flesh use explains its failed guard.");
    Check(QuestItemPolicy.TryUse(true, wrong, _ => throw new Exception("Must not act")) == QuestItemUseResult.Blocked,
        "Failed mound proof cannot dispatch another item or ordinary interaction.");
}
Check(QuestItemPolicy.RuleForTarget(2000772)?.ItemId != 2000690,
    "The identically named flesh from a different quest must never substitute.");
Check(QuestItemPolicy.RuleForTarget(good.TargetDataId)?.ItemId == 2000350,
    "The original shovel rule stays distinct from the flesh rule.");
Check(!QuestItemPolicy.CanUse(flesh with { TargetDataId = 2001243 }),
    "Dodo context cannot qualify a rock from the other quest.");
Check(!QuestItemPolicy.CanUse(good with { TargetDataId = 2000772 }),
    "Shovel context cannot qualify a dodo mound.");
Check(QuestItemPolicy.RuleForTarget(2001246) is null,
    "Unmapped ordinary interactions do not fabricate an item action.");
var rejectedFleshCalls = 0;
Check(QuestItemPolicy.TryUse(true, flesh, _ => { ++rejectedFleshCalls; return false; }) == QuestItemUseResult.Blocked
    && rejectedFleshCalls == 1, "Rejected flesh use is not retried or replaced.");
Check(QuestItemPolicy.TryUse(false, flesh, _ => throw new Exception("Must not act")) == QuestItemUseResult.NotApplicable,
    "Disabled quest-item support never uses flesh.");
Console.WriteLine($"Passed {count} quest-item policy checks. No game calls or inventory were used.");
