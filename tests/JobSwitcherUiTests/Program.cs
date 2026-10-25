using OmniKit.Jobs;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;

var gate = new JobSwitcherInputGate();
Require(!gate.Accept(3, 1, InputState.Up), "Unpaired release must not switch.");
Require(!gate.Accept(3, 1, InputState.Down), "Press must not switch.");
Require(!gate.Accept(3, 1, InputState.Held), "Held input must not repeat.");
Require(gate.Accept(3, 1, InputState.Up), "Matching release must switch once.");
Require(!gate.Accept(3, 1, InputState.Up), "Repeated release must not repeat.");
gate.Accept(3, 1, InputState.Down);
Require(!gate.Accept(4, 1, InputState.Up), "Changing focus must not switch another job.");
gate.Accept(3, 1, InputState.Down);
gate.Reset();
Require(!gate.Accept(3, 1, InputState.Up), "Closing the window clears pending input.");
gate.Accept(3, 1, InputState.Down);
gate.Accept(4, 1, InputState.Held);
Require(!gate.Accept(4, 1, InputState.Up), "Held input must not retarget the original press.");
gate.Accept(3, 1, InputState.Held);
gate.Accept(4, 1, InputState.Held);
gate.Accept(3, 1, InputState.Held);
Require(!gate.Accept(3, 1, InputState.Up), "Returning focus must not rearm an invalidated hold.");
gate.Accept(3, 1, InputState.Down);
gate.Accept(3, 1, InputState.Down);
Require(gate.Accept(3, 1, InputState.Up), "Repeated Down on one class still switches once.");
Require(!gate.Accept(3, 1, InputState.Up), "Repeated release after duplicate Down must not repeat.");
gate.Accept(3, 1, InputState.Held);
Require(gate.Accept(3, 1, InputState.Up), "An initial Held can cover an omitted Down.");
gate.Accept(3, 1, InputState.Down);
gate.Accept(4, 1, InputState.Held);
gate.Accept(4, 1, InputState.Down);
Require(gate.Accept(4, 1, InputState.Up), "A new Down starts a new physical press after invalidation.");
gate.Accept(3, 1, InputState.Down);
gate.Accept(3, 2, InputState.Down);
gate.Accept(3, 1, InputState.Held);
Require(!gate.Accept(3, 1, InputState.Up), "Other controller input must not rearm an ongoing press.");
unsafe
{
    AtkEventData mouse = default;
    Require(JobSwitcherMouseInput.IsLeftClick(&mouse), "Native ButtonId 0 must be accepted as left.");
    mouse.MouseData.ButtonId = 1;
    Require(!JobSwitcherMouseInput.IsLeftClick(&mouse), "Native ButtonId 1 must not activate a job.");
    mouse.MouseData.ButtonId = 2;
    Require(!JobSwitcherMouseInput.IsLeftClick(&mouse), "Other mouse buttons must not activate a job.");
    Require(!JobSwitcherMouseInput.IsLeftClick(null), "Missing mouse data must not activate a job.");
    Require(sizeof(AddonCharacterClass) == 0x8D8, "Bundled CharacterClass layout changed.");
    AddonCharacterClass addon = default;
    Require(addon.ClassComponents.Length == 34 && addon.ClassEntries.Length == 34, "Bundled class arrays changed.");
}
Console.WriteLine("Passed: controller edge/focus/reset cases and bundled CharacterClass layout. Production adapter compiled. No host, profiles, or native game functions initialized.");
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

namespace OmniKit
{
    internal sealed class Configuration { public bool CharacterJobSwitcherEnabled = false; }
}
namespace OmniKit.Jobs
{
    internal sealed class JobSwitchService
    {
        public bool Busy => false;
        public bool TryRequest(uint job) => throw new InvalidOperationException("Native worker must not execute in this fixture.");
    }
}
