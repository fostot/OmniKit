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
