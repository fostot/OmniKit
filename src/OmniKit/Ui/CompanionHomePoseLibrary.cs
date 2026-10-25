using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;

namespace OmniKit.Ui;

internal enum CompanionHomePose { TurnBack, RearCrouch, RearClimbA, RearClimbB }

internal sealed record CompanionHomePoseFrame(string File, Vector2 SheetSize, Vector4 Bounds,
    Vector4 BodyBounds, Vector2 Anchor, float ScaleReference)
{
    public CompanionFrame Place(Vector2 support, Vector2 spriteSize, float scale, bool mirrored)
    {
        var minimum = new Vector2(Bounds.X, Bounds.Y); var maximum = new Vector2(Bounds.Z, Bounds.W);
        var factor = spriteSize.Y * scale / ScaleReference;
        var size = (maximum - minimum) * factor;
        var offset = (Anchor - minimum) * factor;
        if (mirrored) offset.X = size.X - offset.X;
        var uvStart = minimum / SheetSize; var uvEnd = maximum / SheetSize;
        if (mirrored) (uvStart.X, uvEnd.X) = (uvEnd.X, uvStart.X);
        return new(uvStart, uvEnd, support - offset, size);
    }
}

/// <summary>Optional complete rear artwork. Exact identity matching never substitutes another material.</summary>
internal sealed class CompanionHomePoseLibrary
{
    public const string MetadataFile = "companion-homeposes.frames.json";
    private static readonly string[] OriginalAvatars = ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism"];
    private static readonly HashSet<string> AllowedFiles = new(OriginalAvatars.SelectMany(avatar => new[]
        { $"companion-homeposes-{avatar.ToLowerInvariant()}-a.png", $"companion-homeposes-{avatar.ToLowerInvariant()}-b.png" })
        .Append("companion-homeposes-newcomers.png"), StringComparer.Ordinal);
    private Dictionary<(string Avatar, string Id), CompanionHomePoseFrame[]> looks = [];
    public bool IsAvailable => looks.Count == 117;
    public CompanionHomePoseFrame? Find(string avatar, string? id, CompanionHomePose pose)
        => Enum.IsDefined(pose) && looks.TryGetValue((avatar, id ?? "Default"), out var frames) ? frames[(int)pose] : null;

