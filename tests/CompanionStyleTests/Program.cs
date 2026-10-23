using System.Numerics;
using System.Text.Json.Nodes;
using OmniKit.Ui;

var root = Path.GetFullPath(args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)) ?? ".");
var directory = Path.Combine(root, "assets");
var library = new CompanionHomeStyleLibrary(directory);
var checks = 0;
var homeIds = CompanionHomeStyleLibrary.HomeIds.Where(x => x != "Random").ToArray();
var handIds = CompanionHomeStyleLibrary.HandIds.Where(x => x != "Random").ToArray();
Check(homeIds.Length == 13 && handIds.Length == 5, "Every approved style plus Default loads.");
Check(library.GetHome("unknown").Id == "Default" && library.GetHand("unknown").Id == "Default", "Unknown choices preserve Default.");
foreach (var id in homeIds)
foreach (var edge in new[] { 48f, 96f, 192f })
foreach (var mirrored in new[] { false, true })
{
    var min = new Vector2(90, 65); var max = min + new Vector2(edge);
    var frame = library.GetHome(id).Place(min, max, mirrored);
    Check(frame.Minimum.X >= min.X && frame.Minimum.Y >= min.Y && frame.Maximum.X <= max.X + .001f && frame.Maximum.Y <= max.Y + .001f,
        "Aspect fit retains its available bounds.");
    Check(In(frame.Bed, frame.OpeningMinimum, frame.OpeningMaximum), "Bed sits inside the doorway bounds.");
    var twin = library.GetHome(id).Place(min, max, !mirrored);
    Check(Near(frame.Bed.X + twin.Bed.X, frame.Minimum.X + frame.Maximum.X) && Near(frame.Bed.Y, twin.Bed.Y), "Mirror preserves bed height.");
    Check(Near(frame.Chimney.X + twin.Chimney.X, frame.Minimum.X + frame.Maximum.X), "Mirror preserves chimney placement.");
    Check(Near(frame.OutsidePosition(new(64)).X + 32, frame.Threshold.X), "Approach aligns with the measured threshold.");
    Check(NearVector(frame.BedPosition(new(64)) + new Vector2(32, 57.6f), frame.Bed), "Accepted nap sprite pivot is retained.");
    var area = 0f;
    for (var i = 0; i < frame.WallCount; i++)
    {
        var quad = frame.Wall(i);
        Check(In(quad.Minimum, frame.Minimum, frame.Maximum) && In(quad.Maximum, frame.Minimum, frame.Maximum), "Every wall stays inside the home.");
        Check(new[] { quad.UvA, quad.UvB, quad.UvC, quad.UvD }.All(p => In(p, Vector2.Zero, Vector2.One)), "Wall UVs remain in source atlas.");
        area += Area([quad.A, quad.B, quad.C, quad.D]);
    }
    var door = Enumerable.Range(0, frame.DoorVertexCount).Select(frame.DoorVertex).ToArray();
    Check(Math.Abs(area + Area(door) - frame.Size.X * frame.Size.Y) < edge * edge * .0001f, "Wall pieces cover exactly the area outside the opening.");
}
var original = library.GetHome("Default").Place(Vector2.Zero, new(100), false);
Check(original.Minimum == Vector2.Zero && original.Maximum == new Vector2(100), "Default keeps the accepted square draw.");
Check(NearVector(original.Bed, new(66.35f, 87.2f)) && NearVector(original.Chimney, new(24, 17)), "Default bed and chimney are unchanged.");
Check(NearVector(original.OpeningMinimum, new(46, 55)) && NearVector(original.OpeningMaximum, new(86.7f, 89)), "Default opening is unchanged.");
Check(original.Image.UvA == new Vector2(.08f, .035f) && original.Image.UvC == new Vector2(.925f, .9f), "Default UV crop is unchanged.");

