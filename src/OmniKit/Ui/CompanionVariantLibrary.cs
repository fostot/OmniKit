using System.Numerics;
using System.Text.Json;

namespace OmniKit.Ui;

internal enum CompanionVariantPose { Neutral, MoveA, MoveB, ClosedBlink, CarrySquirmA, CarrySquirmB, EnterBed, Wake }

/// <summary>One immutable source crop. All frames use the look's common scale, including sheet unit conversion.</summary>
internal sealed record CompanionVariantFrame(string File, Vector2 SheetSize, Vector4 Bounds, Vector4 Body,
    Vector2 Anchor, Vector2 Grip, Vector2 AccessoryAnchor, float SourceScale, float AccessoryHeadWidth = 0)
{
    public CompanionSpriteAtlas AsAtlas() => new(File, SheetSize, SourceScale, [new(Bounds, Anchor, Body)]);
    public CompanionFrame GetFrame(Vector2 position, Vector2 cellSize, bool mirrored = false)
    {
        var sourceMin = new Vector2(Bounds.X, Bounds.Y); var sourceMax = new Vector2(Bounds.Z, Bounds.W);
        var scale = cellSize.Y / SourceScale; var size = (sourceMax - sourceMin) * scale;
        var offset = (Anchor - sourceMin) * scale;
        if (mirrored) offset.X = size.X - offset.X;
        var uvMin = sourceMin / SheetSize; var uvMax = sourceMax / SheetSize;
        if (mirrored) (uvMin.X, uvMax.X) = (uvMax.X, uvMin.X);
        return new(uvMin, uvMax, position + cellSize * new Vector2(.5f, .9f) - offset, size);
    }
    public Vector2 Map(Vector2 source, Vector2 position, Vector2 cellSize, bool mirrored = false)
    {
        var offset = (source - Anchor) * (cellSize.Y / SourceScale);
        if (mirrored) offset.X = -offset.X;
        return position + cellSize * new Vector2(.5f, .9f) + offset;
    }
}

internal sealed record CompanionVariantLook(string Avatar, string Choice, string Id, string Label,
    float ScaleReference, IReadOnlyList<CompanionVariantFrame> Frames, Vector4? EyeEllipse, bool Ringless)
{
    public CompanionVariantFrame Normal => Frames[(int)CompanionVariantPose.Neutral];
    public CompanionVariantFrame this[CompanionVariantPose pose] => Frames[(int)pose];
    public CompanionSpriteAtlas NormalAtlas => Normal.AsAtlas();
}

