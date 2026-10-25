using System.Collections;
using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
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
object? Field(object value, string name) => value.GetType().GetField(name, Instance)!.GetValue(value);
void Set(object value, string name, object? data) => (value.GetType().GetField(name, Instance) ?? value.GetType().GetField("<" + name + ">P", Instance)
    ?? value.GetType().GetField("<" + name + ">k__BackingField", Instance))!.SetValue(value, data);
object? Call(object value, string name, params object?[] values) => value.GetType().GetMethod(name, Instance)!.Invoke(value, values);
object EnumValue(string name, string value) => Enum.Parse(Type(name), value);
var checks = 0;
void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
var assets = Path.GetFullPath("assets"); var output = Directory.CreateDirectory(Path.GetFullPath(".local/companion-wardrobe-integration"));
var wardrobe = New("Ui.CompanionWardrobeLibrary", assets); var variants = New("Ui.CompanionVariantLibrary", assets);
var looks = ((IEnumerable)Get(wardrobe, "Looks")).Cast<object>().ToArray();
Check(looks.Length == 65 && (bool)Get(wardrobe, "IsAvailable"), "The actual DLL loads all65 bare/painted bodies.");
var config = New("Configuration"); Set(config, "CompanionHolidayBodiesEnabled", false); Set(config, "CompanionPalette", "Moonlight"); Set(config, "AnimationsEnabled", true);
var roamer = RuntimeHelpers.GetUninitializedObject(Type("Ui.RoamingCompanion"));
Set(roamer, "config", config); Set(roamer, "wardrobe", wardrobe); Set(roamer, "variants", variants); Set(roamer, "assetDirectory", assets);
var gait = New("Ui.CompanionGait"); Set(gait, "Phase", .23f); Set(roamer, "gait", gait);
var motion = New("Ui.CompanionBodyMotion"); for (var i = 0; i < 30; i++) Call(motion, "Update", .05f, true, true); Set(roamer, "bodyMotion", motion);
var rings = New("Ui.OrbitRingRenderer"); Set(roamer, "orbitRings", rings);
var routine = New("Ui.CompanionHomeRoutine", 1); Set(roamer, "homeRoutine", routine);
string[] avatars = ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism", "Nib", "Echo", "Fizz", "Glim", "Rune"];
foreach (var avatar in avatars)
{
    Set(roamer, "displayAvatar", avatar); Set(config, "CompanionCostume", "None"); Call(roamer, "ResolveLook", null, true);
    Check(ReferenceEquals(Field(roamer, "currentLook"), Call(wardrobe, "FindDefault", avatar)), "Plain selection retains original defaults and resolves new bodies: " + avatar);
    foreach (var theme in new[] { "AllSaintsWake", "Starlight", "MakeItRain", "MoonfireFaire" })
    {
        var before = Field(roamer, "currentLook"); Set(config, "CompanionCostume", theme); Call(roamer, "ResolveLook", null, false);
        Check(ReferenceEquals(before, Field(roamer, "currentLook")), "Unsafe automatic changes remain deferred.");
        Call(roamer, "ResolveLook", null, true);
        Check((string)Get(Field(roamer, "currentLook")!, "Choice") == "Costume" + theme, "Explicit costume selection resolves the named painted row.");
    }
}
// An absent optional pack neither invents new bodies nor removes the old independent base look.
var missing = Directory.CreateDirectory(Path.Combine(output.FullName, "empty-pack")); var absent = New("Ui.CompanionWardrobeLibrary", missing.FullName);
Set(roamer, "wardrobe", absent); Set(config, "CompanionCostume", "AllSaintsWake"); Set(roamer, "displayAvatar", "Nib"); Call(roamer, "ResolveLook", null, true);
Check(Field(roamer, "currentLook") == null, "Missing pack fails closed for a new-only companion.");
var oldLook = ((IEnumerable)Get(variants, "Looks")).Cast<object>().First(); Set(roamer, "displayAvatar", Get(oldLook, "Avatar")); Call(roamer, "ResolveLook", oldLook, true);
Check(ReferenceEquals(Field(roamer, "currentLook"), oldLook), "Missing wardrobe keeps the independent everyday base look."); Set(roamer, "wardrobe", wardrobe);

