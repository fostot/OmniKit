using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

namespace OmniKit.Ui;

internal readonly record struct CompanionBlinkEye(Vector4 Ellipse, Vector4 Sample);
internal sealed record CompanionBlinkProfile(string Avatar, string NormalFile, Vector4 NormalBounds,
    string ClosedFile, Vector4 ClosedBounds, Vector2 ClosedSheetSize, IReadOnlyList<CompanionBlinkEye> Eyes);

/// <summary>Measured face regions bound to reviewed PNG bytes and the active normal/closed crop pairs.</summary>
internal sealed class CompanionBlinkLibrary
{
    public const string MetadataFile = "companion-blinks.frames.json";
    private readonly Dictionary<(string File, Vector4 Bounds), CompanionBlinkProfile> profiles = [];
    public int Count => profiles.Count;
    public bool IsAvailable => Count > 0;
    public IReadOnlyList<CompanionBlinkProfile> Profiles { get; private set; } = Array.Empty<CompanionBlinkProfile>();

    public CompanionBlinkLibrary(string assetDirectory)
    {
        try
        {
            var path = Path.Combine(assetDirectory, MetadataFile);
            if (!File.Exists(path) || new FileInfo(path).Length > 512 * 1024) return;
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
            if (document?.Version != 1 || document.Sheets is not { Length: > 0 and <= 32 }
                || document.Profiles is not { Length: > 0 and <= 112 }) return;
            var expected = ExpectedSources(assetDirectory);
            var allowed = expected.Values.SelectMany(s => new[] { s.NormalFile, s.ClosedFile }).ToHashSet(StringComparer.Ordinal);
            var sheets = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            var seenFiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sheet in document.Sheets)
            {
                if (sheet == null || !SafeFile(sheet.File) || !seenFiles.Add(sheet.File)
                    || sheet.Width is < 1 or > 8192 || sheet.Height is < 1 or > 8192) return;
                // Optional source sets may be missing or replaced. Only profiles using
                // those exact files are disabled; the other reviewed eyes keep working.
                if (allowed.Contains(sheet.File) && Matches(assetDirectory, sheet)) sheets.Add(sheet.File, new(sheet.Width, sheet.Height));
            }
            var parsed = new Dictionary<(string, Vector4), CompanionBlinkProfile>();
            foreach (var value in document.Profiles)
            {
                if (value == null || !TryRect(value.NormalBounds, out var normal) || !TryRect(value.ClosedBounds, out var closed)
                    || !expected.TryGetValue((value.NormalFile, normal), out var source)
                    || source.Avatar != value.Avatar || source.ClosedFile != value.ClosedFile || source.ClosedBounds != closed
                    || !sheets.TryGetValue(value.NormalFile, out var normalSize) || normalSize != source.NormalSheetSize
                    || !sheets.TryGetValue(value.ClosedFile, out var closedSize) || closedSize != source.ClosedSheetSize
                    || value.Eyes == null || value.Eyes.Length != (value.Avatar == "Wink" ? 1 : 2)) continue;
                var eyes = new List<CompanionBlinkEye>();
                foreach (var eye in value.Eyes)
                {
                    if (eye?.Ellipse is not { Length: 4 } ellipse || ellipse.Any(n => !float.IsFinite(n))
                        || ellipse[2] < 2 || ellipse[3] < 2 || !TryRect(eye.Sample, out var sample)) break;
                    var e = new Vector4(ellipse[0], ellipse[1], ellipse[2], ellipse[3]);
                    var eyeBounds = new Vector4(e.X - e.Z - 2, e.Y - e.W - 2, e.X + e.Z + 2, e.Y + e.W + 2);
                    if (!Inside(eyeBounds, normal) || !Inside(sample, closed) || sample.Z - sample.X < 2 || sample.W - sample.Y < 2) break;
                    eyes.Add(new(e, sample));
                }
                if (eyes.Count != value.Eyes.Length) continue;
                if (!parsed.TryAdd((value.NormalFile, normal), new(value.Avatar, value.NormalFile, normal,
                    value.ClosedFile, closed, closedSize, eyes.AsReadOnly()))) return;
            }
            foreach (var entry in parsed) profiles.Add(entry.Key, entry.Value);
            Profiles = Array.AsReadOnly(profiles.Values.ToArray());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException
            or InvalidOperationException or OverflowException) { }
    }

    public CompanionBlinkProfile? Find(string normalFile, Vector4 normalBounds)
        => profiles.GetValueOrDefault((normalFile, normalBounds));

    private static Dictionary<(string, Vector4), Source> ExpectedSources(string directory)
    {
        var result = new Dictionary<(string, Vector4), Source>();
        var originals = new CompanionAnimationLibrary(directory);
        string[] avatars = ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism"];
        foreach (var avatar in avatars)
        {
            if (avatar == "Orbit" || !originals.TryGet(avatar, "Default", out var atlas) || atlas == null) continue;
            var closed = avatar is "Moonblob" or "Wink" or "Nova" ? 11 : 10;
            result.Add((atlas.File, atlas.Frames[0].Rect), new(avatar, atlas.File, atlas.Frames[0].Rect, atlas.SheetSize,
                atlas.File, atlas.Frames[closed].Rect, atlas.SheetSize));
        }
        var variants = new CompanionVariantLibrary(directory);
        if (variants.OriginalOrbitCore.Count == 16) Add("Orbit", variants.OriginalOrbitCore[0], variants.OriginalOrbitCore[2]);
        foreach (var look in variants.Looks.Concat(new CompanionHolidayLibrary(directory).Looks))
            Add(look.Avatar, look.Normal, look[CompanionVariantPose.ClosedBlink]);
        return result;

        void Add(string avatar, CompanionVariantFrame normal, CompanionVariantFrame closed)
            => result.Add((normal.File, normal.Bounds), new(avatar, normal.File, normal.Bounds, normal.SheetSize,
                closed.File, closed.Bounds, closed.SheetSize));
    }

    private static bool Matches(string directory, Sheet sheet)
    {
        if (sheet.SHA256 is not { Length: 64 } || sheet.SHA256.Any(c => !char.IsAsciiHexDigit(c))) return false;
        var file = new FileInfo(Path.Combine(directory, sheet.File));
        if (!file.Exists || file.Length is < 24 or > 64 * 1024 * 1024) return false;
        using var stream = file.OpenRead(); Span<byte> header = stackalloc byte[24];
        stream.ReadExactly(header);
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!header[..8].SequenceEqual(signature) || BinaryPrimitives.ReadInt32BigEndian(header[16..20]) != sheet.Width
            || BinaryPrimitives.ReadInt32BigEndian(header[20..24]) != sheet.Height) return false;
        stream.Position = 0;
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(sheet.SHA256, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SafeFile(string value) => !string.IsNullOrEmpty(value) && value.Length <= 80
        && value.EndsWith(".png", StringComparison.Ordinal) && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_')
        && value == Path.GetFileName(value);
    private static bool TryRect(float[]? values, out Vector4 rect)
    {
        rect = default;
        if (values is not { Length: 4 } || values.Any(n => !float.IsFinite(n)) || values[0] < 0 || values[1] < 0
            || values[2] <= values[0] || values[3] <= values[1]) return false;
        rect = new(values[0], values[1], values[2], values[3]); return true;
    }
    private static bool Inside(Vector4 inner, Vector4 outer) => inner.X >= outer.X && inner.Y >= outer.Y && inner.Z <= outer.Z && inner.W <= outer.W;
    private sealed record Source(string Avatar, string NormalFile, Vector4 NormalBounds, Vector2 NormalSheetSize,
        string ClosedFile, Vector4 ClosedBounds, Vector2 ClosedSheetSize);
    private sealed class Document { public int Version { get; set; } public Sheet[] Sheets { get; set; } = []; public Profile[] Profiles { get; set; } = []; }
    private sealed class Sheet { public string File { get; set; } = ""; public int Width { get; set; } public int Height { get; set; } public string SHA256 { get; set; } = ""; }
    private sealed class Profile
    {
        public string Avatar { get; set; } = ""; public string NormalFile { get; set; } = ""; public float[] NormalBounds { get; set; } = [];
        public string ClosedFile { get; set; } = ""; public float[] ClosedBounds { get; set; } = []; public Eye[] Eyes { get; set; } = [];
    }
    private sealed class Eye { public float[] Ellipse { get; set; } = []; public float[] Sample { get; set; } = []; }
}
