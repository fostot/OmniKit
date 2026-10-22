using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using OmniKit.Storage;

namespace OmniKit;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool SetupCompleted = false;
    public bool LauncherEnabled = true;
    public VirtualKey LauncherKey = VirtualKey.SPACE;
    public VirtualKey LauncherModifier = VirtualKey.MENU;
    public VirtualKey SettingsKey = VirtualKey.F10;
    public VirtualKey SettingsModifier = VirtualKey.CONTROL;
    public bool SoftInteractEnabled = false;
    public bool InteractionHudEnabled = true;
    public VirtualKey InteractKey = VirtualKey.F;
    public float InteractionCone = 0.10f;
    public float InteractionDistance = 3.5f;
    public float InteractionHudOffsetY = 120f;
    public bool GatheringAutoSwitch = false;
    public bool GatheringRestore = true;
    public int MinerGearset = -1;
    public int BotanistGearset = -1;
    public bool AvatarEnabled = true;
    public bool AnimationsEnabled = true;
    public string Avatar = "Bimble";
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
