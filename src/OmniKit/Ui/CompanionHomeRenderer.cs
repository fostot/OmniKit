using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

internal static class CompanionHomeRenderer
{
    public static void DrawHouse(ImDrawListPtr draw, ImTextureID texture, CompanionHomeRect house, bool mirrored)
        => draw.AddImage(texture, house.Minimum, house.Maximum,
            new Vector2(mirrored ? .925f : .08f, .035f), new Vector2(mirrored ? .08f : .925f, .9f));

    public static (Vector2 Minimum, Vector2 Maximum) DrawNap(ImDrawListPtr draw, ImTextureID texture,
        ImTextureID houseTexture, NapSheet sheet, CompanionHomeRoutine routine, Vector2 spriteSize,
        CompanionHomeRect house, bool houseMirrored, bool showHouse = true, uint tint = 0xFFFFFFFFu)
    {
        var frame = sheet.Frame(routine.ArtFrame, routine.Position, spriteSize, routine.ArtScale, routine.FacingLeft);
        var bounds = (Minimum: frame.Position, Maximum: frame.Position + frame.Size);
        if (routine.Sleeping)
        {
            var blend = (1 - MathF.Cos(routine.SleepPhase * MathF.Tau / 3.2f)) * .5f;
            draw.AddImage(texture, frame.Position, frame.Position + frame.Size, frame.UvStart, frame.UvEnd,
                ((uint)((1 - blend) * 255) << 24) | (tint & 0xFFFFFFu));
            var other = sheet.Frame(3, routine.Position, spriteSize, routine.ArtScale, routine.FacingLeft);
            draw.AddImage(texture, other.Position, other.Position + other.Size, other.UvStart, other.UvEnd,
                ((uint)(blend * 255) << 24) | (tint & 0xFFFFFFu));
            bounds = (Vector2.Min(bounds.Minimum, other.Position), Vector2.Max(bounds.Maximum, other.Position + other.Size));
        }
        else draw.AddImage(texture, frame.Position, frame.Position + frame.Size, frame.UvStart, frame.UvEnd, tint);

        if (!showHouse) return bounds;
        // The visitor goes behind the house walls but stays visible on its bed.
        var doorMin = house.At(houseMirrored ? .12f : .43f, .55f);
        var doorMax = house.At(houseMirrored ? .57f : .88f, .89f);
        Span<Vector4> clips =
        [
            new(house.Minimum.X, house.Minimum.Y, house.Maximum.X, doorMin.Y),
            new(house.Minimum.X, doorMin.Y, doorMin.X, house.Maximum.Y),
            new(doorMax.X, doorMin.Y, house.Maximum.X, house.Maximum.Y),
            new(doorMin.X, doorMax.Y, doorMax.X, house.Maximum.Y)
        ];
        foreach (var clip in clips)
        {
            draw.PushClipRect(new Vector2(clip.X, clip.Y), new Vector2(clip.Z, clip.W), true);
            DrawHouse(draw, houseTexture, house, houseMirrored);
            draw.PopClipRect();
        }
        return bounds;
    }

    public static void DrawZs(ImDrawListPtr draw, CompanionHomeRect house, bool mirrored,
        float time, Vector2 viewportPosition, Vector2 viewportSize)
    {
        var font = ImGui.GetFont();
        var chimney = house.At(mirrored ? .76f : .24f, .17f);
        draw.PushClipRect(viewportPosition, viewportPosition + viewportSize, true);
        for (var i = 0; i < 3; i++)
        {
            var phase = (time * .3f + i / 3f) % 1;
            var alpha = MathF.Sin(phase * MathF.PI);
            var size = Math.Clamp(house.Size.Y * (.09f + phase * .045f), 10, 20);
            var position = chimney + new Vector2(MathF.Sin(phase * 4 + i) * house.Size.X * .055f, -phase * house.Size.Y * .6f);
            position = Vector2.Clamp(position, viewportPosition + new Vector2(2),
                Vector2.Max(viewportPosition + new Vector2(2), viewportPosition + viewportSize - new Vector2(size + 2)));
            draw.AddText(font, size, position + Vector2.One, (uint)(alpha * 180) << 24, "z");
            draw.AddText(font, size, position, ((uint)(alpha * 255) << 24) | 0x00F4D9C6u, i == 2 ? "Z" : "z");
        }
        draw.PopClipRect();
    }
}
