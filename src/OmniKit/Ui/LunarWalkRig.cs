using System.Numerics;
using System.Text.Json;

namespace OmniKit.Ui;

internal readonly record struct LunarRigPart(Vector4 Rect, Vector2 Pivot, Vector2 EndPivot, float WidthScale = 1f)
{
    public LunarRigPart(Vector4 rect, Vector2 pivot) : this(rect, pivot, new Vector2(.5f, 1)) { }
}

internal readonly record struct LunarRigQuad(int Part, int Section, Vector2 A, Vector2 B, Vector2 C, Vector2 D,
    Vector2 UvA, Vector2 UvB, Vector2 UvC, Vector2 UvD);

internal sealed record LunarWalkPose(LunarRigQuad[] Quads, Vector2 NearHip, Vector2 FarHip,
    Vector2 NearKnee, Vector2 FarKnee, Vector2 NearAnkle, Vector2 FarAnkle,
    Vector2 NearFoot, Vector2 FarFoot, bool NearStance, bool FarStance)
{
    public (Vector2 Minimum, Vector2 Maximum) Bounds
    {
        get
        {
            var min = new Vector2(float.MaxValue); var max = new Vector2(float.MinValue);
            foreach (var q in Quads)
            {
                min = Vector2.Min(min, Vector2.Min(Vector2.Min(q.A, q.B), Vector2.Min(q.C, q.D)));
                max = Vector2.Max(max, Vector2.Max(Vector2.Max(q.A, q.B), Vector2.Max(q.C, q.D)));
            }
            return (min, max);
        }
    }
}

