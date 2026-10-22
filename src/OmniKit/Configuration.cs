using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using OmniKit.Input;
using OmniKit.Storage;

namespace OmniKit;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;
    public bool SetupCompleted = false;
    public bool LauncherEnabled = true;
    public VirtualKey LauncherKey = VirtualKey.SPACE;
    public ModifierKeys LauncherModifier = ModifierKeys.Alt;
    public VirtualKey SettingsKey = VirtualKey.F10;
    public ModifierKeys SettingsModifier = ModifierKeys.Control;
    public bool SoftInteractEnabled = false;
    public bool InteractionHudEnabled = true;
    public VirtualKey InteractKey = VirtualKey.F;
    public ModifierKeys InteractModifier = ModifierKeys.None;
    public float InteractionCone = 0.10f;
    public float InteractionDistance = 3.5f;
    public float InteractionHudOffsetY = 120f;
    public bool InteractionHudLocked = true;
    public float InteractionHudOffsetX = 0f;
    public float InteractionHudScale = 1f;
    public bool GatheringAutoSwitch = false;
    public bool GatheringRestore = true;
    public int MinerGearset = -1;
    public int BotanistGearset = -1;
    public bool AvatarEnabled = true;
    public bool AnimationsEnabled = true;
    public string Avatar = "Bimble";
    public float CompanionBoxLeft = .72f;
    public float CompanionBoxTop = .55f;
    public float CompanionBoxRight = .95f;
    public float CompanionBoxBottom = .92f;
    public float CompanionSize = 96f;
    public bool VoiceEnabled = false;
    public bool AiEnabled = false;
    public string AiEndpoint = "https://api.openai.com/v1/responses";
    public string AiModel = "";
    public List<VoiceBinding> VoiceBindings = [];
    public bool WikiInGame = false;
    public string WikiOverlayName = "omnikit";

    [NonSerialized] internal Action<Configuration>? Persist;
    public void Save()
    {
        if (Persist != null) Persist(this);
        else Services.PluginInterface.SavePluginConfig(this);
    }
}
