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

internal sealed class Fixture : IDisposable
{
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private readonly Assembly assembly;
    private readonly Dictionary<(Type, string), MethodInfo> methods = [];
    private readonly Dictionary<string, (ulong Id, int Width, int Height)> textures = [];
    private readonly ImGuiContextPtr context;
    public readonly string Assets = Path.GetFullPath("assets");
    public readonly string Output = Path.GetFullPath(".local/companion-material-motion-tests");
    public readonly object Variants, Holidays, Wardrobe, Blinks, Config, Roamer, Gait, Rings;
    public readonly object[] Looks;
    public readonly SoftwareRenderer Painter;
    public int Checks;

    public Fixture()
    {
        var plugin = Path.GetFullPath("src/OmniKit/bin/Release/OmniKit.dll");
        var host = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
        AssemblyLoadContext.Default.Resolving += (loadContext, name) => {
            foreach (var directory in new[] { Path.GetDirectoryName(plugin)!, host })
            { var path = Path.Combine(directory, name.Name + ".dll"); if (File.Exists(path)) return loadContext.LoadFromAssemblyPath(path); }
            return null;
        };
        assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(plugin);
        Variants = New("CompanionVariantLibrary", Assets); Holidays = New("CompanionHolidayLibrary", Assets);
        Wardrobe = New("CompanionWardrobeLibrary", Assets); Blinks = New("CompanionBlinkLibrary", Assets);
        var daily = List(Variants, "Looks"); var holiday = List(Holidays, "Looks");
        var costumes = List(Wardrobe, "Looks").Where(look => Text(look, "Choice").StartsWith("Costume", StringComparison.Ordinal)).ToArray();
        Check(daily.Length == 32 && holiday.Length == 72 && costumes.Length == 52, "Load exact public 32/72/52 artwork sets.");
        Looks = [.. daily, .. holiday, .. costumes];
        context = ImGui.CreateContext(); var io = ImGui.GetIO();
        unsafe { io.IniFilename = null; io.LogFilename = null; }
        io.DisplaySize = new(600); io.DeltaTime = 1f / 60; io.Fonts.AddFontDefault(); io.Fonts.Build();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset; Painter = new();
        Config = Activator.CreateInstance(assembly.GetType("OmniKit.Configuration", true)!)!;
        Set(Config, "CompanionPalette", "Moonlight"); Set(Config, "AnimationsEnabled", true);
        Set(Config, "CompanionMagicEnabled", false); Set(Config, "CompanionBlinkEnabled", true);
        Roamer = RuntimeHelpers.GetUninitializedObject(Type("RoamingCompanion"));
        Set(Roamer, "config", Config); Set(Roamer, "assetDirectory", Assets); Set(Roamer, "variants", Variants);
        Set(Roamer, "wardrobe", Wardrobe); Set(Roamer, "blinks", Blinks); Set(Roamer, "homeRoutine", New("CompanionHomeRoutine", 1));
        Gait = New("CompanionGait"); Rings = New("OrbitRingRenderer"); Set(Roamer, "gait", Gait); Set(Roamer, "orbitRings", Rings);
        var services = assembly.GetType("OmniKit.Services", true)!;
        services.GetProperty("Textures", Static)!.SetValue(null, LocalProxy.Create<ITextureProvider>((method, values) => {
            if (method.Name != "GetFromFile") throw new InvalidOperationException("Unexpected texture service access.");
            return Wrap(method.ReturnType, Path.GetFileName(values[0]!.ToString()!));
        }));
        services.GetProperty("Framework", Static)!.SetValue(null, LocalProxy.Create<IFramework>((_, _) => throw new InvalidOperationException("Framework/game calls are forbidden.")));
        Directory.CreateDirectory(Output);
    }
    public Type Type(string name) => assembly.GetType("OmniKit.Ui." + name, true)!;
    public object New(string name, params object?[] values) => Activator.CreateInstance(Type(name), Instance, null, values, null)!;
    public object Get(object value, string name) => value.GetType().GetProperty(name, Instance)?.GetValue(value)
        ?? value.GetType().GetField(name, Instance)!.GetValue(value)!;
    public string Text(object value, string name) => (string)Get(value, name);
    public void Set(object value, string name, object? data) => (value.GetType().GetField(name, Instance)
        ?? value.GetType().GetField("<" + name + ">P", Instance)
        ?? value.GetType().GetField("<" + name + ">k__BackingField", Instance))!.SetValue(value, data);
    public object? Call(object value, string name, params object?[] arguments)
    {
        var key = (value.GetType(), name);
        if (!methods.TryGetValue(key, out var method)) methods.Add(key, method = key.Item1.GetMethod(name, Instance)!);
        return method.Invoke(value, arguments);
    }
    public object? StaticCall(string name, string method, params object?[] values) => Type(name).GetMethod(method, Static)!.Invoke(null, values);
    public object[] List(object value, string name) => ((IEnumerable)Get(value, name)).Cast<object>().ToArray();
    public void Check(bool value, string reason) { Checks++; if (!value) throw new InvalidOperationException(reason); }
    public object Motion(string avatar, string? id, bool active = true, float seconds = 2)
    {
        var result = New("CompanionBodyMotion"); Call(result, "SetLook", avatar, id);
        for (var n = 0; n < (int)(seconds * 60); n++) Call(result, "Update", 1f / 60, true, active);
        return result;
    }
    public Vector2 Warp(object state, string avatar, Vector2 point, Vector2 position, Vector2 size, float phase, bool mirror, bool animate = true)
        => (Vector2)Call(state, "Warp", avatar, point, position, size, phase, mirror, animate)!;
    public ImDrawVert[] Copy(ImDrawListPtr draw, int first = 0) => Enumerable.Range(first, draw.VtxBuffer.Size - first).Select(n => draw.VtxBuffer[n]).ToArray();
    public (ImDrawVert[] Vertices, (Vector2 Minimum, Vector2 Maximum) Bounds) Draw(object look, object state, ImDrawListPtr draw,
        Vector2 at, float edge, bool mirror, float phase, float closure = 0, string pose = "Walk")
    {
        var avatar = Text(look, "Avatar");
        Set(Roamer, "currentLook", look); Set(Roamer, "displayAvatar", avatar); Set(Roamer, "bodyMotion", state); Set(Gait, "Phase", phase);
        var first = draw.VtxBuffer.Size;
        object?[] values = [draw, at, new Vector2(edge), mirror, Enum.Parse(Type("CompanionPose"), pose), closure, null];
        Check((bool)Call(Roamer, "TryDrawLook", values)!, avatar + "/" + Text(look, "Id") + " must draw through actual runtime.");
        return (Copy(draw, first), ((Vector2, Vector2))values[^1]!);
    }
    public ImTextureID Texture(string file)
    {
        if (!textures.TryGetValue(file, out var texture))
        {
            using var bitmap = new Bitmap(Path.Combine(Assets, file));
            var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = new byte[bitmap.Width * bitmap.Height * 4];
            try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
            finally { bitmap.UnlockBits(bits); }
            for (var n = 0; n < pixels.Length; n += 4) (pixels[n], pixels[n + 2]) = (pixels[n + 2], pixels[n]);
            texture = ((ulong)textures.Count + 2, bitmap.Width, bitmap.Height); textures.Add(file, texture);
            Painter.AddTexture(texture.Id, pixels, texture.Width, texture.Height);
        }
        return new(texture.Id);
    }
    private object Wrap(Type type, string file)
    {
        Texture(file);
        return LocalProxy.Create(type, (method, _) => method.Name switch {
            "get_Handle" => new ImTextureID(textures[file].Id), "get_Width" => textures[file].Width, "get_Height" => textures[file].Height,
            "GetWrapOrDefault" => Wrap(method.ReturnType, file), _ => throw new InvalidOperationException("Unexpected texture operation.") });
    }
    public void Dispose() => ImGui.DestroyContext(context);
}

public class LocalProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, LocalProxy>(); ((LocalProxy)(object)proxy).Handler = handler; return proxy; }
    public static object Create(Type type, Func<MethodInfo, object?[], object?> handler)
    { var proxy = DispatchProxy.Create(type, typeof(LocalProxy)); ((LocalProxy)proxy).Handler = handler; return proxy; }
}
