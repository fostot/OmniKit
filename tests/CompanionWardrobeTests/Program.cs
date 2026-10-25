using System.Numerics;
using System.Text.Json.Nodes;
using OmniKit.Ui;

var checks = PaddingChecks.Run();
if (args.Contains("--parser-only")) { Console.WriteLine($"Wardrobe padding parser: {checks} checks passed."); return; }
var assets = Path.GetFullPath(args.FirstOrDefault(arg => !arg.StartsWith("--",StringComparison.Ordinal)) ?? "assets");
void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
var wardrobe = new CompanionWardrobeLibrary(assets);
string[] expected = ["Nib", "Echo", "Fizz", "Glim", "Rune"];
string[] allAvatars = ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism", .. expected];
SeasonalCompanionTheme[] costumes = [SeasonalCompanionTheme.AllSaintsWake, SeasonalCompanionTheme.Starlight,
    SeasonalCompanionTheme.MakeItRain, SeasonalCompanionTheme.MoonfireFaire];
Check(wardrobe.IsAvailable && wardrobe.Looks.Count == 65 && wardrobe.Looks.Where(look => look.Choice == "Default")
    .Select(look => look.Avatar).SequenceEqual(allAvatars), "The complete wardrobe includes thirteen bare bodies and 52 costumes.");
Check(wardrobe.Files.SequenceEqual(allAvatars.Select(avatar => avatar.ToLowerInvariant() + "-wardrobe.png"))
    && wardrobe.BlinkProfiles.Count == 65 && wardrobe.BlinkProfiles.Sum(profile => profile.Eyes.Count) == 125,
    "Thirteen validated PNGs supply all exact normal/closed-eye identities.");
foreach (var avatar in allAvatars)
foreach (var costume in costumes)
    Check(wardrobe.FindCostume(avatar, costume)?.Choice == "Costume" + costume, "Every companion has all four separately named painted costumes.");
foreach (var look in wardrobe.Looks)
{
    Check(look.Frames.Count == 8 && look.Ringless == (look.Avatar == "Orbit")
        && (look.Choice != "Default" || ReferenceEquals(wardrobe.FindBare(look.Avatar), look)),
        "Every wardrobe look retains eight complete painted poses and the existing ringless Orbit contract.");
    var blink = wardrobe.FindBlink(look.Normal.File, look.Normal.Bounds);
    Check(blink != null && blink.Avatar == look.Avatar && blink.Eyes.Count == (look.Avatar == "Wink" ? 1 : 2)
        && blink.ClosedBounds == look[CompanionVariantPose.ClosedBlink].Bounds,
        "The measured two-eye profile belongs to the exact normal and closed crop pair.");
    Check(wardrobe.FindBlink("../" + look.Normal.File, look.Normal.Bounds) == null &&
        wardrobe.FindBlink(look.Normal.File, look.Normal.Bounds + Vector4.One) == null,
        "Different source filenames or crop identities cannot reuse an eye profile.");
    foreach (var pose in look.Frames)
    foreach (var edge in new[] { 96f, 192f })
    foreach (var mirrored in new[] { false, true })
    {
        var frame = pose.GetFrame(Vector2.Zero, new Vector2(edge), mirrored);
        Check(pose.SourceScale == look.ScaleReference && pose.AccessoryHeadWidth == look.Normal.AccessoryHeadWidth,
            "All poses retain one common source scale and reviewed head width.");
        var padding = edge * CompanionWardrobeLibrary.VisualPaddingFraction;
        Check(frame.Position.X >= -padding-.001f && frame.Position.Y >= -padding-.001f && frame.Position.X + frame.Size.X <= edge+padding+.001f
            && frame.Position.Y + frame.Size.Y <= edge+padding+.001f, "Every full painted crop fits the reserved wardrobe bounds at 96/192 and both facings.");
        var bodyA=pose.Map(new(pose.Body.X,pose.Body.Y),Vector2.Zero,new(edge),mirrored);
        var bodyB=pose.Map(new(pose.Body.Z,pose.Body.W),Vector2.Zero,new(edge),mirrored);
        var bodyMin=Vector2.Min(bodyA,bodyB);var bodyMax=Vector2.Max(bodyA,bodyB);
        Check(bodyMin.X>=-.001f&&bodyMin.Y>=-.001f&&bodyMax.X<=edge+.001f&&bodyMax.Y<=edge+.001f,
            "Measured body mass remains inside its original logical cell while the hat may extend above it.");
        Check(Math.Abs(frame.Size.X / frame.Size.Y - (pose.Bounds.Z - pose.Bounds.X) / (pose.Bounds.W - pose.Bounds.Y)) < .0001f,
            "Whole-body poses preserve source aspect without per-pose stretch.");
        Check(Vector2.Distance(pose.Map(pose.Anchor, Vector2.Zero, new Vector2(edge), mirrored), new Vector2(edge * .5f, edge * .9f)) < .001f,
            "Each measured source anchor maps to the same stable baseline.");
    }
}
Check(wardrobe.FindDefault("Moonblob") == null && wardrobe.FindDefault("Unknown") == null
    && expected.All(avatar => ReferenceEquals(wardrobe.FindDefault(avatar), wardrobe.FindBare(avatar)))
    && allAvatars.All(avatar => wardrobe.FindBare(avatar) != null) && wardrobe.FindBare("Unknown") == null
    && wardrobe.FindCostume("Nib", SeasonalCompanionTheme.TheRising) == null,
    "The wardrobe does not replace original defaults or invent unsupported costumes.");
