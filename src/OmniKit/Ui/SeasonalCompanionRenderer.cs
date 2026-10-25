using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

/// <summary>Actual body source-to-screen affine transform, including its mirroring/rotation.</summary>
internal readonly record struct SeasonalBodyTransform(Vector4 SourceRect, float SourceScale, Vector2 A, Vector2 B, Vector2 D)
{
    public Vector2 Map(Vector2 point) => A + (B - A) * ((point.X - SourceRect.X) / (SourceRect.Z - SourceRect.X))
        + (D - A) * ((point.Y - SourceRect.Y) / (SourceRect.W - SourceRect.Y));

    public static SeasonalBodyTransform FromFrame(Vector4 sourceRect, float sourceScale, CompanionFrame frame, bool mirrored)
    {
        var left = frame.Position; var right = left + new Vector2(frame.Size.X, 0);
        var a = mirrored ? right : left; var b = mirrored ? left : right;
        return new(sourceRect, sourceScale, a, b, a + new Vector2(0, frame.Size.Y));
    }
}

internal readonly record struct SeasonalOverlayQuad(Vector2 A, Vector2 B, Vector2 C, Vector2 D, Vector2 UvStart, Vector2 UvEnd)
{
    public (Vector2 Minimum, Vector2 Maximum) Bounds =>
        (Vector2.Min(Vector2.Min(A, B), Vector2.Min(C, D)), Vector2.Max(Vector2.Max(A, B), Vector2.Max(C, D)));
}

/// <summary>Reviewed crown and head width in the same source-pixel units as the body transform.</summary>
internal readonly record struct SeasonalAccessoryFit(Vector2 Anchor, float HeadWidth);

/// <summary>One rigid seasonal accessory. No body recoloring, animation clock, movement or game actions.</summary>
internal sealed class SeasonalCompanionRenderer
{
    private readonly Dictionary<SeasonalCompanionTheme, Accessory> themes = [];
    private readonly Dictionary<string, Profile> profiles = new(StringComparer.Ordinal);
    private readonly Vector2 sheetSize;
    public const string File = "seasonal-accessories.png";
    // Reserve room for fitted hats; the caller fits the complete body and hat in the roaming area.
    public const float RequiredVisualPaddingFraction = .60f;
    public static bool IsBow(SeasonalCompanionTheme theme) => theme is SeasonalCompanionTheme.Heavensturn
        or SeasonalCompanionTheme.Valentione or SeasonalCompanionTheme.LittleLadies
        or SeasonalCompanionTheme.HatchingTide or SeasonalCompanionTheme.TheRising;
    public bool IsAvailable => themes.Count == 9 && profiles.Count == 8;

