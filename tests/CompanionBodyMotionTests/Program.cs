using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

var assets = Path.GetFullPath("assets");
var output = Directory.CreateDirectory(Path.GetFullPath(".local/visualharness/body-motion"));
var original = new CompanionAnimationLibrary(assets);
var wardrobe = new CompanionWardrobeLibrary(assets);
var checks = 0;
void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
var supported = new[] { "Ember", "Mochi", "Puff", "Nib", "Echo", "Fizz", "Glim", "Rune", "Wink", "Nova", "Prism" };
var added = supported.Skip(3).ToArray();
Check(supported.All(CompanionBodyMotion.Supports), "All connected body identities opt into their mesh.");
Check(new[] { "Moonblob", "Orbit", "", "Unknown" }.All(a => !CompanionBodyMotion.Supports(a)), "Roll, orbit and unknown paths stay separate.");
Check(wardrobe.IsAvailable, "Reviewed complete wardrobe metadata loads.");
var models = new List<Model>();
foreach (var avatar in supported)
{
    var bare = wardrobe.FindDefault(avatar);
    if (bare != null) models.Add(new(avatar, "Default", bare.NormalAtlas, .1f));
    else
    {
        Check(original.TryGet(avatar, "Default", out var atlas) && atlas != null, "Reviewed original is available: " + avatar);
        models.Add(new(avatar, "Default", atlas!, .1f));
    }
    foreach (var look in wardrobe.Looks.Where(l => l.Avatar == avatar && l.Choice != "Default"))
        models.Add(new(avatar, look.Choice, look.NormalAtlas, .5f));
}
Check(models.Count == 55, "Eleven original identities and all forty-four painted costumes are covered.");
CompanionBodyMotion Motion(int rate, float seconds)
{
    var motion = new CompanionBodyMotion();
    for (var n = 0; n < (int)Math.Round(rate * seconds); n++) motion.Update(1f / rate, true, true);
    return motion;
}
var signatures = new List<Vector2[]>();
foreach (var avatar in added)
{
    var size = new Vector2(96); var sixty = Motion(60, 2); var fast = Motion(144, 2);
    var signature = new List<Vector2>();
    for (var n = 0; n <= 32; n++)
    {
        var phase = n / 32f; var p = new Vector2(32, 31);
        var q = sixty.Warp(avatar, p, Vector2.Zero, size, phase, false, true); signature.Add(q);
        Check(Vector2.Distance(q, fast.Warp(avatar, p, Vector2.Zero, size, phase, false, true)) < .01f, "60/144Hz pacing agrees: " + avatar);
        Check(Vector2.Distance(q, sixty.Warp(avatar, p, Vector2.Zero, size, phase + 1, false, true)) < .001f, "Stride loops without a seam: " + avatar);
        var mirrored = sixty.Warp(avatar, new(size.X - p.X, p.Y), Vector2.Zero, size, phase, true, true);
        Check(Vector2.Distance(mirrored, new(size.X - q.X, q.Y)) < .001f, "Facing preserves the reflected deformation: " + avatar);
        var baseline = size * new Vector2(.5f, .9f);
        Check(sixty.Warp(avatar, baseline, Vector2.Zero, size, phase, false, true) == baseline, "The supporting baseline remains anchored: " + avatar);
        Check(sixty.Warp(avatar, p, Vector2.Zero, size, phase, false, false) == p, "Disabled animation has exactly zero deformation: " + avatar);
    }
    Check(signatures.All(other => other.Zip(signature).Sum(pair => Vector2.Distance(pair.First, pair.Second)) > 5), "Each new identity has a distinct continuous motion.");
    signatures.Add(signature.ToArray());
    var running = new CompanionBodyMotion(); var previous = new Vector2(32, 31); var maxStep = 0f;
    for (var n = 0; n < 1400; n++)
    {
        var dt = n % 3 == 0 ? 1f / 60 : 1f / 144;
        running.Update(dt, true, n is < 350 or > 700);
        var next = running.Warp(avatar, new(32, 31), Vector2.Zero, size, n < 350 ? n / 144f : n < 700 ? 350 / 144f : (n - 350) / 144f, false, true);
        if (n > 0) maxStep = Math.Max(maxStep, Vector2.Distance(next, previous));
        previous = next;
    }
    Check(maxStep < 1.5f, "Variable pacing, clock wrap and stop/start stay continuous: " + avatar);
    var frozen = running.Warp(avatar, new(32, 31), Vector2.Zero, size, .3f, false, true);
    running.Update(.05f, false, true);
    Check(frozen == running.Warp(avatar, new(32, 31), Vector2.Zero, size, .3f, false, true), "Disabled clock does not advance: " + avatar);
}

