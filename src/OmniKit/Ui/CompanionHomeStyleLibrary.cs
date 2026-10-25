using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

namespace OmniKit.Ui;

internal readonly record struct CompanionStyleQuad(Vector2 A, Vector2 B, Vector2 C, Vector2 D,
    Vector2 UvA, Vector2 UvB, Vector2 UvC, Vector2 UvD)
{
    public Vector2 Minimum => Vector2.Min(Vector2.Min(A, B), Vector2.Min(C, D));
    public Vector2 Maximum => Vector2.Max(Vector2.Max(A, B), Vector2.Max(C, D));
}

internal readonly record struct CompanionHomeStyleFrame(CompanionHomeStyle Style, Vector2 Minimum, Vector2 Maximum, bool Mirrored)
{
    public string File => Style.File;
    public Vector2 Size => Maximum - Minimum;
    public Vector2 At(Vector2 point) => Minimum + Size * new Vector2(Mirrored ? 1 - point.X : point.X, point.Y);
    public Vector2 Bed => At(Style.BedAnchor);
    public Vector2 Threshold => At(Style.Threshold);
    public Vector2 Chimney => At(Style.Chimney);
    public Vector2 OpeningMinimum => Vector2.Min(At(new(Style.Opening.X, Style.Opening.Y)), At(new(Style.Opening.Z, Style.Opening.W)));
    public Vector2 OpeningMaximum => Vector2.Max(At(new(Style.Opening.X, Style.Opening.Y)), At(new(Style.Opening.Z, Style.Opening.W)));
    public Vector2 BedPosition(Vector2 spriteSize) => Bed - spriteSize * new Vector2(.5f, .9f);
    public Vector2 OutsidePosition(Vector2 spriteSize, float clearance = 4, bool grounded = false)
        => new(Threshold.X - spriteSize.X * .5f, Maximum.Y + clearance - (grounded ? spriteSize.Y * .9f : 0));
    public int DoorVertexCount => Style.DoorPolygon.Length;
    public Vector2 DoorVertex(int index) => At(Style.DoorPolygon[index]);
    public CompanionStyleQuad Image => Transform(new(Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY));
    public int WallCount => Style.Walls.Length;
    public CompanionStyleQuad Wall(int index) => Transform(Style.Walls[index]);

    private CompanionStyleQuad Transform(CompanionHomeStyle.Shape shape)
    {
        var uvMin = new Vector2(Style.Uv.X, Style.Uv.Y);
        var uvSize = new Vector2(Style.Uv.Z - Style.Uv.X, Style.Uv.W - Style.Uv.Y);
        Vector2 Uv(Vector2 p) => uvMin + uvSize * p;
        return Mirrored
            ? new(At(shape.B), At(shape.A), At(shape.D), At(shape.C), Uv(shape.B), Uv(shape.A), Uv(shape.D), Uv(shape.C))
            : new(At(shape.A), At(shape.B), At(shape.C), At(shape.D), Uv(shape.A), Uv(shape.B), Uv(shape.C), Uv(shape.D));
    }
}

internal sealed class CompanionHomeStyle
{
    internal readonly record struct Shape(Vector2 A, Vector2 B, Vector2 C, Vector2 D);
    public string Id { get; }
    public string File { get; }
    public Vector4 Uv { get; }
    public Vector4 Opening { get; }
    public Vector2 BedAnchor { get; }
    public Vector2 Threshold { get; }
    public Vector2 Chimney { get; }
    public ImmutableArray<Vector2> DoorPolygon { get; }
    internal ImmutableArray<Shape> Walls { get; }
    private readonly float aspect;

    internal CompanionHomeStyle(string id, string file, Vector4 uv, float aspect, Vector4 opening,
        Vector2 bed, Vector2 threshold, Vector2 chimney, ImmutableArray<Vector2> polygon)
    {
        Id = id; File = file; Uv = uv; this.aspect = aspect; Opening = opening;
        BedAnchor = bed; Threshold = threshold; Chimney = chimney; DoorPolygon = polygon;
        Walls = BuildWalls(polygon);
    }

    // Bottom-centred aspect fit. The returned bounds must also own collision and nap geometry.
    public CompanionHomeStyleFrame Place(Vector2 minimum, Vector2 maximum, bool mirrored)
    {
        var available = maximum - minimum;
        if (!Finite(minimum) || !Finite(maximum) || available.X <= 0 || available.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        var height = Math.Min(available.Y, available.X / aspect);
        var size = new Vector2(height * aspect, height);
        var start = minimum + new Vector2((available.X - size.X) * .5f, available.Y - size.Y);
        return new(this, start, start + size, mirrored);
    }

    private static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);

