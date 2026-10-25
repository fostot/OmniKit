using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

namespace OmniKit.Ui;

/// <summary>Complete painted costumes and five new default characters, independent of existing artwork sets.</summary>
internal sealed class CompanionWardrobeLibrary
{
    public const string MetadataFile = "companion-wardrobe.frames.json";
    public const float VisualPaddingFraction = .5f;
    private static readonly string[] NewAvatars = ["Nib", "Echo", "Fizz", "Glim", "Rune"];
    private static readonly string[] Avatars = ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism", .. NewAvatars];
    private static readonly SeasonalCompanionTheme[] Costumes = [SeasonalCompanionTheme.AllSaintsWake,
        SeasonalCompanionTheme.Starlight, SeasonalCompanionTheme.MakeItRain, SeasonalCompanionTheme.MoonfireFaire];
    private static readonly HashSet<string> AllowedFiles = new(Avatars.Select(FileFor), StringComparer.Ordinal);
    private readonly Dictionary<string, CompanionVariantLook> defaults = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Avatar, SeasonalCompanionTheme Theme), CompanionVariantLook> costumes = [];
    private readonly Dictionary<(string File, Vector4 Bounds), CompanionBlinkProfile> blinks = [];
    public bool IsAvailable => defaults.Count == 13 && costumes.Count == 52;
    public IReadOnlyList<CompanionVariantLook> Looks { get; private set; } = Array.Empty<CompanionVariantLook>();
    public IReadOnlyList<CompanionBlinkProfile> BlinkProfiles { get; private set; } = Array.Empty<CompanionBlinkProfile>();
    public IReadOnlyList<string> Files { get; private set; } = Array.Empty<string>();

    public CompanionWardrobeLibrary(string assetDirectory)
    {
        try
        {
            var path = Path.Combine(assetDirectory, MetadataFile);
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return;
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
            if (document?.Version != 1 || document.Sheets is not { Length: 13 } || document.Looks is not { Length: 65 }) return;
            var sheets = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            foreach (var sheet in document.Sheets)
            {
                if (sheet == null || !AllowedFiles.Contains(sheet.File) || sheet.Width is < 1 or > 8192 || sheet.Height is < 1 or > 8192
                    || !sheets.TryAdd(sheet.File, new(sheet.Width, sheet.Height)) || !Matches(assetDirectory, sheet)) return;
            }
            var parsed = new Dictionary<(string Avatar, SeasonalCompanionTheme? Theme), CompanionVariantLook>();
            var parsedBlinks = new Dictionary<(string, Vector4), CompanionBlinkProfile>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in document.Looks)
            {
                if (value == null || !Avatars.Contains(value.Avatar) || !Text(value.Id, true) || !ids.Add(value.Id)
                    || !Text(value.Label, false) || value.Frames is not { Length: 8 }
                    || value.Eyes == null || value.Eyes.Length != (value.Avatar == "Wink" ? 1 : 2)) return;
                SeasonalCompanionTheme? costume = null;
                if (value.Costume != "None")
                {
                    if (!Enum.TryParse<SeasonalCompanionTheme>(value.Costume, out var theme) || !Costumes.Contains(theme)
                        || value.Costume != theme.ToString()) return;
                    costume = theme;
                }
                var frames = new List<CompanionVariantFrame>(8);
                for (var i = 0; i < value.Frames.Length; i++)
                {
                    var source = value.Frames[i];
                    if (source == null || source.State != ((CompanionVariantPose)i).ToString() || source.File != FileFor(value.Avatar)
                        || source.SourceUnitScale != 1 || source.AccessoryHeadWidth <= 0) return;
                    var frame = CompanionVariantLibrary.ParseFrame(source, value.ScaleReference, sheets, VisualPaddingFraction);
                    if (frame == null || i > 0 && Math.Abs(frame.AccessoryHeadWidth - frames[0].AccessoryHeadWidth) > .01f) return;
                    frames.Add(frame);
                }
                var normal = frames[0]; var closed = frames[(int)CompanionVariantPose.ClosedBlink];
                var eyes = new List<CompanionBlinkEye>(value.Eyes.Length);
                foreach (var eye in value.Eyes)
                {
                    if (eye == null || !Numbers(eye.Ellipse) || !Numbers(eye.Sample)) return;
                    var ellipse = V4(eye.Ellipse); var sample = V4(eye.Sample);
                    if (ellipse.Z < 2 || ellipse.W < 2 || !Inside(new(ellipse.X - ellipse.Z - 2, ellipse.Y - ellipse.W - 2,
                        ellipse.X + ellipse.Z + 2, ellipse.Y + ellipse.W + 2), normal.Bounds)
                        || sample.Z - sample.X < 2 || sample.W - sample.Y < 2 || !Inside(sample, closed.Bounds)) return;
                    eyes.Add(new(ellipse, sample));
                }
                var choice = costume is { } selected ? "Costume" + selected : "Default";
                if (!parsed.TryAdd((value.Avatar, costume), new(value.Avatar, choice, value.Id, value.Label, value.ScaleReference,
                        frames.AsReadOnly(), value.Avatar == "Wink" ? eyes[0].Ellipse : null, value.Avatar == "Orbit"))
                    || !parsedBlinks.TryAdd((normal.File, normal.Bounds), new(value.Avatar, normal.File, normal.Bounds,
                        closed.File, closed.Bounds, closed.SheetSize, eyes.AsReadOnly()))) return;
            }
            // Publish no partial roster or blink profiles: a missing/mismatched file,
            // duplicate character or invalid pose leaves the existing packs untouched.
            foreach (var avatar in Avatars)
            foreach (var costume in Costumes)
                if (!parsed.ContainsKey((avatar, costume))) return;
            foreach (var avatar in Avatars)
                if (!parsed.ContainsKey((avatar, null))) return;
            foreach (var pair in parsed)
                if (pair.Key.Theme is { } theme) costumes.Add((pair.Key.Avatar, theme), pair.Value);
                else defaults.Add(pair.Key.Avatar, pair.Value);
            foreach (var pair in parsedBlinks) blinks.Add(pair.Key, pair.Value);
            Looks = Array.AsReadOnly(Avatars.Select(avatar => defaults[avatar])
                .Concat(Avatars.SelectMany(avatar => Costumes.Select(theme => costumes[(avatar, theme)]))).ToArray());
            BlinkProfiles = Array.AsReadOnly(Looks.Select(look => parsedBlinks[(look.Normal.File, look.Normal.Bounds)]).ToArray());
            Files = Array.AsReadOnly(Avatars.Select(FileFor).ToArray());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException
            or InvalidOperationException or OverflowException) { }
    }

    public CompanionVariantLook? FindBare(string avatar) => defaults.GetValueOrDefault(avatar);
    public CompanionVariantLook? FindDefault(string avatar) => NewAvatars.Contains(avatar) ? FindBare(avatar) : null;
    public CompanionVariantLook? FindCostume(string avatar, SeasonalCompanionTheme theme) => costumes.GetValueOrDefault((avatar, theme));
    public CompanionBlinkProfile? FindBlink(string normalFile, Vector4 normalBounds) => blinks.GetValueOrDefault((normalFile, normalBounds));

    private static bool Matches(string directory, Sheet sheet)
    {
        if (sheet.SHA256 is not { Length: 64 } || sheet.SHA256.Any(c => !char.IsAsciiHexDigit(c))) return false;
        var file = new FileInfo(Path.Combine(directory, sheet.File));
        if (!file.Exists || file.Length is < 24 or > 64 * 1024 * 1024) return false;
        using var stream = file.OpenRead(); Span<byte> header = stackalloc byte[24]; stream.ReadExactly(header);
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!header[..8].SequenceEqual(signature) || BinaryPrimitives.ReadUInt32BigEndian(header[8..12]) != 13
            || !header[12..16].SequenceEqual("IHDR"u8) || BinaryPrimitives.ReadInt32BigEndian(header[16..20]) != sheet.Width
            || BinaryPrimitives.ReadInt32BigEndian(header[20..24]) != sheet.Height) return false;
        stream.Position = 0;
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(sheet.SHA256, StringComparison.OrdinalIgnoreCase);
    }
    private static bool Text(string? value, bool identifier) => value is { Length: > 0 and <= 64 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' || !identifier && c is ' ' or '\'');
    private static bool Numbers(float[]? values) => values is { Length: 4 } && values.All(float.IsFinite);
    private static Vector4 V4(float[] values) => new(values[0], values[1], values[2], values[3]);
    private static bool Inside(Vector4 inner, Vector4 outer) => inner.X >= outer.X && inner.Y >= outer.Y
        && inner.Z <= outer.Z && inner.W <= outer.W;
    private static string FileFor(string avatar) => avatar.ToLowerInvariant() + "-wardrobe.png";
    private sealed class Document { public int Version { get; set; } public Sheet[] Sheets { get; set; } = []; public Look[] Looks { get; set; } = []; }
    private sealed class Sheet { public string File { get; set; } = ""; public int Width { get; set; } public int Height { get; set; } public string SHA256 { get; set; } = ""; }
    private sealed class Look
    {
        public string Avatar { get; set; } = "";
        public string Costume { get; set; } = "";
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public float ScaleReference { get; set; }
        public CompanionVariantLibrary.Frame[] Frames { get; set; } = [];
        public Eye[] Eyes { get; set; } = [];
    }
    private sealed class Eye { public float[] Ellipse { get; set; } = []; public float[] Sample { get; set; } = []; }
}