// Authored local reactions share the body support and attachment deformation.
foreach (var avatar in supported)
{
    var motion = Motion(60, 2); var size = new Vector2(96); var p = new Vector2(32, 31);
    var originalWarp = motion.Warp(avatar, p, Vector2.Zero, size, .3f, false, true);
    motion.SetIntensity(1); motion.SetDynamics(0, 0, 0);
    Check(originalWarp == motion.Warp(avatar, p, Vector2.Zero, size, .3f, false, true), "Default controls preserve exact authored motion: " + avatar);
    motion.SetIntensity(.5f);
    Check(Vector2.Distance(motion.Warp(avatar, p, Vector2.Zero, size, .3f, false, true), p + (originalWarp - p) * .5f) < .00001f,
        "Strength applies to the complete final warp: " + avatar);
    motion.SetIntensity(1); motion.SetDynamics(.04f, .03f, .025f);
    var reaction = motion.Warp(avatar, p, Vector2.Zero, size, .3f, false, true);
    var mirrored = motion.Warp(avatar, new(size.X - p.X, p.Y), Vector2.Zero, size, .3f, true, true);
    if (added.Contains(avatar)) Check(Vector2.Distance(mirrored, new(size.X - reaction.X, reaction.Y)) < .001f, "Reaction facing stays mirrored: " + avatar);
    motion.SetDynamics(99, 99, 99);
    Check(reaction == motion.Warp(avatar, p, Vector2.Zero, size, .3f, false, true), "Reaction inputs are bounded: " + avatar);
    Check(motion.Warp(avatar, p, Vector2.Zero, size, .3f, false, false) == p, "Animation off bypasses reactions: " + avatar);
    motion.SetIntensity(0);
    Check(motion.Warp(avatar, p, Vector2.Zero, size, .3f, false, true) == p, "Zero body strength is exactly neutral: " + avatar);
    motion.SetIntensity(float.NaN); motion.SetDynamics(float.NaN, float.PositiveInfinity, float.NegativeInfinity);
    Check(originalWarp == motion.Warp(avatar, p, Vector2.Zero, size, .3f, false, true), "Non-finite controls return to safe defaults: " + avatar);
}
{
    var orbit = Motion(60, 2); var center = new Vector2(100); var radius = 48f;
    var baseline = orbit.OrbitMovement(center, radius, .3f, false, true);
    orbit.SetIntensity(1); orbit.SetDynamics(0, 0, 0);
    Check(baseline == orbit.OrbitMovement(center, radius, .3f, false, true), "Neutral Orbit is exactly the existing rigid travel.");
    orbit.SetDynamics(.04f, .03f, .025f);
    var reacted = orbit.OrbitMovement(center, radius, .3f, false, true);
    Check(Math.Abs(reacted.GetDeterminant() - 1) < .00001f, "Orbit reactions preserve rigid scale for body, hat and rings.");
    orbit.SetIntensity(0);
    Check(orbit.OrbitMovement(center, radius, .3f, false, true) == Matrix3x2.Identity, "Zero intensity makes Orbit exactly neutral.");
}

