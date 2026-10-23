using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
var output = Path.Combine(root, ".local", "visualharness", "companion-expressions");
Directory.CreateDirectory(output);
var library = new CompanionAnimationLibrary(Path.Combine(root, "assets"));
var variants = new CompanionVariantLibrary(Path.Combine(root, "assets"));
var checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
bool Near(Vector2 a, Vector2 b, float tolerance = .001f) => Vector2.Distance(a, b) <= tolerance;
bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);
CompanionBodyMotion Motion(int rate, float seconds, bool moving = true)
{
    var state = new CompanionBodyMotion();
    for (var n = 0; n < (int)Math.Round(seconds * rate); n++) state.Update(1f / rate, true, moving);
    return state;
}

var models = new List<(string Avatar, string Look, CompanionSpriteAtlas Atlas)>();
foreach (var avatar in new[] { "Ember", "Mochi", "Puff" })
{
    Check(library.TryGet(avatar, "Default", out var atlas) && atlas != null, "Original reviewed atlas is available.");
    models.Add((avatar, "Original", atlas!));
    foreach (var look in variants.Looks.Where(v => v.Avatar == avatar)) models.Add((avatar, look.Choice, look.NormalAtlas));
}
Check(models.Count == 15, "Original plus four reviewed looks load for all three bodies.");
foreach (var (avatar, _, atlas) in models)
foreach (var edge in new[] { 48f, 96f, 192f })
foreach (var mirrored in new[] { false, true })
{
    var size = new Vector2(edge); var position = new Vector2(15, 19);
    var sixty = Motion(60, 2); var fast = Motion(144, 2);
    var equalPacing = true; var bounded = true; var continuous = true; var frozen = true;
    var disabled = true; var folded = false;
    for (var phase = 0; phase <= 64; phase++)
    {
        var stride = phase / 64f;
        var quad = atlas.GetFloat(stride, mirrored, position, size);
        var previousRow = new Vector2[9];
        for (var y = 0; y <= 8; y++)
        {
            var previous = Vector2.Zero;
            for (var x = 0; x <= 8; x++)
            {
                var p = Vector2.Lerp(Vector2.Lerp(quad.A, quad.B, x / 8f), Vector2.Lerp(quad.D, quad.C, x / 8f), y / 8f);
                var q = sixty.Warp(avatar, p, position, size, stride, mirrored, true);
                equalPacing &= Near(q, fast.Warp(avatar, p, position, size, stride, mirrored, true), .01f);
                bounded &= Finite(q) && q.X >= position.X - edge * .08f && q.X <= position.X + edge * 1.08f
                    && q.Y >= position.Y - edge * .08f && q.Y <= position.Y + edge * 1.08f;
                disabled &= sixty.Warp(avatar, p, position, size, stride, mirrored, false) == p;
                if (x > 0) folded |= mirrored ? q.X >= previous.X : q.X <= previous.X;
                if (y > 0) folded |= q.Y <= previousRow[x].Y;
                previous = q; previousRow[x] = q;
                continuous &= Near(q, sixty.Warp(avatar, p, position, size, stride + 1, mirrored, true), .001f);
                sixty.Update(1f / 60, false, false);
                frozen &= q == sixty.Warp(avatar, p, position, size, stride, mirrored, true);
            }
        }
    }
    Check(equalPacing, "The same elapsed time at60/144Hz produces matching production mesh positions.");
    Check(bounded && !folded, "All stride phases remain finite, bounded and free of folded body mesh cells.");
    Check(continuous, "The stride seam is continuous in both directions.");
    Check(frozen && disabled, "Disabled animation is a static original body and does not advance motion state.");
}

var ring60 = new OrbitRingRenderer(); var ring144 = new OrbitRingRenderer();
for (var n = 0; n < 120; n++) ring60.Update(1f / 60, true, 1);
for (var n = 0; n < 288; n++) ring144.Update(1f / 144, true, 1);
var ringPacing = true; var ringContinuous = true; var ringStatic = true;
for (var n = 0; n <= 128; n++)
{
    var angle = n * MathF.Tau / 128;
    var p = ring60.Point(angle, 96);
    ringPacing &= Vector3.Distance(p, ring144.Point(angle, 96)) < .01f;
    ringContinuous &= Vector3.Distance(p, ring60.Point(angle + MathF.Tau, 96)) < .001f;
    ring60.Update(1f / 60, false, 3);
    ringStatic &= p == ring60.Point(angle, 96);
}
Check(ringPacing && ringContinuous && ringStatic, "Ring orientation has continuous angle seams, equivalent pacing and exact disabled state.");

