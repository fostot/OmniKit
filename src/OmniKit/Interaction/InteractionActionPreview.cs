namespace OmniKit.Interaction;

internal sealed record InteractionActionPreview(ulong TargetId, nint Address, string Name, string Action,
    uint IconId, bool CanUse, string Reason);
