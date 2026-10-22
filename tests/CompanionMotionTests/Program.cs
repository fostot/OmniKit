using System.Numerics;
using OmniKit.Ui;

var checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}
bool Close(float a, float b) => Math.Abs(a - b) < .001f;
void Contained(CompanionMotion motion, CompanionLayout layout)
{
    var end = motion.Position + layout.SpriteSize;
    Check(motion.Position.X >= layout.Minimum.X - .001f && motion.Position.Y >= layout.Minimum.Y - .001f
        && end.X <= layout.Maximum.X + .001f && end.Y <= layout.Maximum.Y + .001f,
        "The entire sprite cell, including its gutters, must stay inside its area.");
}

var layout = CompanionLayout.Create(new Vector2(200, 50), new Vector2(620, 500),
    new Vector4(.1f, .2f, .9f, .9f), 96, 1.5f, 1);
var motion = new CompanionMotion(17);
motion.Update(layout, 0, true);
Check(Close(motion.Position.X, layout.Minimum.X), "The companion starts at the area's left edge.");
Contained(motion, layout);
var previous = motion.Position;
motion.Update(layout, 1000, true);
Check(Vector2.Distance(previous, motion.Position) <= layout.SpriteSize.Y * .36f * .05f + .001f,
    "Long stalls must not teleport the companion.");
previous = motion.Position;
motion.Update(layout, float.NaN, true);
Check(motion.Position == previous, "A nonfinite frame delta must not move the companion.");
motion.Update(layout, -5, true);
Check(motion.Position == previous, "A negative frame delta must not move the companion.");
motion.Update(layout, .05f, false);
Check(motion.Position == previous && motion.Frame == 8 && !motion.Walking,
    "Disabling animations stops both position and animation at the resting frame.");

var sawLeft = false;
var sawRight = false;
var sawPause = false;
var sawAnimation = false;
for (var frame = 0; frame < 6000; frame++)
{
    motion.Update(layout, .05f, true);
    Contained(motion, layout);
    sawLeft |= motion.Walking && motion.FacingLeft;
    sawRight |= motion.Walking && !motion.FacingLeft;
    sawPause |= !motion.Walking;
    sawAnimation |= motion.Walking && motion.Frame > 0;
}
Check(sawLeft && sawRight && sawPause && sawAnimation, "Roaming must walk both ways, animate, and pause.");

foreach (var viewport in new[] { new Vector2(620, 500), new Vector2(80, 30), new Vector2(1, 1) })
foreach (var aspect in new[] { .5f, 1f, 2f })
{
    var small = CompanionLayout.Create(Vector2.Zero, viewport, new Vector4(.1f, .1f, .9f, .9f), 192, 2, aspect);
    motion.Update(small, .05f, true);
    Contained(motion, small);
    Check(Close(small.SpriteSize.X / small.SpriteSize.Y, aspect), "Fitting a narrow viewport preserves atlas-cell aspect ratio.");
}
var inverted = CompanionLayout.Create(Vector2.Zero, new Vector2(400, 300), new Vector4(.9f, .9f, .1f, .1f), 96, 1, 1);
Check(inverted.Minimum.X < inverted.Maximum.X && inverted.Minimum.Y < inverted.Maximum.Y,
    "Defensive layout handling never produces inverted bounds.");
Check(!CompanionLayout.Create(Vector2.Zero, Vector2.Zero, Vector4.One, 96, 1, 1).IsVisible,
    "An unavailable viewport renders nothing.");

