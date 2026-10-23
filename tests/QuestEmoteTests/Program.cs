using OmniKit.Interaction;

var checks = 0;
void Check(bool value, string message) { ++checks; if (!value) throw new InvalidOperationException(message); }
var good = new QuestEmoteContext(1002449, 135, true, true, 2, 1, 0, true, true, true);
foreach (var coachman in new uint[] { 1002449, 1002450 })
{
    var current = good with { TargetDataId = coachman }; var calls = 0;
    Check(QuestEmotePolicy.CanUse(current), "Each verified coachman qualifies at the required step.");
    Check(QuestEmotePolicy.TryUse(true, current, () => { calls++; return true; }) == QuestEmoteUseResult.Used && calls == 1,
        "One deliberate request makes exactly one emote attempt.");
}
foreach (var unrelated in new uint[] { 0, 1002231, 1002447, 1002448, 1002451, 1002452, 2000756, uint.MaxValue })
{
    var current = good with { TargetDataId = unrelated };
    Check(!QuestEmotePolicy.CanUse(current), "Other quest actors cannot receive the soothing rule.");
    Check(QuestEmotePolicy.TryUse(true, current, () => throw new Exception("Must not act")) == QuestEmoteUseResult.NotApplicable,
        "Unmapped actors retain ordinary interaction.");
}
Check(QuestEmotePolicy.TryUse(false, good, () => throw new Exception("Must not act")) == QuestEmoteUseResult.NotApplicable,
    "Off by default can never issue an emote.");
var blocked = new[]
{
    good with { TerritoryId = 0 }, good with { TerritoryId = 134 }, good with { Targetable = false },
    good with { QuestAccepted = false }, good with { Sequence = 0 }, good with { Sequence = 1 },
    good with { Sequence = 3 }, good with { Sequence = 255 }, good with { ActiveQuestHandlers = 0 },
    good with { ActiveQuestHandlers = 2 }, good with { ActiveQuestHandlers = -1 },
    good with { OtherActiveQuestHandlers = 1 }, good with { MetadataMatches = false },
    good with { EmoteUnlocked = false }, good with { EmoteAllowed = false },
};
foreach (var current in blocked)
{
    Check(!QuestEmotePolicy.CanUse(current), "Missing or ambiguous context rejects the action.");
    Check(QuestEmotePolicy.BlockReason(current).Length > 0, "A rejected guard has a fixed safe explanation.");
    Check(QuestEmotePolicy.TryUse(true, current, () => throw new Exception("Must not act")) == QuestEmoteUseResult.Blocked,
        "A matched failure cannot fall through to dialogue or a second native action.");
}
Check(QuestEmotePolicy.CanOffer(good with { EmoteAllowed = false, EmoteUnlocked = false }),
    "Observation does not require executing native emote-state queries.");
var rejected = 0;
Check(QuestEmotePolicy.TryUse(true, good, () => { rejected++; return false; }) == QuestEmoteUseResult.Blocked && rejected == 1,
    "Native rejection cannot retry or fall through.");
Check(QuestEmotePolicy.BlockReason(good) == string.Empty, "Usable context shows no routine success status.");
Check(QuestEmotePolicy.BlockReason(good with { Sequence = 3, EmoteUnlocked = false }) == "The required quest step is not active.",
    "Quest-step guidance precedes emote availability.");
Check(QuestEmotePolicy.BlockReason(good with { ActiveQuestHandlers = 2 }).Contains("ambiguous", StringComparison.Ordinal),
    "Competing native context is distinct from unlock failure.");
Check(QuestEmotePolicy.BlockReason(good with { EmoteUnlocked = false }) != QuestEmotePolicy.BlockReason(good with { EmoteAllowed = false }),
    "Unlock and current-state failures have different guidance.");
Console.WriteLine($"Passed {checks} quest-emote policy checks. No game calls, quest state or profiles were used.");