    public SeasonalCompanionRenderer(string assetDirectory)
    {
        try
        {
            var path = Path.Combine(assetDirectory, "seasonal-accessories.frames.json");
            if (!System.IO.File.Exists(path) || new FileInfo(path).Length > 128 * 1024) return;
            var data = JsonSerializer.Deserialize<Document>(System.IO.File.ReadAllText(path));
            if (data == null || !Valid(data)) return;
            sheetSize = new(data.Width, data.Height);
            foreach (var item in data.Themes)
            {
                var theme = Enum.Parse<SeasonalCompanionTheme>(item.Id);
                themes.Add(theme, item);
            }
            foreach (var profile in data.Profiles) profiles.Add(profile.Avatar, profile);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
    }

    public bool TryGetQuad(SeasonalCompanionTheme theme, string avatar, int frameIndex, bool nap,
        SeasonalBodyTransform body, out SeasonalOverlayQuad quad)
    {
        quad = default;
        if (!IsAvailable || !themes.TryGetValue(theme, out var accessory) || !profiles.TryGetValue(avatar, out var profile)
            || !ValidRect(body.SourceRect, new Vector2(8192)) || !float.IsFinite(body.SourceScale) || body.SourceScale <= 0
            || !Finite(body.A) || !Finite(body.B) || !Finite(body.D)) return false;
        var anchors = nap ? profile.NapSourceAnchors : profile.NormalSourceAnchors;
        if ((uint)frameIndex >= (uint)anchors.Length) return false;
        var attach = Point(anchors[frameIndex]);
        // Fail closed if metadata no longer matches this source pose after an art replacement.
        if (attach.X < body.SourceRect.X || attach.X > body.SourceRect.Z
            || attach.Y < body.SourceRect.Y || attach.Y > body.SourceRect.W) return false;
        var widths = nap ? profile.NapHeadWidths : profile.NormalHeadWidths;
        return widths.Length == anchors.Length
            ? TryGetQuad(theme, body, new SeasonalAccessoryFit(attach, widths[frameIndex]), out quad)
            : TryGetQuad(theme, body, attach, out quad);
    }

    public bool TryGetQuad(SeasonalCompanionTheme theme, SeasonalBodyTransform body, Vector2 attach, out SeasonalOverlayQuad quad)
        => TryGetQuadCore(theme, body, attach, 0, out quad);

    public bool TryGetSourceAnchor(string avatar, int frameIndex, bool nap, out Vector2 anchor)
    {
        anchor = default;
        if (!IsAvailable || !profiles.TryGetValue(avatar, out var profile)) return false;
        var anchors = nap ? profile.NapSourceAnchors : profile.NormalSourceAnchors;
        if ((uint)frameIndex >= (uint)anchors.Length) return false;
        anchor = Point(anchors[frameIndex]);
        return true;
    }

    public bool TryGetFit(string avatar, int frameIndex, bool nap, out SeasonalAccessoryFit fit)
    {
        fit = default;
        if (!profiles.TryGetValue(avatar, out var profile)) return false;
        var anchors = nap ? profile.NapSourceAnchors : profile.NormalSourceAnchors;
        var widths = nap ? profile.NapHeadWidths : profile.NormalHeadWidths;
        if ((uint)frameIndex >= (uint)anchors.Length || widths.Length != anchors.Length) return false;
        fit = new(Point(anchors[frameIndex]), widths[frameIndex]);
        return true;
    }

    public bool TryGetQuad(SeasonalCompanionTheme theme, SeasonalBodyTransform body, SeasonalAccessoryFit fit, out SeasonalOverlayQuad quad)
    {
        quad = default;
        return float.IsFinite(fit.HeadWidth) && fit.HeadWidth is >= 8 and <= 4096
            && TryGetQuadCore(theme, body, fit.Anchor, fit.HeadWidth, out quad);
    }

    private bool TryGetQuadCore(SeasonalCompanionTheme theme, SeasonalBodyTransform body, Vector2 attach, float headWidth, out SeasonalOverlayQuad quad)
    {
        quad = default;
        if (!IsAvailable || !themes.TryGetValue(theme, out var accessory) || !Finite(attach)
            || !ValidRect(body.SourceRect, new Vector2(8192)) || !float.IsFinite(body.SourceScale) || body.SourceScale <= 0
            || !Finite(body.A) || !Finite(body.B) || !Finite(body.D)
            || attach.X < body.SourceRect.X || attach.X > body.SourceRect.Z
            || attach.Y < body.SourceRect.Y || attach.Y > body.SourceRect.W) return false;
        var r = Rect(accessory.Bounds);
        var width = headWidth > 0 ? headWidth * accessory.HeadWidthFraction : body.SourceScale * accessory.WidthFraction;
        var factor = width / (r.Z - r.X);
        var pivot = Point(accessory.Pivot);
        Vector2 Map(float x, float y) => body.Map(attach + (new Vector2(x, y) - pivot) * factor);
        quad = new(Map(r.X, r.Y), Map(r.Z, r.Y), Map(r.Z, r.W), Map(r.X, r.W),
            new Vector2(r.X, r.Y) / sheetSize, new Vector2(r.Z, r.W) / sheetSize);
        return Finite(quad.A) && Finite(quad.B) && Finite(quad.C) && Finite(quad.D);
    }

    /// <summary>Caller applies the body's clipping, opacity and house-wall occlusion after this front layer.</summary>
    public static (Vector2 Minimum, Vector2 Maximum) Draw(ImDrawListPtr draw, ImTextureID texture,
        SeasonalOverlayQuad quad, uint tint = 0xFFFFFFFFu)
    {
        draw.AddImageQuad(texture, quad.A, quad.B, quad.C, quad.D, quad.UvStart,
            new Vector2(quad.UvEnd.X, quad.UvStart.Y), quad.UvEnd, new Vector2(quad.UvStart.X, quad.UvEnd.Y), tint);
        return quad.Bounds;
    }

    /// <summary>Bows draw once in front. Painted wardrobe hats have no separate overlay.</summary>
    public (Vector2 Minimum, Vector2 Maximum) DrawLayer(ImDrawListPtr draw, ImTextureID texture,
        SeasonalOverlayQuad quad, SeasonalCompanionTheme theme, bool front, uint tint = 0xFFFFFFFFu)
    {
        if (front && IsBow(theme)) return Draw(draw, texture, quad, tint);
        return quad.Bounds;
    }

    private static bool Valid(Document d)
    {
        if (d.Version != 1 || d.File != File || d.Width is <= 0 or > 8192 || d.Height is <= 0 or > 8192
            || d.Themes is not { Length: 9 } || d.Profiles is not { Length: 8 }) return false;
        var size = new Vector2(d.Width, d.Height);
        var ids = new HashSet<string>(StringComparer.Ordinal); var avatars = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in d.Themes)
        {
            if (t == null || !Enum.TryParse<SeasonalCompanionTheme>(t.Id, out var theme) || !Enum.IsDefined(theme)
                || t.Id != theme.ToString() || !ids.Add(t.Id) || t.Bounds is not { Length: 4 } || t.Pivot is not { Length: 2 }
                || !ValidRect(Rect(t.Bounds), size) || !Finite(Point(t.Pivot)) || !float.IsFinite(t.WidthFraction)
                || t.WidthFraction is < .1f or > .4f || !float.IsFinite(t.HeadWidthFraction) || t.HeadWidthFraction is < .2f or > 1.5f
                || t.Pivot[0] < t.Bounds[0] || t.Pivot[0] > t.Bounds[2]
                || t.Pivot[1] < t.Bounds[1] || t.Pivot[1] > t.Bounds[3]) return false;
        }
        foreach (var p in d.Profiles)
            if (p == null || !Configuration.AvatarNames.Contains(p.Avatar) || !avatars.Add(p.Avatar)
                || p.NormalSourceAnchors is not { Length: 24 } || p.NapSourceAnchors is not { Length: 4 }
                || !p.NormalSourceAnchors.Concat(p.NapSourceAnchors).All(a => a is { Length: 2 }
                    && a.All(float.IsFinite) && a.All(v => v is >= 0 and <= 8192))
                || !ValidWidths(p.NormalHeadWidths, 24) || !ValidWidths(p.NapHeadWidths, 4)) return false;
        return true;
    }
    private static Vector2 Point(float[] p) => new(p[0], p[1]);
    private static bool ValidWidths(float[]? widths, int count) => widths is { Length: 0 }
        || widths?.Length == count && widths.All(w => float.IsFinite(w) && w is >= 8 and <= 4096);
    private static Vector4 Rect(float[] p) => new(p[0], p[1], p[2], p[3]);
    private static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);
    private static bool ValidRect(Vector4 r, Vector2 size) => float.IsFinite(r.X) && float.IsFinite(r.Y)
        && float.IsFinite(r.Z) && float.IsFinite(r.W) && r.X >= 0 && r.Y >= 0
        && r.Z > r.X && r.W > r.Y && r.Z <= size.X && r.W <= size.Y;

    private sealed class Document
    {
        public int Version { get; set; }
        public string File { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public Accessory[] Themes { get; set; } = [];
        public Profile[] Profiles { get; set; } = [];
    }
    private sealed class Accessory
    {
        public string Id { get; set; } = string.Empty;
        public float[] Bounds { get; set; } = [];
        public float[] Pivot { get; set; } = [];
        public float WidthFraction { get; set; }
        public float HeadWidthFraction { get; set; } = 1;
    }
    private sealed class Profile
    {
        public string Avatar { get; set; } = string.Empty;
        public float[][] NormalSourceAnchors { get; set; } = [];
        public float[][] NapSourceAnchors { get; set; } = [];
        public float[] NormalHeadWidths { get; set; } = [];
        public float[] NapHeadWidths { get; set; } = [];
    }
}
