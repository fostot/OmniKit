using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Interaction;

internal readonly record struct HudTextMetrics(Vector2 Size, Vector2 InkMinimum, Vector2 InkMaximum);

/// <summary>Native font-atlas effects with a stable opaque core and bounded decorative passes.</summary>
internal static unsafe class HudTextRenderer
{
    private const uint Core = 0xFFF0F3F6;
    private static readonly Vector3 White = new(.965f, .953f, .941f);

    public static HudTextMetrics Measure(ImFontPtr font, float size, string text)
    {
        var scale = size / font.FontSize;
        var measured = NativeMeasure(font, size, text);
        var inkMinimum = Vector2.Zero; var overhang = Vector2.Zero;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value is '\r' or '\n') continue;
            var codepoint = Codepoint(rune);
            var glyph = font.FindGlyph(codepoint);
            if (glyph != null && glyph->Visible != 0)
            {
                inkMinimum = Vector2.Min(inkMinimum, new Vector2(glyph->X0, glyph->Y0) * scale);
                overhang = Vector2.Max(overhang, new Vector2(glyph->X1 - glyph->AdvanceX, glyph->Y1 - font.FontSize) * scale);
            }
        }
        return new HudTextMetrics(measured, inkMinimum, measured + overhang);
    }

    public static string Elide(ImFontPtr font, float size, string text, float width)
    {
        if (Measure(font, size, text).Size.X <= width) return text;
        var remaining = width - Measure(font, size, "…").Size.X;
        if (remaining < 0) return string.Empty;
        var boundaries = new List<int> { 0 }; var length = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            length += rune.Utf16SequenceLength; boundaries.Add(length);
        }
        var low = 0; var high = boundaries.Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (NativeMeasure(font, size, text[..boundaries[middle]] + "…").X <= width) low = middle;
            else high = middle - 1;
        }
        return text[..boundaries[low]] + "…";
    }

    /// <summary>Maximum decoration reach, independent of phase, so clamping/hit bounds never pulse.</summary>
    public static Vector2 Padding(HudTextEffect effect, float size, float intensity, bool outline)
    {
        var scale = Scale(size); var amount = Amount(intensity);
        var extent = effect switch
        {
            HudTextEffect.Shadow => new Vector2(1.5f),
            HudTextEffect.SoftGlow => new Vector2(2),
            HudTextEffect.NeonPulse => new Vector2(3),
            HudTextEffect.ChromaticGlitch => new Vector2(3.5f, 1.5f),
            HudTextEffect.Hologram => new Vector2(1.5f, 2),
            HudTextEffect.Flame => new Vector2(1.5f, 5.5f),
            HudTextEffect.Frost => new Vector2(2.5f, 4),
            _ => Vector2.Zero,
        } * (scale * amount);
        if (outline) extent = Vector2.Max(extent, new Vector2(scale));
        return new Vector2(MathF.Ceiling(extent.X + 1), MathF.Ceiling(extent.Y + 1));
    }

    public static void Draw(ImDrawListPtr draw, ImFontPtr font, float size, Vector2 position,
        string prefix, string name, string suffix, HudTextEffect effect, bool outline,
        float intensity, float speed, bool animated, double seconds)
    {
        var amount = Amount(intensity); var scale = Scale(size);
        if (!Enum.IsDefined(effect) || amount == 0) effect = HudTextEffect.None;
        var time = animated && double.IsFinite(seconds)
            ? (float)((seconds % 10000) * Math.Clamp(float.IsFinite(speed) ? speed : 1, .1f, 3)) : 0;
        var text = prefix + name + suffix;
        var metrics = Measure(font, size, text);
        var width = Math.Max(1, metrics.Size.X);
        void Layer(Vector2 offset, uint color) => draw.AddText(font, size, position + offset, color, text, 0);
        void Ring(float radius, uint color)
        {
            for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
                if (x != 0 || y != 0) Layer(new Vector2(x, y) * radius, color);
        }
        void Glow(float radius, Vector3 color, float opacity)
        {
            // Nested low-opacity samples fill the halo instead of leaving eight
            // sharp, widely separated copies of the same glyph.
            for (var ring = 1; ring <= 2; ring++)
            for (var direction = 0; direction < 4; direction++)
            {
                var angle = direction * MathF.PI * .5f + (ring == 2 ? MathF.PI * .25f : 0);
                Layer(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius * ring * .5f), Pack(color, opacity / ring));
            }
        }

        switch (effect)
        {
            case HudTextEffect.Shadow:
                Layer(new Vector2(1.5f * scale * amount), Pack(Vector3.Zero, .9f * Math.Min(1, amount)));
                break;
            case HudTextEffect.SoftGlow:
                Glow(2 * scale * amount, new Vector3(.84f, .72f, .49f), .09f * amount);
                break;
            case HudTextEffect.NeonPulse:
                var pulse = .5f + .5f * MathF.Sin(time * 2.2f);
                Glow((1.2f + 1.3f * pulse) * scale * amount, new Vector3(.30f, .85f, 1), (.07f + .07f * pulse) * amount);
                Layer(new Vector2(0, -scale * amount), Pack(new Vector3(.85f, .30f, 1), .12f * amount));
                break;
            case HudTextEffect.ChromaticGlitch:
                var drift = MathF.Sin(time * 7.1f) * MathF.Pow(.5f + .5f * MathF.Sin(time * 1.3f), 6);
                var displacement = (1 + 2.5f * drift) * scale * amount;
                Layer(new Vector2(-displacement, .5f * scale * amount), Pack(new Vector3(.1f, 1, 1), .42f * amount));
                Layer(new Vector2(displacement, -.5f * scale * amount), Pack(new Vector3(1, .2f, .65f), .42f * amount));
                // Only the colored echoes glitch; the readable original never moves.
                var band = position.Y + size * (.2f + .5f * (.5f + .5f * MathF.Sin(time * .9f)));
                draw.PushClipRect(new Vector2(position.X - Padding(effect, size, amount, false).X, band),
                    new Vector2(position.X + width + Padding(effect, size, amount, false).X, band + Math.Max(1, size * .14f)), true);
                Layer(new Vector2(-displacement, 0), Pack(new Vector3(.4f, 1, 1), .65f * amount));
                draw.PopClipRect();
                break;
            case HudTextEffect.Hologram:
                Layer(new Vector2(1.5f * MathF.Sin(time * .8f), -1 - MathF.Sin(time * .7f)) * (scale * amount),
                    Pack(new Vector3(.15f, .85f, 1), .22f * amount));
                break;
            case HudTextEffect.Flame:
                for (var layer = 3; layer >= 1; layer--)
                {
                    var rise = layer * 1.7f * scale * amount;
                    var first = draw.VtxBuffer.Size;
                    Layer(Vector2.Zero, 0xFFFFFFFF);
                    for (var glyph = first; glyph + 3 < draw.VtxBuffer.Size; glyph += 4)
                    {
                        var x = (draw.VtxBuffer[glyph].Pos.X + draw.VtxBuffer[glyph + 2].Pos.X) * .5f - position.X;
                        var sway = MathF.Sin(time * 2.6f + x * .11f + layer) * 1.5f * scale * amount;
                        var heat = .6f + .4f * MathF.Sin(time * 2 + x * .09f + layer);
                        for (var v = 0; v < 4; v++)
                        {
                            ref var vertex = ref draw.VtxBuffer.Data[glyph + v];
                            var height = Math.Clamp(1 - (vertex.Pos.Y - position.Y) / Math.Max(1, size), 0, 1);
                            vertex.Pos += new Vector2(sway * height, -rise * height * heat);
                            vertex.Col = Pack(new Vector3(1, .22f + layer * .08f, .04f), (.26f - layer * .04f) * amount * (1 - height * .65f));
                        }
                    }
                }
                break;
        }

        if (outline) Ring(scale, 0xE6000000);
        draw.AddText(font, size, position, 0xFF9BC9DD, prefix, 0);
        var namePosition = position + new Vector2(Measure(font, size, prefix).Size.X, 0);
        var coreStart = draw.VtxBuffer.Size;
        draw.AddText(font, size, namePosition, Core, name, 0);
        var coreEnd = draw.VtxBuffer.Size;
        for (var i = coreStart; i < coreEnd; i++)
        {
            ref var vertex = ref draw.VtxBuffer.Data[i];
            var x = vertex.Pos.X - position.X; var y = (vertex.Pos.Y - position.Y) / Math.Max(1, size);
            var color = effect switch
            {
                HudTextEffect.GentleShimmer => Vector3.Lerp(White, new Vector3(.86f, .94f, 1), (.5f + .5f * MathF.Sin(time * .8f)) * Math.Min(1, amount)),
                HudTextEffect.Rainbow => Vector3.Lerp(White, Rainbow(x / Math.Max(80, width) - time * .12f), Math.Min(1, .75f * amount)),
                HudTextEffect.NeonPulse => Vector3.Lerp(White, new Vector3(.64f, .97f, 1), Math.Min(1, .65f * amount)),
                HudTextEffect.Hologram => Vector3.Lerp(White, new Vector3(.4f, .92f, 1), Math.Min(1, .65f * amount)),
                HudTextEffect.Flame => Vector3.Lerp(White, Vector3.Lerp(new Vector3(1, .97f, .65f), new Vector3(1, .50f, .22f), Math.Clamp(y, 0, 1)), Math.Min(1, .7f * amount)),
                HudTextEffect.Frost => Vector3.Lerp(White, Vector3.Lerp(new Vector3(.7f, .89f, 1), Vector3.One, Math.Clamp(y, 0, 1)), Math.Min(1, amount)),
                HudTextEffect.LightSweep => Vector3.Lerp(White, new Vector3(1, .87f, .55f), Math.Min(1, .2f * amount)),
                _ => White,
            };
            vertex.Col = Pack(color, 1);
        }
        draw.AddText(font, size, namePosition + new Vector2(Measure(font, size, name).Size.X, 0), 0xFFBEC4CA, suffix, 0);

        if (effect == HudTextEffect.Hologram)
        {
            var scan = position.Y + size * (.1f + .8f * (.5f + .5f * MathF.Sin(time * 1.1f)));
            draw.PushClipRect(new Vector2(position.X, scan), new Vector2(position.X + width, scan + Math.Max(1, scale * 1.4f)), true);
            Layer(Vector2.Zero, Pack(Vector3.One, Math.Min(.8f, .4f * amount)));
            draw.PopClipRect();
        }
        if (effect == HudTextEffect.LightSweep)
        {
            var first = draw.VtxBuffer.Size;
            Layer(Vector2.Zero, 0xFFFFFFFF);
            var sweep = ((time * .18f + .2f) % 1.6f - .3f) * width;
            for (var i = first; i < draw.VtxBuffer.Size; i++)
            {
                ref var vertex = ref draw.VtxBuffer.Data[i];
                var distance = Math.Abs(vertex.Pos.X - position.X - sweep);
                vertex.Col = Pack(new Vector3(1, 1, .92f), Math.Clamp(1 - distance / Math.Max(12, size * 1.4f), 0, 1) * Math.Min(1, amount));
            }
        }
        if (effect == HudTextEffect.Frost)
        {
            // Locate sparkles from real glyph quads, never from UTF-16 indices or guessed character widths.
            for (var i = coreStart; i + 3 < coreEnd; i += 4)
            {
                var ordinal = (i - coreStart) / 4;
                if (ordinal % 3 != 1) continue;
                var center = new Vector2((draw.VtxBuffer[i].Pos.X + draw.VtxBuffer[i + 2].Pos.X) * .5f,
                    position.Y + 1 - 1.8f * scale * amount);
                var sparkle = .5f + .5f * MathF.Sin(time * 1.7f + ordinal * 1.9f);
                var radius = (1 + sparkle) * scale * amount;
                var color = Pack(new Vector3(.75f, .95f, 1), (.3f + .5f * sparkle) * Math.Min(1, amount));
                draw.AddLine(center - new Vector2(radius, 0), center + new Vector2(radius, 0), color, Math.Max(.5f, scale * .7f));
                draw.AddLine(center - new Vector2(0, radius), center + new Vector2(0, radius), color, Math.Max(.5f, scale * .7f));
            }
        }
    }

    private static ushort Codepoint(Rune rune) => (ushort)(rune.Value <= ushort.MaxValue ? rune.Value : 0xFFFD);
    private static Vector2 NativeMeasure(ImFontPtr font, float size, string text)
    {
        // Dalamud's native font may kern glyph pairs; summing AdvanceX alone
        // loses that spacing. Use the same font's native measurement as AddText.
        var capacity = Encoding.UTF8.GetMaxByteCount(text.Length);
        Span<byte> encoded = capacity <= 1024 ? stackalloc byte[capacity] : new byte[capacity];
        var length = Encoding.UTF8.GetBytes(text, encoded);
        Vector2 measured;
        fixed (byte* data = encoded)
            ImGuiNative.CalcTextSizeA(&measured, font.Handle, size, float.MaxValue, 0, data, data + length, null);
        return measured;
    }
    private static float Scale(float size) => Math.Clamp(size / 17, .1f, 8);
    private static float Amount(float value) => Math.Clamp(float.IsFinite(value) ? value : 1, 0, 2);
    private static Vector3 Rainbow(float hue)
    {
        hue -= MathF.Floor(hue);
        return new Vector3(.7f + .3f * MathF.Cos(MathF.Tau * hue), .7f + .3f * MathF.Cos(MathF.Tau * (hue - 1f / 3)),
            .7f + .3f * MathF.Cos(MathF.Tau * (hue - 2f / 3)));
    }
    private static uint Pack(Vector3 color, float alpha)
        => (uint)(Math.Clamp(color.X, 0, 1) * 255) | (uint)(Math.Clamp(color.Y, 0, 1) * 255) << 8
            | (uint)(Math.Clamp(color.Z, 0, 1) * 255) << 16 | (uint)(Math.Clamp(alpha, 0, 1) * 255) << 24;
}
