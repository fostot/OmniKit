using System.Numerics;
using System.Text.Json;

namespace OmniKit.Ui;

internal sealed class CompanionNapArtwork
{
    private readonly NapSheet[] sheets = [];
    public CompanionNapArtwork(string directory)
    {
        try
        {
            var path = Path.Combine(directory, "companion-naps.frames.json");
            if (new FileInfo(path).Length > 64 * 1024) return;
            var parsed = JsonSerializer.Deserialize<NapDocument>(File.ReadAllText(path));
            if (parsed?.Version != 1 || parsed.Atlases is not { Length: 8 } || !parsed.Atlases.All(s => s != null && Valid(s))) return;
            sheets = parsed.Atlases;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
    }

    public bool TryGet(string avatar, string outfit, out NapSheet? sheet)
    {
        sheet = sheets.FirstOrDefault(s => s.Avatar == avatar && (s.Outfit == outfit || s.Outfit == "Default"));
        return sheet != null;
    }

    private static bool Valid(NapSheet s) => Configuration.AvatarNames.Contains(s.Avatar)
        && (Configuration.OutfitNames.Contains(s.Outfit) || s.Outfit == "Default")
        && s.File is "companion-naps-abstract.png" or "mochi-poses.png" or "puff-poses.png"
            or "orbit-poses.png" or "ember-poses.png" or "prism-poses.png"
        && s.Width is > 0 and <= 8192 && s.Height is > 0 and <= 8192
        && float.IsFinite(s.ScaleReference) && s.ScaleReference > 0 && s.Frames is { Length: 4 }
        && s.Frames.All(f => f != null && f.Bounds is { Length: 4 } && f.Anchor is { Length: 2 }
            && f.Bounds.All(float.IsFinite) && f.Anchor.All(float.IsFinite)
            && f.Bounds[0] >= 0 && f.Bounds[1] >= 0 && f.Bounds[2] <= s.Width && f.Bounds[3] <= s.Height
            && f.Bounds[2] > f.Bounds[0] && f.Bounds[3] > f.Bounds[1]
            && f.Anchor[0] >= f.Bounds[0] && f.Anchor[0] <= f.Bounds[2]
            && f.Anchor[1] >= f.Bounds[1] && f.Anchor[1] <= f.Bounds[3]);
}

internal sealed class NapDocument
{
    public int Version { get; set; }
    public NapSheet[] Atlases { get; set; } = [];
}

internal sealed class NapSheet
{
    public string Avatar { get; set; } = string.Empty;
    public string Outfit { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public float ScaleReference { get; set; }
    public NapCell[] Frames { get; set; } = [];

    public CompanionFrame Frame(int index, Vector2 position, Vector2 spriteSize, float scale, bool facingLeft)
    {
        var cell = Frames[Math.Clamp(index, 0, 3)];
        var sourceMinimum = new Vector2(cell.Bounds[0], cell.Bounds[1]);
        var sourceMaximum = new Vector2(cell.Bounds[2], cell.Bounds[3]);
        var anchor = new Vector2(cell.Anchor[0], cell.Anchor[1]);
        var factor = spriteSize.Y * scale / ScaleReference;
        var size = (sourceMaximum - sourceMinimum) * factor;
        var offset = (anchor - sourceMinimum) * factor;
        if (facingLeft) offset.X = size.X - offset.X;
        var minimum = position + spriteSize * new Vector2(.5f, .9f) - offset;
        var uvMinimum = sourceMinimum / new Vector2(Width, Height);
        var uvMaximum = sourceMaximum / new Vector2(Width, Height);
        if (facingLeft) (uvMinimum.X, uvMaximum.X) = (uvMaximum.X, uvMinimum.X);
        return new CompanionFrame(uvMinimum, uvMaximum, minimum, size);
    }
}

internal sealed class NapCell
{
    public float[] Bounds { get; set; } = [];
    public float[] Anchor { get; set; } = [];
}