var context = ImGui.CreateContext();
try
{
    var io = ImGui.GetIO(); io.DisplaySize = new(600, 600); io.DeltaTime = 1f / 60;
    unsafe { io.IniFilename = null; io.LogFilename = null; }
    io.Fonts.AddFontDefault(); io.Fonts.Build();
    var painter = new SoftwareRenderer(); var textures = new Dictionary<string, ulong>();
    ulong Texture(string file)
    {
        if (textures.TryGetValue(file, out var known)) return known;
        using var bitmap = new Bitmap(Path.Combine(assets, file));
        var bits = bitmap.LockBits(new(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
        finally { bitmap.UnlockBits(bits); }
        for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        var id = (ulong)textures.Count + 2; textures.Add(file, id); painter.AddTexture(id, pixels, bitmap.Width, bitmap.Height); return id;
    }
    float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    foreach (var model in models)
    foreach (var edge in new[] { 96f, 192f })
    foreach (var mirrored in new[] { false, true })
    {
        var motion = Motion(144, 2); var size = new Vector2(edge); var at = new Vector2(180);
        var strongest = 0f; var largestOffset = 0f;
        for (var n = 0; n < 8; n++)
        {
            var phase = n / 8f; var quad = model.Atlas.GetFloat(phase, mirrored, at, size);
            ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
            var bounds = motion.DrawPadded(draw, new ImTextureID(2UL), model.Avatar, quad, at, size, phase, mirrored, true, 0xFFFFFFFF, model.Padding);
            strongest = Math.Max(strongest, motion.LastDrawStrength);
            Check(draw.VtxBuffer.Size == 256 && draw.IdxBuffer.Size == 384, "One connected eight-by-eight mesh, no duplicated body.");
            var limitMin = at - size * model.Padding; var limitMax = at + size * (1 + model.Padding);
            var bounded = true; var coherent = true; var unfurled = true; var area = 0f;
            for (var y = 0; y < 8; y++) for (var x = 0; x < 8; x++)
            {
                var i = (y * 8 + x) * 4;
                var a = draw.VtxBuffer[i]; var b = draw.VtxBuffer[i + 1]; var c = draw.VtxBuffer[i + 2]; var d = draw.VtxBuffer[i + 3];
                var sign = MathF.Sign(Cross(quad.B - quad.A, quad.D - quad.A));
                var first = Cross(b.Pos - a.Pos, c.Pos - a.Pos); var second = Cross(c.Pos - a.Pos, d.Pos - a.Pos);
                unfurled &= first * sign > 0 && second * sign > 0; area += Math.Abs(first + second) / 2;
                if (x < 7) coherent &= b.Pos == draw.VtxBuffer[i + 4].Pos && c.Pos == draw.VtxBuffer[i + 7].Pos;
                if (y < 7) coherent &= d.Pos == draw.VtxBuffer[i + 32].Pos && c.Pos == draw.VtxBuffer[i + 33].Pos;
                for (var corner = 0; corner < 4; corner++)
                {
                    var p = draw.VtxBuffer[i + corner].Pos;
                    bounded &= float.IsFinite(p.X) && float.IsFinite(p.Y) && p.X >= limitMin.X - .002f && p.Y >= limitMin.Y - .002f
                        && p.X <= limitMax.X + .002f && p.Y <= limitMax.Y + .002f
                        && p.X >= bounds.Minimum.X - .002f && p.Y >= bounds.Minimum.Y - .002f
                        && p.X <= bounds.Maximum.X + .002f && p.Y <= bounds.Maximum.Y + .002f;
                }
                var before = Vector2.Lerp(Vector2.Lerp(quad.A, quad.B, x / 8f), Vector2.Lerp(quad.D, quad.C, x / 8f), y / 8f);
                largestOffset = Math.Max(largestOffset, Vector2.Distance(a.Pos, before));
                Check(Vector2.Distance(a.Pos, motion.WarpAttachment(model.Avatar, before, at, size, phase, mirrored, true)) < .002f,
                    "Eyes and bows share the actual bounded body warp.");
            }
            Check(bounded, "Actual full crop fits its reserved bounds: " + model.Avatar + "/" + model.Choice);
            Check(coherent && unfurled, "Shared mesh edges stay joined and triangles do not fold.");
            var originalArea = Math.Abs(Cross(quad.B - quad.A, quad.D - quad.A));
            if (added.Contains(model.Avatar)) Check(Math.Abs(area / originalArea - 1) < .015f, "Soft deformation preserves body mass within1.5%.");
            ImGui.Render();
        }
        Check(strongest > .2f && largestOffset > edge * .002f, "The full painted crop must not silently disable motion: " + model.Avatar + "/" + model.Choice);
        var frozenQuad = model.Atlas.GetFloat(.25f, mirrored, at, size);
        ImGui.NewFrame(); var frozenDraw = ImGui.GetForegroundDrawList();
        motion.DrawPadded(frozenDraw, new ImTextureID(2UL), model.Avatar, frozenQuad, at, size, .25f, mirrored, false, 0xFFFFFFFF, model.Padding);
        var unmodified = true;
        for (var y = 0; y < 8; y++) for (var x = 0; x < 8; x++)
        {
            var u = x / 8f; var v = y / 8f;
            var p = Vector2.Lerp(Vector2.Lerp(frozenQuad.A, frozenQuad.B, u), Vector2.Lerp(frozenQuad.D, frozenQuad.C, u), v);
            var uv = Vector2.Lerp(Vector2.Lerp(frozenQuad.UvA, frozenQuad.UvB, u), Vector2.Lerp(frozenQuad.UvD, frozenQuad.UvC, u), v);
            var actual = frozenDraw.VtxBuffer[(y * 8 + x) * 4];
            unmodified &= actual.Pos == p && actual.Uv == uv;
        }
        Check(unmodified, "Disabled native drawing preserves body positions and source UVs exactly.");
        ImGui.Render();
    }
    foreach (var edge in new[] { 96f, 192f })
    foreach (var costume in new[] { false, true })
    {
        var tile = new Vector2(edge * 2 + 20, edge * 2 + 34);
        io.DisplaySize = tile * new Vector2(5, supported.Length);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var row = 0; row < supported.Length; row++)
        {
            var candidates = models.Where(m => m.Avatar == supported[row] && (m.Choice != "Default") == costume).ToArray();
            var model = candidates[costume ? row % candidates.Length : 0];
            for (var column = 0; column < 5; column++)
            {
                var at = tile * new Vector2(column, row) + new Vector2(edge * .5f + 10, edge * .5f + 24);
                var phase = column == 0 ? 0 : (column - 1) / 4f;
                var motion = Motion(144, 2); var size = new Vector2(edge);
                motion.DrawPadded(draw, new ImTextureID(Texture(model.Atlas.File)), model.Avatar,
                    model.Atlas.GetFloat(phase, false, at, size), at, size, phase, false, column != 0, 0xFFFFFFFF, model.Padding);
                draw.AddText(tile * new Vector2(column, row) + new Vector2(8, 5), 0xFFFFFFFF,
                    column == 0 ? model.Avatar + " / still" : "phase " + phase.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"{(costume ? "costumes" : "originals")}-{edge:0}.png"));
    }
}
finally { ImGui.DestroyContext(context); }
Console.WriteLine($"Connected body motion: {checks} focused checks passed; 55 public bodies, native96/192 galleries; no game or network calls.");
internal sealed record Model(string Avatar, string Choice, CompanionSpriteAtlas Atlas, float Padding);