    public CompanionHomePoseLibrary(string directory)
    {
        try
        {
            var path = Path.Combine(directory, MetadataFile);
            if (!File.Exists(path) || new FileInfo(path).Length > 512 * 1024) return;
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), new JsonSerializerOptions { MaxDepth = 16 });
            if (document?.Version != 1 || document.Sheets is not { Length: 17 } || document.Looks is not { Length: 117 }) return;
            var sheets = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            foreach (var sheet in document.Sheets)
                if (sheet == null || !AllowedFiles.Contains(sheet.File) || sheet.Width is < 1 or > 8192 || sheet.Height is < 1 or > 8192
                    || !sheets.TryAdd(sheet.File, new(sheet.Width, sheet.Height)) || !Matches(directory, sheet)) return;
            var parsed = new Dictionary<(string Avatar, string Id), CompanionHomePoseFrame[]>();
            foreach (var look in document.Looks)
            {
                if (look == null || !Configuration.AvatarNames.Contains(look.Avatar) || string.IsNullOrWhiteSpace(look.Id)
                    || look.Id.Length > 80 || look.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))
                    || !float.IsFinite(look.ScaleReference) || look.ScaleReference is < 1 or > 8192 || look.Frames is not { Length: 4 }) return;
                var frames = new CompanionHomePoseFrame[4];
                for (var index = 0; index < frames.Length; index++)
                {
                    var frame = look.Frames[index];
                    if (frame == null || frame.State != ((CompanionHomePose)index).ToString() || frame.SourceUnitScale != 1
                        || !sheets.TryGetValue(frame.File, out var sheetSize) || !FileForAvatar(frame.File, look.Avatar)
                        || !Rect(frame.Bounds, sheetSize) || !Rect(frame.BodyBounds, sheetSize)
                        || frame.Anchor is not { Length: 2 } || !frame.Anchor.All(float.IsFinite)) return;
                    var bounds = frame.Bounds; var body = frame.BodyBounds;
                    if (body[0] < bounds[0] || body[1] < bounds[1] || body[2] > bounds[2] || body[3] > bounds[3]
                        || frame.Anchor[0] < bounds[0] || frame.Anchor[0] > bounds[2] || frame.Anchor[1] < bounds[1] || frame.Anchor[1] > bounds[3]) return;
                    var reference = look.ScaleReference;
                    var bodyWidth = body[2] - body[0]; var bodyHeight = body[3] - body[1];
                    if (bounds[2] - bounds[0] > reference * 1.4f || bounds[3] - bounds[1] > reference * 1.6f
                        || bodyWidth < reference * .1f || bodyHeight < reference * .1f
                        || Math.Abs(frame.Anchor[0] - (body[0] + body[2]) * .5f) > bodyWidth * .3f
                        || frame.Anchor[1] < body[1] + bodyHeight * .7f || frame.Anchor[1] > body[3] + reference * .08f) return;
                    frames[index] = new(frame.File, sheetSize, new(bounds[0], bounds[1], bounds[2], bounds[3]),
                        new(body[0], body[1], body[2], body[3]), new(frame.Anchor[0], frame.Anchor[1]), look.ScaleReference);
                }
                if (!parsed.TryAdd((look.Avatar, look.Id), frames)) return;
            }
            foreach (var avatar in Configuration.AvatarNames)
                if (!parsed.ContainsKey((avatar, "Default")) || parsed.Keys.Count(key => key.Avatar == avatar) != (OriginalAvatars.Contains(avatar) ? 14 : 1)) return;
            looks = parsed;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
    }

    private static bool FileForAvatar(string file, string avatar) => OriginalAvatars.Contains(avatar)
        ? file == $"companion-homeposes-{avatar.ToLowerInvariant()}-a.png" || file == $"companion-homeposes-{avatar.ToLowerInvariant()}-b.png"
        : file == "companion-homeposes-newcomers.png";
    private static bool Rect(float[]? value, Vector2 sheet) => value is { Length: 4 } && value.All(float.IsFinite)
        && value[0] >= 0 && value[1] >= 0 && value[2] > value[0] && value[3] > value[1] && value[2] <= sheet.X && value[3] <= sheet.Y;
    private static bool Matches(string directory, Sheet sheet)
    {
        using var stream = File.OpenRead(Path.Combine(directory, sheet.File));
        if (stream.Length is < 24 or > 128 * 1024 * 1024) return false;
        Span<byte> header = stackalloc byte[24]; stream.ReadExactly(header);
        return header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && header.Slice(12, 4).SequenceEqual("IHDR"u8)
            && BinaryPrimitives.ReadInt32BigEndian(header[16..20]) == sheet.Width
            && BinaryPrimitives.ReadInt32BigEndian(header[20..24]) == sheet.Height;
    }
    private sealed class Document { public int Version { get; set; } public Sheet[] Sheets { get; set; } = []; public Look[] Looks { get; set; } = []; }
    private sealed class Sheet { public string File { get; set; } = ""; public int Width { get; set; } public int Height { get; set; } }
    private sealed class Look { public string Avatar { get; set; } = ""; public string Id { get; set; } = ""; public float ScaleReference { get; set; } public Frame[] Frames { get; set; } = []; }
    private sealed class Frame { public string State { get; set; } = ""; public string File { get; set; } = ""; public float[] Bounds { get; set; } = []; public float[] BodyBounds { get; set; } = []; public float[] Anchor { get; set; } = []; public float SourceUnitScale { get; set; } = 1; }
}
