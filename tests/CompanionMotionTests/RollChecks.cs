using System.Numerics;
using OmniKit.Ui;

internal static class RollChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks++; }
        bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .001f;
        var body = new Vector4(30, 40, 170, 180);
        var region = new CompanionSpriteRegion(new Vector4(25, 35, 175, 185), new Vector2(100, 180), body);
        var atlas = new CompanionSpriteAtlas("synthetic-roll.png", new Vector2(1024, 1536), 220, [region], MathF.PI * 140 / 220);
        var origin = new Vector2(200, 150); var size = new Vector2(96);
        var start = atlas.GetRoll(0, false, origin, size);
        var loop = atlas.GetRoll(1, false, origin, size);
        var nearLoop = atlas.GetRoll(.999999f, false, origin, size);
        Check(Near(start.A, loop.A) && Near(start.B, loop.B) && Near(start.C, loop.C) && Near(start.D, loop.D), "Rolling completes an identical zero/one loop.");
        Check(Near(start.A, nearLoop.A) && Near(start.C, nearLoop.C), "Rolling approaches the loop continuously.");
        var ninety = atlas.GetRoll(.25f, false, origin, size);
        Check(Near(ninety.A - ninety.Center, new Vector2(-(start.A - start.Center).Y, (start.A - start.Center).X)),
            "A quarter travelled circumference rotates the unchanged upright artwork by ninety degrees.");
        Check(Math.Abs(atlas.CycleDistance * size.X - 2 * MathF.PI * start.Radius) < .001f,
            "A full rotation travels the reviewed body's actual rendered circumference.");
        var previous = start;
        for (var sample = 0; sample <= 256; sample++)
        {
            var roll = atlas.GetRoll(sample / 256f, false, origin, size);
            var mirror = atlas.GetRoll(sample / 256f, true, origin, size);
            Check(Near(roll.Center, start.Center) && Math.Abs(roll.Radius - start.Radius) < .001f,
                "Rotation does not bob or resize the rolling body's center/radius.");
            Check(Math.Abs(roll.Center.Y + roll.Radius - (origin.Y + size.Y * .9f)) < .001f,
                "The rolling body stays tangent to the same ground baseline.");
            Check(Near(roll.UvA, start.UvA) && Near(roll.UvC, start.UvC), "Every rolling phase samples the same unedited source frame.");
            var bounds = roll.HitBounds;
            Check(bounds.Minimum.X >= origin.X && bounds.Minimum.Y >= origin.Y && bounds.Maximum.X <= origin.X + size.X && bounds.Maximum.Y <= origin.Y + size.Y,
                "Stable circular body picking bounds remain inside the logical cell.");
            foreach (var point in new[] { roll.A, roll.B, roll.C, roll.D })
                Check(float.IsFinite(point.X) && float.IsFinite(point.Y), "Every rotated quad vertex remains finite.");
            Check(Near(mirror.A, new Vector2(2 * roll.Center.X - roll.A.X, roll.A.Y)) && Near(mirror.C, new Vector2(2 * roll.Center.X - roll.C.X, roll.C.Y)),
                "Leftward rolling reverses orientation without changing the body center or baseline.");
            Check(Math.Abs(Vector2.Distance(roll.A, roll.B) - Vector2.Distance(start.A, start.B)) < .001f,
                "Rolling preserves the original texture scale.");
            Check(Vector2.Distance(previous.A, roll.A) < 1.2f, "Adjacent rolling samples advance continuously without sprite-frame jumps.");
            previous = roll;
        }
        return checks;
    }
}
