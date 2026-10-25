using System.Collections;
using System.Drawing.Imaging;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using LauncherUiSmoke;

if (args.Contains("--help"))
{
    Console.WriteLine("dotnet run --project tests/CompanionHomePoseIntegrationTests -- [compiled OmniKit.dll] [--logic-only]");
    return;
}
var plugin = Path.GetFullPath(args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal)) ?? "src/OmniKit/bin/Release/OmniKit.dll");
var host = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (var directory in new[] { Path.GetDirectoryName(plugin)!, host })
    {
        var path = Path.Combine(directory, name.Name + ".dll");
        if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
    }
    return null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(plugin);
const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
Type Type(string name) => assembly.GetType("OmniKit." + name, true)!;
object New(string name, params object?[] values) => Activator.CreateInstance(Type(name), Instance, null, values, null)!;
object Get(object value, string name) => value.GetType().GetProperty(name, Instance)!.GetValue(value)!;
void Set(object value, string name, object? content) => (value.GetType().GetField(name, Instance)
    ?? value.GetType().GetField("<" + name + ">P", Instance) ?? value.GetType().GetField("<" + name + ">k__BackingField", Instance))!.SetValue(value, content);
object? Call(object value, string name, params object?[] values) => value.GetType().GetMethod(name, Instance)!.Invoke(value, values);
object EnumValue(string name, string value) => Enum.Parse(Type(name), value);
var checks = 0;
void Check(bool valid, string reason) { checks++; if (!valid) throw new InvalidOperationException(reason); }
var assets = Path.GetFullPath("assets");
var library = New("Ui.CompanionHomePoseLibrary", assets);
Check((bool)Get(library, "IsAvailable"), "Actual DLL must load all17rear atlases and117exact identities before this harness can validate artwork.");
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(assets, "companion-homeposes.frames.json")));
foreach (var look in manifest.RootElement.GetProperty("Looks").EnumerateArray())
foreach (var pose in new[] { "TurnBack", "RearCrouch", "RearClimbA", "RearClimbB" })
{
    var source = Call(library, "Find", look.GetProperty("Avatar").GetString(), look.GetProperty("Id").GetString(), EnumValue("Ui.CompanionHomePose", pose));
    Check(source != null, "Each of468authored frames resolves by its exact identity and state.");
    foreach (var mirrored in new[] { false, true })
    {
        var support = new Vector2(500, 400);
        var placed = Call(source!, "Place", support, new Vector2(192), .45f, mirrored)!;
        var bounds = (Vector4)Get(source!, "Bounds"); var anchor = (Vector2)Get(source!, "Anchor");
        var offset = (anchor - new Vector2(bounds.X, bounds.Y)) * (192 * .45f / (float)Get(source!, "ScaleReference"));
        var size = (Vector2)Get(placed, "Size"); if (mirrored) offset.X = size.X - offset.X;
        Check(Vector2.Distance((Vector2)Get(placed, "Position") + offset, support) < .001f, "Every rear source preserves its authored foot pivot in both facings.");
        Check(size.X <= 192 * .45f * 1.4f + .001f && size.Y <= 192 * .45f * 1.6f + .001f,
            "Every rear source crop stays bounded by the common row scale.");
    }
}
var wardrobe = New("Ui.CompanionWardrobeLibrary", assets);
var variants = New("Ui.CompanionVariantLibrary", assets);
var holidays = New("Ui.CompanionHolidayLibrary", assets);
var homes = New("Ui.CompanionHomeStyleLibrary", assets);
var identities = new List<(string Avatar, object Look)>();
foreach (var avatar in new[] { "Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism", "Nib", "Echo", "Fizz", "Glim", "Rune" })
    identities.Add((avatar, Call(wardrobe, "FindBare", avatar)!));
foreach (var avatar in new[] { "Moonblob", "Prism" })
    identities.Add((avatar, ((IEnumerable)Get(variants, "Looks")).Cast<object>().First(look => (string)Get(look, "Avatar") == avatar)));
foreach (var avatar in new[] { "Moonblob", "Orbit" })
    identities.Add((avatar, ((IEnumerable)Get(wardrobe, "Looks")).Cast<object>().First(look => (string)Get(look, "Avatar") == avatar
        && ((string)Get(look, "Choice")).StartsWith("Costume", StringComparison.Ordinal))));
