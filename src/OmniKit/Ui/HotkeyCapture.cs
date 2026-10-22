using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using OmniKit.Input;

namespace OmniKit.Ui;

internal sealed class HotkeyCapture
{
    private static readonly VirtualKey[] KeyboardKeys = Enum.GetValues<VirtualKey>()
        .Where(key => (int)key is > 7 and < 255).Distinct().ToArray();
    private readonly HotkeyCaptureState state = new();
    private string? activeLabel;

    public bool IsCapturing => state.IsCapturing;

    public void Cancel() { state.Cancel(); activeLabel = null; }

    public bool Draw(string label, ref VirtualKey key, ref ModifierKeys modifiers)
    {
        var changed = false;
        if (activeLabel == label && state.IsCapturing)
        {
            if (state.Sample(PressedKeys()))
            {
                key = state.Key;
                modifiers = state.Modifiers;
                changed = true;
            }
            if (!state.IsCapturing) activeLabel = null;
        }
        ImGui.PushID(label);
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        var capturingThis = activeLabel == label && state.IsCapturing;
        if (ImGui.Button((capturingThis ? state.Prompt : HotkeyChord.Format(key, modifiers)) + "##capture"))
        {
            activeLabel = label;
            state.Begin(PressedKeys());
        }
        if (capturingThis)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Cancel")) Cancel();
            ImGui.TextDisabled("Hold modifiers, press a key, then release all keys. Escape cancels.");
        }
        ImGui.PopID();
        return changed;
    }

    private static VirtualKey[] PressedKeys() => KeyboardKeys.Where(HotkeyChord.IsKeyDown).ToArray();
}
