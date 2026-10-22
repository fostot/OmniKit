using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using OmniKit.Input;

namespace OmniKit.Ui;

/// <summary>A purely visual companion with a bounded, opt-in area picker.</summary>
internal sealed class RoamingCompanion(Configuration config, Action openCompanion)
{
    private readonly CompanionMotion motion = new();
    private readonly string assetDirectory = Path.Combine(Services.PluginInterface.AssemblyLocation.DirectoryName!, "assets");
    private long lastTick;
    private float animationTime;
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
    public CompanionPose PreviewPose { get; set; } = CompanionPose.Automatic;

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
        const float aspect = 1f;
        if (IsChoosingArea)
        {
            DrawAreaPicker(viewport.Pos, viewport.Size, scale, aspect);
            lastTick = 0;
            return;
        }
        if (lastAvatar != config.Avatar)
        {
            lastAvatar = config.Avatar;
            motion.Reset();
            animationTime = 0;
        }

        var layout = CompanionLayout.Create(viewport.Pos, viewport.Size,
            new Vector4(config.CompanionBoxLeft, config.CompanionBoxTop, config.CompanionBoxRight, config.CompanionBoxBottom),
            config.CompanionSize, scale, aspect);
        var now = Environment.TickCount64;
        var elapsed = lastTick == 0 ? 0 : (now - lastTick) / 1000f;
        lastTick = now;
        if (config.AnimationsEnabled)
            animationTime = (animationTime + Math.Clamp(elapsed, 0, .05f)) % 1000;
        motion.Update(layout, elapsed, config.AnimationsEnabled && !hoveredLastFrame && PreviewPose == CompanionPose.Automatic);
        if (!layout.IsVisible) return;
        var pose = !config.AnimationsEnabled ? CompanionPose.Idle : PreviewPose == CompanionPose.Automatic
            ? motion.Walking ? CompanionPose.Walk : CompanionPose.Idle : PreviewPose;
        var draw = ImGui.GetForegroundDrawList();
        var hitStart = motion.Position + layout.SpriteSize * .18f;
        var hitEnd = motion.Position + layout.SpriteSize * .82f;
        draw.PushClipRect(layout.Minimum, layout.Maximum, true);
        try
        {
            if (ProceduralCompanion.IsProcedural(config.Avatar))
                ProceduralCompanion.Draw(draw, motion.Position, layout.SpriteSize, config, pose, animationTime, hoveredLastFrame);
            else
            {
                var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory,
                    CompanionAtlas.FileName(config.Avatar, config.CompanionOutfit))).GetWrapOrDefault();
                if (texture == null) { hoveredLastFrame = false; return; }
                var frameIndex = PreviewPose == CompanionPose.Automatic && motion.Walking ? motion.Frame
                    : CompanionAtlas.SelectFrame(pose, animationTime, config.AnimationsEnabled);
                var frame = CompanionAtlas.GetFrame(config.Avatar, frameIndex, motion.FacingLeft, motion.Position, layout.SpriteSize);
                draw.AddImage(texture.Handle, frame.Position, frame.Position + frame.Size, frame.UvStart, frame.UvEnd);
                // The inspected frame bounds exclude large transparent gutters.
                // Clicking uses a rectangle, not per-pixel silhouette detection.
                hitStart = frame.Position;
                hitEnd = frame.Position + frame.Size;
            }
            ProceduralCompanion.DrawEffects(draw, motion.Position, layout.SpriteSize, config, pose, animationTime, hoveredLastFrame);
        }
        finally { draw.PopClipRect(); }
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
