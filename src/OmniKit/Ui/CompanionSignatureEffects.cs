using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

/// <summary>Small companion-specific magic, drawn once around the existing body. No textures or body copies.</summary>
internal static class CompanionSignatureEffects
{
    public const float PaddingFraction = .18f;

    /// <summary>Caller owns layer/clock. Animation off freezes geometry; the appearance editor bypasses all effects.</summary>
    public static (Vector2 Minimum, Vector2 Maximum) Draw(ImDrawListPtr draw, string avatar,
        SeasonalCompanionTheme? theme, Vector2 position, Vector2 size, Vector4 paletteColor, float timeSeconds,
        bool magicEnabled, bool animationsEnabled, bool editorPreview = false, bool mirrored = false)
    {
        if (!magicEnabled || editorPreview || !Finite(position) || !Finite(size) || size.X <= 0 || size.Y <= 0
            || !KnownAvatar(avatar)) return (position, position);
        var t = animationsEnabled && float.IsFinite(timeSeconds) ? timeSeconds : 0;
        // Keep precision stable for a long-running session; every animated period divides this interval.
        t %= MathF.Tau * 120;
        if (!float.IsFinite(paletteColor.X) || !float.IsFinite(paletteColor.Y) || !float.IsFinite(paletteColor.Z)
            || !float.IsFinite(paletteColor.W)) paletteColor = new(.66f, .85f, .95f, 1);
        paletteColor = Vector4.Clamp(paletteColor, Vector4.Zero, Vector4.One);
        var signatureColor = avatar switch
        {
            "Ember" => new Vector3(1, .57f, .22f), "Mochi" => new(1, .91f, .78f),
            "Puff" => new(.84f, .91f, 1), "Prism" => new(.83f, .68f, 1),
            "Nova" => new(.85f, .66f, 1), "Orbit" => new(.59f, .82f, 1),
            "Wink" => new(.63f, .94f, .88f), _ => new Vector3(.61f, .88f, 1),
        };
        var c = new Canvas(draw, position, size, new(Vector3.Lerp(signatureColor,
            new(paletteColor.X, paletteColor.Y, paletteColor.Z), .32f), paletteColor.W), mirrored);
        switch (avatar)
        {
            case "Nova":
                // Two offset comet tails, a short ribbon, and a luminous tip.
                for (var i = 0; i < 2; i++)
                {
                    var y = .20f + i * .55f + MathF.Sin(t * .5f + i) * .025f;
                    var x = i == 0 ? .08f : .92f;
                    c.Curve(new(x + .11f, y - .10f), new(x - .16f, y - .06f), new(x - .12f, y + .10f), new(x, y), .55f, .012f);
                    c.Star(new(x, y), .045f, .78f);
                    c.Curve(new(x + .10f, y - .04f), new(x - .04f, y - .02f), new(x - .09f, y + .03f), new(x - .04f, y + .07f), .28f, .008f);
                }
                break;
            case "Prism":
                for (var i = 0; i < 3; i++)
                {
                    var p = new Vector2(i == 1 ? .97f : .04f, .28f + i * .22f + MathF.Sin(t * .5f + i) * .018f);
                    c.Diamond(p, new(.035f, .075f), .35f + i * .1f);
                    c.Line(p + new Vector2(-.08f, -.035f), p + new Vector2(.08f, .035f), .25f, .008f);
                    c.Line(p + new Vector2(-.07f, -.015f), p + new Vector2(.07f, .055f), .17f, .007f);
                }
                break;
            case "Wink":
                // An external eye-shaped halo with lashes; never paints over the authored iris/lid.
                c.Arc(new(.5f, .52f), new(.51f, .38f), 3.48f, 5.95f, .36f, .011f);
                c.Arc(new(.5f, .52f), new(.51f, .38f), .33f, 2.81f, .22f, .008f);
                for (var i = 0; i < 5; i++)
                {
                    var a = 3.7f + i * .48f;
                    var p = new Vector2(.5f + MathF.Cos(a) * .51f, .52f + MathF.Sin(a) * .38f);
                    c.Line(p, p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (.04f + .005f * MathF.Sin(t * .5f)), .58f, .009f);
                }
                c.Circle(new(.99f, .51f + MathF.Sin(t * .5f) * .03f), .025f, .62f);
                break;
            case "Orbit":
                for (var i = 0; i < 2; i++)
                {
                    var a = t * .25f + i * MathF.PI;
                    c.Arc(new(.5f, .53f), new(.58f, .30f), a - .78f, a + .08f, .43f, .011f);
                    c.Circle(new(.5f + MathF.Cos(a) * .58f, .53f + MathF.Sin(a) * .30f), .022f, .76f);
                }
                break;
            case "Ember":
                for (var i = 0; i < 5; i++)
                {
                    var a = t * .5f + i * 1.2f;
                    var p = new Vector2(i % 2 == 0 ? .02f + MathF.Sin(a) * .025f : .97f + MathF.Sin(a) * .025f,
                        .68f - i * .11f + MathF.Cos(a) * .04f);
                    c.Flame(p, .025f + i % 2 * .008f, .58f);
                }
                c.Arc(new(.5f, .87f), new(.32f, .09f), .1f, 3.04f, .24f, .013f);
                break;
            case "Mochi":
                for (var i = 0; i < 7; i++)
                {
                    var x = .15f + i * .115f;
                    c.Circle(new(x, .93f + MathF.Sin(t * .5f + i) * .014f), .010f + i % 3 * .004f, .50f, true);
                }
                c.Curve(new(.02f, .74f), new(.03f, .86f), new(.14f, .91f), new(.22f, .88f), .45f, .013f);
                c.Curve(new(.98f, .74f), new(.97f, .86f), new(.86f, .91f), new(.78f, .88f), .45f, .013f);
                break;
            case "Puff":
                for (var i = 0; i < 3; i++)
                {
                    var y = .28f + i * .27f + MathF.Sin(t * .5f + i) * .025f;
                    var x = i == 1 ? .87f : .02f;
                    c.Curve(new(x - .04f, y), new(x + .16f, y - .10f), new(x + .17f, y + .11f), new(x + .03f, y + .04f), .40f, .016f);
                    c.Curve(new(x, y + .06f), new(x + .10f, y + .02f), new(x + .12f, y + .10f), new(x + .04f, y + .10f), .24f, .009f);
                }
                break;
            case "Moonblob":
                for (var i = 0; i < 4; i++)
                {
                    var a = t * .25f + i * MathF.PI / 2;
                    var p = new Vector2(.5f + MathF.Cos(a) * .52f, .53f + MathF.Sin(a) * .40f);
                    c.Ellipse(p, new(.018f, .030f), .48f);
                    c.Circle(p + new Vector2(-.006f, -.010f), .006f, .78f, true);
                }
                break;
        }
        if (theme is { } holiday && Enum.IsDefined(holiday)) DrawHoliday(c, holiday, t);
        return (position - size * PaddingFraction, position + size * (1 + PaddingFraction));
    }

