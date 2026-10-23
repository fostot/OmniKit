using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;

internal static class FourierBlinkRender
{
    public static unsafe void Run(Assembly assembly, string output)
    {
        const BindingFlags allStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var amount = assembly.GetType("OmniKit.Ui.FourierBlink", true)!.GetMethod("Amount", allStatic)!;
        var oldAmount = assembly.GetType("OmniKit.Ui.RoamingCompanion", true)!.GetMethod("BlinkAmount", allStatic)!;
        var drawBlink = assembly.GetType("OmniKit.Ui.RoamingCompanion", true)!.GetMethod("DrawWinkBlink", allStatic)!;
        var libraryType = assembly.GetType("OmniKit.Ui.CompanionAnimationLibrary", true)!;
        var library = Activator.CreateInstance(libraryType, Path.GetFullPath("assets"))!;
        object?[] lookup = ["Wink", "Navigator", null];
        if (!(bool)libraryType.GetMethod("TryGet")!.Invoke(library, lookup)!) throw new InvalidOperationException("Missing approved Wink art.");
        var atlas = lookup[2]!;
        var getFloat = atlas.GetType().GetMethod("GetFloat")!;
        var file = (string)atlas.GetType().GetProperty("File")!.GetValue(atlas)!;
        var checks = 0;
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); checks++; }
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.IniFilename = null; io.LogFilename = null; io.DeltaTime = 1f / 60;
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            io.Fonts.AddFontDefault(); io.Fonts.Build();
            var renderer = new SoftwareRenderer();
            using (var image = new Bitmap(Path.Combine("assets", file)))
            {
                var bits = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var pixels = new byte[image.Width * image.Height * 4];
                try
                {
                    for (var y = 0; y < image.Height; y++)
                        Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * image.Width * 4, image.Width * 4);
                }
                finally { image.UnlockBits(bits); }
                for (var p = 0; p < pixels.Length; p += 4) (pixels[p], pixels[p + 2]) = (pixels[p + 2], pixels[p]);
                renderer.AddTexture(2, pixels, image.Width, image.Height);
            }
            Directory.CreateDirectory(output);
            var samples = new[] { .30f, .32f, .34f, .36f, .38f, .42f, .46f, .50f, .56f, .60f };
            foreach (var size in new[] { 96f, 192f })
            {
                var tile = size + 12; var rowHeight = size + 38;
                io.DisplaySize = new Vector2(tile * samples.Length + 16, rowHeight * 4 + 38);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                draw.AddText(new Vector2(8, 4), 0xFFFFFFFF, $"Wink / {size:0}px / cached Fourier eyelid / stable frame 0 / idle then float, both facings");
                for (var row = 0; row < 4; row++)
                {
                    var mirrored = (row & 1) != 0; var walking = row >= 2;
                    for (var column = 0; column < samples.Length; column++)
                    {
                        var time = samples[column];
                        var closure = (float)amount.Invoke(null, [time, 3f, true])!;
                        var position = new Vector2(8 + column * tile, 34 + row * rowHeight);
                        // Idle uses the same frozen phase as the last movement.
                        // Floating continues the existing production transform.
                        var phase = walking ? .2f + time * .37f : .2f;
                        var quad = getFloat.Invoke(atlas, [phase, mirrored, position, new Vector2(size)])!;
                        Vector2 Q(string name) => (Vector2)quad.GetType().GetProperty(name)!.GetValue(quad)!;
                        var hit = (ValueTuple<Vector2, Vector2>)quad.GetType().GetProperty("HitBounds")!.GetValue(quad)!;
                        var first = draw.VtxBuffer.Size;
                        draw.PushClipRect(position, position + new Vector2(size), true);
                        draw.AddImageQuad(new ImTextureID(2UL), Q("A"), Q("B"), Q("C"), Q("D"),
                            Q("UvA"), Q("UvB"), Q("UvC"), Q("UvD"), 0xFFFFFFFF);
                        var body = Enumerable.Range(first, 4).Select(index => draw.VtxBuffer[index]).ToArray();
                        drawBlink.Invoke(null, [draw, new ImTextureID(2UL), quad, closure, 0xFFFFFFFFu]);
                        Check(Enumerable.Range(0, 4).All(i => body[i].Pos == draw.VtxBuffer[first + i].Pos
                            && body[i].Uv == draw.VtxBuffer[first + i].Uv), "The lid must not redraw, rescale or move the body texture.");
                        Check(hit == ((Vector2, Vector2))quad.GetType().GetProperty("HitBounds")!.GetValue(quad)!,
                            "Blinking leaves the production sprite pick bounds unchanged.");
                        var valid = true;
                        for (var i = first; i < draw.VtxBuffer.Size; i++)
                        {
                            var p = draw.VtxBuffer[i].Pos;
                            valid &= float.IsFinite(p.X) && float.IsFinite(p.Y) && p.X >= position.X && p.Y >= position.Y
                                && p.X <= position.X + size && p.Y <= position.Y + size;
                        }
                        Check(valid, "Mirrored lid and body vertices remain inside the complete logical cell.");
                        draw.PopClipRect();
                        draw.AddText(position + new Vector2(0, size + 1), 0xFFCDD6DE,
                            $"{(walking ? "Float" : "Idle")} {(mirrored ? "L" : "R")} {time:0.00}/{closure:0.00}");
                    }
                }
                ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"wink-fourier-{size:0}.png"));
            }
            foreach (var time in samples)
            {
                Check((float)amount.Invoke(null, [time, 3f, false])! == 0, "The opt-in curve respects disabled blinking or animations.");
                var legacy = (float)oldAmount.Invoke(null, [time, 3f, true])!;
                var t = (Math.Max(0, time) + 3 - .3f) % 3;
                var expected = t >= .28f ? 0 : t < .08f ? t / .08f : t < .16f ? 1 : 1 - (t - .16f) / .12f;
                Check(legacy == expected, "The existing default blink remains exactly unchanged.");
            }
            Console.WriteLine($"PASS: {checks} focused native Fourier eyelid checks; 80 production texture samples at 96/192px; no game/config/network access.");
        }
        finally { ImGui.DestroyContext(context); }
    }
}
