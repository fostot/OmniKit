using System.Diagnostics;
using System.Numerics;
using OmniKit.Ui;

internal static class HeadingChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
        var directory = Path.Combine(AppContext.BaseDirectory, "assets");
        var rigs = new LunarWalkRigLibrary(directory);
        foreach (var avatar in new[] { "Bimble", "Moonbear" })
        foreach (var outfit in new[] { "Navigator", "Stargazer" })
        {
            Check(rigs.TryGet(avatar, outfit, out var rig) && rig != null, "Heading checks use each actual packaged rig.");
            foreach (var mirrored in new[] { false, true })
            for (var degrees = 0; degrees < 360; degrees += 5)
            {
                var angle = degrees * MathF.PI / 180;
                var heading = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var cycle = CompanionGait.DirectionalCycleDistance(rig!.CycleDistance, heading);
                var first = rig.Evaluate(0, mirrored, Vector2.Zero, Vector2.One, heading);
                var last = rig.Evaluate(1, mirrored, Vector2.Zero, Vector2.One, heading);
                Check(Vector2.Distance(first.NearFoot, last.NearFoot) < .0001f && Vector2.Distance(first.FarFoot, last.FarFoot) < .0001f,
                    "Every travel heading has an identical gait loop boundary.");
                Vector2 nearPlant = default, farPlant = default;
                var nearWasPlanted = false; var farWasPlanted = false;
                for (var sample = 0; sample < 64; sample++)
                {
                    var phase = sample / 64f;
                    var position = heading * (cycle * phase);
                    var pose = rig.Evaluate(phase, mirrored, position, Vector2.One, heading);
                    var bounds = pose.Bounds;
                    Check(bounds.Minimum.X >= position.X - .0001f && bounds.Minimum.Y >= position.Y - .0001f
                        && bounds.Maximum.X <= position.X + 1.0001f && bounds.Maximum.Y <= position.Y + 1.0001f,
                        "All upright articulated geometry fits its cell at diagonal, vertical and horizontal headings.");
                    Check(Math.Abs(Vector2.Distance(pose.NearHip, pose.NearKnee) - rig.ThighLength) < .0001f
                        && Math.Abs(Vector2.Distance(pose.NearKnee, pose.NearAnkle) - rig.ShinLength) < .0001f,
                        "Two-dimensional steps preserve exact bone lengths without IK clamping.");
                    if (pose.NearStance)
                    {
                        if (nearWasPlanted) Check(Vector2.Distance(nearPlant, pose.NearFoot) < .0001f,
                            "The near supporting foot is stationary in both world axes at a steady heading.");
                        else nearPlant = pose.NearFoot;
                    }
                    if (pose.FarStance)
                    {
                        if (farWasPlanted) Check(Vector2.Distance(farPlant, pose.FarFoot) < .0001f,
                            "The far supporting foot is stationary in both world axes at a steady heading.");
                        else farPlant = pose.FarFoot;
                    }
                    nearWasPlanted = pose.NearStance; farWasPlanted = pose.FarStance;
                }
            }
        }
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
        Check(rigs.TryGet("Bimble", "Navigator", out var transitionRig) && transitionRig != null, "Turn checks use an accepted rig.");
        gait.Update(point, 96, 0, false, false, 0, .34f, directionalStride: true);
        var phaseBeforePause = 0f;
        foreach (var direction in new[] { Vector2.UnitX, Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY })
        {
            for (var frame = 0; frame < 72; frame++)
            {
                var previousHeading = gait.Heading;
                var before = transitionRig!.Evaluate(gait.Phase, false, point / 96, Vector2.One, gait.Heading);
                point += direction * (96 * .36f / 144);
                gait.Update(point, 96, 1f / 144, true, direction.X < 0, 0, .34f, directionalStride: true);
                var change = Math.Abs(MathF.Atan2(previousHeading.X * gait.Heading.Y - previousHeading.Y * gait.Heading.X,
                    Vector2.Dot(previousHeading, gait.Heading)));
                Check(change <= MathF.Tau * 2 / 144 + .0001f && Math.Abs(gait.Heading.Length() - 1) < .0001f,
                    "Even a sharp path turn changes heading continuously at a bounded rate.");
                var after = transitionRig.Evaluate(gait.Phase, false, point / 96, Vector2.One, gait.Heading);
                Check(Vector2.Distance(before.NearFoot, after.NearFoot) < .035f && Vector2.Distance(before.FarFoot, after.FarFoot) < .035f,
                    "Filtered turns remain within the valid IK range and introduce no abrupt foot-position reset.");
            }
            phaseBeforePause = gait.Phase; var stoppedHeading = gait.Heading;
            var stopped = transitionRig!.Evaluate(gait.Phase, false, point / 96, Vector2.One, gait.Heading);
            gait.Update(point, 96, .05f, false, direction.X < 0, 0, .34f, directionalStride: true);
            Check(gait.Phase == phaseBeforePause && gait.Heading == stoppedHeading, "Stopping preserves phase and heading without a reset.");
            var frozen = transitionRig.Evaluate(gait.Phase, false, point / 96, Vector2.One, gait.Heading);
            Check(stopped.NearFoot == frozen.NearFoot && stopped.FarFoot == frozen.FarFoot,
                "Stopped skeletal feet remain exactly stationary; existing idle artwork is selected separately.");
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
