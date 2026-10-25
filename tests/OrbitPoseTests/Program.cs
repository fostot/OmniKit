using System.Collections;
using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using LauncherUiSmoke;

var plugin = Path.GetFullPath("src/OmniKit/bin/Release/OmniKit.dll");
var host = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
AssemblyLoadContext.Default.Resolving += (context, name) => {
    foreach (var dir in new[] { Path.GetDirectoryName(plugin)!, host }) { var path = Path.Combine(dir, name.Name + ".dll"); if (File.Exists(path)) return context.LoadFromAssemblyPath(path); }
    return null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(plugin);
const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
Type Type(string name) => assembly.GetType("OmniKit." + name, true)!;
object New(string name, params object?[] values) => Activator.CreateInstance(Type(name), Instance, null, values, null)!;
object Get(object value, string name) => value.GetType().GetProperty(name, Instance)!.GetValue(value)!;
void Set(object value, string name, object? data) => (value.GetType().GetField(name, Instance) ?? value.GetType().GetField("<" + name + ">P", Instance)
    ?? value.GetType().GetField("<" + name + ">k__BackingField", Instance))!.SetValue(value, data);
object? Call(object value, string name, params object?[] values) => value.GetType().GetMethod(name, Instance)!.Invoke(value, values);
object? CallStatic(string type, string name, params object?[] values) => Type(type).GetMethod(name, Static)!.Invoke(null, values);
object EnumValue(string name, string value) => Enum.Parse(Type(name), value);
object[] List(object value, string name) => ((IEnumerable)Get(value, name)).Cast<object>().ToArray();
var checks = 0;
void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
var assets = Path.GetFullPath("assets"); var output = Directory.CreateDirectory(Path.GetFullPath(".local/orbit-pose-tests"));
var variants = New("Ui.CompanionVariantLibrary", assets); var holidays = New("Ui.CompanionHolidayLibrary", assets); var wardrobe = New("Ui.CompanionWardrobeLibrary", assets);
var looks = List(variants, "Looks").Concat(List(holidays, "Looks")).Concat(List(wardrobe, "Looks")).Where(look => (string)Get(look, "Avatar") == "Orbit").ToArray();
var originals = List(variants, "OriginalOrbitCore");
Check(originals.Length == 16 && looks.Length == 18, "The actual DLL loads original16, daily4, holiday9 and wardrobe5 Orbit bodies.");
var frames = originals.Select((frame, index) => new Source(frame, originals[0], "Original " + index, null)).ToList();
foreach (var look in looks) frames.AddRange(List(look, "Frames").Select((frame, index) => new Source(frame, Get(look, "Normal"), (string)Get(look, "Choice") + " " + index, look)));
Check(frames.Count == 160, "All160 source poses are covered without substituting source art.");
Vector2 BodyCenter(object source, object frame)
{
    var body = (Vector4)Get(source, "Body"); var uv = new Vector2((body.X + body.Z) * .5f, (body.Y + body.W) * .5f) / (Vector2)Get(source, "SheetSize");
    var start = (Vector2)Get(frame, "UvStart"); var end = (Vector2)Get(frame, "UvEnd");
    return (Vector2)Get(frame, "Position") + (Vector2)Get(frame, "Size") * ((uv - start) / (end - start));
}
foreach (var edge in new[] { 96f, 192f }) foreach (var mirror in new[] { false, true }) foreach (var source in frames)
{
    var placed = CallStatic("Ui.OrbitRingRenderer", "Place", source.Frame, source.Normal, new Vector2(123, 87), new Vector2(edge), mirror)!;
    var normal = CallStatic("Ui.OrbitRingRenderer", "Place", source.Normal, source.Normal, new Vector2(123, 87), new Vector2(edge), mirror)!;
    var frame = Get(placed, "Frame"); var center = (Vector2)Get(placed, "Center");
    Check(Vector2.Distance(center, BodyCenter(source.Frame, frame)) < .0001f, source.Label + ": ring follows actual pose's painted Body center through source UVs.");
    Check(Math.Abs((float)Get(placed, "Radius") - (float)Get(normal, "Radius")) < .00001f, "Reaction poses preserve the existing normal-body ring radius.");
    Check(Vector2.Distance((Vector2)Get(normal, "Center"), new Vector2(123, 87) + new Vector2(edge * .5f)) < .0001f, "Normal body keeps its established logical center.");
}

var context = ImGui.CreateContext();
try
{
    var io = ImGui.GetIO(); unsafe { io.IniFilename = null; io.LogFilename = null; }
    io.DisplaySize = new(600); io.DeltaTime = 1f / 60; io.Fonts.AddFontDefault(); io.Fonts.Build(); io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
    var painter = new SoftwareRenderer(); var accent = new Vector4(.65f, .85f, .95f, 1); var center = new Vector2(150);
    (Vector2 Minimum, Vector2 Maximum) DrawRing(object rings, ImDrawListPtr draw, Vector2 at, float radius, bool front, object? theme, bool marks, bool mirror)
        => ((Vector2, Vector2))Call(rings, "Draw", draw, at, radius, accent, front, theme, marks, mirror)!;
    ImDrawVert[] Copy(ImDrawListPtr draw, int first, int count) => Enumerable.Range(first, count).Select(index => draw.VtxBuffer[index]).ToArray();
    bool Same(ImDrawVert a, ImDrawVert b) => a.Pos == b.Pos && a.Uv == b.Uv && a.Col == b.Col;
    ImDrawVert[] Sample(object rings, float radius, bool front, object? theme, bool marks, bool mirror, Matrix3x2? pose = null)
    {
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        draw.AddTriangleFilled(new(-50, -50), new(-30, -50), new(-40, -30), 0xFF123456); var before = Copy(draw, 0, draw.VtxBuffer.Size); var first = draw.VtxBuffer.Size; var flags = draw.Flags;
        (Vector2 Minimum, Vector2 Maximum) bounds = pose is { } matrix ? ((Vector2, Vector2))Call(rings, "DrawPosed", draw, center, radius, accent, front, theme, marks, mirror, matrix)!
            : DrawRing(rings, draw, center, radius, front, theme, marks, mirror); var ring = Copy(draw, first, draw.VtxBuffer.Size - first);
        Check(before.Select((v, i) => Same(v, draw.VtxBuffer[i])).All(v => v), "Ring reflection never mutates already drawn body/hat/foreground sentinel vertices.");
        Check(draw.Flags == flags && ring.Length is > 0 and < 6000, "Each pass restores native flags and emits bounded geometry.");
        Check(ring.All(v => float.IsFinite(v.Pos.X) && float.IsFinite(v.Pos.Y) && v.Pos.X >= bounds.Minimum.X && v.Pos.Y >= bounds.Minimum.Y && v.Pos.X <= bounds.Maximum.X && v.Pos.Y <= bounds.Maximum.Y), "Returned bounds contain post-reflection native geometry.");
        var minimum = ring.Aggregate(center, (p, v) => Vector2.Min(p, v.Pos)); var maximum = ring.Aggregate(center, (p, v) => Vector2.Max(p, v.Pos));
        Check(minimum == bounds.Minimum && maximum == bounds.Maximum, "Reported bounds are measured after reflection, excluding prior sentinel geometry.");
        draw.AddTriangleFilled(new(510, 510), new(530, 510), new(520, 530), 0xFF654321); var prior = Copy(draw, 0, draw.VtxBuffer.Size);
        if (pose is { } nextMatrix) Call(rings, "DrawPosed", draw, center, radius, accent, !front, theme, marks, mirror, nextMatrix);
        else DrawRing(rings, draw, center, radius, !front, theme, marks, mirror);
        Check(prior.Select((v, i) => Same(v, draw.VtxBuffer[i])).All(v => v), "A later ring pass also preserves the earlier ring, body and intervening foreground sentinel.");
        ImGui.Render(); return ring;
    }
    string Fingerprint(ImDrawListPtr draw)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        for (var i = 0; i < draw.VtxBuffer.Size; i++) { var vertex = draw.VtxBuffer[i]; writer.Write(MathF.Round(vertex.Pos.X, 4)); writer.Write(MathF.Round(vertex.Pos.Y, 4)); writer.Write(vertex.Col); }
        for (var i = 0; i < draw.IdxBuffer.Size; i++) writer.Write(draw.IdxBuffer[i]);
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }
    // Established native fingerprints from the original ring renderer, before themes or facing changes.
    string[] hashes = ["6a518479380c862b1bf2a5dea9128d37413dbf54afeefac402f564883be2f476", "84fc79bea89117158cf9db12fe676e28760016ecc2ade161318c2369eb680149",
        "dfac5847d4df2031ddfc64144a1a652deec1fb29adec5614e8b06c59db330796", "2b393384343afc03a899ac9ff0f41c810ef41a5a7db2467c093b661cb6528c3a"];
    var fingerprint = 0;
    foreach (var steps in new[] { 0, 120 }) foreach (var front in new[] { false, true })
    {
        var rings = New("Ui.OrbitRingRenderer"); for (var i = 0; i < steps; i++) Call(rings, "Update", 1f / 60, true, 1f);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); DrawRing(rings, draw, center, 30, front, null, true, false);
        Check(Fingerprint(draw) == hashes[fingerprint++], "Unmirrored default output retains its original geometry, colors and indices exactly."); ImGui.Render();
    }
    foreach (var edge in new[] { 96f, 192f }) foreach (var steps in new[] { 0, 120 }) foreach (var front in new[] { false, true })
    {
        var rings = New("Ui.OrbitRingRenderer"); for (var i = 0; i < steps; i++) Call(rings, "Update", 1f / 60, true, 1f);
        foreach (var theme in new object?[] { null }.Concat(Enum.GetValues(Type("Ui.SeasonalCompanionTheme")).Cast<object>()))
        {
            var plain = Sample(rings, edge * .3125f, front, theme, false, false);
            foreach (var marks in new[] { false, true })
            {
                var normal = marks ? Sample(rings, edge * .3125f, front, theme, true, false) : plain;
                var mirror = Sample(rings, edge * .3125f, front, theme, marks, true);
                Check(normal.Length == mirror.Length && normal.Select((v, i) => Vector2.Distance(mirror[i].Pos, new(center.X * 2 - v.Pos.X, v.Pos.Y)) < .00001f && v.Col == mirror[i].Col && v.Uv == mirror[i].Uv).All(v => v), "Facing reflects the complete band/outlines/motifs exactly, preserving palette and depth-pass membership.");
                if (theme != null && marks)
                {
                    var x = (Vector3)Call(rings, "Point", 0f, 1f)!; var y = (Vector3)Call(rings, "Point", MathF.PI / 2, 1f)!; var determinant = x.X * y.Y - x.Y * y.X;
                    Check(normal.Length > plain.Length && normal.Take(plain.Length).Select((v, i) => Same(v, plain[i])).All(v => v), "Motifs remain optional additions to the same holiday-colored band.");
                    Check(mirror.Skip(plain.Length).All(v => { var p = new Vector2(center.X - v.Pos.X, v.Pos.Y - center.Y); var u = (p.X * y.Y - p.Y * y.X) / determinant; var w = (x.X * p.Y - x.Y * p.X) / determinant; var z = u * x.Z + w * y.Z;
                        return (front ? z >= -.0003f : z <= .0003f) && new Vector2(u, w).Length() <= edge * .3125f * 1.47f * 1.04f + .001f; }), "Mirrored motifs retain their original ring-plane Z clipping and radius.");
                }
                Call(rings, "Update", 1f, false, 3f); var frozen = Sample(rings, edge * .3125f, front, theme, marks, true);
                Check(mirror.Select((v, i) => Same(v, frozen[i])).All(v => v), "Disabled animation leaves mirrored geometry and colors exactly static.");
            }
        }
    }

    Matrix3x2 Movement(Vector2 at, float radius, float stride, float activity, bool mirror, bool animate = true)
        => (Matrix3x2)CallStatic("Ui.OrbitRingRenderer", "Movement", at, radius, stride, activity, mirror, animate)!;
    Vector2 Reflect(Vector2 point, Vector2 at) => new(at.X * 2 - point.X, point.Y);
    foreach (var radius in new[] { 30f, 60f }) foreach (var stride in new[] { 0f, .125f, .25f, .375f, .5f, .75f, .875f, 1f })
    foreach (var activity in new[] { 0f, .25f, 1f })
    {
        var right = Movement(center, radius, stride, activity, false); var left = Movement(center, radius, stride, activity, true);
        Check(new[] { right.M11, right.M12, right.M21, right.M22, right.M31, right.M32 }.All(float.IsFinite), "Movement produces a finite matrix at every sampled stride and activity.");
        Check(Math.Abs(right.GetDeterminant() - 1) < .00001f, "The complete planet/hat/ring pose remains rigid, with no scale or shear.");
        Check(Movement(center, radius, stride, activity, false, false) == Matrix3x2.Identity
            && Movement(center, radius, stride, 0, true) == Matrix3x2.Identity, "Animation-off and zero activity preserve exact identity.");
        Check(Math.Abs(MathF.Atan2(right.M12, right.M11)) <= .05501f && Vector2.Distance(Vector2.Transform(center, right), center) <= radius * .044f + .0001f,
            "Travel sway and bob remain within the intended gentle amplitude.");
        foreach (var point in new[] { center, center + new Vector2(radius, 0), center + new Vector2(0, -radius * 1.5f) })
        {
            Check(Vector2.Distance(Vector2.Transform(Reflect(point, center), left), Reflect(Vector2.Transform(point, right), center)) < .0001f, "Facing couples the same rotation and offset to the entire mirrored assembly.");
            Check(Vector2.Distance(Vector2.Transform(point, right), Vector2.Transform(point, Movement(center, radius, stride + 1, activity, false))) < .0001f,
                "The movement matrix is periodic across stride0/1 without a jump.");
        }
    }
    Check(Movement(center, 30, float.NaN, 1, false) == Matrix3x2.Identity && Movement(center, 30, .2f, float.NaN, false) == Matrix3x2.Identity,
        "Invalid activity/stride cannot inject nonfinite geometry.");
    var settling = New("Ui.CompanionBodyMotion"); for (var i = 0; i < 30; i++) Call(settling, "Update", .05f, true, true);
    var pointToSettle = center + new Vector2(0, -90); var previousPoint = Vector2.Transform(pointToSettle, Movement(center, 60, .125f, (float)Get(settling, "Activity"), false));
    var previousActivity = (float)Get(settling, "Activity");
    for (var i = 0; i < 90; i++)
    {
        Call(settling, "Update", 1f / 60, true, false); var activity = (float)Get(settling, "Activity");
        var point = Vector2.Transform(pointToSettle, Movement(center, 60, .125f, activity, false));
        Check(activity <= previousActivity && Vector2.Distance(point, previousPoint) < .8f, "Production activity decays continuously without a stop-pose jump.");
        previousActivity = activity; previousPoint = point;
    }
    Check(Vector2.Distance(previousPoint, pointToSettle) < .001f, "Settling converges to the unchanged resting placement.");
    foreach (var radius in new[] { 30f, 60f }) foreach (var mirror in new[] { false, true }) foreach (var front in new[] { false, true })
    foreach (var theme in Enum.GetValues(Type("Ui.SeasonalCompanionTheme")).Cast<object>())
    {
        var rings = New("Ui.OrbitRingRenderer"); var regular = Sample(rings, radius, front, theme, true, mirror);
        foreach (var stride in new[] { .125f, .375f }) foreach (var activity in new[] { .25f, 1f })
        {
            var pose = Movement(center, radius, stride, activity, mirror); var posed = Sample(rings, radius, front, theme, true, mirror, pose);
            Check(regular.Length == posed.Length && regular.Select((v, i) => Vector2.Distance(Vector2.Transform(v.Pos, pose), posed[i].Pos) < .00001f && v.Col == posed[i].Col && v.Uv == posed[i].Uv).All(v => v),
                "DrawPosed transforms every completed band/motif vertex once, preserving colors, UVs and depth-pass membership.");
        }
    }

    var textures = new Dictionary<string, (ulong Id, int Width, int Height)>();
    foreach (var file in frames.Select(source => (string)Get(source.Frame, "File")).Distinct())
    {
        using var bitmap = new Bitmap(Path.Combine(assets, file)); var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb); var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); } finally { bitmap.UnlockBits(bits); }
        for (var p = 0; p < pixels.Length; p += 4) (pixels[p], pixels[p + 2]) = (pixels[p + 2], pixels[p]);
        var id = (ulong)textures.Count + 2; painter.AddTexture(id, pixels, bitmap.Width, bitmap.Height); textures.Add(file, (id, bitmap.Width, bitmap.Height));
    }
    var config = New("Configuration"); Set(config, "CompanionPalette", "Moonlight"); Set(config, "AnimationsEnabled", true);
    var roamer = RuntimeHelpers.GetUninitializedObject(Type("Ui.RoamingCompanion")); Set(roamer, "config", config); Set(roamer, "assetDirectory", assets); Set(roamer, "variants", variants); Set(roamer, "wardrobe", wardrobe); Set(roamer, "displayAvatar", "Orbit");
    Set(roamer, "homeRoutine", New("Ui.CompanionHomeRoutine", 1)); var runtimeRings = New("Ui.OrbitRingRenderer"); Set(roamer, "orbitRings", runtimeRings);
    var runtimeMotion = New("Ui.CompanionBodyMotion"); Set(roamer, "bodyMotion", runtimeMotion);
    var runtimeGait = New("Ui.CompanionGait"); Set(roamer, "gait", runtimeGait);
    for (var i = 0; i < 120; i++) Call(runtimeRings, "Update", 1f / 60, true, 1f);
    object Wrap(Type type, string file) => LocalProxy.Create(type, (method, _) => method.Name switch { "get_Handle" => new ImTextureID(textures[file].Id), "get_Width" => textures[file].Width, "get_Height" => textures[file].Height, "GetWrapOrDefault" => Wrap(method.ReturnType, file), _ => throw new InvalidOperationException("Unexpected texture operation.") });
    Type("Services").GetProperty("Textures", Static)!.SetValue(null, LocalProxy.Create<ITextureProvider>((method, values) => { if (method.Name != "GetFromFile") throw new InvalidOperationException("Unexpected texture provider access."); return Wrap(method.ReturnType, Path.GetFileName(values[0]!.ToString()!)); }));
    Type("Services").GetProperty("Framework", Static)!.SetValue(null, LocalProxy.Create<IFramework>((_, _) => throw new InvalidOperationException("Game/framework calls are forbidden.")));
    var costumeLooks = looks.Where(look => ((string)Get(look, "Choice")).StartsWith("Costume", StringComparison.Ordinal)).ToArray();
    foreach (var edge in new[] { 96f, 192f })
    {
        var tile = new Vector2(edge * 1.7f + 24, edge * 1.7f + 35); io.DisplaySize = new(tile.X * 6, tile.Y * costumeLooks.Length);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var row = 0; row < costumeLooks.Length; row++) for (var column = 0; column < 6; column++)
        {
            var look = costumeLooks[row]; var mirror = column % 2 == 1; var carry = column >= 2; var time = column < 4 ? .1f : .5f;
            var position = new Vector2(column * tile.X + edge * .35f + 12, row * tile.Y + edge * .35f + 30); var pose = carry ? "Carry" : "Idle";
            Set(roamer, "currentLook", look); Set(roamer, "poseTime", time); var first = draw.VtxBuffer.Size;
            object?[] values = [draw, position, new Vector2(edge), mirror, EnumValue("Ui.CompanionPose", pose), 0f, null];
            Check((bool)Call(roamer, "TryDrawLook", values)!, "Actual runtime paints the complete costume/ring assembly.");
            var source = List(look, "Frames")[carry ? (time < .4f ? 4 : 5) : 0]; var file = (string)Get(source, "File"); var ids = new HashSet<int>();
            for (var c = 0; c < draw.CmdBuffer.Size; c++) { var command = draw.CmdBuffer[c]; if (command.TextureId.Handle != textures[file].Id) continue; for (uint n = 0; n < command.ElemCount; n++) { var index = (int)(draw.IdxBuffer[(int)(command.IdxOffset + n)] + command.VtxOffset); if (index >= first) ids.Add(index); } }
            var body = ids.Order().ToArray(); Check(body.Length == 4 && body[0] > first && body[^1] < draw.VtxBuffer.Size - 1, "Actual runtime draws rear ring, one painted body, then front ring.");
            var a = draw.VtxBuffer[body[0]]; var b = draw.VtxBuffer[body[1]]; var d = draw.VtxBuffer[body[3]]; var measured = (Vector4)Get(source, "Body"); var sourceUv = new Vector2((measured.X + measured.Z) * .5f, (measured.Y + measured.W) * .5f) / (Vector2)Get(source, "SheetSize");
            var actualCenter = a.Pos + (b.Pos - a.Pos) * ((sourceUv.X - a.Uv.X) / (b.Uv.X - a.Uv.X)) + (d.Pos - a.Pos) * ((sourceUv.Y - a.Uv.Y) / (d.Uv.Y - a.Uv.Y));
            var placed = CallStatic("Ui.OrbitRingRenderer", "Place", source, Get(look, "Normal"), position, new Vector2(edge), mirror)!; var radius = (float)Get(placed, "Radius");
            var theme = CallStatic("Ui.OrbitRingRenderer", "ThemeForLook", (string)Get(look, "Choice"));
            var ringBefore = Copy(draw, first, body[0] - first); var ringAfter = Copy(draw, body[^1] + 1, draw.VtxBuffer.Size - body[^1] - 1);
            var comparison = ImGui.GetBackgroundDrawList(); comparison.PushClipRect(new(-1), new(-1), true);
            var comparisonStart = comparison.VtxBuffer.Size; DrawRing(runtimeRings, comparison, actualCenter, radius, false, theme, true, mirror); var expectedBack = Copy(comparison, comparisonStart, comparison.VtxBuffer.Size - comparisonStart);
            comparisonStart = comparison.VtxBuffer.Size; DrawRing(runtimeRings, comparison, actualCenter, radius, true, theme, true, mirror); var expectedFront = Copy(comparison, comparisonStart, comparison.VtxBuffer.Size - comparisonStart);
            comparison.PopClipRect();
            // The themed ring ignores the fixture accent; compare exact colors and projected positions.
            // Reconstructing source UVs at the wide gallery's screen coordinates
            // incurs subpixel float roundoff; one thousandth of a pixel is sufficient.
            Check(ringBefore.Length == expectedBack.Length && ringBefore.Select((v, i) => Vector2.Distance(v.Pos, expectedBack[i].Pos) < .001f && v.Col == expectedBack[i].Col).All(v => v)
                && ringAfter.Length == expectedFront.Length && ringAfter.Select((v, i) => Vector2.Distance(v.Pos, expectedFront[i].Pos) < .001f && v.Col == expectedFront[i].Col).All(v => v), "Runtime passes facing and the actual carried-pose body center into both ring passes.");
            draw.AddText(new(column * tile.X + 5, row * tile.Y + 5), 0xFFFFFFFF, ((string)Get(look, "Choice"))[7..] + " " + pose + (mirror ? " L" : " R"));
        }
        ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"painted-orbit-{edge:0}.png"));
    }
    var activeMotion = New("Ui.CompanionBodyMotion"); for (var step = 0; step < 30; step++) Call(activeMotion, "Update", .05f, true, true);
    var activityStrength = (float)Get(activeMotion, "Activity"); Check(activityStrength > .99f, "Actual runtime fixture exercises active movement rather than identity activity.");
    ImDrawVert[] RuntimeSample(object? look, float edge, bool mirror, float stride, bool moving, float closure)
    {
        Set(roamer, "currentLook", look); Set(roamer, "bodyMotion", moving ? activeMotion : runtimeMotion); Set(runtimeGait, "Phase", stride);
        io.DisplaySize = new(600); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        object?[] values = [draw, new Vector2(180), new Vector2(edge), mirror, EnumValue("Ui.CompanionPose", "Walk"), closure, null];
        Check((bool)Call(roamer, "TryDrawLook", values)!, "Actual walking runtime draws original, everyday, holiday and painted Orbit bodies.");
        var result = Copy(draw, 0, draw.VtxBuffer.Size); ImGui.Render(); return result;
    }
    foreach (var look in new object?[] { null }.Concat(looks)) foreach (var edge in new[] { 96f, 192f })
    foreach (var mirror in new[] { false, true }) foreach (var stride in new[] { .125f, .375f })
    {
        var normal = look == null ? originals[0] : Get(look, "Normal");
        var placed = CallStatic("Ui.OrbitRingRenderer", "Place", normal, normal, new Vector2(180), new Vector2(edge), mirror)!;
        var pose = Movement((Vector2)Get(placed, "Center"), (float)Get(placed, "Radius"), stride, activityStrength, mirror);
        foreach (var closure in new[] { 0f, 1f })
        {
            var rest = RuntimeSample(look, edge, mirror, stride, false, closure); var moving = RuntimeSample(look, edge, mirror, stride, true, closure);
            Check(rest.Length == moving.Length && rest.Select((v, i) => Vector2.Distance(Vector2.Transform(v.Pos, pose), moving[i].Pos) < .005f
                && v.Col == moving[i].Col && v.Uv == moving[i].Uv).All(v => v),
                "Actual walking uses one shared rigid matrix for the planet, painted hat, eye closure and both complete ring passes.");
        }
    }
    // Eight native stride frames are diagnostic samples of the production painter,
    // without changing travel timing or source art. They can be assembled into a local GIF.
    foreach (var edge in new[] { 96f, 192f }) for (var step = 0; step < 8; step++)
    {
        var tile = new Vector2(edge * 1.7f + 24, edge * 1.7f + 35); io.DisplaySize = new(tile.X * 2, tile.Y * costumeLooks.Length);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); Set(roamer, "bodyMotion", activeMotion); Set(runtimeGait, "Phase", step / 8f);
        for (var row = 0; row < costumeLooks.Length; row++) for (var side = 0; side < 2; side++)
        {
            Set(roamer, "currentLook", costumeLooks[row]); var position = new Vector2(side * tile.X + edge * .35f + 12, row * tile.Y + edge * .35f + 30);
            object?[] values = [draw, position, new Vector2(edge), side == 1, EnumValue("Ui.CompanionPose", "Walk"), 0f, null];
            Check((bool)Call(roamer, "TryDrawLook", values)!, "Native sway gallery paints every costume in both facings.");
            draw.AddText(new(side * tile.X + 5, row * tile.Y + 5), 0xFFFFFFFF, ((string)Get(costumeLooks[row], "Choice"))[7..] + (side == 1 ? " L" : " R"));
        }
        ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"sway-{edge:0}-{step}.png"));
    }
    Console.WriteLine($"Orbit pose integration: {checks} checks passed; all160 pose centers, exact native reflection/default fingerprints,9 holiday palettes/motifs, rigid movement/settling and actual coupled runtime at96/192. No game/framework calls.");
}
finally { ImGui.DestroyContext(context); }

internal sealed record Source(object Frame, object Normal, string Label, object? Look);
public class LocalProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class { var proxy = Create<T, LocalProxy>(); ((LocalProxy)(object)proxy).Handler = handler; return proxy; }
    public static object Create(Type type, Func<MethodInfo, object?[], object?> handler) { var proxy = DispatchProxy.Create(type, typeof(LocalProxy)); ((LocalProxy)proxy).Handler = handler; return proxy; }
}
