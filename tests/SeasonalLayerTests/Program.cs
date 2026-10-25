using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

var assets = Path.GetFullPath("assets");
var output = Directory.CreateDirectory(Path.GetFullPath(".local/seasonal-layers"));
var renderer = new SeasonalCompanionRenderer(assets);
var checks = 0;
void Check(bool condition, string description) { checks++; if (!condition) throw new InvalidOperationException(description); }
Check(renderer.IsAvailable, "Production layered metadata loads.");
var hats = new[] { SeasonalCompanionTheme.MakeItRain, SeasonalCompanionTheme.MoonfireFaire, SeasonalCompanionTheme.AllSaintsWake, SeasonalCompanionTheme.Starlight };
float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
float Area(SeasonalLayerTriangle t) => Cross(t.B - t.A, t.C - t.A) * .5f;
foreach (var theme in Enum.GetValues<SeasonalCompanionTheme>())
{
    Check(renderer.TryGetLayerTriangles(theme, false, out var rear), "Rear layer resolves.");
    Check(renderer.TryGetLayerTriangles(theme, true, out var front), "Front layer resolves.");
    Check((rear.Count > 0) == hats.Contains(theme), "Only the four hats have rear pieces.");
    var triangles = front.Concat(rear).ToArray();
    Check(Math.Abs(triangles.Sum(Area) - 1) < .00001f, "Layers cover exactly the original source area.");
    foreach (var t in triangles) Check(Area(t) > 0, "Every layer triangle has positive area.");
    // Non-grid offsets avoid deliberately sampling shared triangle edges.
    for (var y = 0; y < 57; y++) for (var x = 0; x < 61; x++)
    {
        var p = new Vector2((x + .217f) / 61, (y + .391f) / 57);
        var coverage = 0; var edge = false;
        foreach (var t in triangles)
        {
            var a = Cross(t.B - t.A, p - t.A); var b = Cross(t.C - t.B, p - t.B); var c = Cross(t.A - t.C, p - t.C);
            if (Math.Abs(a) < .0000001f || Math.Abs(b) < .0000001f || Math.Abs(c) < .0000001f) edge = true;
            if (a > 0 && b > 0 && c > 0) coverage++;
        }
        if (!edge) Check(coverage == 1, "Each source point belongs to exactly one layer triangle.");
    }
}

var metadataPath = Path.Combine(assets, "seasonal-accessories.frames.json");
var metadata = File.ReadAllText(metadataPath);
void Reject(Action<JsonObject> mutation, string label)
{
    var folder = Directory.CreateDirectory(Path.Combine(output.FullName, "invalid-" + label));
    var document = JsonNode.Parse(metadata)!.AsObject(); mutation(document);
    File.WriteAllText(Path.Combine(folder.FullName, "seasonal-accessories.frames.json"), document.ToJsonString());
    Check(!new SeasonalCompanionRenderer(folder.FullName).IsAvailable, "Malformed layer partition rejected: " + label);
}
JsonObject Hat(JsonObject d) => d["Themes"]!.AsArray().Select(n => n!.AsObject()).First(n => n["Id"]!.GetValue<string>() == "AllSaintsWake");
Reject(d => Hat(d).Remove("RearPolygon"), "missing-complement");
Reject(d => Hat(d)["RearPolygon"]![0]![0] = -1, "outside-source");
Reject(d => Hat(d)["RearPolygon"] = Hat(d)["FrontPolygon"]!.DeepClone(), "overlap");
Reject(d => Hat(d)["RearPolygon"] = JsonNode.Parse("[[0,0],[1,1],[1,0],[0,1]]"), "self-crossing");

