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
    private readonly CompanionHomeRoutine homeRoutine = new();
    private readonly CompanionSurfaces surfaceReader = new();
    private readonly SeasonalCompanionService seasonal = new(config);
    private readonly CompanionRotation rotation = new();
    private SeasonalCompanionRenderer? seasonalArtwork;
    private string displayAvatar = "Moonblob";
    private bool seasonalFaulted;
    private bool seasonalVisible;
    private readonly string assetDirectory = Path.Combine(Services.PluginInterface.AssemblyLocation.DirectoryName!, "assets");
    private long lastTick;
    private float animationTime;
    private float poseTime;
    private CompanionPose lastPose;
    private CompanionPose lastPreviewPose;
    private CompanionLayout lastLayout;
    private CompanionAnimationLibrary? artwork;
    private CompanionNapArtwork? napArtwork;
    private bool hoveredLastFrame;
    private bool hoverPausedLastFrame;
    private string lastAvatar = string.Empty;
    private bool areaDragging;
    private Vector2 areaStart;
    private string areaHint = string.Empty;
    private bool pointerPressed;
    private Vector2 pointerPressPosition;
    private double nextCommentAt;
    private double commentUntil;
    private string comment = string.Empty;
    private int commentIndex;
    private bool housePressed;
    private bool houseDragging;
    private bool houseMirrored;
    private bool homeVisible;
    private Vector2 housePressPosition;
    private Vector2 houseOriginalAnchor;
    private bool handWasDragging;
    private float handTime = 1;
    private Vector2 handPoint;

    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus
        | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDocking;

    public bool IsChoosingArea { get; private set; }
    public CompanionPose PreviewPose { get; set; } = CompanionPose.Automatic;
    public string SeasonalStatus => seasonal?.Status ?? string.Empty;
    public void Update()
    {
        if (seasonalFaulted || seasonal == null) return;
        try { seasonal.Update(); }
        catch (Exception error)
        {
            seasonalFaulted = true; seasonal.Clear();
            Services.Log.Error("Seasonal appearance stopped ({ErrorType}).", error.GetType().Name);
        }
    }

    public void BeginChooseArea()
    {
        if (!config.AvatarEnabled) return;
        FinishHouseDrag(false);
        IsChoosingArea = true;
        physics.Reset();
        homeRoutine.Reset(config.CompanionNapInterval);
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
            FinishHouseDrag(false);
            CancelChooseArea();
            lastTick = 0;
            hoveredLastFrame = false;
            hoverPausedLastFrame = false;
            motion.Reset();
            gait.Reset();
            physics.Reset();
            homeRoutine.Reset(config.CompanionNapInterval);
            pointerPressed = false;
            nextCommentAt = commentUntil = 0;
            comment = string.Empty;
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
        displayAvatar = rotation.Update(config.Avatar, config.CompanionAutoRotateEnabled, config.CompanionRotationInterval,
            Environment.TickCount64, !physics.Active && !homeRoutine.Active && !pointerPressed && !houseDragging
                && !hoverPausedLastFrame && PreviewPose == CompanionPose.Automatic);
        if (lastAvatar != displayAvatar)
        {
            lastAvatar = displayAvatar;
            motion.Reset();
            gait.Reset();
            physics.Reset();
            homeRoutine.Reset(config.CompanionNapInterval);
            animationTime = 0;
        }

        var layout = CompanionLayout.Create(viewport.Pos, viewport.Size,
            new Vector4(config.CompanionBoxLeft, config.CompanionBoxTop, config.CompanionBoxRight, config.CompanionBoxBottom),
            config.CompanionSize, scale, aspect);
        seasonalArtwork ??= new SeasonalCompanionRenderer(assetDirectory);
        seasonalVisible = false;
        if (seasonal?.CurrentTheme != null && seasonalArtwork.IsAvailable)
        {
            var padding = layout.SpriteSize * SeasonalCompanionRenderer.RequiredVisualPaddingFraction;
            if (layout.Maximum.X - layout.Minimum.X >= layout.SpriteSize.X + padding.X * 2
                && layout.Maximum.Y - layout.Minimum.Y >= layout.SpriteSize.Y + padding.Y * 2)
            {
                seasonalVisible = true;
                layout = layout with { Minimum = layout.Minimum + padding, Maximum = layout.Maximum - padding };
            }
        }
        if (layout != lastLayout) gait.Rebase();
        lastLayout = layout;
        if (PreviewPose != lastPreviewPose) gait.Reset();
        lastPreviewPose = PreviewPose;
        var now = Stopwatch.GetTimestamp();
        var elapsed = lastTick == 0 ? 0 : (float)Stopwatch.GetElapsedTime(lastTick, now).TotalSeconds;
        lastTick = now;
        if (!config.CompanionHomeEnabled) FinishHouseDrag(false);
        var roamLayout = layout;
        var home = HomeBounds(layout);
        houseMirrored = CompanionPhysics.HomeFacesLeft((home.Minimum.X + home.Maximum.X) * .5f,
            viewport.Pos.X, viewport.Size.X, houseMirrored);
        CompanionSurface? homeObstacle = config.CompanionHomeEnabled
            ? new CompanionSurface(0, home.Minimum, home.Maximum, 0, 0) : null;
        // Initialize/clamp the dormant roaming position before physics uses it as
        // its return destination. Its approved movement and gait are unchanged.
        motion.Update(roamLayout, 0, false, homeObstacle);
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
        var wasAttached = physics.Active;
        physics.Update(surfaces, viewport.Pos, viewport.Pos + viewport.Size, layout.SpriteSize, elapsed,
            config.AnimationsEnabled && PreviewPose == CompanionPose.Automatic, hoverPausedLastFrame && !physics.IsCarried, options, motion.Position, homeObstacle);
        napArtwork ??= new CompanionNapArtwork(assetDirectory);
        var wasHomeActive = homeRoutine.Active;
        if (physics.Active) homeRoutine.Reset(config.CompanionNapInterval);
        homeRoutine.Update(elapsed, config.CompanionHomeEnabled && config.CompanionNapsEnabled && !physics.Active
            && napArtwork.TryGet(displayAvatar, config.CompanionOutfit, out _),
            !config.AnimationsEnabled || PreviewPose != CompanionPose.Automatic || hoverPausedLastFrame || pointerPressed || housePressed || houseDragging,
            motion.Position, motion.FacingLeft, layout, home, houseMirrored, config.CompanionNapInterval,
            config.CompanionNapDuration, viewport.Pos, viewport.Pos + viewport.Size, config.CompanionHomeEnabled);
        if (wasHomeActive && !homeRoutine.Active && !physics.Active) { motion.ResumeAt(homeRoutine.Position, roamLayout); gait.Rebase(); }
        motion.Update(roamLayout, elapsed, config.AnimationsEnabled && !hoverPausedLastFrame && !pointerPressed
            && !houseDragging && !wasAttached && !physics.Active && !homeRoutine.Active && PreviewPose == CompanionPose.Automatic, homeObstacle);
        if (!layout.IsVisible) return;
        var position = physics.Active ? physics.Position : homeRoutine.Active ? homeRoutine.Position : motion.Position;
        var facingLeft = physics.Active ? physics.FacingLeft : homeRoutine.Active ? homeRoutine.FacingLeft : motion.FacingLeft;
        var pose = !config.AnimationsEnabled ? CompanionPose.Idle : PreviewPose == CompanionPose.Automatic
            ? physics.Active ? physics.Pose : homeRoutine.Active ? homeRoutine.UsesNapArt ? CompanionPose.Idle : CompanionPose.Walk
                : motion.Walking ? CompanionPose.Walk : CompanionPose.Idle : PreviewPose;
        poseTime = pose != lastPose || !config.AnimationsEnabled ? 0 : (poseTime + Math.Clamp(elapsed, 0, .05f)) % 1000;
        lastPose = pose;
        artwork ??= new CompanionAnimationLibrary(assetDirectory);
        var hasAtlas = artwork.TryGet(displayAvatar, config.CompanionOutfit, out var atlas);
        var artTexture = hasAtlas ? Services.Textures.GetFromFile(Path.Combine(assetDirectory, atlas!.File)).GetWrapOrDefault() : null;
        var gaitPosition = position - (physics.Active || homeRoutine.Active ? viewport.Pos : layout.Minimum);
        var gaitSpace = physics.Active ? ((ulong)physics.Placement << 56) ^ physics.AttachedSurfaceId : homeRoutine.Active ? 0x7000000000000000ul : 0;
        if (physics.AttachedSurfaceId != 0)
            foreach (var surface in surfaces)
                if (surface.Id == physics.AttachedSurfaceId) { gaitPosition = position - surface.Minimum; break; }
        gait.Update(gaitPosition, layout.SpriteSize.X, elapsed, config.AnimationsEnabled && pose == CompanionPose.Walk,
            facingLeft, gaitSpace, atlas?.CycleDistance ?? CompanionGait.DefaultCycleDistance,
            PreviewPose == CompanionPose.Walk);
        var frameIndex = pose == CompanionPose.Walk ? gait.Frame : CompanionAtlas.SelectFrame(pose, poseTime, config.AnimationsEnabled);
        var fourierWink = displayAvatar == "Wink" && config.CompanionFourierBlinkEnabled;
        var blinkEnabled = config.AnimationsEnabled && config.CompanionBlinkEnabled;
        var blink = fourierWink ? FourierBlink.Amount(animationTime, config.CompanionBlinkInterval, blinkEnabled)
            : BlinkAmount(animationTime, config.CompanionBlinkInterval, blinkEnabled);
        if (displayAvatar == "Wink" && pose == CompanionPose.Idle)
            frameIndex = 8 + Math.Clamp((int)MathF.Round(blink * 3), 0, 3);
        var draw = ImGui.GetForegroundDrawList();
        DrawHome(draw, layout);
        var hitStart = position + layout.SpriteSize * .18f;
        var hitEnd = position + layout.SpriteSize * .82f;
        var visualPadding = !seasonalVisible ? Vector2.Zero
            : layout.SpriteSize * SeasonalCompanionRenderer.RequiredVisualPaddingFraction;
        var clipMinimum = physics.Active ? physics.ClipMinimum : homeRoutine.Active ? viewport.Pos : layout.Minimum - visualPadding;
        var clipMaximum = physics.Active ? physics.ClipMaximum : homeRoutine.Active ? viewport.Pos + viewport.Size : layout.Maximum + visualPadding;
        clipMinimum = Vector2.Max(clipMinimum, viewport.Pos); clipMaximum = Vector2.Min(clipMaximum, viewport.Pos + viewport.Size);
        draw.PushClipRect(clipMinimum, clipMaximum, true);
        try
        {
            if (homeRoutine.UsesNapArt && DrawNap(draw, layout, out var napBounds))
            {
                (hitStart, hitEnd) = napBounds;
            }
            else if (artTexture != null)
            {
                var tint = ProceduralCompanion.IsProcedural(displayAvatar)
                    ? ImGui.ColorConvertFloat4ToU32(Vector4.Lerp(Vector4.One, ProceduralCompanion.Palette(config.CompanionPalette), .36f))
                    : 0xFFFFFFFFu;
                SeasonalBodyTransform body;
                var seasonalFrame = frameIndex;
                if (displayAvatar == "Moonblob" && pose == CompanionPose.Walk)
                {
                    var roll = atlas!.GetRoll(gait.Phase, facingLeft, position, layout.SpriteSize);
                    draw.AddImageQuad(artTexture.Handle, roll.A, roll.B, roll.C, roll.D, roll.UvA, roll.UvB, roll.UvC, roll.UvD, tint);
                    (hitStart, hitEnd) = roll.HitBounds;
                    seasonalFrame = 0;
                    body = new(atlas.Frames[0].Rect, atlas.SourceScale, roll.A, roll.B, roll.D);
                }
                else if (pose == CompanionPose.Walk || fourierWink && pose == CompanionPose.Idle)
                {
                    // Optional idle blinking keeps the same frame-0 body and
                    // frozen float phase as the last step; only the lid moves.
                    var floating = atlas!.GetFloat(gait.Phase, facingLeft, position, layout.SpriteSize);
                    draw.AddImageQuad(artTexture.Handle, floating.A, floating.B, floating.C, floating.D,
                        floating.UvA, floating.UvB, floating.UvC, floating.UvD, tint);
                    if (displayAvatar == "Wink" && blink > 0)
                        DrawWinkBlink(draw, artTexture.Handle, floating, blink, tint);
                    (hitStart, hitEnd) = floating.HitBounds;
                    seasonalFrame = 0;
                    body = new(atlas.Frames[0].Rect, atlas.SourceScale, floating.A, floating.B, floating.D);
                }
                else
                {
                    var frame = atlas!.GetFrame(frameIndex, facingLeft, position, layout.SpriteSize);
                    draw.AddImage(artTexture.Handle, frame.Position, frame.Position + frame.Size, frame.UvStart, frame.UvEnd, tint);
                    (hitStart, hitEnd) = atlas.HitBounds(frameIndex, facingLeft, position, layout.SpriteSize);
                    body = SeasonalBodyTransform.FromFrame(atlas.Frames[frameIndex].Rect, atlas.SourceScale, frame, facingLeft);
                }
                var extra = DrawSeasonal(draw, seasonalFrame, false, body);
                if (extra is { } bounds) { hitStart = Vector2.Min(hitStart, bounds.Minimum); hitEnd = Vector2.Max(hitEnd, bounds.Maximum); }
            }
            else
                ProceduralCompanion.Draw(draw, position, layout.SpriteSize, config, pose,
                    displayAvatar == "Wink" ? .17f + .13f * blink : animationTime, hoveredLastFrame, displayAvatar);
            if (!homeRoutine.UsesNapArt)
                ProceduralCompanion.DrawEffects(draw, position, layout.SpriteSize, config, pose, animationTime, hoveredLastFrame);
        }
        finally { draw.PopClipRect(); }
        if (homeRoutine.Sleeping && config.CompanionNapZsEnabled) DrawSleepZs(draw, home, viewport.Pos, viewport.Size);
        // A stable pause region avoids rapid walk/idle changes as animated ear
        // and foot silhouettes move past a stationary pointer. Only the visible
        // body hit rectangle below creates an input window.
        hitStart = Vector2.Max(hitStart, clipMinimum);
        hitEnd = Vector2.Min(hitEnd, clipMaximum);
        hoverPausedLastFrame = ImGui.IsMouseHoveringRect(position, position + layout.SpriteSize, false);
        hoveredLastFrame = ImGui.IsMouseHoveringRect(hitStart, hitEnd, false);
        if (ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel))
        {
            pointerPressed = false;
            if (physics.IsCarried)
                physics.Drop(pointer, [], viewport.Pos, viewport.Pos + viewport.Size, layout.SpriteSize, options);
            FinishHouseDrag(false);
            return;
        }
        var commentHasInput = DrawComment(draw, position, layout.SpriteSize, viewport.Pos, viewport.Size, scale);
        Vector2? handReady = hoveredLastFrame ? CompanionGrabAnchor.Center(position, layout.SpriteSize) : null;
        if (handReady == null && homeVisible)
        {
            var (houseMin, houseMax) = HomeBounds(layout);
            var houseSize = houseMax - houseMin;
            if (ImGui.IsMouseHoveringRect(houseMin + houseSize * new Vector2(.08f, .035f), houseMax - houseSize * new Vector2(.08f, .08f), false))
                handReady = CompanionGrabAnchor.Center(houseMin, houseSize);
        }
        Vector2? heldGrip = physics.IsCarried ? CompanionGrabAnchor.Center(position, layout.SpriteSize)
            : houseDragging ? CompanionGrabAnchor.Center(home.Minimum, home.Size) : null;
        DrawGrabHand(draw, viewport.Pos, viewport.Size, layout.SpriteSize, elapsed, physics.IsCarried || houseDragging, handReady, heldGrip);
        if (commentHasInput && !pointerPressed && !physics.IsCarried && !housePressed) return;
        if (HandleHouse(layout, viewport.Pos, viewport.Size, scale, hoveredLastFrame || pointerPressed || physics.IsCarried)) return;
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

    private CompanionHomeRect HomeBounds(CompanionLayout layout)
    {
        var viewport = ImGui.GetMainViewport();
        return CompanionHomeGeometry.Bounds(layout, viewport.Pos, viewport.Size,
            new Vector2(config.CompanionHomeX, config.CompanionHomeY));
    }

    private bool DrawNap(ImDrawListPtr draw, CompanionLayout layout,
        out (Vector2 Minimum, Vector2 Maximum) bounds)
    {
        bounds = default;
        if (napArtwork?.TryGet(displayAvatar, config.CompanionOutfit, out var sheet) != true) return false;
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, sheet!.File)).GetWrapOrDefault();
        var houseTexture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, "companion-house.png")).GetWrapOrDefault();
        if (texture == null || houseTexture == null) return false;
        var tint = ProceduralCompanion.IsProcedural(displayAvatar)
            ? ImGui.ColorConvertFloat4ToU32(Vector4.Lerp(Vector4.One, ProceduralCompanion.Palette(config.CompanionPalette), .36f))
            : 0xFFFFFFFFu;
        bounds = CompanionHomeRenderer.DrawNap(draw, texture.Handle, houseTexture.Handle, sheet, homeRoutine,
            layout.SpriteSize, HomeBounds(layout), houseMirrored, false, tint);
        var blend = homeRoutine.Sleeping ? (1 - MathF.Cos(homeRoutine.SleepPhase * MathF.Tau / 3.2f)) * .5f : 0;
        var napBounds = bounds;
        NapAccessory(homeRoutine.ArtFrame, 1 - blend);
        if (homeRoutine.Sleeping) NapAccessory(3, blend);
        if (homeVisible) CompanionHomeRenderer.DrawHouseWalls(draw, houseTexture.Handle, HomeBounds(layout), houseMirrored);
        bounds = napBounds;
        return true;

        void NapAccessory(int index, float opacity)
        {
            var frame = sheet.Frame(index, homeRoutine.Position, layout.SpriteSize, homeRoutine.ArtScale, homeRoutine.FacingLeft);
            var b = sheet.Frames[index].Bounds;
            var transform = SeasonalBodyTransform.FromFrame(new(b[0], b[1], b[2], b[3]), sheet.ScaleReference, frame, homeRoutine.FacingLeft);
            var extra = DrawSeasonal(draw, index, true, transform, (uint)(opacity * 255) << 24 | 0xFFFFFFu);
            // The hit rectangle remains the visible visitor; the wall mask still owns the roof.
            if (extra is { } hat) napBounds = (Vector2.Min(napBounds.Minimum, hat.Minimum), Vector2.Max(napBounds.Maximum, hat.Maximum));
        }
    }

    private (Vector2 Minimum, Vector2 Maximum)? DrawSeasonal(ImDrawListPtr draw, int index, bool nap,
        SeasonalBodyTransform body, uint tint = 0xFFFFFFFFu)
    {
        if (!seasonalVisible || seasonal?.CurrentTheme is not { } theme || seasonalArtwork == null
            || !seasonalArtwork.TryGetQuad(theme, displayAvatar, index, nap, body, out var quad)) return null;
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, SeasonalCompanionRenderer.File)).GetWrapOrDefault();
        return texture == null ? null : SeasonalCompanionRenderer.Draw(draw, texture.Handle, quad, tint);
    }

    private void DrawSleepZs(ImDrawListPtr draw, CompanionHomeRect house, Vector2 viewportPosition, Vector2 viewportSize)
        => CompanionHomeRenderer.DrawZs(draw, house, houseMirrored, homeRoutine.SleepPhase, viewportPosition, viewportSize);
    private void DrawHome(ImDrawListPtr draw, CompanionLayout layout)
    {
        homeVisible = false;
        if (!config.CompanionHomeEnabled || !layout.IsVisible) return;
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, "companion-house.png")).GetWrapOrDefault();
        if (texture == null) return;
        homeVisible = true;
        var (top, bottom) = HomeBounds(layout);
        var viewport = ImGui.GetMainViewport();
        var middle = (top.X + bottom.X) * .5f;
        houseMirrored = CompanionPhysics.HomeFacesLeft(middle, viewport.Pos.X, viewport.Size.X, houseMirrored);
        draw.PushClipRect(viewport.Pos, viewport.Pos + viewport.Size, true);
        CompanionHomeRenderer.DrawHouse(draw, texture.Handle, new CompanionHomeRect(top, bottom), houseMirrored);
        draw.PopClipRect();
    }

    private bool HandleHouse(CompanionLayout layout, Vector2 viewportPosition, Vector2 viewportSize, float scale, bool petHasPriority)
    {
        if (!config.CompanionHomeEnabled || !homeVisible || !layout.IsVisible || viewportSize.X <= 0 || viewportSize.Y <= 0) return false;
        var (minimum, maximum) = HomeBounds(layout);
        var edge = maximum - minimum;
        // The source artwork has generous transparent gutters; those do not
        // capture clicks. The pet's visible hitbox always wins an overlap.
        minimum += edge * new Vector2(.015f, .015f);
        maximum -= edge * new Vector2(.015f, .015f);
        var hovered = ImGui.IsMouseHoveringRect(minimum, maximum, false);
        if (!housePressed && (!hovered || petHasPriority)) return false;
        if (BeginOverlay("##OmniKitCompanionHouse", minimum, maximum - minimum))
        {
            ImGui.InvisibleButton("##house", maximum - minimum, ImGuiButtonFlags.MouseButtonLeft);
            ImGui.SetMouseCursor(houseDragging ? ImGuiMouseCursor.ResizeAll : ImGuiMouseCursor.Hand);
            if (!housePressed && hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                housePressed = true;
                housePressPosition = ImGui.GetMousePos();
                houseOriginalAnchor = new Vector2(config.CompanionHomeX, config.CompanionHomeY);
            }
            if (housePressed && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var delta = ImGui.GetMousePos() - housePressPosition;
                if (delta.LengthSquared() >= 36 * scale * scale) houseDragging = true;
                if (houseDragging)
                {
                    SetHomeAnchor(Vector2.Clamp(houseOriginalAnchor + delta / viewportSize, Vector2.Zero, Vector2.One));
                }
            }
            if (housePressed && ImGui.IsMouseReleased(ImGuiMouseButton.Left)) FinishHouseDrag(true);
        }
        ImGui.End();
        return housePressed || hovered;
    }

    private void FinishHouseDrag(bool save)
    {
        if (houseDragging)
        {
            if (save)
            {
                try { config.Save(); }
                catch (Exception exception)
                {
                    SetHomeAnchor(houseOriginalAnchor);
                    Services.Log.Error("Companion home could not be saved ({ErrorType}).", exception.GetType().Name);
                }
            }
            else SetHomeAnchor(houseOriginalAnchor);
        }
        housePressed = houseDragging = false;
    }

    private void SetHomeAnchor(Vector2 anchor)
    {
        config.CompanionHomeX = anchor.X; config.CompanionHomeY = anchor.Y;
    }

    private void DrawGrabHand(ImDrawListPtr draw, Vector2 viewportPosition, Vector2 viewportSize, Vector2 spriteSize,
        float elapsed, bool dragging, Vector2? readyPoint, Vector2? gripPoint = null)
    {
        if (!config.CompanionGrabHandEnabled || !config.AnimationsEnabled)
        { handWasDragging = false; handTime = 1; return; }
        if (dragging != handWasDragging) handTime = 0;
        handWasDragging = dragging;
        handTime += float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0;
        if (dragging && gripPoint.HasValue) handPoint = gripPoint.Value;
        var ready = !dragging && handTime >= .25f && readyPoint.HasValue;
        if (!dragging && handTime >= .25f && !ready) return;
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, "companion-grab-hand.png")).GetWrapOrDefault();
        if (texture == null) return;
        var incoming = Math.Clamp(handTime / .2f, 0, 1);
        var ease = 1 - MathF.Pow(1 - incoming, 3);
        var frame = ready ? 0 : !dragging ? 3 : incoming < .45f ? 0 : incoming < 1 ? 1 : 2;
        var handScale = float.IsFinite(config.CompanionGrabHandScale) ? Math.Clamp(config.CompanionGrabHandScale, .2f, 1) : .4f;
        var edge = Math.Min(Math.Min(viewportSize.X, viewportSize.Y), spriteSize.Y * handScale);
        var size = new Vector2(edge);
        // Position by the finger contact, independently of hand texture size.
        // The actual clamped body centre remains the shared grip while carried.
        var top = CompanionGrabAnchor.GripTopAt(ready ? readyPoint!.Value : handPoint, size, frame)
            - new Vector2(0, edge * (ready ? 0 : dragging ? (1 - ease) * .8f : handTime * 1.4f));
        var start = new Vector2(frame % 2, frame / 2) * .5f;
        var opacity = ready || dragging ? 1 : Math.Clamp(1 - handTime / .25f, 0, 1);
        draw.PushClipRect(viewportPosition, viewportPosition + viewportSize, true);
        draw.AddImage(texture.Handle, top, top + size, start, start + new Vector2(.5f), ((uint)(opacity * 255) << 24) | 0xFFFFFF);
        draw.PopClipRect();
    }

    private bool DrawComment(ImDrawListPtr draw, Vector2 position, Vector2 size, Vector2 viewportPosition, Vector2 viewportSize, float scale)
    {
        if (homeRoutine.UsesNapArt) return false;
        var now = ImGui.GetTime();
        if (!config.CompanionCommentsEnabled)
        { nextCommentAt = commentUntil = 0; comment = string.Empty; return false; }
        var interval = Math.Clamp(float.IsFinite(config.CompanionCommentInterval) ? config.CompanionCommentInterval : 120, 30, 600);
        if (nextCommentAt == 0) nextCommentAt = now + interval;
        if (now >= nextCommentAt && !physics.IsCarried && PreviewPose == CompanionPose.Automatic)
        {
            comment = physics.Placement switch
            {
                CompanionPlacement.Falling => "Tiny moon landing incoming!",
                CompanionPlacement.Returning => "My cushion is calling.",
                CompanionPlacement.Perched => "Excellent view from up here.",
                CompanionPlacement.Contained => "Who put this wall here?",
                _ => (++commentIndex % 4) switch
                {
                    0 => "One small hop for a moon friend.",
                    1 => "You quest. I'll supervise the stars.",
                    2 => "A little adventure, then a little nap.",
                    _ => "All systems cozy.",
                },
            };
            commentUntil = now + 7;
            nextCommentAt = now + interval;
        }
        if (now >= commentUntil || comment.Length == 0 || physics.IsCarried || housePressed) return false;
        scale = Math.Clamp(float.IsFinite(scale) ? scale : 1, .5f, 2);
        var padding = new Vector2(10, 7) * scale;
        var wrap = Math.Max(1, Math.Min(220 * scale, viewportSize.X - padding.X * 2 - 16));
        var textSize = ImGui.CalcTextSize(comment, false, wrap);
        var bubbleSize = textSize + padding * 2;
        var wanted = position + new Vector2((size.X - bubbleSize.X) * .5f, -bubbleSize.Y - 6 * scale);
        if (wanted.Y < viewportPosition.Y + 8) wanted.Y = position.Y + size.Y + 6 * scale;
        var minimum = viewportPosition + new Vector2(8);
        var bubblePosition = Vector2.Clamp(wanted, minimum, Vector2.Max(minimum, viewportPosition + viewportSize - bubbleSize - new Vector2(8)));
        draw.PushClipRect(viewportPosition, viewportPosition + viewportSize, true);
        draw.AddRectFilled(bubblePosition, bubblePosition + bubbleSize, 0xF21E1923, 9 * scale);
        draw.AddRect(bubblePosition, bubblePosition + bubbleSize, 0xBE9BC9DD, 9 * scale);
        draw.AddText(ImGui.GetFont(), ImGui.GetFontSize(), bubblePosition + padding, 0xFFF0F3F6, comment, wrap);
        draw.PopClipRect();
        if (!ImGui.IsMouseHoveringRect(bubblePosition, bubblePosition + bubbleSize, false)) return false;
        if (BeginOverlay("##OmniKitCompanionComment", bubblePosition, bubbleSize))
        {
            if (ImGui.InvisibleButton("##dismiss", bubbleSize)) commentUntil = 0;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        ImGui.End();
        return true;
    }

    internal static float BlinkAmount(float seconds, float interval, bool enabled)
    {
        if (!enabled || !float.IsFinite(seconds)) return 0;
        interval = Math.Clamp(float.IsFinite(interval) ? interval : 3, 1, 10);
        var time = (Math.Max(0, seconds) + interval - .3f) % interval;
        if (time >= .28f) return 0;
        return time < .08f ? time / .08f : time < .16f ? 1 : 1 - (time - .16f) / .12f;
    }

    internal static void DrawWinkBlink(ImDrawListPtr draw, ImTextureID texture, CompanionFloatQuad quad, float closure, uint tint)
    {
        // Eyelid follows the unchanged floating image's affine transform. These
        // inspected frame-0 eye coordinates never transform the body or stride.
        closure = Math.Clamp(closure, 0, 1);
        if (closure <= .001f) return;
        Vector2 Point(Vector2 source) => quad.A + (quad.B - quad.A) * ((source.X - 9) / 241)
            + (quad.D - quad.A) * ((source.Y - 35) / 204);
        var angle = MathF.Asin(closure * 2 - 1);
        Span<Vector2> points = stackalloc Vector2[33];
        var center = Vector2.Zero;
        for (var i = 0; i < points.Length; i++)
        {
            var t = MathF.PI - angle + (MathF.PI + 2 * angle) * i / 32;
            points[i] = new Vector2(162 + MathF.Cos(t) * 45, 125 + MathF.Sin(t) * 42);
            center += points[i];
        }
        center /= points.Length;
        // Sample the existing closed-pose forehead's painted cream shading,
        // avoiding its eye and mouth. Explicit image triangles retain the atlas
        // texture even with a host that restores its font texture for paths.
        Vector2 Uv(Vector2 p) => new Vector2(854 + Math.Clamp((p.X - 117) / 90, 0, 1) * 90,
            592 + Math.Clamp((p.Y - 83) / 84, 0, 1) * 32) / new Vector2(1024, 1536);
        for (var i = 0; i < points.Length; i++)
        {
            var a = points[i]; var b = points[(i + 1) % points.Length];
            draw.AddImageQuad(texture, Point(center), Point(a), Point(b), Point(b), Uv(center), Uv(a), Uv(b), Uv(b), tint);
            if (Vector2.DistanceSquared(a, b) < .00001f) continue;
            var deltaA = a - center; var deltaB = b - center;
            var outerA = a + (deltaA.LengthSquared() > .000001f ? Vector2.Normalize(deltaA) : -Vector2.UnitY) * 2;
            var outerB = b + (deltaB.LengthSquared() > .000001f ? Vector2.Normalize(deltaB) : -Vector2.UnitY) * 2;
            var first = draw.VtxBuffer.Size;
            draw.AddImageQuad(texture, Point(a), Point(b), Point(outerB), Point(outerA), Uv(a), Uv(b), Uv(outerB), Uv(outerA), tint);
            unsafe
            {
                draw.VtxBuffer.Data[first + 2].Col &= 0xFFFFFF;
                draw.VtxBuffer.Data[first + 3].Col &= 0xFFFFFF;
            }
        }
        if (closure < .7f) return;
        var opacity = (uint)(Math.Clamp((closure - .7f) / .3f, 0, 1) * 255);
        for (var i = 0; i <= 24; i++)
        {
            var x = -1f + i / 12f;
            draw.PathLineTo(Point(new Vector2(162 + x * 33, 136 - 9 * (1 - x * x))));
        }
        draw.PathStroke(opacity << 24 | 0x00291B27, ImDrawFlags.None, Math.Max(.8f, Vector2.Distance(quad.A, quad.B) / 241 * 4));
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
