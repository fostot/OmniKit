using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;

Console.WriteLine("Native emote binding proof compiled. No game functions were called.");

internal static class BindingProof
{
    // Compiler-only proof against the locally installed API15 bindings. Never invoked.
    internal static unsafe bool Execute(GameObject* target, Emote row)
    {
        var manager = EmoteManager.Instance(); var state = UIState.Instance();
        if (manager == null || state == null || target == null || row.TextCommand.RowId != 436
            || row.UnlockLink != 0 || !state->IsEmoteUnlocked(35) || !manager->CanExecuteEmote(35)
            || EmoteController.PlayEmoteOption.StaticVirtualTablePointer == null) return false;
        var options = new EmoteController.PlayEmoteOption
        {
            VirtualTable = EmoteController.PlayEmoteOption.StaticVirtualTablePointer,
            TargetId = target->GetGameObjectId(),
        };
        return manager->ExecuteEmote(35, &options);
    }
}
