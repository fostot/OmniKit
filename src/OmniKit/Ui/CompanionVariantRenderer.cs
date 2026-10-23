using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

/// <summary>Draws one authored body image. Travel uses Neutral; movement mesh/float/roll stays with the caller.</summary>
internal static class CompanionVariantRenderer
{
    public static CompanionFrame Frame(CompanionVariantLook look, CompanionVariantPose pose, Vector2 position,
        Vector2 cellSize, bool mirrored = false) => look[pose].GetFrame(position, cellSize, mirrored);

    public static CompanionFrame CarriedFrame(CompanionVariantLook look, bool alternate, Vector2 gripPosition,
        Vector2 cellSize, bool mirrored = false)
    {
        var source = look[alternate ? CompanionVariantPose.CarrySquirmB : CompanionVariantPose.CarrySquirmA];
        var initial = source.GetFrame(Vector2.Zero, cellSize, mirrored);
        return initial with { Position = initial.Position + gripPosition - source.Map(source.Grip, Vector2.Zero, cellSize, mirrored) };
    }

    public static (Vector2 Minimum, Vector2 Maximum) Draw(ImDrawListPtr draw, ImTextureID texture, CompanionFrame frame, uint tint = 0xFFFFFFFFu)
    {
        draw.AddImage(texture, frame.Position, frame.Position + frame.Size, frame.UvStart, frame.UvEnd, tint);
        return (frame.Position, frame.Position + frame.Size);
    }

    /// <summary>A face-only lid on the unchanged neutral quad, colored from this look's own closed forehead.</summary>
    public static void DrawWinkBlink(ImDrawListPtr draw, ImTextureID closedTexture, CompanionVariantLook look,
        CompanionFloatQuad normalQuad, float closure, uint tint)
    {
        if (look.Avatar != "Wink" || look.EyeEllipse is not { } eye || !float.IsFinite(closure)) return;
        closure = Math.Clamp(closure, 0, 1);
        if (closure <= .001f) return;
        var normal = look.Normal; var closed = look[CompanionVariantPose.ClosedBlink]; var rect = normal.Bounds;
        Vector2 Point(Vector2 source) => normalQuad.A + (normalQuad.B - normalQuad.A) * ((source.X - rect.X) / (rect.Z - rect.X))
            + (normalQuad.D - normalQuad.A) * ((source.Y - rect.Y) / (rect.W - rect.Y));
        var body = closed.Body; var bodyWidth = body.Z - body.X; var bodyHeight = body.W - body.Y;
        // The inspected strip is below the forehead highlight and above the closed eye.
        // It follows the selected look's source crop, never another palette or the original Wink atlas.
        Vector2 Uv(Vector2 p) => new Vector2(
            body.X + bodyWidth * (.40f + Math.Clamp((p.X - eye.X + eye.Z) / (2 * eye.Z), 0, 1) * .42f),
            body.Y + bodyHeight * (.40f + Math.Clamp((p.Y - eye.Y + eye.W) / (2 * eye.W), 0, 1) * .11f)) / closed.SheetSize;
        var angle = MathF.Asin(closure * 2 - 1);
        Span<Vector2> points = stackalloc Vector2[33]; var center = Vector2.Zero;
        for (var i = 0; i < points.Length; i++)
        {
            var t = MathF.PI - angle + (MathF.PI + 2 * angle) * i / (points.Length - 1);
            points[i] = new(eye.X + MathF.Cos(t) * eye.Z, eye.Y + MathF.Sin(t) * eye.W);
            center += points[i];
        }
        center /= points.Length;
        for (var i = 0; i < points.Length; i++)
        {
            var a = points[i]; var b = points[(i + 1) % points.Length];
            draw.AddImageQuad(closedTexture, Point(center), Point(a), Point(b), Point(b), Uv(center), Uv(a), Uv(b), Uv(b), tint);
            if (Vector2.DistanceSquared(a, b) < .00001f) continue;
            var deltaA = a - center; var deltaB = b - center;
            var outerA = a + (deltaA.LengthSquared() > .000001f ? Vector2.Normalize(deltaA) : -Vector2.UnitY) * 2;
            var outerB = b + (deltaB.LengthSquared() > .000001f ? Vector2.Normalize(deltaB) : -Vector2.UnitY) * 2;
            var first = draw.VtxBuffer.Size;
            draw.AddImageQuad(closedTexture, Point(a), Point(b), Point(outerB), Point(outerA), Uv(a), Uv(b), Uv(outerB), Uv(outerA), tint);
            unsafe
            {
                draw.VtxBuffer.Data[first + 2].Col &= 0xFFFFFF;
                draw.VtxBuffer.Data[first + 3].Col &= 0xFFFFFF;
            }
        }
        if (closure < .7f) return;
        var opacity = (uint)(Math.Clamp((closure - .7f) / .3f, 0, 1) * (tint >> 24));
        for (var i = 0; i <= 24; i++)
        {
            var x = -1f + i / 12f;
            draw.PathLineTo(Point(new(eye.X + x * eye.Z * .74f, eye.Y + eye.W * (.30f - .22f * (1 - x * x)))));
        }
        draw.PathStroke(opacity << 24 | 0x00291B27, ImDrawFlags.None,
            Math.Max(.65f, Vector2.Distance(normalQuad.A, normalQuad.B) / (rect.Z - rect.X) * 3));
    }
}
