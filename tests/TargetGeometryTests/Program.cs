using System.Numerics;
using OmniKit.Interaction;

var checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}

// Synthetic perspective camera: a target 30 yalms ahead, with 70-degree vertical
// FOV. Camera pitch rotates that point; no game state or screenshots are used.
static (Vector2 Screen, bool Visible) Project(float pitchDegrees, Vector2 viewport, Vector2 origin)
{
    var pitch = pitchDegrees * MathF.PI / 180;
    var depth = 30 * MathF.Cos(pitch);
    var height = 30 * MathF.Sin(pitch);
    var focalLength = viewport.Y * .5f / MathF.Tan(35 * MathF.PI / 180);
    return (origin + new Vector2(viewport.X * .5f, viewport.Y * .5f - height / depth * focalLength), depth > 0);
}

foreach (var viewport in new[] { new Vector2(1920, 1080), new Vector2(3440, 1440), new Vector2(1080, 1920) })
foreach (var origin in new[] { Vector2.Zero, new Vector2(150, 80) })
{
    foreach (var pitch in new[] { -25f, -18f, -12f, -8f, -5f, 0f, 5f, 8f, 12f, 18f, 25f })
    {
        var projected = Project(pitch, viewport, origin);
        Check(SoftTargetGeometry.TryScore(projected.Screen, projected.Visible, origin, viewport, .1f, 30, 30, false, out _),
            "A visible 30-yalm target ahead survives pitch changes, independent of viewport aspect/offset.");
    }
    var outside = Project(40, viewport, origin);
    Check(!SoftTargetGeometry.TryScore(outside.Screen, outside.Visible, origin, viewport, .1f, 30, 30, true, out _),
        "Pitch that actually moves the target offscreen loses even an incumbent.");
    var behind = Project(180, viewport, origin);
    Check(!SoftTargetGeometry.TryScore(behind.Screen, behind.Visible, origin, viewport, .1f, 30, 30, true, out _),
        "A behind-camera target cannot be acquired or retained despite plausible screen coordinates.");
}

var size = new Vector2(1920, 1080);
var center = size * .5f;
bool Score(Vector2 point, out float score, bool incumbent = false, float distance = 30, float maximum = 30, bool visible = true) =>
    SoftTargetGeometry.TryScore(point, visible, Vector2.Zero, size, .1f, distance, maximum, incumbent, out score);

var shifted = Project(8, size, Vector2.Zero);
Check(Vector2.Distance(shifted.Screen, center) > 108 && Score(shifted.Screen, out _),
    "Reproduces the old failure: only eight degrees of pitch leaves the 108px circle but remains eligible.");
Check(Score(center + new Vector2(108, 300), out _), "Vertical pitch does not consume the horizontal aiming budget.");
Check(!Score(center + new Vector2(109, 0), out _), "A new target outside the horizontal aim band is rejected.");
Check(Score(center + new Vector2(115, 0), out _, incumbent: true), "Small horizontal jitter retains the current eligible target.");
Check(!Score(center + new Vector2(125, 0), out _, incumbent: true), "Hysteresis cannot keep a target after deliberate horizontal camera movement.");
Check(!Score(center + new Vector2(600, 0), out _), "Visible side targets remain excluded.");
foreach (var point in new[] { new Vector2(-1, 540), new Vector2(1920, 540), new Vector2(960, -1), new Vector2(960, 1080) })
    Check(!Score(point, out _, incumbent: true), "No viewport edge is relaxed for an incumbent.");
Check(!Score(center, out _, visible: false), "Native projection rejection cannot be bypassed by the geometry score.");
Check(Score(center, out _, distance: 30) && !Score(center, out _, incumbent: true, distance: 30.01f), "Configured world range remains strict, including for incumbents.");
Check(!Score(center, out _, distance: 7, maximum: 6), "A shorter saved detection distance remains effective.");
Score(center + new Vector2(10, 350), out var aheadScore);
Score(center + new Vector2(80, 0), out var sideScore);
Check(aheadScore < sideScore, "Horizontal alignment dominates vertical crosshair proximity.");
Score(center, out var nearScore, distance: 3);
Score(center, out var farScore, distance: 30);
Check(nearScore < farScore, "Distance breaks equal-alignment ties.");
Score(center + new Vector2(5, 0), out var incumbentScore, incumbent: true);
Score(center, out var challengerScore);
Check(incumbentScore < challengerScore, "Small adjacent-target jitter does not cause flicker.");
Score(center + new Vector2(60, 0), out incumbentScore, incumbent: true);
Check(challengerScore < incumbentScore, "A clearly better-aligned target defeats hysteresis.");
foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    Check(!Score(new Vector2(invalid, 540), out _), "Non-finite projections fail closed.");
    Check(!Score(center, out _, distance: invalid), "Non-finite world distances fail closed.");
}
Check(!SoftTargetGeometry.TryScore(center, true, Vector2.Zero, Vector2.Zero, .1f, 1, 30, false, out _), "An unavailable viewport fails closed.");
Console.WriteLine($"PASS: {checks} synthetic target-geometry checks; 30-yalm pitch tolerance, explicit viewport/behind-camera rejection, horizontal selection, strict saved range and bounded hysteresis. Native visibility/occlusion and gameplay require live validation.");
