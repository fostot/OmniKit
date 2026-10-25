using System.Numerics;

namespace OmniKit.Ui;

/// <summary>Saved offsets use head widths in the body's local axes, never screen pixels.</summary>
internal static class CompanionAccessoryPlacement
{
    public static CompanionAccessoryAdjustment Find(Configuration config, string avatar, string look, string theme)
        => config.CompanionAccessoryFits.FirstOrDefault(f => f.Avatar == avatar && f.Look == look && f.Theme == theme)
            ?? new(avatar, look, theme, 0, 0, 1);

    public static SeasonalOverlayQuad Apply(SeasonalOverlayQuad quad, SeasonalBodyTransform body,
        SeasonalAccessoryFit fit, CompanionAccessoryAdjustment adjustment)
    {
        var pivot = body.Map(fit.Anchor);
        var shifted = body.Map(AdjustedAnchor(fit, adjustment));
        var scale = float.IsFinite(adjustment.Scale) ? Math.Clamp(adjustment.Scale, .25f, 3) : 1;
        var angle = float.IsFinite(adjustment.RotationDegrees) ? Math.Clamp(adjustment.RotationDegrees, -180, 180) * MathF.PI / 180 : 0;
        var cosine = MathF.Cos(angle); var sine = MathF.Sin(angle);
        Vector2 Point(Vector2 point)
        {
            if (angle == 0) return shifted + (point - pivot) * scale;
            // Rotate in the head's axes so a mirrored or rolling body carries the fit with it.
            var local = LocalDelta(body, fit, point - pivot);
            var rotated = new Vector2(local.X * cosine - local.Y * sine, local.X * sine + local.Y * cosine);
            return body.Map(AdjustedAnchor(fit, adjustment) + rotated * fit.HeadWidth * scale);
        }
        return quad with { A = Point(quad.A), B = Point(quad.B), C = Point(quad.C), D = Point(quad.D) };
    }

    public static Vector2 AdjustedAnchor(SeasonalAccessoryFit fit, CompanionAccessoryAdjustment adjustment)
        => fit.Anchor + new Vector2(Safe(adjustment.OffsetX), Safe(adjustment.OffsetY)) * fit.HeadWidth;

    public static Vector2 LocalDelta(SeasonalBodyTransform body, SeasonalAccessoryFit fit, Vector2 screenDelta)
    {
        var origin = body.Map(fit.Anchor);
        var x = body.Map(fit.Anchor + new Vector2(fit.HeadWidth, 0)) - origin;
        var y = body.Map(fit.Anchor + new Vector2(0, fit.HeadWidth)) - origin;
        var determinant = x.X * y.Y - x.Y * y.X;
        if (!float.IsFinite(determinant) || Math.Abs(determinant) < .00001f) return Vector2.Zero;
        return new((screenDelta.X * y.Y - screenDelta.Y * y.X) / determinant,
            (x.X * screenDelta.Y - x.Y * screenDelta.X) / determinant);
    }

    public static float Padding(CompanionAccessoryAdjustment adjustment)
        => SeasonalCompanionRenderer.RequiredVisualPaddingFraction * Math.Max(1,
            float.IsFinite(adjustment.Scale) ? Math.Clamp(adjustment.Scale, .25f, 3) : 1)
            + new Vector2(Safe(adjustment.OffsetX), Safe(adjustment.OffsetY)).Length();

    public static bool IsDefault(CompanionAccessoryAdjustment adjustment)
        => adjustment.OffsetX == 0 && adjustment.OffsetY == 0 && adjustment.Scale == 1 && adjustment.RotationDegrees == 0;
    private static float Safe(float value) => float.IsFinite(value) ? Math.Clamp(value, -1, 1) : 0;
}
