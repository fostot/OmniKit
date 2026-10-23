namespace OmniKit;

// Only the appearance fields used by the linked production control renderer.
// No persisted settings, host services or game state are initialized.
internal sealed class Configuration
{
    public float LauncherWidth = 640;
    public float LauncherTextScale = 1;
    public int LauncherResultCount = 7;
    public float LauncherCenterX = .5f;
    public float LauncherTop = .20f;
}
