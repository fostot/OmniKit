using System.Numerics;
using System.Text.Json;
using OmniKit.Ui;

internal static class RigChecks
{
    internal static int Run()
    {
        var count = 0;
        void Check(bool pass, string message) { if (!pass) throw new Exception(message); count++; }
        bool Near(Vector2 a, Vector2 b, float tolerance = .0001f) => Vector2.Distance(a, b) < tolerance;
        var parts = Enumerable.Range(0, 9).Select(i => new LunarRigPart(new Vector4(i * 20, 0, i * 20 + 12, 40),
            i is 7 or 8 ? new Vector2(.28f, .1f) : new Vector2(.43f, .15f), new Vector2(.52f, .85f))).ToArray();
        var rig = new LunarWalkRig("synthetic-parts.png", new Vector2(256), parts, new Vector4(.27f, .06f, .46f, .59f),
            new Vector2(.52f, .62f), new Vector2(.52f, .4f), .15f, .14f, new Vector2(.15f, .055f), .21f, .4f, true);
        var rigidBody = new LunarWalkRig("synthetic-parts.png", new Vector2(256), parts, new Vector4(.27f, .06f, .46f, .59f),
            new Vector2(.52f, .62f), new Vector2(.52f, .4f), .15f, .14f, new Vector2(.15f, .055f), .21f, .4f);
        var front = rig.Evaluate(0, false, Vector2.Zero, Vector2.One);
        var contact = front.NearFoot;
        // Phase comes directly from distance / cycleDistance. A planted foot must
        // remain at exactly this world point while the body travels past it.
        foreach (var phase in new[] { .05f, .15f, .3f, .45f, .59f })
        {
            var pose = rig.Evaluate(phase, false, new Vector2(phase * rig.CycleDistance, 0), Vector2.One);
            Check(pose.NearStance && Near(pose.NearFoot, contact), "A stance foot must stay stationary in world space.");
            var mirrored = rig.Evaluate(phase, true, new Vector2(-phase * rig.CycleDistance, 0), Vector2.One);
            Check(Near(mirrored.NearFoot, new Vector2(1 - contact.X, contact.Y)), "Mirrored stance feet stay planted during leftward walking.");
        }
        var middleSwing = rig.Evaluate(.8f, false, Vector2.Zero, Vector2.One);
        Check(!middleSwing.NearStance && middleSwing.NearFoot.Y < .85f && middleSwing.FarStance,
            "One foot visibly lifts during swing while the opposite foot supports the body.");
        var opposite = rig.Evaluate(.5f, false, Vector2.Zero, Vector2.One);
        Check(front.NearFoot.X > front.NearHip.X && front.FarFoot.X < front.FarHip.X
            && opposite.NearFoot.X < opposite.NearHip.X && opposite.FarFoot.X > opposite.FarHip.X,
            "Near and far legs genuinely exchange front/back roles across the cycle.");
        var nearArm = front.Quads.Single(q => q.Part == 1);
        Check(((nearArm.C + nearArm.D) * .5f).X < ((nearArm.A + nearArm.B) * .5f).X,
            "The near arm swings back when the near foot is forward.");
        Check(front.Quads.Select(q => q.Part).SequenceEqual(new[] { 2, 4, 6, 8, 3, 5, 7, 0, 0, 0, 0, 0, 0, 1 }),
            "Far limbs render first, legs under shorts, then body and near arm.");
        var loop = rig.Evaluate(1, false, Vector2.Zero, Vector2.One);
        var beforeLoop = rig.Evaluate(.99999f, false, Vector2.Zero, Vector2.One);
        foreach (var q in front.Quads)
        {
            var end = loop.Quads.Single(value => value.Part == q.Part && value.Section == q.Section);
            var before = beforeLoop.Quads.Single(value => value.Part == q.Part && value.Section == q.Section);
            Check(Near(q.A, end.A) && Near(q.B, end.B) && Near(q.C, end.C) && Near(q.D, end.D), "The zero/one loop boundary is identical.");
            Check(Near(q.A, before.A) && Near(q.C, before.C), "The approach to the loop boundary is continuous.");
        }
        foreach (var boundary in new[] { .1f, .5f, .6f })
        {
            var a = rig.Evaluate(boundary - .00001f, false, Vector2.Zero, Vector2.One);
            var b = rig.Evaluate(boundary + .00001f, false, Vector2.Zero, Vector2.One);
            Check(Near(a.NearFoot, b.NearFoot) && Near(a.FarFoot, b.FarFoot), "Contact and toe-off transitions have no position jump.");
        }
        for (var sample = 0; sample < 128; sample++)
        {
            var pose = rig.Evaluate(sample / 128f, false, Vector2.Zero, Vector2.One);
            var rigid = rigidBody.Evaluate(sample / 128f, false, Vector2.Zero, Vector2.One);
            Check(Math.Abs(Vector2.Distance(pose.NearHip, pose.NearKnee) - rig.ThighLength) < .0001f
                && Math.Abs(Vector2.Distance(pose.NearKnee, pose.NearAnkle) - rig.ShinLength) < .0001f
                && Math.Abs(Vector2.Distance(pose.FarHip, pose.FarKnee) - rig.ThighLength) < .0001f
                && Math.Abs(Vector2.Distance(pose.FarKnee, pose.FarAnkle) - rig.ShinLength) < .0001f,
                "Two-bone IK preserves both leg lengths throughout the entire cycle.");
            var bounds = pose.Bounds;
            Check(bounds.Minimum.X >= 0 && bounds.Minimum.Y >= 0 && bounds.Maximum.X <= 1 && bounds.Maximum.Y <= 1,
                "All articulated quads remain inside the logical cell.");
            foreach (var part in pose.Quads)
            {
                var start = front.Quads.Single(q => q.Part == part.Part && q.Section == part.Section);
                if (part.Part != 0) Check(Math.Abs(Vector2.Distance(part.A, part.B) - Vector2.Distance(start.A, start.B)) < .0001f
                    && Math.Abs(Vector2.Distance(part.B, part.C) - Vector2.Distance(start.B, start.C)) < .0001f,
                    "Animation rotates/translates each part without changing its scale.");
                if (part.Part is 7 or 8)
                {
                    Check(Math.Abs(part.C.Y - part.D.Y) < .0001f, "Feet remain flat instead of rotating with the shins.");
                    var pivot = part.A + (part.B - part.A) * parts[part.Part].Pivot.X + (part.D - part.A) * parts[part.Part].Pivot.Y;
                    Check(Near(pivot, part.Part == 7 ? pose.NearAnkle : pose.FarAnkle), "The actual cropped foot ankle pivot joins the shin.");
                    if (part.Part == 7 && pose.NearStance || part.Part == 8 && pose.FarStance)
                        Check(Math.Abs(part.C.Y - .9f) < .0001f, "A planted foot's sole stays on the logical ground.");
                }
                if (part.Part is 3 or 4 or 5 or 6)
                {
                    var pivot = part.A + (part.B - part.A) * parts[part.Part].Pivot.X + (part.D - part.A) * parts[part.Part].Pivot.Y;
                    var (proximal, distal) = part.Part switch
                    {
                        3 => (pose.NearHip, pose.NearKnee), 4 => (pose.FarHip, pose.FarKnee),
                        5 => (pose.NearKnee, pose.NearAnkle), _ => (pose.FarKnee, pose.FarAnkle),
                    };
                    var endPivot = part.A + (part.B - part.A) * parts[part.Part].EndPivot.X + (part.D - part.A) * parts[part.Part].EndPivot.Y;
                    Check(Near(pivot, proximal) && Near(endPivot, distal),
                        "Both internal cropped limb pivots join the exact skeletal joints with overlapping ends.");
                }
                if (part.Part == 0)
                {
                    var unflexed = rigid.Quads.Single(q => q.Part == 0 && q.Section == part.Section);
                    Check(Vector2.Distance(part.A, unflexed.A) <= .015f && Vector2.Distance(part.C, unflexed.C) <= .015f,
                        "Ear-band follow-through stays bounded to .015 logical cell units.");
                    if (part.Section >= 2) Check(Near(part.A, unflexed.A) && Near(part.B, unflexed.B) && Near(part.C, unflexed.C) && Near(part.D, unflexed.D),
                        "The lower two thirds of the body are not flexed.");
                    if (part.Section < 5)
                    {
                        var next = pose.Quads.Single(q => q.Part == 0 && q.Section == part.Section + 1);
                        Check(Near(part.D, next.A) && Near(part.C, next.B) && Near(part.UvD, next.UvA) && Near(part.UvC, next.UvB),
                            "Body band geometry and UVs share exact boundaries without cracks.");
                    }
                }
            }
        }
        var directory = Path.Combine(AppContext.BaseDirectory, "assets");
        var metadataPath = Path.Combine(directory, "lunar-walk-rigs.json");
        if (File.Exists(metadataPath))
        {
            using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
            var entries = metadata.RootElement.GetProperty("rigs");
            var production = new LunarWalkRigLibrary(directory);
            Check(production.Count == entries.GetArrayLength(), "Every packaged articulated rig passes geometry validation.");
            foreach (var entry in entries.EnumerateArray())
            {
                Check(production.TryGet(entry.GetProperty("character").GetString()!, entry.GetProperty("outfit").GetString()!, out var accepted) && accepted != null,
                    "Every packaged character/outfit resolves its actual rig.");
                foreach (var mirrored in new[] { false, true })
                foreach (var cellWidth in new[] { 48f, 96f, 192f })
                {
                    var origin = new Vector2(200, 100); var cell = new Vector2(cellWidth);
                    var initial = accepted!.Evaluate(0, mirrored, origin, cell);
                    for (var sample = 0; sample < 128; sample++)
                    {
                        var phase = sample / 128f;
                        var placement = origin + new Vector2((mirrored ? -1 : 1) * phase * accepted.CycleDistance * cellWidth, 0);
                        var actual = accepted.Evaluate(phase, mirrored, placement, cell);
                        if (actual.NearStance) Check(Vector2.Distance(actual.NearFoot, initial.NearFoot) < .002f,
                            "A production stance foot remains stationary at actual output sizes.");
                        var bounds = actual.Bounds;
                        Check(bounds.Minimum.X >= placement.X && bounds.Minimum.Y >= placement.Y
                            && bounds.Maximum.X <= placement.X + cell.X && bounds.Maximum.Y <= placement.Y + cell.Y,
                            "Every production rig frame fits its real logical cell in both directions.");
                    }
                }
            }
        }
        return count;
    }
}
