using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using OmniKit.Input;

namespace OmniKit.Ui;

/// <summary>A purely visual companion with a bounded, opt-in area picker.</summary>
internal sealed class RoamingCompanion(Configuration config, Action openCompanion)
{
    private readonly CompanionMotion motion = new();
    private readonly string atlasPath = Path.Combine(Services.PluginInterface.AssemblyLocation.DirectoryName!,
        "assets", "companions-walk.png");
    private long lastTick;
    private bool hoveredLastFrame;
    private string lastAvatar = string.Empty;
    private bool areaDragging;
    private Vector2 areaStart;
    private string areaHint = string.Empty;

    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus
        | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDocking;

    public bool IsChoosingArea { get; private set; }

    public void BeginChooseArea()
    {
        if (!config.AvatarEnabled) return;
        IsChoosingArea = true;
        areaDragging = false;
        areaHint = "Drag an area for your companion. Escape cancels.";
    }

    public void CancelChooseArea()
    {
        IsChoosingArea = false;
        areaDragging = false;
        areaHint = string.Empty;
    }

    public void Draw(float scale)
    {
        if (!config.AvatarEnabled)
        {
            CancelChooseArea();
            lastTick = 0;
            hoveredLastFrame = false;
            motion.Reset();
            return;
        }
        var viewport = ImGui.GetMainViewport();
        var texture = Services.Textures.GetFromFile(atlasPath).GetWrapOrDefault();
        var aspect = texture == null ? 1f : (texture.Width / 4f) / (texture.Height / 2f);
        if (IsChoosingArea)
        {
            DrawAreaPicker(viewport.Pos, viewport.Size, scale, aspect);
            lastTick = 0;
            return;
        }
        if (texture == null) return;
        if (lastAvatar != config.Avatar)
        {
            lastAvatar = config.Avatar;
            motion.Reset();
        }

        var layout = CompanionLayout.Create(viewport.Pos, viewport.Size,
            new Vector4(config.CompanionBoxLeft, config.CompanionBoxTop, config.CompanionBoxRight, config.CompanionBoxBottom),
            config.CompanionSize, scale, aspect);
        var now = Environment.TickCount64;
        var elapsed = lastTick == 0 ? 0 : (now - lastTick) / 1000f;
        lastTick = now;
        motion.Update(layout, elapsed, config.AnimationsEnabled && !hoveredLastFrame);
        if (!layout.IsVisible) return;
        var row = config.Avatar == "Moonbear" ? 1 : 0;
        var uv = CompanionLayout.FrameUv(motion.Frame, row, motion.FacingLeft);
        ImGui.GetForegroundDrawList().AddImage(texture.Handle, motion.Position, motion.Position + layout.SpriteSize,
            uv.Start, uv.End);

        // Tight visible-body bounds exclude the atlas's large transparent gutters.
        // The artwork has an irregular silhouette, so this is a rectangular hit
        // region, not per-pixel alpha hit testing.
        var body = row == 0 ? new Vector4(.20f, .08f, .88f, .99f) : new Vector4(.24f, .10f, .90f, .90f);
        if (motion.FacingLeft) body = new Vector4(1 - body.Z, body.Y, 1 - body.X, body.W);
        var hitStart = motion.Position + layout.SpriteSize * new Vector2(body.X, body.Y);
        var hitEnd = motion.Position + layout.SpriteSize * new Vector2(body.Z, body.W);
        hoveredLastFrame = ImGui.IsMouseHoveringRect(hitStart, hitEnd, false);
        if (!hoveredLastFrame || ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel))
            return;

