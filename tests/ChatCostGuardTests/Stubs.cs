using OmniKit.Assistant;

namespace OmniKit;

// Only the configuration surface consumed by the actual linked ChatService.
public sealed class Configuration
{
    public bool AiEnabled = true;
    public AiConnectionKind AiConnection = AiConnectionKind.ApiKey;
    public string AiEndpoint = "http://127.0.0.1:1/v1/responses";
    public string AiModel = "synthetic-api-model";
    public string ChatGptModel = "synthetic-chatgpt-model";
    public string CodexModel = "";
    public string ClaudeModel = "";
    public string Avatar = "Moonblob";
    public static readonly string[] AvatarNames = ["Moonblob"];
}

internal static class Services
{
    public static SyntheticPluginInterface PluginInterface { get; } = new();
}

internal sealed class SyntheticPluginInterface
{
    public DirectoryInfo ConfigDirectory { get; set; } = null!;
}