var context = ImGui.CreateContext();
try
{
    var io = ImGui.GetIO(); unsafe { io.IniFilename = null; io.LogFilename = null; }
    io.DeltaTime = 1f / 60; io.Fonts.AddFontDefault(); io.Fonts.Build(); io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
    var painter = new SoftwareRenderer(); var textures = new Dictionary<string, (ulong Id, int Width, int Height)>();
    foreach (var file in ((IEnumerable)Get(wardrobe, "Files")).Cast<string>().Append("companion-house.png"))
    {
        using var bitmap = new Bitmap(Path.Combine(assets, file)); var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
        finally { bitmap.UnlockBits(bits); }
        for (var p = 0; p < pixels.Length; p += 4) (pixels[p], pixels[p + 2]) = (pixels[p + 2], pixels[p]);
        var id = (ulong)textures.Count + 2; painter.AddTexture(id, pixels, bitmap.Width, bitmap.Height); textures.Add(file, (id, bitmap.Width, bitmap.Height));
    }
    var requested = new List<string>();
    object Wrap(Type type, string file) => LocalProxy.Create(type, (method, _) => method.Name switch {
        "get_Handle" => new ImTextureID(textures[file].Id), "get_Width" => textures[file].Width, "get_Height" => textures[file].Height,
        "GetWrapOrDefault" => Wrap(method.ReturnType, file), _ => throw new InvalidOperationException("Unexpected texture method.") });
    var services = Type("Services"); services.GetProperty("Textures", Static)!.SetValue(null, LocalProxy.Create<ITextureProvider>((method, values) => {
        if (method.Name != "GetFromFile") throw new InvalidOperationException("Unexpected texture-provider call.");
        var file = Path.GetFileName(values[0]!.ToString()!); requested.Add(file); return Wrap(method.ReturnType, file);
    }));
    services.GetProperty("Framework", Static)!.SetValue(null, LocalProxy.Create<IFramework>((_, _) => throw new InvalidOperationException("No framework/game access is allowed.")));
    int[] Vertices(ImDrawListPtr draw, string file, int first)
    {
        var indices = new HashSet<int>();
        for (var c = 0; c < draw.CmdBuffer.Size; c++) { var command = draw.CmdBuffer[c]; if (command.TextureId.Handle != textures[file].Id) continue;
            for (var n = 0u; n < command.ElemCount; n++) { var vertex = (int)(draw.IdxBuffer[(int)(command.IdxOffset + n)] + command.VtxOffset); if (vertex >= first) indices.Add(vertex); } }
        return indices.Order().ToArray();
    }
    void Configure(object look) { Set(roamer, "currentLook", look); Set(roamer, "displayAvatar", Get(look, "Avatar")); Set(roamer, "poseTime", .5f); Set(roamer, "visualGrip", null); requested.Clear(); }
    foreach (var size in new[] { 96f, 192f }) foreach (var mirror in new[] { false, true }) foreach (var pose in new[] { "Walk", "Carry" })
    {
        var tile = new Vector2(size * 2 + 20, size * 2 + 40); io.DisplaySize = new(tile.X * 5, tile.Y * 13);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var i = 0; i < looks.Length; i++)
        {
            var look = looks[i]; var avatar = (string)Get(look, "Avatar"); var row = Array.IndexOf(avatars, avatar); var column = (string)Get(look, "Choice") == "Default" ? 0 : new[] { "CostumeAllSaintsWake", "CostumeStarlight", "CostumeMakeItRain", "CostumeMoonfireFaire" }.ToList().IndexOf((string)Get(look, "Choice")) + 1;
            var position = new Vector2(column * tile.X + size * .5f + 10, row * tile.Y + size * .5f + 30); Configure(look); Set(routine, "State", EnumValue("Ui.CompanionHomeState", "Roaming"));
            var first = draw.VtxBuffer.Size; object?[] values = [draw, position, new Vector2(size), mirror, EnumValue("Ui.CompanionPose", pose), 0f, null];
            Check((bool)Call(roamer, "TryDrawLook", values)!, "Actual runtime paints every bare/costume body.");
            var file = (string)Get(Get(look, "Normal"), "File"); var ids = Vertices(draw, file, first);
            Check(ids.Length == (pose == "Walk" && avatar is "Ember" or "Mochi" or "Puff" ? 256 : 4), "One runtime body quad or continuous mesh, without duplicate body art.");
            Check(requested.SequenceEqual(new[] { file }), "Painted costumes do not request a separate hat piece.");
            if (avatar == "Orbit") Check(ids.Min() > first && ids.Max() < draw.VtxBuffer.Size - 1, "Orbit rings remain behind and in front of its ringless core.");
            if (pose == "Carry") Check(Vector2.Distance((Vector2)Field(roamer, "visualGrip")!, position + new Vector2(size * .5f)) < .001f, "Carried painted bodies keep the hand grip pinned.");
            draw.AddText(new(column * tile.X + 5, row * tile.Y + 4), 0xFFFFFFFF, avatar + " " + Get(look, "Choice"));
        }
        ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"actual-{pose}-{size:0}-{(mirror ? "left" : "right")}.png"));
    }
    var paintedLooks = looks.Where(look => ((string)Get(look, "Choice")).StartsWith("Costume", StringComparison.Ordinal)).ToArray();
    io.DisplaySize = new(640); ImGui.NewFrame(); var blinkDraw = ImGui.GetForegroundDrawList();
    foreach (var look in looks) foreach (var mirror in new[] { false, true })
    {
        Configure(look); Set(routine, "State", EnumValue("Ui.CompanionHomeState", "Roaming"));
        var first = blinkDraw.VtxBuffer.Size; object?[] values = [blinkDraw, new Vector2(180), new Vector2(96), mirror, EnumValue("Ui.CompanionPose", "Idle"), 1f, null];
        Check((bool)Call(roamer, "TryDrawLook", values)!, "Actual normal-body runtime supports full closure for every wardrobe profile.");
        var file = (string)Get(Get(look, "Normal"), "File");
        Check(requested.SequenceEqual(new[] { file, file }), "Runtime resolves the wardrobe-specific closed-eye texture without swapping the body.");
        var avatar = (string)Get(look, "Avatar");
        Check(Enumerable.Range(first, blinkDraw.VtxBuffer.Size - first).Any(i => blinkDraw.VtxBuffer[i].Col ==
            (avatar == "Nib" ? 0xFFF3E8E2u : 0xFF291B27u)), "Fresh DLL draws readable Nib silver and retains original ink for all other characters.");
    }
    ImGui.Render();
    foreach (var size in new[] { 96f, 192f }) foreach (var mirror in new[] { false, true })
    {
        io.DisplaySize = new(640); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        foreach (var look in paintedLooks) foreach (var state in new[] { "Entering", "Sleeping", "Waking", "Leaving", "Returning" })
        {
            Configure(look); Set(routine, "State", EnumValue("Ui.CompanionHomeState", state)); Set(routine, "Position", new Vector2(200)); Set(routine, "FacingLeft", mirror); Set(routine, "ArtScale", .85f); Set(routine, "stateTime", .4f); Set(routine, "approachLeg", state == "Leaving" ? 2 : 0); Set(roamer, "homeVisible", false);
            var avatar = (string)Get(look, "Avatar"); var bare = Call(wardrobe, "FindBare", avatar)!; var expectedLook = state is "Leaving" or "Returning" ? look : bare;
            var poseIndex = state == "Sleeping" ? 3 : state == "Waking" ? 7 : 6;
            var source = ((IEnumerable)Get(expectedLook, "Frames")).Cast<object>().ElementAt(poseIndex); var first = draw.VtxBuffer.Size;
            object?[] values = [draw, New("Ui.CompanionLayout", Vector2.Zero, io.DisplaySize, new Vector2(size)), null];
            Check((bool)Call(roamer, "DrawLookNap", values)!, "Actual nap renderer handles every painted body.");
            var file = (string)Get(source, "File"); var ids = Vertices(draw, file, first); Check(ids.Length == 4, "Nap paints one whole source pose.");
            var rect = (Vector4)Get(source, "Bounds"); var dimensions = (Vector2)Get(source, "SheetSize");
            var uvMin = ids.Select(i => draw.VtxBuffer[i].Uv).Aggregate(Vector2.Min); var uvMax = ids.Select(i => draw.VtxBuffer[i].Uv).Aggregate(Vector2.Max);
            Check(Vector2.Distance(uvMin, new Vector2(rect.X, rect.Y) / dimensions) < .0001f && Vector2.Distance(uvMax, new Vector2(rect.Z, rect.W) / dimensions) < .0001f,
                "Entry/sleep/wake use the bare row; the original painted costume returns outside.");
            Check(ReferenceEquals(Field(roamer, "currentLook"), look), "Bedtime never overwrites the selected costume.");
        }
        ImGui.Render();
    }
    // Inspectable house-entry strip uses the same actual runtime methods as the UV assertions.
    foreach (var size in new[] { 96f, 192f })
    {
        var tile = new Vector2(size * 2 + 20, size * 2 + 40); io.DisplaySize = new(tile.X * 4, tile.Y * avatars.Length);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var row = 0; row < avatars.Length; row++) for (var column = 0; column < 4; column++)
        {
            var look = paintedLooks.Single(value => (string)Get(value, "Avatar") == avatars[row] && (string)Get(value, "Choice") == "CostumeAllSaintsWake");
            Configure(look); var position = new Vector2(column * tile.X + size * .5f + 10, row * tile.Y + size * .5f + 30);
            var state = new[] { "Roaming", "Entering", "Sleeping", "Leaving" }[column]; Set(routine, "State", EnumValue("Ui.CompanionHomeState", state));
            Set(routine, "Position", position); Set(routine, "FacingLeft", false); Set(routine, "ArtScale", .85f); Set(routine, "approachLeg", column == 3 ? 2 : 0);
            if (column == 0) Call(roamer, "TryDrawLook", draw, position, new Vector2(size), false, EnumValue("Ui.CompanionPose", "Idle"), 0f, null);
            else Call(roamer, "DrawLookNap", draw, New("Ui.CompanionLayout", Vector2.Zero, io.DisplaySize, new Vector2(size)), null);
            draw.AddText(new(column * tile.X + 5, row * tile.Y + 4), 0xFFFFFFFF, avatars[row] + " " + state);
        }
        ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"actual-house-costume-{size:0}.png"));
    }
    Console.WriteLine($"Compiled wardrobe integration: {checks} checks passed; all65 Walk/Carry bodies and52 costumes entering/sleeping/waking/leaving/returning at96/192 both facings. No game/framework calls.");
}
finally { ImGui.DestroyContext(context); }

public class LocalProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class { var proxy = Create<T, LocalProxy>(); ((LocalProxy)(object)proxy).Handler = handler; return proxy; }
    public static object Create(Type type, Func<MethodInfo, object?[], object?> handler) { var proxy = DispatchProxy.Create(type, typeof(LocalProxy)); ((LocalProxy)proxy).Handler = handler; return proxy; }
}
