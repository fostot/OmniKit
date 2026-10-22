using System.Numerics;
using System.Text.Json;

namespace OmniKit.Ui;

internal readonly record struct CompanionSpriteRegion(Vector4 Rect, Vector2 Anchor, Vector4 Body);

internal sealed record CompanionSpriteAtlas(string File, Vector2 SheetSize, float SourceScale, CompanionSpriteRegion[] Frames)
{
    public CompanionFrame GetFrame(int index, bool mirrored, Vector2 position, Vector2 cellSize)
    {
        var region = Frames[Math.Clamp(index, 0, Frames.Length - 1)];
        var rect = region.Rect;
        var uvStart = new Vector2(rect.X, rect.Y) / SheetSize;
        var uvEnd = new Vector2(rect.Z, rect.W) / SheetSize;
        if (mirrored) (uvStart.X, uvEnd.X) = (uvEnd.X, uvStart.X);
        var scale = cellSize / SourceScale;
        var anchorOffset = new Vector2(mirrored ? rect.Z - region.Anchor.X : region.Anchor.X - rect.X, region.Anchor.Y - rect.Y);
        return new CompanionFrame(uvStart, uvEnd, position + new Vector2(cellSize.X * .5f, cellSize.Y * .9f) - anchorOffset * scale,
            new Vector2(rect.Z - rect.X, rect.W - rect.Y) * scale);
    }

    public (Vector2 Minimum, Vector2 Maximum) HitBounds(int index, bool mirrored, Vector2 position, Vector2 cellSize)
    {
        var region = Frames[Math.Clamp(index, 0, Frames.Length - 1)];
        var body = region.Body;
        var scale = cellSize / SourceScale;
        var origin = position + new Vector2(cellSize.X * .5f, cellSize.Y * .9f);
        var left = mirrored ? region.Anchor.X - body.Z : body.X - region.Anchor.X;
        var right = mirrored ? region.Anchor.X - body.X : body.Z - region.Anchor.X;
        return (origin + new Vector2(left, body.Y - region.Anchor.Y) * scale,
            origin + new Vector2(right, body.W - region.Anchor.Y) * scale);
    }
}

/// <summary>Reviewed art coordinates only; metadata never supplies executable behavior or paths outside assets.</summary>
internal sealed class CompanionAnimationLibrary
{
    private readonly Dictionary<string, CompanionSpriteAtlas> atlases = new(StringComparer.OrdinalIgnoreCase);
    public int Count => atlases.Count;

    public CompanionAnimationLibrary(string assetDirectory)
    {
        foreach (var name in new[] { "abstract-companions.frames.json", "lunar-companions.frames.json" })
        {
            var path = Path.Combine(assetDirectory, name);
            try
            {
                if (!System.IO.File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) continue;
                Load(System.IO.File.ReadAllText(path));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException
                or ArgumentException or KeyNotFoundException or IndexOutOfRangeException or FormatException or OverflowException)
            {
                // Missing or invalid optional art falls back to the existing sprite/vector renderer.
            }
        }
    }

    public bool TryGet(string avatar, string outfit, out CompanionSpriteAtlas? atlas)
        => atlases.TryGetValue(avatar + "/" + outfit, out atlas) || atlases.TryGetValue(avatar + "/*", out atlas);

    private void Load(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1) return;
        foreach (var item in root.GetProperty("atlases").EnumerateArray())
        {
            var character = item.GetProperty("character").GetString();
            var outfit = item.TryGetProperty("outfit", out var outfitNode) ? outfitNode.GetString() : "*";
            var file = item.GetProperty("file").GetString();
            if (string.IsNullOrEmpty(character) || string.IsNullOrEmpty(file) || file != Path.GetFileName(file)
                || file.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) || !file.EndsWith(".png", StringComparison.Ordinal)) continue;
            var dimensions = item.GetProperty("size");
            var sheetSize = new Vector2(dimensions[0].GetSingle(), dimensions[1].GetSingle());
            var sourceScale = item.GetProperty("commonSourceScalePx").GetSingle();
            if (!Finite(sheetSize) || sheetSize.X <= 0 || sheetSize.Y <= 0 || sheetSize.X > 8192 || sheetSize.Y > 8192
                || !float.IsFinite(sourceScale) || sourceScale < 64 || sourceScale > 2048) continue;
            var framesNode = item.GetProperty("frames");
            if (framesNode.GetArrayLength() != 24) continue;
            var frames = new CompanionSpriteRegion[24];
            var valid = true;
            var index = 0;
            foreach (var frame in framesNode.EnumerateArray())
            {
                var rect = Rectangle(frame.GetProperty("rect"));
                var anchorNode = frame.GetProperty("anchor");
                var anchor = new Vector2(anchorNode[0].GetSingle(), anchorNode[1].GetSingle());
                var body = frame.TryGetProperty("bodyBounds96", out var bodyNode) ? Rectangle(bodyNode) : rect;
                if (frame.GetProperty("index").GetInt32() != index || !ValidRect(rect, sheetSize) || !ValidRect(body, sheetSize) || !Finite(anchor)
                    || body.X < rect.X || body.Y < rect.Y || body.Z > rect.Z || body.W > rect.W
                    || anchor.X < rect.X || anchor.X > rect.Z || anchor.Y < rect.Y || anchor.Y > rect.W
                    || anchor.X - rect.X > sourceScale * .5f || rect.Z - anchor.X > sourceScale * .5f
                    || anchor.Y - rect.Y > sourceScale * .9f || rect.W - anchor.Y > sourceScale * .1f) { valid = false; break; }
                frames[index++] = new CompanionSpriteRegion(rect, anchor, body);
            }
            if (valid) atlases[character + "/" + outfit] = new CompanionSpriteAtlas(file, sheetSize, sourceScale, frames);
        }
    }

    private static Vector4 Rectangle(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle(), value[3].GetSingle());
    private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
    private static bool ValidRect(Vector4 r, Vector2 size) => float.IsFinite(r.X) && float.IsFinite(r.Y) && float.IsFinite(r.Z) && float.IsFinite(r.W)
        && r.X >= 0 && r.Y >= 0 && r.Z > r.X && r.W > r.Y && r.Z <= size.X && r.W <= size.Y;
}
