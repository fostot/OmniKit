using System.Diagnostics;
using System.Numerics;
using OmniKit.Ui;

internal static class HeadingChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
        foreach (var initiallyLeft in new[] { false, true })
        {
            var facing = initiallyLeft;
            for (var i = 0; i < 100; i++)
            {
                facing = CompanionGait.FacingFor(new Vector2(i % 2 == 0 ? -.02f : .02f, 1), facing);
                Check(facing == initiallyLeft, "Small alternating horizontal drift on a vertical path cannot flip facing.");
            }
        }
        Check(CompanionGait.FacingFor(new Vector2(-1, 1), false) && !CompanionGait.FacingFor(new Vector2(1, 1), true),
            "A clear diagonal direction can still change facing.");
        var gait = new CompanionGait(); var point = Vector2.Zero;
        gait.Update(point, 96, 0, false, false, 0, .34f, directionalStride: true);
        var phaseBeforePause = 0f;
        foreach (var direction in new[] { Vector2.UnitX, Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY })
        {
            for (var frame = 0; frame < 72; frame++)
            {
                var previousHeading = gait.Heading;
                point += direction * (96 * .36f / 144);
                gait.Update(point, 96, 1f / 144, true, direction.X < 0, 0, .34f, directionalStride: true);
                var change = Math.Abs(MathF.Atan2(previousHeading.X * gait.Heading.Y - previousHeading.Y * gait.Heading.X,
                    Vector2.Dot(previousHeading, gait.Heading)));
                Check(change <= MathF.Tau * 2 / 144 + .0001f && Math.Abs(gait.Heading.Length() - 1) < .0001f,
                    "Even a sharp path turn changes heading continuously at a bounded rate.");
            }
            phaseBeforePause = gait.Phase; var stoppedHeading = gait.Heading;
            gait.Update(point, 96, .05f, false, direction.X < 0, 0, .34f, directionalStride: true);
            Check(gait.Phase == phaseBeforePause && gait.Heading == stoppedHeading, "Stopping preserves phase and heading without a reset.");
        }
        var layout = CompanionLayout.Create(Vector2.Zero, new Vector2(2000), new Vector4(0, 0, 1, 1), 96, 1, 1);
        Vector2 Travel(int frames, bool variable)
        {
            var motion = new CompanionMotion(101); motion.Update(layout, 0, false);
            var start = motion.Position; var previous = start; long timestamp = 0;
            var weights = Enumerable.Range(0, frames).Select(i => variable ? 1 + (i % 5 - 2) * .17 : 1d).ToArray();
            var total = weights.Sum(); var elapsed = 0d;
            for (var i = 0; i < frames; i++)
            {
                elapsed += weights[i] / total;
                var next = (long)Math.Round(elapsed * Stopwatch.Frequency);
                var dt = (float)Stopwatch.GetElapsedTime(timestamp, next).TotalSeconds;
                timestamp = next;
                motion.Update(layout, dt, true);
                Check(dt > 0 && Vector2.Distance(motion.Position, previous) > 0,
                    "High-resolution60/144Hz or variable-frame deltas produce movement on every rendered frame.");
                previous = motion.Position;
            }
            return motion.Position - start;
        }
        var sixty = Travel(60, false); var fast = Travel(144, false); var variable = Travel(144, true);
        Check(Vector2.Distance(sixty, fast) < .002f && Vector2.Distance(sixty, variable) < .002f,
            "Equal elapsed time at60/144Hz and variable pacing produces equal travel, without speed changes.");
        return checks;
    }
}