var config = New("Configuration"); Set(config, "CompanionSeasonalEnabled", false);
var companion = RuntimeHelpers.GetUninitializedObject(Type("Ui.RoamingCompanion"));
Set(companion, "config", config); Set(companion, "assetDirectory", assets); Set(companion, "homePoses", library);
Set(companion, "wardrobe", wardrobe); Set(companion, "variants", variants); Set(companion, "homeVisible", true);
Set(companion, "napArtwork", New("Ui.CompanionNapArtwork", assets));
var authoredLooks = ((IEnumerable)Get(variants, "Looks")).Cast<object>().Concat(((IEnumerable)Get(holidays, "Looks")).Cast<object>())
    .ToDictionary(look => ((string)Get(look, "Avatar"), (string)Get(look, "Id")));
using var napManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(assets, "companion-naps.frames.json")));
var napSources = napManifest.RootElement.GetProperty("Atlases").EnumerateArray().ToDictionary(sheet => sheet.GetProperty("Avatar").GetString()!);
var measuredNapWidths = new Dictionary<string, float>();
float NormalWidth(object frame)
{
    var body = (Vector4)Get(frame, "Body"); return (body.Z - body.X) / (float)Get(frame, "SourceScale");
}
float DefaultWidth(string avatar)
{
    if (avatar == "Orbit") return NormalWidth(((IEnumerable)Get(variants, "OriginalOrbitCore")).Cast<object>().First());
    if (!napSources.TryGetValue(avatar, out var sheet)) return NormalWidth(Get(Call(wardrobe, "FindBare", avatar)!, "Normal"));
    if (!measuredNapWidths.TryGetValue(avatar, out var width))
    {
        var values = sheet.GetProperty("Frames")[0].GetProperty("Bounds").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        width = MeasureBodyWidth(Path.Combine(assets, sheet.GetProperty("File").GetString()!), new(values[0], values[1], values[2] - values[0], values[3] - values[1]));
        measuredNapWidths.Add(avatar, width);
    }
    return width / sheet.GetProperty("ScaleReference").GetSingle();
}
void CheckWidth(string avatar, object? front, float expectedWidth)
{
    Set(companion, "displayAvatar", avatar); Set(companion, "currentLook", front);
    var id = front == null || (string)Get(front, "Choice") == "Default" || ((string)Get(front, "Choice")).StartsWith("Costume", StringComparison.Ordinal)
        ? "Default" : (string)Get(front, "Id");
    var crouch = Call(library, "Find", avatar, id, EnumValue("Ui.CompanionHomePose", "RearCrouch"))!;
    var correction = (float)Call(companion, "RearEntryScale", crouch)!;
    var body = (Vector4)Get(crouch, "BodyBounds");
    var rearWidth = (body.Z - body.X) / (float)Get(crouch, "ScaleReference") * correction;
    Check(correction is >= .25f and <= 2f && Math.Abs(rearWidth / expectedWidth - 1) < .00001f,
        avatar + "/" + id + " actual runtime resolver matches the independently measured front body width.");
    foreach (var pose in new[] { "TurnBack", "RearCrouch", "RearClimbA", "RearClimbB" })
    {
        var frame = Call(library, "Find", avatar, id, EnumValue("Ui.CompanionHomePose", pose))!;
        var original = Call(frame, "Place", new Vector2(500), new Vector2(192), .45f, false)!;
        var scaled = Call(frame, "Place", new Vector2(500), new Vector2(192), .45f * correction, false)!;
        Check(Vector2.Distance((Vector2)Get(scaled, "Size"), (Vector2)Get(original, "Size") * correction) < .001f,
            "All four authored poses retain one common correction and their distinct relative dimensions.");
    }
}
foreach (var value in manifest.RootElement.GetProperty("Looks").EnumerateArray())
{
    var avatar = value.GetProperty("Avatar").GetString()!; var id = value.GetProperty("Id").GetString()!;
    object? front = id == "Default" ? napSources.ContainsKey(avatar) ? null : Call(wardrobe, "FindBare", avatar)
        : authoredLooks[(avatar, id)];
    CheckWidth(avatar, front, id == "Default" ? DefaultWidth(avatar) : NormalWidth(Get(front!, "Normal")));
}
foreach (var avatar in identities.Take(13).Select(identity => identity.Avatar))
{
    var costume = ((IEnumerable)Get(wardrobe, "Looks")).Cast<object>().First(look => (string)Get(look, "Avatar") == avatar
        && ((string)Get(look, "Choice")).StartsWith("Costume", StringComparison.Ordinal));
    CheckWidth(avatar, costume, NormalWidth(Get(Call(wardrobe, "FindBare", avatar)!, "Normal")));
}
if (args.Contains("--logic-only")) { Console.WriteLine($"Compiled rear sources and130actual front paths: {checks} checks passed."); return; }
var requested = new List<string>();
var textures = new Dictionary<string, (ulong Id, int Width, int Height)>();
SoftwareRenderer painter = null!;
void LoadTexture(string file)
{
    if (textures.ContainsKey(file)) return;
    using var image = new Bitmap(Path.Combine(assets, file));
    var bits = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var pixels = new byte[image.Width * image.Height * 4];
    try { for (var y = 0; y < image.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * image.Width * 4, image.Width * 4); }
    finally { image.UnlockBits(bits); }
    for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
    var id = (ulong)textures.Count + 2; textures.Add(file, (id, image.Width, image.Height)); painter.AddTexture(id, pixels, image.Width, image.Height);
}
object Wrap(System.Type type, string file) => TextureProxy.Create(type, (method, _) => method.Name switch
{
    "get_Handle" => new ImTextureID(textures[file].Id), "get_Width" => textures[file].Width, "get_Height" => textures[file].Height,
    "GetWrapOrDefault" => Wrap(method.ReturnType, file), _ => throw new InvalidOperationException("Unexpected texture access: " + method.Name)
});
Type("Services").GetProperty("Textures", Static)!.SetValue(null, TextureProxy.Create(typeof(ITextureProvider), (method, values) =>
{
    if (method.Name != "GetFromFile") throw new InvalidOperationException("Unexpected service access: " + method.Name);
    var file = Path.GetFileName(values[0]!.ToString()!); requested.Add(file); LoadTexture(file); return Wrap(method.ReturnType, file);
}));
var context = ImGui.CreateContext();
try
{
    var io = ImGui.GetIO(); unsafe { io.IniFilename = null; io.LogFilename = null; }
    io.DeltaTime = 1f / 60; io.Fonts.AddFontDefault(); io.Fonts.Build(); io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
    painter = new SoftwareRenderer();
    var output = Path.GetFullPath(".local/visualharness/companion-homeposes"); Directory.CreateDirectory(output);
    foreach (var (petEdge, homeEdge, mirrored, homeId) in new[]
    {
        (96f, 48f, false, "Default"), (192f, 384f, true, "CloudCushion"),
        (96f, 384f, false, "CloudCushion"), (192f, 48f, true, "Default")
    })
    {
        // Synthetic approach only. A present navigator selects production's grounded threshold lane;
        // the real routine still owns shrinking, turn timing, climbing and doorway occlusion.
        var navigationType = Type("Ui.CompanionHomeRoutine").GetMethod("Update", Instance)!.GetParameters()[^1].ParameterType;
        var navigationParameters = navigationType.GetGenericArguments().SkipLast(1)
            .Select((type, index) => Expression.Parameter(type, "arg" + index)).ToArray();
        var travelType = Type("Ui.CompanionHomeTravel");
        var travel = Expression.New(travelType.GetConstructors(Instance).Single(), navigationParameters[1],
            Expression.Constant(mirrored), Expression.Constant(EnumValue("Ui.CompanionPose", "Walk")), Expression.Constant(false));
        var navigator = Expression.Lambda(navigationType, travel, navigationParameters).Compile();
        var selected = homeEdge == 48 && petEdge == 96 ? identities : identities.Where(identity => identity.Avatar is "Moonblob" or "Mochi" or "Prism").Take(3).ToList();
        var tile = new Vector2(Math.Max(homeEdge, petEdge) + 96, homeEdge + petEdge + 100);
        io.DisplaySize = tile * new Vector2(4, selected.Count);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var row = 0; row < selected.Count; row++)
        for (var stage = 0; stage < 4; stage++)
        {
            var identity = selected[row]; var top = tile * new Vector2(stage, row);
            var minimum = top + new Vector2((tile.X - homeEdge) * .5f, 44); var maximum = minimum + new Vector2(homeEdge);
            var style = Call(Call(homes, "GetHome", homeId)!, "Place", minimum, maximum, mirrored)!;
            var house = New("Ui.CompanionHomeRect", Get(style, "Minimum"), Get(style, "Maximum"));
            var sprite = new Vector2(petEdge); var layout = New("Ui.CompanionLayout", top + new Vector2(8, 32), top + tile - new Vector2(8), sprite);
            var routine = New("Ui.CompanionHomeRoutine", 17); Call(routine, "Reset", 15f);
            var start = (Vector2)Call(style, "OutsidePosition", sprite, 4f, true)!;
            var targetTime = stage switch { 0 => .90f, 1 => .935f, 2 => 1.45f, _ => 1.9f };
            for (var tick = 0; tick < 3000; tick++)
            {
                Call(routine, "Update", .01f, true, false, start, mirrored, layout, house, mirrored, 15f, 5f,
                    top, top + tile, true, style, navigator);
                if (Get(routine, "State").ToString() == "Entering" && (float)Get(routine, "SleepPhase") >= targetTime) break;
            }
            Check(Get(routine, "State").ToString() == "Entering", "Synthetic setup reaches the actual entry state.");
            Set(companion, "homeRoutine", routine); Set(companion, "homeStyleFrame", style); Set(companion, "houseMirrored", mirrored);
            var originalDefault = (string)Get(identity.Look, "Choice") == "Default"
                && identity.Avatar is "Moonblob" or "Wink" or "Nova" or "Mochi" or "Puff" or "Orbit" or "Ember" or "Prism";
            Set(companion, "currentLook", originalDefault ? null : identity.Look); Set(companion, "displayAvatar", identity.Avatar);
            draw.PushClipRect(top, top + tile, true);
            draw.AddText(top + new Vector2(8, 6), 0xFFFFFFFF, identity.Avatar + "/" + Get(identity.Look, "Choice") + " " + stage);
            draw.AddText(top + new Vector2(8, 22), 0xFFB3C1D0, $"pet {petEdge:0} / home {homeEdge:0} / {(mirrored ? "left" : "right")}");
            Call(companion, "DrawHome", draw, layout);
            requested.Clear(); var first = draw.VtxBuffer.Size;
            object?[] values = [draw, layout, null];
            Check((bool)Call(companion, "DrawNap", values)!, "Actual production nap renderer draws this exact look.");
            var bodyFiles = requested.Where(file => file != (string)Get(style, "File")).Distinct().ToArray();
            Check(bodyFiles.Length == 1, "The entry renderer requests one complete body source, with no ghost-body blending.");
            if (stage == 0) Check(!bodyFiles[0].StartsWith("companion-homeposes-", StringComparison.Ordinal),
                "The first narrow turn sample retains the actual front source before the rear swap.");
            if (stage > 0)
            {
                Check(bodyFiles[0].StartsWith("companion-homeposes-", StringComparison.Ordinal), "Turn and climb use authored rear artwork.");
                var climb = (float)Get(routine, "EntryClimb");
                var pose = climb == 0 ? "TurnBack" : climb < .15f ? "RearCrouch"
                    : (int)((climb - .15f) / .85f * 4) % 2 == 0 ? "RearClimbA" : "RearClimbB";
                var choice = (string)Get(identity.Look, "Choice");
                var id = choice == "Default" || choice.StartsWith("Costume", StringComparison.Ordinal) ? "Default" : (string)Get(identity.Look, "Id");
                var expectedSource = Call(library, "Find", identity.Avatar, id, EnumValue("Ui.CompanionHomePose", pose))!;
                Check(bodyFiles[0] == (string)Get(expectedSource, "File"), "Actual rear source belongs to the exact selected identity.");
                var expectedFrame = Call(expectedSource, "Place", (Vector2)Get(routine, "Position") + sprite * new Vector2(.5f, .9f),
                    sprite, (float)Get(routine, "ArtScale") * (float)Call(companion, "RearEntryScale",
                        Call(library, "Find", identity.Avatar, id, EnumValue("Ui.CompanionHomePose", "RearCrouch")))!,
                    (bool)Get(routine, "FacingLeft"))!;
                Check(Vector2.Distance(draw.VtxBuffer[first].Uv, (Vector2)Get(expectedFrame, "UvStart")) < .00001f
                    && Vector2.Distance(draw.VtxBuffer[first + 2].Uv, (Vector2)Get(expectedFrame, "UvEnd")) < .00001f,
                    "Actual rear UV crop and mirror preserve the selected material identity and authored state.");
            }
            var bodyId = textures[bodyFiles[0]].Id;
            var bodyVertices = new HashSet<int>();
            for (var commandIndex = 0; commandIndex < draw.CmdBuffer.Size; commandIndex++)
            {
                var command = draw.CmdBuffer[commandIndex]; if (command.TextureId.Handle != bodyId) continue;
                for (var index = 0u; index < command.ElemCount; index++)
                {
                    var vertex = (int)(draw.IdxBuffer[(int)(command.IdxOffset + index)] + command.VtxOffset);
                    if (vertex >= first) bodyVertices.Add(vertex);
                }
            }
            Check(bodyVertices.Count == 4, "Exactly one connected whole-body quad is emitted for each entry sample.");
            foreach (var vertex in bodyVertices)
            {
                var point = draw.VtxBuffer[vertex].Pos;
                Check(float.IsFinite(point.X) && float.IsFinite(point.Y) && point.X >= top.X && point.X <= top.X + tile.X
                    && point.Y >= top.Y + 36 && point.Y <= top.Y + tile.Y, "Entry geometry fits its tile and leaves a visible label gutter.");
            }
            if (!(bool)Get(routine, "InFrontOfHome")) Check(draw.VtxBuffer.Size - first > 4,
                "Crossing the verified doorstep paints the house wall mask after the single body.");
            if (stage < 2) Check(((Vector2)Get(routine, "ArtStretch")).X < .2f,
                "The front/rear source change occurs only at the narrow turn silhouette.");
            draw.PopClipRect();
        }
        ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output, $"actual-rear-pet{petEdge:0}-home{homeEdge:0}-{(mirrored ? "left" : "right")}.png"));
    }
}
finally { ImGui.DestroyContext(context); }
Console.WriteLine($"Compiled rear integration: {checks} checks passed; actual production entry samples saved. No game, account or network services called.");