var context = ImGui.CreateContext();
try
{
    var io = ImGui.GetIO(); io.DeltaTime = 1f / 60;
    io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
    unsafe { io.IniFilename = null; io.LogFilename = null; }
    io.Fonts.AddFontDefault(); io.Fonts.Build();
    var renderer = new SoftwareRenderer();
    var textures = new Dictionary<string, ulong>(StringComparer.Ordinal);
    ulong Texture(string file)
    {
        if (textures.TryGetValue(file, out var known)) return known;
        using var bitmap = new Bitmap(Path.Combine(root, "assets", file));
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var bytes = new byte[bitmap.Width * bitmap.Height * 4];
        try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(data.Scan0 + data.Stride * y, bytes, bitmap.Width * 4 * y, bitmap.Width * 4); }
        finally { bitmap.UnlockBits(data); }
        for (var n = 0; n < bytes.Length; n += 4) (bytes[n], bytes[n + 2]) = (bytes[n + 2], bytes[n]);
        var id = (ulong)textures.Count + 2; renderer.AddTexture(id, bytes, bitmap.Width, bitmap.Height); textures.Add(file, id); return id;
    }

    foreach (var edge in new[] { 48f, 96f, 192f })
    foreach (var mirrored in new[] { false, true })
    {
        var tile = edge + 24; var rowHeight = edge + 46;
        io.DisplaySize = new(8 * tile, models.Count * rowHeight);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var row = 0; row < models.Count; row++)
        {
            var model = models[row];
            draw.AddText(new(6, row * rowHeight + 3), 0xFFFFFFFF, model.Avatar + " " + model.Look);
            for (var column = 0; column < 8; column++)
            {
                var position = new Vector2(column * tile + 12, row * rowHeight + 25); var size = new Vector2(edge);
                var state = Motion(60, 2 + column * .25f); var stride = column / 8f;
                var quad = model.Atlas.GetFloat(stride, mirrored, position, size);
                var firstVertex = draw.VtxBuffer.Size; var firstIndex = draw.IdxBuffer.Size;
                var bounds = state.Draw(draw, new ImTextureID(Texture(model.Atlas.File)), model.Avatar, quad, position, size, stride, mirrored, true, 0xFFFFFFFF);
                Check(draw.VtxBuffer.Size - firstVertex == 256 && draw.IdxBuffer.Size - firstIndex == 384,
                    "Actual native draw emits exactly one8x8 connected body mesh, with no stacked duplicate body.");
                var actualMin = new Vector2(float.MaxValue); var actualMax = new Vector2(float.MinValue);
                for (var i = firstVertex; i < draw.VtxBuffer.Size; i++)
                { actualMin = Vector2.Min(actualMin, draw.VtxBuffer[i].Pos); actualMax = Vector2.Max(actualMax, draw.VtxBuffer[i].Pos); }
                Check(actualMin == bounds.Minimum && actualMax == bounds.Maximum, "Reported mesh bounds exactly enclose the native vertices.");
            }
        }
        ImGui.Render();
        var path = Path.Combine(output, $"bodies-{edge:0}-{(mirrored ? "left" : "right")}.png");
        renderer.Save(ImGui.GetDrawData(), path);
        using var pixels = new Bitmap(path);
        for (var row = 0; row < models.Count; row++)
        {
            var outsideInk = 0;
            for (var column = 0; column < 8; column++)
            for (var y = (int)(row * rowHeight + 20); y < (int)((row + 1) * rowHeight); y++)
            for (var x = (int)(column * tile); x < (int)((column + 1) * tile); x++)
            {
                var left = column * tile + 12; var top = row * rowHeight + 25;
                if (x >= left && x < left + edge && y >= top && y < top + edge) continue;
                var pixel = pixels.GetPixel(x, y);
                if (Math.Max(Math.Abs(pixel.R - 24), Math.Max(Math.Abs(pixel.G - 29), Math.Abs(pixel.B - 38))) >= 4) outsideInk++;
            }
            Check(outsideInk == 0, $"Unclipped actual rendered {models[row].Avatar}/{models[row].Look} ink fits its logical cell.");
        }
    }
    var planets = new List<(string Label, CompanionVariantFrame Frame)> { ("Original", variants.OriginalOrbitCore[0]) };
    planets.AddRange(variants.Looks.Where(v => v.Avatar == "Orbit").Select(v => (v.Choice, v.Normal)));
    foreach (var edge in new[] { 48f, 96f, 192f })
    foreach (var mirrored in new[] { false, true })
    {
        var tile = edge + 24; var rowHeight = edge + 46;
        io.DisplaySize = new(8 * tile, planets.Count * rowHeight);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var row = 0; row < planets.Count; row++)
        {
            var model = planets[row];
            draw.AddText(new(6, row * rowHeight + 3), 0xFFFFFFFF, "Orbit " + model.Label);
            for (var column = 0; column < 8; column++)
            {
                var position = new Vector2(column * tile + 12, row * rowHeight + 25); var size = new Vector2(edge);
                var state = new OrbitRingRenderer();
                for (var tick = 0; tick < column * 240; tick++) state.Update(1f / 60, true, 1);
                var placement = OrbitRingRenderer.Place(model.Frame, model.Frame, position, size, mirrored);
                var first = draw.VtxBuffer.Size;
                var back = state.Draw(draw, placement.Center, placement.Radius, new(.65f, .8f, 1, 1), false);
                var firstCore = draw.VtxBuffer.Size;
                var image = placement.Frame;
                draw.AddImage(new ImTextureID(Texture(model.Frame.File)), image.Position, image.Position + image.Size, image.UvStart, image.UvEnd);
                Check(draw.VtxBuffer.Size - firstCore == 4, "Orbit emits exactly one painted core between back and front ring halves.");
                var front = state.Draw(draw, placement.Center, placement.Radius, new(.65f, .8f, 1, 1), true);
                var fits = true; var extents = true; var outsideExtent = 0f;
                var actualMin = new Vector2(float.MaxValue); var actualMax = new Vector2(float.MinValue);
                for (var index = first; index < draw.VtxBuffer.Size; index++)
                {
                    var point = draw.VtxBuffer[index].Pos;
                    actualMin = Vector2.Min(actualMin, point); actualMax = Vector2.Max(actualMax, point);
                    fits &= point.X >= position.X - .001f && point.Y >= position.Y - .001f
                        && point.X <= position.X + edge + .001f && point.Y <= position.Y + edge + .001f;
                    if (index >= firstCore && index < firstCore + 4) continue;
                    var bounds = index < firstCore ? back : front;
                    outsideExtent = Math.Max(outsideExtent, Math.Max(Math.Max(bounds.Minimum.X - point.X, bounds.Minimum.Y - point.Y),
                        Math.Max(point.X - bounds.Maximum.X, point.Y - bounds.Maximum.Y)));
                    extents &= point.X >= bounds.Minimum.X - .001f && point.Y >= bounds.Minimum.Y - .001f
                        && point.X <= bounds.Maximum.X + .001f && point.Y <= bounds.Maximum.Y + .001f;
                }
                Check(fits, $"Actual ring/core vertices including anti-alias fringes fit the logical cell ({model.Label}, {edge}, {column}, relative {actualMin - position}..{actualMax - position}).");
                Check(extents, $"Reported ring extents include its native line and anti-alias geometry ({model.Label}, {edge}, {column}, spill {outsideExtent}).");
            }
        }
        ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"orbit-{edge:0}-{(mirrored ? "left" : "right")}.png"));
    }
}
finally { ImGui.DestroyContext(context); }
foreach (var avatar in new[] { "Ember", "Mochi", "Puff", "Orbit" })
{
    var offsets = variants.Looks.Where(look => look.Avatar == avatar)
        .SelectMany(look => new[] { look[CompanionVariantPose.CarrySquirmA], look[CompanionVariantPose.CarrySquirmB] })
        .Select(frame => Vector2.Distance(frame.Map(frame.Grip, Vector2.Zero, new Vector2(96)), new Vector2(48))).ToArray();
    Console.WriteLine($"{avatar} carry-art grip offset from logical centre at96px: {offsets.Min():0.0}..{offsets.Max():0.0}px.");
}
Console.WriteLine($"Companion expression geometry/native mesh: {checks} checks passed.");
