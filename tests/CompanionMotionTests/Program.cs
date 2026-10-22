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
Check(motion.Position == previous && motion.Frame == 0 && !motion.Walking,
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
    sawAnimation |= motion.Frame > 0;
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

for (var row = 0; row < 2; row++)
for (var frame = 0; frame < 4; frame++)
{
    var right = CompanionLayout.FrameUv(frame, row, false);
    var left = CompanionLayout.FrameUv(frame, row, true);
    Check(Close(right.Start.X, frame / 4f) && Close(right.End.X, (frame + 1) / 4f)
        && Close(right.Start.Y, row / 2f) && Close(right.End.Y, (row + 1) / 2f), "UVs select the exact atlas cell.");
    Check(left.Start.X == right.End.X && left.End.X == right.Start.X
        && left.Start.Y == right.Start.Y && left.End.Y == right.End.Y, "Leftward walking mirrors only the horizontal axis.");
}
Check(CompanionLayout.TryNormalizeArea(new Vector2(500, 400), new Vector2(100, 100), Vector2.Zero,
    new Vector2(620, 500), new Vector2(96), out var area) && area.X < area.Z && area.Y < area.W,
    "A drag in either direction produces ordered normalized bounds.");
Check(!CompanionLayout.TryNormalizeArea(Vector2.Zero, new Vector2(20), Vector2.Zero,
    new Vector2(620, 500), new Vector2(96), out _), "A selection smaller than the entire avatar is rejected.");
Check(CompanionLayout.TryNormalizeArea(new Vector2(-100), new Vector2(900), Vector2.Zero,
    new Vector2(620, 500), new Vector2(96), out area) && area == new Vector4(0, 0, 1, 1),
    "Selections are clamped to the viewport before saving.");

Console.WriteLine($"Companion motion/layout: {checks} checks passed.");
