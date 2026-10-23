using System.Numerics;
using System.Text.Json;

namespace OmniKit.Ui;

internal readonly record struct CompanionSpriteRegion(Vector4 Rect, Vector2 Anchor, Vector4 Body);

internal readonly record struct CompanionRollQuad(Vector2 A, Vector2 B, Vector2 C, Vector2 D,
    Vector2 UvA, Vector2 UvB, Vector2 UvC, Vector2 UvD, Vector2 Center, float Radius)
{
    public (Vector2 Minimum, Vector2 Maximum) HitBounds => (Center - new Vector2(Radius), Center + new Vector2(Radius));
}

internal readonly record struct CompanionFloatQuad(Vector2 A, Vector2 B, Vector2 C, Vector2 D,
    Vector2 UvA, Vector2 UvB, Vector2 UvC, Vector2 UvD, Vector2 HitMinimum, Vector2 HitMaximum)
{
    public (Vector2 Minimum, Vector2 Maximum) HitBounds => (HitMinimum, HitMaximum);
}

internal sealed record CompanionSpriteAtlas(string File, Vector2 SheetSize, float SourceScale, CompanionSpriteRegion[] Frames,
    float CycleDistance = CompanionGait.DefaultCycleDistance)
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

    /// <summary>Roll the original upright texture continuously around its reviewed body circle.</summary>
    public CompanionRollQuad GetRoll(float phase, bool mirrored, Vector2 position, Vector2 cellSize)
    {
        var region = Frames[0]; var rect = region.Rect; var body = region.Body;
        var sourceCenter = new Vector2((body.X + body.Z) * .5f, (body.Y + body.W) * .5f);
        var sourceRadius = ((body.Z - body.X) + (body.W - body.Y)) * .25f;
        var scale = Math.Min(cellSize.X, cellSize.Y) / SourceScale;
        var radius = sourceRadius * scale;
        var center = position + new Vector2(cellSize.X * .5f, cellSize.Y * .9f - radius);
        phase = float.IsFinite(phase) ? phase - MathF.Floor(phase) : 0;
        var angle = phase * MathF.Tau;
        var cosine = MathF.Cos(angle); var sine = MathF.Sin(angle);
        Vector2 Point(float x, float y)
        {
            var p = (new Vector2(x, y) - sourceCenter) * scale;
            p = new Vector2(p.X * cosine - p.Y * sine, p.X * sine + p.Y * cosine);
            if (mirrored) p.X = -p.X;
            return center + p;
        }
        return new CompanionRollQuad(Point(rect.X, rect.Y), Point(rect.Z, rect.Y), Point(rect.Z, rect.W), Point(rect.X, rect.W),
            new Vector2(rect.X, rect.Y) / SheetSize, new Vector2(rect.Z, rect.Y) / SheetSize,
            new Vector2(rect.Z, rect.W) / SheetSize, new Vector2(rect.X, rect.W) / SheetSize, center, radius);
    }

    /// <summary>Float one stable image without redrawing or stretching its body and face.</summary>
    public CompanionFloatQuad GetFloat(float phase, bool mirrored, Vector2 position, Vector2 cellSize)
    {
        var region = Frames[0]; var rect = region.Rect; var body = region.Body;
        var sourceCenter = new Vector2((body.X + body.Z) * .5f, (body.Y + body.W) * .5f);
        var scale = Math.Min(cellSize.X, cellSize.Y) / SourceScale;
        var center = new Vector2(cellSize.X * .5f, cellSize.Y * .9f) + (sourceCenter - region.Anchor) * scale;
        var left = center + (new Vector2(rect.X, rect.Y) - sourceCenter) * scale;
        var right = center + (new Vector2(rect.Z, rect.W) - sourceCenter) * scale;
        var gap = Math.Max(0, Math.Min(Math.Min(left.X, left.Y), Math.Min(cellSize.X - right.X, cellSize.Y - right.Y)));
        var cornerRadius = Vector2.Max(Vector2.Abs(left - center), Vector2.Abs(right - center)).Length();
        // Limit motion by the source crop's actual spare room, so even its
        // transparent corners stay inside the user-chosen logical cell.
        var angleLimit = Math.Min(.035f, gap * .6f / Math.Max(.001f, cornerRadius));
        var bobLimit = Math.Min(Math.Min(cellSize.X, cellSize.Y) * .012f, gap * .35f);
        phase = float.IsFinite(phase) ? phase - MathF.Floor(phase) : 0;
        var angle = angleLimit * MathF.Sin(phase * MathF.Tau - .4f);
        var bob = bobLimit * MathF.Sin(phase * MathF.Tau);
        var cosine = MathF.Cos(angle); var sine = MathF.Sin(angle);
        if (mirrored) center.X = cellSize.X - center.X;
        center += position;
        Vector2 Point(float x, float y)
        {
            var p = (new Vector2(x, y) - sourceCenter) * scale;
            p = new Vector2(p.X * cosine - p.Y * sine, p.X * sine + p.Y * cosine);
            if (mirrored) p.X = -p.X;
            return center + p + new Vector2(0, bob);
        }
        var bodyHalf = new Vector2(body.Z - body.X, body.W - body.Y) * scale * .5f;
        var hitHalf = bodyHalf + new Vector2(angleLimit * bodyHalf.Length() + bobLimit);
        return new CompanionFloatQuad(Point(rect.X, rect.Y), Point(rect.Z, rect.Y), Point(rect.Z, rect.W), Point(rect.X, rect.W),
            new Vector2(rect.X, rect.Y) / SheetSize, new Vector2(rect.Z, rect.Y) / SheetSize,
            new Vector2(rect.Z, rect.W) / SheetSize, new Vector2(rect.X, rect.W) / SheetSize,
            Vector2.Max(position, center - hitHalf), Vector2.Min(position + cellSize, center + hitHalf));
    }
}

