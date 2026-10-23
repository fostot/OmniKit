using OmniKit.Interaction;

var checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}

var heldAtEnable = new SoftInteractionPress();
Check(!heldAtEnable.Sample(69, 0, true), "Enabling with a held key cannot trigger.");
Check(!heldAtEnable.Sample(69, 0, true), "A held key cannot repeat.");
Check(!heldAtEnable.Sample(69, 0, false), "Releasing does not trigger.");
Check(heldAtEnable.Sample(69, 0, true), "A fresh press may trigger.");
heldAtEnable.Consume();
Check(heldAtEnable.Consumed, "An eligible press can suppress native repeats.");
Check(!heldAtEnable.Sample(69, 0, true), "Suppression cannot manufacture another key edge.");
heldAtEnable.Pause();
Check(!heldAtEnable.Consumed, "Opening UI releases native key suppression.");
Check(!heldAtEnable.Sample(69, 0, true), "Closing UI while holding cannot trigger.");
Check(!heldAtEnable.Consumed, "Closing UI cannot resume suppression for the old hold.");
heldAtEnable.Sample(69, 0, false);
Check(heldAtEnable.Sample(69, 0, true), "Release and repress after UI permits one new edge.");

var pausedPress = new SoftInteractionPress();
pausedPress.Sample(69, 1, false);
Check(pausedPress.Sample(69, 1, true), "Track the physical edge even if the caller rejects its chord.");
pausedPress.Pause();
Check(!pausedPress.Sample(69, 1, true), "Adding the required modifier after holding E cannot trigger.");
Check(!pausedPress.Sample(69, 1, true), "Removing and readding modifiers while E is held cannot trigger.");
pausedPress.Sample(69, 1, false);
Check(pausedPress.Sample(69, 1, true), "A new primary press with the correct chord may trigger.");
pausedPress.Consume();
Check(!pausedPress.Sample(69, 3, true) && !pausedPress.Consumed,
    "Changing configured modifiers while held resets suppression without triggering.");
Check(!pausedPress.Sample(70, 3, true), "Changing the configured key while it is held cannot trigger.");
pausedPress.Sample(70, 3, false);
Check(pausedPress.Sample(70, 3, true), "A fresh press works after rebinding.");
pausedPress.Consume();
pausedPress.Sample(70, 3, false);
Check(!pausedPress.Consumed, "Physical release clears suppression.");

Console.WriteLine($"Interaction input policy: {checks} checks passed.");

var defaults = InteractionTargets.DefaultWorld;
foreach (var category in Enum.GetValues<InteractionTargets>().Where(kind => kind is not (InteractionTargets.None or InteractionTargets.DefaultWorld or InteractionTargets.All)))
{
    Check(SoftTargetPolicy.IsEnabled(defaults, category) == !SoftTargetPolicy.IsEnemy(category), "Existing world targets stay enabled; enemies require opt-in.");
    Check(!SoftTargetPolicy.IsEnabled(InteractionTargets.None, category), "Turning every category off must disable all candidates.");
    Check(SoftTargetPolicy.ActionFor(InteractionTargets.All, EnemyInteractMode.Choose, category)
        == (SoftTargetPolicy.IsEnemy(category) ? SoftTargetAction.None : SoftTargetAction.UseObject),
        "Choosing no enemy action cannot grant enemy targeting or attacks.");
}
foreach (var enemy in new[] { InteractionTargets.HostileEnemies, InteractionTargets.NeutralEnemies })
{
    Check(SoftTargetPolicy.ActionFor(enemy, EnemyInteractMode.TargetOnly, enemy) == SoftTargetAction.TargetEnemy, "Target-only must never request starting auto-attack.");
    Check(SoftTargetPolicy.ActionFor(enemy, EnemyInteractMode.StartAttack, enemy) == SoftTargetAction.AttackEnemy, "Starting auto-attack needs both the category and action choice.");
    Check(SoftTargetPolicy.ActionFor(defaults, EnemyInteractMode.StartAttack, enemy) == SoftTargetAction.None, "An attack preference alone cannot enable enemy interactions.");
    Check(SoftTargetPolicy.ActionFor(enemy, (EnemyInteractMode)99, enemy) == SoftTargetAction.None, "Unknown enemy actions fail closed.");
}
Check(!SoftTargetPolicy.IsEnabled(InteractionTargets.HostileEnemies, InteractionTargets.NeutralEnemies)
    && !SoftTargetPolicy.IsEnabled(InteractionTargets.NeutralEnemies, InteractionTargets.HostileEnemies), "Hostile and neutral categories are independent.");
Check(!SoftTargetPolicy.IsEnabled(InteractionTargets.All, InteractionTargets.All)
    && !SoftTargetPolicy.IsEnabled((InteractionTargets)2048, InteractionTargets.Npcs), "Invalid masks and compound candidate categories fail closed.");
