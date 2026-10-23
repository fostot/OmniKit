using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace OmniKit.Ui;

/// <summary>Draw-thread snapshots only; no retained native pointers or foreign UI mutations.</summary>
internal sealed unsafe class CompanionSurfaces
{
    private readonly List<CompanionSurface> surfaces = [];
    public IReadOnlyList<CompanionSurface> Read(Vector2 viewportPosition, Vector2 viewportSize)
    {
        surfaces.Clear();
        if (Services.ClientState.IsLoggedIn && !Services.Conditions[ConditionFlag.BetweenAreas]
            && !Services.Conditions[ConditionFlag.BetweenAreas51]) ReadNative(viewportPosition, viewportSize);
        var context = ImGui.GetCurrentContext();
        if (context.Handle == null) return surfaces;
        for (var i = 0; i < Math.Min(context.Windows.Size, 2048); i++)
        {
            var window = context.Windows[i];
            if (window.Handle == null || window.Hidden || window.IsFallbackWindow || window.Collapsed
                || (!window.Active && !window.WasActive) || window.LastFrameActive < context.FrameCount - 1
                || (window.Flags & (ImGuiWindowFlags.ChildWindow | ImGuiWindowFlags.Popup | ImGuiWindowFlags.Tooltip | ImGuiWindowFlags.NoMouseInputs)) != 0) continue;
            var nameLength = 0;
            while (window.Name != null && nameLength < 160 && window.Name[nameLength] != 0) nameLength++;
            var name = window.Name == null ? string.Empty : Encoding.UTF8.GetString(new ReadOnlySpan<byte>(window.Name, nameLength));
            if (name.Contains("##OmniKitRoamingCompanion", StringComparison.Ordinal)
                || name.Contains("##OmniKitChooseCompanionArea", StringComparison.Ordinal)
                || name.Contains("##OmniKitCompanionComment", StringComparison.Ordinal)
                || name.Contains("##OmniKitCompanionHouse", StringComparison.Ordinal)
                || name.Contains("##OmniKitVisibleTarget", StringComparison.Ordinal)
                || name.Contains("##OmniKitInteract", StringComparison.Ordinal)) continue;
            var header = (window.Flags & ImGuiWindowFlags.NoTitleBar) != 0 ? Math.Min(12, window.Size.Y)
                : Math.Clamp(window.InnerRect.Min.Y - window.Pos.Y, 12, 45);
            Add(new CompanionSurface(0x8000000000000000ul | window.ID, window.Pos, window.Pos + window.Size,
                header, 10000 + i), viewportPosition, viewportSize);
        }
        return surfaces;
    }

    private void ReadNative(Vector2 viewportPosition, Vector2 viewportSize)
    {
        var stage = AtkStage.Instance();
        if (stage == null || stage->RaptureAtkUnitManager == null) return;
        ref var list = ref stage->RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;
        for (var i = 0; i < Math.Min(list.Count, list.Entries.Length); i++)
        {
            var unit = list.Entries[i].Value;
            if (unit == null || !unit->IsReady || !unit->IsVisible || unit->RootNode == null
                || unit->RootNode->IsDrawDisabled || (unit->RootNode->NodeFlags & NodeFlags.Visible) == 0) continue;
            var position = viewportPosition + new Vector2(unit->X, unit->Y);
            var size = new Vector2(unit->GetScaledWidth(true), unit->GetScaledHeight(true));
            var header = unit->WindowHeaderCollisionNode;
            var headerHeight = header == null ? Math.Min(12, size.Y) : Math.Clamp(header->Height * unit->Scale, 10, 45);
            Add(new CompanionSurface(((ulong)(nuint)unit << 16) ^ unit->Id, position, position + size,
                headerHeight, (int)unit->DepthLayer * 512 + unit->DrawOrderIndex), viewportPosition, viewportSize);
        }
    }

    private void Add(CompanionSurface surface, Vector2 viewportPosition, Vector2 viewportSize)
    {
        var size = surface.Size;
        if (!float.IsFinite(surface.Minimum.X) || !float.IsFinite(surface.Minimum.Y)
            || !float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 20 || size.Y < 8
            || size.X > viewportSize.X * .95f && size.Y > viewportSize.Y * .9f) return;
        if (surface.Maximum.X <= viewportPosition.X || surface.Maximum.Y <= viewportPosition.Y
            || surface.Minimum.X >= viewportPosition.X + viewportSize.X || surface.Minimum.Y >= viewportPosition.Y + viewportSize.Y) return;
        surfaces.Add(surface);
    }
}
