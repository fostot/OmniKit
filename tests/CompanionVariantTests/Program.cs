using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

var assets = Path.GetFullPath("assets");
var output = Directory.CreateDirectory(Path.GetFullPath(".local/companion-variant-tests"));
var library = new CompanionVariantLibrary(assets);
var checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
if (!args.Contains("--blink-only"))
{
Check(library.IsAvailable && library.Looks.Count == 32 && library.OriginalOrbitCore.Count == 16, "Full reviewed variant library loads atomically.");
foreach (var source in library.OriginalOrbitCore)
{
    var frame = source.GetFrame(Vector2.Zero, new(96));
    Check(source.File == "orbit-core.png" && frame.Position.X >= 0 && frame.Position.Y >= 0
        && frame.Position.X + frame.Size.X <= 96 && frame.Position.Y + frame.Size.Y <= 96, "Original ringless Orbit source stays within its logical cell.");
}
var text = File.ReadAllText(Path.Combine(assets, CompanionVariantLibrary.MetadataFile));
var document = JsonNode.Parse(text)!;
foreach (var sheet in document["Sheets"]!.AsArray())
    Check(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(assets, (string)sheet!["File"]!)))) == (string)sheet["SHA256"]!, "Raw source PNG hash preserved.");
foreach (var look in library.Looks)
{
    Check(look.Frames.Count == 8 && look.NormalAtlas.File == look.Normal.File, "Neutral atlas retains its own texture.");
    Check(look.Ringless == (look.Avatar == "Orbit"), "Only Orbit is a ringless planet core.");
    Check((look.EyeEllipse != null) == (look.Avatar == "Wink"), "Eye coordinates belong to each Wink look.");
    foreach (var size in new[] { 96f, 192f }) foreach (var mirror in new[] { false, true }) foreach (var pose in Enum.GetValues<CompanionVariantPose>())
    {
        var source = look[pose]; var cell = new Vector2(size); var position = new Vector2(17, 29);
        var frame = CompanionVariantRenderer.Frame(look, pose, position, cell, mirror);
        Check(frame.Position.X >= position.X - .001f && frame.Position.Y >= position.Y - .001f
            && frame.Position.X + frame.Size.X <= position.X + size + .001f && frame.Position.Y + frame.Size.Y <= position.Y + size + .001f, "Full source crop fits logical cell.");
        Check(Math.Abs(frame.Size.X / frame.Size.Y - (source.Bounds.Z - source.Bounds.X) / (source.Bounds.W - source.Bounds.Y)) < .001f, "Source aspect is preserved.");
        Check(Vector2.Distance(source.Map(source.Anchor, position, cell, mirror), position + cell * new Vector2(.5f, .9f)) < .001f, "Source anchor maps to stable logical baseline.");
        if (pose is CompanionVariantPose.CarrySquirmA or CompanionVariantPose.CarrySquirmB)
        {
            var grip = new Vector2(300, 120);
            var carried = CompanionVariantRenderer.CarriedFrame(look, pose == CompanionVariantPose.CarrySquirmB, grip, cell, mirror);
            var origin = source.GetFrame(Vector2.Zero, cell, mirror);
            Check(Vector2.Distance(source.Map(source.Grip, Vector2.Zero, cell, mirror) + carried.Position - origin.Position, grip) < .001f, "Both squirm poses remain at exactly the requested grip.");
        }
    }
}

var randomCalls = 0;
var selection = new CompanionVariantSelection(library, max => { randomCalls++; return max - 1; });
Check(selection.Resolve("Wink", "Random", false, 30, 0, false) == null && randomCalls == 0, "Locked first resolve makes no random choice.");
var first = selection.Resolve("Wink", "Random", false, 30, 0, true);
Check(first?.Choice == "Look4" && randomCalls == 1, "Initial explicit random selection.");
for (var i = 1; i < 100; i++) selection.Resolve("Wink", "Random", false, 30, i, true);
Check(randomCalls == 1 && ReferenceEquals(first, selection.Current), "Frames and elapsed time do not reroll when rotation is disabled.");
Check(ReferenceEquals(first, selection.Resolve("Wink", "Random", true, 30, 100, false)) && randomCalls == 1, "Due rotation is deferred during carry, nap or hover lock.");
Check(selection.Resolve("Wink", "Random", true, 30, 100, true)?.Choice == "Look3" && randomCalls == 2, "Unlock performs exactly one different choice, not queued catch-up rotations.");
Check(selection.Resolve("Wink", "Look1", true, 30, 101, true)?.Choice == "Look1" && randomCalls == 2, "Specific choice is deterministic.");
Check(selection.Resolve("Wink", "Look1", true, 30, 500, true)?.Choice == "Look1", "A specific look does not silently rotate.");
Check(selection.Resolve("Wink", "Default", true, 30, 501, true) == null, "Default routes to unchanged base artwork.");
selection.Resolve("Wink", "Look2", false, 30, 502, true);
Check(selection.Resolve("Nova", "Look1", false, 30, 503, false)?.Avatar == "Wink" && selection.Avatar == "Wink", "Avatar and look request remain deferred together while locked.");
Check(selection.Resolve("Nova", "Look1", false, 30, 504, true)?.Avatar == "Nova", "Unlock applies pending avatar request.");
selection.Reset(); Check(selection.Current == null && selection.Avatar == "", "Logout/reset clears cached choice.");

