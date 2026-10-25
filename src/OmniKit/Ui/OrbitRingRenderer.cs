using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

internal readonly record struct OrbitCorePlacement(CompanionFrame Frame, Vector2 Center, float Radius);

/// <summary>A rotating ring plane, split by depth around a stationary painted core.</summary>
internal sealed class OrbitRingRenderer
{
    private float precession;
    private float tiltPhase;
    private float lightPhase;
    public static SeasonalCompanionTheme? ThemeForLook(string? choice)
    {
        if (choice == null || !(choice.StartsWith("Holiday", StringComparison.Ordinal) || choice.StartsWith("Costume", StringComparison.Ordinal))) return null;
        var name = choice[7..];
        return Enum.TryParse<SeasonalCompanionTheme>(name, out var theme) && Enum.IsDefined(theme)
            && name == theme.ToString() ? theme : null;
    }
    public static OrbitCorePlacement Place(CompanionVariantFrame source, CompanionVariantFrame normal,
        Vector2 position, Vector2 cellSize, bool mirrored)
    {
        // Keep the complete tilted ring inside the logical cell. The painted
        // planet stays centred and never rotates with its ring plane.
        var bodySize = new Vector2(normal.Body.Z - normal.Body.X, normal.Body.W - normal.Body.Y);
        var diameter = Math.Min(cellSize.Y * .64f, Math.Max(0, Math.Min(cellSize.X, cellSize.Y) - 4) / (1.47f * 1.04f));
        var factor = Math.Min(1, diameter / cellSize.Y * normal.SourceScale / Math.Max(bodySize.X, bodySize.Y));
        var paintedCell = cellSize * factor;
        var sourceCenter = new Vector2((normal.Body.X + normal.Body.Z) * .5f, (normal.Body.Y + normal.Body.W) * .5f);
        var center = position + cellSize * .5f;
        var offset = center - normal.Map(sourceCenter, Vector2.Zero, paintedCell, mirrored);
        var frame = source.GetFrame(Vector2.Zero, paintedCell, mirrored);
        var poseCenter = new Vector2((source.Body.X + source.Body.Z) * .5f, (source.Body.Y + source.Body.W) * .5f);
        var ringCenter = source.Map(poseCenter, Vector2.Zero, paintedCell, mirrored) + offset;
        return new(frame with { Position = frame.Position + offset }, ringCenter,
            Math.Min(bodySize.X, bodySize.Y) * paintedCell.Y / normal.SourceScale * .5f);
    }
    public void Update(float elapsed, bool animate, float speed)
    {
        if (!animate) return;
        var dt = (float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0)
            * (float.IsFinite(speed) ? Math.Clamp(speed, .2f, 3f) : 1);
        precession = (precession + dt * .24f) % MathF.Tau;
        tiltPhase = (tiltPhase + dt * .17f) % MathF.Tau;
        lightPhase = (lightPhase + dt * .65f) % MathF.Tau;
    }

