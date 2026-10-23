using System.Numerics;

namespace OmniKit.Interaction;

/// <summary>Screen-space aim ranking after native world eligibility checks.</summary>
internal static class SoftTargetGeometry
{
    internal static bool TryScore(Vector2 screenPosition, bool projectedOnScreen,
        Vector2 viewportPosition, Vector2 viewportSize, float aimWidth, float distance,
        float maximumDistance, bool incumbent, out float score)
    {
        score = float.MaxValue;
        if (!projectedOnScreen || !Finite(screenPosition) || !Finite(viewportPosition) || !Finite(viewportSize)
            || viewportSize.X < 1 || viewportSize.Y < 1 || !float.IsFinite(aimWidth) || aimWidth <= 0
            || !float.IsFinite(distance) || distance < 0 || !float.IsFinite(maximumDistance)
            || maximumDistance <= 0 || distance > maximumDistance)
            return false;

        var local = screenPosition - viewportPosition;
        // Projection success alone must not admit offscreen points. Never retain
        // an incumbent outside the viewport or beyond the configured world range.
        if (local.X < 0 || local.Y < 0 || local.X >= viewportSize.X || local.Y >= viewportSize.Y)
            return false;

        var radius = Math.Min(viewportSize.X, viewportSize.Y) * aimWidth;
        var offset = Vector2.Abs(local - viewportSize * .5f);
        // Horizontal aim chooses what is ahead. Pitch may move a fully visible
        // target vertically without pushing it out of a circular crosshair test.
        // The small retention margin applies only to an already eligible target.
        if (offset.X > radius * (incumbent ? 1.15f : 1f))
            return false;

        score = offset.X / radius + offset.Y / (viewportSize.Y * .5f) * .1f
            + distance / maximumDistance * .1f - (incumbent ? .075f : 0);
        return float.IsFinite(score);
    }

    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
