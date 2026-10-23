using System.Numerics;
using System.Diagnostics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using OmniKit.Input;

namespace OmniKit.Ui;

/// <summary>A purely visual companion with a bounded, opt-in area picker.</summary>
internal sealed class RoamingCompanion(Configuration config, Action openCompanion)
{
    private readonly CompanionMotion motion = new();
    private readonly CompanionGait gait = new();
    private readonly CompanionPhysics physics = new();
    private readonly CompanionSurfaces surfaceReader = new();
    private readonly string assetDirectory = Path.Combine(Services.PluginInterface.AssemblyLocation.DirectoryName!, "assets");
    private long lastTick;
    private float animationTime;
    private float poseTime;
    private CompanionPose lastPose;
    private CompanionPose lastPreviewPose;
    private CompanionLayout lastLayout;
    private CompanionAnimationLibrary? artwork;
    private LunarWalkRigLibrary? walkRigs;
    private bool hoveredLastFrame;
    private bool hoverPausedLastFrame;
    private string lastAvatar = string.Empty;
    private bool areaDragging;
    private Vector2 areaStart;
    private string areaHint = string.Empty;
    private bool pointerPressed;
    private Vector2 pointerPressPosition;

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
        physics.Reset();
        gait.Rebase();
        pointerPressed = false;
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
            hoverPausedLastFrame = false;
            motion.Reset();
            gait.Reset();
            physics.Reset();
            pointerPressed = false;
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
            gait.Reset();
            physics.Reset();
            animationTime = 0;
        }

        var layout = CompanionLayout.Create(viewport.Pos, viewport.Size,
            new Vector4(config.CompanionBoxLeft, config.CompanionBoxTop, config.CompanionBoxRight, config.CompanionBoxBottom),
            config.CompanionSize, scale, aspect);
        if (layout != lastLayout) gait.Rebase();
        lastLayout = layout;
        if (PreviewPose != lastPreviewPose) gait.Reset();
        lastPreviewPose = PreviewPose;
        var now = Stopwatch.GetTimestamp();
        var elapsed = lastTick == 0 ? 0 : (float)Stopwatch.GetElapsedTime(lastTick, now).TotalSeconds;
        lastTick = now;
        var frameFeature = config.CompanionFrameInteractionEnabled;
        if (!frameFeature || !config.CompanionPickupEnabled && physics.IsCarried)
        {
            if (physics.IsCarried) pointerPressed = false;
            physics.Reset();
            if (frameFeature) pointerPressed = false;
        }
        IReadOnlyList<CompanionSurface> surfaces = frameFeature ? surfaceReader.Read(viewport.Pos, viewport.Size) : [];
        var options = new CompanionPhysicsOptions(config.CompanionPerchEnabled, config.CompanionContainmentEnabled, config.CompanionFallingEnabled);
        var pointer = ImGui.GetMousePos();
        if (physics.IsCarried)
        {
            physics.Carry(pointer, viewport.Pos, viewport.Pos + viewport.Size, layout.SpriteSize, elapsed);
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                physics.Drop(pointer, surfaces, viewport.Pos, viewport.Pos + viewport.Size, layout.SpriteSize, options);
                pointerPressed = false;
            }
        }
        if (config.AnimationsEnabled)
            animationTime = (animationTime + Math.Clamp(elapsed, 0, .05f)) % 1000;
        physics.Update(surfaces, viewport.Pos, viewport.Pos + viewport.Size, layout.SpriteSize, elapsed,
            config.AnimationsEnabled && PreviewPose == CompanionPose.Automatic, hoverPausedLastFrame && !physics.IsCarried, options);
        motion.Update(layout, elapsed, config.AnimationsEnabled && !hoverPausedLastFrame && !pointerPressed
            && !physics.Active && PreviewPose == CompanionPose.Automatic);
        if (!layout.IsVisible) return;
        var position = physics.Active ? physics.Position : motion.Position;
        var facingLeft = physics.Active ? physics.FacingLeft : motion.FacingLeft;
        var pose = !config.AnimationsEnabled ? CompanionPose.Idle : PreviewPose == CompanionPose.Automatic
            ? physics.Active ? physics.Pose : motion.Walking ? CompanionPose.Walk : CompanionPose.Idle : PreviewPose;
        poseTime = pose != lastPose || !config.AnimationsEnabled ? 0 : (poseTime + Math.Clamp(elapsed, 0, .05f)) % 1000;
        lastPose = pose;
        artwork ??= new CompanionAnimationLibrary(assetDirectory);
        walkRigs ??= new LunarWalkRigLibrary(assetDirectory);
        var hasAtlas = artwork.TryGet(config.Avatar, config.CompanionOutfit, out var atlas);
        var artTexture = hasAtlas ? Services.Textures.GetFromFile(Path.Combine(assetDirectory, atlas!.File)).GetWrapOrDefault() : null;
        LunarWalkRig? rig = null;
        if (pose == CompanionPose.Walk) walkRigs.TryGet(config.Avatar, config.CompanionOutfit, out rig);
        var rigTexture = rig == null ? null : Services.Textures.GetFromFile(Path.Combine(assetDirectory, rig.File)).GetWrapOrDefault();
        var gaitPosition = position - (physics.Active ? viewport.Pos : layout.Minimum);
        var gaitSpace = physics.Active ? ((ulong)physics.Placement << 56) ^ physics.AttachedSurfaceId : 0;
        if (physics.AttachedSurfaceId != 0)
            foreach (var surface in surfaces)
                if (surface.Id == physics.AttachedSurfaceId) { gaitPosition = position - surface.Minimum; break; }
        gait.Update(gaitPosition, layout.SpriteSize.X, elapsed, config.AnimationsEnabled && pose == CompanionPose.Walk,
            facingLeft, gaitSpace, rigTexture != null ? rig!.CycleDistance : atlas?.CycleDistance ?? CompanionGait.DefaultCycleDistance,
            PreviewPose == CompanionPose.Walk, directionalStride: rigTexture != null);
        LunarWalkPose? rigPose = null;
        if (rigTexture != null)
        {
            try { rigPose = rig!.Evaluate(gait.Phase, facingLeft, position, layout.SpriteSize, gait.Heading); }
            catch (InvalidOperationException) { /* Optional art geometry retains the existing atlas fallback. */ }
        }
        var frameIndex = pose == CompanionPose.Walk ? gait.Frame : CompanionAtlas.SelectFrame(pose, poseTime, config.AnimationsEnabled);
        var draw = ImGui.GetForegroundDrawList();
        var hitStart = position + layout.SpriteSize * .18f;
        var hitEnd = position + layout.SpriteSize * .82f;
        var clipMinimum = physics.Active ? physics.ClipMinimum : layout.Minimum;
        var clipMaximum = physics.Active ? physics.ClipMaximum : layout.Maximum;
        draw.PushClipRect(clipMinimum, clipMaximum, true);
        try
        {
            if (rigPose != null && rigTexture != null)
            {
                foreach (var q in rigPose.Quads)
                    draw.AddImageQuad(rigTexture.Handle, q.A, q.B, q.C, q.D, q.UvA, q.UvB, q.UvC, q.UvD, 0xFFFFFFFFu);
                (hitStart, hitEnd) = rigPose.Bounds;
            }
            else if (artTexture != null)
            {
                var tint = ProceduralCompanion.IsProcedural(config.Avatar)
                    ? ImGui.ColorConvertFloat4ToU32(Vector4.Lerp(Vector4.One, ProceduralCompanion.Palette(config.CompanionPalette), .36f))
                    : 0xFFFFFFFFu;
                if (config.Avatar == "Moonblob" && pose == CompanionPose.Walk)
                {
                    var roll = atlas!.GetRoll(gait.Phase, facingLeft, position, layout.SpriteSize);
                    draw.AddImageQuad(artTexture.Handle, roll.A, roll.B, roll.C, roll.D, roll.UvA, roll.UvB, roll.UvC, roll.UvD, tint);
                    (hitStart, hitEnd) = roll.HitBounds;
                }
                else if (config.Avatar is "Wink" or "Nova" && pose == CompanionPose.Walk)
                {
                    var floating = atlas!.GetFloat(gait.Phase, facingLeft, position, layout.SpriteSize);
                    draw.AddImageQuad(artTexture.Handle, floating.A, floating.B, floating.C, floating.D,
                        floating.UvA, floating.UvB, floating.UvC, floating.UvD, tint);
                    (hitStart, hitEnd) = floating.HitBounds;
                }
                else
                {
                    var frame = atlas!.GetFrame(frameIndex, facingLeft, position, layout.SpriteSize);
                    draw.AddImage(artTexture.Handle, frame.Position, frame.Position + frame.Size, frame.UvStart, frame.UvEnd, tint);
                    (hitStart, hitEnd) = atlas.HitBounds(frameIndex, facingLeft, position, layout.SpriteSize);
                }
            }
            else if (ProceduralCompanion.IsProcedural(config.Avatar))
                ProceduralCompanion.Draw(draw, position, layout.SpriteSize, config, pose, animationTime, hoveredLastFrame);
            else
            {
                var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory,
                    CompanionAtlas.FileName(config.Avatar, config.CompanionOutfit))).GetWrapOrDefault();
                if (texture == null) { hoveredLastFrame = false; return; }
                var frame = CompanionAtlas.GetFrame(config.Avatar, frameIndex, facingLeft, position, layout.SpriteSize);
                draw.AddImage(texture.Handle, frame.Position, frame.Position + frame.Size, frame.UvStart, frame.UvEnd);
                // The inspected frame bounds exclude large transparent gutters.
                // Clicking uses a rectangle, not per-pixel silhouette detection.
                hitStart = frame.Position;
                hitEnd = frame.Position + frame.Size;
            }
            ProceduralCompanion.DrawEffects(draw, position, layout.SpriteSize, config, pose, animationTime, hoveredLastFrame);
        }
        finally { draw.PopClipRect(); }
        // A stable pause region avoids rapid walk/idle changes as animated ear
        // and foot silhouettes move past a stationary pointer. Only the visible
        // body hit rectangle below creates an input window.
        hoverPausedLastFrame = ImGui.IsMouseHoveringRect(position, position + layout.SpriteSize, false);
        hoveredLastFrame = ImGui.IsMouseHoveringRect(hitStart, hitEnd, false);
        if (ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel))
        {
            pointerPressed = false;
            if (physics.IsCarried) physics.Reset();
            return;
        }
        if (!hoveredLastFrame && !pointerPressed && !physics.IsCarried) return;

        var visible = BeginOverlay("##OmniKitRoamingCompanion", hitStart, hitEnd - hitStart);
        var clicked = false;
        if (visible)
        {
            ImGui.InvisibleButton("##companion", hitEnd - hitStart, ImGuiButtonFlags.MouseButtonLeft);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (hoveredLastFrame && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                pointerPressed = true;
                pointerPressPosition = pointer;
            }
            if (pointerPressed && frameFeature && config.CompanionPickupEnabled && !physics.IsCarried
                && ImGui.IsMouseDown(ImGuiMouseButton.Left) && Vector2.DistanceSquared(pointer, pointerPressPosition) >= 36 * scale * scale)
            {
                physics.PickUp(position, pointerPressPosition, facingLeft);
                physics.Carry(pointer, viewport.Pos, viewport.Pos + viewport.Size, layout.SpriteSize, elapsed);
            }
            if (pointerPressed && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                clicked = !physics.IsCarried && hoveredLastFrame;
                pointerPressed = false;
            }
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
