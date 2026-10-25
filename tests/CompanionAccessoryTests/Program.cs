using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;

var pluginPath = Path.GetFullPath(args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal))
    ?? "src/OmniKit/bin/Release/OmniKit.dll");
var hostPath = Environment.GetEnvironmentVariable("DALAMUD_LIB_PATH") ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (var directory in new[] { Path.GetDirectoryName(pluginPath)!, hostPath })
    {
        var path = Path.Combine(directory, name.Name + ".dll");
        if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
    }
    return null;
};
var plugin = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginPath);
if (args.Contains("--blink-wiring"))
{
    var blinkChecks = BlinkWiringChecks.Run(plugin, Path.GetFullPath("assets"));
    Console.WriteLine($"Actual DLL universal-blink wiring: {blinkChecks} checks passed. Eight public looks; no personal settings, game or network calls.");
    return;
}
var placement = Type("CompanionAccessoryPlacement");
var apply = placement.GetMethod("Apply")!;
var localDelta = placement.GetMethod("LocalDelta")!;
var transformType = Type("SeasonalBodyTransform");
var fitType = Type("SeasonalAccessoryFit");
var quadType = Type("SeasonalOverlayQuad");
var adjustmentType = plugin.GetType("OmniKit.CompanionAccessoryAdjustment", true)!;
var rendererType = Type("SeasonalCompanionRenderer");
var sourceRect = new Vector4(200, 300, 500, 600);
var anchor = new Vector2(350, 340);
const float headWidth = 90;
var fit = Activator.CreateInstance(fitType, [anchor, headWidth])!;
var uvStart = new Vector2(.1f, .2f); var uvEnd = new Vector2(.6f, .7f);
var checks = 0;
Type Type(string name) => plugin.GetType("OmniKit.Ui." + name, true)!;
void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
bool Near(Vector2 a, Vector2 b, float tolerance = .0003f) => Vector2.Distance(a, b) <= tolerance;
Vector2 Property(object value, string name) => (Vector2)value.GetType().GetProperty(name)!.GetValue(value)!;
Vector2[] Points(object quad) => [Property(quad, "A"), Property(quad, "B"), Property(quad, "C"), Property(quad, "D")];
object Adjustment(float x = 0, float y = 0, float scale = 1, float rotation = 0) => Activator.CreateInstance(adjustmentType,
    ["Wink", "Default", "Starlight", x, y, scale, rotation])!;
object Apply(object quad, object body, object adjustment) => apply.Invoke(null, [quad, body, fit, adjustment])!;
Vector2 LocalDelta(object body, Vector2 delta) => (Vector2)localDelta.Invoke(null, [body, fit, delta])!;
var drawings = new List<(object Original, object Adjusted, Vector2 Pivot, string Label)>();
var sourceOffsets = new[] { new Vector2(-45, -50), new Vector2(45, -50), new Vector2(45, 15), new Vector2(-45, 15) };
float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