/// <summary>Reviewed art coordinates only; metadata never supplies executable behavior or paths outside assets.</summary>
internal sealed class CompanionAnimationLibrary
{
    private readonly Dictionary<string, CompanionSpriteAtlas> atlases = new(StringComparer.OrdinalIgnoreCase);
    public int Count => atlases.Count;

    public CompanionAnimationLibrary(string assetDirectory)
    {
        var path = Path.Combine(assetDirectory, "abstract-companions.frames.json");
        try
        {
            if (!System.IO.File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return;
            Load(System.IO.File.ReadAllText(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException
            or ArgumentException or KeyNotFoundException or IndexOutOfRangeException or FormatException or OverflowException)
        {
            // Missing or invalid optional art falls back to the existing sprite/vector renderer.
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
            if (character?.ToLowerInvariant() is not ("moonblob" or "wink" or "nova" or "mochi" or "puff" or "orbit" or "ember" or "prism")
                || string.IsNullOrEmpty(file) || file != Path.GetFileName(file)
                || file.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) || !file.EndsWith(".png", StringComparison.Ordinal)) continue;
            var dimensions = item.GetProperty("size");
            var sheetSize = new Vector2(dimensions[0].GetSingle(), dimensions[1].GetSingle());
            var sourceScale = item.GetProperty("commonSourceScalePx").GetSingle();
            var cycleDistance = item.TryGetProperty("cycleDistance", out var cycleNode)
                ? cycleNode.GetSingle() : CompanionGait.DefaultCycleDistance;
            if (!Finite(sheetSize) || sheetSize.X <= 0 || sheetSize.Y <= 0 || sheetSize.X > 8192 || sheetSize.Y > 8192
                || !float.IsFinite(sourceScale) || sourceScale < 64 || sourceScale > 2048
                || !float.IsFinite(cycleDistance) || cycleDistance < .1f || cycleDistance > 8f) continue;
            var framesNode = item.GetProperty("frames");
            const int frameCount = 24;
            if (framesNode.GetArrayLength() != frameCount) continue;
            var frames = new CompanionSpriteRegion[frameCount];
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
            if (valid) atlases[character + "/" + outfit]
                = new CompanionSpriteAtlas(file, sheetSize, sourceScale, frames, cycleDistance);
        }
    }

    private static Vector4 Rectangle(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle(), value[3].GetSingle());
    private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
    private static bool ValidRect(Vector4 r, Vector2 size) => float.IsFinite(r.X) && float.IsFinite(r.Y) && float.IsFinite(r.Z) && float.IsFinite(r.W)
        && r.X >= 0 && r.Y >= 0 && r.Z > r.X && r.W > r.Y && r.Z <= size.X && r.W <= size.Y;
}