Console.WriteLine($"Interaction category/action policy: {checks} combined checks passed.");

foreach (var action in new[] { SoftTargetAction.UseObject, SoftTargetAction.TargetEnemy, SoftTargetAction.AttackEnemy })
{
    foreach (var failure in new[] { "none", "select", "confirm", "action" })
    {
        var calls = new List<string>();
        var result = SoftTargetPolicy.AfterConfirmedTarget(
            () => { calls.Add("select"); return failure != "select"; },
            () => { calls.Add("confirm"); return failure != "confirm"; },
            () => { calls.Add(action.ToString()); return failure != "action"; });
        var expected = failure switch
        {
            "select" => new[] { "select" },
            "confirm" => new[] { "select", "confirm" },
            _ => new[] { "select", "confirm", action.ToString() },
        };
        Check(calls.SequenceEqual(expected), "Every action selects and confirms first, stops on rejection and never retries.");
        Check(result == (failure == "none"), "Failed target selection, confirmation or action is propagated.");
    }
}

var expectedId = 123UL;
var expectedAddress = (nint)456;
foreach (var identity in new[] { (Id: 789UL, Address: expectedAddress), (Id: expectedId, Address: (nint)987) })
{
    var interactions = 0;
    Check(!SoftTargetPolicy.AfterConfirmedTarget(() => true,
            () => identity.Id == expectedId && identity.Address == expectedAddress,
            () => { interactions++; return true; }) && interactions == 0,
        "A changed target ID or recycled native address prevents the interaction.");
}

var orderingPress = new SoftInteractionPress();
var selections = 0;
var uses = 0;
void Frame(bool down)
{
    if (!orderingPress.Sample(69, 0, down)) return;
    orderingPress.Consume();
    SoftTargetPolicy.AfterConfirmedTarget(() => { selections++; return true; }, () => true, () => { uses++; return true; });
}
Frame(false); Frame(false);
Check(selections == 0 && uses == 0, "Observation without a fresh press neither selects nor interacts.");
Frame(true); Frame(true); Frame(true);
Check(selections == 1 && uses == 1, "A held physical key produces only one select-confirm-action sequence.");
Frame(false); Frame(true);
Check(selections == 2 && uses == 2, "A later fresh press permits exactly one new sequence.");

Console.WriteLine($"Interaction target-before-action ordering: {checks} combined checks passed (synthetic delegates only).");

foreach (var name in new[] { "Inventory", "InventoryLarge", "InventoryExpansion", "InventoryGrid", "InventoryGrid0",
    "InventoryGrid1", "InventoryGrid0E", "InventoryGrid1E", "InventoryGrid2E", "InventoryGrid3E" })
{
    Check(SoftInteractionUiPolicy.PauseReason(false, false, true, name) == null, "Passive player bags may retain focus.");
    Check(SoftInteractionUiPolicy.PauseReason(true, false, true, name) != null, "An inventory text filter still blocks interaction.");
    Check(SoftInteractionUiPolicy.PauseReason(false, true, true, name) != null, "An active NPC, vendor or gathering interaction wins over bag focus.");
}
foreach (var name in new string?[] { "ChatLog", "Talk", "Shop", "RetainerList", "InventoryRetainer", "InventoryRetainerLarge",
    "InventoryBuddy", "InventoryEvent", "ConfigSystem", "ContextMenu", "InventoryGrid4E", "InventoryUnknown", "inventory", "", null })
    Check(SoftInteractionUiPolicy.PauseReason(false, false, true, name) != null, "Chat, vendor, retainer and unknown focused menus stay blocked.");
Check(SoftInteractionUiPolicy.PauseReason(false, false, false, null) == null, "No focused menu permits the other interaction guards to decide.");
Check(SoftInteractionUiPolicy.PauseReason(false, false, false, "Talk") == null, "An invisible unfocused addon alone does not pause input.");
Check(SoftInteractionUiPolicy.PauseReason(true, false, false, null) != null, "Text input blocks even without a focused addon.");
Check(SoftInteractionUiPolicy.PauseReason(false, true, false, null) != null, "World interaction lifecycle blocks even between addon pages.");
var alreadySelected = true;
var duplicateSelections = 0;
var repeatedTargetActions = 0;
bool SelectAgain() { duplicateSelections++; return false; }
Check(SoftTargetPolicy.AfterConfirmedTarget(() => alreadySelected || SelectAgain(), () => alreadySelected,
    () => { repeatedTargetActions++; return true; }) && duplicateSelections == 0 && repeatedTargetActions == 1,
    "An already selected confirmed object can act without depending on a duplicate native selection result.");
Console.WriteLine($"Interaction input/focus policy: {checks} combined checks passed (synthetic only).");
