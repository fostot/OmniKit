using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

/// <summary>A still, isolated fitting stage. Only Save writes head-relative preferences.</summary>
internal sealed class CompanionAccessoryEditor(Configuration config, string assetDirectory)
{
    private readonly Dictionary<(string Avatar, string Look, string Theme), CompanionAccessoryAdjustment> drafts = [];
    private CompanionVariantLibrary? variants;
    private CompanionHolidayLibrary? holidays;
    private CompanionAnimationLibrary? originals;
    private SeasonalCompanionRenderer? accessories;
    private CompanionWardrobeLibrary? wardrobe;
    private CompanionBlinkLibrary? blinks;
    private float previewTime;
    private readonly OrbitRingRenderer rings = new();
    private string avatar = "Moonblob";
    private string look = "Default";
    private SeasonalCompanionTheme theme = SeasonalCompanionTheme.AllSaintsWake;
    private Vector2 dragStart;
    private CompanionAccessoryAdjustment? dragFit;
    private bool resizing;
    private bool dragging;
    private Vector2 resizeDirection;
    private float stageEdge;
    private Vector2 stageCanvas;
    private string message = "";
    public bool IsOpen { get; private set; }
    public bool IsDirty { get; private set; }

    public void Open(string selectedAvatar, string selectedLook, SeasonalCompanionTheme? selectedTheme)
    {
        drafts.Clear();
        foreach (var fit in config.CompanionAccessoryFits) drafts[(fit.Avatar, fit.Look, fit.Theme)] = fit;
        avatar = Configuration.AvatarNames.Contains(selectedAvatar) ? selectedAvatar : config.Avatar;
        look = Choices.Contains(selectedLook) ? selectedLook : "Default";
        theme = selectedTheme is { } chosen && SeasonalCompanionRenderer.IsBow(chosen) ? chosen : SeasonalCompanionTheme.Heavensturn;
        previewTime = 0;
        IsDirty = false; message = ""; dragging = resizing = false; stageEdge = 0; IsOpen = true;
    }

    public void Cancel() { IsOpen = false; IsDirty = false; drafts.Clear(); dragging = resizing = false; }
    private (string, string, string) Key => (avatar, look, theme.ToString());
    private CompanionAccessoryAdjustment Current => drafts.GetValueOrDefault(Key) ?? new(avatar, look, theme.ToString(), 0, 0, 1);
    private void Change(CompanionAccessoryAdjustment fit) { drafts[Key] = fit; IsDirty = true; message = ""; }
    internal static IReadOnlyList<string> Choices { get; } = new[] { "Default", "Look1", "Look2", "Look3", "Look4" }
        .Concat(Configuration.SeasonalThemeNames.Skip(1).Select(t => "Holiday" + t))
        .Concat(Configuration.CostumeNames.Skip(2).Select(t => "Costume" + t)).ToArray();

    public bool Save()
    {
        if (!IsOpen) return false;
        var previous = config.CompanionAccessoryFits;
        config.CompanionAccessoryFits = drafts.Values.Where(f => !CompanionAccessoryPlacement.IsDefault(f)).ToList();
        try { config.Save(); IsDirty = false; message = "Saved"; return true; }
        catch
        {
            config.CompanionAccessoryFits = previous;
            message = "Save failed; your saved fits were kept.";
            return false;
        }
    }