    private static void DrawHoliday(Canvas c, SeasonalCompanionTheme theme, float time)
    {
        c = c.WithColor(theme switch
        {
            SeasonalCompanionTheme.Heavensturn => new(1, .78f, .38f),
            SeasonalCompanionTheme.Valentione => new(1, .53f, .73f),
            SeasonalCompanionTheme.LittleLadies => new(1, .72f, .85f),
            SeasonalCompanionTheme.HatchingTide => new(.73f, .94f, .65f),
            SeasonalCompanionTheme.MakeItRain => new(1, .85f, .39f),
            SeasonalCompanionTheme.MoonfireFaire => new(.39f, .90f, 1),
            SeasonalCompanionTheme.TheRising => new(.87f, .69f, 1),
            SeasonalCompanionTheme.AllSaintsWake => new(1, .64f, .28f),
            _ => new Vector3(.78f, .91f, 1),
        });
        // Two legible motifs leave the face and accessory crown clear. Positions drift, never jump or flash.
        var a = new Vector2(.09f, .08f + MathF.Sin(time * .5f) * .015f);
        var b = new Vector2(.91f, .89f + MathF.Sin(time * .5f + 1) * .015f);
        switch (theme)
        {
            case SeasonalCompanionTheme.Heavensturn:
                foreach (var p in new[] { a, b })
                {
                    c.Arc(p + new Vector2(0, .025f), new(.062f), MathF.PI, MathF.Tau, .72f, .011f);
                    for (var i = 0; i < 5; i++) { var angle = MathF.PI + i * MathF.PI / 4; c.Line(p + new Vector2(0, .025f), p + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * .062f + new Vector2(0, .025f), .55f, .007f); }
                }
                break;
            case SeasonalCompanionTheme.Valentione:
                c.Heart(a, .052f, .74f); c.Heart(b, .042f, .58f); break;
            case SeasonalCompanionTheme.LittleLadies:
                c.Flower(a, .045f); c.Flower(b, .036f); break;
            case SeasonalCompanionTheme.HatchingTide:
                foreach (var p in new[] { a, b })
                {
                    c.Ellipse(p, new(.035f, .045f), .42f);
                    c.Line(p + new Vector2(-.025f, 0), p + new Vector2(-.008f, .012f), .85f, .008f);
                    c.Line(p + new Vector2(-.008f, .012f), p + new Vector2(.009f, -.006f), .85f, .008f);
                    c.Line(p + new Vector2(.009f, -.006f), p + new Vector2(.027f, .009f), .85f, .008f);
                }
                break;
            case SeasonalCompanionTheme.MakeItRain:
                foreach (var p in new[] { a, b })
                { c.Ellipse(p, new(.033f, .044f), .65f); c.Arc(p, new(.021f, .030f), 0, MathF.Tau, .88f, .006f); c.Line(p - new Vector2(0, .021f), p + new Vector2(0, .021f), .90f, .008f); }
                break;
            case SeasonalCompanionTheme.MoonfireFaire:
                for (var i = 0; i < 6; i++)
                { var d = new Vector2(MathF.Cos(i * MathF.Tau / 6), MathF.Sin(i * MathF.Tau / 6)); c.Line(a + d * .020f, a + d * .058f, .72f, .009f); }
                c.Curve(b + new Vector2(-.07f, .02f), b + new Vector2(.05f, -.09f), b + new Vector2(.09f, .045f), b + new Vector2(.01f, .02f), .65f, .013f);
                break;
            case SeasonalCompanionTheme.TheRising:
                c.Diamond(a, new(.025f, .064f), .76f); c.Star(b, .047f, .68f);
                c.Line(a + new Vector2(-.045f, .04f), a + new Vector2(-.045f, -.025f), .37f, .007f);
                c.Line(a + new Vector2(.045f, .04f), a + new Vector2(.045f, -.025f), .37f, .007f); break;
            case SeasonalCompanionTheme.AllSaintsWake:
                c.Bat(a, .063f); c.Bat(b, .052f); break;
            case SeasonalCompanionTheme.Starlight:
                c.Snowflake(a, .055f); c.Star(b, .043f, .72f); break;
        }
    }