        var visible = BeginOverlay("##OmniKitRoamingCompanion", hitStart, hitEnd - hitStart);
        var clicked = false;
        if (visible)
        {
            ImGui.InvisibleButton("##companion", hitEnd - hitStart, ImGuiButtonFlags.MouseButtonLeft);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        }
        ImGui.End();
        if (clicked) openCompanion();
    }

    private void DrawAreaPicker(Vector2 viewportPosition, Vector2 viewportSize, float scale, float aspect)
    {
        if (HotkeyChord.IsKeyDown(VirtualKey.ESCAPE))
        {
            Services.Keys[VirtualKey.ESCAPE] = false;
            CancelChooseArea();
            return;
        }
        if (viewportSize.X <= 0 || viewportSize.Y <= 0) return;
        if (!BeginOverlay("##OmniKitChooseCompanionArea", viewportPosition, viewportSize))
        {
            ImGui.End();
            return;
        }
        ImGui.InvisibleButton("##choose-area", viewportSize, ImGuiButtonFlags.MouseButtonLeft);
        var mouse = Vector2.Clamp(ImGui.GetMousePos(), viewportPosition, viewportPosition + viewportSize);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            areaStart = mouse;
            areaDragging = true;
        }
        var draw = ImGui.GetForegroundDrawList();
        draw.AddRectFilled(viewportPosition, viewportPosition + viewportSize, 0x42000000);
        var hintPosition = viewportPosition + new Vector2(20, 20) * Math.Clamp(scale, .5f, 2f);
        var wrapWidth = Math.Max(1f, viewportPosition.X + viewportSize.X - hintPosition.X - 20);
        draw.AddText(ImGui.GetFont(), ImGui.GetFontSize(), hintPosition + Vector2.One, 0xFF000000, areaHint, wrapWidth);
        draw.AddText(ImGui.GetFont(), ImGui.GetFontSize(), hintPosition, 0xFFF0F3F6, areaHint, wrapWidth);
        var oldStart = viewportPosition + viewportSize * new Vector2(config.CompanionBoxLeft, config.CompanionBoxTop);
        var oldEnd = viewportPosition + viewportSize * new Vector2(config.CompanionBoxRight, config.CompanionBoxBottom);
        draw.AddRect(oldStart, oldEnd, 0x809BC9DD);
        if (areaDragging)
        {
            var start = Vector2.Min(areaStart, mouse);
            var end = Vector2.Max(areaStart, mouse);
            var full = CompanionLayout.Create(viewportPosition, viewportSize, new Vector4(0, 0, 1, 1),
                config.CompanionSize, scale, aspect);
            var valid = CompanionLayout.TryNormalizeArea(start, end, viewportPosition, viewportSize,
                full.SpriteSize, out var normalized);
            draw.AddRectFilled(start, end, valid ? 0x309BC9DDu : 0x303E5CE0u);
            draw.AddRect(start, end, valid ? 0xFF9BC9DD : 0xFF3E5CE0, 0, ImDrawFlags.None, 2f);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                areaDragging = false;
                if (valid)
                    SaveArea(new Vector2(normalized.X, normalized.Y), new Vector2(normalized.Z, normalized.W));
                else
                    areaHint = "That area is too small. Drag a larger area, or press Escape to cancel.";
            }
        }
        ImGui.End();
    }

    private void SaveArea(Vector2 minimum, Vector2 maximum)
    {
        var old = new Vector4(config.CompanionBoxLeft, config.CompanionBoxTop,
            config.CompanionBoxRight, config.CompanionBoxBottom);
        config.CompanionBoxLeft = minimum.X;
        config.CompanionBoxTop = minimum.Y;
        config.CompanionBoxRight = maximum.X;
        config.CompanionBoxBottom = maximum.Y;
        try
        {
            config.Save();
            CancelChooseArea();
            motion.Reset();
        }
        catch (Exception exception)
        {
            config.CompanionBoxLeft = old.X;
            config.CompanionBoxTop = old.Y;
            config.CompanionBoxRight = old.Z;
            config.CompanionBoxBottom = old.W;
            areaHint = "The area could not be saved. Try again, or press Escape to cancel.";
            Services.Log.Error("Companion area could not be saved ({ErrorType}).", exception.GetType().Name);
        }
    }

    private static bool BeginOverlay(string id, Vector2 position, Vector2 size)
    {
        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.One);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        var visible = ImGui.Begin(id, OverlayFlags);
        ImGui.PopStyleVar(3);
        return visible;
    }
}
