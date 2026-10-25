using OmniKit.Jobs;

var checks = 0;
void Check(bool condition, string name)
{
    checks++;
    if (!condition) throw new Exception(name);
}
void BlockedWithoutSteps(JobSwitchContext context, string name)
{
    var plan = JobSwitchPlan.Build(context);
    Check(!plan.Allowed && plan.Reason.Length > 0 && plan.CurrentSnapshot == null
        && plan.TargetSnapshot == null && plan.Weapon == null && plan.SoulCrystal == null
        && plan.ExistingGearsetIndex == null && plan.RestoreGearsetIndex == null, name);
}

var weapon = new OwnedJobItem(100, 3500, 2, 1, 50, false, true, false);
var soul = new OwnedJobItem(200, 3200, 0, 30, 1, false, true, true);
var context = new JobSwitchContext(1, "Gladiator", 19, "Paladin", true, true, 50, 200,
    [], [2, 4], [weapon, soul]);
var plan = JobSwitchPlan.Build(context);
Check(plan.Allowed && plan.CurrentSnapshot == new NewJobGearset(2, 1, "Gladiator")
    && plan.TargetSnapshot == new NewJobGearset(4, 19, "Paladin")
    && plan.Weapon == weapon && plan.SoulCrystal == soul && plan.RestoreGearsetIndex == 2, "snapshot both missing classes");
BlockedWithoutSteps(context with { FreeGearsetSlots = [2] }, "two absent sets need two slots");
BlockedWithoutSteps(context with { CanSwitch = false }, "busy state blocks all steps");
BlockedWithoutSteps(context with { TargetUnlocked = false }, "locked job blocks all steps");
BlockedWithoutSteps(context with { TargetLevel = 0 }, "zero level blocks all steps");
BlockedWithoutSteps(context with { CurrentJobName = "" }, "missing current name blocks snapshot");
BlockedWithoutSteps(context with { OwnedItems = [soul] }, "no weapon blocks before current snapshot");
BlockedWithoutSteps(context with { OwnedItems = [weapon] }, "no soul blocks before current snapshot");
BlockedWithoutSteps(context with { OwnedItems = [weapon with { RequiredLevel = 51 }, soul] }, "weapon level restriction");
BlockedWithoutSteps(context with { OwnedItems = [weapon with { CanEquipTarget = false }, soul] }, "weapon class restriction");
BlockedWithoutSteps(context with { OwnedItems = [weapon with { ItemId = 0 }, soul] }, "zero weapon ID rejected");
BlockedWithoutSteps(context with { OwnedItems = [weapon with { Slot = -1 }, soul] }, "invalid weapon locator rejected");
BlockedWithoutSteps(context with { OwnedItems = [weapon, soul with { ItemId = 201 }] }, "wrong soul rejected");
BlockedWithoutSteps(context with { OwnedItems = [weapon, soul with { CanEquipTarget = false }] }, "incompatible soul rejected");

plan = JobSwitchPlan.Build(context with { Gearsets = [new(5, 1, true, true)], FreeGearsetSlots = [2] });
Check(plan.Allowed && plan.CurrentSnapshot == null && plan.TargetSnapshot?.Index == 2
    && plan.RestoreGearsetIndex == 5, "reuse exact current class equipment set");
plan = JobSwitchPlan.Build(context with { Gearsets = [new(5, 19, true)], FreeGearsetSlots = [2], OwnedItems = [] });
Check(plan.Allowed && plan.ExistingGearsetIndex == 5 && plan.CurrentSnapshot?.Index == 2
    && plan.RestoreGearsetIndex == 2, "existing target preserves unsaved current equipment");
BlockedWithoutSteps(context with { Gearsets = [new(5, 19, true)], FreeGearsetSlots = [], OwnedItems = [] },
    "existing target cannot bypass current snapshot capacity");
plan = JobSwitchPlan.Build(context with { Gearsets = [new(5, 1, true, true), new(6, 19, true)], FreeGearsetSlots = [], OwnedItems = [] });
Check(plan.Allowed && plan.ExistingGearsetIndex == 6 && plan.CurrentSnapshot == null
    && plan.RestoreGearsetIndex == 5, "matching current plus existing target needs no slots");
plan = JobSwitchPlan.Build(context with { Gearsets = [new(5, 1, true), new(6, 19, true)], FreeGearsetSlots = [], OwnedItems = [] });
Check(plan.Allowed && plan.ExistingGearsetIndex == 6 && plan.CurrentSnapshot == null && plan.RestoreGearsetIndex == null,
    "dirty existing current plus existing target needs no slots and never creates a duplicate");
