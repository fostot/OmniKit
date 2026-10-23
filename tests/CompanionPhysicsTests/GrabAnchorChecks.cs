using System.Numerics;
using OmniKit.Ui;

internal static class GrabAnchorChecks
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool valid, string reason) { if (!valid) throw new InvalidOperationException(reason); checks++; }
        var min = new Vector2(100, 50); var max = new Vector2(1100, 850);
        foreach (var edge in new[] { 48f, 96f, 144f, 192f })
        foreach (var click in new[] { new Vector2(.1f, .1f), new Vector2(.9f, .1f), new Vector2(.5f), new Vector2(.1f, .9f), new Vector2(.9f, .9f) })
        foreach (var rate in new[] { 60, 144 })
        {
            var size = new Vector2(edge); var origin = new Vector2(400, 300); var pointer = origin + size * click;
            var physics = new CompanionPhysics(); physics.PickUp(origin, pointer, false);
            Check(physics.Position == origin, "Picking any point preserves the exact initial body position.");
            physics.Carry(pointer, min, max, size, 0);
            Check(physics.Position == origin, "The centre transition has no instantaneous jump at pickup.");
            var previous = physics.Position; var target = pointer - size * .5f - new Vector2(0, Math.Clamp(edge * .07f, 3, 12));
            var previousDistance = Vector2.Distance(previous, target);
            for (var frame = 0; frame < (int)Math.Ceiling(rate * .25f); frame++)
            {
                physics.Carry(pointer, min, max, size, 1f / rate);
                var distance = Vector2.Distance(physics.Position, target);
                Check(distance <= previousDistance + .0001f, "The easing approaches one centred grip without oscillation or overshoot.");
                previous = physics.Position; previousDistance = distance;
            }
            Check(Vector2.Distance(physics.Position, target) < .0001f, "Every initial click converges to the same body centre and small lift.");
            foreach (var delta in new[] { new Vector2(25, 0), new Vector2(-25, 0), new Vector2(0, 25), new Vector2(0, -25), new Vector2(19, 23) })
            {
                physics.Carry(pointer + delta, min, max, size, 1f / rate);
                Check(Vector2.Distance(physics.Position, target + delta) < .0001f, "Settled carrying follows the pointer in every direction without joint drift.");
            }
            foreach (var pointerEdge in new[] { min - new Vector2(500), max + new Vector2(500),
                new Vector2(min.X - 500, max.Y + 500), new Vector2(max.X + 500, min.Y - 500) })
            {
                physics.Carry(pointerEdge, min, max, size, 1f / rate);
                Check(physics.Position.X >= min.X && physics.Position.Y >= min.Y
                    && physics.Position.X + size.X <= max.X && physics.Position.Y + size.Y <= max.Y,
                    "Viewport edges clamp the whole carried body before hand positioning.");
                var center = CompanionGrabAnchor.Center(physics.Position, size);
                foreach (var handScale in new[] { .2f, .4f, 1f })
                for (var pose = 0; pose < 4; pose++)
                {
                    var handSize = size * handScale;
                    var top = CompanionGrabAnchor.GripTopAt(center, handSize, pose);
                    var contact = top + handSize * CompanionGrabAnchor.GripInCell(pose);
                    Check(Vector2.Distance(contact, center) < .0001f,
                        "Every hand pose and size contacts the actual clamped centre, including screen edges.");
                }
            }
        }
        var clickOffset = new Vector2(7, 19); var itemSize = new Vector2(160, 100); var location = new Vector2(300, 240);
        Check(CompanionGrabAnchor.CarryTopAt(location, itemSize, clickOffset, 0) == location - clickOffset,
            "The same pure centre transition preserves a house's initial click offset without touching its saved anchor.");
        Check(CompanionGrabAnchor.CarryTopAt(location, itemSize, clickOffset, float.NaN) == location - clickOffset,
            "Non-finite elapsed time does not advance the centre transition.");
        foreach (var offset in new[] { new Vector2(10, 15), new Vector2(85, 82) })
        {
            var stationary = new CompanionPhysics(); var initial = new Vector2(400, 250); var pointer = initial + offset;
            stationary.PickUp(initial, pointer, false);
            stationary.Carry(pointer, min, max, new Vector2(96), .05f);
            var release = stationary.Position;
            var options = new CompanionPhysicsOptions(false, false, true);
            stationary.Drop(pointer, [], min, max, new Vector2(96), options);
            stationary.Update([], min, max, new Vector2(96), .05f, true, false, options);
            Check(stationary.Position.X == release.X, "An early release after a stationary pickup gains no sideways momentum from centering.");
        }
        return checks;
    }
}