foreach (var avatar in new[] { "Bimble", "Moonbear" })
for (var frame = 0; frame < 16; frame++)
{
    var origin = new Vector2(200, 140);
    var right = CompanionAtlas.GetFrame(avatar, frame, false, origin, new Vector2(144));
    var left = CompanionAtlas.GetFrame(avatar, frame, true, origin, new Vector2(144));
    Check(right.UvStart.X >= 0 && right.UvStart.Y >= 0 && right.UvEnd.X <= 1 && right.UvEnd.Y <= 1
        && right.UvStart.X < right.UvEnd.X && right.UvStart.Y < right.UvEnd.Y, "Every pose uses a nonempty bounded UV rectangle.");
    Check(left.UvStart.X == right.UvEnd.X && left.UvEnd.X == right.UvStart.X
        && left.UvStart.Y == right.UvStart.Y && left.UvEnd.Y == right.UvEnd.Y
        && left.Position == right.Position && left.Size == right.Size, "Mirroring changes UVs without changing size or anchor.");
    Check(right.Position.X >= origin.X && right.Position.Y >= origin.Y
        && right.Position.X + right.Size.X <= origin.X + 144 && right.Position.Y + right.Size.Y <= origin.Y + 144,
        "Every inspected full-body pose remains inside the logical sprite cell.");
    Check(Close(right.Position.Y + right.Size.Y, origin.Y + 144 * .9f), "Every pose shares a stable foot baseline.");
    var uvSize = right.UvEnd - right.UvStart;
    Check(Close(right.Size.X / right.Size.Y, uvSize.X / uvSize.Y), "Sampling preserves the original pose's aspect ratio.");
    var pixelsPerUnit = right.Size.X / (uvSize.X * 1254);
    Check(Close(pixelsPerUnit, 144 / (avatar == "Moonbear" ? 330f : 380f)), "Pose changes preserve source-pixel scale.");
    for (var other = frame + 1; other < 16; other++)
    {
        var adjacent = CompanionAtlas.GetFrame(avatar, other, false, origin, new Vector2(144));
        Check(right.UvEnd.X <= adjacent.UvStart.X || adjacent.UvEnd.X <= right.UvStart.X
            || right.UvEnd.Y <= adjacent.UvStart.Y || adjacent.UvEnd.Y <= right.UvStart.Y,
            "Frame regions never overlap or sample an adjacent pose.");
    }
}
foreach (var pose in new[] { CompanionPose.Walk, CompanionPose.Idle, CompanionPose.Talk })
{
    var seen = new HashSet<int>();
    for (var step = 0; step < 500; step++)
    {
        var selected = CompanionAtlas.SelectFrame(pose, step / 100f, true);
        seen.Add(selected);
        Check(CompanionAtlas.SelectFrame(pose, step / 100f, false) == 8, "Animations disabled always uses the resting frame.");
    }
    Check(seen.SetEquals(pose == CompanionPose.Walk ? Enumerable.Range(0, 8)
        : pose == CompanionPose.Talk ? Enumerable.Range(12, 4) : Enumerable.Range(8, 4)),
        "Every requested animation reaches all and only its intended frames.");
}
Check(CompanionAtlas.SelectFrame(CompanionPose.Walk, 1f / 12f + .001f, true) == 1, "Walking uses twelve frames per second.");
Check(CompanionAtlas.SelectFrame(CompanionPose.Walk, float.NaN, true) == 0, "Nonfinite animation time fails safely.");
Check(CompanionAtlas.FileName("Bimble", "Navigator") == "bimble-motion.png"
    && CompanionAtlas.FileName("Bimble", "Stargazer") == "bimble-stargazer.png"
    && CompanionAtlas.FileName("Moonbear", "Navigator") == "moonbear-motion.png"
    && CompanionAtlas.FileName("Moonbear", "Stargazer") == "moonbear-stargazer.png", "Character and outfit select all four expected atlases.");
Check(CompanionLayout.TryNormalizeArea(new Vector2(500, 400), new Vector2(100, 100), Vector2.Zero,
    new Vector2(620, 500), new Vector2(96), out var area) && area.X < area.Z && area.Y < area.W,
    "A drag in either direction produces ordered normalized bounds.");
Check(!CompanionLayout.TryNormalizeArea(Vector2.Zero, new Vector2(20), Vector2.Zero,
    new Vector2(620, 500), new Vector2(96), out _), "A selection smaller than the entire avatar is rejected.");
Check(CompanionLayout.TryNormalizeArea(new Vector2(-100), new Vector2(900), Vector2.Zero,
    new Vector2(620, 500), new Vector2(96), out area) && area == new Vector4(0, 0, 1, 1),
    "Selections are clamped to the viewport before saving.");

Console.WriteLine($"Companion motion/layout: {checks} checks passed.");
