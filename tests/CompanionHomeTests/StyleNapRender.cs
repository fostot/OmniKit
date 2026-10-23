using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

internal static class StyleNapRender
{
    internal static void Run(string root, Action<bool, string> check)
    {
        var directory = Path.Combine(root, "assets");
        var output = Path.Combine(root, ".local", "visualharness", "companion-home", "styles");
        var styles = new CompanionHomeStyleLibrary(directory);
        var naps = new CompanionNapArtwork(directory);
        if (!naps.TryGet("Wink", "Default", out var nap) || nap == null) throw new InvalidOperationException("Missing approved nap artwork.");
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.DeltaTime = 1f / 60;
            unsafe { io.IniFilename = null; io.LogFilename = null; }
            io.Fonts.AddFontDefault(); io.Fonts.Build(); var renderer = new SoftwareRenderer();
            var textures = new Dictionary<string, ulong>();
            foreach (var file in new[] { "homes-standard.png", "homes-holiday.png", nap.File })
            {
                var id = (ulong)textures.Count + 2;
                using var image = new Bitmap(Path.Combine(directory, file));
                var bits = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var pixels = new byte[image.Width * image.Height * 4];
                try { for (var y = 0; y < image.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * image.Width * 4, image.Width * 4); }
                finally { image.UnlockBits(bits); }
                for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                renderer.AddTexture(id, pixels, image.Width, image.Height); textures.Add(file, id);
            }
            foreach (var edge in new[] { 96f, 192f })
            foreach (var mirrored in new[] { false, true })
            {
                var spriteSize = new Vector2(edge / 1.3f);
                var gutter = edge * .12f + 12;
                var tile = new Vector2(edge + gutter * 2, edge + spriteSize.Y + 30);
                var card = new Vector2(tile.X * 2 + 12, tile.Y * 2 + 35);
                io.DisplaySize = new(card.X * 3, card.Y * 4);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var row = 0;
                foreach (var id in CompanionHomeStyleLibrary.HomeIds.Where(id => id is not ("Default" or "Random")))
                {
                    var cardTop = new Vector2(row % 3 * card.X, row / 3 * card.Y);
                    draw.AddText(cardTop + new Vector2(8, 4), 0xFFFFFFFF, id + (mirrored ? " / left" : " / right"));
                    for (var stage = 0; stage < 4; stage++)
                    {
                        var top = cardTop + new Vector2(stage % 2 * tile.X, stage / 2 * tile.Y + 24);
                        var layout = new CompanionLayout(top, top + tile, spriteSize);
                        var house = new CompanionHomeRect(top + new Vector2(gutter, 12), top + new Vector2(gutter + edge, 12 + edge));
                        var style = styles.GetHome(id).Place(house.Minimum, house.Maximum, mirrored);
                        var state = stage < 2 ? CompanionHomeState.Entering : stage == 2 ? CompanionHomeState.Sleeping : CompanionHomeState.Leaving;
                        var time = stage switch { 0 => .3f, 1 => 1f, 2 => .5f, _ => .7f };
                        var routine = StyleNavigationChecks.Sample(layout, house, style, state, time);
                        draw.PushClipRect(top, top + tile, true);
                        var texture = new ImTextureID(textures[style.File]);
                        CompanionHomeRenderer.DrawHouse(draw, texture, house, mirrored, style);
                        var bounds = CompanionHomeRenderer.DrawNap(draw, new ImTextureID(textures[nap.File]), texture, nap, routine,
                            spriteSize, house, mirrored, styleFrame: style);
                        check(bounds.Minimum.X >= top.X - .01f && bounds.Minimum.Y >= top.Y - .01f
                            && bounds.Maximum.X <= top.X + tile.X + .01f && bounds.Maximum.Y <= top.Y + tile.Y + .01f,
                            "The actual styled nap painter remains bounded during entry, sleep and exit.");
                        check(Math.Abs(routine.Position.X - style.BedPosition(spriteSize).X) < .01f,
                            $"Actual front door lane: {id}, {edge}, mirrored={mirrored}, stage={stage}, actual={routine.Position.X}, expected={style.BedPosition(spriteSize).X}.");
                        if (routine.Sleeping) CompanionHomeRenderer.DrawZs(draw, house, mirrored, routine.SleepPhase, top, tile, style);
                        draw.AddText(top + new Vector2(12, tile.Y - 16), 0xFFB3C1D0, stage switch { 0 => "Enter .30", 1 => "Enter 1.00", 2 => "Sleep", _ => "Leave .70" });
                        draw.PopClipRect();
                    }
                    row++;
                }
                var finite = true;
                for (var i = 0; i < draw.VtxBuffer.Size; i++)
                {
                    var p = draw.VtxBuffer[i].Pos;
                    finite &= float.IsFinite(p.X) && float.IsFinite(p.Y) && p.X >= 0 && p.Y >= 0 && p.X <= io.DisplaySize.X && p.Y <= io.DisplaySize.Y;
                }
                check(finite, "All styled nap/font/sleep-symbol vertices fit the viewport.");
                ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"style-naps-{edge:0}-{(mirrored ? "left" : "right")}.png"));
            }
        }
        finally { ImGui.DestroyContext(context); }
    }
}
