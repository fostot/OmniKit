using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

/// <summary>Original vector companions and restrained, optional accents. No textures or shaders.</summary>
internal static class ProceduralCompanion
{
    public static bool IsProcedural(string avatar) => avatar is "Moonblob" or "Wink" or "Nova";

    public static Vector4 Palette(string name) => name switch
    {
        "Aurora" => new(.42f, .89f, .73f, 1),
        "Nebula" => new(.69f, .58f, .96f, 1),
        "Rose" => new(.95f, .59f, .77f, 1),
        "Ember" => new(.98f, .67f, .40f, 1),
        "Starlight" => new(.93f, .90f, .68f, 1),
        _ => new(.66f, .85f, .95f, 1),
    };

    public static void Draw(ImDrawListPtr draw, Vector2 position, Vector2 size, Configuration config,
        CompanionPose pose, float timeSeconds, bool hovered = false)
    {
        if (!IsProcedural(config.Avatar) || size.X <= 0 || size.Y <= 0) return;
        var animated = config.AnimationsEnabled;
        var t = animated && float.IsFinite(timeSeconds) ? timeSeconds : 0;
        if (!animated) { pose = CompanionPose.Idle; hovered = false; }
        var edge = Math.Min(size.X, size.Y);
        var center = position + size * .5f;
        var color = Palette(config.CompanionPalette);
        var ink = new Vector4(.065f, .085f, .11f, 1);
        var talk = pose == CompanionPose.Talk && animated;
        switch (config.Avatar)
        {
            case "Moonblob":
            {
                var roll = pose == CompanionPose.Walk ? t * 1.7f : 0;
                var squish = animated ? MathF.Sin(t * 3.2f) * .055f : 0;
                var radius = edge * .29f;
                center.Y += edge * .09f;
                Ellipse(draw, center + new Vector2(0, radius * .9f), new Vector2(radius * .9f, edge * .045f), Color(ink, .25f));
                for (var i = 0; i < 40; i++)
                {
                    var angle = i * MathF.Tau / 40;
                    var ripple = 1 + MathF.Sin(angle * 3 + roll) * .035f;
                    draw.PathLineTo(center + new Vector2(MathF.Cos(angle) * (1 + squish), MathF.Sin(angle) * (1 - squish)) * radius * ripple);
                }
                draw.PathFillConvex(Color(color));
                Ellipse(draw, center + new Vector2(-radius * .28f, -radius * .35f), new Vector2(radius * .38f, radius * .18f), 0x38FFFFFF);
                var eyeLeft = Rotate(new Vector2(-radius * .29f, -radius * .04f), roll);
                var eyeRight = Rotate(new Vector2(radius * .29f, -radius * .04f), roll);
                draw.AddCircleFilled(center + eyeLeft, edge * .026f, Color(ink));
                draw.AddCircleFilled(center + eyeRight, edge * .026f, Color(ink));
                var mouth = center + Rotate(new Vector2(0, radius * .31f), roll);
                if (talk) Ellipse(draw, mouth, new Vector2(edge * .028f, edge * (.025f + .009f * MathF.Sin(t * 4))), Color(ink));
                else draw.AddLine(mouth + Rotate(new Vector2(-edge * .035f, 0), roll), mouth + Rotate(new Vector2(edge * .035f, 0), roll), Color(ink), Math.Max(1, edge * .012f));
                break;
            }
            case "Wink":
            {
                if (animated) center.Y += MathF.Sin(t * 1.8f) * edge * .028f;
                var blink = animated ? Math.Clamp(MathF.Abs((t + 2.1f) % 4.8f - 2.4f) / .13f, .055f, 1) : 1;
                Ellipse(draw, center, new Vector2(edge * .34f, edge * .23f * blink), Color(color));
                Ellipse(draw, center, new Vector2(edge * .29f, edge * .19f * blink), 0xFFF4F6F7);
                if (blink > .6f)
                {
                    var gaze = center + new Vector2(animated ? MathF.Sin(t * .7f) * edge * .025f : 0, hovered ? -edge * .025f : 0);
                    draw.AddCircleFilled(gaze, edge * .115f, Color(color));
                    draw.AddCircleFilled(gaze, edge * (talk ? .061f : .071f), Color(ink));
                    draw.AddCircleFilled(gaze + new Vector2(-.025f, -.035f) * edge, edge * .024f, 0xFFFFFFFF);
                }
                break;
            }
            case "Nova":
            {
                // Less than one pulse per second, with a small amplitude and no on/off flashes.
                var pulse = animated ? 1 + MathF.Sin(t * (talk ? 3.4f : 2.5f)) * .045f : 1;
                var rotation = animated ? t * .18f : 0;
                draw.AddCircleFilled(center, edge * .36f * pulse, Color(color, .08f), 40);
                for (var i = 0; i < 24; i++)
                {
                    var angle = i * MathF.Tau / 24 + rotation;
                    var radius = edge * (i % 2 == 0 ? .30f : .17f) * pulse;
                    var nextAngle = (i + 1) * MathF.Tau / 24 + rotation;
                    var nextRadius = edge * (i % 2 == 0 ? .17f : .30f) * pulse;
                    draw.AddTriangleFilled(center, center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius,
                        center + new Vector2(MathF.Cos(nextAngle), MathF.Sin(nextAngle)) * nextRadius, Color(color, .86f));
                }
                draw.AddCircleFilled(center, edge * .10f, 0xFFF4F8F8, 24);
                draw.AddCircleFilled(center + new Vector2(-.025f, -.025f) * edge, edge * .026f, 0xFFFFFFFF);
                break;
            }
        }
    }

