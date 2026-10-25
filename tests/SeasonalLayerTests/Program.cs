using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

var assets = Path.GetFullPath("assets");
var output = Directory.CreateDirectory(Path.GetFullPath(".local/seasonal-layers"));
var renderer = new SeasonalCompanionRenderer(assets);
var checks = 0;
void Check(bool condition, string description) { checks++; if (!condition) throw new InvalidOperationException(description); }
Check(renderer.IsAvailable, "Reviewed source metadata loads.");
var bows = new[] { SeasonalCompanionTheme.Heavensturn, SeasonalCompanionTheme.Valentione,
    SeasonalCompanionTheme.LittleLadies, SeasonalCompanionTheme.HatchingTide, SeasonalCompanionTheme.TheRising };
var artPath = Path.Combine(assets, SeasonalCompanionRenderer.File);
var artHash = SHA256.HashData(File.ReadAllBytes(artPath));
Check(Convert.ToHexString(artHash).Equals("236b61c902a0cef852ef46d861d5e4d778c3755cff429969ec87a5a2377d0058", StringComparison.OrdinalIgnoreCase),
    "Original accessory pixels remain byte-identical to the approved source.");
using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(assets, "seasonal-accessories.frames.json")));
var sourceSize = new Vector2(metadata.RootElement.GetProperty("Width").GetSingle(), metadata.RootElement.GetProperty("Height").GetSingle());
var sourceRects = new Dictionary<SeasonalCompanionTheme, Vector4>();
foreach (var theme in metadata.RootElement.GetProperty("Themes").EnumerateArray())
{
    var id = Enum.Parse<SeasonalCompanionTheme>(theme.GetProperty("Id").GetString()!);
    var values = theme.GetProperty("Bounds").EnumerateArray().Select(n => n.GetSingle()).ToArray();
    sourceRects.Add(id, new(values[0], values[1], values[2], values[3]));
    Check(!theme.TryGetProperty("FrontPolygon", out _) && !theme.TryGetProperty("RearPolygon", out _),
        "No accessory source rectangle is partitioned or cut.");
    Check(SeasonalCompanionRenderer.IsBow(id) == bows.Contains(id), "Only the five ribbon themes are overlay bows.");
}

var native = ImGui.CreateContext();
try
{
    var io = ImGui.GetIO(); io.DeltaTime = 1f / 60; io.DisplaySize = new(1000, 300);
    unsafe { io.IniFilename = null; io.LogFilename = null; }
    io.Fonts.AddFontDefault(); io.Fonts.Build();
    var painter = new SoftwareRenderer();
    using (var bitmap = new Bitmap(artPath))
    {
        var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
        finally { bitmap.UnlockBits(bits); }
        for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        painter.AddTexture(3, pixels, bitmap.Width, bitmap.Height);
    }
    foreach (var size in new[] { 96f, 192f })
    foreach (var mirrored in new[] { false, true })
    foreach (var theme in Enum.GetValues<SeasonalCompanionTheme>())
    {
        var body = SeasonalBodyTransform.FromFrame(new(0, 0, 300, 300), 300,
            new CompanionFrame(new(200, 70), new(size)), mirrored);
        Check(renderer.TryGetQuad(theme, body, new SeasonalAccessoryFit(new(150, 80), 180), out var quad), "Fitted source quad resolves.");
        var pivot = body.Map(new(150, 80)); var angle = mirrored ? -.6f : .6f;
        Vector2 Adjust(Vector2 p)
        {
            var local = p - pivot;
            return pivot + new Vector2(local.X * MathF.Cos(angle) - local.Y * MathF.Sin(angle),
                local.X * MathF.Sin(angle) + local.Y * MathF.Cos(angle)) + new Vector2(7, 13);
        }
        quad = quad with { A = Adjust(quad.A), B = Adjust(quad.B), C = Adjust(quad.C), D = Adjust(quad.D) };
        var rect = sourceRects[theme];
        Check(quad.UvStart == new Vector2(rect.X, rect.Y) / sourceSize && quad.UvEnd == new Vector2(rect.Z, rect.W) / sourceSize,
            "Fitting preserves the complete original source UV rectangle, including hat brims.");
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        var vertices = draw.VtxBuffer.Size; var indices = draw.IdxBuffer.Size;
        var rearBounds = renderer.DrawLayer(draw, new ImTextureID(3UL), quad, theme, false);
        Check(draw.VtxBuffer.Size == vertices && draw.IdxBuffer.Size == indices, "Every rear pass emits zero geometry.");
        Check(rearBounds == quad.Bounds, "An empty rear pass preserves fitted bounds.");
        var frontBounds = renderer.DrawLayer(draw, new ImTextureID(3UL), quad, theme, true, 0xDDFAFBFCu);
        Check(frontBounds == quad.Bounds, "Front bounds preserve the complete fitted quad.");
        if (bows.Contains(theme))
        {
            Check(draw.VtxBuffer.Size - vertices == 4 && draw.IdxBuffer.Size - indices == 6, "A bow draws exactly one complete quad.");
            var reference = draw.VtxBuffer.Size;
            SeasonalCompanionRenderer.Draw(draw, new ImTextureID(3UL), quad, 0xDDFAFBFCu);
            for (var i = 0; i < 4; i++)
            {
                var actual = draw.VtxBuffer[vertices + i]; var expected = draw.VtxBuffer[reference + i];
                Check(actual.Pos == expected.Pos && actual.Uv == expected.Uv && actual.Col == expected.Col,
                    "Front bow geometry, UV and tint equal the original uncut quad draw.");
                Check(float.IsFinite(actual.Pos.X) && float.IsFinite(actual.Pos.Y), "Rotated and mirrored fits remain finite.");
            }
        }
        else Check(draw.VtxBuffer.Size == vertices && draw.IdxBuffer.Size == indices, "Painted hats emit no separate front or rear overlay.");
        ImGui.Render();
    }
    // One compact visual proof of the five complete front bows, without changing source pixels.
    ImGui.NewFrame(); var gallery = ImGui.GetForegroundDrawList();
    for (var i = 0; i < bows.Length; i++)
    {
        var body = SeasonalBodyTransform.FromFrame(new(0, 0, 300, 300), 300,
            new CompanionFrame(new(30 + i * 195, 90), new(160)), false);
        Check(renderer.TryGetQuad(bows[i], body, new SeasonalAccessoryFit(new(150, 80), 200), out var quad), "Gallery bow resolves.");
        renderer.DrawLayer(gallery, new ImTextureID(3UL), quad, bows[i], true);
        gallery.AddText(new(12 + i * 195, 220), 0xFFFFFFFF, bows[i].ToString());
    }
    ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, "whole-front-bows.png"));
}
finally { ImGui.DestroyContext(native); }
Check(artHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(artPath))), "Original accessory PNG remains unchanged after native rendering.");
Console.WriteLine($"Seasonal layers: {checks} native/metadata checks passed; five complete front bows, zero hat/rear geometry; original PNG unchanged.");
