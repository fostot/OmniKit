using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using OmniKit.Interaction;

namespace OmniKit.Ui;

/// <summary>Quiet icon surfaces: warm thin borders; chrome appears only while unlocked.</summary>
internal sealed class QuestActionHud(Configuration config, SoftInteractionService interaction)
{
    private int page;
    private string? dragging;
    private Vector2 dragOffset;
    private bool positionDirty;
    private Vector4? nextPanelBounds;
    public bool OwnsActionInput { get; private set; }
    private const ImGuiWindowFlags Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoDocking;

    public void Draw(float scale)
    {
        OwnsActionInput = false;
        nextPanelBounds = null;
        if (interaction.HudHiddenForInteraction) { FinishDrag(); return; }
        scale = Math.Clamp(float.IsFinite(scale) ? scale : 1, .5f, 3);
        if (config.NextActionEnabled && (config.SoftInteractEnabled || !config.NextActionLocked)) DrawNext(scale);
        if (config.QuestItemsBarEnabled) DrawItems(scale);
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) FinishDrag();
    }

    private void DrawNext(float scale)
    {
        var preview = interaction.NextAction;
        if (preview == null || preview.IconId == 0 || preview.QuestKind is not (QuestActionKind.EventItem or QuestActionKind.Emote)) return;
        if (interaction.ActionPaused) return;
        var edge = 52 * scale;
        var size = new Vector2(edge);
        if (!BeginPanel("NextAction", "NEXT ACTION", size, config.NextActionLocked,
                ref config.NextActionX, ref config.NextActionY, scale, out var origin)) { ImGui.End(); return; }
        var lockMinimum = config.NextActionLocked ? origin + new Vector2(edge - 17 * scale, 2 * scale) : default;
        var overLock = config.NextActionLocked && ImGui.IsMouseHoveringRect(lockMinimum, lockMinimum + new Vector2(15 * scale));
        var enabled = preview.CanUse && !interaction.ActionPaused;
        var clicked = ActionButtonRenderer.Draw("next", origin, size, preview.IconId,
            "·", 0, enabled,
            preview.Action + ": " + preview.Name + (preview.Reason.Length == 0 ? "" : "\n" + preview.Reason), out var ownsInput);
        OwnsActionInput |= ownsInput;
        if (clicked && !overLock && preview != null) interaction.RequestNextAction(preview);
        if (LockButton("next-lock", origin, size, config.NextActionLocked, scale))
        { config.NextActionLocked = !config.NextActionLocked; Save(); }
        ImGui.End();
    }

    private void DrawItems(float scale)
    {
        var items = interaction.Inventory.Snapshot;
        if (items.Count == 0 && config.QuestItemsBarLocked) return;
        var edge = Math.Clamp(config.QuestItemsButtonSize, 24, 56) * scale;
        var viewport = ImGui.GetMainViewport();
        var capacity = Math.Clamp((int)((viewport.Size.X - 60 * scale) / (edge + 5 * scale)), 1, 8);
        var pages = Math.Max(1, (items.Count + capacity - 1) / capacity);
        page = Math.Clamp(page, 0, pages - 1);
        var visible = Math.Max(1, Math.Min(capacity, items.Count - page * capacity));
        var size = new Vector2(visible * edge + (visible - 1) * 5 * scale + (pages > 1 ? 44 * scale : 0), edge);
        if (!BeginPanel("QuestItems", "QUEST ITEMS", size, config.QuestItemsBarLocked,
                ref config.QuestItemsBarX, ref config.QuestItemsBarY, scale, out var origin)) { ImGui.End(); return; }
        var lockMinimum = origin + new Vector2(size.X - 17 * scale, 2 * scale);
        var overLock = config.QuestItemsBarLocked && ImGui.IsMouseHoveringRect(lockMinimum, lockMinimum + new Vector2(15 * scale));
        for (var index = 0; index < visible; index++)
        {
            var offset = page * capacity + index;
            var hasItem = offset < items.Count;
            var item = hasItem ? items[offset] : default;
            var tooltip = hasItem ? item.Name + (item.Reason.Length == 0 ? "" : "\n" + item.Reason)
                : "No supported usable quest items in your bags.";
            var clicked = ActionButtonRenderer.Draw("item-" + index, origin + new Vector2(index * (edge + 5 * scale), 0),
                new Vector2(edge), hasItem ? item.IconId : 0, "·", hasItem ? item.Count : 0,
                hasItem && item.CanUse && !interaction.InventoryActionPaused, tooltip, out var ownsInput);
            OwnsActionInput |= ownsInput;
            if (clicked && !overLock && hasItem) interaction.RequestQuestItem(item);
        }
        if (pages > 1)
        {
            var navigation = origin + new Vector2(size.X - 40 * scale, edge - 21 * scale);
            if (ActionButtonRenderer.Draw("prev-page", navigation, new Vector2(18, 20) * scale,
                    0, "<", 0, true, $"Previous page ({page + 1} / {pages})", out _)) page = (page + pages - 1) % pages;
            if (ActionButtonRenderer.Draw("next-page", navigation + new Vector2(21 * scale, 0), new Vector2(18, 20) * scale,
                    0, ">", 0, true, $"Next page ({page + 1} / {pages})", out _)) page = (page + 1) % pages;
        }
        if (LockButton("items-lock", origin, size, config.QuestItemsBarLocked, scale))
        { config.QuestItemsBarLocked = !config.QuestItemsBarLocked; Save(); }
        ImGui.End();
    }

    private bool BeginPanel(string id, string title, Vector2 contentSize, bool locked,
        ref float x, ref float y, float scale, out Vector2 origin)
    {
        var viewport = ImGui.GetMainViewport();
        var padding = locked ? 0 : 6 * scale;
        var heading = locked ? 0 : 20 * scale;
        var size = contentSize + new Vector2(padding * 2, padding * 2 + heading);
        if (!locked)
        {
            var titleWidth = ImGui.CalcTextSize(title).X / Math.Max(1, ImGui.GetFontSize()) * 11 * scale;
            size.X = Math.Max(size.X, titleWidth + 34 * scale);
        }
        var desired = viewport.Pos + viewport.Size * new Vector2(x, y) - new Vector2(size.X * .5f, 0);
        var position = Vector2.Clamp(desired, viewport.Pos, Vector2.Max(viewport.Pos, viewport.Pos + viewport.Size - size));
        if (!locked && dragging == id && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            position = Vector2.Clamp(ImGui.GetMousePos() - dragOffset, viewport.Pos,
                Vector2.Max(viewport.Pos, viewport.Pos + viewport.Size - size));
            x = Math.Clamp((position.X + size.X * .5f - viewport.Pos.X) / Math.Max(1, viewport.Size.X), 0, 1);
            y = Math.Clamp((position.Y - viewport.Pos.Y) / Math.Max(1, viewport.Size.Y), 0, 1);
            positionDirty = true;
        }
        if (id == "QuestItems" && nextPanelBounds is { } other
            && position.X < other.Z && position.X + size.X > other.X
            && position.Y < other.W && position.Y + size.Y > other.Y)
        {
            var below = other.W + 8 * scale;
            var above = other.Y - size.Y - 8 * scale;
            if (below + size.Y <= viewport.Pos.Y + viewport.Size.Y) position.Y = below;
            else if (above >= viewport.Pos.Y) position.Y = above;
            else if (other.Z + 8 * scale + size.X <= viewport.Pos.X + viewport.Size.X) position.X = other.Z + 8 * scale;
            else if (other.X - size.X - 8 * scale >= viewport.Pos.X) position.X = other.X - size.X - 8 * scale;
        }
        if (id == "NextAction") nextPanelBounds = new(position.X, position.Y, position.X + size.X, position.Y + size.Y);
        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        var open = ImGui.Begin("##OmniKit" + id, Flags);
        ImGui.PopStyleVar();
        origin = position + new Vector2(padding, padding + heading);
        if (open && !locked)
        {
            var draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(position, position + size, 0xE5191717, 4 * scale);
            draw.AddRect(position + Vector2.One, position + size - Vector2.One, 0xFF514038, 4 * scale);
            draw.AddText(ImGui.GetFont(), 11 * scale, position + new Vector2(7, 5) * scale, 0xFFB3A59B, title);
            var dragMaximum = position + new Vector2(size.X - 21 * scale, heading + padding);
            if (ImGui.IsMouseHoveringRect(position + new Vector2(3 * scale), dragMaximum))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) { dragging = id; dragOffset = ImGui.GetMousePos() - position; }
            }
        }
        return open;
    }

    private static bool LockButton(string id, Vector2 origin, Vector2 size, bool locked, float scale)
    {
        var minimum = locked ? origin + new Vector2(size.X - 17 * scale, 2 * scale)
            : ImGui.GetWindowPos() + new Vector2(ImGui.GetWindowSize().X - 21 * scale, 6 * scale);
        ImGui.SetCursorScreenPos(minimum);
        var clicked = ImGui.InvisibleButton(id, new Vector2(15 * scale));
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(minimum, minimum + new Vector2(15 * scale), 0xD51B1818, 3 * scale);
        var color = ImGui.IsItemHovered() ? 0xFFE9D8C5u : 0xFFB5A495u;
        draw.AddRect(minimum + new Vector2(4, 7) * scale, minimum + new Vector2(11, 12) * scale, color, 1 * scale);
        draw.PathArcTo(minimum + new Vector2(7.5f, 7) * scale, 3 * scale,
            locked ? MathF.PI : MathF.PI * 1.3f, locked ? MathF.Tau : MathF.PI * 2.3f, 10);
        draw.PathStroke(color, ImDrawFlags.None, scale);
        if (ImGui.IsItemHovered()) { ImGui.SetMouseCursor(ImGuiMouseCursor.Hand); ActionButtonRenderer.Tooltip(locked ? "Unlock to move" : "Lock position"); }
        return clicked;
    }

    private void FinishDrag()
    {
        dragging = null;
        if (!positionDirty) return;
        positionDirty = false;
        Save();
    }
    private void Save()
    {
        try { config.Save(); }
        catch (Exception error) { Services.Log.Error("Action-bar settings could not be saved ({ErrorType}).", error.GetType().Name); }
    }
}

