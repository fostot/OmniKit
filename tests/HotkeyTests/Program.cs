using Dalamud.Game.ClientState.Keys;
using OmniKit.Input;

for (var expected = 0; expected < 8; expected++)
for (var actual = 0; actual < 8; actual++)
{
    Check(HotkeyChord.Matches(true, (ModifierKeys)actual, (ModifierKeys)expected) == (actual == expected),
        "Every combination must require an exact modifier mask.");
    Check(!HotkeyChord.Matches(false, (ModifierKeys)actual, (ModifierKeys)expected), "A released key must never activate.");
    Check(!HotkeyChord.Matches(true, (ModifierKeys)actual, (ModifierKeys)expected, true), "A Windows-key chord must not activate.");
}
Check(!HotkeyChord.IsValidModifiers((ModifierKeys)8) && !HotkeyChord.IsValidModifiers((ModifierKeys)(-1)), "Unknown modifier bits must be rejected.");
Check(HotkeyChord.IsValidKey(VirtualKey.E) && HotkeyChord.IsValidKey(VirtualKey.F12) && HotkeyChord.IsValidKey(VirtualKey.OEM_PLUS), "Ordinary keys must be accepted.");
foreach (var invalid in new[] { 0, 1, 7, 16, 17, 18, 91, 92, 160, 161, 162, 163, 164, 165, 255 })
    Check(!HotkeyChord.IsValidKey((VirtualKey)invalid), "Mouse, modifier and undefined main keys must be rejected.");
var sideKeys = new[] { VirtualKey.RCONTROL, VirtualKey.LMENU, VirtualKey.RSHIFT };
Check(HotkeyChord.ReadModifiers(sideKeys.Contains) == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift), "Left/right modifiers must normalize together.");
Check(HotkeyChord.Format(VirtualKey.E, ModifierKeys.Control | ModifierKeys.Shift) == "Ctrl + Shift + E", "Chord labels must include every modifier.");

var edge = new HotkeyPressState();
Check(!edge.Update(true, true, true), "A key held at startup must not fire.");
Check(!edge.Update(false, false, true), "Release does not fire.");
Check(edge.Update(true, true, true), "One physical key-down fires once.");
Check(!edge.Update(true, true, true), "Holding does not repeat.");
Check(!edge.Update(true, false, true) && !edge.Update(true, true, true), "Modifier changes while the main key is held do not retrigger.");
edge.Update(false, false, true);
Check(!edge.Update(true, true, false) && !edge.Update(true, true, true), "A blocked press cannot become a delayed action when capture/focus ends.");
edge.Update(false, false, true);
Check(edge.Update(true, true, true), "A new press after release is accepted.");

var capture = new HotkeyCaptureState();
capture.Begin([]);
Check(!capture.Sample([VirtualKey.CONTROL, VirtualKey.SHIFT]), "Modifiers alone keep listening.");
Check(!capture.Sample([VirtualKey.CONTROL, VirtualKey.SHIFT, VirtualKey.E]), "Main key-down records but does not commit.");
Check(!capture.Sample([VirtualKey.CONTROL, VirtualKey.SHIFT]), "Releasing only the main key does not commit.");
Check(!capture.Sample([VirtualKey.SHIFT]), "Every modifier must be released.");
Check(capture.Sample([]) && !capture.IsCapturing, "All keys released commits exactly once.");
Check(capture.Key == VirtualKey.E && capture.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift), "Ctrl+Shift+E remains complete after releasing modifiers.");
Check(!capture.Sample([]), "A completed capture cannot emit twice.");

capture.Begin([]);
capture.Sample([VirtualKey.CONTROL, VirtualKey.MENU, VirtualKey.SHIFT, VirtualKey.E]);
capture.Sample([VirtualKey.E]);
Check(capture.Sample([]) && capture.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift), "Releasing modifiers before the main key preserves the original full chord.");
capture.Begin([]);
capture.Sample([VirtualKey.E]);
capture.Sample([VirtualKey.E, VirtualKey.CONTROL]);
Check(capture.Sample([]) && capture.Modifiers == ModifierKeys.None, "Modifiers pressed after main key-down are not added retroactively.");

capture.Begin([VirtualKey.CONTROL, VirtualKey.A]);
capture.Sample([VirtualKey.CONTROL]);
capture.Sample([VirtualKey.CONTROL, VirtualKey.E]);
Check(!capture.Sample([]) && capture.IsCapturing, "Keys already down when capture is armed must be ignored until release.");
capture.Sample([VirtualKey.F12]);
Check(capture.Sample([]) && capture.Key == VirtualKey.F12, "Capture starts cleanly after the initial release.");
capture.Begin([]);
capture.Sample([VirtualKey.E]);
Check(!capture.Sample([VirtualKey.ESCAPE]) && !capture.IsCapturing, "Escape cancels a pending chord.");
capture.Begin([]);
capture.Sample([VirtualKey.A, VirtualKey.B]);
Check(!capture.Sample([]) && capture.IsCapturing, "An ambiguous multi-key press must be released and retried.");
capture.Sample([VirtualKey.LWIN, VirtualKey.E]);
Check(!capture.Sample([]) && capture.IsCapturing, "Unsupported Windows-key chords must not silently lose their modifier.");
capture.Cancel();
Check(!capture.IsCapturing && !capture.Sample([]), "Explicit cancellation emits no binding.");

Console.WriteLine("Hotkey checks passed: exact masks, physical press edges, all modifier combinations and complete-release capture/cancellation.");

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