var original = new CompanionAnimationLibrary(assets); var variants = new CompanionVariantLibrary(assets); var holiday = new CompanionHolidayLibrary(assets);
Check(original.Count == 8 && variants.Looks.Count == 32 && holiday.Looks.Count == 72, "Original, everyday and holiday artwork packs remain independently available.");

var fixture = Directory.CreateDirectory(Path.Combine(Path.GetFullPath(".local/companion-wardrobe-tests"), Guid.NewGuid().ToString("N")));
var metadataPath = Path.Combine(fixture.FullName, CompanionWardrobeLibrary.MetadataFile);
foreach (var file in wardrobe.Files) File.Copy(Path.Combine(assets, file), Path.Combine(fixture.FullName, file));
var copiedPng = Path.Combine(fixture.FullName, wardrobe.Files[0]);
Check(!new CompanionWardrobeLibrary(fixture.FullName).IsAvailable, "A missing optional metadata file leaves the wardrobe unavailable.");
var document = JsonNode.Parse(File.ReadAllText(Path.Combine(assets, CompanionWardrobeLibrary.MetadataFile)))!;
var mutations = new (string Name, Action<JsonNode> Mutate)[]
{
    ("future version", doc => doc["Version"] = 2),
    ("missing character", doc => doc["Looks"]!.AsArray().RemoveAt(4)),
    ("duplicate character/costume", doc => { doc["Looks"]![1]!["Avatar"] = doc["Looks"]![0]!["Avatar"]!.GetValue<string>();
        doc["Looks"]![1]!["Costume"] = doc["Looks"]![0]!["Costume"]!.GetValue<string>(); }),
    ("unknown character", doc => doc["Looks"]![4]!["Avatar"] = "Unknown"),
    ("unsupported costume", doc => doc["Looks"]![0]!["Costume"] = "TheRising"),
    ("numeric costume", doc => doc["Looks"]![0]!["Costume"] = "7"),
    ("noncanonical costume", doc => doc["Looks"]![0]!["Costume"] = "starlight"),
    ("bad source hash", doc => doc["Sheets"]![0]!["SHA256"] = new string('0',64)),
    ("mismatched dimensions", doc => doc["Sheets"]![0]!["Width"] = 1),
    ("unsafe source name", doc => doc["Sheets"]![0]!["File"] = "../nib-wardrobe.png"),
    ("missing pose", doc => doc["Looks"]![0]!["Frames"]!.AsArray().RemoveAt(7)),
    ("wrong pose order", doc => doc["Looks"]![0]!["Frames"]![0]!["State"] = "Wake"),
    ("negative source scale", doc => doc["Looks"]![0]!["ScaleReference"] = -1),
    ("shifted grip outside crop", doc => doc["Looks"]![0]!["Frames"]![0]!["Grip"]![0] = 9000),
    ("invalid head width", doc => doc["Looks"]![0]!["Frames"]![0]!["AccessoryHeadWidth"] = 0),
    ("inconsistent head width", doc => doc["Looks"]![0]!["Frames"]![1]!["AccessoryHeadWidth"] = 4096),
    ("unknown frame image", doc => doc["Looks"]![0]!["Frames"]![0]!["File"] = "other.png"),
    ("eye outside normal crop", doc => doc["Looks"]![0]!["Eyes"]![0]!["Ellipse"]![0] = -10),
    ("skin patch outside closed crop", doc => doc["Looks"]![0]!["Eyes"]![0]!["Sample"]![0] = -10),
    ("missing measured eye", doc => doc["Looks"]![0]!["Eyes"]!.AsArray().RemoveAt(0)),
};
foreach (var mutation in mutations)
{
    var changed = document.DeepClone(); mutation.Mutate(changed); File.WriteAllText(metadataPath, changed.ToJsonString());
    var invalid = new CompanionWardrobeLibrary(fixture.FullName);
    Check(!invalid.IsAvailable && invalid.Looks.Count == 0 && invalid.BlinkProfiles.Count == 0 && invalid.Files.Count == 0,
        mutation.Name + ": invalid input never publishes a partial character or blink set.");
}
File.WriteAllText(metadataPath, "{");
Check(!new CompanionWardrobeLibrary(fixture.FullName).IsAvailable, "Malformed metadata fails closed.");
File.WriteAllText(metadataPath, document.ToJsonString());
File.Move(copiedPng, copiedPng + ".fixture-away");
Check(!new CompanionWardrobeLibrary(fixture.FullName).IsAvailable, "Missing source art never substitutes invented placeholder bodies.");
File.Move(copiedPng + ".fixture-away", copiedPng);
using (var changedBytes = File.Open(copiedPng, FileMode.Open, FileAccess.ReadWrite))
{ changedBytes.Position = changedBytes.Length - 1; var last = changedBytes.ReadByte(); changedBytes.Position--; changedBytes.WriteByte((byte)(last ^ 1)); }
Check(!new CompanionWardrobeLibrary(fixture.FullName).IsAvailable, "An altered PNG is rejected even when its header and dimensions still match.");
Check(original.Count == 8 && variants.IsAvailable && holiday.IsAvailable && wardrobe.IsAvailable,
    "Failed optional loads do not mutate independently loaded packs or the already validated set.");
if (args.Contains("--native")) checks += NativeChecks.Run(assets, wardrobe,
    args.FirstOrDefault(arg => arg.StartsWith("--native-avatars=", StringComparison.Ordinal))?.Split('=')[1].Split(','));
Console.WriteLine($"Companion wardrobe: {checks} checks passed. 65 painted looks, 520 poses, 125 measured eyes; synthetic invalid fixtures only.");