foreach (var edge in new[] { 96f, 192f })
foreach (var mirrored in new[] { false, true })
foreach (var angle in new[] { 0f, MathF.PI / 3, MathF.PI, MathF.Tau })
foreach (var shear in new[] { -.3f, 0f, .3f })
foreach (var origin in new[] { new Vector2(0, 0), new Vector2(237, 109) })
{
    var matrix = Matrix3x2.CreateScale(mirrored ? -1 : 1, 1) * new Matrix3x2(1, 0, shear, 1, 0, 0)
        * Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateScale(edge / 300) * Matrix3x2.CreateTranslation(origin);
    Vector2 Map(Vector2 point) => Vector2.Transform(point - new Vector2(sourceRect.X, sourceRect.Y), matrix);
    var body = Activator.CreateInstance(transformType, [sourceRect, 300f, Map(new(200, 300)), Map(new(500, 300)), Map(new(200, 600))])!;
    var pivot = Map(anchor);
    var original = Activator.CreateInstance(quadType,
        [Map(anchor + new Vector2(-45, -50)), Map(anchor + new Vector2(45, -50)), Map(anchor + new Vector2(45, 15)),
            Map(anchor + new Vector2(-45, 15)), uvStart, uvEnd])!;
    var unchanged = Apply(original, body, Adjustment());
    Check(Points(original).Zip(Points(unchanged)).All(pair => Near(pair.First, pair.Second)), "Default placement preserves all four painted corners.");
    foreach (var scale in new[] { .25f, 1f, 3f })
    {
        var adjusted = Apply(original, body, Adjustment(.4f, -.2f, scale));
        var shift = Map(anchor + new Vector2(.4f, -.2f) * headWidth) - pivot;
        var vertices = Points(adjusted); var originalVertices = Points(original);
        Check(vertices.Zip(originalVertices).All(pair => Near(pair.First, pivot + shift + (pair.Second - pivot) * scale)),
            "Each corner scales uniformly about the measured crown pivot, then receives the local head-width offset.");
        Check(Property(adjusted, "UvStart") == uvStart && Property(adjusted, "UvEnd") == uvEnd,
            "Resizing and translating an accessory never changes its source crop.");
        Check(Near(vertices[1] - vertices[0], (originalVertices[1] - originalVertices[0]) * scale) &&
            Near(vertices[3] - vertices[0], (originalVertices[3] - originalVertices[0]) * scale),
            "Mirroring, rotation and shear preserve the accessory's scaled local axes.");
    }
    foreach (var rotation in new[] { -450f, -180f, -90f, -35f, 0f, 35f, 90f, 180f, 450f })
    foreach (var size in new[] { .25f, 1f, 3f })
    {
        var adjusted = Apply(original, body, Adjustment(.4f, -.2f, size, rotation));
        // The oracle starts from source artwork rather than inverting production screen
        // geometry. Positive source angles are clockwise in the image's downward Y axis.
        var rotate = Matrix3x2.CreateRotation(Math.Clamp(rotation, -180, 180) * MathF.PI / 180);
        var expected = sourceOffsets.Select(offset => Map(anchor + new Vector2(.4f, -.2f) * headWidth
            + Vector2.Transform(offset, rotate) * size)).ToArray();
        var vertices = Points(adjusted);
        Check(vertices.Zip(expected).All(pair => Near(pair.First, pair.Second)),
            "Accessory rotation precedes body mirroring, roll and shear; the offset stays in unrotated head-local axes.");
        Check(Property(adjusted, "UvStart") == uvStart && Property(adjusted, "UvEnd") == uvEnd,
            "Local rotation preserves the complete source crop and corner-to-UV order.");
        var originalVertices = Points(original);
        Check(Math.Sign(Cross(vertices[1] - vertices[0], vertices[3] - vertices[0])) ==
            Math.Sign(Cross(originalVertices[1] - originalVertices[0], originalVertices[3] - originalVertices[0])),
            "Rotation preserves winding; only the body's mirror changes handedness.");
        var pivotQuad = Activator.CreateInstance(quadType, [pivot, pivot, pivot, pivot, uvStart, uvEnd])!;
        Check(Points(Apply(pivotQuad, body, Adjustment(.4f, -.2f, size, rotation)))
                .All(point => Near(point, Map(anchor + new Vector2(.4f, -.2f) * headWidth))),
            "The measured crown pivot does not orbit when the angle or uniform scale changes.");
    }
    foreach (var delta in new[] { new Vector2(.6f, -.35f), new Vector2(-.9f, .8f), Vector2.Zero })
    {
        var screen = Map(anchor + delta * headWidth) - pivot;
        Check(Near(LocalDelta(body, screen), delta, .00002f), "Inverse dragging recovers measured head-width units for every transform.");
        var applied = Apply(original, body, Adjustment(delta.X, delta.Y));
        Check(Points(applied).Zip(Points(original)).All(pair => Near(pair.First - pair.Second, screen)),
            "Applying inverse drag units follows the requested screen displacement without a mirror/roll reversal.");
    }
    Check(Near(LocalDelta(body, new Vector2(13, -7)) + LocalDelta(body, new Vector2(-3, 19)), LocalDelta(body, new Vector2(10, 12))),
        "Splitting a mouse drag into frames does not change its final local displacement.");
    foreach (var invalidAngle in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    {
        var bounded = Apply(original, body, Adjustment(float.NaN, float.PositiveInfinity, float.NaN, invalidAngle));
        Check(Points(bounded).Zip(Points(original)).All(pair => Near(pair.First, pair.Second)),
            "Nonfinite offsets, scale and rotation fall back to a finite neutral placement.");
    }
    if (origin == Vector2.Zero && edge == 96 && shear == .3f && angle != MathF.Tau)
        drawings.Add((original, Apply(original, body, Adjustment(.4f, -.2f, 1.4f, 45)), pivot,
            $"{(mirrored ? "Mirror" : "Normal")} roll {angle:0.0}, hat +45"));
}
var singular = Activator.CreateInstance(transformType, [sourceRect, 300f, Vector2.Zero, Vector2.UnitX, Vector2.UnitX])!;
Check(LocalDelta(singular, new Vector2(12, 8)) == Vector2.Zero, "A degenerate transform rejects the drag instead of dividing by zero.");
var zeroWidth = Activator.CreateInstance(fitType, [anchor, 0f])!;
Check((Vector2)localDelta.Invoke(null, [singular, zeroWidth, Vector2.One])! == Vector2.Zero, "A zero-width head cannot produce an unbounded drag.");
foreach (var rotation in new[] { -180f, -37.5f, 0f, 62.25f, 180f })
{
    var saved = Adjustment(.3f, -.2f, 1.4f, rotation);
    var json = JsonSerializer.Serialize(saved, adjustmentType);
    using var parsed = JsonDocument.Parse(json);
    Check(parsed.RootElement.GetProperty("RotationDegrees").GetSingle() == rotation,
        "The real adjustment record serializes rotation as a named numeric preference.");
    Check(Equals(saved, JsonSerializer.Deserialize(json, adjustmentType)),
        "The complete synthetic saved fit, including angle, round-trips without altering its identity or other adjustments.");
}
var legacy = JsonSerializer.Deserialize("""{"Avatar":"Wink","Look":"Default","Theme":"Starlight","OffsetX":0.3,"OffsetY":-0.2,"Scale":1.4}""", adjustmentType)!;
Check((float)adjustmentType.GetProperty("RotationDegrees")!.GetValue(legacy)! == 0,
    "A legacy serialized fit without an angle keeps its original zero rotation.");
Check(!(bool)placement.GetMethod("IsDefault")!.Invoke(null, [Adjustment(rotation: 35)])!,
    "An angle-only preference is retained rather than discarded as the default fit.");
Render();
checks += RotationPaddingChecks.Run(plugin, Path.GetFullPath("assets"));
checks += LayerOrderingChecks.Run(plugin, Path.GetFullPath("assets"));
checks += EditorIsolationChecks.Run(plugin, Path.GetFullPath("assets"));
if (args.Contains("--editor")) checks += EditorChecks.Run(plugin, Path.GetFullPath("assets"));
Console.WriteLine($"Actual DLL accessory placement: {checks} checks passed; transformed-quads.png rendered. No personal settings, framework, game or network calls.");

unsafe void Render()
{
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO(); io.DisplaySize = new Vector2(720, 440); io.DeltaTime = 1f / 60;
        io.IniFilename = null; io.LogFilename = null; io.Fonts.AddFontDefault(); io.Fonts.Build();
        var renderer = new SoftwareRenderer(); var pixels = new byte[16 * 16 * 4];
        for (var n = 0; n < pixels.Length; n += 4)
        { pixels[n] = (byte)(40 + n / 4 % 16 * 12); pixels[n + 1] = 190; pixels[n + 2] = 240; pixels[n + 3] = 255; }
        renderer.AddTexture(2, pixels, 16, 16);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var index = 0; index < drawings.Count; index++)
        {
            var item = drawings[index]; var shift = new Vector2(120 + index % 3 * 240, 100 + index / 3 * 210) - item.Pivot;
            object Move(object quad) => Activator.CreateInstance(quadType,
                [Property(quad, "A") + shift, Property(quad, "B") + shift, Property(quad, "C") + shift,
                    Property(quad, "D") + shift, uvStart, uvEnd])!;
            var original = Move(item.Original); var adjusted = Move(item.Adjusted);
            rendererType.GetMethod("Draw")!.Invoke(null, [draw, new ImTextureID(2UL), original, 0x60909090u]);
            var before = draw.VtxBuffer.Size;
            rendererType.GetMethod("Draw")!.Invoke(null, [draw, new ImTextureID(2UL), adjusted, 0xFFFFFFFFu]);
            Check(draw.VtxBuffer.Size - before == 4, "The production renderer emits one adjusted accessory quad, not a duplicated body.");
            for (var vertex = 0; vertex < 4; vertex++)
            {
                Check(Near(draw.VtxBuffer[before + vertex].Pos, Points(adjusted)[vertex]), "Native vertices match the tested adjusted geometry.");
                var expectedUv = new[] { uvStart, new Vector2(uvEnd.X, uvStart.Y), uvEnd, new Vector2(uvStart.X, uvEnd.Y) }[vertex];
                Check(draw.VtxBuffer[before + vertex].Uv == expectedUv, "Native rotated vertices retain their original source UV correspondence.");
            }
            draw.AddCircleFilled(item.Pivot + shift, 3, 0xFF70CFFFu);
            draw.AddText(new Vector2(index % 3 * 240 + 12, index / 3 * 210 + 12), 0xFFFFFFFFu, item.Label);
        }
        ImGui.Render();
        renderer.Save(ImGui.GetDrawData(), Path.GetFullPath(".local/visualharness/companion-accessories/transformed-quads.png"));
    }
    finally { ImGui.DestroyContext(context); }
}
