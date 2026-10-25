using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

var assets = Path.GetFullPath("assets"); var output = Directory.CreateDirectory(Path.GetFullPath(".local/companion-blink-tests"));
var library = new CompanionBlinkLibrary(assets); var originals = new CompanionAnimationLibrary(assets);
var variants = new CompanionVariantLibrary(assets); var holidays = new CompanionHolidayLibrary(assets);
string[] avatars = ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism"];
var filterIndex = Array.IndexOf(args, "--avatars");
var renderAvatars = filterIndex >= 0 ? args[filterIndex + 1].Split(',') : avatars;
if (renderAvatars.Any(a => !avatars.Contains(a))) throw new ArgumentException("Unknown render avatar.");
var checks = 0;
void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
Check(library.Count == 112 && library.Profiles.Sum(p => p.Eyes.Count) == 210, "All112 bodies/210 measured eyes match current PNG hashes and active crop pairs.");
var definitions = new List<Definition>();
foreach (var avatar in avatars)
{
    CompanionSpriteAtlas atlas;
    if (avatar == "Orbit") atlas = variants.OriginalOrbitCore[0].AsAtlas();
    else { Check(originals.TryGet(avatar, "Default", out var value) && value != null, "Original atlas exists."); atlas = value!; }
    Add(avatar, "Default", atlas);
}
foreach (var look in variants.Looks.Concat(holidays.Looks)) Add(look.Avatar, look.Choice, look.NormalAtlas);
Check(definitions.Count == 112, "Normal body coverage includes8 originals,32 ordinary,72 holiday looks.");
void Add(string avatar, string label, CompanionSpriteAtlas atlas)
{
    var profile = library.Find(atlas.File, atlas.Frames[0].Rect);
    Check(profile != null && profile.Avatar == avatar, "Exact source crop resolves: " + avatar + "/" + label);
    definitions.Add(new(avatar, label, atlas, profile!));
}
Check(library.Find("wrong.png", definitions[0].Profile.NormalBounds) == null, "Filename identity is required.");
Check(library.Find(definitions[0].Profile.NormalFile, definitions[0].Profile.NormalBounds + Vector4.One) == null, "Exact crop identity is required.");

if (!args.Contains("--render-only"))
{
    var fixture = Directory.CreateDirectory(Path.Combine(output.FullName, "invalid-fixtures"));
    foreach (var file in library.Profiles.SelectMany(p => new[] { p.NormalFile, p.ClosedFile }).Distinct()
        .Concat(new[] { "abstract-companions.frames.json", "companion-variants.frames.json", "companion-holiday-bodies.frames.json" }))
        File.Copy(Path.Combine(assets, file), Path.Combine(fixture.FullName, file), true);
    var originalText = File.ReadAllText(Path.Combine(assets, CompanionBlinkLibrary.MetadataFile));
    var path = Path.Combine(fixture.FullName, CompanionBlinkLibrary.MetadataFile);
    foreach (var fault in new[] { "future", "truncated", "duplicate", "path", "wrong-normal", "wrong-closed", "wrong-eye-count", "outside-eye", "outside-sample", "wrong-hash", "null-hash" })
    {
        var data = JsonNode.Parse(originalText)!; var profile = data["Profiles"]![0]!;
        switch (fault)
        {
            case "future": data["Version"] = 2; break;
            case "duplicate": data["Profiles"]![1] = profile.DeepClone(); break;
            case "path": data["Sheets"]![0]!["File"] = "../outside.png"; break;
            case "wrong-normal": profile["NormalBounds"]![0] = profile["NormalBounds"]![0]!.GetValue<float>() + 1; break;
            case "wrong-closed": profile["ClosedBounds"]![0] = profile["ClosedBounds"]![0]!.GetValue<float>() + 1; break;
            case "wrong-eye-count": profile["Eyes"]!.AsArray().RemoveAt(0); break;
            case "outside-eye": profile["Eyes"]![0]!["Ellipse"]![0] = -100; break;
            case "outside-sample": profile["Eyes"]![0]!["Sample"]![0] = -1; break;
            case "wrong-hash": data["Sheets"]![0]!["SHA256"] = new string('0', 64); break;
            case "null-hash": data["Sheets"]![0]!["SHA256"] = null; break;
        }
        File.WriteAllText(path, fault == "truncated" ? "{" : data.ToJsonString());
        var rejected = new CompanionBlinkLibrary(fixture.FullName);
        Check(rejected.Count < 112, "Invalid metadata cannot drive all profiles: " + fault);
        if (fault is "wrong-normal" or "wrong-closed" or "wrong-eye-count" or "outside-eye" or "outside-sample")
            Check(rejected.Count == 111 && rejected.Find(definitions[0].Profile.NormalFile, definitions[0].Profile.NormalBounds) == null, "Only the malformed optional profile is disabled.");
        if (fault is "future" or "truncated" or "duplicate" or "path") Check(!rejected.IsAvailable, "Invalid document fails closed.");
    }
}