var invalidDirectory = Directory.CreateDirectory(Path.Combine(output.FullName, "invalid-fixture"));
foreach (var mode in new[] { "truncated", "future", "path", "duplicate", "crop", "scale", "eye", "null-label", "null-frame" })
{
    var bad = JsonNode.Parse(text)!;
    var firstLook = bad["Variants"]![0]!;
    if (mode == "future") bad["Version"] = 999;
    if (mode == "path") firstLook["Frames"]![0]!["File"] = "../outside.png";
    if (mode == "duplicate") bad["Variants"]![1]!["Choice"] = "Look1";
    if (mode == "crop") firstLook["Frames"]![0]!["Bounds"]![2] = 90000;
    if (mode == "scale") firstLook["Frames"]![0]!["SourceUnitScale"] = 100;
    if (mode == "eye") bad["Variants"]![4]!["EyeEllipse"]![0] = -200;
    if (mode == "null-label") firstLook["Label"] = null;
    if (mode == "null-frame") firstLook["Frames"]![0] = null;
    File.WriteAllText(Path.Combine(invalidDirectory.FullName, CompanionVariantLibrary.MetadataFile), mode == "truncated" ? "{" : bad.ToJsonString());
    Check(!new CompanionVariantLibrary(invalidDirectory.FullName).IsAvailable, "Invalid metadata fails closed: " + mode);
}
}

var native = ImGui.CreateContext();
try
{
    var io = ImGui.GetIO(); io.DeltaTime = 1f / 60; unsafe { io.IniFilename = null; io.LogFilename = null; }
    io.Fonts.AddFontDefault(); io.Fonts.Build(); var painter = new SoftwareRenderer(); var textures = new Dictionary<string, ulong>();
    foreach (var file in library.Files)
    {
        using var bitmap = new Bitmap(Path.Combine(assets, file));
        var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var bytes = new byte[bitmap.Width * bitmap.Height * 4];
        try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, bytes, y * bitmap.Width * 4, bitmap.Width * 4); }
        finally { bitmap.UnlockBits(bits); }
        for (var n = 0; n < bytes.Length; n += 4) (bytes[n], bytes[n + 2]) = (bytes[n + 2], bytes[n]);
        var id = (ulong)textures.Count + 2; painter.AddTexture(id, bytes, bitmap.Width, bitmap.Height); textures.Add(file, id);
    }
    checks += WinkBlinkChecks.Run(library, painter, textures, output.FullName);
    if (!args.Contains("--blink-only"))
    {
    foreach (var avatar in library.Looks.Select(v => v.Avatar).Distinct()) foreach (var size in new[] { 96f, 192f })
    {
        var tile = new Vector2(size + 20, size + 46); io.DisplaySize = new Vector2(tile.X * 8, tile.Y * 4 + 24);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var row = 0;
        foreach (var look in library.Looks.Where(v => v.Avatar == avatar))
        {
            draw.AddText(new Vector2(10, 24 + row * tile.Y), 0xFFFFFFFF, look.Avatar + " / " + look.Label);
            foreach (var pose in Enum.GetValues<CompanionVariantPose>())
            {
                var col = (int)pose; var position = new Vector2(col * tile.X + 10, 44 + row * tile.Y);
                CompanionVariantRenderer.Draw(draw, new ImTextureID(textures[look[pose].File]), CompanionVariantRenderer.Frame(look, pose, position, new(size)));
                draw.AddText(new Vector2(col * tile.X + 3, 44 + row * tile.Y + size + 2), 0xFFBBAAAA, pose.ToString());
            }
            row++;
        }
        ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"native-{avatar.ToLowerInvariant()}-{size:0}.png"));
    }
    for (var alternate = 0; alternate < 2; alternate++)
    {
        io.DisplaySize = new(8 * 136, 4 * 150); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var i = 0; i < library.Looks.Count; i++)
        {
            var look = library.Looks[i]; var col = i / 4; var row = i % 4;
            var grip = new Vector2(col * 136 + 68, row * 150 + 53);
            var pose = alternate == 0 ? CompanionVariantPose.CarrySquirmA : CompanionVariantPose.CarrySquirmB;
            CompanionVariantRenderer.Draw(draw, new ImTextureID(textures[look[pose].File]), CompanionVariantRenderer.CarriedFrame(look, alternate != 0, grip, new(96)));
            draw.AddCircleFilled(grip, 2, 0xFF66CCCC);
            draw.AddText(new Vector2(col * 136 + 5, row * 150 + 5), 0xFFFFFFFF, look.Avatar + " " + look.Choice);
        }
        ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"carry-{alternate}.png"));
    }
    io.DisplaySize = new(4 * 212, 4 * 212); ImGui.NewFrame();
    for (var i = 0; i < library.OriginalOrbitCore.Count; i++)
    {
        var source = library.OriginalOrbitCore[i];
        CompanionVariantRenderer.Draw(ImGui.GetForegroundDrawList(), new ImTextureID(textures[source.File]),
            source.GetFrame(new(i % 4 * 212 + 10, i / 4 * 212 + 10), new(192)));
    }
    ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, "original-orbit-core-192.png"));
    }
}
finally { ImGui.DestroyContext(native); }
Console.WriteLine($"Companion variants: {checks} checks passed; native actual-texture rendering at96/192px. No game or provider calls.");
