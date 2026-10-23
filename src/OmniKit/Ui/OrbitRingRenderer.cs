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
        return new(frame with { Position = frame.Position + offset }, center,
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
        Vector4 accent, bool front)
    {
        const int segments = 128;
        var firstVertex = draw.VtxBuffer.Size;
        var minimum = center; var maximum = center;
        var radius = coreRadius * 1.47f;
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
                color.W = front ? .98f : .78f;
                draw.AddQuadFilled(innerA, outerA, outerB, innerB, ImGui.ColorConvertFloat4ToU32(color));
                draw.AddLine(outerA, outerB, 0xDDFBE4C2u, Math.Max(.65f, coreRadius * .025f));
                draw.AddLine(innerA, innerB, 0x99F6F0E0u, Math.Max(.55f, coreRadius * .018f));
            }
        }
        finally { draw.Flags = flags; }
        // Read only this bounded ring pass. Native antialias miters and line
        // pixel alignment extend beyond the projected mathematical endpoints.
        for (var index = firstVertex; index < draw.VtxBuffer.Size; index++)
        {
            var point = draw.VtxBuffer[index].Pos;
            minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
        }
        return (minimum, maximum);
        Vector2 Project(Vector3 p) => center + new Vector2(p.X, p.Y);
    }
}