var context = ImGui.CreateContext();
try
{
    var io = ImGui.GetIO(); unsafe { io.IniFilename = null; io.LogFilename = null; }
    io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset; io.DeltaTime = 1f / 60;
    io.Fonts.AddFontDefault(); io.Fonts.Build(); var painter = new SoftwareRenderer(); var textures = new Dictionary<string, ulong>();
    foreach (var file in library.Profiles.SelectMany(p => new[] { p.NormalFile, p.ClosedFile }).Distinct())
    {
        using var bitmap = new Bitmap(Path.Combine(assets, file));
        var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
        finally { bitmap.UnlockBits(bits); }
        for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        var id = (ulong)textures.Count + 2; painter.AddTexture(id, pixels, bitmap.Width, bitmap.Height); textures.Add(file, id);
    }
    var motion = new CompanionBodyMotion(); for (var step = 0; step < 40; step++) motion.Update(.05f, true, true);
    (SeasonalBodyTransform Transform, Func<Vector2, Vector2>? Deform) Body(ImDrawListPtr draw, Definition definition, Vector2 position, Vector2 size, bool mirrored, float phase)
    {
        var atlas = definition.Atlas; var texture = new ImTextureID(textures[atlas.File]);
        if (definition.Avatar == "Moonblob")
        {
            var quad = atlas.GetRoll(phase, mirrored, position, size);
            draw.AddImageQuad(texture, quad.A, quad.B, quad.C, quad.D, quad.UvA, quad.UvB, quad.UvC, quad.UvD);
            return (new(atlas.Frames[0].Rect, atlas.SourceScale, quad.A, quad.B, quad.D), null);
        }
        var floating = atlas.GetFloat(phase, mirrored, position, size);
        var transform = new SeasonalBodyTransform(atlas.Frames[0].Rect, atlas.SourceScale, floating.A, floating.B, floating.D);
        if (definition.Avatar is "Ember" or "Mochi" or "Puff")
        {
            motion.Draw(draw, texture, definition.Avatar, floating, position, size, phase, mirrored, true, 0xFFFFFFFF);
            return (transform, p => motion.WarpAttachment(definition.Avatar, p, position, size, phase, mirrored, true));
        }
        draw.AddImageQuad(texture, floating.A, floating.B, floating.C, floating.D, floating.UvA, floating.UvB, floating.UvC, floating.UvD);
        return (transform, null);
    }
    void DrawCase(ImDrawListPtr draw, Definition definition, Vector2 position, Vector2 size, bool mirrored, float phase, float closure)
    {
        var bodyStart = draw.VtxBuffer.Size;
        var body = Body(draw, definition, position, size, mirrored, phase);
        var first = draw.VtxBuffer.Size;
        var original = Enumerable.Range(bodyStart, first - bodyStart).Select(i => draw.VtxBuffer[i]).ToArray();
        CompanionBlinkRenderer.Draw(draw, new(textures[definition.Profile.ClosedFile]), definition.Profile, body.Transform, closure, 0xFFFFFFFF, body.Deform);
        Check(original.Length == (definition.Avatar is "Ember" or "Mochi" or "Puff" ? 256 : 4), "One connected normal body quad/mesh is drawn.");
        Check(original.Select((v, i) => v.Pos == draw.VtxBuffer[bodyStart + i].Pos && v.Uv == draw.VtxBuffer[bodyStart + i].Uv
            && v.Col == draw.VtxBuffer[bodyStart + i].Col).All(v => v), "Eyelids never alter or duplicate normal body geometry.");
        Check(closure <= .001f ? draw.VtxBuffer.Size == first : draw.VtxBuffer.Size > first, "Open/toggle-disabled draws no eyelid; positive closure draws eyes only.");
        Check(Enumerable.Range(first, draw.VtxBuffer.Size - first).All(i => {
            var p = draw.VtxBuffer[i].Pos;
            return float.IsFinite(p.X) && float.IsFinite(p.Y) && p.X >= position.X - 1 && p.Y >= position.Y - 1
                && p.X <= position.X + size.X + 1 && p.Y <= position.Y + size.Y + 1;
        }), "Moving/mirrored lid geometry stays inside the existing body cell.");
        var ownSamples = true; var sampled = 0;
        for (var commandIndex = 0; commandIndex < draw.CmdBuffer.Size; commandIndex++)
        {
            var command = draw.CmdBuffer[commandIndex];
            if (command.TextureId.Handle != textures[definition.Profile.ClosedFile]) continue;
            for (var index = command.IdxOffset; index < command.IdxOffset + command.ElemCount; index++)
            {
                var vertex = (int)(command.VtxOffset + draw.IdxBuffer[(int)index]);
                if (vertex < first) continue;
                sampled++; var source = draw.VtxBuffer[vertex].Uv * definition.Profile.ClosedSheetSize;
                ownSamples &= definition.Profile.Eyes.Any(e => source.X >= e.Sample.X - .01f && source.Y >= e.Sample.Y - .01f
                    && source.X <= e.Sample.Z + .01f && source.Y <= e.Sample.W + .01f);
            }
        }
        Check(ownSamples && (closure <= .001f ? sampled == 0 : sampled > 0), "Every textured eyelid samples only its own reviewed closed-frame skin patches.");
        foreach (var eye in definition.Profile.Eyes)
        {
            var sourceCenter = new Vector2(eye.Ellipse.X, eye.Ellipse.Y);
            var actual = body.Transform.Map(sourceCenter); if (body.Deform != null) actual = body.Deform(actual);
            Check(float.IsFinite(actual.X) && float.IsFinite(actual.Y), "Each eye uses the same roll/float/mesh map as its body.");
        }
    }
    foreach (var avatar in renderAvatars) foreach (var size in new[] { 96f, 192f }) foreach (var mirrored in new[] { false, true })
    {
        var selected = definitions.Where(d => d.Avatar == avatar).ToArray();
        var tile = new Vector2(size + 18, size + 30); io.DisplaySize = new(tile.X * 5, tile.Y * selected.Length);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var row = 0; row < selected.Length; row++)
        {
            draw.AddText(new(4, row * tile.Y + 3), 0xFFFFFFFF, selected[row].Avatar + " " + selected[row].Label);
            for (var column = 0; column < 5; column++)
                DrawCase(draw, selected[row], new(column * tile.X + 8, row * tile.Y + 23), new(size), mirrored, avatar == "Moonblob" ? 0 : .23f, column / 4f);
        }
        ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"{avatar.ToLowerInvariant()}-{size:0}-{(mirrored ? "left" : "right")}.png"));
    }
    // A full-turn rolling face, plus deformed movers, at distinct motion phases.
    foreach (var size in new[] { 96f, 192f }) foreach (var mirrored in new[] { false, true })
    {
        var tile = new Vector2(size + 18, size + 30); io.DisplaySize = new(tile.X * 4, tile.Y * 8);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var row = 0; row < avatars.Length; row++) for (var column = 0; column < 4; column++)
        {
            var definition = definitions.First(d => d.Avatar == avatars[row]);
            draw.AddText(new(column * tile.X + 3, row * tile.Y + 3), 0xFFFFFFFF, definition.Avatar + " phase" + column / 4f);
            DrawCase(draw, definition, new(column * tile.X + 8, row * tile.Y + 23), new(size), mirrored, column / 4f, .85f);
        }
        ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"moving-{size:0}-{(mirrored ? "left" : "right")}.png"));
    }
    io.DisplaySize = new(300); ImGui.NewFrame(); var disabledDraw = ImGui.GetForegroundDrawList();
    var example = definitions[0]; var map = Body(disabledDraw, example, new(20), new(96), false, .25f); var before = disabledDraw.VtxBuffer.Size;
    CompanionBlinkRenderer.Draw(disabledDraw, new(textures[example.Profile.ClosedFile]), example.Profile, map.Transform, float.NaN, 0xFFFFFFFF);
    CompanionBlinkRenderer.Draw(disabledDraw, new(textures[example.Profile.ClosedFile]), example.Profile, map.Transform with { SourceRect = Vector4.Zero }, 1, 0xFFFFFFFF);
    Check(disabledDraw.VtxBuffer.Size == before, "Invalid closure or mismatched normal crop paints nothing.");
    ImGui.Render();
}
finally { ImGui.DestroyContext(context); }
Console.WriteLine($"Companion blinks: {checks} checks passed; all112 normal bodies/210 eyes loaded, native96/192 for {string.Join(',', renderAvatars)} with moving transforms and both facings. No game/network/profile access.");

internal sealed record Definition(string Avatar, string Label, CompanionSpriteAtlas Atlas, CompanionBlinkProfile Profile);
