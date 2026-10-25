using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

/// <summary>Face-only lids on the normal body, including its roll/float and the same optional mesh deformation.</summary>
internal static class CompanionBlinkRenderer
{
    public static void Draw(ImDrawListPtr draw, ImTextureID closedTexture, CompanionBlinkProfile profile,
        SeasonalBodyTransform body, float closure, uint tint, Func<Vector2, Vector2>? deform = null)
    {
        if (!float.IsFinite(closure) || closure <= .001f || body.SourceRect != profile.NormalBounds) return;
        Vector2 Point(Vector2 source)
        {
            var mapped = body.Map(source);
            return deform == null ? mapped : deform(mapped);
        }
        foreach (var eye in profile.Eyes)
            CompanionVariantRenderer.DrawEyeLid(draw, closedTexture, eye.Ellipse, eye.Sample,
                profile.ClosedSheetSize, Point, closure, tint, 1,
                profile.Avatar == "Nib" ? 0x00F3E8E2u : 0x00291B27u);
    }
}
