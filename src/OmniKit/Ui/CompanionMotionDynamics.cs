using System.Numerics;

namespace OmniKit.Ui;

internal enum CompanionMotionState { Idle, Starting, Moving, Stopping, Turning, Carried, Falling, Settling, Sleeping }

/// <summary>Small whole-body reactions to actual motion events, independent of distance-driven gait.</summary>
internal sealed class CompanionMotionDynamics
{
    private bool initialized, wasMoving, wasMirrored;
    private Vector2 previous;
    private float previousWidth, stateTime;
    private ulong previousSpace;
    private CompanionPose previousPose;
    private float compression, compressionVelocity, lean, leanVelocity, turn, turnVelocity;

    public CompanionMotionState State { get; private set; }
    public float Compression => Math.Clamp(compression, -.04f, .04f);
    public float Lean => Math.Clamp(lean, -.03f, .03f);
    public float Turn => Math.Clamp(turn, -.025f, .025f);

    public void Update(float elapsed, bool animate, Vector2 position, bool moving, bool mirrored,
        CompanionPose pose, ulong space, float cellWidth = 96)
    {
        if (!animate) return;
        var dt = float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0;
        if (dt <= 0) return;
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(cellWidth) || cellWidth <= 0)
        { initialized = false; Clear(); return; }
        var relocation = !initialized || previousWidth != cellWidth || previousSpace != space
            || Vector2.DistanceSquared(position, previous) > cellWidth * cellWidth * .0625f;
        var previousCarried = previousPose == CompanionPose.Carry;
        previous = position; previousWidth = cellWidth; previousSpace = space;
        if (relocation || pose == CompanionPose.Carry || previousCarried)
        {
            Clear(); initialized = true;
            State = pose == CompanionPose.Carry ? CompanionMotionState.Carried
                : pose == CompanionPose.Fall ? CompanionMotionState.Falling
                : moving ? CompanionMotionState.Moving : CompanionMotionState.Idle;
        }
        else
        {
            // Reactions use local forward coordinates; the body renderer mirrors once.
            if (pose == CompanionPose.Land && previousPose != pose)
            {
                compressionVelocity += .8f; leanVelocity += .12f;
                Enter(CompanionMotionState.Settling);
            }
            else if (pose == CompanionPose.Fall)
            {
                if (previousPose != pose) compressionVelocity -= .2f;
                Enter(CompanionMotionState.Falling);
            }
            else if (moving && wasMoving && mirrored != wasMirrored)
            {
                leanVelocity += .3f; turnVelocity += .28f;
                Enter(CompanionMotionState.Turning);
            }
            else if (moving && !wasMoving)
            {
                compressionVelocity += .32f; leanVelocity += .2f;
                Enter(CompanionMotionState.Starting);
            }
            else if (!moving && wasMoving)
            {
                compressionVelocity += .4f; leanVelocity -= .25f;
                Enter(CompanionMotionState.Stopping);
            }
            stateTime += dt;
            Spring(ref compression, ref compressionVelocity, 13, dt);
            Spring(ref lean, ref leanVelocity, 11, dt);
            Spring(ref turn, ref turnVelocity, 12, dt);
            if (stateTime > .45f && State is not CompanionMotionState.Falling)
                State = moving ? CompanionMotionState.Moving : CompanionMotionState.Idle;
        }
        wasMoving = moving; wasMirrored = mirrored; previousPose = pose;
    }

    private void Enter(CompanionMotionState state) { State = state; stateTime = 0; }
    private void Clear()
    {
        compression = compressionVelocity = lean = leanVelocity = turn = turnVelocity = stateTime = 0;
        State = CompanionMotionState.Idle;
    }

    // Exact critically damped response avoids frame-count-dependent easing.
    private static void Spring(ref float value, ref float velocity, float frequency, float dt)
    {
        var coupled = velocity + frequency * value;
        var decay = MathF.Exp(-frequency * dt);
        velocity = (velocity - frequency * coupled * dt) * decay;
        value = (value + coupled * dt) * decay;
        if (Math.Abs(value) < .000001f && Math.Abs(velocity) < .000001f) value = velocity = 0;
    }
}
