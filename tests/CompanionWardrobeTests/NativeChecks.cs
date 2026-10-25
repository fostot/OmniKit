using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

internal static class NativeChecks
{
    public static int Run(string assets, CompanionWardrobeLibrary library, string[]? selectedAvatars = null)
    {
        var checks = 0;
        void Check(bool condition, string reason) { checks++; if (!condition) throw new InvalidOperationException(reason); }
        var output = Directory.CreateDirectory(Path.GetFullPath(".local/companion-wardrobe-tests/native"));
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); unsafe { io.IniFilename = null; io.LogFilename = null; }
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset; io.DeltaTime = 1f / 60;
            io.Fonts.AddFontDefault(); io.Fonts.Build();
            var painter = new SoftwareRenderer(); var textures = new Dictionary<string, ulong>();
            foreach (var file in library.Files)
            {
                using var bitmap = new Bitmap(Path.Combine(assets, file));
                var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var pixels = new byte[bitmap.Width * bitmap.Height * 4];
                try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
                finally { bitmap.UnlockBits(bits); }
                for (var p = 0; p < pixels.Length; p += 4) (pixels[p], pixels[p + 2]) = (pixels[p + 2], pixels[p]);
                var id = (ulong)textures.Count + 2; painter.AddTexture(id, pixels, bitmap.Width, bitmap.Height); textures.Add(file, id);
            }
            foreach (var avatar in library.Looks.Select(look => look.Avatar).Distinct().Where(avatar => selectedAvatars == null || selectedAvatars.Contains(avatar)))
            foreach (var edge in new[] { 96f, 192f })
            foreach (var mirrored in new[] { false, true })
            {
                var looks = library.Looks.Where(look => look.Avatar == avatar).ToArray();
                var tile = new Vector2(edge * 2 + 12, edge * 2 + 28);
                Vector2 Position(int row, int column) => new(column * tile.X + edge * .5f + 6, row * tile.Y + edge * .5f + 24);
                io.DisplaySize = new(tile.X * 8, tile.Y * looks.Length);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                for (var row = 0; row < looks.Length; row++)
                {
                    draw.AddText(new(5, row * tile.Y + 3), 0xFFFFFFFF, looks[row].Avatar + " " + looks[row].Choice);
                    for (var column = 0; column < 8; column++)
                    {
                        var position = Position(row, column); var frame = CompanionVariantRenderer.Frame(looks[row], (CompanionVariantPose)column, position, new(edge), mirrored);
                        var first = draw.VtxBuffer.Size;
                        CompanionVariantRenderer.Draw(draw, new(textures[looks[row].Normal.File]), frame);
                        Check(draw.VtxBuffer.Size == first + 4, "Each complete painted pose is exactly one body quad.");
                        Check(Enumerable.Range(first, 4).All(i => InTile(draw.VtxBuffer[i].Pos, row, column, tile)), "Full painted brims remain inside explicitly padded gallery tiles.");
                    }
                }
                ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"{avatar.ToLowerInvariant()}-poses-{edge:0}-{(mirrored ? "left" : "right")}.png"));

                io.DisplaySize = new(tile.X * 3, tile.Y * looks.Length);
                ImGui.NewFrame(); draw = ImGui.GetForegroundDrawList();
                for (var row = 0; row < looks.Length; row++)
                {
                    var look = looks[row]; var profile = library.FindBlink(look.Normal.File, look.Normal.Bounds)!;
                    draw.AddText(new(5, row * tile.Y + 3), 0xFFFFFFFF, look.Avatar + " " + look.Choice);
                    for (var column = 0; column < 3; column++)
                    {
                        var quad = look.NormalAtlas.GetFloat(.17f, mirrored, Position(row, column), new(edge));
                        var first = draw.VtxBuffer.Size;
                        draw.AddImageQuad(new(textures[look.Normal.File]), quad.A, quad.B, quad.C, quad.D, quad.UvA, quad.UvB, quad.UvC, quad.UvD);
                        var vertices = Enumerable.Range(first, 4).Select(i => draw.VtxBuffer[i]).ToArray();
                        var lidStart = draw.VtxBuffer.Size;
                        var transform = new SeasonalBodyTransform(look.Normal.Bounds, look.ScaleReference, quad.A, quad.B, quad.D);
                        CompanionBlinkRenderer.Draw(draw, new(textures[profile.ClosedFile]), profile, transform, column * .5f, 0xFFFFFFFF);
                        Check(vertices.Select((v, i) => v.Pos == draw.VtxBuffer[first + i].Pos && v.Uv == draw.VtxBuffer[first + i].Uv && v.Col == draw.VtxBuffer[first + i].Col).All(v => v), "Blinking preserves the single painted normal body exactly.");
                        Check(column == 0 ? draw.VtxBuffer.Size == lidStart : draw.VtxBuffer.Size > lidStart, "Open lids add no geometry; partial and full closure add only eye geometry.");
                        Check(Enumerable.Range(lidStart, draw.VtxBuffer.Size - lidStart).All(i => InTile(draw.VtxBuffer[i].Pos, row, column, tile)), "Eye geometry stays inside its padded body tile in both facings.");
                        var sampled = 0; var ownPalette = true;
                        for (var c = 0; c < draw.CmdBuffer.Size; c++)
                        {
                            var command = draw.CmdBuffer[c]; if (command.TextureId.Handle != textures[profile.ClosedFile]) continue;
                            for (var index = command.IdxOffset; index < command.IdxOffset + command.ElemCount; index++)
                            {
                                var vertex = (int)(command.VtxOffset + draw.IdxBuffer[(int)index]); if (vertex < lidStart) continue;
                                sampled++; var uv = draw.VtxBuffer[vertex].Uv * profile.ClosedSheetSize;
                                ownPalette &= profile.Eyes.Any(eye => uv.X >= eye.Sample.X - .01f && uv.Y >= eye.Sample.Y - .01f && uv.X <= eye.Sample.Z + .01f && uv.Y <= eye.Sample.W + .01f);
                            }
                        }
                        Check(ownPalette && (column == 0 ? sampled == 0 : sampled > 0), "Every painted-body lid samples only its own ClosedBlink skin patches.");
                        if (column == 2)
                            Check(Enumerable.Range(lidStart, draw.VtxBuffer.Size - lidStart).Any(i => draw.VtxBuffer[i].Col ==
                                (profile.Avatar == "Nib" ? 0xFFF3E8E2u : 0xFF291B27u)),
                                "Nib's closed arcs have visible silver contrast; every existing avatar retains the original ink stroke.");
                    }
                }
                ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"{avatar.ToLowerInvariant()}-blink-{edge:0}-{(mirrored ? "left" : "right")}.png"));
            }
        }
        finally { ImGui.DestroyContext(context); }
        return checks;
    }

    private static bool InTile(Vector2 p, int row, int column, Vector2 tile) => float.IsFinite(p.X) && float.IsFinite(p.Y)
        && p.X >= column * tile.X && p.X <= (column + 1) * tile.X && p.Y >= row * tile.Y && p.Y <= (row + 1) * tile.Y;
}
