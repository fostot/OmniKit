using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.UI;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace OmniKit.Ui;

internal sealed class ShortcutInput
{
    private bool launcherHeld;
    private bool settingsHeld;
    private bool initialized;

    public static unsafe bool GameAcceptsInput()
    {
        var framework = NativeFramework.Instance();
        var atk = RaptureAtkModule.Instance();
        return Services.ClientState.IsLoggedIn && framework != null && !framework->WindowInactive
            && atk != null && !atk->IsTextInputActive();
    }

    public void Update(Configuration config, bool textInput, bool launcherSearchActive, Action launcher, Action settings)
    {
        var launch = Down(config.LauncherKey) && ModifiersMatch(config.LauncherModifier);
        var setup = Down(config.SettingsKey) && ModifiersMatch(config.SettingsModifier);
        if (!initialized) { launcherHeld = launch; settingsHeld = setup; initialized = true; }
        if (GameAcceptsInput())
        {
            if (config.LauncherEnabled && launch && !launcherHeld &&
                (!textInput || (launcherSearchActive && config.LauncherModifier != 0)))
            { Services.Keys[config.LauncherKey] = false; launcher(); }
            if (setup && !settingsHeld && !textInput)
            { Services.Keys[config.SettingsKey] = false; settings(); }
        }
        launcherHeld = launch; settingsHeld = setup;
    }

    private static bool ModifiersMatch(VirtualKey modifier) =>
        Down(VirtualKey.CONTROL) == (modifier == VirtualKey.CONTROL) &&
        Down(VirtualKey.MENU) == (modifier == VirtualKey.MENU) &&
        Down(VirtualKey.SHIFT) == (modifier == VirtualKey.SHIFT);

    private static bool Down(VirtualKey key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
