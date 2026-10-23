using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

var root = Path.GetFullPath(args.ElementAtOrDefault(0) ?? ".");
var output = Path.Combine(root, ".local", "visualharness", "bimble-v2");
Directory.CreateDirectory(output);
var assets = Path.Combine(root, "assets");
var library = new LunarWalkRigLibrary(assets);
var context = ImGui.CreateContext();
try
{
    var io = ImGui.GetIO();
    io.DisplaySize = new Vector2(1060, 430); io.DeltaTime = 1f / 60;
    unsafe { io.IniFilename = null; io.LogFilename = null; }
    io.Fonts.AddFontDefault(); io.Fonts.Build();
    var renderer = new SoftwareRenderer();
    ulong textureId = 2;
    foreach (var outfit in new[] { "Navigator", "Stargazer" })
    {
        if (!library.TryGet("Bimble", outfit, out var rig) || rig?.SupportsIdle != true)
            throw new InvalidOperationException("Expected the reviewed shared-idle Bimble rig.");
        using var bitmap = new Bitmap(Path.Combine(assets, rig.File));
        var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        try
        {
            for (var y = 0; y < bitmap.Height; y++)
                Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4);
        }
        finally { bitmap.UnlockBits(bits); }
        for (var p = 0; p < pixels.Length; p += 4) (pixels[p], pixels[p + 2]) = (pixels[p + 2], pixels[p]);
        renderer.AddTexture(textureId, pixels, bitmap.Width, bitmap.Height);
        foreach (var left in new[] { false, true })
        {
            ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
            draw.AddText(new Vector2(10, 8), 0xFFFFFFFF, $"Bimble {outfit} / {(left ? "left" : "right")} / shared body and limb textures / native ImGui quads");
            for (var column = 0; column < 5; column++)
            {
                var phase = column == 0 ? .75f : (column - 1) * .25f;
                var blend = column == 0 ? 0 : 1;
                draw.AddText(new Vector2(15 + column * 208, 32), 0xFFFFFFFF, column == 0 ? "SETTLED IDLE" : $"WALK {phase:0.00}");
                foreach (var size in new[] { 192f, 96f })
                {
                    var origin = new Vector2(12 + column * 208 + (192 - size) * .5f, size == 192 ? 54 : 292);
                    var pose = rig.Evaluate(phase, left, origin, new Vector2(size), walkBlend: blend);
                    foreach (var q in pose.Quads)
                        draw.AddImageQuad(new ImTextureID(textureId), q.A, q.B, q.C, q.D, q.UvA, q.UvB, q.UvC, q.UvD, 0xFFFFFFFF);
                    draw.AddLine(origin + new Vector2(0, .9f * size), origin + new Vector2(size, .9f * size), 0xFF65735E);
                    if (pose.Bounds.Minimum.X < origin.X || pose.Bounds.Maximum.X > origin.X + size
                        || pose.Bounds.Minimum.Y < origin.Y || pose.Bounds.Maximum.Y > origin.Y + size)
                        throw new InvalidOperationException("Native preview geometry escaped its logical cell.");
                }
            }
            ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"idle-walk-{outfit}-{(left ? "left" : "right")}.png"));
        }
        textureId++;
    }
    Console.WriteLine("PASS: four native ImGui texture comparisons: actual settled idle plus quarter walks, both outfits/facings at96/192px; geometry fits each cell. No game process.");
}
finally { ImGui.DestroyContext(context); }
