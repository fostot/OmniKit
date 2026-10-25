using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmniKit;
using OmniKit.Ui;

internal static class HomePoseChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var factor = CompanionHomePoseScale.Correction(195, 228, 218, 213);
        check(Math.Abs(factor * 218 / 213 - 195f / 228) < .000001f, "One common rear factor matches the actual front width.");
        foreach (var invalid in new[] { 0f, -1, float.NaN, float.PositiveInfinity, .01f, 10000f })
            check(CompanionHomePoseScale.Correction(invalid, 228, 218, 213) == 0, "Invalid or unbounded width corrections fail closed.");
        var directory = Path.Combine(Path.GetTempPath(), "OmniKitHomePoseTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var originals = Configuration.AvatarNames.Take(8).ToArray();
            var files = originals.SelectMany(avatar => new[] { $"companion-homeposes-{avatar.ToLowerInvariant()}-a.png",
                $"companion-homeposes-{avatar.ToLowerInvariant()}-b.png" }).Append("companion-homeposes-newcomers.png").ToArray();
            foreach (var file in files)
            {
                // Header-only fixtures exercise metadata validation without loading textures.
                var header = new byte[24]; new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(header, 0);
                "IHDR"u8.CopyTo(header.AsSpan(12));
                BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16), 1024); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(20), 1024);
                File.WriteAllBytes(Path.Combine(directory, file), header);
            }
            var looks = new List<object>();
            foreach (var avatar in Configuration.AvatarNames)
            for (var row = 0; row < (originals.Contains(avatar) ? 14 : 1); row++)
            {
                var file = originals.Contains(avatar) ? $"companion-homeposes-{avatar.ToLowerInvariant()}-a.png" : files[^1];
                looks.Add(new { Avatar = avatar, Id = row == 0 ? "Default" : "Identity" + row, ScaleReference = 120,
                    Frames = Enum.GetValues<CompanionHomePose>().Select((pose, index) => new { State = pose.ToString(), File = file,
                        Bounds = new[] { index * 100f, 0, index * 100f + 80 - index * 4, 100 },
                        BodyBounds = new[] { index * 100f + 4, 4, index * 100f + 76 - index * 4, 96 },
                        Anchor = new[] { index * 100f + 40, 96 }, SourceUnitScale = 1 }).ToArray() });
            }
            var valid = JsonSerializer.Serialize(new { Version = 1, Sheets = files.Select(file => new { File = file, Width = 1024, Height = 1024 }), Looks = looks });
            var path = Path.Combine(directory, CompanionHomePoseLibrary.MetadataFile);
            File.WriteAllText(path, valid);
            var library = new CompanionHomePoseLibrary(directory);
            check(library.IsAvailable, "A complete17atlas117identity rear manifest loads atomically.");
            check(library.Find("Moonblob", "Missing", CompanionHomePose.TurnBack) == null,
                "Missing material identity never substitutes a different rear body.");
            var first = library.Find("Moonblob", "Identity1", CompanionHomePose.TurnBack)!;
            var second = library.Find("Moonblob", "Identity1", CompanionHomePose.RearCrouch)!;
            var support = new Vector2(500, 400); var sprite = new Vector2(192);
            foreach (var mirrored in new[] { false, true })
            {
                var placed = first.Place(support, sprite, .45f, mirrored);
                var offset = (first.Anchor - new Vector2(first.Bounds.X, first.Bounds.Y)) * (sprite.Y * .45f / first.ScaleReference);
                if (mirrored) offset.X = placed.Size.X - offset.X;
                check(Vector2.Distance(placed.Position + offset, support) < .0001f, "Rear art retains its authored support in both facings.");
                check(second.Place(support, sprite, .45f, mirrored).Size.X < placed.Size.X,
                    "Common scale preserves authored crouch width instead of per-pose fitting.");
            }
            foreach (var corruption in new Action<JsonNode>[]
            {
                doc => doc["Looks"]![116]!["Frames"]![3]!["Bounds"]![2] = 99999,
                doc => doc["Looks"]![1]!["Id"] = "Default",
                doc => doc["Sheets"]![0]!["File"] = "../outside.png",
                doc => doc["Looks"]![0]!["Frames"]![0]!["File"] = "companion-homeposes-wink-a.png",
                doc => doc["Looks"]![0]!["Frames"]![0]!["Anchor"]![0] = -1,
                doc => doc["Looks"]![0]!["Frames"]![0]!["State"] = "RearClimbB"
                ,doc => doc["Looks"]![0]!["ScaleReference"] = 1
                ,doc => doc["Looks"]![0]!["Frames"]![0]!["Anchor"]![0] = 4
                ,doc => doc["Looks"]![0]!["Frames"]![0]!["Anchor"]![1] = 4
            })
            {
                var document = JsonNode.Parse(valid)!; corruption(document); File.WriteAllText(path, document.ToJsonString());
                var invalid = new CompanionHomePoseLibrary(directory);
                check(!invalid.IsAvailable && invalid.Find("Moonblob", "Default", CompanionHomePose.TurnBack) == null,
                    "Invalid rear metadata fails as a unit without partial identities or unsafe file paths.");
            }
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