    private static ImmutableArray<Shape> BuildWalls(ImmutableArray<Vector2> polygon)
    {
        var walls = ImmutableArray.CreateBuilder<Shape>();
        var levels = polygon.Select(p => p.Y).Distinct().Order().ToArray();
        Add(0, 0, 1, levels[0], 0, 1);
        for (var i = 0; i + 1 < levels.Length; i++)
        {
            var top = levels[i]; var bottom = levels[i + 1];
            var (leftTop, rightTop) = CrossSection(top);
            var (leftBottom, rightBottom) = CrossSection(bottom);
            Add(0, top, leftTop, bottom, 0, leftBottom);
            Add(rightTop, top, 1, bottom, rightBottom, 1);
        }
        Add(0, levels[^1], 1, 1, 0, 1);
        return walls.ToImmutable();

        void Add(float leftTop, float top, float rightTop, float bottom, float leftBottom, float rightBottom)
        {
            if (bottom > top && (rightTop > leftTop || rightBottom > leftBottom))
                walls.Add(new(new(leftTop, top), new(rightTop, top), new(rightBottom, bottom), new(leftBottom, bottom)));
        }
        (float Left, float Right) CrossSection(float y)
        {
            var left = float.PositiveInfinity; var right = float.NegativeInfinity;
            for (var j = 0; j < polygon.Length; j++)
            {
                var a = polygon[j]; var b = polygon[(j + 1) % polygon.Length];
                if (y < Math.Min(a.Y, b.Y) || y > Math.Max(a.Y, b.Y)) continue;
                if (a.Y == b.Y) { left = Math.Min(left, Math.Min(a.X, b.X)); right = Math.Max(right, Math.Max(a.X, b.X)); }
                else { var x = float.Lerp(a.X, b.X, (y - a.Y) / (b.Y - a.Y)); left = Math.Min(left, x); right = Math.Max(right, x); }
            }
            return (left, right);
        }
    }
}

internal enum CompanionHandPose { Ready, OpenReach, ClosedHold, Release }
internal sealed record CompanionHandFrame(string File, Vector4 Uv, Vector2 CropSize, Vector2 GripInCropPixels,
    float SourceUnitsPerLogicalCell)
{
    public CompanionStyleQuad Place(Vector2 contact, float logicalEdge)
    {
        if (!float.IsFinite(logicalEdge) || logicalEdge <= 0 || !float.IsFinite(contact.X) || !float.IsFinite(contact.Y))
            throw new ArgumentOutOfRangeException(nameof(logicalEdge));
        var scale = logicalEdge / SourceUnitsPerLogicalCell;
        var top = contact - GripInCropPixels * scale; var end = top + CropSize * scale;
        return new(top, new(end.X, top.Y), end, new(top.X, end.Y),
            new(Uv.X, Uv.Y), new(Uv.Z, Uv.Y), new(Uv.Z, Uv.W), new(Uv.X, Uv.W));
    }
}

internal sealed record CompanionHandStyle(string Id, ImmutableArray<CompanionHandFrame> Poses)
{
    public CompanionHandFrame Pose(CompanionHandPose pose) => Poses[(int)pose];
    public CompanionStyleQuad Frame(CompanionHandPose pose, Vector2 contact, float logicalEdge) => Pose(pose).Place(contact, logicalEdge);
}

/// <summary>Loads reviewed local sprite metadata only; no game state, settings, or network access.</summary>
internal sealed class CompanionHomeStyleLibrary
{
    public static ImmutableArray<string> HomeIds { get; } = ["Default", "Random", "CrystalPod", "CloudCushion", "LanternHut",
        "AllSaintsWake", "Starlight", "Heavensturn", "Valentione", "LittleLadies", "HatchingTide", "MakeItRain", "MoonfireFaire", "TheRising"];
    public static ImmutableArray<string> HandIds { get; } = ["Default", "Random", "IvoryChibi", "StarlightGlove", "MagicalPaw", "HologramMitt"];
    private readonly Dictionary<string, CompanionHomeStyle> homes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CompanionHandStyle> hands = new(StringComparer.Ordinal);
    public CompanionHomeStyle GetHome(string? id) => id != null && homes.TryGetValue(id, out var value) ? value : homes["Default"];
    public CompanionHandStyle GetHand(string? id) => id != null && hands.TryGetValue(id, out var value) ? value : hands["Default"];

