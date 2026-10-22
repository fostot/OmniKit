using FFXIVClientStructs.FFXIV.Client.UI;
using OmniKit.Input;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace OmniKit.Ui;

internal sealed class ShortcutInput
{
    private readonly HotkeyPressState launcherPress = new();
    private readonly HotkeyPressState settingsPress = new();

    public static unsafe bool GameAcceptsInput()
    {
        var framework = NativeFramework.Instance();
        var atk = RaptureAtkModule.Instance();
        return Services.ClientState.IsLoggedIn && framework != null && !framework->WindowInactive
            && atk != null && !atk->IsTextInputActive();
    }

    public void Update(Configuration config, bool textInput, bool launcherSearchActive, Action launcher, Action settings, bool captureInProgress = false)
    {
        var launch = HotkeyChord.IsDown(config.LauncherKey, config.LauncherModifier);
        var setup = HotkeyChord.IsDown(config.SettingsKey, config.SettingsModifier);
        var allowed = !captureInProgress && GameAcceptsInput();
        if (launcherPress.Update(HotkeyChord.IsKeyDown(config.LauncherKey), launch,
                allowed && config.LauncherEnabled && (!textInput ||
                    (launcherSearchActive && config.LauncherModifier != ModifierKeys.None))))
        { Services.Keys[config.LauncherKey] = false; launcher(); }
        if (settingsPress.Update(HotkeyChord.IsKeyDown(config.SettingsKey), setup, allowed && !textInput))
        { Services.Keys[config.SettingsKey] = false; settings(); }
    }

}
