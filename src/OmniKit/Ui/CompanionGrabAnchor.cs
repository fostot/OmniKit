using System.Numerics;

namespace OmniKit.Ui;

/// <summary>Shared screen-space contact point for a carried object and its optional hand.</summary>
internal static class CompanionGrabAnchor
{
    internal const float CenteringSeconds = .18f;

    public static Vector2 Center(Vector2 position, Vector2 size) => position + size * .5f;

    public static Vector2 CarryTopAt(Vector2 pointer, Vector2 size, Vector2 initialOffset, float seconds)
    {
        var time = Math.Clamp(float.IsFinite(seconds) ? seconds / CenteringSeconds : 0, 0, 1);
        var ease = time * time * (3 - 2 * time);
        var centered = size * .5f + new Vector2(0, Math.Clamp(size.Y * .07f, 3, 12));
        return pointer - Vector2.Lerp(initialOffset, centered, ease);
    }

    // Each source cell's wrist is slightly offset; preserve the inspected
    // relative grip calibration without allowing texture size to move the body.
    public static Vector2 GripInCell(int frame) => frame switch
    {
        0 or 1 => new Vector2(.51f, .805f),
        3 => new Vector2(.485f, .77f),
        _ => new Vector2(.5f, .77f),
    };

    public static Vector2 GripTopAt(Vector2 center, Vector2 handSize, int frame)
        => center - handSize * GripInCell(frame);
}