    public Vector3 Point(float angle, float radius)
    {
        var rotation = Matrix4x4.CreateRotationX(.88f + MathF.Sin(tiltPhase) * .32f)
            * Matrix4x4.CreateRotationY(MathF.Sin(precession) * .44f)
            * Matrix4x4.CreateRotationZ(-.34f + MathF.Sin(precession) * .40f);
        return Vector3.Transform(new Vector3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 0), rotation);
    }

    public (Vector2 Minimum, Vector2 Maximum) Draw(ImDrawListPtr draw, Vector2 center, float coreRadius,
        Vector4 accent, bool front, SeasonalCompanionTheme? theme = null, bool decorations = true, bool mirrored = false)
        => DrawPosed(draw, center, coreRadius, accent, front, theme, decorations, mirrored, Matrix3x2.Identity);

    public static Matrix3x2 Movement(Vector2 center, float radius, float stride, float activity, bool mirrored, bool animate)
    {
        if (!animate || !float.IsFinite(activity) || activity <= 0 || !float.IsFinite(stride)
            || !float.IsFinite(radius) || radius <= 0 || !float.IsFinite(center.X) || !float.IsFinite(center.Y)) return Matrix3x2.Identity;
        var phase = (stride - MathF.Floor(stride)) * MathF.Tau;
        var strength = Math.Clamp(activity, 0, 1); var facing = mirrored ? -1 : 1;
        var angle = MathF.Sin(phase) * .055f * strength * facing;
        var offset = new Vector2(MathF.Sin(phase) * radius * .025f * facing, MathF.Sin(phase * 2) * radius * .035f) * strength;
        return Matrix3x2.CreateTranslation(-center) * Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(center + offset);
    }

    public (Vector2 Minimum, Vector2 Maximum) DrawPosed(ImDrawListPtr draw, Vector2 center, float coreRadius,
        Vector4 accent, bool front, SeasonalCompanionTheme? theme, bool decorations, bool mirrored, Matrix3x2 pose)
    {
        const int segments = 128;
        var firstVertex = draw.VtxBuffer.Size;
        var minimum = center; var maximum = center;
        var radius = coreRadius * 1.47f;
        var palette = PaletteFor(theme);
        var flags = draw.Flags;
        // Adjacent pieces form one band. Antialias only its inner/outer lines,
        // not every radial join, which would stripe the ring with dark seams.
        draw.Flags &= ~ImDrawListFlags.AntiAliasedFill;
        try
        {
            for (var i = 0; i < segments; i++)
            {
                var angle = i * MathF.Tau / segments;
                var next = (i + 1) * MathF.Tau / segments;
                var middle = Point((angle + next) * .5f, 1);
                if ((middle.Z >= 0) != front) continue;
                var innerA = Project(Point(angle, radius * .88f));
                var outerA = Project(Point(angle, radius * 1.04f));
                var outerB = Project(Point(next, radius * 1.04f));
                var innerB = Project(Point(next, radius * .88f));
                var shimmer = .5f + .5f * MathF.Sin(angle * 3 - lightPhase);
                var baseColor = Vector4.Lerp(new(.42f, .63f, .93f, 1), accent, .5f);
                var color = Vector4.Lerp(baseColor, new(1, .93f, .72f, 1), .25f + shimmer * .5f);
                if (palette is { } holiday) color = Vector4.Lerp(holiday.Base, holiday.Light, .25f + shimmer * .5f);
                color.W = front ? .98f : .78f;
                draw.AddQuadFilled(innerA, outerA, outerB, innerB, ImGui.ColorConvertFloat4ToU32(color));
                draw.AddLine(outerA, outerB, palette is { } edge ? Ink(edge.Light, .87f) : 0xDDFBE4C2u, Math.Max(.65f, coreRadius * .025f));
                draw.AddLine(innerA, innerB, palette is { } inner ? Ink(inner.Mark, .60f) : 0x99F6F0E0u, Math.Max(.55f, coreRadius * .018f));
            }
            if (decorations && palette is { } motif && theme is { } selected)
                DrawMotifs(draw, center, radius, selected, motif, front);
        }
        finally { draw.Flags = flags; }
        // Read only this bounded ring pass. Native antialias miters and line
        // pixel alignment extend beyond the projected mathematical endpoints.
        for (var index = firstVertex; index < draw.VtxBuffer.Size; index++)
        {
            var point = draw.VtxBuffer[index].Pos;
            // Reflect the completed band, outlines and motifs together, retaining
            // their depth split and native antialiasing when the painted body turns.
            if (mirrored)
            {
                point.X = center.X * 2 - point.X;
            }
            if (pose != Matrix3x2.Identity) point = Vector2.Transform(point, pose);
            if (mirrored || pose != Matrix3x2.Identity)
                unsafe { ((ImDrawVert*)draw.VtxBuffer.Data)[index].Pos = point; }
            minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
        }
        return (minimum, maximum);
        Vector2 Project(Vector3 p) => center + new Vector2(p.X, p.Y);
    }

    private readonly record struct RingPalette(Vector4 Base, Vector4 Light, Vector4 Mark);
    private static RingPalette? PaletteFor(SeasonalCompanionTheme? theme) => theme switch
    {
        SeasonalCompanionTheme.Heavensturn => new(Rgb(0xA92F39), Rgb(0xF4CD70), Rgb(0xFFF0C9)),
        SeasonalCompanionTheme.Valentione => new(Rgb(0xB2467E), Rgb(0xF7B6CB), Rgb(0xFFF1EE)),
        SeasonalCompanionTheme.LittleLadies => new(Rgb(0xA577AE), Rgb(0xF5CDE0), Rgb(0xFFEAB0)),
        SeasonalCompanionTheme.HatchingTide => new(Rgb(0x659F8B), Rgb(0xC9E5A9), Rgb(0xFFF0C0)),
        SeasonalCompanionTheme.MakeItRain => new(Rgb(0xA86F31), Rgb(0xF7D469), Rgb(0xFFF7C6)),
        SeasonalCompanionTheme.MoonfireFaire => new(Rgb(0x237FAD), Rgb(0x6DDEC9), Rgb(0xFFE0A6)),
        SeasonalCompanionTheme.TheRising => new(Rgb(0x5A459C), Rgb(0xAAA7EF), Rgb(0xF6DCAF)),
        SeasonalCompanionTheme.AllSaintsWake => new(Rgb(0x654187), Rgb(0xE39A43), Rgb(0xF8EACA)),
        SeasonalCompanionTheme.Starlight => new(Rgb(0x317F79), Rgb(0xA7E0DB), Rgb(0xFFF1D2)),
        _ => null,
    };
    private static Vector4 Rgb(uint value) => new((value >> 16 & 255) / 255f, (value >> 8 & 255) / 255f, (value & 255) / 255f, 1);
    private static uint Ink(Vector4 color, float alpha) { color.W = alpha; return ImGui.ColorConvertFloat4ToU32(color); }

    private void DrawMotifs(ImDrawListPtr draw, Vector2 center, float radius, SeasonalCompanionTheme theme, RingPalette palette, bool front)
    {
        // Every mark lies inside the existing band. Its plane/depth come from Point,
        // with no extra clock, screen-space bobbing or enlarged hit/layout bounds.
        var size = radius * .055f;
        var ink = Ink(palette.Mark, front ? .98f : .78f);
        var dark = Ink(palette.Base, front ? .98f : .78f);
        Span<Vector2> heart = stackalloc Vector2[24];
        for (var n = 0; n < 10; n++)
        {
            var angle = n * MathF.Tau / 10 + .11f;
            var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tangent = new Vector2(-radial.Y, radial.X);
            Vector3 Plane(Vector2 local)
            {
                var p = radial * (radius * .96f + local.Y * size) + tangent * (local.X * size);
                return Point(MathF.Atan2(p.Y, p.X), p.Length());
            }
            void Triangle(Vector2 a, Vector2 b, Vector2 c, uint color)
            {
                // Clip in the ring plane at Z=0 so marks crossing the planet's depth
                // boundary are split, instead of jumping wholesale between passes.
                Span<Vector3> input = stackalloc Vector3[3] { Plane(a), Plane(b), Plane(c) };
                Span<Vector3> clipped = stackalloc Vector3[4]; var count = 0;
                for (var j = 0; j < input.Length; j++)
                {
                    var p = input[j]; var q = input[(j + 1) % input.Length];
                    var insideP = front ? p.Z >= 0 : p.Z < 0; var insideQ = front ? q.Z >= 0 : q.Z < 0;
                    if (insideP) clipped[count++] = p;
                    if (insideP != insideQ) clipped[count++] = Vector3.Lerp(p, q, p.Z / (p.Z - q.Z));
                }
                for (var j = 1; j + 1 < count; j++)
                    draw.AddTriangleFilled(center + new Vector2(clipped[0].X, clipped[0].Y),
                        center + new Vector2(clipped[j].X, clipped[j].Y), center + new Vector2(clipped[j + 1].X, clipped[j + 1].Y), color);
            }
            void Polygon(ReadOnlySpan<Vector2> points, uint color)
            {
                var middle = Vector2.Zero;
                foreach (var point in points) middle += point;
                middle /= points.Length;
                for (var j = 0; j < points.Length; j++) Triangle(middle, points[j], points[(j + 1) % points.Length], color);
            }
            void Disc(Vector2 at, Vector2 half, uint color)
            {
                for (var j = 0; j < 12; j++)
                {
                    var a = j * MathF.Tau / 12; var b = (j + 1) * MathF.Tau / 12;
                    Triangle(at, at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * half,
                        at + new Vector2(MathF.Cos(b), MathF.Sin(b)) * half, color);
                }
            }
            void Stroke(Vector2 a, Vector2 b, float width, uint color)
            {
                var normal = Vector2.Normalize(new Vector2(a.Y - b.Y, b.X - a.X)) * width * .5f;
                Triangle(a - normal, a + normal, b + normal, color); Triangle(a - normal, b + normal, b - normal, color);
            }
            switch (theme)
            {
                case SeasonalCompanionTheme.Heavensturn:
                    Polygon([new(-.5f,-.65f), new(.5f,-.65f), new(.7f,0), new(.5f,.65f), new(-.5f,.65f), new(-.7f,0)], ink);
                    Stroke(new(0,-.65f), new(0,.65f), .18f, dark);
                    Stroke(new(-.45f,-.82f), new(.45f,-.82f), .20f, ink);
                    Stroke(new(-.45f,.82f), new(.45f,.82f), .20f, ink);
                    break;
                case SeasonalCompanionTheme.Valentione:
                    for (var j = 0; j < heart.Length; j++)
                    {
                        var t = j * MathF.Tau / heart.Length;
                        heart[j] = new(MathF.Pow(MathF.Sin(t), 3) * .88f,
                            -(13 * MathF.Cos(t) - 5 * MathF.Cos(2*t) - 2 * MathF.Cos(3*t) - MathF.Cos(4*t)) / 18);
                    }
                    Polygon(heart, ink); break;
                case SeasonalCompanionTheme.LittleLadies:
                    for (var j = 0; j < 5; j++)
                    { var t = j * MathF.Tau / 5; Disc(new Vector2(MathF.Cos(t), MathF.Sin(t)) * .47f, new(.36f), ink); }
                    Disc(Vector2.Zero, new(.22f), dark); break;
                case SeasonalCompanionTheme.HatchingTide:
                    Disc(Vector2.Zero, new(.62f,.88f), ink);
                    Stroke(new(-.49f,-.05f), new(0,.18f), .18f, dark);
                    Stroke(new(0,.18f), new(.49f,-.05f), .18f, dark); break;
                case SeasonalCompanionTheme.MakeItRain:
                    Disc(Vector2.Zero, new(.85f), ink); Disc(Vector2.Zero, new(.55f), dark);
                    Polygon([new(-.2f,-.2f),new(.2f,-.2f),new(.2f,.2f),new(-.2f,.2f)], ink); break;
                case SeasonalCompanionTheme.MoonfireFaire:
                    for (var j = 0; j < 6; j++)
                    {
                        var x = -.85f + j * .28f; var next = x + .28f;
                        Stroke(new(x,MathF.Sin(x*4)*.4f),new(next,MathF.Sin(next*4)*.4f),.25f,ink);
                    }
                    Disc(new(-.4f,-.65f),new(.2f),ink); break;
                case SeasonalCompanionTheme.TheRising:
                    Polygon([new(0,-.95f),new(.62f,-.15f),new(.4f,.5f),new(0,.95f),new(-.4f,.5f),new(-.62f,-.15f)], ink);
                    Triangle(new(0,-.95f),new(0,.95f),new(.62f,-.15f),dark); break;
                case SeasonalCompanionTheme.AllSaintsWake:
                    Polygon([new(-.65f,.7f),new(-.65f,-.2f),new(-.45f,-.7f),new(0,-.9f),new(.45f,-.7f),new(.65f,-.2f),
                        new(.65f,.7f),new(.25f,.45f),new(0,.75f),new(-.25f,.45f)], ink);
                    Disc(new(-.25f,-.15f),new(.13f,.2f),dark); Disc(new(.25f,-.15f),new(.13f,.2f),dark); break;
                case SeasonalCompanionTheme.Starlight:
                    for (var j = 0; j < 6; j++)
                    {
                        var t = j * MathF.Tau / 6; var direction = new Vector2(MathF.Cos(t), MathF.Sin(t));
                        var side = new Vector2(-direction.Y,direction.X);
                        Stroke(Vector2.Zero,direction*.9f,.16f,ink);
                        Stroke(direction*.55f,direction*.36f+side*.22f,.13f,ink);
                        Stroke(direction*.55f,direction*.36f-side*.22f,.13f,ink);
                    }
                    break;
            }
        }
    }
}
