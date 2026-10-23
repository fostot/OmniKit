using System.Numerics;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Interaction;

internal static unsafe class OutlineChecks
{
    private readonly record struct Vertex(Vector2 Position, Vector2 Uv, uint Color);

    public static int Run(SoftwareRenderer renderer, string output)
    {
        // The comparison sheet contains many HUDs; the renderer already honors
        // per-command vertex offsets when the draw list exceeds 16-bit indices.
        ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        var checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
        const string prefix = "AV";
        const string name = "ATAR Étoile 旅人 😀";
        const string suffix = "To · Preview";
        const uint black = 0xE6000000;

        Vertex[] Render(HudTextEffect effect, float thickness, float size, double time,
            bool outline = true, bool animated = true, bool bottomRight = false)
        {
            ImGui.NewFrame();
            var font = ImGui.GetFont(); var draw = ImGui.GetForegroundDrawList();
            var reference = ImGui.GetBackgroundDrawList();
            var metrics = HudTextRenderer.Measure(font, size, prefix, name, suffix);
            var padding = HudTextRenderer.Padding(effect, size, 2, outline, thickness);
            var bounds = metrics.InkMaximum - metrics.InkMinimum + padding * 2;
            var corner = bottomRight ? ImGui.GetIO().DisplaySize - bounds - Vector2.One : Vector2.One;
            var position = corner + padding - metrics.InkMinimum;
            var namePosition = position + new Vector2(HudTextRenderer.Measure(font, size, prefix).Size.X, 0);
            var suffixPosition = namePosition + new Vector2(HudTextRenderer.Measure(font, size, name).Size.X, 0);
            void Expected(Vector2 offset)
            {
                reference.AddText(font, size, position + offset, 0xFFFFFFFF, prefix, 0);
                reference.AddText(font, size, namePosition + offset, 0xFFFFFFFF, name, 0);
                reference.AddText(font, size, suffixPosition + offset, 0xFFFFFFFF, suffix, 0);
            }
            Expected(Vector2.Zero);
            var coreCount = reference.VtxBuffer.Size;
            HudTextRenderer.Draw(draw, font, size, position, prefix, name, suffix, effect, outline, 2, 3, animated, time, thickness);
            var result = Enumerable.Range(0, draw.VtxBuffer.Size).Select(i =>
                new Vertex(draw.VtxBuffer[i].Pos, draw.VtxBuffer[i].Uv, draw.VtxBuffer[i].Col)).ToArray();
            Check(result.All(v => float.IsFinite(v.Position.X) && float.IsFinite(v.Position.Y)
                && v.Position.X >= corner.X - .01f && v.Position.Y >= corner.Y - .01f
                && v.Position.X <= corner.X + bounds.X + .01f && v.Position.Y <= corner.Y + bounds.Y + .01f),
                $"{effect}/{thickness} must fit the measured outline and effect hit bounds at either screen corner.");
            Check(result.All(v => v.Position.X >= 0 && v.Position.Y >= 0
                && v.Position.X <= ImGui.GetIO().DisplaySize.X && v.Position.Y <= ImGui.GetIO().DisplaySize.Y),
                "Maximum-thickness native vertices must remain inside the viewport.");
            if (outline)
            {
                var normalized = Math.Clamp(float.IsFinite(thickness) ? thickness : 1, .5f, 4);
                var rings = (int)MathF.Ceiling(normalized);
                var outlineVertices = result.Where(v => v.Color == black).ToArray();
                Check(outlineVertices.Length == coreCount * 8 * rings && rings <= 4,
                    "Only primary glyph runs receive at most 32 outline passes; colored echoes and wisps do not.");
                for (var ring = 1; ring <= rings; ring++)
                for (var y = -1; y <= 1; y++)
                for (var x = -1; x <= 1; x++)
                    if (x != 0 || y != 0) Expected(new Vector2(x, y) * (Math.Clamp(size / 17, .1f, 8) * normalized * ring / rings));
                Check(outlineVertices.Select((v, i) => v.Position == reference.VtxBuffer[coreCount + i].Pos
                    && v.Uv == reference.VtxBuffer[coreCount + i].Uv).All(v => v),
                    "Outline quads must surround the displayed prefix, name and suffix at their exact native glyph positions.");
                var endOfOutline = Array.FindLastIndex(result, v => v.Color == black) + 1;
                Check(result.Skip(endOfOutline).Take(coreCount).Select((v, i) => v.Position == reference.VtxBuffer[i].Pos
                    && v.Uv == reference.VtxBuffer[i].Uv && v.Color >> 24 == 255).All(v => v),
                    "Effects must retain the exact opaque primary glyphs above their matching outline, including glitch.");
            }
            ImGui.Render();
            return result;
        }

        foreach (var effect in Enum.GetValues<HudTextEffect>())
        foreach (var thickness in new[] { .5f, 1f, 4f })
        foreach (var size in new[] { 17f, 34f })
        {
            Render(effect, thickness, size, .13);
            Render(effect, thickness, size, 1.8, bottomRight: true);
        }
        foreach (var effect in Enum.GetValues<HudTextEffect>())
        {
            var thinOff = Render(effect, .5f, 24, 1.1, outline: false);
            var thickOff = Render(effect, 4, 24, 1.1, outline: false);
            Check(thinOff.SequenceEqual(thickOff), "Outline width has no effect when its independent toggle is off.");
            var frozen = Render(effect, 4, 24, 0, animated: false);
            Check(frozen.SequenceEqual(Render(effect, 4, 24, 99, animated: false)), "Thick outlined effects freeze deterministically.");
        }
        foreach (var (invalid, expected) in new[] { (float.NaN, 1f), (float.PositiveInfinity, 1f), (-1f, .5f), (99f, 4f) })
            Check(Render(HudTextEffect.None, invalid, 24, 0).SequenceEqual(Render(HudTextEffect.None, expected, 24, 0)),
                "Renderer must defensively bound invalid outline widths even before settings validation.");
        Check(HudTextRenderer.Padding(HudTextEffect.None, 34, 2, true, 4).X
            > HudTextRenderer.Padding(HudTextEffect.None, 34, 2, true, .5f).X,
            "Thicker outlines reserve larger stable clamping and hit bounds.");

        foreach (var time in new[] { .13, 1.8 })
        {
            ImGui.GetIO().DisplaySize = new Vector2(1400, 820);
            ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var row = 0;
            var widths = new[] { .5f, 1f, 2f, 4f };
            for (var column = 0; column < widths.Length; column++)
                draw.AddText(new Vector2(225 + column * 290, 5), 0xFFFFFFFF, $"Outline {widths[column]:0.0}");
            foreach (var effect in Enum.GetValues<HudTextEffect>())
            {
                var y = 38 + row++ * 69;
                draw.AddText(new Vector2(12, y + 5), 0xFFB7BEC9, effect.ToString());
                for (var column = 0; column < widths.Length; column++)
                    HudTextRenderer.Draw(draw, ImGui.GetFont(), 26, new Vector2(225 + column * 290, y),
                        "[E]  ", "Moonlit AV", " · Preview", effect, true, 2, 3, true, time, widths[column]);
            }
            ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"outline-effects-{time:0.00}.png"));
        }
        return checks;
    }
}