    public void Draw(float scale)
    {
        if (!IsOpen) return;
        var viewport = ImGui.GetMainViewport();
        var gutter = 12 * scale;
        var size = Vector2.Min(new Vector2(580, 570) * scale, viewport.WorkSize - new Vector2(gutter * 2));
        ImGui.SetNextWindowSize(Vector2.Max(new(1), size), ImGuiCond.Always);
        ImGui.SetNextWindowPos(viewport.WorkPos + viewport.WorkSize * .5f, ImGuiCond.Always, new(.5f));
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoDocking;
        if (!ImGui.Begin("Accessory fit###OmniKit.AccessoryFit", flags)) { ImGui.End(); return; }
        try
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape)) { Cancel(); return; }
            UiText.Line("Fit accessory", new(.48f, .78f, .74f, 1), outline: true);
            EnsureArtwork();
            if (ImGui.BeginTable("fit-choices", 3, ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableNextColumn(); ImGui.TextUnformatted("Companion"); ImGui.SetNextItemWidth(-1);
                if (UiText.BeginCombo("##fit-avatar", avatar))
                {
                    foreach (var value in Configuration.AvatarNames)
                        if (UiText.Selectable(value, avatar == value))
                        { avatar = value; if (!AvailableLook(look)) look = "Default"; stageEdge = previewTime = 0; dragging = resizing = false; }
                    ImGui.EndCombo();
                }
                ImGui.TableNextColumn(); ImGui.TextUnformatted("Body"); ImGui.SetNextItemWidth(-1);
                if (UiText.BeginCombo("##fit-body", LookLabel(look)))
                {
                    foreach (var value in Choices.Where(AvailableLook))
                        if (UiText.Selectable(LookLabel(value), look == value)) { look = value; stageEdge = previewTime = 0; dragging = resizing = false; }
                    ImGui.EndCombo();
                }
                ImGui.TableNextColumn(); ImGui.TextUnformatted("Accessory"); ImGui.SetNextItemWidth(-1);
                if (UiText.BeginCombo("##fit-accessory", SeasonalCompanionPolicy.Label(theme)))
                {
                    foreach (var value in Enum.GetValues<SeasonalCompanionTheme>().Where(SeasonalCompanionRenderer.IsBow))
                        if (UiText.Selectable(SeasonalCompanionPolicy.Label(value), theme == value)) { theme = value; stageEdge = 0; dragging = resizing = false; }
                    ImGui.EndCombo();
                }
                ImGui.EndTable();
            }
            var footer = ImGui.GetFrameHeightWithSpacing() * 2 + ImGui.GetStyle().ItemSpacing.Y;
            var available = ImGui.GetContentRegionAvail();
            var canvas = new Vector2(available.X, Math.Max(1, available.Y - footer));
            DrawStage(canvas, scale);
            var current = Current;
            var percent = current.Scale * 100;
            var controlWidth = Math.Max(1, (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * .5f);
            ImGui.SetNextItemWidth(controlWidth);
            if (UiText.SliderFloat("##fit-size", ref percent, 25, 300, "Size %.0f%%"))
                Change(current with { Scale = percent / 100 });
            ImGui.SameLine(); ImGui.SetNextItemWidth(controlWidth);
            var angle = Current.RotationDegrees;
            if (UiText.SliderFloat("##fit-angle", ref angle, -180, 180, "Angle %.0f deg"))
                Change(Current with { RotationDegrees = angle });
            ImGui.BeginDisabled(!IsDirty);
            if (UiText.Button("Save fits")) Save();
            ImGui.EndDisabled(); ImGui.SameLine();
            if (UiText.Button("Reset fit")) Change(new(avatar, look, theme.ToString(), 0, 0, 1));
            ImGui.SameLine();
            if (UiText.Button("Fit view")) stageEdge = 0;
            ImGui.SameLine();
            if (UiText.Button("Cancel")) { Cancel(); return; }
            ImGui.SameLine();
            UiText.Status(message.Length > 0 ? message : IsDirty ? "Unsaved" : "");
        }
        finally { ImGui.End(); }
    }

    private void EnsureArtwork()
    {
        variants ??= new CompanionVariantLibrary(assetDirectory);
        holidays ??= new CompanionHolidayLibrary(assetDirectory);
        originals ??= new CompanionAnimationLibrary(assetDirectory);
        accessories ??= new SeasonalCompanionRenderer(assetDirectory);
        wardrobe ??= new CompanionWardrobeLibrary(assetDirectory);
        blinks ??= new CompanionBlinkLibrary(assetDirectory);
    }

    private bool AvailableLook(string value) => value == "Default" || value.StartsWith("Costume", StringComparison.Ordinal)
        || value.StartsWith("Holiday", StringComparison.Ordinal) && holidays?.Looks.Any(l => l.Avatar == avatar) == true
        || variants?.Find(avatar, value) != null;

    private string LookLabel(string value)
        => value.StartsWith("Costume", StringComparison.Ordinal) && Enum.TryParse<SeasonalCompanionTheme>(value[7..], out var costume)
            ? wardrobe?.FindCostume(avatar, costume)?.Label ?? SeasonalCompanionPolicy.Label(costume) + " costume"
            : value == "Default" ? "Original" : value.StartsWith("Holiday", StringComparison.Ordinal)
            && Enum.TryParse<SeasonalCompanionTheme>(value[7..], out var holiday)
                ? holidays?.FindHoliday(avatar, holiday)?.Label ?? SeasonalCompanionPolicy.Label(holiday) + " body"
                : variants?.Find(avatar, value)?.Label ?? value;

    private bool TryBody(Vector2 position, Vector2 cell, bool mirrored, out CompanionVariantFrame source,
        out CompanionFrame frame, out SeasonalBodyTransform body, out SeasonalAccessoryFit fit)
    {
        source = null!; frame = default; body = default; fit = default;
        var selected = look.StartsWith("Costume", StringComparison.Ordinal) && Enum.TryParse<SeasonalCompanionTheme>(look[7..], out var costume)
            ? wardrobe?.FindCostume(avatar, costume) : look == "Default" && wardrobe?.FindDefault(avatar) is { } extra
                ? extra : look.StartsWith("Holiday", StringComparison.Ordinal)
            && Enum.TryParse<SeasonalCompanionTheme>(look[7..], out var holiday)
                ? holidays?.FindHoliday(avatar, holiday) : variants?.Find(avatar, look);
        if (selected != null) source = selected.Normal;
        else if (look == "Default" && avatar == "Orbit" && variants?.OriginalOrbitCore.Count == 16) source = variants.OriginalOrbitCore[0];
        else if (look == "Default" && originals?.TryGet(avatar, config.CompanionOutfit, out var atlas) == true)
        {
            var region = atlas!.Frames[0];
            if (accessories?.TryGetFit(avatar, 0, false, out fit) != true) return false;
            source = new(atlas.File, atlas.SheetSize, region.Rect, region.Body, region.Anchor, region.Anchor,
                fit.Anchor, atlas.SourceScale, fit.HeadWidth);
        }
        if (source == null) return false;
        fit = new(source.AccessoryAnchor, source.AccessoryHeadWidth);
        frame = avatar == "Orbit" ? OrbitRingRenderer.Place(source, source, position, cell, mirrored).Frame
            : source.GetFrame(position, cell, mirrored);
        body = SeasonalBodyTransform.FromFrame(source.Bounds, source.SourceScale, frame, mirrored);
        return true;
    }

    private void DrawStage(Vector2 canvas, float scale)
    {
        var minimum = ImGui.GetCursorScreenPos(); var maximum = minimum + canvas;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(minimum, maximum, 0xFF221C17, 2 * scale);
        ImGui.InvisibleButton("##accessory-stage", canvas);
        var hovered = ImGui.IsItemHovered();
        if (hovered && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) UiText.Hint("Drag accessory to position it. Drag its corner to resize. Fit view changes only the preview zoom.");
        var adjustment = Current;
        var fraction = CompanionAccessoryPlacement.Padding(adjustment);
        if (look.StartsWith("Costume", StringComparison.Ordinal)) fraction = Math.Max(fraction, CompanionWardrobeLibrary.VisualPaddingFraction);
        if (stageEdge <= 0 || stageCanvas != canvas)
        {
            stageCanvas = canvas;
            stageEdge = Math.Min(230 * scale, Math.Max(1, Math.Min(canvas.X, canvas.Y) - 12 * scale) / (1 + fraction * 2));
        }
        var edge = stageEdge;
        var cell = new Vector2(edge); var position = minimum + (canvas - cell) * .5f;
        draw.PushClipRect(minimum + Vector2.One, maximum - Vector2.One, true);
        try
        {
            if (!TryBody(position, cell, false, out var source, out var frame, out var body, out var fit))
            { draw.AddText(minimum + new Vector2(10 * scale), 0xFFB3A59B, "This body artwork is unavailable."); return; }
            var texture = Services.Textures.GetFromFile(Path.Combine(assetDirectory, source.File)).GetWrapOrDefault();
            if (texture == null) return;
            var hasAccessory = accessories!.TryGetQuad(theme, body, fit, out var original);
            var quad = hasAccessory ? CompanionAccessoryPlacement.Apply(original, body, fit, adjustment) : default;
            var accessoryTexture = hasAccessory
                ? Services.Textures.GetFromFile(Path.Combine(assetDirectory, SeasonalCompanionRenderer.File)).GetWrapOrDefault() : null;
            if (accessoryTexture != null) accessories.DrawLayer(draw, accessoryTexture.Handle, quad, theme, false);
            if (avatar == "Orbit")
            {
                var orbit = OrbitRingRenderer.Place(source, source, position, cell, false);
                var ringTheme = OrbitRingRenderer.ThemeForLook(look);
                rings.Draw(draw, orbit.Center, orbit.Radius, Vector4.One, false, ringTheme, config.CompanionMagicEnabled);
                CompanionVariantRenderer.Draw(draw, texture.Handle, frame);
                rings.Draw(draw, orbit.Center, orbit.Radius, Vector4.One, true, ringTheme, config.CompanionMagicEnabled);
            }
            else CompanionVariantRenderer.Draw(draw, texture.Handle, frame);
            if (config.AnimationsEnabled && config.CompanionBlinkEnabled)
            {
                previewTime += Math.Clamp(ImGui.GetIO().DeltaTime, 0, .05f);
                var closure = avatar == "Wink" && config.CompanionFourierBlinkEnabled
                    ? FourierBlink.Amount(previewTime, config.CompanionBlinkInterval, true)
                    : RoamingCompanion.BlinkAmount(previewTime, config.CompanionBlinkInterval, true);
                var profile = blinks?.Find(source.File, source.Bounds) ?? wardrobe?.FindBlink(source.File, source.Bounds);
                if (closure > .001f && profile != null)
                {
                    var closed = Services.Textures.GetFromFile(Path.Combine(assetDirectory, profile.ClosedFile)).GetWrapOrDefault();
                    if (closed != null) CompanionBlinkRenderer.Draw(draw, closed.Handle, profile, body, closure, 0xFFFFFFFFu);
                }
            }
            if (accessoryTexture == null) return;
            accessories.DrawLayer(draw, accessoryTexture.Handle, quad, theme, true);
            var bounds = quad.Bounds;
            var radius = 5 * scale;
            var handle = Vector2.Clamp(bounds.Maximum, minimum + new Vector2(radius + 1),
                Vector2.Max(minimum + new Vector2(radius + 1), maximum - new Vector2(radius + 1)));
            draw.AddRect(bounds.Minimum, bounds.Maximum, 0xFFB7CECB, 0, ImDrawFlags.None, scale);
            draw.AddRectFilled(handle - new Vector2(radius), handle + new Vector2(radius), 0xFFE9D8C5, scale);
            var mouse = ImGui.GetMousePos();
            var onHandle = hovered && Vector2.Distance(mouse, handle) <= 12 * scale;
            var onHat = hovered && mouse.X >= bounds.Minimum.X && mouse.Y >= bounds.Minimum.Y
                && mouse.X <= bounds.Maximum.X && mouse.Y <= bounds.Maximum.Y;
            if (onHandle || resizing) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNwse);
            else if (onHat || dragging) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && (onHandle || onHat))
            {
                resizing = onHandle; dragging = !onHandle; dragStart = mouse; dragFit = adjustment;
                resizeDirection = Vector2.Max(Vector2.One, bounds.Maximum - bounds.Minimum);
            }
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) { dragging = resizing = false; dragFit = null; }
            if (dragFit is { } start && (dragging || resizing))
            {
                var delta = mouse - dragStart;
                if (resizing) Change(start with { Scale = Math.Clamp(start.Scale *
                    (1 + Vector2.Dot(delta, resizeDirection) / resizeDirection.LengthSquared()), .25f, 3) });
                else
                {
                    var local = CompanionAccessoryPlacement.LocalDelta(body, fit, delta);
                    Change(start with { OffsetX = Math.Clamp(start.OffsetX + local.X, -1, 1), OffsetY = Math.Clamp(start.OffsetY + local.Y, -1, 1) });
                }
            }
        }
        finally { draw.PopClipRect(); }
    }
}
