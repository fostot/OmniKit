using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit;
using OmniKit.Ui;

internal static class NativeRender
{
    public static void Run(string root, Action<bool, string> check)
    {
        var directory = Path.Combine(root, "assets");
        var output = Path.Combine(root, ".local", "visualharness", "companion-home");
        Directory.CreateDirectory(output);
        var library = new CompanionNapArtwork(directory);
        var identities = Configuration.AvatarNames.Select(avatar => (avatar, "Default")).ToArray();
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.DeltaTime = 1f / 60;
            unsafe { io.IniFilename = null; io.LogFilename = null; }
            io.Fonts.AddFontDefault(); io.Fonts.Build();
            var renderer = new SoftwareRenderer();
            foreach (var avatar in Configuration.AvatarNames)
            foreach (var pose in Enum.GetValues<CompanionPose>())
            {
                io.DisplaySize = new Vector2(192);
                ImGui.NewFrame(); var fallback = ImGui.GetForegroundDrawList();
                ProceduralCompanion.Draw(fallback, Vector2.Zero, new Vector2(192), new Configuration { Avatar = avatar }, pose, 1);
                var contained = fallback.VtxBuffer.Size > 0;
                for (var index = 0; index < fallback.VtxBuffer.Size; index++)
                {
                    var p = fallback.VtxBuffer[index].Pos;
                    contained &= float.IsFinite(p.X) && float.IsFinite(p.Y) && p.X >= 0 && p.Y >= 0 && p.X <= 192 && p.Y <= 192;
                }
                check(contained, "Every active companion has one visible, bounded whole-body fallback when optional texture art is missing.");
                ImGui.Render();
            }
            var textures = new Dictionary<string, ulong>(StringComparer.Ordinal);
            var files = identities.Select(identity => library.TryGet(identity.Item1, identity.Item2, out var sheet) && sheet != null
                ? sheet.File : throw new InvalidOperationException("Missing active companion nap artwork."));
            foreach (var file in files.Append("companion-house.png").Distinct(StringComparer.Ordinal))
            {
                var id = (ulong)textures.Count + 2;
                using var image = new Bitmap(Path.Combine(directory, file));
                var bits = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var pixels = new byte[image.Width * image.Height * 4];
                try
                {
                    for (var y = 0; y < image.Height; y++)
                        Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * image.Width * 4, image.Width * 4);
                }
                finally { image.UnlockBits(bits); }
                for (var p = 0; p < pixels.Length; p += 4) (pixels[p], pixels[p + 2]) = (pixels[p + 2], pixels[p]);
                renderer.AddTexture(id, pixels, image.Width, image.Height); textures.Add(file, id);
            }
            foreach (var size in new[] { 96f, 192f })
            foreach (var mirrored in new[] { false, true })
            {
                var tile = new Vector2(MathF.Ceiling(size * 3.6f), MathF.Ceiling(size * 1.5f));
                io.DisplaySize = new Vector2(tile.X * 4, (tile.Y + 32) * identities.Length + 22);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                draw.AddText(new Vector2(8, 4), 0xFFFFFFFF, $"Production home painter / {size:0}px / {(mirrored ? "left" : "right")} door / Enter, Settle, Inhale, Exhale");
                for (var row = 0; row < identities.Length; row++)
                {
                    var (avatar, outfit) = identities[row];
                    if (!library.TryGet(avatar, outfit, out var sheet) || sheet == null) throw new InvalidOperationException("Missing reviewed nap row.");
                    draw.AddText(new Vector2(8, 23 + row * (tile.Y + 32)), 0xFFFFFFFF, avatar + " / " + outfit);
                    for (var column = 0; column < 4; column++)
                    {
                        var minimum = new Vector2(column * tile.X, 43 + row * (tile.Y + 32));
                        var layout = new CompanionLayout(minimum, minimum + tile, new Vector2(size));
                        var house = CompanionHomeGeometry.Bounds(layout, minimum, tile, new Vector2(.5f, 1));
                        var state = column switch { 0 => CompanionHomeState.Entering, 1 => CompanionHomeState.Settling, _ => CompanionHomeState.Sleeping };
                        var time = column switch { 0 => .7f, 1 => .35f, 2 => 0, _ => 1.6f };
                        var routine = Sample(layout, house, mirrored, state, time);
                        draw.PushClipRect(minimum, minimum + tile, true);
                        var houseTexture = new ImTextureID(textures["companion-house.png"]);
                        CompanionHomeRenderer.DrawHouse(draw, houseTexture, house, mirrored);
                        var painted = CompanionHomeRenderer.DrawNap(draw, new ImTextureID(textures[sheet.File]), houseTexture,
                            sheet, routine, layout.SpriteSize, house, mirrored);
                        check(painted.Minimum.X >= minimum.X - .1f && painted.Minimum.Y >= minimum.Y - .1f &&
                            painted.Maximum.X <= minimum.X + tile.X + .1f && painted.Maximum.Y <= minimum.Y + tile.Y + .1f,
                            "Production nap painter bounds fit the synthetic viewport at each state, size and facing.");
                        if (routine.Sleeping) CompanionHomeRenderer.DrawZs(draw, house, mirrored, routine.SleepPhase, minimum, tile);
                        draw.PopClipRect();
                    }
                }
                ImGui.Render();
                renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"all-eight-{size:0}-{(mirrored ? "left" : "right")}.png"));
            }

            io.DisplaySize = new Vector2(640, 420);
            ImGui.NewFrame(); var edgeDraw = ImGui.GetForegroundDrawList();
            foreach (var corner in new[] { Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One })
            {
                var layout = new CompanionLayout(Vector2.Zero, io.DisplaySize, new Vector2(96));
                var house = CompanionHomeGeometry.Bounds(layout, Vector2.Zero, io.DisplaySize, corner);
                var mirrored = corner.X > .5f;
                CompanionHomeRenderer.DrawHouse(edgeDraw, new ImTextureID(textures["companion-house.png"]), house, mirrored);
                var before = edgeDraw.VtxBuffer.Size;
                CompanionHomeRenderer.DrawZs(edgeDraw, house, mirrored, 1.2f, Vector2.Zero, io.DisplaySize);
                var fits = true;
                for (var index = before; index < edgeDraw.VtxBuffer.Size; index++)
                {
                    var p = edgeDraw.VtxBuffer[index].Pos;
                    fits &= p.X >= 0 && p.Y >= 0 && p.X <= io.DisplaySize.X && p.Y <= io.DisplaySize.Y;
                }
                check(fits, "Actual sleep-symbol font vertices remain onscreen at each viewport corner.");
            }
            ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, "sleep-symbol-edges.png"));
        }
        finally { ImGui.DestroyContext(context); }
    }

    private static CompanionHomeRoutine Sample(CompanionLayout layout, CompanionHomeRect home, bool mirrored,
        CompanionHomeState state, float stateTime)
    {
        var routine = new CompanionHomeRoutine(17); routine.Reset(15);
        var obstacle = new CompanionSurface(1, home.Minimum, home.Maximum, 0, 0);
        var initial = CompanionObstacle.Destination(CompanionHomeGeometry.Outside(layout, home, mirrored), layout.SpriteSize,
            layout.Minimum, layout.Maximum, obstacle);
        for (var tick = 0; tick < 2000; tick++)
        {
            routine.Update(.05f, true, false, initial, mirrored, layout, home, mirrored, 15, 5, layout.Minimum, layout.Maximum);
            if (routine.State == state && routine.SleepPhase + .0001f >= stateTime) return routine;
        }
        throw new InvalidOperationException("Synthetic renderer sample could not reach " + state);
    }
}
