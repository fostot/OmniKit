namespace OmniKit.Assistant;

public enum AiConnectionKind
{
    ApiKey = 0,
    ChatGpt = 1,
    CodexCli = 2,
    ClaudeCli = 3,
}

public sealed record AiModelOption(string Id, string Name);