var artHash = SHA256.HashData(File.ReadAllBytes(Path.Combine(assets, SeasonalCompanionRenderer.File)));
using var bodyDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(assets, "abstract-companions.frames.json")));
var bodyAtlas = bodyDocument.RootElement.GetProperty("atlases").EnumerateArray().First(a => a.GetProperty("character").GetString() == "mochi");
var bodyFile = bodyAtlas.GetProperty("file").GetString()!;
var source = bodyAtlas.GetProperty("frames")[0];
var rectValues = source.GetProperty("rect").EnumerateArray().Select(n => n.GetSingle()).ToArray();
var rect = new Vector4(rectValues[0], rectValues[1], rectValues[2], rectValues[3]);
var anchorValues = source.GetProperty("anchor").EnumerateArray().Select(n => n.GetSingle()).ToArray();
var anchor = new Vector2(anchorValues[0], anchorValues[1]);
var bodyScale = bodyAtlas.GetProperty("commonSourceScalePx").GetSingle();
var native = ImGui.CreateContext();
try
{
    var io = ImGui.GetIO(); io.DeltaTime = 1f / 60;
    unsafe { io.IniFilename = null; io.LogFilename = null; }
    io.Fonts.AddFontDefault(); io.Fonts.Build(); var painter = new SoftwareRenderer();
    Vector2 Load(string file, ulong id)
    {
        using var bitmap = new Bitmap(Path.Combine(assets, file));
        var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
        finally { bitmap.UnlockBits(bits); }
        for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        painter.AddTexture(id, pixels, bitmap.Width, bitmap.Height); return new(bitmap.Width, bitmap.Height);
    }
    var bodySheet = Load(bodyFile, 2); Load(SeasonalCompanionRenderer.File, 3);
    foreach (var size in new[] { 96f, 192f })
    {
        var tile = new Vector2(size * 1.8f, size * 1.9f + 34); var themes = Enum.GetValues<SeasonalCompanionTheme>();
        io.DisplaySize = tile * new Vector2(4, themes.Length); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var row = 0; row < themes.Length; row++) for (var col = 0; col < 4; col++)
        {
            var mirrored = col >= 2; var layered = col % 2 == 1;
            var cell = new Vector2(col * tile.X + size * .4f, row * tile.Y + size * .65f + 30);
            var factor = size / bodyScale;
            var position = cell + new Vector2(size * .5f, size * .9f) - (anchor - new Vector2(rect.X, rect.Y)) * factor;
            var frame = new CompanionFrame(position, new Vector2(rect.Z - rect.X, rect.W - rect.Y) * factor);
            var body = SeasonalBodyTransform.FromFrame(rect, bodyScale, frame, mirrored);
            Check(renderer.TryGetQuad(themes[row], "Mochi", 0, false, body, out var quad), "Fitted source quad resolves.");
            Check(renderer.TryGetFit("Mochi", 0, false, out var fit), "Reviewed body fit exists.");
            var pivot = body.Map(fit.Anchor); var angle = mirrored ? -.35f : .12f;
            Vector2 Adjust(Vector2 p)
            {
                var local = p - pivot; var rotated = new Vector2(local.X * MathF.Cos(angle) - local.Y * MathF.Sin(angle), local.X * MathF.Sin(angle) + local.Y * MathF.Cos(angle));
                return pivot + rotated + new Vector2(0, size * .12f);
            }
            quad = quad with { A = Adjust(quad.A), B = Adjust(quad.B), C = Adjust(quad.C), D = Adjust(quad.D) };
            var first = draw.VtxBuffer.Size;
            if (layered) renderer.DrawLayer(draw, new ImTextureID(3UL), quad, themes[row], false);
            draw.AddImageQuad(new ImTextureID(2UL), body.A, body.B, body.B + body.D - body.A, body.D,
                new Vector2(rect.X, rect.Y) / bodySheet, new Vector2(rect.Z, rect.Y) / bodySheet,
                new Vector2(rect.Z, rect.W) / bodySheet, new Vector2(rect.X, rect.W) / bodySheet);
            if (layered) renderer.DrawLayer(draw, new ImTextureID(3UL), quad, themes[row], true);
            else SeasonalCompanionRenderer.Draw(draw, new ImTextureID(3UL), quad);
            for (var i = first; i < draw.VtxBuffer.Size; i++)
            {
                var p = draw.VtxBuffer[i].Pos;
                Check(float.IsFinite(p.X) && float.IsFinite(p.Y), "Adjusted layer vertices remain finite.");
                Check(p.X > col * tile.X + 3 && p.X < (col + 1) * tile.X - 3 && p.Y > row * tile.Y + 24 && p.Y < (row + 1) * tile.Y - 3, "Rendered layer fits its explicit gallery gutter.");
            }
            draw.AddText(new(col * tile.X + 6, row * tile.Y + 5), 0xFFFFFFFF, themes[row].ToString());
            draw.AddText(new(col * tile.X + 6, row * tile.Y + 20), 0xFFBBBBBB, layered ? "Rear / body / front" : "Single front layer");
        }
        ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"wearing-{size:0}.png"));
    }
}
finally { ImGui.DestroyContext(native); }
Check(artHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(assets, SeasonalCompanionRenderer.File)))), "Original accessory PNG unchanged.");
Console.WriteLine($"Seasonal layers: {checks} geometry/native checks passed; four hat partitions, five full-front decorations; PNG unchanged.");
