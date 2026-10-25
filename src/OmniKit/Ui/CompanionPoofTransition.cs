using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

internal readonly record struct CompanionPoofDecision(bool CanApplyAppearance, bool IsActive,
    string AvatarToApply, float Coverage, float Progress);

/// <summary>Cosmetic swap gate only. The caller retains position/motion and applies the captured avatar at the opaque peak.</summary>
internal sealed class CompanionPoofTransition
{
    public const float CoverSeconds = .18f;
    public const float RevealSeconds = .30f;
    public const float PaddingFraction = .44f;
    private string? target;
    private string? pending;
    private bool revealing;
    private float elapsed;
    public bool IsActive => target != null;
    public float Coverage => !IsActive ? 0 : revealing ? 1 - Smooth(elapsed / RevealSeconds) : Smooth(elapsed / CoverSeconds);
    public float Progress => !IsActive ? 0 : (revealing ? CoverSeconds + elapsed : elapsed) / (CoverSeconds + RevealSeconds);

    public CompanionPoofDecision Update(string currentAvatar, string requestedAvatar, bool canSwitch, bool enabled, float deltaSeconds)
    {
        if (!enabled)
        {
            Reset();
            return new(true, false, requestedAvatar, 0, 0);
        }
        var dt = float.IsFinite(deltaSeconds) ? Math.Clamp(deltaSeconds, 0, .05f) : 0;
        if (IsActive)
        {
            // New automatic requests wait for the current reveal; they cannot change
            // the body hidden inside an already expanding poof.
            if (requestedAvatar != currentAvatar && requestedAvatar != target) pending = requestedAvatar;
            else if (requestedAvatar == target) pending = null;
            if (!revealing && !canSwitch)
            {
                pending ??= target;
                target = null; elapsed = 0;
                return new(false, false, currentAvatar, 0, 0);
            }
            elapsed += dt;
            if (!revealing && elapsed >= CoverSeconds)
            {
                revealing = true;
                elapsed = 0; // Always paint one fully opaque frame, even after a delayed update.
                return Decision(true, target!);
            }
            if (revealing && elapsed >= RevealSeconds)
            {
                target = null; revealing = false; elapsed = 0;
                // Do not start another cover until the caller has rendered the completed frame.
                return new(false, false, currentAvatar, 0, 0);
            }
            return Decision(false, currentAvatar);
        }

        if (requestedAvatar != currentAvatar) pending = requestedAvatar;
        if (pending == currentAvatar) pending = null;
        if (pending == null) return new(true, false, currentAvatar, 0, 0);
        if (!canSwitch) return new(false, false, currentAvatar, 0, 0);
        target = pending; pending = null; revealing = false; elapsed = 0;
        return Decision(false, currentAvatar);
    }

    public void Reset() { target = pending = null; revealing = false; elapsed = 0; }

    /// <summary>Draw after the body and hat. Bounds are their union; RGB is tinted softly, but peak opacity is always one.</summary>
    public (Vector2 Minimum, Vector2 Maximum) Draw(ImDrawListPtr draw, Vector2 minimum, Vector2 maximum, Vector4 color)
    {
        var size = maximum - minimum;
        var opacity = Coverage;
        if (!IsActive || opacity <= .001f || !Finite(minimum) || !Finite(maximum) || size.X <= 0 || size.Y <= 0)
            return (minimum, minimum);
        var reveal = revealing ? Smooth(elapsed / RevealSeconds) : 0;
        var growth = revealing ? 1 + reveal * .04f : .18f + Smooth(elapsed / CoverSeconds) * .82f;
        var center = (minimum + maximum) * .5f;
        var edge = Math.Min(size.X, size.Y);
        var half = size * (.5f * growth);
        var rgb = new Vector3(color.X, color.Y, color.Z);
        if (!Finite(rgb)) rgb = new(.65f, .82f, 1);
        rgb = Vector3.Lerp(Vector3.Clamp(rgb, Vector3.Zero, Vector3.One), Vector3.One, .55f);
        uint Ink(float alpha, float light = 0) => ImGui.ColorConvertFloat4ToU32(new Vector4(Vector3.Lerp(rgb, Vector3.One, light), Math.Clamp(alpha, 0, 1)));

        // The ellipse's .72 radii exceed the .707 needed to enclose all four
        // normalized rectangle corners. The body/hat swap is fully concealed.
        for (var i = 0; i < 48; i++)
        {
            var angle = i * MathF.Tau / 48;
            draw.PathLineTo(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size * (.72f * growth));
        }
        draw.PathFillConvex(Ink(opacity));
        for (var i = 0; i < 10; i++)
        {
            var angle = i * MathF.Tau / 10 + .10f;
            var radius = edge * (.15f + i % 3 * .012f) * growth;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size * ((.66f + i % 3 * .007f) * growth);
            var p = center + offset;
            draw.AddCircleFilled(p, radius + edge * .020f, Ink(opacity * .10f), 24);
            draw.AddCircleFilled(p, radius + edge * .012f, Ink(opacity * .22f), 24);
            draw.AddCircleFilled(p, radius, Ink(opacity, .07f + i % 3 * .045f), 24);
        }
        // A few overlapping highlights make the core read as a cloud instead of a panel.
        for (var i = 0; i < 4; i++)
        {
            var p = center + new Vector2((i % 2 - .5f) * half.X, (i / 2 - .5f) * half.Y);
            draw.AddCircleFilled(p, edge * (.18f + i % 2 * .02f) * growth, Ink(opacity * .13f, .8f), 24);
        }
        for (var i = 0; i < 6; i++)
        {
            var angle = i * MathF.Tau / 6 + .2f;
            var p = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size * (.61f + reveal * .05f);
            var radius = edge * .026f * opacity;
            var ink = Ink(opacity * .85f, .9f);
            draw.AddLine(p - new Vector2(radius, 0), p + new Vector2(radius, 0), ink, Math.Max(.6f, edge * .008f));
            draw.AddLine(p - new Vector2(0, radius), p + new Vector2(0, radius), ink, Math.Max(.6f, edge * .008f));
        }
        return (minimum - size * PaddingFraction, maximum + size * PaddingFraction);
    }

    private CompanionPoofDecision Decision(bool apply, string avatar) => new(apply, IsActive, avatar, Coverage, Progress);
    private static float Smooth(float value) { var t = Math.Clamp(value, 0, 1); return t * t * (3 - 2 * t); }
    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
