using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

var output = Directory.CreateDirectory(Path.GetFullPath(".local/companion-holiday-tests"));
var fixture = Directory.CreateDirectory(Path.Combine(output.FullName, "synthetic"));
string[] avatars = ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism"];
var count = 0;
void Check(bool value, string message) { count++; if (!value) throw new InvalidOperationException(message); }
string FileFor(string avatar) => avatar.ToLowerInvariant() + "-holiday-bodies.png";
object Frame(string avatar, CompanionVariantPose pose) => new
{
    File = FileFor(avatar), State = pose.ToString(), Bounds = new[] { 50, 80, 150, 180 },
    BodyBounds = new[] { 54, 84, 146, 177 }, Anchor = new[] { 100, 180 }, Grip = new[] { 100, 96 },
    AccessoryAnchor = new[] { 100, 90 }, AccessoryHeadWidth = 80, SourceUnitScale = 1,
};
var valid = JsonSerializer.Serialize(new
{
    Version = 1,
    Sheets = avatars.Select(a => new { File = FileFor(a), Width = 512, Height = 512 }),
    Looks = avatars.SelectMany(a => Enum.GetValues<SeasonalCompanionTheme>().Select(t => new
    {
        Avatar = a, Theme = t.ToString(), Id = a + "-" + t, Label = t.ToString(), ScaleReference = 160,
        Ringless = a == "Orbit", EyeEllipse = a == "Wink" ? new[] { 100, 120, 24, 24 } : null,
        Frames = Enum.GetValues<CompanionVariantPose>().Select(p => Frame(a, p)),
    })),
});
foreach (var avatar in avatars) { using var image = new Bitmap(512, 512); image.Save(Path.Combine(fixture.FullName, FileFor(avatar)), ImageFormat.Png); }
var metadata = Path.Combine(fixture.FullName, CompanionHolidayLibrary.MetadataFile);
File.WriteAllText(metadata, valid);
var library = new CompanionHolidayLibrary(fixture.FullName);
Check(library.IsAvailable && library.Looks.Count == 72 && library.Files.Count == 8, "All 72 synthetic holiday looks load atomically.");
foreach (var avatar in avatars) foreach (var theme in Enum.GetValues<SeasonalCompanionTheme>())
{
    var look = library.FindHoliday(avatar, theme);
    Check(look != null && look.Choice == "Holiday" + theme && look.Avatar == avatar, "Canonical lookup has a distinct holiday choice.");
}
Check(library.FindHoliday("unknown", SeasonalCompanionTheme.AllSaintsWake) == null
    && library.FindHoliday("Wink", (SeasonalCompanionTheme)999) == null, "Unknown lookup does not substitute another skin.");
Geometry(library.Looks);

foreach (var mode in new[] { "truncated", "future", "sheet-count", "look-count", "duplicate", "numeric-theme", "unknown-theme", "cross-avatar",
    "path", "png-size", "null-frame", "crop", "anchor", "nan", "scale", "per-pose-scale", "head-width", "head-drift", "eye", "missing-eye", "ring", "label" })
{
    var bad = JsonNode.Parse(valid)!; var look = bad["Looks"]![0]!; var frame = look["Frames"]![0]!;
    switch (mode)
    {
        case "future": bad["Version"] = 2; break;
        case "sheet-count": bad["Sheets"]!.AsArray().RemoveAt(7); break;
        case "look-count": bad["Looks"]!.AsArray().RemoveAt(71); break;
        case "duplicate": bad["Looks"]![71]!["Theme"] = bad["Looks"]![70]!["Theme"]!.GetValue<string>(); break;
        case "numeric-theme": look["Theme"] = "0"; break;
        case "unknown-theme": look["Theme"] = "UnknownHoliday"; break;
        case "cross-avatar": frame["File"] = FileFor("Puff"); break;
        case "path": bad["Sheets"]![0]!["File"] = "../outside.png"; break;
        case "png-size": bad["Sheets"]![0]!["Width"] = 400; break;
        case "null-frame": bad["Looks"]![71]!["Frames"]![7] = null; break;
        case "crop": frame["Bounds"]![2] = 90000; break;
        case "anchor": frame["Anchor"]![0] = -1; break;
        case "scale": look["ScaleReference"] = 0; break;
        case "per-pose-scale": frame["SourceUnitScale"] = 1.1; break;
        case "head-width": frame["AccessoryHeadWidth"] = 0; break;
        case "head-drift": look["Frames"]![1]!["AccessoryHeadWidth"] = 90; break;
        case "eye": bad["Looks"]![9]!["EyeEllipse"]![0] = -1; break;
        case "missing-eye": bad["Looks"]![9]!["EyeEllipse"] = null; break;
        case "ring": bad["Looks"]![45]!["Ringless"] = false; break;
        case "label": look["Label"] = null; break;
    }
    var text = mode == "truncated" ? "{" : bad.ToJsonString();
    if (mode == "nan") text = text.Replace("\"ScaleReference\":160", "\"ScaleReference\":NaN", StringComparison.Ordinal);
    File.WriteAllText(metadata, text);
    var rejected = new CompanionHolidayLibrary(fixture.FullName);
    Check(!rejected.IsAvailable && rejected.Looks.Count == 0 && rejected.Files.Count == 0
        && rejected.FindHoliday("Moonblob", SeasonalCompanionTheme.Heavensturn) == null, "Entire invalid optional set stays hidden: " + mode);
}
File.WriteAllText(metadata, valid);
var pngPath = Path.Combine(fixture.FullName, FileFor("Moonblob")); var png = File.ReadAllBytes(pngPath);
File.WriteAllBytes(pngPath, [0, 1, 2]);
Check(!new CompanionHolidayLibrary(fixture.FullName).IsAvailable, "Truncated PNG header rejects the optional set.");
File.WriteAllBytes(pngPath, png);
var absent = Directory.CreateDirectory(Path.Combine(output.FullName, "missing-files"));
File.WriteAllText(Path.Combine(absent.FullName, CompanionHolidayLibrary.MetadataFile), valid);
Check(!new CompanionHolidayLibrary(absent.FullName).IsAvailable, "Missing PNGs reject a complete metadata document.");
var missing = Directory.CreateDirectory(Path.Combine(output.FullName, "missing-metadata"));
Check(!new CompanionHolidayLibrary(missing.FullName).IsAvailable, "Missing optional metadata is a safe fallback.");
Check(new CompanionVariantLibrary(Path.GetFullPath("assets")).IsAvailable, "Existing 32-look library still loads through the shared parser.");

