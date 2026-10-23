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
        var inkBudget = 65536;
        for (var i = 0; i < Math.Min(context.Windows.Size, 2048); i++)
        {
            var window = context.Windows[i];
            if (window.Handle == null || window.Hidden || window.IsFallbackWindow || window.Collapsed
                || (!window.Active && !window.WasActive) || window.LastFrameActive < context.FrameCount - 1
                || (window.Flags & (ImGuiWindowFlags.ChildWindow | ImGuiWindowFlags.Popup | ImGuiWindowFlags.Tooltip)) != 0) continue;
            var nameLength = 0;
            while (window.Name != null && nameLength < 160 && window.Name[nameLength] != 0) nameLength++;
            var name = window.Name == null ? string.Empty : Encoding.UTF8.GetString(new ReadOnlySpan<byte>(window.Name, nameLength));
            if (name.Contains("##OmniKitRoamingCompanion", StringComparison.Ordinal)
                || name.Contains("##OmniKitChooseCompanionArea", StringComparison.Ordinal)
                || name.Contains("##OmniKitCompanionComment", StringComparison.Ordinal)
                || name.Contains("##OmniKitCompanionHouse", StringComparison.Ordinal)
                || name.Contains("##OmniKitVisibleTarget", StringComparison.Ordinal)
                || name.Contains("##OmniKitInteract", StringComparison.Ordinal)) continue;
            var minimum = window.Pos;
            var maximum = window.Pos + window.Size;
            if ((window.Flags & ImGuiWindowFlags.NoMouseInputs) != 0)
            {
                // DelvUI-style frames intentionally pass mouse input through.
                // Require actual visible geometry, not an empty input overlay,
                // and do not reinterpret a fullscreen HUD canvas as a platform.
                if (window.Size.X > viewportSize.X * .95f && window.Size.Y > viewportSize.Y * .9f
                    || !TryInkBounds(window.DrawList, ref inkBudget, out var inkMinimum, out var inkMaximum)) continue;
                minimum = Vector2.Max(minimum, inkMinimum);
                maximum = Vector2.Min(maximum, inkMaximum);
            }
            var header = (window.Flags & ImGuiWindowFlags.NoTitleBar) != 0 ? Math.Min(12, maximum.Y - minimum.Y)
                : Math.Clamp(window.InnerRect.Min.Y - window.Pos.Y, 12, 45);
            Add(new CompanionSurface(0x8000000000000000ul | window.ID, minimum, maximum,
                header, 10000 + i), viewportPosition, viewportSize);
        }
        return surfaces;
    }

    private static bool TryInkBounds(ImDrawListPtr draw, ref int budget, out Vector2 minimum, out Vector2 maximum)
    {
        minimum = new Vector2(float.MaxValue);
        maximum = new Vector2(float.MinValue);
        if (draw.Handle == null || draw.VtxBuffer.Size is < 3 or > 16384 || draw.CmdBuffer.Size > 512
            || draw.IdxBuffer.Size > budget) return false;
        budget -= draw.IdxBuffer.Size;
        for (var c = 0; c < draw.CmdBuffer.Size; c++)
        {
            var command = draw.CmdBuffer[c];
            if (command.ElemCount == 0) continue;
            if ((ulong)command.IdxOffset + command.ElemCount > (ulong)draw.IdxBuffer.Size) return false;
            var first = new Vector2(float.MaxValue); var last = new Vector2(float.MinValue);
            for (uint i = 0; i < command.ElemCount; i++)
            {
                var index = (ulong)draw.IdxBuffer[(int)(command.IdxOffset + i)] + command.VtxOffset;
                if (index >= (ulong)draw.VtxBuffer.Size) return false;
                var vertex = draw.VtxBuffer[(int)index];
                if ((vertex.Col >> 24) < 16 || !float.IsFinite(vertex.Pos.X) || !float.IsFinite(vertex.Pos.Y)) continue;
                first = Vector2.Min(first, vertex.Pos);
                last = Vector2.Max(last, vertex.Pos);
            }
            first = Vector2.Max(first, new Vector2(command.ClipRect.X, command.ClipRect.Y));
            last = Vector2.Min(last, new Vector2(command.ClipRect.Z, command.ClipRect.W));
            if (first.X >= last.X || first.Y >= last.Y) continue;
            minimum = Vector2.Min(minimum, first);
            maximum = Vector2.Max(maximum, last);
        }
        return minimum.X < maximum.X && minimum.Y < maximum.Y;
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
