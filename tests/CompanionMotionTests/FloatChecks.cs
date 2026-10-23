using System.Numerics;
using OmniKit.Ui;

internal static class FloatChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks++; }
        bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .002f;
        var library = new CompanionAnimationLibrary(Path.Combine(AppContext.BaseDirectory, "assets"));
        foreach (var avatar in new[] { "Wink", "Nova" })
        {
            Check(library.TryGet(avatar, "Navigator", out var atlas) && atlas != null, "Floating companions use their packaged artwork.");
            foreach (var size in new[] { 48f, 96f, 192f })
            {
                var origin = new Vector2(100, 200); var cell = new Vector2(size);
                var first = atlas!.GetFloat(0, false, origin, cell);
                var end = atlas.GetFloat(1, false, origin, cell);
                var approach = atlas.GetFloat(.999999f, false, origin, cell);
                Check(Near(first.A, end.A) && Near(first.B, end.B) && Near(first.C, end.C) && Near(first.D, end.D),
                    "Floating motion has an identical zero/one loop boundary.");
                Check(Near(first.A, approach.A) && Near(first.C, approach.C), "Floating motion approaches the loop continuously.");
                var previous = first;
                var moved = false;
                for (var sample = 0; sample <= 128; sample++)
                {
                    var actual = atlas.GetFloat(sample / 128f, false, origin, cell);
                    var mirror = atlas.GetFloat(sample / 128f, true, origin, cell);
                    Check(Near(actual.HitMinimum, first.HitMinimum) && Near(actual.HitMaximum, first.HitMaximum),
                        "Float picking bounds stay fixed throughout the bob/tilt cycle.");
                    Check(Near(actual.UvA, first.UvA) && Near(actual.UvB, first.UvB) && Near(actual.UvC, first.UvC) && Near(actual.UvD, first.UvD),
                        "Floating never changes the body or face source image.");
                    Check(Math.Abs(Vector2.Distance(actual.A, actual.B) - Vector2.Distance(first.A, first.B)) < .002f
                        && Math.Abs(Vector2.Distance(actual.B, actual.C) - Vector2.Distance(first.B, first.C)) < .002f
                        && Math.Abs(Vector2.Distance(actual.A, actual.C) - Vector2.Distance(first.A, first.C)) < .002f,
                        "Floating rotates/translates its complete body without deformation or changing scale.");
                    foreach (var point in new[] { actual.A, actual.B, actual.C, actual.D, mirror.A, mirror.B, mirror.C, mirror.D,
                        actual.HitMinimum, actual.HitMaximum, mirror.HitMinimum, mirror.HitMaximum })
                        Check(float.IsFinite(point.X) && float.IsFinite(point.Y) && point.X >= origin.X && point.Y >= origin.Y
                            && point.X <= origin.X + cell.X && point.Y <= origin.Y + cell.Y,
                            "Floating artwork and picking bounds remain finite and within their logical cell in both directions.");
                    Check(Near(mirror.A, new Vector2(2 * origin.X + cell.X - actual.A.X, actual.A.Y))
                        && Near(mirror.C, new Vector2(2 * origin.X + cell.X - actual.C.X, actual.C.Y)),
                        "Facing mirrors the exact continuous floating transform.");
                    Check(Vector2.Distance(previous.A, actual.A) < cell.X * .004f,
                        "Consecutive floating samples contain no redraw jumps.");
                    moved |= !Near(first.A, actual.A);
                    previous = actual;
                }
                Check(moved, "The stable source still visibly bobs and tilts.");
            }
        }
        return checks;
    }
}