plan = JobSwitchPlan.Build(context with { Gearsets = [new(5, 1, true)], FreeGearsetSlots = [2, 4] });
Check(plan.Allowed && plan.CurrentSnapshot == null && plan.TargetSnapshot?.Index == 2
    && plan.RestoreGearsetIndex == null, "existing current class never creates a duplicate or pretends dirty gear is a matching restore set");
plan = JobSwitchPlan.Build(context with { Gearsets = [new(5, 1, true)], FreeGearsetSlots = [2] });
Check(plan.Allowed && plan.CurrentSnapshot == null && plan.TargetSnapshot?.Index == 2,
    "existing current class plus absent target needs only one slot");
plan = JobSwitchPlan.Build(context with { Gearsets = [new(5, 1, false, true)], FreeGearsetSlots = [2, 4] });
Check(plan.Allowed && plan.CurrentSnapshot == null && plan.RestoreGearsetIndex == null,
    "broken existing current set prevents duplicates without serving as a restore set");
plan = JobSwitchPlan.Build(context with { Gearsets = [new(5, 99, true, true)], FreeGearsetSlots = [2, 4] });
Check(plan.Allowed && plan.CurrentSnapshot?.Index == 2, "different class match cannot serve as restore set");
BlockedWithoutSteps(context with { Gearsets = [new(5, 19, false)] }, "broken target set never overwritten");
plan = JobSwitchPlan.Build(context with { Gearsets = [new(5, 19, false), new(6, 19, true)] });
Check(plan.Allowed && plan.ExistingGearsetIndex == 6, "prefer valid existing target");
BlockedWithoutSteps(context with { FreeGearsetSlots = [2, 2] }, "duplicate free slots not counted twice");
BlockedWithoutSteps(context with { Gearsets = [new(2, 99, true)], FreeGearsetSlots = [2, 4] }, "occupied free slot not overwritten");

var better = weapon with { ItemId = 101, ItemLevel = 51, Slot = 3 };
var highQuality = weapon with { ItemId = 102, HighQuality = true, Slot = 4 };
plan = JobSwitchPlan.Build(context with { OwnedItems = [highQuality, better, weapon, soul] });
Check(plan.Weapon == better, "item level precedes HQ");
plan = JobSwitchPlan.Build(context with { OwnedItems = [weapon, highQuality, soul] });
Check(plan.Weapon == highQuality, "HQ breaks equal item level");
plan = JobSwitchPlan.Build(context with { OwnedItems = [weapon with { Slot = 5 }, soul, weapon] });
Check(plan.Weapon == weapon, "stable inventory position breaks ties");
plan = JobSwitchPlan.Build(context with { RequiredSoulCrystalId = 0, OwnedItems = [weapon] });
Check(plan.Allowed && plan.SoulCrystal == null, "base class needs no soul");
plan = JobSwitchPlan.Build(context with { TargetJobId = 1, TargetJobName = "Gladiator", OwnedItems = [], FreeGearsetSlots = [2] });
Check(plan.Allowed && plan.CurrentSnapshot?.Index == 2 && plan.TargetSnapshot == null && plan.Weapon == null,
    "current class creates one snapshot without equipment changes");
plan = JobSwitchPlan.Build(context with { TargetJobId = 1, TargetJobName = "Gladiator", OwnedItems = [],
    Gearsets = [new(5, 1, true)], FreeGearsetSlots = [2] });
Check(plan.Allowed && plan.CurrentSnapshot == null && plan.ExistingGearsetIndex == null
    && plan.RestoreGearsetIndex == null, "current class dirty existing set is a no-op without creating or overwriting anything");
plan = JobSwitchPlan.Build(context with { TargetJobId = 1, TargetJobName = "Gladiator", OwnedItems = [],
    Gearsets = [new(5, 1, true, true)], FreeGearsetSlots = [] });
Check(plan.Allowed && plan.CurrentSnapshot == null && plan.ExistingGearsetIndex == null
    && plan.RestoreGearsetIndex == 5, "current class exact set is a no-op");
Check(context.Gearsets.Count == 0 && context.FreeGearsetSlots.SequenceEqual(new[] { 2, 4 })
    && context.OwnedItems.SequenceEqual(new[] { weapon, soul }), "planning does not mutate observations");
Console.WriteLine($"PASS: {checks} job switch plan checks.");
