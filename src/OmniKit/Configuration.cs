using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using OmniKit.Input;
using OmniKit.Interaction;
using OmniKit.Assistant;
using OmniKit.Storage;

namespace OmniKit;

public sealed record CompanionAccessoryAdjustment(string Avatar, string Look, string Theme, float OffsetX, float OffsetY, float Scale);

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public const float MinimumDetectionDistance = 1f;
    public const float MaximumDetectionDistance = 30f;
    public const float DefaultDetectionDistance = 6f;
    public int Version { get; set; } = 10;
    public static IReadOnlyList<string> AvatarNames { get; } = Array.AsReadOnly(new[] { "Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism" });
    public static IReadOnlyList<string> OutfitNames { get; } = Array.AsReadOnly(new[] { "Navigator", "Stargazer" });
    public static IReadOnlyList<string> PaletteNames { get; } = Array.AsReadOnly(new[] { "Moonlight", "Aurora", "Nebula", "Rose", "Ember", "Starlight" });
    public bool SetupCompleted = false;
    public bool LauncherEnabled = true;
    public float LauncherWidth = 640f;
    public float LauncherHeight = 64f;
    public float LauncherTextScale = 1f;
    public int LauncherResultCount = 7;
    public float LauncherCenterX = .5f;
    public float LauncherTop = .20f;
    public VirtualKey LauncherKey = VirtualKey.SPACE;
    public ModifierKeys LauncherModifier = ModifierKeys.Alt;
    public VirtualKey SettingsKey = VirtualKey.F10;
    public ModifierKeys SettingsModifier = ModifierKeys.Control;
    public bool SoftInteractEnabled = false;
    public bool QuestItemUseEnabled = false;
    public bool QuestEmoteUseEnabled = false;
    public bool NextActionEnabled = true;
    public bool NextActionLocked = true;
    public float NextActionX = .5f;
    public float NextActionY = .76f;
    public bool QuestItemsBarEnabled = true;
    public bool QuestItemsBarLocked = true;
    public float QuestItemsBarX = .5f;
    public float QuestItemsBarY = .84f;
    public float QuestItemsButtonSize = 40f;
    public InteractionTargets InteractionTargets = InteractionTargets.DefaultWorld;
    public EnemyInteractMode EnemyInteractMode = EnemyInteractMode.Choose;
    public bool InteractionHudEnabled = true;
    public VirtualKey InteractKey = VirtualKey.F;
    public ModifierKeys InteractModifier = ModifierKeys.None;
    public float InteractionCone = 0.10f;
    public float InteractionDistance = DefaultDetectionDistance;
    public float InteractionHudOffsetY = 120f;
    public bool InteractionHudLocked = true;
    public float InteractionHudOffsetX = 0f;
    public float InteractionHudScale = 1f;
    public bool InteractionHudOutline = false;
    public float InteractionHudOutlineThickness = 1f;
    public HudTextEffect InteractionHudEffect = HudTextEffect.Shadow;
    public float InteractionHudEffectIntensity = 1f;
    public float InteractionHudEffectSpeed = 1f;
    public bool InteractionHudEffectAnimated = true;
    public bool GatheringAutoSwitch = false;
    public bool GatheringRestore = true;
    public int MinerGearset = -1;
    public int BotanistGearset = -1;
    public bool AvatarEnabled = true;
    public bool AnimationsEnabled = true;
    public string Avatar = "Moonblob";
    public string CompanionOutfit = "Navigator";
    public string CompanionPalette = "Moonlight";
    public bool CompanionMagicEnabled = true;
    public bool CompanionFrameInteractionEnabled = false;
    public bool CompanionPickupEnabled = true;
    public bool CompanionPerchEnabled = true;
    public bool CompanionContainmentEnabled = true;
    public bool CompanionFallingEnabled = true;
    public bool CompanionHomeEnabled = true;
    public float CompanionHomeX = .835f;
    public float CompanionHomeY = .92f;
    public bool CompanionGrabHandEnabled = true;
    public float CompanionGrabHandScale = .40f;
    public bool CompanionNapsEnabled = true;
    public float CompanionNapInterval = 45f;
    public float CompanionNapDuration = 20f;
    public bool CompanionNapZsEnabled = true;
    public bool CompanionCommentsEnabled = true;
    public float CompanionCommentInterval = 120f;
    public bool CompanionBlinkEnabled = true;
    public float CompanionBlinkInterval = 3f;
    public bool CompanionFourierBlinkEnabled = false;
    public bool CompanionSeasonalEnabled = true;
    public string CompanionSeasonalTheme = "Auto";
    public bool CompanionAutoRotateEnabled = false;
    public float CompanionRotationInterval = 300f;
    public string CompanionVariant = "Random";
    public bool CompanionLookAutoRotateEnabled = false;
    public float CompanionLookInterval = 300f;
    public string CompanionHomeStyle = "Default";
    public bool CompanionHolidayHomeEnabled = true;
    public bool CompanionHolidayBodiesEnabled = true;
    public string CompanionHolidayBody = "Auto";
    public List<CompanionAccessoryAdjustment> CompanionAccessoryFits = [];
    public bool CompanionTransitionEffectsEnabled = true;
    public string CompanionHandStyle = "Default";
    public bool CompanionGroundedEnabled = true;
    public bool CompanionAutoHopEnabled = true;
    public bool CompanionOrbitRingsEnabled = true;
    public float CompanionOrbitRingSpeed = 1f;
    public static IReadOnlyList<string> SeasonalThemeNames { get; } = Array.AsReadOnly(new[]
    { "Auto", "AllSaintsWake", "Starlight", "Heavensturn", "Valentione", "LittleLadies", "HatchingTide", "MakeItRain", "MoonfireFaire", "TheRising" });
    public static IReadOnlyList<string> CompanionVariantNames { get; } = Array.AsReadOnly(new[]
    { "Default", "Random", "Look1", "Look2", "Look3", "Look4" });
    public static IReadOnlyList<string> HomeStyleNames { get; } = Array.AsReadOnly(new[]
    { "Default", "Random", "CrystalPod", "CloudCushion", "LanternHut" }.Concat(SeasonalThemeNames.Skip(1)).ToArray());
    public static IReadOnlyList<string> HandStyleNames { get; } = Array.AsReadOnly(new[]
    { "Default", "Random", "IvoryChibi", "StarlightGlove", "MagicalPaw", "HologramMitt" });
    public float CompanionBoxLeft = .72f;
    public float CompanionBoxTop = .55f;
    public float CompanionBoxRight = .95f;
    public float CompanionBoxBottom = .92f;
    public float CompanionSize = 96f;
    public bool VoiceEnabled = false;
    public bool AiEnabled = false;
    public AiConnectionKind AiConnection = AiConnectionKind.ApiKey;
    public string AiEndpoint = "https://api.openai.com/v1/responses";
    public string AiModel = "";
    public string ChatGptModel = "";
    public string CodexModel = "";
    public string ClaudeModel = "";
    public List<VoiceBinding> VoiceBindings = [];
    public bool WikiInGame = true;
    public bool BrowserInterceptLinks = false;
    public string WikiOverlayName = "omnikit";

    [NonSerialized] internal Action<Configuration>? Persist;
    public void Save()
    {
        if (Persist != null) Persist(this);
        else Services.PluginInterface.SavePluginConfig(this);
    }
}