    private static bool KnownAvatar(string avatar) => avatar is "Moonblob" or "Wink" or "Nova" or "Mochi" or "Puff" or "Orbit" or "Ember" or "Prism";
    private static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);

    private readonly struct Canvas(ImDrawListPtr draw, Vector2 position, Vector2 size, Vector4 color, bool mirrored)
    {
        private float Edge => Math.Min(size.X, size.Y);
        public Canvas WithColor(Vector3 rgb) => new(draw, position, size, new(rgb, color.W), mirrored);
        private Vector2 P(Vector2 p) => position + new Vector2(mirrored ? 1 - p.X : p.X, p.Y) * size;
        private uint C(float opacity, bool white = false) => ImGui.ColorConvertFloat4ToU32(new Vector4(
            white ? Vector3.One : new Vector3(color.X, color.Y, color.Z), Math.Clamp(opacity * color.W, 0, 1)));
        public void Line(Vector2 a, Vector2 b, float opacity, float width) => draw.AddLine(P(a), P(b), C(opacity), Math.Max(.6f, Edge * width));
        public void Circle(Vector2 p, float radius, float opacity, bool white = false) => draw.AddCircleFilled(P(p), Edge * radius, C(opacity, white), 12);
        public void Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float opacity, float width)
            => draw.AddBezierCubic(P(a), P(b), P(c), P(d), C(opacity), Math.Max(.6f, Edge * width), 20);
        public void Arc(Vector2 center, Vector2 radius, float first, float last, float opacity, float width)
        {
            for (var i = 0; i <= 28; i++) { var angle = first + (last - first) * i / 28; draw.PathLineTo(P(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius)); }
            draw.PathStroke(C(opacity), ImDrawFlags.None, Math.Max(.6f, Edge * width));
        }
        public void Ellipse(Vector2 center, Vector2 radius, float opacity)
        {
            for (var i = 0; i < 24; i++) { var angle = i * MathF.Tau / 24; draw.PathLineTo(P(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius)); }
            draw.PathFillConvex(C(opacity));
        }
        public void Diamond(Vector2 p, Vector2 radius, float opacity)
        {
            draw.AddQuadFilled(P(p - new Vector2(0, radius.Y)), P(p + new Vector2(radius.X, 0)), P(p + new Vector2(0, radius.Y)), P(p - new Vector2(radius.X, 0)), C(opacity));
            Line(p - new Vector2(0, radius.Y), p + new Vector2(0, radius.Y), opacity * .8f, .006f);
        }
        public void Star(Vector2 p, float radius, float opacity)
        { Diamond(p, new(radius * .35f, radius), opacity); Diamond(p, new(radius, radius * .35f), opacity); }
        public void Flame(Vector2 p, float radius, float opacity)
        { Ellipse(p + new Vector2(0, radius * .25f), new(radius * .7f, radius), opacity); draw.AddTriangleFilled(P(p + new Vector2(-radius * .6f, 0)), P(p + new Vector2(radius * .5f, 0)), P(p + new Vector2(radius * .25f, -radius * 1.8f)), C(opacity)); }
        public void Heart(Vector2 p, float radius, float opacity)
        { Circle(p + new Vector2(-radius * .37f, -radius * .22f), radius * .53f, opacity); Circle(p + new Vector2(radius * .37f, -radius * .22f), radius * .53f, opacity); draw.AddTriangleFilled(P(p + new Vector2(-radius * .85f, -.05f * radius)), P(p + new Vector2(radius * .85f, -.05f * radius)), P(p + new Vector2(0, radius)), C(opacity)); }
        public void Flower(Vector2 p, float radius)
        { for (var i = 0; i < 5; i++) { var a = i * MathF.Tau / 5; Circle(p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius * .62f, radius * .43f, .64f); } Circle(p, radius * .19f, .86f, true); }
        public void Bat(Vector2 p, float radius)
        {
            Circle(p, radius * .21f, .77f);
            foreach (var side in new[] { -1, 1 })
            {
                draw.AddTriangleFilled(P(p), P(p + new Vector2(side * radius, -radius * .40f)), P(p + new Vector2(side * radius * .65f, radius * .28f)), C(.65f));
                draw.AddTriangleFilled(P(p), P(p + new Vector2(side * radius * .65f, radius * .28f)), P(p + new Vector2(side * radius * .20f, radius * .5f)), C(.65f));
            }
        }
        public void Snowflake(Vector2 p, float radius)
        {
            for (var i = 0; i < 6; i++)
            {
                var a = i * MathF.Tau / 6; var d = new Vector2(MathF.Cos(a), MathF.Sin(a)); var normal = new Vector2(-d.Y, d.X);
                Line(p, p + d * radius, .72f, .008f);
                Line(p + d * radius * .58f, p + d * radius * .82f + normal * radius * .22f, .60f, .006f);
                Line(p + d * radius * .58f, p + d * radius * .82f - normal * radius * .22f, .60f, .006f);
            }
        }
    }
}
