using System.Numerics;
using OmniKit.Ui;

internal static class MotionDynamicsChecks
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        var dynamics = new CompanionMotionDynamics();
        dynamics.Update(.01f, true, Vector2.Zero, false, false, CompanionPose.Idle, 0);
        dynamics.Update(.01f, true, new(1, 0), true, false, CompanionPose.Walk, 0);
        Check(dynamics.State == CompanionMotionState.Starting && dynamics.Compression > 0, "Starting produces one bounded anticipation impulse.");
        var paused = (dynamics.Compression, dynamics.Lean, dynamics.Turn, dynamics.State);
        dynamics.Update(.05f, false, new(100, 0), false, true, CompanionPose.Idle, 9);
        Check(paused == (dynamics.Compression, dynamics.Lean, dynamics.Turn, dynamics.State), "A paused reaction freezes all output and event history.");
        dynamics.Update(.01f, true, new(2, 0), true, true, CompanionPose.Walk, 0);
        Check(dynamics.State == CompanionMotionState.Turning && dynamics.Turn > 0, "A real moving reversal produces a local forward whole-body turn response.");
        dynamics.Update(.01f, true, new(2, 0), false, true, CompanionPose.Idle, 0);
        Check(dynamics.State == CompanionMotionState.Stopping, "Stopping has its own settling reaction.");
        dynamics.Update(.01f, true, new(600, 0), true, true, CompanionPose.Walk, 0);
        Check(dynamics.Compression == 0 && dynamics.Lean == 0 && dynamics.Turn == 0, "Teleport rebases without firing a momentum impulse.");
        dynamics.Update(.01f, true, new(601, 0), false, true, CompanionPose.Land, 1, 192);
        Check(dynamics.Compression == 0 && dynamics.Lean == 0, "Changed surface and resized body rebase their sample.");
        dynamics.Update(.01f, true, new(602, 0), false, true, CompanionPose.Carry, 1, 192);
        Check(dynamics.State == CompanionMotionState.Carried && dynamics.Compression == 0, "Carrying suppresses dynamics and preserves the shared grip.");
        dynamics.Update(.01f, true, new(602, 0), false, true, CompanionPose.Fall, 1, 192);
        Check(dynamics.State == CompanionMotionState.Falling && dynamics.Compression == 0, "Drop rebases from carry rather than inventing a release impulse.");
        dynamics.Update(.01f, true, new(602, 0), false, true, CompanionPose.Land, 1, 192);
        Check(dynamics.State == CompanionMotionState.Settling && dynamics.Compression > 0, "Landing produces a damped compression.");
        for (var i = 0; i < 400; i++)
        {
            dynamics.Update(.01f, true, new(602, 0), false, true, CompanionPose.Idle, 1, 192);
            Check(float.IsFinite(dynamics.Compression) && Math.Abs(dynamics.Compression) <= .04f
                && Math.Abs(dynamics.Lean) <= .03f && Math.Abs(dynamics.Turn) <= .025f, "Every spring remains finite and within whole-body limits.");
        }
        Check(dynamics.Compression == 0 && dynamics.Lean == 0 && dynamics.Turn == 0, "Settled idle has no continuing animation-clock jitter.");
        dynamics.Update(float.NaN, true, new(float.NaN, 0), true, false, CompanionPose.Walk, 0);
        Check(dynamics.Compression == 0, "Invalid elapsed time cannot advance dynamics.");
        dynamics.Update(.01f, true, new(float.NaN, 0), true, false, CompanionPose.Walk, 0);
        Check(dynamics.Compression == 0 && dynamics.Lean == 0, "Invalid motion input clears the response.");
        var a = Sample(60); var b = Sample(144);
        Check(Math.Abs(a.Compression - b.Compression) < .000001f && Math.Abs(a.Lean - b.Lean) < .000001f,
            "Exact damped response agrees at 60 and 144 fps for matching physical event times.");
        var mirrored = Sample(144, true);
        Check(Math.Abs(b.Compression - mirrored.Compression) < .000001f && Math.Abs(b.Lean - mirrored.Lean) < .000001f,
            "Mirrored travel provides the same local coefficients so the renderer reflects them exactly once.");
        return checks;
    }

    private static CompanionMotionDynamics Sample(int fps, bool mirrored = false)
    {
        var result = new CompanionMotionDynamics();
        result.Update(.01f, true, Vector2.Zero, false, mirrored, CompanionPose.Idle, 0);
        for (var i = 1; i <= fps / 2; i++) result.Update(1f / fps, true, new(i * (mirrored ? -20f : 20f) / fps, 0), true, mirrored, CompanionPose.Walk, 0);
        return result;
    }
}
