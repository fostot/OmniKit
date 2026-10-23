using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;

namespace OmniKit.Ui;

/// <summary>Optional complete holiday artwork set. Failure leaves the independent base/variant libraries untouched.</summary>
internal sealed class CompanionHolidayLibrary
{
    public const string MetadataFile = "companion-holiday-bodies.frames.json";
    private static readonly string[] Avatars = ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism"];
    private static readonly HashSet<string> AllowedFiles = new(Avatars.Select(FileFor), StringComparer.Ordinal);
    private readonly Dictionary<(string Avatar, SeasonalCompanionTheme Theme), CompanionVariantLook> lookup = [];
    public IReadOnlyList<CompanionVariantLook> Looks { get; private set; } = Array.Empty<CompanionVariantLook>();
    public IReadOnlyList<string> Files { get; private set; } = Array.Empty<string>();
    public bool IsAvailable => Looks.Count == 72;

    public CompanionHolidayLibrary(string assetDirectory)
    {
        try
        {
            var path = Path.Combine(assetDirectory, MetadataFile);
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return;
            var data = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
            if (data?.Version != 1 || data.Sheets is not { Length: 8 } || data.Looks is not { Length: 72 }) return;
            var sheets = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            foreach (var sheet in data.Sheets)
            {
                if (sheet == null || !AllowedFiles.Contains(sheet.File) || sheet.Width is < 1 or > 8192
                    || sheet.Height is < 1 or > 8192 || !sheets.TryAdd(sheet.File, new(sheet.Width, sheet.Height))
                    || !PngDimensionsMatch(Path.Combine(assetDirectory, sheet.File), sheet.Width, sheet.Height)) return;
            }
            var parsed = new Dictionary<(string, SeasonalCompanionTheme), CompanionVariantLook>();
            foreach (var look in data.Looks)
            {
                if (look == null || !Avatars.Contains(look.Avatar)
                    || !Enum.TryParse<SeasonalCompanionTheme>(look.Theme, out var theme) || !Enum.IsDefined(theme)
                    || look.Theme != theme.ToString() || !Text(look.Id, true) || !Text(look.Label, false)
                    || look.Ringless != (look.Avatar == "Orbit") || look.Frames is not { Length: 8 }) return;
                var frames = new List<CompanionVariantFrame>(8);
                for (var i = 0; i < look.Frames.Length; i++)
                {
                    var value = look.Frames[i];
                    if (value?.State != ((CompanionVariantPose)i).ToString() || value.File != FileFor(look.Avatar)
                        || value.SourceUnitScale != 1 || value.AccessoryHeadWidth <= 0) return;
                    var frame = CompanionVariantLibrary.ParseFrame(value, look.ScaleReference, sheets);
                    if (frame == null || i > 0 && Math.Abs(frame.AccessoryHeadWidth - frames[0].AccessoryHeadWidth) > .01f) return;
                    frames.Add(frame);
                }
                if (!CompanionVariantLibrary.TryParseEye(look.Avatar, look.EyeEllipse, frames[0].Bounds, out var eye)
                    || !parsed.TryAdd((look.Avatar, theme), new(look.Avatar, "Holiday" + theme, look.Id, look.Label,
                        look.ScaleReference, frames.AsReadOnly(), eye, look.Ringless))) return;
            }
            // Only publish state once all 8 avatars x 9 canonical themes and their files have passed validation.
            foreach (var pair in parsed) lookup.Add(pair.Key, pair.Value);
            Looks = Array.AsReadOnly(parsed.Values.ToArray());
            Files = Array.AsReadOnly(sheets.Keys.ToArray());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
    }

    public CompanionVariantLook? FindHoliday(string avatar, SeasonalCompanionTheme theme)
        => lookup.GetValueOrDefault((avatar, theme));

    private static string FileFor(string avatar) => avatar.ToLowerInvariant() + "-holiday-bodies.png";
    private static bool Text(string? value, bool identifier) => value is { Length: > 0 and <= 64 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' || !identifier && c is ' ' or '\'');
    private static bool PngDimensionsMatch(string path, int width, int height)
    {
        using var file = File.OpenRead(path);
        Span<byte> header = stackalloc byte[24];
        file.ReadExactly(header);
        return header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && BinaryPrimitives.ReadUInt32BigEndian(header[8..12]) == 13 && header[12..16].SequenceEqual("IHDR"u8)
            && BinaryPrimitives.ReadUInt32BigEndian(header[16..20]) == width
            && BinaryPrimitives.ReadUInt32BigEndian(header[20..24]) == height;
    }
    private sealed class Document { public int Version { get; set; } public Sheet[] Sheets { get; set; } = []; public Look[] Looks { get; set; } = []; }
    private sealed class Sheet { public string File { get; set; } = ""; public int Width { get; set; } public int Height { get; set; } }
    private sealed class Look
    {
        public string Avatar { get; set; } = "";
        public string Theme { get; set; } = "";
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public float ScaleReference { get; set; }
        public bool Ringless { get; set; }
        public float[]? EyeEllipse { get; set; }
        public CompanionVariantLibrary.Frame[] Frames { get; set; } = [];
    }
}
