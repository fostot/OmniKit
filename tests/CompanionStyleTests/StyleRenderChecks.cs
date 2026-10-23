using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

internal static class StyleRenderChecks
{
    public static void Run(string root, CompanionHomeStyleLibrary library, Action<bool, string> check)
    {
        var directory = Path.Combine(root, "assets");
        var output = Path.Combine(root, ".local", "visualharness", "companion-styles");
        var naps = new CompanionNapArtwork(directory);
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.DeltaTime = 1f / 60;
            unsafe { io.IniFilename = null; io.LogFilename = null; }
            io.Fonts.AddFontDefault(); io.Fonts.Build();
            var renderer = new SoftwareRenderer();
            var textures = new Dictionary<string, ulong>();
            foreach (var file in new[] { "companion-house.png", "companion-grab-hand.png", "homes-standard.png", "homes-holiday.png", "hands-styles.png", "companion-naps-abstract.png" })
            {
                var id = (ulong)textures.Count + 2;
                using var bitmap = new Bitmap(Path.Combine(directory, file));
                var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var pixels = new byte[bitmap.Width * bitmap.Height * 4];
                try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
                finally { bitmap.UnlockBits(bits); }
                for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                renderer.AddTexture(id, pixels, bitmap.Width, bitmap.Height); textures.Add(file, id);
            }
            foreach (var edge in new[] { 96f, 192f })
            foreach (var mirrored in new[] { false, true })
            foreach (var avatar in new[] { "Moonblob", "Wink" })
            {
                if (!naps.TryGet(avatar, "Default", out var nap) || nap == null) throw new InvalidOperationException("Missing reviewed nap fixture.");
                var tile = new Vector2(edge + 28, edge + 34);
                io.DisplaySize = new(tile.X * 5, tile.Y * 3);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var index = 0;
                foreach (var id in CompanionHomeStyleLibrary.HomeIds.Where(id => id != "Random"))
                {
                    var top = new Vector2(index % 5 * tile.X + 14, index / 5 * tile.Y + 8);
                    var frame = library.GetHome(id).Place(top, top + new Vector2(edge), mirrored);
                    var texture = new ImTextureID(textures[frame.File]);
                    CompanionStyleRenderer.DrawHome(draw, texture, frame);
                    var sprite = new Vector2(edge / 1.3f);
                    var visitor = nap.Frame(2, frame.BedPosition(sprite), sprite, .62f, !mirrored);
                    draw.AddImage(new ImTextureID(textures[nap.File]), visitor.Position, visitor.Position + visitor.Size, visitor.UvStart, visitor.UvEnd);
                    CompanionStyleRenderer.DrawHomeWalls(draw, texture, frame);
                    check(visitor.Position.X >= top.X - .01f && visitor.Position.Y >= top.Y - .01f
                        && visitor.Position.X + visitor.Size.X <= top.X + edge + .01f && visitor.Position.Y + visitor.Size.Y <= top.Y + edge + .01f,
                        "Accepted nap art stays inside each selected house before wall masking.");
                    draw.AddText(top + new Vector2(0, edge + 4), 0xFFFFFFFF, id);
                    index++;
                }
                ValidateVertices(draw, io.DisplaySize, check);
                ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"homes-{avatar}-{edge:0}-{(mirrored ? "left" : "right")}.png"));
            }
            foreach (var edge in new[] { 96f, 192f })
            {
                var tile = new Vector2(edge + 28, edge + 42);
                io.DisplaySize = new(tile.X * 4, tile.Y * 5);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var row = 0;
                foreach (var id in CompanionHomeStyleLibrary.HandIds.Where(id => id != "Random"))
                {
                    var hand = library.GetHand(id);
                    foreach (var pose in Enum.GetValues<CompanionHandPose>())
                    {
                        var top = new Vector2((int)pose * tile.X + 14, row * tile.Y + 8);
                        var contact = top + new Vector2(.5f, .77f) * edge;
                        var quad = hand.Frame(pose, contact, edge);
                        CompanionStyleRenderer.DrawQuad(draw, new ImTextureID(textures[hand.Pose(pose).File]), quad);
                        draw.AddCircle(contact, 2, 0xFF80FF80);
                        draw.AddText(top + new Vector2(0, edge + 4), 0xFFFFFFFF, id);
                        draw.AddText(top + new Vector2(0, edge + 17), 0xFFABB8C8, pose.ToString());
                    }
                    row++;
                }
                ValidateVertices(draw, io.DisplaySize, check);
                ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"hands-{edge:0}.png"));
            }
        }
        finally { ImGui.DestroyContext(context); }
    }

    private static void ValidateVertices(ImDrawListPtr draw, Vector2 viewport, Action<bool, string> check)
    {
        var valid = draw.VtxBuffer.Size > 0;
        for (var i = 0; i < draw.VtxBuffer.Size; i++)
        {
            var point = draw.VtxBuffer[i].Pos;
            valid &= float.IsFinite(point.X) && float.IsFinite(point.Y) && point.X >= 0 && point.Y >= 0 && point.X <= viewport.X && point.Y <= viewport.Y;
        }
        check(valid, "Actual native renderer vertices stay in the synthetic viewport.");
    }
}