    public static void DrawEffects(ImDrawListPtr draw, Vector2 position, Vector2 size, Configuration config,
        CompanionPose pose, float timeSeconds, bool hovered = false)
    {
        if (!config.AnimationsEnabled || !config.CompanionMagicEnabled || size.X <= 0 || size.Y <= 0) return;
        var t = float.IsFinite(timeSeconds) ? timeSeconds : 0;
        var edge = Math.Min(size.X, size.Y);
        var center = position + size * .5f;
        var color = Palette(config.CompanionPalette);
        var thickness = Math.Max(1, edge * .009f);
        var orbit = center + new Vector2(0, edge * .26f);
        for (var i = 0; i < 40; i++)
        {
            var angle = i * MathF.Tau / 40;
            draw.PathLineTo(orbit + new Vector2(MathF.Cos(angle) * edge * .38f, MathF.Sin(angle) * edge * .10f));
        }
        draw.PathStroke(Color(color, .24f), ImDrawFlags.Closed, thickness);
        for (var i = 0; i < 4; i++)
        {
            var angle = t * .6f + i * MathF.Tau / 4;
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * edge * .42f;
            var alpha = .24f + .08f * MathF.Sin(t * 1.6f + i);
            var radius = edge * .025f;
            draw.AddLine(point - new Vector2(radius, 0), point + new Vector2(radius, 0), Color(color, alpha), thickness);
            draw.AddLine(point - new Vector2(0, radius), point + new Vector2(0, radius), Color(color, alpha), thickness);
        }
    }

    private static Vector2 Rotate(Vector2 value, float angle) =>
        new(value.X * MathF.Cos(angle) - value.Y * MathF.Sin(angle), value.X * MathF.Sin(angle) + value.Y * MathF.Cos(angle));

    private static void Ellipse(ImDrawListPtr draw, Vector2 center, Vector2 radius, uint color)
    {
        for (var i = 0; i < 40; i++)
        {
            var angle = i * MathF.Tau / 40;
            draw.PathLineTo(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }
        draw.PathFillConvex(color);
    }

    private static uint Color(Vector4 color, float alpha = 1) => ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, alpha));
}
