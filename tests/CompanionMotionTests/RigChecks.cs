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
            new Vector2(.52f, .62f), new Vector2(.58f, .57f), new Vector2(.50f, .56f),
            new Vector2(.012f, 0), new Vector2(-.012f, -.004f), .15f, .14f, new Vector2(.15f, .055f), .21f, .4f, true);
        var rigidBody = new LunarWalkRig("synthetic-parts.png", new Vector2(256), parts, new Vector4(.27f, .06f, .46f, .59f),
            new Vector2(.52f, .62f), new Vector2(.58f, .57f), new Vector2(.50f, .56f),
            new Vector2(.012f, 0), new Vector2(-.012f, -.004f), .15f, .14f, new Vector2(.15f, .055f), .21f, .4f);
        var fullerParts = parts.Select((part, index) => part with { WidthScale = index is >= 3 and <= 6 ? 1.35f : 1f }).ToArray();
        var fuller = new LunarWalkRig("synthetic-parts.png", new Vector2(256), fullerParts, new Vector4(.27f, .06f, .46f, .59f),
            new Vector2(.52f, .62f), new Vector2(.58f, .57f), new Vector2(.50f, .56f),
            new Vector2(.012f, 0), new Vector2(-.012f, -.004f), .15f, .14f, new Vector2(.15f, .055f), .21f, .4f, true);
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
        Check(front.Quads.Select(q => q.Part).SequenceEqual(new[] { 2, 4, 6, 8, 3, 5, 7, 1, 0, 0, 0, 0, 0, 0 }),
            "Both sleeve roots stay behind the cape, and both legs behind the shorts.");
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
            var widened = fuller.Evaluate(sample / 128f, false, Vector2.Zero, Vector2.One);
            Check(widened.NearHip == pose.NearHip && widened.FarHip == pose.FarHip && widened.NearKnee == pose.NearKnee && widened.FarKnee == pose.FarKnee &&
                widened.NearAnkle == pose.NearAnkle && widened.FarAnkle == pose.FarAnkle && widened.NearFoot == pose.NearFoot && widened.FarFoot == pose.FarFoot &&
                widened.NearStance == pose.NearStance && widened.FarStance == pose.FarStance,
                "Fuller visual proportions preserve every joint, foot trajectory and stance flag exactly.");
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
                var widerPart = widened.Quads.Single(q => q.Part == part.Part && q.Section == part.Section);
                if (part.Part is >= 3 and <= 8)
                {
                    float Area(LunarRigQuad q) { var x = q.B - q.A; var y = q.D - q.A; return Math.Abs(x.X * y.Y - x.Y * y.X); }
                    Check(Math.Abs(Area(widerPart) / Area(part) - fullerParts[part.Part].WidthScale) < .0001f,
                        "Visual enlargement uses one constant transverse scale throughout the cycle.");
                    var startPivot = widerPart.A + (widerPart.B - widerPart.A) * fullerParts[part.Part].Pivot.X +
                        (widerPart.D - widerPart.A) * fullerParts[part.Part].Pivot.Y;
                    var joint = part.Part switch { 3 => pose.NearHip, 4 => pose.FarHip, 5 => pose.NearKnee, 6 => pose.FarKnee, 7 => pose.NearAnkle, _ => pose.FarAnkle };
                    Check(Near(startPivot, joint), "A widened part still joins its exact off-center proximal or ankle pivot.");
                    if (part.Part <= 6)
                    {
                        var endPivot = widerPart.A + (widerPart.B - widerPart.A) * fullerParts[part.Part].EndPivot.X +
                            (widerPart.D - widerPart.A) * fullerParts[part.Part].EndPivot.Y;
                        var distal = part.Part switch { 3 => pose.NearKnee, 4 => pose.FarKnee, 5 => pose.NearAnkle, _ => pose.FarAnkle };
                        Check(Near(endPivot, distal), "Transverse scaling preserves the distal joint even for slanted source bones.");
                    }
                }
                else Check(part == widerPart, "The upper body, ears and arms remain identical after lower-half enlargement.");
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
            var idleArtwork = new CompanionAnimationLibrary(directory);
            Check(production.Count == entries.GetArrayLength(), "Every packaged articulated rig passes geometry validation.");
            foreach (var entry in entries.EnumerateArray())
            {
                Check(production.TryGet(entry.GetProperty("character").GetString()!, entry.GetProperty("outfit").GetString()!, out var accepted) && accepted != null,
                    "Every packaged character/outfit resolves its actual rig.");
                Check(idleArtwork.TryGet(entry.GetProperty("character").GetString()!, entry.GetProperty("outfit").GetString()!, out var idleAtlas) && idleAtlas != null,
                    "Every walking rig has its actual idle atlas for transition-size validation.");
                var bimble = entry.GetProperty("character").GetString() == "Bimble";
                Check(accepted!.SupportsIdle == bimble, "Only rebuilt Bimble uses the shared automatic idle/walk renderer.");
                if (bimble)
                {
                    foreach (var mirrored in new[] { false, true })
                    {
                        var settled = accepted.Evaluate(0, mirrored, Vector2.Zero, Vector2.One, walkBlend: 0);
                        Check(settled.NearFoot.Y == .9f && settled.FarFoot.Y == .9f && settled.NearStance && settled.FarStance,
                            "Settled Bimble plants both paws instead of freezing one in mid-swing.");
                        for (var headingIndex = 0; headingIndex < 72; headingIndex++)
                        {
                            var angle = headingIndex * MathF.Tau / 72;
                            var heading = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                            for (var phaseIndex = 0; phaseIndex < 16; phaseIndex++)
                            {
                                var phase = phaseIndex / 16f;
                                var resting = accepted.Evaluate(phase, mirrored, Vector2.Zero, Vector2.One, heading, 0);
                                Check(resting.Quads.SequenceEqual(settled.Quads), "Settled anatomy is independent of the frozen walking phase and travel direction.");
                                var walk = accepted.Evaluate(phase, mirrored, Vector2.Zero, Vector2.One, heading);
                                var explicitWalk = accepted.Evaluate(phase, mirrored, Vector2.Zero, Vector2.One, heading, 1);
                                Check(walk.Quads.SequenceEqual(explicitWalk.Quads) && walk.NearFoot == explicitWalk.NearFoot && walk.FarFoot == explicitWalk.FarFoot,
                                    "Full walking remains the unchanged distance-driven pose.");
                                var previous = resting;
                                for (var step = 1; step <= 8; step++)
                                {
                                    var blended = accepted.Evaluate(phase, mirrored, Vector2.Zero, Vector2.One, heading, step / 8f);
                                    Check(blended.Bounds.Minimum.X >= 0 && blended.Bounds.Minimum.Y >= 0
                                        && blended.Bounds.Maximum.X <= 1 && blended.Bounds.Maximum.Y <= 1,
                                        "Every settle/start pose remains inside its cell at all headings.");
                                    for (var index = 0; index < blended.Quads.Length; index++)
                                    {
                                        var q = blended.Quads[index]; var p = previous.Quads[index]; var source = walk.Quads[index];
                                        Check(q.Part == source.Part && q.UvA == source.UvA && q.UvB == source.UvB && q.UvC == source.UvC && q.UvD == source.UvD,
                                            "Idle and walking use identical body and limb source pixels throughout the transition.");
                                        Check(Vector2.Distance(q.A, p.A) < .09f && Vector2.Distance(q.C, p.C) < .09f,
                                            "A bounded mesh blend avoids discontinuous body swaps on hover stop/start.");
                                    }
                                    previous = blended;
                                }
                            }
                        }
                    }
                    foreach (var fps in new[] { 60, 144 })
                    {
                        var blend = 1f;
                        for (var frame = 0; frame < Math.Ceiling(.2f * fps); frame++)
                            blend = LunarWalkRig.NextWalkBlend(blend, false, true, 1f / fps);
                        Check(blend == 0, "A hover stop settles within 200ms at both60/144Hz.");
                        for (var frame = 0; frame < Math.Ceiling(.2f * fps); frame++)
                            blend = LunarWalkRig.NextWalkBlend(blend, true, true, 1f / fps);
                        Check(blend == 1, "Movement and manual walk preview return to full gait within200ms.");
                    }
                    Check(LunarWalkRig.NextWalkBlend(.7f, true, false, .016f) == 0, "Disabling animation selects a stable grounded idle immediately.");
                }
                var body = entry.GetProperty("bodyRect");
                var crop = entry.GetProperty("parts")[0].GetProperty("rect");
                var nearSleeve = entry.GetProperty("nearShoulderUv"); var farSleeve = entry.GetProperty("farShoulderUv");
                Check(nearSleeve[1].GetSingle() >= .7f && farSleeve[1].GetSingle() >= .7f
                    && nearSleeve[0].GetSingle() - farSleeve[0].GetSingle() > .45f,
                    "The reviewed art's two sleeve anchors sit below the clasp on opposite sides, not under the chin.");
                // Captured sole positions from the approved gait before the anatomy
                // correction. Joint and paw-fit changes must not alter this motion.
                var approvedFeet = new[]
                {
                    (Phase: 0f, Near: new Vector2(.66400003f, .9f), Far: new Vector2(.47f, .9f)),
                    (Phase: .25f, Near: new Vector2(.57900006f, .9f), Far: new Vector2(.4925781f, .85781246f)),
                    (Phase: .5f, Near: new Vector2(.49400005f, .9f), Far: new Vector2(.64f, .9f)),
                    (Phase: .75f, Near: new Vector2(.51657814f, .85781246f), Far: new Vector2(.555f, .9f)),
                };
                foreach (var expected in approvedFeet)
                {
                    var actual = accepted!.Evaluate(expected.Phase, false, Vector2.Zero, Vector2.One);
                    Check(Near(actual.NearFoot, expected.Near, .000001f) && Near(actual.FarFoot, expected.Far, .000001f),
                        "The corrected anatomy preserves the approved sole trajectories and quarter-cycle timing.");
                }
                Check(Math.Abs(body[2].GetSingle() / body[3].GetSingle() -
                    (crop[2].GetSingle() - crop[0].GetSingle()) / (crop[3].GetSingle() - crop[1].GetSingle())) < .0001f,
                    "The walking body preserves source aspect rather than stretching the face.");
                foreach (var mirrored in new[] { false, true })
                foreach (var cellWidth in new[] { 48f, 96f, 192f })
                {
                    var origin = new Vector2(200, 100); var cell = new Vector2(cellWidth);
                    var initial = accepted!.Evaluate(0, mirrored, origin, cell);
                    var idleBounds = idleAtlas!.HitBounds(8, mirrored, Vector2.Zero, Vector2.One);
                    for (var sample = 0; sample < 128; sample++)
                    {
                        var phase = sample / 128f;
                        var placement = origin + new Vector2((mirrored ? -1 : 1) * phase * accepted.CycleDistance * cellWidth, 0);
                        var actual = accepted.Evaluate(phase, mirrored, placement, cell);
                        Check(Vector2.Distance(actual.NearHip, actual.FarHip) / cellWidth >= .09f,
                            "The production hip joints remain distinct under the two sides of the shorts.");
                        foreach (var (partIndex, anchorName) in new[] { (1, "nearShoulderUv"), (2, "farShoulderUv") })
                        {
                            var anchor = entry.GetProperty(anchorName);
                            var u = anchor[0].GetSingle(); var v = anchor[1].GetSingle();
                            var band = Math.Min(5, (int)(v * 6));
                            var bodyQuad = actual.Quads.Single(q => q.Part == 0 && q.Section == band);
                            var expectedShoulder = bodyQuad.A + (bodyQuad.B - bodyQuad.A) * u + (bodyQuad.D - bodyQuad.A) * (v * 6 - band);
                            var armQuad = actual.Quads.Single(q => q.Part == partIndex);
                            var pivot = entry.GetProperty("parts")[partIndex].GetProperty("pivot");
                            var actualShoulder = armQuad.A + (armQuad.B - armQuad.A) * pivot[0].GetSingle()
                                + (armQuad.D - armQuad.A) * pivot[1].GetSingle();
                            Check(Vector2.Distance(expectedShoulder, actualShoulder) < .002f,
                                "Each arm follows its own painted sleeve through body rotation, scale and mirroring.");
                        }
                        foreach (var partIndex in new[] { 7, 8 })
                        {
                            var paw = actual.Quads.Single(q => q.Part == partIndex);
                            var pawCrop = entry.GetProperty("parts")[partIndex].GetProperty("rect");
                            var aspect = (pawCrop[2].GetSingle() - pawCrop[0].GetSingle()) / (pawCrop[3].GetSingle() - pawCrop[1].GetSingle());
                            Check(Math.Abs(Vector2.Distance(paw.A, paw.B) / Vector2.Distance(paw.A, paw.D) - aspect) < .0001f,
                                "Paws keep their source aspect throughout the walk, without horizontal flattening.");
                        }
                        if (actual.NearStance) Check(Vector2.Distance(actual.NearFoot, initial.NearFoot) < .002f,
                            "A production stance foot remains stationary at actual output sizes.");
                        var bounds = actual.Bounds;
                        var plantedSole = actual.NearStance ? actual.NearFoot : actual.FarFoot;
                        Check(Math.Abs((plantedSole.Y - bounds.Minimum.Y) / cellWidth - (idleBounds.Maximum.Y - idleBounds.Minimum.Y)) < .04f,
                            "Switching from idle frame eight to any walk phase preserves top-to-ground height within four percent of the logical cell.");
                        Check(Math.Abs((plantedSole.Y - placement.Y) / cellWidth - idleBounds.Maximum.Y) < .004f,
                            "Walking and idle artwork share the same grounded baseline.");
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