    public CompanionHomeStyleLibrary(string assetDirectory)
    {
        AddDefault();
        var path = Path.Combine(assetDirectory, "companion-styles.frames.json");
        if (new FileInfo(path).Length > 256 * 1024) throw new InvalidDataException("Style metadata is too large.");
        var metadata = JsonSerializer.Deserialize<Metadata>(File.ReadAllText(path), new JsonSerializerOptions
            { PropertyNameCaseInsensitive = true, MaxDepth = 16 }) ?? throw new InvalidDataException("Style metadata is missing.");
        if (metadata.Schema != 1 || metadata.Assets is not { Length: 3 } || metadata.Houses is not { Length: 12 } || metadata.Hands is not { Length: 16 }
            || metadata.Assets.Any(a => a == null) || metadata.Houses.Any(h => h == null) || metadata.Hands.Any(h => h == null))
            throw new InvalidDataException("Unsupported style metadata.");
        var assets = new Dictionary<string, Asset>(StringComparer.Ordinal);
        foreach (var asset in metadata.Assets)
        {
            if (asset.File is not ("homes-standard.png" or "homes-holiday.png" or "hands-styles.png")
                || asset.Size is not { Length: 2 } || asset.Size.Any(v => v is < 1 or > 8192) || !assets.TryAdd(asset.File, asset))
                throw new InvalidDataException("Invalid style asset.");
            using var input = File.OpenRead(Path.Combine(assetDirectory, asset.File));
            if (!Convert.ToHexString(SHA256.HashData(input)).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Style asset integrity mismatch.");
        }
        foreach (var home in metadata.Houses)
        {
            var id = home.Name switch { "Moonfire" => "MoonfireFaire", "Rising" => "TheRising", _ => home.Name };
            if (!HomeIds.Contains(id) || id is "Default" or "Random" || homes.ContainsKey(id)) throw new InvalidDataException("Invalid home style.");
            if (home.File != (id is "CrystalPod" or "CloudCushion" or "LanternHut" ? "homes-standard.png" : "homes-holiday.png"))
                throw new InvalidDataException("Home style uses the wrong atlas.");
            var uv = ReadCrop(home.File, home.Rect, assets);
            var opening = Rect(home.DoorAperture);
            var polygon = (home.DoorPolygon ?? throw new InvalidDataException("Missing doorway polygon.")).Select(Point).ToImmutableArray();
            if (polygon.Length is < 4 or > 12 || polygon.Any(p => p.X < opening.X || p.X > opening.Z || p.Y < opening.Y || p.Y > opening.W))
                throw new InvalidDataException("Invalid doorway polygon.");
            // This renderer accepts a convex doorway; concave/self-intersecting metadata is rejected.
            float sign = 0;
            for (var i = 0; i < polygon.Length; i++)
            {
                var a = polygon[(i + 1) % polygon.Length] - polygon[i];
                for (var j = 0; j < polygon.Length; j++)
                {
                    var b = polygon[j] - polygon[i];
                    var cross = a.X * b.Y - a.Y * b.X;
                    if (Math.Abs(cross) < .000001f) continue;
                    if (sign != 0 && Math.Sign(cross) != Math.Sign(sign)) throw new InvalidDataException("Doorway must be convex.");
                    sign = cross;
                }
            }
            if (sign == 0) throw new InvalidDataException("Doorway has no area.");
            var bed = Point(home.BedAnchor); var threshold = Point(home.Threshold);
            for (var i = 0; i < polygon.Length; i++)
            {
                var edge = polygon[(i + 1) % polygon.Length] - polygon[i]; var towardBed = bed - polygon[i];
                var side = edge.X * towardBed.Y - edge.Y * towardBed.X;
                if (Math.Abs(side) > .000001f && Math.Sign(side) != Math.Sign(sign))
                    throw new InvalidDataException("Bed anchor is outside the doorway.");
            }
            if (threshold.Y < bed.Y || threshold.X < opening.X || threshold.X > opening.Z)
                throw new InvalidDataException("Threshold does not lead into the doorway.");
            homes.Add(id, new(id, home.File, uv, (float)(home.Rect[2] - home.Rect[0]) / (home.Rect[3] - home.Rect[1]),
                opening, bed, threshold, Point(home.Chimney), polygon));
        }
        foreach (var style in HandIds.Where(id => id is not ("Default" or "Random")))
        {
            var rows = metadata.Hands.Where(h => h.Style == style).ToArray();
            if (rows.Length != 4 || rows.Any(h => h.File != "hands-styles.png") || rows.Select(h => h.SourceUnitsPerLogicalCell).Distinct().Count() != 1)
                throw new InvalidDataException("Incomplete or inconsistently scaled hand style.");
            var frames = ImmutableArray.CreateBuilder<CompanionHandFrame>(4);
            foreach (var pose in Enum.GetValues<CompanionHandPose>())
            {
                var rowsForPose = rows.Where(h => h.Pose == pose.ToString()).ToArray();
                if (rowsForPose.Length != 1) throw new InvalidDataException("Duplicate or missing hand pose.");
                var hand = rowsForPose[0]; var uv = ReadCrop(hand.File, hand.Rect, assets);
                if (hand.GripSourcePixels is not { Length: 2 } || !float.IsFinite(hand.SourceUnitsPerLogicalCell) || hand.SourceUnitsPerLogicalCell is < 32 or > 4096)
                    throw new InvalidDataException("Invalid hand contact.");
                var grip = new Vector2(hand.GripSourcePixels[0] - hand.Rect[0], hand.GripSourcePixels[1] - hand.Rect[1]);
                var size = new Vector2(hand.Rect[2] - hand.Rect[0], hand.Rect[3] - hand.Rect[1]);
                if (!float.IsFinite(grip.X) || !float.IsFinite(grip.Y) || grip.X < 0 || grip.Y < 0 || grip.X > size.X || grip.Y > size.Y)
                    throw new InvalidDataException("Hand contact is outside its crop.");
                frames.Add(new(hand.File, uv, size, grip, hand.SourceUnitsPerLogicalCell));
            }
            hands.Add(style, new(style, frames.ToImmutable()));
        }
    }

    private void AddDefault()
    {
        homes.Add("Default", CompanionHomeGeometry.DefaultStyle);
        var frames = ImmutableArray.CreateBuilder<CompanionHandFrame>(4);
        for (var i = 0; i < 4; i++)
        {
            var start = new Vector2(i % 2, i / 2) * .5f;
            frames.Add(new("companion-grab-hand.png", new(start.X, start.Y, start.X + .5f, start.Y + .5f), Vector2.One,
                i is 0 or 1 ? new(.51f, .805f) : i == 3 ? new(.485f, .77f) : new(.5f, .77f), 1));
        }
        hands.Add("Default", new("Default", frames.ToImmutable()));
    }

    private static Vector4 ReadCrop(string file, int[] rect, Dictionary<string, Asset> assets)
    {
        if (file == null || !assets.TryGetValue(file, out var asset) || rect is not { Length: 4 } || rect[0] < 0 || rect[1] < 0
            || rect[2] <= rect[0] || rect[3] <= rect[1] || rect[2] > asset.Size[0] || rect[3] > asset.Size[1])
            throw new InvalidDataException("Invalid sprite crop.");
        return new((float)rect[0] / asset.Size[0], (float)rect[1] / asset.Size[1], (float)rect[2] / asset.Size[0], (float)rect[3] / asset.Size[1]);
    }
    private static Vector2 Point(float[] value) => value is { Length: 2 } && value.All(v => float.IsFinite(v) && v >= 0 && v <= 1)
        ? new(value[0], value[1]) : throw new InvalidDataException("Invalid normalized anchor.");
    private static Vector4 Rect(float[] value) => value is { Length: 4 } && value.All(v => float.IsFinite(v) && v >= 0 && v <= 1)
        && value[2] > value[0] && value[3] > value[1] ? new(value[0], value[1], value[2], value[3])
        : throw new InvalidDataException("Invalid normalized opening.");

    private sealed class Metadata { public int Schema { get; set; } public Asset[] Assets { get; set; } = []; public Home[] Houses { get; set; } = []; public Hand[] Hands { get; set; } = []; }
    private sealed class Asset { public string File { get; set; } = ""; public int[] Size { get; set; } = []; public string Sha256 { get; set; } = ""; }
    private sealed class Home
    {
        public string Name { get; set; } = ""; public string File { get; set; } = ""; public int[] Rect { get; set; } = [];
        public float[] DoorAperture { get; set; } = []; public float[][] DoorPolygon { get; set; } = [];
        public float[] BedAnchor { get; set; } = []; public float[] Threshold { get; set; } = []; public float[] Chimney { get; set; } = [];
    }
    private sealed class Hand
    {
        public string Style { get; set; } = ""; public string Pose { get; set; } = ""; public string File { get; set; } = "";
        public int[] Rect { get; set; } = []; public float[] GripSourcePixels { get; set; } = []; public float SourceUnitsPerLogicalCell { get; set; }
    }
}