var artIndex = Array.IndexOf(args, "--art-dir");
if (artIndex >= 0)
{
    var directory = Path.GetFullPath(args[artIndex + 1]); var real = new CompanionHolidayLibrary(directory);
    Check(real.IsAvailable, "Requested complete real holiday set loads.");
    using var record = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, CompanionHolidayLibrary.MetadataFile)));
    foreach (var sheet in record.RootElement.GetProperty("Sheets").EnumerateArray())
    {
        var file = sheet.GetProperty("File").GetString()!;
        Check(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, file)))) == sheet.GetProperty("SHA256").GetString(), "Accepted raw PNG hash preserved: " + file);
    }
    Geometry(real.Looks); Render(real, directory);
}
for (var i = 0; i < args.Length; i++) if (args[i] == "--draft") ValidateDraft(Path.GetFullPath(args[++i]));
Console.WriteLine($"Companion holiday loader: {count} checks passed; complete-set fallback, shared source geometry, no game/network/profile access.");

void Geometry(IEnumerable<CompanionVariantLook> looks)
{
    foreach (var look in looks) foreach (var source in look.Frames) foreach (var size in new[] { 96f, 192f }) foreach (var mirrored in new[] { false, true })
    {
        var p = new Vector2(13, 17); var cell = new Vector2(size); var frame = source.GetFrame(p, cell, mirrored);
        Check(frame.Position.X >= p.X - .001f && frame.Position.Y >= p.Y - .001f
            && frame.Position.X + frame.Size.X <= p.X + size + .001f && frame.Position.Y + frame.Size.Y <= p.Y + size + .001f, "Complete crop fits the logical body cell.");
        Check(Math.Abs(frame.Size.X / frame.Size.Y - (source.Bounds.Z - source.Bounds.X) / (source.Bounds.W - source.Bounds.Y)) < .001f, "Source aspect is preserved.");
        Check(Vector2.Distance(source.Map(source.Anchor, p, cell, mirrored), p + cell * new Vector2(.5f, .9f)) < .001f, "Anchor remains at the common baseline.");
        Check(source.Grip.Y >= source.Body.Y && source.Grip.Y <= source.Body.Y + (source.Body.W - source.Body.Y) * .4f,
            "Carry grip stays in the reviewed upper-body region, above the face.");
    }
}
void ValidateDraft(string path)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(path)); var root = doc.RootElement;
    var sheets = root.GetProperty("Sheets").EnumerateArray().ToDictionary(s => s.GetProperty("File").GetString()!, s => new Vector2(s.GetProperty("Width").GetSingle(), s.GetProperty("Height").GetSingle()));
    foreach (var look in root.GetProperty("Looks").EnumerateArray())
    {
        var avatar = look.GetProperty("Avatar").GetString()!; var frames = new List<CompanionVariantFrame>();
        foreach (var value in look.GetProperty("Frames").EnumerateArray())
        {
            var data = value.Deserialize<CompanionVariantLibrary.Frame>()!;
            var frame = CompanionVariantLibrary.ParseFrame(data, look.GetProperty("ScaleReference").GetSingle(), sheets);
            Check(frame != null, avatar + " draft frame passes actual shared parser."); frames.Add(frame!);
            Check(data.SourceUnitScale == 1 && Math.Abs(data.AccessoryHeadWidth - frames[0].AccessoryHeadWidth) < .01f, avatar + " " + look.GetProperty("Theme").GetString() + " " + data.State + ": one source unit and head width per holiday row.");
        }
        var eyeValues = look.TryGetProperty("EyeEllipse", out var eye) && eye.ValueKind != JsonValueKind.Null ? eye.Deserialize<float[]>() : null;
        Check(CompanionVariantLibrary.TryParseEye(avatar, eyeValues, frames[0].Bounds, out var ellipse), "Draft eye belongs to its own normal frame.");
        Geometry([new(avatar, "Draft", "Draft", "Draft", look.GetProperty("ScaleReference").GetSingle(), frames, ellipse, avatar == "Orbit")]);
    }
}
void Render(CompanionHolidayLibrary art, string directory)
{
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO(); unsafe { io.IniFilename = null; io.LogFilename = null; } io.DeltaTime = 1f / 60; io.Fonts.AddFontDefault(); io.Fonts.Build();
        var renderer = new SoftwareRenderer(); var textures = new Dictionary<string, ulong>();
        foreach (var file in art.Files)
        {
            using var bitmap = new Bitmap(Path.Combine(directory, file)); var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = new byte[bitmap.Width * bitmap.Height * 4];
            try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
            finally { bitmap.UnlockBits(bits); }
            for (var n = 0; n < pixels.Length; n += 4) (pixels[n], pixels[n + 2]) = (pixels[n + 2], pixels[n]);
            var id = (ulong)textures.Count + 2; renderer.AddTexture(id, pixels, bitmap.Width, bitmap.Height); textures.Add(file, id);
        }
        foreach (var avatar in avatars) foreach (var size in new[] { 96f, 192f })
        {
            var tile = new Vector2(size + 16, size + 40); io.DisplaySize = new(tile.X * 8, tile.Y * 9); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var row = 0;
            foreach (var look in art.Looks.Where(l => l.Avatar == avatar))
            {
                draw.AddText(new(4, row * tile.Y + 4), 0xFFFFFFFF, look.Choice);
                for (var column = 0; column < 8; column++)
                {
                    var source = look.Frames[column]; var frame = source.GetFrame(new(column * tile.X + 8, row * tile.Y + 22), new(size));
                    var first = draw.VtxBuffer.Size; CompanionVariantRenderer.Draw(draw, new(textures[source.File]), frame);
                    Check(draw.VtxBuffer.Size - first == 4, "One complete holiday body is drawn, without body duplication.");
                }
                row++;
            }
            ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"native-{avatar.ToLowerInvariant()}-{size:0}.png"));
        }
        foreach (var size in new[] { 96f, 192f }) foreach (var mirrored in new[] { false, true })
        {
            var tile = new Vector2(size + 16, size + 30); io.DisplaySize = new(tile.X * 5, tile.Y * 9); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var row = 0;
            foreach (var look in art.Looks.Where(l => l.Avatar == "Wink"))
            {
                draw.AddText(new(4, row * tile.Y + 3), 0xFFFFFFFF, look.Choice);
                for (var column = 0; column < 5; column++)
                {
                    var quad = look.NormalAtlas.GetFloat(0, mirrored, new(column * tile.X + 8, row * tile.Y + 20), new(size));
                    var first = draw.VtxBuffer.Size; draw.AddImageQuad(new(textures[look.Normal.File]), quad.A, quad.B, quad.C, quad.D, quad.UvA, quad.UvB, quad.UvC, quad.UvD);
                    var original = Enumerable.Range(first, 4).Select(i => draw.VtxBuffer[i]).ToArray();
                    CompanionVariantRenderer.DrawWinkBlink(draw, new(textures[look[CompanionVariantPose.ClosedBlink].File]), look, quad, column / 4f, 0xFFFFFFFF);
                    Check(Enumerable.Range(0, 4).All(i => draw.VtxBuffer[first + i].Pos == original[i].Pos && draw.VtxBuffer[first + i].Uv == original[i].Uv), "Holiday Wink lid preserves the single normal body quad.");
                }
                row++;
            }
            ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"native-wink-lids-{size:0}-{(mirrored ? "left" : "right")}.png"));
        }
    }
    finally { ImGui.DestroyContext(context); }
}