internal static class ActionButtonRenderer
{
    // Native rendering harness supplies a synthetic texture; production uses host game icons.
    internal static Func<uint, ImTextureID>? TextureForTest { get; set; }
    public static bool Draw(string id, Vector2 position, Vector2 size, uint iconId, string fallback,
        int count, bool enabled, string tooltip, out bool ownsInput)
    {
        ImGui.SetCursorScreenPos(position);
        ImGui.BeginDisabled(!enabled);
        var clicked = ImGui.InvisibleButton(id, size);
        ownsInput = ImGui.IsItemActive() || clicked;
        ImGui.SetItemAllowOverlap();
        ImGui.EndDisabled();
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(position, position + size, 0xC5191615, 5);
        draw.AddRect(position + Vector2.One, position + size - Vector2.One,
            enabled && ImGui.IsItemHovered() ? 0xFFB69270u : 0xFF514038u, 5);
        ImTextureID texture = default;
        if (iconId != 0)
            texture = TextureForTest != null ? TextureForTest(iconId)
                : Services.Textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault()?.Handle ?? default;
        var tint = enabled ? 0xFFFFFFFFu : 0x90FFFFFFu;
        if (texture != default(ImTextureID))
            draw.AddImageRounded(texture, position + new Vector2(3), position + size - new Vector2(3),
                Vector2.Zero, Vector2.One, tint, 3);
        else
        {
            var textSize = ImGui.CalcTextSize(fallback);
            draw.AddText(position + (size - textSize) * .5f, enabled ? 0xFFEEDCC4u : 0xFF81756Bu, fallback);
        }
        if (count > 1)
        {
            var text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var at = position + size - ImGui.CalcTextSize(text) - new Vector2(4);
            draw.AddText(at + Vector2.One, 0xFF000000, text);
            draw.AddText(at, 0xFFFFFFFF, text);
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) Tooltip(tooltip);
        return clicked;
    }

    internal static void Tooltip(string text)
    {
        var viewport = ImGui.GetMainViewport();
        var padding = ImGui.GetStyle().WindowPadding * 2;
        var availableWidth = Math.Max(1, viewport.Size.X - padding.X - 16);
        var wrap = Math.Min(340, availableWidth);
        var measured = ImGui.CalcTextSize(text, false, wrap);
        if (measured.Y + padding.Y + 2 > viewport.Size.Y - 8 && wrap < availableWidth)
        {
            wrap = availableWidth;
            measured = ImGui.CalcTextSize(text, false, wrap);
        }
        var size = new Vector2(wrap + padding.X, MathF.Ceiling(measured.Y) + padding.Y + 2);
        var position = Vector2.Clamp(ImGui.GetMousePos() + new Vector2(12, 18), viewport.Pos + new Vector2(4),
            Vector2.Max(viewport.Pos + new Vector2(4), viewport.Pos + viewport.Size - size - new Vector2(4)));
        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.BeginTooltip();
        // A tooltip already ignores mouse input. Drawing directly avoids a
        // child's extra padding/scrollbar reducing the measured text height.
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
