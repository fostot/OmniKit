using System.Numerics;
using System.Diagnostics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using OmniKit.Input;
using OmniKit.Companions;

namespace OmniKit.Ui;

/// <summary>A purely visual companion with a bounded, opt-in area picker.</summary>
internal sealed class RoamingCompanion(Configuration config, Action openCompanion)
{
    private readonly CompanionMotion motion = new();
    private readonly CompanionGait gait = new();
    private readonly CompanionPhysics physics = new();
    private readonly CompanionPhysics napPhysics = new();
    private readonly CompanionBodyMotion bodyMotion = new();
    private readonly CompanionMotionDynamics motionDynamics = new();
    private readonly OrbitRingRenderer orbitRings = new();
    private CompanionHomeStyleLibrary? styles;
    private CompanionStyleSelection? styleSelection;
    private CompanionHomeStyleFrame? homeStyleFrame;
    private bool stylesFaulted;
    private readonly List<CompanionSurface> napSurfaces = [];
    private CompanionLayout navigationLayout;
    private Vector2 navigationMinimum;
    private Vector2 navigationMaximum;
    private CompanionPhysicsOptions navigationOptions;
    private CompanionSurface? navigationObstacle;
    private readonly CompanionHomeRoutine homeRoutine = new();
    private readonly CompanionSurfaces surfaceReader = new();
    private readonly SeasonalCompanionService seasonal = new(config);
    private readonly CompanionRotation rotation = new();
    private readonly CompanionAppearanceSelection appearanceSelection = new();
    private readonly CompanionPoofTransition poof = new();
    private SeasonalCompanionRenderer? seasonalArtwork;
    private string displayAvatar = "Moonblob";
    private bool seasonalFaulted;
    private bool seasonalVisible;
    private float accessoryPadding = SeasonalCompanionRenderer.RequiredVisualPaddingFraction;
    private CompanionVariantLibrary? variants;
    private CompanionBlinkLibrary? blinks;
    private CompanionVariantSelection? variantSelection;
    private CompanionHolidayLibrary? holidayBodies;
    private CompanionWardrobeLibrary? wardrobe;
    private bool lookSelectionInitialized;
    private CompanionVariantLook? currentLook;
    private CompanionVariantLook? cachedLook;
    private CompanionSpriteAtlas? lookAtlas;
    private Vector2? visualGrip;
    private readonly string assetDirectory = Path.Combine(Services.PluginInterface.AssemblyLocation.DirectoryName!, "assets");
    private long lastTick;
    private float animationTime;
    private float poseTime;
    private CompanionPose lastPose;
    private CompanionPose lastPreviewPose;
    private CompanionLayout lastLayout;
    private CompanionAnimationLibrary? artwork;
    private CompanionNapArtwork? napArtwork;
    private CompanionHomePoseLibrary? homePoses;
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
    private SayingEntry? commentEntry;
    private string commentAvatar = string.Empty;
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
    public bool IsEditingAccessories { get; set; }
    public SayingController? Sayings { get; set; }
    public string DisplayAvatar => displayAvatar;
    public string CurrentLookId => currentLook?.Choice ?? "Default";
    private float BodyVisualPadding => CurrentLookId.StartsWith("Costume", StringComparison.Ordinal)
        ? CompanionWardrobeLibrary.VisualPaddingFraction + (displayAvatar == "Orbit" ? .08f : 0)
        : bodyMotion.AppliesTo(displayAvatar) ? displayAvatar == "Moonblob" && bodyMotion.UsesRoll(displayAvatar) ? .25f : .10f
            : displayAvatar == "Orbit" ? .08f : 0;
    public string AssetDirectory => assetDirectory;
    public SeasonalCompanionTheme? AccessoryTheme => seasonal?.CurrentTheme;
    public CompanionPose PreviewPose { get; set; } = CompanionPose.Automatic;
    public string SeasonalStatus => seasonal?.Status ?? string.Empty;
    public bool HasEverydayLooks(string avatar) { EnsureVariants(); return variants?.Looks.Any(l => l.Avatar == avatar) == true; }
    public bool HasHolidayBodies(string avatar)
    { holidayBodies ??= new CompanionHolidayLibrary(assetDirectory); return holidayBodies.Looks.Any(l => l.Avatar == avatar); }
    public bool DrawPortrait(ImDrawListPtr draw, Vector2 position, Vector2 size, string avatar)
    {
        wardrobe ??= new CompanionWardrobeLibrary(assetDirectory);
        var costume = ResolveCostume(config.CompanionCostume, seasonal?.CurrentTheme);
        var look = costume is { } theme ? wardrobe.FindCostume(avatar, theme) : null;
        look ??= wardrobe.FindDefault(avatar);
        if (look == null) return false;
        var source = look.Normal;
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, source.File)).GetWrapOrDefault();
        if (texture == null) return false;
        var shape = new Vector2(source.Bounds.Z - source.Bounds.X, source.Bounds.W - source.Bounds.Y);
        var painted = shape * Math.Min(size.X / shape.X, size.Y / shape.Y);
        var at = position + (size - painted) * .5f;
        draw.AddImage(texture.Handle, at, at + painted, new Vector2(source.Bounds.X, source.Bounds.Y) / source.SheetSize,
            new Vector2(source.Bounds.Z, source.Bounds.W) / source.SheetSize);
        return true;
    }
    public string LookLabel(string avatar, string choice)
    {
        if (choice is "Default" or "Random") return choice;
        EnsureVariants();
        return variants?.Find(avatar, choice)?.Label ?? choice.Replace("Look", "Look ", StringComparison.Ordinal);
    }
    public string HolidayBodyLabel(string avatar, string selection)
    {
        if (selection == "Auto") return "Auto · current event";
        if (!Enum.TryParse<SeasonalCompanionTheme>(selection, out var theme)) return selection;
        holidayBodies ??= new CompanionHolidayLibrary(assetDirectory);
        return holidayBodies.FindHoliday(avatar, theme)?.Label ?? SeasonalCompanionPolicy.Label(theme);
    }
    private void EnsureVariants()
    {
        if (variants != null || string.IsNullOrEmpty(assetDirectory)) return;
        variants = new CompanionVariantLibrary(assetDirectory);
        variantSelection = new CompanionVariantSelection(variants);
    }
    public void Update()
    {
        if (IsEditingAccessories) return;
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

    private float currentUiScale = 1;

    public void Draw(float scale)
    {
        currentUiScale = CompanionSceneSizing.UiScale(scale);
        if (IsEditingAccessories) { lastTick = 0; return; }
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
        var now = Stopwatch.GetTimestamp();
        var elapsed = lastTick == 0 ? 0 : (float)Stopwatch.GetElapsedTime(lastTick, now).TotalSeconds;
        lastTick = now;
        var canSwitch = !physics.IsCarried && physics.Placement is not (CompanionPlacement.Falling or CompanionPlacement.Jumping)
            && !homeRoutine.Active && !pointerPressed && !houseDragging && !hoverPausedLastFrame && PreviewPose == CompanionPose.Automatic;
        Update();
        var appearance = appearanceSelection.Update(new(config.Avatar, config.CompanionVariant,
            config.CompanionHolidayBodiesEnabled, config.CompanionHolidayBody, config.CompanionSeasonalTheme)
            { CostumeSelection = config.CompanionCostume },
            seasonal.CurrentTheme, canSwitch);
        var requestedAvatar = rotation.Update(config.Avatar, config.CompanionAutoRotateEnabled, config.CompanionRotationInterval,
            Environment.TickCount64, appearance.CanApplyBody);
        if (appearance.ManualChange) poof.Reset();
        var transition = poof.Update(displayAvatar, requestedAvatar, canSwitch,
            lastAvatar.Length > 0 && !appearance.ManualChange && config.CompanionAutoRotateEnabled && config.CompanionTransitionEffectsEnabled
                && config.AnimationsEnabled && config.CompanionMagicEnabled, elapsed);
        var applyBody = appearance.CanApplyBody && transition.CanApplyAppearance;
        var bodyAvatar = applyBody ? transition.AvatarToApply : displayAvatar;
        wardrobe ??= new CompanionWardrobeLibrary(assetDirectory);
        if (Configuration.AvatarNames.Skip(8).Contains(bodyAvatar) && wardrobe.FindDefault(bodyAvatar) == null) bodyAvatar = "Moonblob";
        EnsureVariants();
        var baseLook = variantSelection?.Resolve(bodyAvatar, config.CompanionVariant, config.CompanionLookAutoRotateEnabled,
            config.CompanionLookInterval, Environment.TickCount64 / 1000d, applyBody);
        displayAvatar = variantSelection?.Avatar is { Length: > 0 } selectedAvatar ? selectedAvatar : bodyAvatar;
        ResolveLook(baseLook, applyBody);
        bodyMotion.SetLook(displayAvatar, currentLook?.Id);
        if (lastAvatar != displayAvatar)
        {
            var firstAppearance = lastAvatar.Length == 0;
            lastAvatar = displayAvatar;
            if (firstAppearance && !homeRoutine.Active && !physics.Active)
            {
                motion.Reset(); gait.Reset(); homeRoutine.Reset(config.CompanionNapInterval); animationTime = 0;
            }
        }

        var layout = CompanionLayout.Create(viewport.Pos, viewport.Size,
            new Vector4(config.CompanionBoxLeft, config.CompanionBoxTop, config.CompanionBoxRight, config.CompanionBoxBottom),
            config.CompanionSize, CompanionSceneSizing.SceneScale(config) * CompanionSceneSizing.UiScale(scale), aspect);
        seasonalArtwork ??= new SeasonalCompanionRenderer(assetDirectory);
        seasonalVisible = false;
        if (layout.IsVisible && config.CompanionSeasonalEnabled && seasonal?.CurrentTheme is { } accessoryTheme
            && SeasonalCompanionRenderer.IsBow(accessoryTheme) && seasonalArtwork.IsAvailable)
        {
            var fraction = accessoryPadding = CompanionAccessoryPlacement.Padding(CompanionAccessoryPlacement.Find(config,
                displayAvatar, CurrentLookId, seasonal.CurrentTheme.Value.ToString()));
            var room = layout.Maximum - layout.Minimum;
            var fit = Math.Clamp(Math.Min(room.X / (layout.SpriteSize.X * (1 + fraction * 2)),
                room.Y / (layout.SpriteSize.Y * (1 + fraction * 2))), 0, 1);
            var sprite = layout.SpriteSize * fit;
            var padding = sprite * fraction;
            seasonalVisible = fit > 0;
            layout = layout with { Minimum = layout.Minimum + padding, Maximum = layout.Maximum - padding, SpriteSize = sprite };
        }
        if (BodyVisualPadding > 0)
        {
            var padding = layout.SpriteSize * BodyVisualPadding;
            var room = layout.Maximum - layout.Minimum;
            padding = Vector2.Min(padding, Vector2.Max(Vector2.Zero, (room - layout.SpriteSize) * .5f));
            layout = layout with { Minimum = layout.Minimum + padding, Maximum = layout.Maximum - padding };
        }
        if (layout != lastLayout) gait.Rebase();
        lastLayout = layout;
        if (PreviewPose != lastPreviewPose) gait.Reset();
        lastPreviewPose = PreviewPose;
        if (!config.CompanionHomeEnabled) FinishHouseDrag(false);
        var roamLayout = layout;
        var home = CompanionHomeGeometry.Bounds(layout, viewport.Pos, viewport.Size,
            new(config.CompanionHomeX, config.CompanionHomeY), CompanionSceneSizing.HomeEdge(config, scale));
        houseMirrored = CompanionPhysics.HomeFacesLeft((home.Minimum.X + home.Maximum.X) * .5f,
            viewport.Pos.X, viewport.Size.X, houseMirrored);
        UpdateHomeStyle(home, viewport.Pos, viewport.Size);
        if (homeStyleFrame is { } styledHome) home = new(styledHome.Minimum, styledHome.Maximum);
        CompanionSurface? homeObstacle = config.CompanionHomeEnabled
            ? new CompanionSurface(0, home.Minimum, home.Maximum, 0, 0) : null;
        // Initialize/clamp the dormant roaming position before physics uses it as
        // its return destination. Its approved movement and gait are unchanged.
        motion.Update(roamLayout, 0, false, homeObstacle);
        var frameFeature = config.CompanionFrameInteractionEnabled;
        var grounded = config.CompanionGroundedEnabled;
        if (!frameFeature && !grounded || !config.CompanionPickupEnabled && physics.IsCarried)
        {
            if (physics.IsCarried) pointerPressed = false;
            physics.Reset();
            if (frameFeature) pointerPressed = false;
        }
        IReadOnlyList<CompanionSurface> surfaces = frameFeature ? surfaceReader.Read(viewport.Pos, viewport.Size) : [];
        var options = new CompanionPhysicsOptions(frameFeature && config.CompanionPerchEnabled,
            frameFeature && config.CompanionContainmentEnabled, config.CompanionFallingEnabled, grounded, config.CompanionAutoHopEnabled);
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
        if (homeRoutine.Active) physics.Reset();
        physics.Update(surfaces, viewport.Pos, viewport.Pos + viewport.Size, layout.SpriteSize, elapsed,
            config.AnimationsEnabled && PreviewPose == CompanionPose.Automatic, hoverPausedLastFrame && !physics.IsCarried,
            options, motion.Position, homeObstacle, grounded && !homeRoutine.Active ? roamLayout : null);
        napArtwork ??= new CompanionNapArtwork(assetDirectory);
        var wasHomeActive = homeRoutine.Active;
        var napEligible = !physics.Active || grounded && (physics.Placement is CompanionPlacement.Perched or CompanionPlacement.Floor);
        if (physics.IsCarried || !grounded && physics.Active) { homeRoutine.Reset(config.CompanionNapInterval); napPhysics.Reset(); }
        PrepareHomeNavigation(surfaces, roamLayout, home, viewport.Pos, viewport.Size, options);
        var current = physics.Active ? physics.Position : motion.Position;
        var currentFacing = physics.Active ? physics.FacingLeft : motion.FacingLeft;
        homeRoutine.Update(elapsed, config.CompanionHomeEnabled && config.CompanionNapsEnabled
            && (currentLook != null || napArtwork.TryGet(displayAvatar, config.CompanionOutfit, out _)),
            !config.AnimationsEnabled || PreviewPose != CompanionPose.Automatic || hoverPausedLastFrame || pointerPressed
                || housePressed || houseDragging || !napEligible && !homeRoutine.Active,
            current, currentFacing, layout, home, houseMirrored, config.CompanionNapInterval,
            config.CompanionNapDuration, viewport.Pos, viewport.Pos + viewport.Size, config.CompanionHomeEnabled,
            homeStyleFrame, grounded ? NavigateHome : null);
        if (homeRoutine.Active) physics.Reset();
        if (wasHomeActive && !homeRoutine.Active)
        {
            if (grounded) physics.NavigateFrom(homeRoutine.Position, homeRoutine.FacingLeft);
            else if (!physics.Active) motion.ResumeAt(homeRoutine.Position, roamLayout);
            napPhysics.Reset(); gait.Rebase();
        }
        motion.Update(roamLayout, elapsed, config.AnimationsEnabled && !hoverPausedLastFrame && !pointerPressed
            && !houseDragging && !wasAttached && !physics.Active && !homeRoutine.Active && !grounded && PreviewPose == CompanionPose.Automatic, homeObstacle);
        if (!layout.IsVisible) return;
        var position = physics.Active ? physics.Position : homeRoutine.Active ? homeRoutine.Position : motion.Position;
        var facingLeft = physics.Active ? physics.FacingLeft : homeRoutine.Active ? homeRoutine.FacingLeft : motion.FacingLeft;
        var pose = !config.AnimationsEnabled ? CompanionPose.Idle : PreviewPose == CompanionPose.Automatic
            ? physics.Active ? physics.Pose : homeRoutine.Active ? homeRoutine.UsesNapArt ? CompanionPose.Idle : homeRoutine.TravelPose
                : motion.Walking ? CompanionPose.Walk : CompanionPose.Idle : PreviewPose;
        poseTime = pose != lastPose || !config.AnimationsEnabled ? 0 : (poseTime + Math.Clamp(elapsed, 0, .05f)) % 1000;
        lastPose = pose;
        bodyMotion.Update(elapsed, config.AnimationsEnabled, pose == CompanionPose.Walk);
        orbitRings.Update(elapsed, config.AnimationsEnabled && config.CompanionOrbitRingsEnabled, config.CompanionOrbitRingSpeed);
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
        motionDynamics.Update(elapsed, config.AnimationsEnabled && !hoverPausedLastFrame, gaitPosition,
            pose == CompanionPose.Walk, facingLeft, pose, gaitSpace, layout.SpriteSize.X);
        bodyMotion.SetIntensity(config.CompanionBodyMotionStrength);
        bodyMotion.SetDynamics(config.CompanionMotionReactions ? motionDynamics.Compression : 0,
            config.CompanionMotionReactions ? motionDynamics.Lean : 0,
            config.CompanionMotionReactions ? motionDynamics.Turn : 0);
        var frameIndex = pose == CompanionPose.Walk ? gait.Frame : CompanionAtlas.SelectFrame(pose, poseTime, config.AnimationsEnabled);
        var fourierWink = displayAvatar == "Wink" && config.CompanionFourierBlinkEnabled;
        var blinkEnabled = config.AnimationsEnabled && config.CompanionBlinkEnabled;
        var blink = fourierWink ? FourierBlink.Amount(animationTime, config.CompanionBlinkInterval, blinkEnabled)
            : BlinkAmount(animationTime, config.CompanionBlinkInterval, blinkEnabled);
        if (displayAvatar == "Wink" && pose == CompanionPose.Idle)
            frameIndex = 8 + Math.Clamp((int)MathF.Round(blink * 3), 0, 3);
        var draw = ImGui.GetForegroundDrawList();
        visualGrip = null;
        DrawHome(draw, layout);
        var hitStart = position + layout.SpriteSize * .18f;
        var hitEnd = position + layout.SpriteSize * .82f;
        var visualPadding = !seasonalVisible ? Vector2.Zero
            : layout.SpriteSize * accessoryPadding;
        visualPadding = Vector2.Max(visualPadding, layout.SpriteSize * BodyVisualPadding);
        var clipMinimum = physics.Active ? physics.ClipMinimum : homeRoutine.Active ? viewport.Pos : layout.Minimum - visualPadding;
        var clipMaximum = physics.Active ? physics.ClipMaximum : homeRoutine.Active ? viewport.Pos + viewport.Size : layout.Maximum + visualPadding;
        clipMinimum = Vector2.Max(clipMinimum, viewport.Pos); clipMaximum = Vector2.Min(clipMaximum, viewport.Pos + viewport.Size);
        draw.PushClipRect(clipMinimum, clipMaximum, true);
        // Background accents stay beneath the body; fitted bows use the foreground pass.
        draw.ChannelsSplit(2);
        try
        {
            if (!homeRoutine.UsesNapArt)
                CompanionSignatureEffects.Draw(draw, displayAvatar, OrbitRingRenderer.ThemeForLook(CurrentLookId) ?? CompanionAppearanceSelection.ResolveHoliday(
                    config.CompanionHolidayBodiesEnabled, config.CompanionHolidayBody, seasonal?.CurrentTheme),
                    position, layout.SpriteSize, ProceduralCompanion.Palette(config.CompanionPalette), animationTime,
                    config.CompanionMagicEnabled, config.AnimationsEnabled, mirrored: facingLeft);
            draw.ChannelsSetCurrent(1);
            if (homeRoutine.UsesNapArt && DrawNap(draw, layout, out var napBounds))
            {
                (hitStart, hitEnd) = napBounds;
            }
            else if (TryDrawLook(draw, position, layout.SpriteSize, facingLeft, pose, blink, out var lookBounds))
            {
                (hitStart, hitEnd) = lookBounds;
            }
            else if (artTexture != null)
            {
                // A loading/absent alternate texture falls back as one complete base body,
                // including its eyes and bow; do not leave the selected material on its face.
                bodyMotion.SetLook(displayAvatar, null);
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
                else if (pose is CompanionPose.Walk or CompanionPose.Idle)
                {
                    // All idle/walking bodies retain frame zero; only measured eye regions blink.
                    var floating = atlas!.GetFloat(gait.Phase, facingLeft, position, layout.SpriteSize);
                    if (bodyMotion.AppliesTo(displayAvatar))
                        (hitStart, hitEnd) = bodyMotion.DrawPadded(draw, artTexture.Handle, displayAvatar, floating, position,
                            layout.SpriteSize, gait.Phase, facingLeft, config.AnimationsEnabled, tint, BodyVisualPadding);
                    else
                        draw.AddImageQuad(artTexture.Handle, floating.A, floating.B, floating.C, floating.D,
                            floating.UvA, floating.UvB, floating.UvC, floating.UvD, tint);
                    if (!bodyMotion.AppliesTo(displayAvatar)) (hitStart, hitEnd) = floating.HitBounds;
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
                if (pose is CompanionPose.Walk or CompanionPose.Idle)
                    DrawBlink(draw, atlas!.File, body, blink, tint, position, layout.SpriteSize, facingLeft,
                        bodyMotion.AppliesTo(displayAvatar));
                var extra = DrawSeasonal(draw, seasonalFrame, false, body, position: position, size: layout.SpriteSize,
                    mirrored: facingLeft, deform: (pose is CompanionPose.Walk or CompanionPose.Idle) && bodyMotion.AppliesTo(displayAvatar));
                if (extra is { } bounds) { hitStart = Vector2.Min(hitStart, bounds.Minimum); hitEnd = Vector2.Max(hitEnd, bounds.Maximum); }
            }
            else
                ProceduralCompanion.Draw(draw, position, layout.SpriteSize, config, pose,
                    displayAvatar == "Wink" ? .17f + .13f * blink : animationTime, hoveredLastFrame, displayAvatar);
            if (!homeRoutine.UsesNapArt)
                ProceduralCompanion.DrawEffects(draw, position, layout.SpriteSize, config, pose, animationTime, hoveredLastFrame);
            if (poof.IsActive)
                poof.Draw(draw, Vector2.Min(hitStart, position), Vector2.Max(hitEnd, position + layout.SpriteSize),
                    ProceduralCompanion.Palette(config.CompanionPalette));
        }
        finally { draw.ChannelsMerge(); draw.PopClipRect(); }
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
        Vector2? handReady = hoveredLastFrame ? visualGrip ?? CompanionGrabAnchor.Center(position, layout.SpriteSize) : null;
        if (handReady == null && homeVisible)
        {
            var (houseMin, houseMax) = HomeBounds(layout);
            var houseSize = houseMax - houseMin;
            if (ImGui.IsMouseHoveringRect(houseMin + houseSize * new Vector2(.08f, .035f), houseMax - houseSize * new Vector2(.08f, .08f), false))
                handReady = CompanionGrabAnchor.Center(houseMin, houseSize);
        }
        Vector2? heldGrip = physics.IsCarried ? visualGrip ?? CompanionGrabAnchor.Center(position, layout.SpriteSize)
            : houseDragging ? CompanionGrabAnchor.Center(home.Minimum, home.Size) : null;
        DrawGrabHand(draw, viewport.Pos, viewport.Size, scale, elapsed, physics.IsCarried || houseDragging, handReady, heldGrip);
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
        if (homeStyleFrame is { } styled) return new(styled.Minimum, styled.Maximum);
        var viewport = ImGui.GetMainViewport();
        return CompanionHomeGeometry.Bounds(layout, viewport.Pos, viewport.Size,
            new Vector2(config.CompanionHomeX, config.CompanionHomeY), CompanionSceneSizing.HomeEdge(config, currentUiScale));
    }

    private void UpdateHomeStyle(CompanionHomeRect available, Vector2 viewportPosition, Vector2 viewportSize)
    {
        if (stylesFaulted) { homeStyleFrame = null; return; }
        try
        {
            styles ??= new CompanionHomeStyleLibrary(assetDirectory);
            styleSelection ??= new CompanionStyleSelection(styles);
            var style = styleSelection.ResolveHome(config.CompanionHomeStyle, config.CompanionHolidayHomeEnabled,
                seasonal?.CurrentTheme?.ToString(), !homeRoutine.Active && !houseDragging);
            var frame = style.Place(available.Minimum, available.Maximum, houseMirrored);
            var anchor = new Vector2(config.CompanionHomeX, config.CompanionHomeY);
            var desired = viewportPosition + Vector2.Clamp(anchor, Vector2.Zero, Vector2.One) * viewportSize
                - new Vector2(frame.Size.X * .5f, frame.Size.Y);
            var minimum = Vector2.Clamp(desired, viewportPosition, Vector2.Max(viewportPosition, viewportPosition + viewportSize - frame.Size));
            homeStyleFrame = frame with { Minimum = minimum, Maximum = minimum + frame.Size };
        }
        catch (Exception error)
        {
            stylesFaulted = true; homeStyleFrame = null;
            Services.Log.Error("Companion styles stopped ({ErrorType}).", error.GetType().Name);
        }
    }

    private void PrepareHomeNavigation(IReadOnlyList<CompanionSurface> surfaces, CompanionLayout layout,
        CompanionHomeRect house, Vector2 viewportPosition, Vector2 viewportSize, CompanionPhysicsOptions options)
    {
        navigationLayout = layout; navigationMinimum = viewportPosition; navigationMaximum = viewportPosition + viewportSize;
        navigationOptions = options with { Grounded = true, Perch = true, Contain = false };
        navigationObstacle = config.CompanionHomeEnabled ? new(0, house.Minimum, house.Maximum, 0, 0, false) : null;
        napSurfaces.Clear();
        if (options.Perch) foreach (var surface in surfaces) napSurfaces.Add(surface);
        if (!config.CompanionHomeEnabled) return;
        var last = Vector2.Max(viewportPosition, navigationMaximum - layout.SpriteSize);
        var corner = Vector2.Clamp(CompanionHomeGeometry.Corner(layout, house, houseMirrored, true), viewportPosition, last);
        var outside = Vector2.Clamp(homeStyleFrame?.OutsidePosition(layout.SpriteSize, grounded: true)
            ?? CompanionHomeGeometry.Outside(layout, house, houseMirrored, true), viewportPosition, last);
        // This is the narrow foreground threshold at the house's ground level,
        // never its roof or wall. Other changes of elevation need actual supports.
        var footY = outside.Y + layout.SpriteSize.Y * .9f;
        // Leave a clear landing beyond either side wall. A plane ending at the
        // corner's centre forces the bounded hop to land inside the wall footprint.
        var minimumX = Math.Min(corner.X, outside.X) - layout.SpriteSize.X * .6f;
        var maximumX = Math.Max(corner.X, outside.X) + layout.SpriteSize.X * 1.6f;
        napSurfaces.Add(new(ulong.MaxValue - 2, new(minimumX, footY), new(maximumX, footY + 1), 1, int.MaxValue - 1, false));
    }

    private CompanionHomeTravel NavigateHome(Vector2 start, Vector2 destination, float elapsed, bool forecourt)
    {
        if (!napPhysics.Active || Vector2.DistanceSquared(start, napPhysics.Position) > .01f)
            napPhysics.NavigateFrom(start, homeRoutine.FacingLeft);
        napPhysics.Update(napSurfaces, navigationMinimum, navigationMaximum, navigationLayout.SpriteSize, elapsed,
            config.AnimationsEnabled, false, navigationOptions, null, forecourt ? null : navigationObstacle,
            navigationLayout, destination, 2);
        return new(napPhysics.Position, napPhysics.FacingLeft, napPhysics.Pose,
            napPhysics.NavigationStatus == CompanionNavigationStatus.Blocked);
    }

    private bool DrawNap(ImDrawListPtr draw, CompanionLayout layout,
        out (Vector2 Minimum, Vector2 Maximum) bounds)
    {
        if (DrawRearEntry(draw, layout, out bounds)) return true;
        if (DrawLookNap(draw, layout, out bounds)) return true;
        bounds = default;
        if (napArtwork?.TryGet(displayAvatar, config.CompanionOutfit, out var sheet) != true) return false;
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, sheet!.File)).GetWrapOrDefault();
        var houseTexture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, homeStyleFrame?.File ?? "companion-house.png")).GetWrapOrDefault();
        if (texture == null || houseTexture == null) return false;
        var tint = ProceduralCompanion.IsProcedural(displayAvatar)
            ? ImGui.ColorConvertFloat4ToU32(Vector4.Lerp(Vector4.One, ProceduralCompanion.Palette(config.CompanionPalette), .36f))
            : 0xFFFFFFFFu;
        bounds = CompanionHomeRenderer.DrawNap(draw, texture.Handle, houseTexture.Handle, sheet, homeRoutine,
            layout.SpriteSize, HomeBounds(layout), houseMirrored, false, tint);
        var napBounds = bounds;
        NapAccessory(homeRoutine.ArtFrame, 1);
        if (homeVisible && !homeRoutine.InFrontOfHome) CompanionHomeRenderer.DrawHouseWalls(draw, houseTexture.Handle, HomeBounds(layout), houseMirrored, homeStyleFrame);
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

    private bool DrawRearEntry(ImDrawListPtr draw, CompanionLayout layout,
        out (Vector2 Minimum, Vector2 Maximum) bounds)
    {
        bounds = default;
        if (homeRoutine.State != CompanionHomeState.Entering || homeRoutine.EntryTurn < .5f) return false;
        homePoses ??= new CompanionHomePoseLibrary(assetDirectory);
        var id = currentLook == null || currentLook.Choice is "Default" or "WardrobeNone"
            || currentLook.Choice.StartsWith("Costume", StringComparison.Ordinal) ? "Default" : currentLook.Id;
        var climb = homeRoutine.EntryClimb;
        var pose = climb == 0 ? CompanionHomePose.TurnBack : climb < .15f ? CompanionHomePose.RearCrouch
            : (int)((climb - .15f) / .85f * 4) % 2 == 0 ? CompanionHomePose.RearClimbA : CompanionHomePose.RearClimbB;
        var source = homePoses.Find(displayAvatar, id, pose);
        if (source == null) return false;
        var crouch = homePoses.Find(displayAvatar, id, CompanionHomePose.RearCrouch)!;
        var correction = RearEntryScale(crouch);
        if (correction == 0) return false;
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, source.File)).GetWrapOrDefault();
        var houseTexture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, homeStyleFrame?.File ?? "companion-house.png")).GetWrapOrDefault();
        if (texture == null || houseTexture == null) return false;
        var support = homeRoutine.Position + layout.SpriteSize * new Vector2(.5f, .9f);
        var frame = source.Place(support, layout.SpriteSize, homeRoutine.ArtScale * correction, homeRoutine.FacingLeft);
        bounds = CompanionHomeRenderer.DrawBody(draw, texture.Handle, frame, homeRoutine, layout.SpriteSize);
        visualGrip = (bounds.Minimum + bounds.Maximum) * .5f;
        if (homeVisible && !homeRoutine.InFrontOfHome)
            CompanionHomeRenderer.DrawHouseWalls(draw, houseTexture.Handle, HomeBounds(layout), houseMirrored, homeStyleFrame);
        return true;
    }

    private float RearEntryScale(CompanionHomePoseFrame rearCrouch)
    {
        var front = currentLook;
        if (front?.Choice.StartsWith("Costume", StringComparison.Ordinal) == true)
            front = wardrobe?.FindBare(displayAvatar);
        var normal = front?.Normal;
        if (normal == null && displayAvatar == "Orbit" && variants?.OriginalOrbitCore.Count == 16)
            normal = variants.OriginalOrbitCore[0];
        float frontWidth; float frontReference;
        if (normal != null)
        {
            frontWidth = normal.Body.Z - normal.Body.X;
            frontReference = normal.SourceScale;
        }
        else
        {
            napArtwork ??= new CompanionNapArtwork(assetDirectory);
            if (!napArtwork.TryGet(displayAvatar, config.CompanionOutfit, out var sheet)) return 0;
            frontWidth = CompanionHomePoseScale.NapBodyWidth(displayAvatar, sheet!);
            frontReference = sheet!.ScaleReference;
        }
        return CompanionHomePoseScale.Correction(frontWidth, frontReference,
            rearCrouch.BodyBounds.Z - rearCrouch.BodyBounds.X, rearCrouch.ScaleReference);
    }

    private void ResolveLook(CompanionVariantLook? baseLook, bool canSwitch)
    {
        if (lookSelectionInitialized && !canSwitch) return;
        lookSelectionInitialized = true;
        wardrobe ??= new CompanionWardrobeLibrary(assetDirectory);
        baseLook ??= wardrobe.FindDefault(displayAvatar);
        var costume = ResolveCostume(config.CompanionCostume, seasonal?.CurrentTheme);
        if (costume is { } costumeTheme && wardrobe.FindCostume(displayAvatar, costumeTheme) is { } painted)
        { currentLook = painted; return; }
        var theme = CompanionAppearanceSelection.ResolveHoliday(config.CompanionHolidayBodiesEnabled,
            config.CompanionHolidayBody, seasonal?.CurrentTheme);
        if (theme is { } currentTheme)
        {
            holidayBodies ??= new CompanionHolidayLibrary(assetDirectory);
            currentLook = holidayBodies.FindHoliday(displayAvatar, currentTheme) ?? baseLook;
        }
        else currentLook = baseLook;
    }

    internal static SeasonalCompanionTheme? ResolveCostume(string selection, SeasonalCompanionTheme? active)
    {
        var value = selection == "Auto" ? active : Enum.TryParse<SeasonalCompanionTheme>(selection, out var theme)
            && selection == theme.ToString() ? theme : (SeasonalCompanionTheme?)null;
        return value is SeasonalCompanionTheme.AllSaintsWake or SeasonalCompanionTheme.Starlight
            or SeasonalCompanionTheme.MakeItRain or SeasonalCompanionTheme.MoonfireFaire ? value : null;
    }

    private bool TryDrawLook(ImDrawListPtr draw, Vector2 position, Vector2 size, bool mirrored, CompanionPose pose,
        float blink, out (Vector2 Minimum, Vector2 Maximum) bounds)
    {
        bounds = default;
        var orbit = displayAvatar == "Orbit";
        if (currentLook == null && (!orbit || variants?.OriginalOrbitCore.Count != 16)) return false;
        bodyMotion.SetLook(displayAvatar, currentLook?.Id);
        var normal = currentLook?.Normal ?? variants!.OriginalOrbitCore[0];
        var variantPose = pose switch
        {
            CompanionPose.Carry => (int)(poseTime / .4f) % 2 == 0 ? CompanionVariantPose.CarrySquirmA : CompanionVariantPose.CarrySquirmB,
            CompanionPose.Push => CompanionVariantPose.CarrySquirmA,
            CompanionPose.Talk => (int)(poseTime / .3f) % 2 == 0 ? CompanionVariantPose.MoveB : CompanionVariantPose.Wake,
            _ => CompanionVariantPose.Neutral
        };
        var coreIndex = pose switch { CompanionPose.Carry => 8, CompanionPose.Fall => 9, CompanionPose.Land => 10,
            CompanionPose.Push => 11, CompanionPose.Talk => 6, _ => 0 };
        var source = currentLook?[variantPose] ?? variants!.OriginalOrbitCore[coreIndex];
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, source.File)).GetWrapOrDefault();
        if (texture == null) return false;
        var tint = ImGui.ColorConvertFloat4ToU32(Vector4.Lerp(Vector4.One, ProceduralCompanion.Palette(config.CompanionPalette), .36f));
        SeasonalBodyTransform body;
        var deformed = false;
        if (orbit)
        {
            var placed = OrbitRingRenderer.Place(source, normal, position, size, mirrored);
            if (pose == CompanionPose.Carry)
            {
                var start = OrbitRingRenderer.Place(normal, normal, position, size, mirrored);
                var initialGrip = SeasonalBodyTransform.FromFrame(normal.Bounds, normal.SourceScale, start.Frame, mirrored).Map(normal.Grip);
                var lift = Math.Clamp(poseTime / CompanionGrabAnchor.CenteringSeconds, 0, 1);
                lift = lift * lift * (3 - 2 * lift);
                var desiredGrip = Vector2.Lerp(initialGrip, position + size * .5f, lift);
                var mappedGrip = SeasonalBodyTransform.FromFrame(source.Bounds, source.SourceScale, placed.Frame, mirrored).Map(source.Grip);
                var shift = desiredGrip - mappedGrip;
                placed = placed with { Frame = placed.Frame with { Position = placed.Frame.Position + shift }, Center = placed.Center + shift };
            }
            var ringTheme = OrbitRingRenderer.ThemeForLook(currentLook?.Choice);
            var movement = pose is CompanionPose.Walk or CompanionPose.Idle
                ? bodyMotion.OrbitMovement(placed.Center, placed.Radius, gait.Phase, mirrored, config.AnimationsEnabled)
                : Matrix3x2.Identity;
            Vector2 Move(Vector2 point) => Vector2.Transform(point, movement);
            var back = orbitRings.DrawPosed(draw, placed.Center, placed.Radius, ProceduralCompanion.Palette(config.CompanionPalette), false,
                ringTheme, config.CompanionMagicEnabled, mirrored, movement);
            var a = Move(placed.Frame.Position); var b = Move(placed.Frame.Position + new Vector2(placed.Frame.Size.X, 0));
            var c = Move(placed.Frame.Position + placed.Frame.Size); var d = Move(placed.Frame.Position + new Vector2(0, placed.Frame.Size.Y));
            draw.AddImageQuad(texture.Handle, a, b, c, d, placed.Frame.UvStart,
                new Vector2(placed.Frame.UvEnd.X, placed.Frame.UvStart.Y), placed.Frame.UvEnd,
                new Vector2(placed.Frame.UvStart.X, placed.Frame.UvEnd.Y), tint);
            var core = (Minimum: Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d)), Maximum: Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d)));
            var front = orbitRings.DrawPosed(draw, placed.Center, placed.Radius, ProceduralCompanion.Palette(config.CompanionPalette), true,
                ringTheme, config.CompanionMagicEnabled, mirrored, movement);
            bounds = (Vector2.Min(core.Minimum, Vector2.Min(back.Minimum, front.Minimum)), Vector2.Max(core.Maximum, Vector2.Max(back.Maximum, front.Maximum)));
            body = SeasonalBodyTransform.FromFrame(source.Bounds, source.SourceScale, placed.Frame, mirrored);
            body = body with { A = Move(body.A), B = Move(body.B), D = Move(body.D) };
        }
        else if (variantPose == CompanionVariantPose.Neutral && pose is CompanionPose.Walk or CompanionPose.Idle)
        {
            if (!ReferenceEquals(cachedLook, currentLook)) { cachedLook = currentLook; lookAtlas = currentLook!.NormalAtlas; }
            if (bodyMotion.UsesRoll(displayAvatar) && pose == CompanionPose.Walk)
            {
                var roll = lookAtlas!.GetRoll(gait.Phase, mirrored, position, size);
                if (bodyMotion.AppliesTo(displayAvatar))
                {
                    deformed = true;
                    var rolling = new CompanionFloatQuad(roll.A, roll.B, roll.C, roll.D, roll.UvA, roll.UvB, roll.UvC, roll.UvD,
                        roll.HitBounds.Minimum, roll.HitBounds.Maximum);
                    bounds = bodyMotion.DrawPadded(draw, texture.Handle, displayAvatar, rolling, position, size, gait.Phase,
                        mirrored, config.AnimationsEnabled, tint, BodyVisualPadding);
                }
                else
                {
                    draw.AddImageQuad(texture.Handle, roll.A, roll.B, roll.C, roll.D, roll.UvA, roll.UvB, roll.UvC, roll.UvD, tint);
                    bounds = roll.HitBounds;
                }
                body = new(source.Bounds, source.SourceScale, roll.A, roll.B, roll.D);
            }
            else
            {
                var floating = lookAtlas!.GetFloat(gait.Phase, mirrored, position, size);
                if (bodyMotion.AppliesTo(displayAvatar))
                {
                    deformed = true;
                    bounds = bodyMotion.DrawPadded(draw, texture.Handle, displayAvatar, floating, position, size, gait.Phase, mirrored,
                        config.AnimationsEnabled, tint, BodyVisualPadding);
                }
                else
                {
                    draw.AddImageQuad(texture.Handle, floating.A, floating.B, floating.C, floating.D, floating.UvA, floating.UvB, floating.UvC, floating.UvD, tint);
                    bounds = floating.HitBounds;
                }
                body = new(source.Bounds, source.SourceScale, floating.A, floating.B, floating.D);
            }
        }
        else
        {
            var lift = Math.Clamp(poseTime / CompanionGrabAnchor.CenteringSeconds, 0, 1);
            lift = lift * lift * (3 - 2 * lift);
            var grip = Vector2.Lerp(normal.Map(normal.Grip, position, size, mirrored), position + size * .5f, lift);
            var frame = pose == CompanionPose.Carry
                ? CompanionVariantRenderer.CarriedFrame(currentLook!, variantPose == CompanionVariantPose.CarrySquirmB, grip, size, mirrored)
                : source.GetFrame(position, size, mirrored);
            bounds = CompanionVariantRenderer.Draw(draw, texture.Handle, frame, tint);
            body = SeasonalBodyTransform.FromFrame(source.Bounds, source.SourceScale, frame, mirrored);
        }
        if (pose is CompanionPose.Walk or CompanionPose.Idle)
            DrawBlink(draw, normal.File, body, blink, tint, position, size, mirrored, deformed);
        var gripPoint = body.Map(source.Grip);
        visualGrip = deformed ? bodyMotion.WarpAttachment(displayAvatar, gripPoint, position, size, gait.Phase, mirrored, config.AnimationsEnabled) : gripPoint;
        var accessory = DrawLookAccessory(draw, source, body, position, size, mirrored,
            deformed);
        if (accessory is { } hat) bounds = (Vector2.Min(bounds.Minimum, hat.Minimum), Vector2.Max(bounds.Maximum, hat.Maximum));
        return true;
    }

    private void DrawBlink(ImDrawListPtr draw, string file, SeasonalBodyTransform body, float closure, uint tint,
        Vector2 position, Vector2 size, bool mirrored, bool deform)
    {
        if (!config.AnimationsEnabled || !config.CompanionBlinkEnabled || closure <= .001f || IsEditingAccessories) return;
        blinks ??= new CompanionBlinkLibrary(assetDirectory);
        var profile = blinks.Find(file, body.SourceRect) ?? wardrobe?.FindBlink(file, body.SourceRect);
        if (profile == null) return;
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, profile.ClosedFile)).GetWrapOrDefault();
        if (texture == null) return;
        CompanionBlinkRenderer.Draw(draw, texture.Handle, profile, body, closure, tint, deform
            ? point => bodyMotion.WarpAttachment(displayAvatar, point, position, size, gait.Phase, mirrored, config.AnimationsEnabled) : null);
    }

    private (Vector2 Minimum, Vector2 Maximum)? DrawLookAccessory(ImDrawListPtr draw, CompanionVariantFrame source,
        SeasonalBodyTransform body, Vector2 position, Vector2 size, bool mirrored, bool deform)
    {
        if (!homeRoutine.AccessoriesVisible || !seasonalVisible || seasonal?.CurrentTheme is not { } theme || seasonalArtwork == null
            || !seasonalArtwork.TryGetQuad(theme, body, new SeasonalAccessoryFit(source.AccessoryAnchor, source.AccessoryHeadWidth), out var quad)) return null;
        var fit = new SeasonalAccessoryFit(source.AccessoryAnchor, source.AccessoryHeadWidth);
        var adjustment = CompanionAccessoryPlacement.Find(config, displayAvatar, CurrentLookId, theme.ToString());
        quad = CompanionAccessoryPlacement.Apply(quad, body, fit, adjustment);
        if (deform)
        {
            var anchor = body.Map(CompanionAccessoryPlacement.AdjustedAnchor(fit, adjustment));
            var shift = Warp(anchor) - anchor;
            quad = quad with { A = quad.A + shift, B = quad.B + shift, C = quad.C + shift, D = quad.D + shift };
        }
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, SeasonalCompanionRenderer.File)).GetWrapOrDefault();
        return texture == null ? null : DrawAccessoryLayers(draw, texture.Handle, quad, theme);
        Vector2 Warp(Vector2 point) => bodyMotion.WarpAttachment(displayAvatar, point, position, size, gait.Phase, mirrored, config.AnimationsEnabled);
    }

    private bool DrawLookNap(ImDrawListPtr draw, CompanionLayout layout, out (Vector2 Minimum, Vector2 Maximum) bounds)
    {
        bounds = default;
        var orbit = displayAvatar == "Orbit";
        if (currentLook == null && (!orbit || variants?.OriginalOrbitCore.Count != 16)) return false;
        var pose = homeRoutine.State switch
        {
            CompanionHomeState.Entering when homeRoutine.EntryTurn < .5f => CompanionVariantPose.Neutral,
            CompanionHomeState.Sleeping => CompanionVariantPose.ClosedBlink,
            CompanionHomeState.Waking => CompanionVariantPose.Wake,
            _ => CompanionVariantPose.EnterBed
        };
        var index = homeRoutine.State switch { CompanionHomeState.Entering when homeRoutine.EntryTurn < .5f => 0,
            CompanionHomeState.Sleeping => 14, CompanionHomeState.Waking => 13, _ => 12 };
        var sleepingLook = currentLook;
        if (CurrentLookId.StartsWith("Costume", StringComparison.Ordinal) && !homeRoutine.AccessoriesVisible)
            sleepingLook = wardrobe?.FindBare(displayAvatar) ?? currentLook;
        var source = sleepingLook?[pose] ?? variants!.OriginalOrbitCore[index];
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, source.File)).GetWrapOrDefault();
        var houseTexture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, homeStyleFrame?.File ?? "companion-house.png")).GetWrapOrDefault();
        if (texture == null || houseTexture == null) return false;
        var size = layout.SpriteSize;
        var painted = size * homeRoutine.ArtScale;
        var anchor = homeRoutine.Position + size * new Vector2(.5f, .9f);
        var frame = source.GetFrame(Vector2.Zero, painted, homeRoutine.FacingLeft);
        frame = frame with { Position = frame.Position + anchor - painted * new Vector2(.5f, .9f) };
        bounds = CompanionHomeRenderer.DrawBody(draw, texture.Handle, frame, homeRoutine, size);
        var transform = SeasonalBodyTransform.FromFrame(source.Bounds, source.SourceScale, frame, homeRoutine.FacingLeft);
        visualGrip = CompanionHomeRenderer.BodyPoint(transform.Map(source.Grip), homeRoutine, size);
        var extra = DrawLookAccessory(draw, source, transform, homeRoutine.Position, size, homeRoutine.FacingLeft, false);
        if (extra is { } hat) bounds = (Vector2.Min(bounds.Minimum, hat.Minimum), Vector2.Max(bounds.Maximum, hat.Maximum));
        if (homeVisible && !homeRoutine.InFrontOfHome) CompanionHomeRenderer.DrawHouseWalls(draw, houseTexture.Handle, HomeBounds(layout), houseMirrored, homeStyleFrame);
        return true;
    }

    private (Vector2 Minimum, Vector2 Maximum)? DrawSeasonal(ImDrawListPtr draw, int index, bool nap,
        SeasonalBodyTransform body, uint tint = 0xFFFFFFFFu, Vector2 position = default, Vector2 size = default,
        bool mirrored = false, bool deform = false)
    {
        if (!homeRoutine.AccessoriesVisible || !seasonalVisible || seasonal?.CurrentTheme is not { } theme || seasonalArtwork == null
            || !seasonalArtwork.TryGetQuad(theme, displayAvatar, index, nap, body, out var quad)) return null;
        var adjustment = CompanionAccessoryPlacement.Find(config, displayAvatar, CurrentLookId, theme.ToString());
        var hasFit = seasonalArtwork.TryGetFit(displayAvatar, index, nap, out var fit);
        if (hasFit) quad = CompanionAccessoryPlacement.Apply(quad, body, fit, adjustment);
        if (deform && seasonalArtwork.TryGetSourceAnchor(displayAvatar, index, nap, out var sourceAnchor))
        {
            var anchor = body.Map(hasFit ? CompanionAccessoryPlacement.AdjustedAnchor(fit, adjustment) : sourceAnchor);
            var shift = Warp(anchor) - anchor;
            quad = quad with { A = quad.A + shift, B = quad.B + shift, C = quad.C + shift, D = quad.D + shift };
        }
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, SeasonalCompanionRenderer.File)).GetWrapOrDefault();
        return texture == null ? null : DrawAccessoryLayers(draw, texture.Handle, quad, theme, tint);
        Vector2 Warp(Vector2 point) => bodyMotion.WarpAttachment(displayAvatar, point, position, size, gait.Phase, mirrored, config.AnimationsEnabled);
    }

    private (Vector2 Minimum, Vector2 Maximum) DrawAccessoryLayers(ImDrawListPtr draw, ImTextureID texture,
        SeasonalOverlayQuad quad, SeasonalCompanionTheme theme, uint tint = 0xFFFFFFFFu)
    {
        draw.ChannelsSetCurrent(0);
        seasonalArtwork!.DrawLayer(draw, texture, quad, theme, false, tint);
        draw.ChannelsSetCurrent(1);
        seasonalArtwork.DrawLayer(draw, texture, quad, theme, true, tint);
        return quad.Bounds;
    }

    private void DrawSleepZs(ImDrawListPtr draw, CompanionHomeRect house, Vector2 viewportPosition, Vector2 viewportSize)
        => CompanionHomeRenderer.DrawZs(draw, house, houseMirrored, homeRoutine.SleepPhase, viewportPosition, viewportSize, homeStyleFrame);
    private void DrawHome(ImDrawListPtr draw, CompanionLayout layout)
    {
        homeVisible = false;
        if (!config.CompanionHomeEnabled || !layout.IsVisible) return;
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, homeStyleFrame?.File ?? "companion-house.png")).GetWrapOrDefault();
        if (texture == null) return;
        homeVisible = true;
        var (top, bottom) = HomeBounds(layout);
        var viewport = ImGui.GetMainViewport();
        var middle = (top.X + bottom.X) * .5f;
        houseMirrored = CompanionPhysics.HomeFacesLeft(middle, viewport.Pos.X, viewport.Size.X, houseMirrored);
        draw.PushClipRect(viewport.Pos, viewport.Pos + viewport.Size, true);
        CompanionHomeRenderer.DrawHouse(draw, texture.Handle, new CompanionHomeRect(top, bottom), houseMirrored, homeStyleFrame);
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

    private void DrawGrabHand(ImDrawListPtr draw, Vector2 viewportPosition, Vector2 viewportSize, float uiScale,
        float elapsed, bool dragging, Vector2? readyPoint, Vector2? gripPoint = null)
    {
        if (!config.CompanionGrabHandEnabled || !config.AnimationsEnabled)
        { handWasDragging = false; handTime = 1; return; }
        if (dragging != handWasDragging)
        {
            handTime = 0;
            if (dragging) styleSelection?.BeginPickup(config.CompanionHandStyle);
        }
        handWasDragging = dragging;
        handTime += float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0;
        if (dragging && gripPoint.HasValue) handPoint = gripPoint.Value;
        var ready = !dragging && handTime >= .25f && readyPoint.HasValue;
        if (!dragging && handTime >= .25f && !ready) return;
        var style = ready ? styleSelection?.ReadyHand(config.CompanionHandStyle) : styleSelection?.CurrentHand;
        var handPose = ready ? CompanionHandPose.Ready : !dragging ? CompanionHandPose.Release
            : handTime < .25f ? CompanionHandPose.OpenReach : CompanionHandPose.ClosedHold;
        var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, style?.Pose(handPose).File ?? "companion-grab-hand.png")).GetWrapOrDefault();
        if (texture == null) return;
        var incoming = Math.Clamp(handTime / .2f, 0, 1);
        var ease = 1 - MathF.Pow(1 - incoming, 3);
        var frame = ready ? 0 : !dragging ? 3 : incoming < .45f ? 0 : incoming < 1 ? 1 : 2;
        var edge = Math.Min(Math.Min(viewportSize.X, viewportSize.Y), CompanionSceneSizing.HandEdge(config, uiScale));
        var size = new Vector2(edge);
        // Position by the finger contact, independently of hand texture size.
        // The actual clamped body centre remains the shared grip while carried.
        var top = CompanionGrabAnchor.GripTopAt(ready ? readyPoint!.Value : handPoint, size, frame)
            - new Vector2(0, edge * (ready ? 0 : dragging ? (1 - ease) * .8f : handTime * 1.4f));
        var start = new Vector2(frame % 2, frame / 2) * .5f;
        var opacity = ready || dragging ? 1 : Math.Clamp(1 - handTime / .25f, 0, 1);
        draw.PushClipRect(viewportPosition, viewportPosition + viewportSize, true);
        var tint = ((uint)(opacity * 255) << 24) | 0xFFFFFFu;
        if (style != null)
        {
            var contact = (ready ? readyPoint!.Value : handPoint)
                - new Vector2(0, edge * (ready ? 0 : dragging ? (1 - ease) * .8f : handTime * 1.4f));
            CompanionStyleRenderer.DrawQuad(draw, texture.Handle, style.Frame(handPose, contact, edge), tint);
        }
        else draw.AddImage(texture.Handle, top, top + size, start, start + new Vector2(.5f), tint);
        draw.PopClipRect();
    }

    private bool DrawComment(ImDrawListPtr draw, Vector2 position, Vector2 size, Vector2 viewportPosition, Vector2 viewportSize, float scale)
    {
        if (homeRoutine.UsesNapArt) return false;
        var now = ImGui.GetTime();
        if (commentAvatar != displayAvatar || commentEntry != null && (Sayings == null || !Sayings.IsAvailable(commentEntry, displayAvatar)))
        { commentAvatar = displayAvatar; commentEntry = null; comment = string.Empty; commentUntil = 0; }
        if (!config.CompanionCommentsEnabled)
        { nextCommentAt = commentUntil = 0; comment = string.Empty; return false; }
        var interval = Math.Clamp(float.IsFinite(config.CompanionCommentInterval) ? config.CompanionCommentInterval : 120, 30, 600);
        if (nextCommentAt == 0) nextCommentAt = now + interval;
        if (now >= nextCommentAt && PreviewPose == CompanionPose.Automatic && !IsEditingAccessories)
        {
            var context = physics.IsCarried ? "Held"
                : homeRoutine.State == CompanionHomeState.Leaving ? "Waking"
                : homeRoutine.State == CompanionHomeState.Approaching ? "Home"
                : homeRoutine.State == CompanionHomeState.Returning || physics.Placement == CompanionPlacement.Returning ? "Returning"
                : physics.Placement == CompanionPlacement.Contained || physics.Placement == CompanionPlacement.Perched && !physics.IsOnRoamingPlane ? "Perched"
                : lastPose == CompanionPose.Walk ? "Walking" : "Idle";
            commentEntry = Sayings?.GetNext(displayAvatar, context);
            comment = commentEntry?.Text ?? string.Empty;
            commentUntil = now + 7;
            nextCommentAt = now + interval;
        }
        if (now >= commentUntil || comment.Length == 0 || housePressed || IsEditingAccessories) return false;
        scale = Math.Clamp(float.IsFinite(scale) ? scale : 1, .5f, 2);
        var padding = new Vector2(10, 7) * scale;
        var wrap = Math.Max(1, Math.Min(220 * scale, viewportSize.X - padding.X * 2 - 16));
        var fitted = CompanionSpeechLayout.Fit(comment, wrap, viewportSize.Y - padding.Y * 2 - 16,
            (text, width) => ImGui.CalcTextSize(text, false, width));
        if (fitted.Text.Length == 0) return false;
        var textSize = fitted.Size;
        var bubbleSize = textSize + padding * 2;
        var wanted = position + new Vector2((size.X - bubbleSize.X) * .5f, -bubbleSize.Y - 6 * scale);
        if (wanted.Y < viewportPosition.Y + 8) wanted.Y = position.Y + size.Y + 6 * scale;
        var minimum = viewportPosition + new Vector2(8);
        var bubblePosition = Vector2.Clamp(wanted, minimum, Vector2.Max(minimum, viewportPosition + viewportSize - bubbleSize - new Vector2(8)));
        draw.PushClipRect(viewportPosition, viewportPosition + viewportSize, true);
        draw.AddRectFilled(bubblePosition, bubblePosition + bubbleSize, 0xF21E1923, 9 * scale);
        draw.AddRect(bubblePosition, bubblePosition + bubbleSize, 0xBE9BC9DD, 9 * scale);
        draw.AddText(ImGui.GetFont(), ImGui.GetFontSize(), bubblePosition + padding, 0xFFF0F3F6, fitted.Text, wrap);
        draw.PopClipRect();
        if (physics.IsCarried) return false; // A speech bubble never captures the pickup/release gesture.
        if (!ImGui.IsMouseHoveringRect(bubblePosition, bubblePosition + bubbleSize, false)) return false;
        if (BeginOverlay("##OmniKitCompanionComment", bubblePosition, bubbleSize))
        {
            if (ImGui.InvisibleButton("##dismiss", bubbleSize)) commentUntil = 0;
            if (fitted.Text != comment) UiText.Hint(comment);
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
                config.CompanionSize, CompanionSceneSizing.SceneScale(config) * CompanionSceneSizing.UiScale(scale), aspect);
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
