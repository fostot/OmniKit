using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;

namespace OmniKit.Input;

[Flags]
public enum ModifierKeys
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
}

internal static class HotkeyChord
{
    public static ModifierKeys CurrentModifiers => ReadModifiers(IsKeyDown);

    public static bool IsDown(VirtualKey key, ModifierKeys modifiers) =>
        IsValidKey(key) && Matches(IsKeyDown(key), CurrentModifiers, modifiers,
            IsKeyDown(VirtualKey.LWIN) || IsKeyDown(VirtualKey.RWIN));

    public static bool IsKeyDown(VirtualKey key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

    public static bool IsValidKey(VirtualKey key) => (int)key is > 7 and < 255 &&
        Enum.IsDefined(key) && !IsModifier(key) && key is not (VirtualKey.LWIN or VirtualKey.RWIN);

    public static bool IsValidModifiers(ModifierKeys modifiers) =>
        (modifiers & ~(ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)) == 0;

    internal static bool IsModifier(VirtualKey key) => (int)key is 16 or 17 or 18 or >= 160 and <= 165;

    internal static bool Matches(bool keyDown, ModifierKeys current, ModifierKeys expected, bool windowsKeyDown = false) =>
        keyDown && !windowsKeyDown && IsValidModifiers(expected) && current == expected;

    internal static ModifierKeys ReadModifiers(Func<VirtualKey, bool> down)
    {
        var result = ModifierKeys.None;
        if (down(VirtualKey.CONTROL) || down(VirtualKey.LCONTROL) || down(VirtualKey.RCONTROL)) result |= ModifierKeys.Control;
        if (down(VirtualKey.MENU) || down(VirtualKey.LMENU) || down(VirtualKey.RMENU)) result |= ModifierKeys.Alt;
        if (down(VirtualKey.SHIFT) || down(VirtualKey.LSHIFT) || down(VirtualKey.RSHIFT)) result |= ModifierKeys.Shift;
        return result;
    }

    public static string Format(VirtualKey key, ModifierKeys modifiers)
    {
        var parts = new List<string>(4);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(key switch
        {
            VirtualKey.SPACE => "Space",
            VirtualKey.RETURN => "Enter",
            VirtualKey.BACK => "Backspace",
            VirtualKey.ESCAPE => "Escape",
            _ => key.ToString(),
        });
        return string.Join(" + ", parts);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}

/// <summary>Tracks the physical main key even while input is blocked or modifiers do not match.</summary>
internal sealed class HotkeyPressState
{
    private bool initialized;
    private bool wasDown;

    public bool Update(bool keyDown, bool chordMatches, bool allowed)
    {
        var pressed = initialized && keyDown && !wasDown && chordMatches && allowed;
        initialized = true;
        wasDown = keyDown;
        return pressed;
    }
}

/// <summary>Pure per-frame capture state. A completed chord is emitted only after every key is released.</summary>
internal sealed class HotkeyCaptureState
{
    private enum Phase { Idle, ReleaseInitial, Listening, ReleaseChord }
    private Phase phase;
    public bool IsCapturing => phase != Phase.Idle;
    public VirtualKey Key { get; private set; }
    public ModifierKeys Modifiers { get; private set; }
    public string Prompt { get; private set; } = "Press a shortcut";

    public void Begin(IReadOnlyCollection<VirtualKey> downKeys)
    {
        phase = downKeys.Count == 0 ? Phase.Listening : Phase.ReleaseInitial;
        Prompt = phase == Phase.Listening ? "Press a shortcut..." : "Release held keys...";
    }

    public void Cancel() { phase = Phase.Idle; }

    public bool Sample(IReadOnlyCollection<VirtualKey> downKeys)
    {
        if (!IsCapturing) return false;
        if (downKeys.Contains(VirtualKey.ESCAPE)) { Cancel(); return false; }
        if (phase == Phase.ReleaseInitial)
        {
            if (downKeys.Count == 0) { phase = Phase.Listening; Prompt = "Press a shortcut..."; }
            return false;
        }
        if (phase == Phase.ReleaseChord)
        {
            if (downKeys.Count != 0) return false;
            phase = Phase.Idle;
            return true;
        }
        var mainKeys = downKeys.Where(HotkeyChord.IsValidKey).ToArray();
        if (downKeys.Any(key => key is VirtualKey.LWIN or VirtualKey.RWIN) || mainKeys.Length > 1)
        {
            phase = Phase.ReleaseInitial;
            Prompt = "Use one key with Ctrl, Alt or Shift";
            return false;
        }
        if (mainKeys.Length == 0) return false;
        Key = mainKeys[0];
        Modifiers = HotkeyChord.ReadModifiers(downKeys.Contains);
        phase = Phase.ReleaseChord;
        Prompt = "Release " + HotkeyChord.Format(Key, Modifiers);
        return false;
    }
}