/// <summary>Deterministic articulated walk rig. Coordinates and joint lengths use logical cell units.</summary>
internal sealed class LunarWalkRig(string file, Vector2 sheetSize, LunarRigPart[] parts, Vector4 bodyRect,
    Vector2 hip, Vector2 nearShoulderUv, Vector2 farShoulderUv, Vector2 nearHipOffset, Vector2 farHipOffset,
    float thighLength, float shinLength, Vector2 footSize,
    float armLength, float cycleDistance, bool earFollowThrough = false, bool sharedIdle = false)
{
    private const float Stance = .6f;
    private const float Ground = .9f;
    public string File { get; } = file;
    public float CycleDistance { get; } = cycleDistance;
    public float ThighLength { get; } = thighLength;
    public float ShinLength { get; } = shinLength;
    public bool SupportsIdle { get; } = sharedIdle;

    public static float NextWalkBlend(float current, bool walking, bool animated, float elapsed)
    {
        if (!animated) return 0;
        current = float.IsFinite(current) ? Math.Clamp(current, 0, 1) : 0;
        var step = (float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0) / .18f;
        return current + Math.Clamp((walking ? 1f : 0f) - current, -step, step);
    }

    public LunarWalkPose Evaluate(float phase, bool facingLeft, Vector2 position, Vector2 cellSize, Vector2 travelHeading = default,
        float walkBlend = 1f)
    {
        phase = float.IsFinite(phase) ? phase - MathF.Floor(phase) : 0;
        walkBlend = SupportsIdle && float.IsFinite(walkBlend) ? Math.Clamp(walkBlend, 0, 1) : 1f;
        walkBlend = walkBlend * walkBlend * (3 - 2 * walkBlend);
        if (!float.IsFinite(travelHeading.X) || !float.IsFinite(travelHeading.Y) || travelHeading.LengthSquared() < .000001f)
            travelHeading = facingLeft ? -Vector2.UnitX : Vector2.UnitX;
        else travelHeading = Vector2.Normalize(travelHeading);
        var cycle = CompanionGait.DirectionalCycleDistance(CycleDistance, travelHeading);
        var localHeading = new Vector2(facingLeft ? -travelHeading.X : travelHeading.X, travelHeading.Y);
        var farPhase = (phase + .5f) % 1f;
        var angle = .018f * MathF.Sin(phase * MathF.Tau - .28f) * walkBlend;
        var movedHip = hip + new Vector2(0, .006f * MathF.Cos(phase * MathF.Tau * 2) * walkBlend);
        var nearHip = movedHip + Rotate(nearHipOffset, angle);
        var farHip = movedHip + Rotate(farHipOffset, angle);
        var nearFoot = Foot(phase, hip.X + .012f, localHeading, cycle);
        var farFoot = Foot(farPhase, hip.X - .012f, localHeading, cycle);
        if (walkBlend < 1)
        {
            // Settle both paws onto the same ground plane without changing the
            // distance-driven phase or the full-walk trajectories.
            nearFoot = Vector2.Lerp(new Vector2(hip.X + .065f, Ground), nearFoot, walkBlend);
            farFoot = Vector2.Lerp(new Vector2(hip.X - .065f, Ground), farFoot, walkBlend);
        }
        var nearAnkle = nearFoot - new Vector2(0, FootSize(7).Y * (1 - parts[7].Pivot.Y));
        var farAnkle = farFoot - new Vector2(0, FootSize(8).Y * (1 - parts[8].Pivot.Y));
        var nearKnee = Solve(nearHip, nearAnkle);
        var farKnee = Solve(farHip, farAnkle);
        var armSwing = -.42f * MathF.Cos(phase * MathF.Tau) * walkBlend;
        Vector2 BodyAnchor(Vector2 uv) => movedHip + Rotate(new Vector2(bodyRect.X, bodyRect.Y)
            + uv * new Vector2(bodyRect.Z, bodyRect.W) - hip, angle);
        var nearShoulder = BodyAnchor(nearShoulderUv);
        var farShoulder = BodyAnchor(farShoulderUv);

        // Sleeve roots sit inside the painted cape. Draw limbs behind the body so
        // the cape covers the attachment instead of a fur cap covering the clasp.
        var quads = new List<LunarRigQuad>(14)
        {
            Segment(2, farShoulder, farShoulder + new Vector2(MathF.Sin(-armSwing), MathF.Cos(-armSwing)) * armLength, armLength),
            Segment(4, farHip, farKnee, ThighLength), Segment(6, farKnee, farAnkle, ShinLength), FootQuad(8, farFoot),
            Segment(3, nearHip, nearKnee, ThighLength), Segment(5, nearKnee, nearAnkle, ShinLength), FootQuad(7, nearFoot),
            Segment(1, nearShoulder, nearShoulder + new Vector2(MathF.Sin(armSwing), MathF.Cos(armSwing)) * armLength, armLength),
        };
        // Shared band boundaries keep the original texture joined. Only the upper
        // third receives secondary flex; the face/torso remain rigid with the hips.
        Vector2 BodyPoint(float x, float y)
        {
            var weight = earFollowThrough ? MathF.Pow(Math.Max(0, 1 - y * 3), 2) : 0;
            var lag = phase * MathF.Tau - .9f * weight;
            var flex = new Vector2(.012f * MathF.Sin(lag), .004f * MathF.Sin(lag - .7f)) * weight * walkBlend;
            return movedHip + Rotate(new Vector2(bodyRect.X + x * bodyRect.Z, bodyRect.Y + y * bodyRect.W) - hip, angle) + flex;
        }
        for (var band = 0; band < 6; band++)
        {
            var top = band / 6f; var bottom = (band + 1) / 6f;
            quads.Add(Quad(0, BodyPoint(0, top), BodyPoint(1, top), BodyPoint(1, bottom), BodyPoint(0, bottom), band, top, bottom));
        }
        Vector2 Place(Vector2 p) => position + new Vector2(facingLeft ? 1 - p.X : p.X, p.Y) * cellSize;
        for (var i = 0; i < quads.Count; i++)
        {
            var q = quads[i];
            if (!Inside(q.A) || !Inside(q.B) || !Inside(q.C) || !Inside(q.D)) throw new InvalidOperationException("Rig geometry is outside its cell.");
            quads[i] = q with { A = Place(q.A), B = Place(q.B), C = Place(q.C), D = Place(q.D) };
        }
        return new LunarWalkPose(quads.ToArray(), Place(nearHip), Place(farHip), Place(nearKnee), Place(farKnee),
            Place(nearAnkle), Place(farAnkle), Place(nearFoot), Place(farFoot), walkBlend == 0 || phase < Stance, walkBlend == 0 || farPhase < Stance);
    }

    private Vector2 Foot(float phase, float center, Vector2 heading, float cycle)
    {
        var origin = new Vector2(center, Ground);
        var front = cycle * Stance * .5f;
        if (phase < Stance) return origin + heading * (front - cycle * phase);
        var t = (phase - Stance) / (1 - Stance);
        // Hermite return: foot velocity matches the planted phase at both ends.
        var smooth = t * t * (3 - 2 * t);
        var matchingSlope = -cycle * (1 - Stance) * (2 * t * t * t - 3 * t * t + t);
        var x = -front + 2 * front * smooth + matchingSlope;
        var lift = Math.Min(.075f, (ThighLength + ShinLength) * .24f) * 16 * t * t * (1 - t) * (1 - t);
        return origin + heading * x - new Vector2(0, lift);
    }

    private Vector2 Solve(Vector2 root, Vector2 target)
    {
        var delta = target - root;
        var distance = delta.Length();
        if (!float.IsFinite(distance) || distance < Math.Abs(ThighLength - ShinLength) + .00001f
            || distance > ThighLength + ShinLength - .00001f) throw new InvalidOperationException("Rig leg cannot reach its foot.");
        var along = (ThighLength * ThighLength - ShinLength * ShinLength + distance * distance) / (2 * distance);
        var across = MathF.Sqrt(Math.Max(0, ThighLength * ThighLength - along * along));
        var direction = delta / distance;
        return root + direction * along + new Vector2(direction.Y, -direction.X) * across;
    }

    private LunarRigQuad Segment(int index, Vector2 start, Vector2 end, float length)
    {
        var part = parts[index]; var rect = part.Rect;
        var aspect = (rect.Z - rect.X) / (rect.W - rect.Y);
        // Both joints sit inside the painted capsule. Its fur extends beyond
        // each joint to overlap the next part rather than meeting at narrow tips.
        var sourceBone = (part.EndPivot - part.Pivot) * new Vector2(aspect, 1);
        var height = length / sourceBone.Length();
        var size = new Vector2(height * aspect, height);
        var direction = end - start;
        var angle = MathF.Atan2(direction.Y, direction.X) - MathF.Atan2(sourceBone.Y, sourceBone.X);
        var transverse = Vector2.Normalize(new Vector2(-sourceBone.Y, sourceBone.X));
        Vector2 Corner(float x, float y)
        {
            var point = (new Vector2(x, y) - part.Pivot) * size;
            // Widen only across the bone. Both joint pivots and every trajectory
            // stay fixed, including when the source pivots are off-center.
            point += transverse * Vector2.Dot(point, transverse) * (part.WidthScale - 1f);
            return start + Rotate(point, angle);
        }
        return Quad(index, Corner(0, 0), Corner(1, 0), Corner(1, 1), Corner(0, 1));
    }
    private LunarRigQuad FootQuad(int index, Vector2 sole)
    {
        var size = FootSize(index);
        var left = -size.X * parts[index].Pivot.X;
        return Quad(index, sole + new Vector2(left, -size.Y), sole + new Vector2(left + size.X, -size.Y),
            sole + new Vector2(left + size.X, 0), sole + new Vector2(left, 0));
    }
    private Vector2 FootSize(int index)
    {
        var rect = parts[index].Rect;
        var source = new Vector2(rect.Z - rect.X, rect.W - rect.Y);
        // Uniform fit preserves the painted paw's aspect; the grounded sole and
        // gait path remain exact while the ankle follows the fitted source pivot.
        return source * Math.Min(footSize.X / source.X, footSize.Y / source.Y);
    }
    private LunarRigQuad Quad(int index, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int section = 0, float top = 0, float bottom = 1)
    {
        var rect = parts[index].Rect;
        var uvTop = rect.Y + (rect.W - rect.Y) * top; var uvBottom = rect.Y + (rect.W - rect.Y) * bottom;
        return new LunarRigQuad(index, section, a, b, c, d, new Vector2(rect.X, uvTop) / sheetSize,
            new Vector2(rect.Z, uvTop) / sheetSize, new Vector2(rect.Z, uvBottom) / sheetSize, new Vector2(rect.X, uvBottom) / sheetSize);
    }
    private static Vector2 Rotate(Vector2 point, float angle)
        => new(point.X * MathF.Cos(angle) - point.Y * MathF.Sin(angle), point.X * MathF.Sin(angle) + point.Y * MathF.Cos(angle));
    private static bool Inside(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && p.X >= 0 && p.Y >= 0 && p.X <= 1 && p.Y <= 1;
}

/// <summary>Optional reviewed sprite parts; absent or invalid data leaves atlas playback available.</summary>
internal sealed class LunarWalkRigLibrary
{
    private readonly Dictionary<string, LunarWalkRig> rigs = new(StringComparer.OrdinalIgnoreCase);
    public int Count => rigs.Count;
    public bool TryGet(string avatar, string outfit, out LunarWalkRig? rig)
        => rigs.TryGetValue(avatar + "/" + outfit, out rig) || rigs.TryGetValue(avatar + "/*", out rig);

    public LunarWalkRigLibrary(string assetDirectory)
    {
        var path = Path.Combine(assetDirectory, "lunar-walk-rigs.json");
        try
        {
            if (!System.IO.File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return;
            using var document = JsonDocument.Parse(System.IO.File.ReadAllText(path));
            if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1) return;
            foreach (var item in document.RootElement.GetProperty("rigs").EnumerateArray())
            {
                var character = item.GetProperty("character").GetString();
                var outfit = item.TryGetProperty("outfit", out var outfitNode) ? outfitNode.GetString() : "*";
                var file = item.GetProperty("file").GetString();
                if (string.IsNullOrEmpty(character) || string.IsNullOrEmpty(file) || file != Path.GetFileName(file)
                    || file.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) || !file.EndsWith(".png", StringComparison.Ordinal)) continue;
                var size = Pair(item.GetProperty("size"));
                if (!Finite(size) || size.X <= 0 || size.Y <= 0 || size.X > 8192 || size.Y > 8192) continue;
                var source = item.GetProperty("parts");
                if (source.GetArrayLength() != 9) continue;
                var parts = new LunarRigPart[9]; var valid = true; var index = 0;
                foreach (var part in source.EnumerateArray())
                {
                    var r = Rect(part.GetProperty("rect"));
                    var pivot = part.TryGetProperty("pivot", out var pivotNode) ? Pair(pivotNode) : new Vector2(.5f, 0);
                    var endPivot = part.TryGetProperty("endPivot", out var endNode) ? Pair(endNode) : new Vector2(.5f, 1);
                    var widthScale = part.TryGetProperty("widthScale", out var widthNode) ? widthNode.GetSingle() : 1f;
                    if (part.GetProperty("index").GetInt32() != index || !Finite(r) || r.X < 0 || r.Y < 0
                        || r.Z <= r.X || r.W <= r.Y || r.Z > size.X || r.W > size.Y || !Finite(pivot)
                        || pivot.X < 0 || pivot.X > 1 || pivot.Y < 0 || pivot.Y >= 1 || !Finite(endPivot)
                        || endPivot.X < 0 || endPivot.X > 1 || endPivot.Y < 0 || endPivot.Y > 1
                        || !float.IsFinite(widthScale) || widthScale < .5f || widthScale > 2f
                        || index is >= 1 and <= 6 && endPivot.Y - pivot.Y < .1f) { valid = false; break; }
                    parts[index++] = new LunarRigPart(r, pivot, endPivot, widthScale);
                }
                if (!valid) continue;
                var body = Rect(item.GetProperty("bodyRect"));
                var hip = Pair(item.GetProperty("hip"));
                var nearShoulder = Pair(item.GetProperty("nearShoulderUv")); var farShoulder = Pair(item.GetProperty("farShoulderUv"));
                var nearHipOffset = Pair(item.GetProperty("nearHipOffset")); var farHipOffset = Pair(item.GetProperty("farHipOffset"));
                var thigh = item.GetProperty("thighLength").GetSingle(); var shin = item.GetProperty("shinLength").GetSingle();
                var foot = Pair(item.GetProperty("footSize")); var arm = item.GetProperty("armLength").GetSingle();
                var cycle = item.GetProperty("cycleDistance").GetSingle();
                if (!Finite(body) || body.X < 0 || body.Y < 0 || body.Z <= 0 || body.W <= 0 || body.X + body.Z > 1 || body.Y + body.W > 1
                    || !Finite(hip) || !Unit(nearShoulder) || !Unit(farShoulder) || !Finite(nearHipOffset) || !Finite(farHipOffset)
                    || nearHipOffset.Length() > .3f || farHipOffset.Length() > .3f || !Length(thigh) || !Length(shin) || !Length(arm)
                    || !Finite(foot) || !Length(foot.X) || !Length(foot.Y) || !float.IsFinite(cycle) || cycle < .1f || cycle > 8) continue;
                var rig = new LunarWalkRig(file, size, parts, body, hip, nearShoulder, farShoulder, nearHipOffset, farHipOffset, thigh, shin, foot, arm, cycle,
                    string.Equals(character, "Bimble", StringComparison.OrdinalIgnoreCase),
                    string.Equals(character, "Bimble", StringComparison.OrdinalIgnoreCase)
                        && item.TryGetProperty("sharedIdle", out var idleNode) && idleNode.ValueKind == JsonValueKind.True);
                try
                {
                    for (var frame = 0; frame < 128; frame++) rig.Evaluate(frame / 128f, false, Vector2.Zero, Vector2.One);
                    rigs[character + "/" + outfit] = rig;
                }
                catch (InvalidOperationException) { /* Unreachable joints or overflowing art fail back to the atlas. */ }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException
            or ArgumentException or KeyNotFoundException or IndexOutOfRangeException or FormatException or OverflowException)
        { /* Optional rig metadata must never prevent the original companion from rendering. */ }
    }
    private static bool Length(float value) => float.IsFinite(value) && value >= .02f && value <= .5f;
    private static bool Unit(Vector2 p) => Finite(p) && p.X >= 0 && p.Y >= 0 && p.X <= 1 && p.Y <= 1;
    private static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);
    private static bool Finite(Vector4 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z) && float.IsFinite(p.W);
    private static Vector2 Pair(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle());
    private static Vector4 Rect(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle(), value[3].GetSingle());
}