/// <summary>Optional reviewed artwork only. Invalid metadata fails as a unit to the existing base artwork.</summary>
internal sealed class CompanionVariantLibrary
{
    public const string MetadataFile = "companion-variants.frames.json";
    private static readonly string[] Avatars = ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism"];
    private static readonly HashSet<string> AllowedFiles = new(Avatars.Select(a => a.ToLowerInvariant() + "-variants.png")
        .Concat(["puff-variants-motion.png", "ember-variants-motion.png", "orbit-core.png"]), StringComparer.Ordinal);
    private IReadOnlyList<CompanionVariantLook> looks = Array.Empty<CompanionVariantLook>();
    public IReadOnlyList<CompanionVariantLook> Looks => looks;
    public IReadOnlyList<CompanionVariantFrame> OriginalOrbitCore { get; private set; } = Array.Empty<CompanionVariantFrame>();
    public IReadOnlyList<string> Files { get; private set; } = Array.Empty<string>();
    public bool IsAvailable => looks.Count == 32;
    public CompanionVariantLibrary(string assetDirectory)
    {
        try
        {
            var path = Path.Combine(assetDirectory, MetadataFile);
            if (!System.IO.File.Exists(path) || new FileInfo(path).Length > 512 * 1024) return;
            var data = JsonSerializer.Deserialize<Document>(System.IO.File.ReadAllText(path));
            if (data?.Version != 1 || data.Variants is not { Length: 32 } || data.Sheets is not { Length: 11 }
                || data.OriginalOrbitCore is not { Frames.Length: 16, Ringless: true }) return;
            var sheets = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            foreach (var s in data.Sheets)
            {
                if (s == null || !AllowedFiles.Contains(s.File) || s.Width is < 1 or > 8192 || s.Height is < 1 or > 8192
                    || !sheets.TryAdd(s.File, new(s.Width, s.Height))) return;
            }
            var parsed = new List<CompanionVariantLook>(); var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var v in data.Variants)
            {
                if (v == null || !Avatars.Contains(v.Avatar) || v.Choice is not ("Look1" or "Look2" or "Look3" or "Look4")
                    || !keys.Add(v.Avatar + "/" + v.Choice) || !Text(v.Id, 48) || !Text(v.Label, 48)
                    || !Scale(v.ScaleReference) || v.Frames is not { Length: 8 } || v.Ringless != (v.Avatar == "Orbit")) return;
                var frames = new List<CompanionVariantFrame>();
                for (var i = 0; i < v.Frames.Length; i++)
                {
                    if (v.Frames[i]?.State != ((CompanionVariantPose)i).ToString()) return;
                    var frame = ParseFrame(v.Frames[i], v.ScaleReference, sheets);
                    if (frame == null || !(frame.File == v.Avatar.ToLowerInvariant() + "-variants.png"
                        || v.Avatar is "Puff" or "Ember" && frame.File == v.Avatar.ToLowerInvariant() + "-variants-motion.png")) return;
                    frames.Add(frame);
                }
                if (!TryParseEye(v.Avatar, v.EyeEllipse, frames[0].Bounds, out var eye)) return;
                parsed.Add(new(v.Avatar, v.Choice, v.Id, v.Label, v.ScaleReference, frames.AsReadOnly(), eye, v.Ringless));
            }
            var original = new List<CompanionVariantFrame>();
            string[] originalStates = ["OpenEyes", "HalfBlink", "ClosedBlink", "Smile", "MouthClosed", "MouthSmallA", "MouthWideO", "MouthHappy", "Carry", "Fall", "Land", "Push", "EnterNap", "SettleNap", "SleepInhale", "SleepExhale"];
            for (var i = 0; i < originalStates.Length; i++)
            {
                var f = data.OriginalOrbitCore.Frames[i];
                if (f?.State != originalStates[i]) return;
                var frame = ParseFrame(f, data.OriginalOrbitCore.ScaleReference, sheets);
                if (frame == null || frame.File != "orbit-core.png") return;
                original.Add(frame);
            }
            looks = parsed.AsReadOnly(); OriginalOrbitCore = original.AsReadOnly(); Files = Array.AsReadOnly(sheets.Keys.ToArray());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
    }

    public CompanionVariantLook? Find(string avatar, string choice) => looks.FirstOrDefault(v => v.Avatar == avatar && v.Choice == choice);

