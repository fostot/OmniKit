using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

internal static class CompanionStyleRenderer
{
    public static void DrawHome(ImDrawListPtr draw, ImTextureID texture, CompanionHomeStyleFrame frame, uint tint = 0xFFFFFFFFu)
        => DrawQuad(draw, texture, frame.Image, tint);

    // Repaint the walls after the visitor. Cached convex doorway strips preserve the open arch.
    public static void DrawHomeWalls(ImDrawListPtr draw, ImTextureID texture, CompanionHomeStyleFrame frame, uint tint = 0xFFFFFFFFu)
    {
        for (var i = 0; i < frame.WallCount; i++) DrawQuad(draw, texture, frame.Wall(i), tint);
    }

    public static void DrawQuad(ImDrawListPtr draw, ImTextureID texture, CompanionStyleQuad quad, uint tint = 0xFFFFFFFFu)
        => draw.AddImageQuad(texture, quad.A, quad.B, quad.C, quad.D, quad.UvA, quad.UvB, quad.UvC, quad.UvD, tint);
}
