using System.Numerics;
using System.Security.Cryptography;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

internal static class HolidayRingChecks
{
    public static unsafe void Run()
    {
        var checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        var themes = Enum.GetValues<SeasonalCompanionTheme>();
        foreach (var theme in themes)
            Check(OrbitRingRenderer.ThemeForLook("Holiday" + theme) == theme, "The actual resolved holiday look supplies its exact canonical ring theme.");
        foreach (var value in new string?[] { null, "", "Default", "Look1", "Look4", "Auto", "Starlight", "Holiday", "Holiday1", "Holiday-1", "Holidaystarlight", "Holiday Starlight", "HolidayStarlight ", "HolidayUnknown" })
            Check(OrbitRingRenderer.ThemeForLook(value) == null, "Everyday, pending, malformed and numeric look choices cannot theme rings.");
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.DisplaySize = new Vector2(400); io.DeltaTime = 1f / 60;
            io.IniFilename = null; io.LogFilename = null; io.Fonts.AddFontDefault(); io.Fonts.Build();
            var accent = new Vector4(.65f, .85f, .95f, 1);
            (Vector2 Position, uint Color)[] Sample(OrbitRingRenderer rings, float edge, bool front, SeasonalCompanionTheme? theme, bool decorations)
            {
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                var bounds = rings.Draw(draw, new Vector2(150), edge * .3125f, accent, front, theme, decorations);
                var data = Enumerable.Range(0, draw.VtxBuffer.Size).Select(i => (draw.VtxBuffer[i].Pos, draw.VtxBuffer[i].Col)).ToArray();
                Check(data.Length is > 0 and < 6000, "Every ring pass has bounded native geometry.");
                Check(data.All(v => float.IsFinite(v.Pos.X) && float.IsFinite(v.Pos.Y) && v.Pos.X >= bounds.Minimum.X &&
                    v.Pos.Y >= bounds.Minimum.Y && v.Pos.X <= bounds.Maximum.X && v.Pos.Y <= bounds.Maximum.Y), "Reported bounds contain every actual native vertex.");
                ImGui.Render(); return data;
            }
            string Fingerprint(ImDrawListPtr draw)
            {
                using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes);
                for (var n = 0; n < draw.VtxBuffer.Size; n++)
                { var vertex = draw.VtxBuffer[n]; writer.Write(MathF.Round(vertex.Pos.X, 4)); writer.Write(MathF.Round(vertex.Pos.Y, 4)); writer.Write(vertex.Col); }
                for (var n = 0; n < draw.IdxBuffer.Size; n++) writer.Write(draw.IdxBuffer[n]);
                return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
            }
            // Captured from the unmodified production renderer with the same native
            // fixture before adding themes. Includes colors, geometry and draw indices.
            string[] original = ["6a518479380c862b1bf2a5dea9128d37413dbf54afeefac402f564883be2f476",
                "84fc79bea89117158cf9db12fe676e28760016ecc2ade161318c2369eb680149",
                "dfac5847d4df2031ddfc64144a1a652deec1fb29adec5614e8b06c59db330796",
                "2b393384343afc03a899ac9ff0f41c810ef41a5a7db2467c093b661cb6528c3a"];
            var fingerprint = 0;
            foreach (var elapsed in new[] { 0, 120 })
            foreach (var front in new[] { false, true })
            {
                var rings = new OrbitRingRenderer();
                for (var n = 0; n < elapsed; n++) rings.Update(1f / 60, true, 1);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                rings.Draw(draw, new Vector2(150), 30, accent, front);
                Check(Fingerprint(draw) == original[fingerprint++], "The default ring path retains its pre-change native output exactly.");
                ImGui.Render();
            }
            foreach (var edge in new[] { 96f, 192f })
            foreach (var steps in new[] { 0, 120, 540 })
            foreach (var front in new[] { false, true })
            {
                var rings = new OrbitRingRenderer();
                for (var n = 0; n < steps; n++) rings.Update(1f / 60, true, 1);
                var plain = Sample(rings, edge, front, null, false);
                Check(plain.SequenceEqual(Sample(rings, edge, front, null, true)), "The decoration toggle leaves ordinary rings unchanged.");
                Check(plain.SequenceEqual(Sample(rings, edge, front, (SeasonalCompanionTheme)999, true)), "An invalid enum fails closed to ordinary rings.");
                var palettes = new HashSet<string>(); var motifs = new HashSet<string>();
                var plainMin = plain.Aggregate(new Vector2(float.MaxValue), (p,v) => Vector2.Min(p,v.Position));
                var plainMax = plain.Aggregate(new Vector2(float.MinValue), (p,v) => Vector2.Max(p,v.Position));
                foreach (var theme in themes)
                {
                    var before = rings.Point(.75f, 1);
                    var colored = Sample(rings, edge, front, theme, false);
                    var decorated = Sample(rings, edge, front, theme, true);
                    palettes.Add(string.Join(',', colored.Select(v => v.Color)));
                    Check(colored.Select(v => v.Position).SequenceEqual(plain.Select(v => v.Position)),
                        "Holiday colors never change the original band geometry or clipping footprint.");
                    Check(decorated.Take(colored.Length).SequenceEqual(colored) && decorated.Length > colored.Length,
                        "Magic adds motifs after the same colored band; disabling it keeps the holiday palette.");
                    var marks = decorated.Skip(colored.Length).ToArray();
                    motifs.Add(string.Join(';', marks.Select(v => v.Position.ToString())));
                    Check(marks.All(v => v.Position.X >= plainMin.X - .001f && v.Position.Y >= plainMin.Y - .001f &&
                        v.Position.X <= plainMax.X + .001f && v.Position.Y <= plainMax.Y + .001f),
                        $"{theme}: projected motifs remain within the preexisting native ring bounds.");
                    var x = rings.Point(0, 1); var y = rings.Point(MathF.PI / 2, 1);
                    var determinant = x.X * y.Y - x.Y * y.X;
                    foreach (var vertex in marks)
                    {
                        var p = vertex.Position - new Vector2(150);
                        var localX = (p.X * y.Y - p.Y * y.X) / determinant;
                        var localY = (x.X * p.Y - x.Y * p.X) / determinant;
                        var z = localX * x.Z + localY * y.Z;
                        Check(front ? z >= -.0002f : z <= .0002f, "Every motif vertex belongs to the requested front/back ring-plane depth.");
                        Check(new Vector2(localX, localY).Length() <= edge * .3125f * 1.47f * 1.04f + .001f,
                            "Motifs cannot enlarge the existing outer ring radius.");
                    }
                    rings.Update(1, false, 3);
                    Check(decorated.SequenceEqual(Sample(rings, edge, front, theme, true)) && rings.Point(.75f, 1) == before,
                        "Disabled animation and repeated painting preserve all themed geometry/colors and the existing clock.");
                }
                Check(palettes.Count == 9 && motifs.Count == 9, "All nine holidays have distinct palettes and distinct ring-plane motif geometry.");
            }
            Gallery(io);
            Console.WriteLine($"Holiday Orbit rings: {checks} checks passed; native 96/192 galleries rendered. Public artwork and synthetic state only.");
        }
        finally { ImGui.DestroyContext(context); }
    }

    private static unsafe void Gallery(ImGuiIOPtr io)
    {
        var assets = Path.GetFullPath("assets"); var library = new CompanionHolidayLibrary(assets);
        if (!library.IsAvailable) throw new InvalidOperationException("Reviewed holiday bodies must load for the native ring gallery.");
        var renderer = new SoftwareRenderer(); var textures = new Dictionary<string, ulong>();
        ulong Texture(string file)
        {
            if (textures.TryGetValue(file, out var existing)) return existing;
            using var bitmap = new Bitmap(Path.Combine(assets, file));
            var data = bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = new byte[bitmap.Width * bitmap.Height * 4];
            try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
            finally { bitmap.UnlockBits(data); }
            for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i+2]) = (pixels[i+2], pixels[i]);
            var id = (ulong)textures.Count + 2; textures.Add(file, id); renderer.AddTexture(id, pixels, bitmap.Width, bitmap.Height); return id;
        }
        foreach (var edge in new[] { 96f, 192f })
        {
            var tile = new Vector2(edge * 2 + 36, edge + 48); io.DisplaySize = tile * new Vector2(3,3);
            ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var row = 0;
            foreach (var theme in Enum.GetValues<SeasonalCompanionTheme>())
            {
                var look = library.FindHoliday("Orbit", theme)!; var origin = new Vector2(row % 3, row / 3) * tile;
                draw.AddText(origin + new Vector2(8,6), 0xFFFFFFFF, theme.ToString());
                for (var column = 0; column < 2; column++)
                {
                    var position = origin + new Vector2(8 + column * (edge + 16), 27);
                    var placed = OrbitRingRenderer.Place(look.Normal, look.Normal, position, new Vector2(edge), false);
                    var rings = new OrbitRingRenderer(); for (var n = 0; n < 120; n++) rings.Update(1f / 60, true, 1);
                    rings.Draw(draw, placed.Center, placed.Radius, Vector4.One, false, theme, column == 1);
                    var f = placed.Frame; draw.AddImage(new ImTextureID(Texture(look.Normal.File)), f.Position, f.Position + f.Size, f.UvStart, f.UvEnd);
                    rings.Draw(draw, placed.Center, placed.Radius, Vector4.One, true, theme, column == 1);
                    draw.AddText(position + new Vector2(0,edge + 2), 0xFFA8B2BB, column == 0 ? "Palette" : "+ motifs");
                }
                row++;
            }
            ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.GetFullPath($".local/visualharness/companion-expressions/holiday-rings-{edge:0}.png"));
        }
    }
}