static int MeasureBodyWidth(string path, Rectangle crop)
{
    using var bitmap = new Bitmap(path);
    var data = bitmap.LockBits(crop, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var pixels = new byte[crop.Width * crop.Height * 4];
    try { for (var y = 0; y < crop.Height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * crop.Width * 4, crop.Width * 4); }
    finally { bitmap.UnlockBits(data); }
    var visited = new bool[crop.Width * crop.Height]; var queue = new int[visited.Length]; var largest = 0; var width = 0;
    for (var origin = 0; origin < visited.Length; origin++)
    {
        if (visited[origin] || pixels[origin * 4 + 3] < 128) continue;
        var count = 1; var read = 0; queue[0] = origin; visited[origin] = true;
        var left = origin % crop.Width; var right = left;
        while (read < count)
        {
            var current = queue[read++]; var x = current % crop.Width; var y = current / crop.Width;
            left = Math.Min(left, x); right = Math.Max(right, x);
            for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
            {
                var nx = x + dx; var ny = y + dy;
                if (nx < 0 || nx >= crop.Width || ny < 0 || ny >= crop.Height) continue;
                var index = ny * crop.Width + nx;
                if (visited[index] || pixels[index * 4 + 3] < 128) continue;
                visited[index] = true; queue[count++] = index;
            }
        }
        if (count > largest) { largest = count; width = right - left + 1; }
    }
    return width;
}

public class TextureProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static object Create(System.Type type, Func<MethodInfo, object?[], object?> handler)
    { var proxy = DispatchProxy.Create(type, typeof(TextureProxy)); ((TextureProxy)proxy).Handler = handler; return proxy; }
}