    internal static CompanionVariantFrame? ParseFrame(Frame? f, float scale, IReadOnlyDictionary<string, Vector2> sheets,
        float visualPadding = 0)
    {
        if (f == null || !float.IsFinite(visualPadding) || visualPadding is < 0 or > .6f
            || !Scale(scale) || !sheets.TryGetValue(f.File, out var size) || !Numbers(f.Bounds, 4)
            || !Numbers(f.BodyBounds, 4) || !Numbers(f.Anchor, 2) || !Numbers(f.Grip, 2) || !Numbers(f.AccessoryAnchor, 2)
            || !float.IsFinite(f.SourceUnitScale) || f.SourceUnitScale is < .5f or > 2f) return null;
        var r = V4(f.Bounds); var body = V4(f.BodyBounds); var a = V2(f.Anchor); var grip = V2(f.Grip); var accessory = V2(f.AccessoryAnchor);
        var sourceScale = scale / f.SourceUnitScale;
        if (!Rect(r, size) || !Rect(body, size) || body.X < r.X || body.Y < r.Y || body.Z > r.Z || body.W > r.W
            || !Inside(a, r) || !Inside(grip, r) || !Inside(accessory, r)
            // Optional painted headwear may occupy reserved visual padding. The
            // measured character mass still fits its original logical body cell.
            || a.X - body.X > sourceScale * .5f || body.Z - a.X > sourceScale * .5f
            || a.Y - body.Y > sourceScale * .9f || body.W - a.Y > sourceScale * .1f
            || a.X - r.X > sourceScale * (.5f + visualPadding) || r.Z - a.X > sourceScale * (.5f + visualPadding)
            || a.Y - r.Y > sourceScale * (.9f + visualPadding) || r.W - a.Y > sourceScale * (.1f + visualPadding)) return null;
        var headWidth = f.AccessoryHeadWidth == 0 ? (body.Z - body.X) * .85f : f.AccessoryHeadWidth;
        if (!float.IsFinite(headWidth) || headWidth is < 8 or > 4096) return null;
        return new(f.File, size, r, body, a, grip, accessory, sourceScale, headWidth);
    }
    internal static bool TryParseEye(string avatar, float[]? values, Vector4 bounds, out Vector4? eye)
    {
        eye = null;
        if (avatar != "Wink") return values == null;
        if (!Numbers(values, 4)) return false;
        var e = V4(values!);
        if (e.Z <= 0 || e.W <= 0 || e.X - e.Z < bounds.X || e.X + e.Z > bounds.Z
            || e.Y - e.W < bounds.Y || e.Y + e.W > bounds.W) return false;
        eye = e;
        return true;
    }
    private static bool Text(string? s, int max) => s is { Length: > 0 } && s.Length <= max && s.All(c => char.IsAsciiLetterOrDigit(c) || c == ' ');
    private static bool Scale(float s) => float.IsFinite(s) && s is >= 64 and <= 2048;
    private static bool Numbers(float[]? a, int length) => a?.Length == length && a.All(float.IsFinite);
    private static bool Inside(Vector2 p, Vector4 r) => p.X >= r.X && p.Y >= r.Y && p.X <= r.Z && p.Y <= r.W;
    private static bool Rect(Vector4 r, Vector2 size) => r.X >= 0 && r.Y >= 0 && r.Z > r.X && r.W > r.Y && r.Z <= size.X && r.W <= size.Y;
    private static Vector2 V2(float[] a) => new(a[0], a[1]);
    private static Vector4 V4(float[] a) => new(a[0], a[1], a[2], a[3]);
    private sealed class Document { public int Version { get; set; } public Look[] Variants { get; set; } = []; public Sheet[] Sheets { get; set; } = []; public Original? OriginalOrbitCore { get; set; } }
    private sealed class Sheet { public string File { get; set; } = ""; public int Width { get; set; } public int Height { get; set; } }
    private sealed class Look { public string Avatar { get; set; } = ""; public string Choice { get; set; } = ""; public string Id { get; set; } = ""; public string Label { get; set; } = ""; public float ScaleReference { get; set; } public bool Ringless { get; set; } public float[]? EyeEllipse { get; set; } public Frame[] Frames { get; set; } = []; }
    private sealed class Original { public float ScaleReference { get; set; } public bool Ringless { get; set; } public Frame[] Frames { get; set; } = []; }
    internal sealed class Frame { public string File { get; set; } = ""; public string State { get; set; } = ""; public float[] Bounds { get; set; } = []; public float[] BodyBounds { get; set; } = []; public float[] Anchor { get; set; } = []; public float[] Grip { get; set; } = []; public float[] AccessoryAnchor { get; set; } = []; public float SourceUnitScale { get; set; } = 1; public float AccessoryHeadWidth { get; set; } }
}