foreach (var id in handIds)
foreach (var pose in Enum.GetValues<CompanionHandPose>())
foreach (var edge in new[] { 48f, 96f, 192f })
{
    var hand = library.GetHand(id); var data = hand.Pose(pose); var contact = new Vector2(200, 160);
    var quad = hand.Frame(pose, contact, edge);
    Check(NearVector(quad.A + data.GripInCropPixels * (edge / data.SourceUnitsPerLogicalCell), contact), "Hand contact remains invariant across poses and sizes.");
    Check(Near((quad.C.X - quad.A.X) / (quad.C.Y - quad.A.Y), data.CropSize.X / data.CropSize.Y), "Hand source aspect is preserved.");
    Check(data.SourceUnitsPerLogicalCell == hand.Pose(CompanionHandPose.Ready).SourceUnitsPerLogicalCell, "No pose rescales a narrow closed hand.");
    if (id != "Default")
    {
        var cell = contact - new Vector2(.5f, .77f) * edge;
        Check(In(quad.Minimum, cell, cell + new Vector2(edge)) && In(quad.Maximum, cell, cell + new Vector2(edge)), "New hand fits its common logical cell.");
    }
}
var choices = new CompanionStyleSelection(library, 42);
var selected = choices.ResolveHome("Random", false, null);
for (var i = 0; i < 40; i++) Check(ReferenceEquals(selected, choices.ResolveHome("Random", false, null)), "Random home is stable across frames.");
var seen = new HashSet<string>();
for (var i = 0; i < 40; i++) seen.Add(choices.ResolveHome("Random", false, "theme-" + i).Id);
Check(seen.Count > 1, "Theme changes permit a new random home.");
Check(choices.ResolveHome("CrystalPod", false, "Starlight").Id == "CrystalPod", "Disabled holiday override honors the saved home choice.");
Check(choices.ResolveHome("CrystalPod", true, "Starlight").Id == "Starlight", "Enabled holiday override selects the resolved seasonal home.");
Check(choices.ResolveHome("CloudCushion", true, "AllSaintsWake", canChange: false).Id == "Starlight", "An active nap freezes its resolved home and geometry.");
Check(choices.ResolveHome("CloudCushion", true, "AllSaintsWake", canChange: true).Id == "AllSaintsWake", "Pending seasonal change applies after the visitor leaves.");
Check(choices.ResolveHome("CloudCushion", true, "unknown").Id == "CloudCushion", "An unknown seasonal state never guesses a holiday home.");
Check(choices.ResolveHome("CloudCushion", true, null).Id == "CloudCushion", "No event restores the saved home choice.");
var held = choices.BeginPickup("Random");
for (var i = 0; i < 40; i++) Check(ReferenceEquals(held, choices.CurrentHand) && ReferenceEquals(held, choices.ReadyHand("Random")), "Pickup hand stays fixed through hold/release.");
seen.Clear(); for (var i = 0; i < 40; i++) seen.Add(choices.BeginPickup("Random").Id);
Check(seen.Count > 1 && choices.BeginPickup("MagicalPaw").Id == "MagicalPaw", "New pickups choose randomly or honor a specific hand.");

var fixture = Path.Combine(root, ".local", "visualharness", "companion-styles", "fixtures");
Directory.CreateDirectory(fixture);
foreach (var file in new[] { "homes-standard.png", "homes-holiday.png", "hands-styles.png" }) File.Copy(Path.Combine(directory, file), Path.Combine(fixture, file), true);
var originalJson = File.ReadAllText(Path.Combine(directory, "companion-styles.frames.json"));
Reject(root => root["schema"] = 99, "Unknown metadata schema.");
Reject(root => root["houses"]![0]!["rect"]![2] = 99999, "Crop overflow.");
Reject(root => root["assets"]![0]!["file"] = "../private.png", "Path traversal.");
Reject(root => root["assets"]![0]!["sha256"] = new string('0', 64), "Unreviewed asset bytes.");
Reject(root => root["houses"]![1]!["name"] = "CrystalPod", "Duplicate house.");
Reject(root => root["houses"]![0]!["bedAnchor"]![0] = -1, "Invalid anchor.");
Reject(root => root["houses"]![0]!["bedAnchor"]![0] = .01, "Bed outside doorway.");
Reject(root => root["houses"]![0]!["threshold"]![1] = .1, "Threshold above bed.");
Reject(root => root["hands"]![1]!["pose"] = "Ready", "Duplicate pose.");
Reject(root => root["hands"]![2]!["sourceUnitsPerLogicalCell"] = 100, "Pose-dependent scale.");
Reject(root => root["hands"]![0]!["gripSourcePixels"]![0] = 99999, "Out-of-crop hand contact.");
Reject(root => root["houses"] = null, "Null metadata collection.");
if (args.Contains("--render")) StyleRenderChecks.Run(root, library, Check);
Console.WriteLine($"Home/hand styles: {checks} geometry, default-parity, selection and metadata checks passed" + (args.Contains("--render") ? "; 10 actual native renderer sheets saved." : "."));

void Reject(Action<JsonNode> mutate, string reason)
{
    var json = JsonNode.Parse(originalJson)!; mutate(json); File.WriteAllText(Path.Combine(fixture, "companion-styles.frames.json"), json.ToJsonString());
    try { _ = new CompanionHomeStyleLibrary(fixture); throw new Exception("Metadata accepted: " + reason); }
    catch (InvalidDataException) { checks++; }
}
void Check(bool passed, string reason) { if (!passed) throw new InvalidOperationException(reason); checks++; }
static bool Near(float a, float b) => Math.Abs(a - b) < .001f;
static bool NearVector(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .001f;
static bool In(Vector2 p, Vector2 min, Vector2 max) => p.X >= min.X - .001f && p.Y >= min.Y - .001f && p.X <= max.X + .001f && p.Y <= max.Y + .001f;
static float Area(Vector2[] points) { var sum = 0f; for (var i = 0; i < points.Length; i++) { var a = points[i]; var b = points[(i + 1) % points.Length]; sum += a.X * b.Y - b.X * a.Y; } return Math.Abs(sum * .5f); }
