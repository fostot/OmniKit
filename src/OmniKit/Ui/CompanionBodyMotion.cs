using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

/// <summary>One connected textured body. No duplicated body or independent limb layers.</summary>
internal sealed class CompanionBodyMotion
{
    private float clock;
    private float activity;
    public float LastDrawStrength { get; private set; } = 1;
    public Vector2 WarpAttachment(string avatar, Vector2 point, Vector2 position, Vector2 size, float stride, bool mirrored, bool animate)
        => Vector2.Lerp(point, Warp(avatar, point, position, size, stride, mirrored, animate), LastDrawStrength);
    public void Update(float elapsed, bool animate, bool moving)
    {
        var dt = float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0;
        if (!animate) return;
        clock = (clock + dt) % MathF.Tau;
        activity += (moving ? 1 - activity : -activity) * (1 - MathF.Exp(-dt * 9));
    }

    public Vector2 Warp(string avatar, Vector2 point, Vector2 position, Vector2 size, float stride, bool mirrored, bool animate)
    {
        if (!animate || size.X <= 0 || size.Y <= 0) return point;
        stride = float.IsFinite(stride) ? stride - MathF.Floor(stride) : 0;
        var local = (point - position) / size;
        var p = local - new Vector2(.5f, .9f);
        var cycle = stride * MathF.Tau;
        var facing = mirrored ? -1 : 1;
        if (avatar == "Ember")
        {
            var pulse = MathF.Sin(clock * 3) * .025f + MathF.Sin(cycle) * .065f * activity;
            p.X *= 1 + pulse;
            p.Y /= 1 + pulse;
            var tip = Math.Clamp(-p.Y, 0, 1);
            p.X += (MathF.Sin(clock * 4 + tip * 5) * .027f + MathF.Sin(cycle) * .025f * activity * facing) * tip * tip;
            p.Y -= MathF.Sin(clock * 5 + tip * 2) * .018f * tip;
        }
        else if (avatar == "Mochi")
        {
            var compression = MathF.Cos(cycle) * .105f * activity + MathF.Sin(clock * 2) * .012f;
            p.X *= 1 + compression;
            p.Y /= 1 + compression;
            p.X += -p.Y * MathF.Sin(cycle) * .045f * activity * facing;
            // A tiny recoil belongs to the body; its supporting plane never moves.
            p.Y -= Math.Max(0, MathF.Sin(cycle)) * .025f * activity;
        }
        else if (avatar == "Puff")
        {
            var height = Math.Clamp(-p.Y, 0, 1);
            p.X *= 1 + MathF.Sin(clock * 2 + height * 3) * .025f;
            p.Y *= 1 + MathF.Sin(clock * 3) * .012f;
        }
        return position + (p + new Vector2(.5f, .9f)) * size;
    }

    public (Vector2 Minimum, Vector2 Maximum) Draw(ImDrawListPtr draw, ImTextureID texture, string avatar,
        CompanionFloatQuad quad, Vector2 position, Vector2 cellSize, float stride, bool mirrored, bool animate, uint tint)
    {
        const int divisions = 8;
        Span<Vector2> points = stackalloc Vector2[(divisions + 1) * (divisions + 1)];
        Span<Vector2> originals = stackalloc Vector2[points.Length];
        var strength = 1f;
        var minimum = new Vector2(float.MaxValue); var maximum = new Vector2(float.MinValue);
        for (var y = 0; y <= divisions; y++)
        for (var x = 0; x <= divisions; x++)
        {
            var u = x / (float)divisions; var v = y / (float)divisions;
            var p = Vector2.Lerp(Vector2.Lerp(quad.A, quad.B, u), Vector2.Lerp(quad.D, quad.C, u), v);
            var index = y * (divisions + 1) + x;
            originals[index] = p;
            var warped = Warp(avatar, p, position, cellSize, stride, mirrored, animate);
            points[index] = warped;
            var offset = warped - p;
            // Preserve one connected body and its source scale. When a reviewed
            // crop has little spare room, reduce the deformation as a whole
            // instead of clipping tips or independently pinning edge vertices.
            strength = Math.Min(strength, Available(p.X, offset.X, position.X, position.X + cellSize.X));
            strength = Math.Min(strength, Available(p.Y, offset.Y, position.Y, position.Y + cellSize.Y));
        }
        LastDrawStrength = strength;
        for (var index = 0; index < points.Length; index++)
        {
            var p = points[index] = Vector2.Lerp(originals[index], points[index], strength);
            minimum = Vector2.Min(minimum, p); maximum = Vector2.Max(maximum, p);
        }
        for (var y = 0; y < divisions; y++)
        for (var x = 0; x < divisions; x++)
        {
            var u = x / (float)divisions; var v = y / (float)divisions;
            var nextU = (x + 1) / (float)divisions; var nextV = (y + 1) / (float)divisions;
            var i = y * (divisions + 1) + x;
            draw.AddImageQuad(texture, points[i], points[i + 1], points[i + divisions + 2], points[i + divisions + 1],
                Uv(u, v), Uv(nextU, v), Uv(nextU, nextV), Uv(u, nextV), tint);
        }
        return (minimum, maximum);

        static float Available(float original, float offset, float first, float last)
            => offset > 0 ? Math.Clamp((last - original) / offset, 0, 1)
                : offset < 0 ? Math.Clamp((first - original) / offset, 0, 1) : 1;
        Vector2 Uv(float u, float v) => Vector2.Lerp(Vector2.Lerp(quad.UvA, quad.UvB, u), Vector2.Lerp(quad.UvD, quad.UvC, u), v);
    }
}
