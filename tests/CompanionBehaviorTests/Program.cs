using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using LauncherUiSmoke;

var positional = args.Where(argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToArray();
var plugin = Path.GetFullPath(positional.ElementAtOrDefault(0) ?? "src/OmniKit/bin/Release/OmniKit.dll");
var output = Path.GetFullPath(positional.ElementAtOrDefault(1) ?? ".local/visualharness/companion-behavior");
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
if (args.Contains("--fourier-blink-only"))
{
    FourierBlinkRender.Run(assembly, output);
    return;
}
var instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
var statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
var roaming = assembly.GetType("OmniKit.Ui.RoamingCompanion", true)!;
var physics = assembly.GetType("OmniKit.Ui.CompanionPhysics", true)!;
var configType = assembly.GetType("OmniKit.Configuration", true)!;
var checks = 0;
var surfacesOnly = args.Contains("--surfaces-only");
void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
void Set(object target, string name, object value) => (target.GetType().GetField(name, instance)
    ?? target.GetType().GetField("<" + name + ">P", instance))!.SetValue(target, value);
(List<object> Vertices, List<object> Triangles) Snapshot(ImDrawListPtr draw)
{
    var vertices = new List<object>(); var triangles = new List<object>();
    for (var i = 0; i < draw.VtxBuffer.Size; i++)
    {
        var v = draw.VtxBuffer[i];
        vertices.Add(new { p = new[] { v.Pos.X, v.Pos.Y }, uv = new[] { v.Uv.X, v.Uv.Y }, color = v.Col });
    }
    for (var c = 0; c < draw.CmdBuffer.Size; c++)
    {
        var cmd = draw.CmdBuffer[c];
        for (uint i = 0; i + 2 < cmd.ElemCount; i += 3)
            triangles.Add(new { texture = cmd.TextureId.Handle, indices = new[] {
                draw.IdxBuffer[(int)(cmd.IdxOffset+i)]+cmd.VtxOffset, draw.IdxBuffer[(int)(cmd.IdxOffset+i+1)]+cmd.VtxOffset,
                draw.IdxBuffer[(int)(cmd.IdxOffset+i+2)]+cmd.VtxOffset } });
    }
    return (vertices, triangles);
}
var blink = roaming.GetMethod("BlinkAmount", statics)!;
if (!surfacesOnly && !args.Contains("--hands-only"))
foreach (var interval in new[] { 1f, 3f, 10f })
{
    var seenClosed = false;
    for (var i = 0; i < 2000; i++)
    {
        var time = i * .01f;
        var amount = (float)blink.Invoke(null, [time, interval, true])!;
        seenClosed |= amount > .99f;
        Check(float.IsFinite(amount) && amount >= 0 && amount <= 1, "Blink closure is bounded.");
        Check((float)blink.Invoke(null, [time, interval, false])! == 0, "Disabling blinking keeps the eye open.");
    }
    Check(seenClosed, "Each supported blink interval actually closes the eye.");
}

var context = ImGui.CreateContext();
try
{
    unsafe
    {
        var io = ImGui.GetIO(); io.IniFilename = null; io.LogFilename = null;
        io.DisplaySize = new Vector2(640, 440); io.DeltaTime = 1f / 60;
        io.Fonts.AddFontDefault(); io.Fonts.Build();
    }
    if (args.Contains("--hands-only"))
    {
        var handConfig = Activator.CreateInstance(configType)!;
        var handCompanion = RuntimeHelpers.GetUninitializedObject(roaming);
        Set(handCompanion, "config", handConfig); Set(handCompanion, "assetDirectory", Path.GetFullPath("assets"));
        Set(handCompanion, "currentUiScale", 1f);
        object HandTexture(Type type) => LocalProxy.Create(type, (method, _) => method.Name switch
        {
            "get_Handle" => new ImTextureID(4UL), "get_Width" or "get_Height" => 1280,
            "GetWrapOrDefault" => HandTexture(method.ReturnType), _ => throw new InvalidOperationException("Unexpected texture call.")
        });
        assembly.GetType("OmniKit.Services", true)!.GetProperty("Textures", statics)!.SetValue(null,
            LocalProxy.Create<ITextureProvider>((method, _) => method.Name == "GetFromFile" ? HandTexture(method.ReturnType)
                : throw new InvalidOperationException("Unexpected service access.")));
        var drawHandOnly = roaming.GetMethod("DrawGrabHand", instance)!;
        var gripMethod = assembly.GetType("OmniKit.Ui.CompanionGrabAnchor", true)!.GetMethod("GripInCell", statics)!;
        ImGui.GetIO().DisplaySize = new(4096, 3072); var contactPoint = new Vector2(2048, 1536);
        foreach (var petSize in new[] { 48f, 192f })
        foreach (var handSize in new[] { 8f, 38.4f, 192f })
        foreach (var sceneScale in new[] { .5f, 2f })
        foreach (var uiScale in new[] { .25f, 4f })
        foreach (var pose in new[] { 0, 1, 2, 3 })
        {
            Set(handConfig, "CompanionSize", petSize); Set(handConfig, "CompanionHandSize", handSize); Set(handConfig, "CompanionSceneScale", sceneScale);
            var dragging = pose is 1 or 2; var time = pose switch { 0 => 1f, 1 => .1f, 2 => .3f, _ => .05f };
            Set(handCompanion, "handWasDragging", dragging); Set(handCompanion, "handTime", time); Set(handCompanion, "handPoint", contactPoint);
            ImGui.NewFrame(); var handDraw = ImGui.GetForegroundDrawList();
            drawHandOnly.Invoke(handCompanion, [handDraw, Vector2.Zero, ImGui.GetIO().DisplaySize, uiScale, 0f, dragging,
                pose == 0 ? contactPoint : null, dragging ? contactPoint : null]);
            var edge = handSize * sceneScale * uiScale;
            Check(handDraw.VtxBuffer.Size == 4 && Math.Abs(handDraw.VtxBuffer[1].Pos.X - handDraw.VtxBuffer[0].Pos.X - edge) < .001f,
                "Independent hand size multiplies scene/UI scale and stays independent of companion size.");
            var paintedSize = handDraw.VtxBuffer[2].Pos - handDraw.VtxBuffer[0].Pos;
            var actualContact = handDraw.VtxBuffer[0].Pos + paintedSize * (Vector2)gripMethod.Invoke(null, [pose])!;
            var cueLift = edge * (pose == 1 ? .1f : pose == 3 ? .07f : 0);
            Check(Vector2.Distance(actualContact, contactPoint - new Vector2(0, cueLift)) < .001f,
                "All four independent hand poses preserve the measured finger contact and cue lift.");
            ImGui.Render();
        }
        Console.WriteLine($"Companion native independent hands: {checks} focused checks passed.");
        return;
    }
    var renderer = new SoftwareRenderer();
    var services = assembly.GetType("OmniKit.Services", true)!;
    // Logged-out service stub ensures the native-game enumeration is never called.
    services.GetProperty("ClientState", statics)!.SetValue(null, LocalProxy.Create<IClientState>((method, _) =>
        method.Name == "get_IsLoggedIn" ? false : throw new InvalidOperationException("Unexpected game service access.")));
    var readerType = assembly.GetType("OmniKit.Ui.CompanionSurfaces", true)!;
    var reader = Activator.CreateInstance(readerType, nonPublic: true)!;
    var read = readerType.GetMethod("Read", instance)!;
    object[] Surfaces(bool visible, Vector2 at)
    {
        ImGui.NewFrame();
        if (visible)
        {
            ImGui.SetNextWindowPos(at); ImGui.SetNextWindowSize(new Vector2(280, 220));
            ImGui.Begin("Synthetic frame", ImGuiWindowFlags.NoSavedSettings); ImGui.TextUnformatted("Read-only surface test"); ImGui.End();
        }
        ImGui.SetNextWindowPos(new Vector2(350, 20)); ImGui.SetNextWindowSize(new Vector2(120, 80));
        ImGui.Begin("##OmniKitCompanionHouse", ImGuiWindowFlags.NoSavedSettings); ImGui.End();
        var result = ((IEnumerable)read.Invoke(reader, [Vector2.Zero, new Vector2(640, 440)])!).Cast<object>().ToArray();
        ImGui.Render(); return result;
    }
    Surfaces(true, new Vector2(20, 100));
    var observed = Surfaces(true, new Vector2(20, 100));
    Check(observed.Length == 1, "The actual ImGui reader includes a supported frame and excludes its own house overlay.");
    Check((bool)observed[0].GetType().GetProperty("CanContain")!.GetValue(observed[0])!,
        "An ordinary titled window retains intentional interior containment.");
    Check((Vector2)observed[0].GetType().GetProperty("Minimum")!.GetValue(observed[0])! == new Vector2(20, 100), "Surface coordinates match the actual frame.");
    observed = Surfaces(true, new Vector2(90, 130));
    Check((Vector2)observed[0].GetType().GetProperty("Minimum")!.GetValue(observed[0])! == new Vector2(90, 130), "Surface snapshots track a moved frame.");
    Surfaces(false, Vector2.Zero); Surfaces(false, Vector2.Zero);
    Check(Surfaces(false, Vector2.Zero).Length == 0, "Closed frames expire from the observed list.");

    object[] HudSurfaces(bool show)
    {
        ImGui.NewFrame();
        if(show)
        {
            var flags=ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoMouseInputs|ImGuiWindowFlags.NoBackground;
            void Frame(string name,Vector2 at,Vector2 size,bool ink,bool clipped=false,bool clickThrough=true)
            {
                ImGui.SetNextWindowPos(at); ImGui.SetNextWindowSize(size);
                ImGui.Begin(name,clickThrough ? flags : flags & ~ImGuiWindowFlags.NoMouseInputs);
                if(clipped) ImGui.GetWindowDrawList().PushClipRect(Vector2.Zero,Vector2.One,true);
                if(ink) ImGui.GetWindowDrawList().AddRectFilled(at+new Vector2(4),at+size-new Vector2(4),0xFFDAA45C);
                if(clipped) ImGui.GetWindowDrawList().PopClipRect();
                ImGui.End();
            }
            Frame("Synthetic click-through unit frame",new Vector2(50,100),new Vector2(220,40),true);
            Frame("Synthetic front unit frame",new Vector2(50,100),new Vector2(220,40),true);
            Frame("Synthetic empty input overlay",new Vector2(350,100),new Vector2(150,30),false);
            Frame("Synthetic fully clipped HUD",new Vector2(350,180),new Vector2(150,30),true,true);
            Frame("##OmniKitVisibleTargetControls",new Vector2(350,240),new Vector2(150,30),true);
            Frame("##OmniKitNextAction",new Vector2(350,280),new Vector2(150,30),true);
            Frame("##OmniKitQuestItems",new Vector2(350,320),new Vector2(150,30),true);
            Frame("Synthetic interactive HUD strip",new Vector2(50,180),new Vector2(220,40),true,clickThrough:false);
            ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(new Vector2(640,440));
            ImGui.Begin("Synthetic fullscreen canvas",flags);
            ImGui.GetWindowDrawList().AddRectFilled(new Vector2(10),new Vector2(50),0xFFDAA45C);
            ImGui.BeginChild("Synthetic child",new Vector2(200,40)); ImGui.TextUnformatted("Child is not a platform"); ImGui.EndChild();
            ImGui.End();
        }
        var result=((IEnumerable)read.Invoke(reader,[Vector2.Zero,new Vector2(640,440)])!).Cast<object>().ToArray();
        ImGui.Render(); return result;
    }
    HudSurfaces(true);
    observed=HudSurfaces(true);
    Check(observed.Length==3,"Visible HUD strips are supported; empty, clipped, own quest/target, child and fullscreen overlays are excluded.");
    foreach(var entry in observed.Take(2))
        Check((Vector2)entry.GetType().GetProperty("Minimum")!.GetValue(entry)! == new Vector2(54,104)
            && (Vector2)entry.GetType().GetProperty("Maximum")!.GetValue(entry)! == new Vector2(266,136),
            "A transparent NoMouseInputs frame uses its actual visible ink bounds.");
    foreach(var entry in observed)
        Check(!(bool)entry.GetType().GetProperty("CanContain")!.GetValue(entry)!,
            "Click-through and transparent titleless HUD strips cannot invent interior walls.");
    Check((int)observed[0].GetType().GetProperty("Order")!.GetValue(observed[0])!
        < (int)observed[1].GetType().GetProperty("Order")!.GetValue(observed[1])!,"Overlapping HUD frames preserve native ImGui stacking order.");
    renderer.Save(ImGui.GetDrawData(),Path.Combine(output,"click-through-surfaces.png"));
    HudSurfaces(false); HudSurfaces(false);
    Check(HudSurfaces(false).Length==0,"A closed click-through HUD expires and cannot remain a stale landing platform.");
    if(surfacesOnly)
    {
        Console.WriteLine($"Companion native surface snapshots: {checks} checks passed; no game calls.");
        return;
    }

    var config = Activator.CreateInstance(configType)!;
    var companion = RuntimeHelpers.GetUninitializedObject(roaming);
    Set(companion, "currentUiScale", 1f);
    Set(companion, "config", config); Set(companion, "physics", Activator.CreateInstance(physics, nonPublic: true)!);
    Set(companion, "homeRoutine", Activator.CreateInstance(assembly.GetType("OmniKit.Ui.CompanionHomeRoutine", true)!, new object[] { 1 })!);
    Set(companion, "comment", "A little adventure, then a little nap."); Set(companion, "commentUntil", 999d);
    var drawComment = roaming.GetMethod("DrawComment", instance)!;
    foreach (var scale in new[] { 1f, 1.5f })
    foreach (var position in new[] { Vector2.Zero, new Vector2(544, 344), new Vector2(0, 344), new Vector2(544, 0) })
    {
        ImGui.GetIO().FontGlobalScale = scale;
        ImGui.NewFrame();
        drawComment.Invoke(companion, [ImGui.GetForegroundDrawList(), position, new Vector2(96), Vector2.Zero, new Vector2(640, 440), scale]);
        var draw = ImGui.GetForegroundDrawList();
        for (var i = 0; i < draw.VtxBuffer.Size; i++)
        {
            var p = draw.VtxBuffer[i].Pos;
            Check(p.X >= 0 && p.Y >= 0 && p.X <= 640 && p.Y <= 440, "Comments remain entirely inside all viewport corners.");
        }
        ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"comment-{position.X}-{position.Y}-{scale:0.0}.png"));
    }
    ImGui.GetIO().FontGlobalScale = 1;
    foreach (var down in new bool?[] { false, null, true, false })
    {
        ImGui.GetIO().AddMousePosEvent(240, 170);
        if (down.HasValue) ImGui.GetIO().AddMouseButtonEvent(0, down.Value);
        ImGui.NewFrame();
        var ownsInput = (bool)drawComment.Invoke(companion, [ImGui.GetForegroundDrawList(), new Vector2(200), new Vector2(96), Vector2.Zero, new Vector2(640, 440), 1f])!;
        Check(ownsInput, "A hovered bubble owns its own click instead of forwarding it to the pet or house.");
        ImGui.Render();
    }
    Check((double)roaming.GetField("commentUntil", instance)!.GetValue(companion)! == 0, "An actual native click dismisses the comment.");
    Set(config, "CompanionCommentsEnabled", false);
    ImGui.NewFrame(); drawComment.Invoke(companion, [ImGui.GetForegroundDrawList(), Vector2.Zero, new Vector2(96), Vector2.Zero, new Vector2(640, 440), 1f]);
    Check(ImGui.GetForegroundDrawList().VtxBuffer.Size == 0, "Disabling comments removes the bubble and its hitbox."); ImGui.Render();

    var saves = 0;
    var persist = configType.GetField("Persist", instance)!;
    persist.SetValue(config, System.Linq.Expressions.Expression.Lambda(persist.FieldType,
        System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant((Action)(() => saves++))),
        System.Linq.Expressions.Expression.Parameter(configType)).Compile());
    Set(companion, "homeVisible", true);
    var layoutType = assembly.GetType("OmniKit.Ui.CompanionLayout", true)!;
    var createLayout = layoutType.GetMethod("Create", statics)!;
    var handleHouse = roaming.GetMethod("HandleHouse", instance)!;
    float Value(string field) => (float)configType.GetField(field)!.GetValue(config)!;
    object Layout() => createLayout.Invoke(null, [Vector2.Zero, new Vector2(640, 440),
        new Vector4(Value("CompanionBoxLeft"), Value("CompanionBoxTop"), Value("CompanionBoxRight"), Value("CompanionBoxBottom")), 96f, 1f, 1f])!;
    void HouseFrame(Vector2 mouse, bool? down, bool petPriority = false)
    {
        var io = ImGui.GetIO(); io.AddMousePosEvent(mouse.X, mouse.Y);
        if (down.HasValue) io.AddMouseButtonEvent(0, down.Value);
        ImGui.NewFrame(); handleHouse.Invoke(companion, [Layout(), Vector2.Zero, new Vector2(640, 440), 1f, petPriority]); ImGui.Render();
    }
    var overHouse = new Vector2(530, 330);
    HouseFrame(overHouse, false, true); HouseFrame(overHouse, true, true);
    Check(!(bool)roaming.GetField("housePressed", instance)!.GetValue(companion)!, "A pet overlapping the house keeps click priority.");
    HouseFrame(overHouse, false); HouseFrame(overHouse, null); HouseFrame(overHouse, true);
    HouseFrame(new Vector2(1000), null);
    Check(saves == 0 && Math.Abs(Value("CompanionHomeX") - 1) < .00001f && Math.Abs(Value("CompanionHomeY") - 1) < .00001f,
        "Actual native mouse dragging moves and clamps the independent house without saving during movement.");
    HouseFrame(new Vector2(1000), null); HouseFrame(new Vector2(1000), false); HouseFrame(new Vector2(1000), null);
    Check(saves == 1, "The actual house drag persists exactly once on release.");
    var movedHouse = roaming.GetMethod("HomeBounds", instance)!.Invoke(companion, [Layout()])!;
    Check((Vector2)movedHouse.GetType().GetProperty("Maximum")!.GetValue(movedHouse)! == new Vector2(640, 440),
        "The actual independent house reaches the full right and bottom viewport edges.");
    Check(Math.Abs(Value("CompanionBoxLeft") - .72f) < .00001f && Math.Abs(Value("CompanionBoxRight") - .95f) < .00001f
        && Math.Abs(Value("CompanionBoxTop") - .55f) < .00001f && Math.Abs(Value("CompanionBoxBottom") - .92f) < .00001f,
        "Dragging the house never moves or resizes the saved roaming area.");

    object TextureResult(Type type, ulong handle) => LocalProxy.Create(type, (method, _) => method.Name switch
    {
        "get_Handle" => new ImTextureID(handle), "get_Width" or "get_Height" => 1280,
        "GetWrapOrDefault" => TextureResult(method.ReturnType, handle),
        _ => throw new InvalidOperationException("Unexpected synthetic texture call: " + method.Name),
    });
    services.GetProperty("Textures", statics)!.SetValue(null, LocalProxy.Create<ITextureProvider>((method, values) =>
        method.Name == "GetFromFile" ? TextureResult(method.ReturnType, values[0]!.ToString()!.Contains("house", StringComparison.Ordinal) ? 3UL : 4UL)
            : throw new InvalidOperationException("Unexpected texture-provider call.")));
    Set(companion, "assetDirectory", Path.GetFullPath("assets"));
    var drawHome = roaming.GetMethod("DrawHome", instance)!;
    var drawHand = roaming.GetMethod("DrawGrabHand", instance)!;
    foreach (var rightSide in new[] { true, false })
    {
        Set(config, "CompanionBoxLeft", rightSide ? .72f : .05f); Set(config, "CompanionBoxRight", rightSide ? .95f : .28f);
        Set(config, "CompanionHomeX", rightSide ? .85f : .15f); Set(config, "CompanionHomeY", .92f);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); drawHome.Invoke(companion, [draw, Layout()]);
        Check(draw.VtxBuffer.Size == 4 && draw.VtxBuffer[0].Uv.X == (rightSide ? .925f : .08f), "The actual home draw crops transparent gutters and mirrors inward.");
        ImGui.Render();
    }
    Set(companion, "handTime", 1f);
    foreach (var step in new[] { (true, 0f, 0), (true, .05f, 0), (true, .05f, 1), (true, .05f, 1), (true, .05f, 2), (false, .01f, 3) })
    {
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        drawHand.Invoke(companion, [draw, Vector2.Zero, new Vector2(640, 440), 1f, step.Item2, step.Item1, null, new Vector2(320, 220)]);
        var expected = new Vector2(step.Item3 % 2, step.Item3 / 2) * .5f;
        Check(draw.VtxBuffer.Size == 4 && draw.VtxBuffer[0].Uv == expected, "Grab cues use open, closing, hold, then release atlas poses.");
        Check(Math.Abs(draw.VtxBuffer[1].Pos.X - draw.VtxBuffer[0].Pos.X - Value("CompanionHandSize")) < .001f,
            "Every grab pose uses the smaller default hand size.");
        for (var c = 0; c < draw.CmdBuffer.Size; c++)
            if (draw.CmdBuffer[c].ElemCount > 0)
                Check(draw.CmdBuffer[c].ClipRect.X >= 0 && draw.CmdBuffer[c].ClipRect.Y >= 0
                    && draw.CmdBuffer[c].ClipRect.Z <= 640 && draw.CmdBuffer[c].ClipRect.W <= 440,
                    "Hand cues are clipped to the viewport even when the pointer is outside it.");
        ImGui.Render();
    }

    var grabAnchor = assembly.GetType("OmniKit.Ui.CompanionGrabAnchor", true)!;
    var gripInCell = grabAnchor.GetMethod("GripInCell", statics)!;
    foreach (var bodySize in new[] { 48f, 96f, 192f })
    foreach (var independentHandSize in new[] { 8f, 38.4f, 192f })
    foreach (var center in new[] { new Vector2(320, 220), new Vector2(96), new Vector2(544, 344) })
    {
        Set(config, "CompanionSize", bodySize); Set(config, "CompanionHandSize", independentHandSize);
        Set(companion, "handWasDragging", true); Set(companion, "handTime", .3f);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        ImGui.GetIO().MousePos = new Vector2(-1000, 1000);
        drawHand.Invoke(companion, [draw, Vector2.Zero, new Vector2(640, 440), 1f, 0f, true, null, center]);
        var handSize = draw.VtxBuffer[2].Pos - draw.VtxBuffer[0].Pos;
        var contact = draw.VtxBuffer[0].Pos + handSize * (Vector2)gripInCell.Invoke(null, [2])!;
        Check(Vector2.Distance(contact, center) < .0001f,
            "The actual held hand contacts the supplied body or house centre at every scale, regardless of mouse position.");
        ImGui.Render();
    }
    Set(config, "CompanionHandSize", 38.4f);

    var libraryType = assembly.GetType("OmniKit.Ui.CompanionAnimationLibrary", true)!;
    var library = Activator.CreateInstance(libraryType, Path.GetFullPath("assets"))!;
    object?[] lookup = ["Wink", "Navigator", null];
    Check((bool)libraryType.GetMethod("TryGet")!.Invoke(library, lookup)!, "Approved Wink atlas is available.");
    var atlas = lookup[2]!; var getFloat = atlas.GetType().GetMethod("GetFloat")!;
    var drawBlink = roaming.GetMethod("DrawWinkBlink", statics)!;
    var samples = new List<object>();
    foreach (var left in new[] { false, true })
    foreach (var closure in new[] { 0f, .5f, 1f })
    {
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        var quad = getFloat.Invoke(atlas, [.2f, left, Vector2.Zero, new Vector2(192)])!;
        Vector2 Q(string name) => (Vector2)quad.GetType().GetProperty(name)!.GetValue(quad)!;
        draw.AddImageQuad(new ImTextureID(2UL), Q("A"), Q("B"), Q("C"), Q("D"), Q("UvA"), Q("UvB"), Q("UvC"), Q("UvD"), 0xFFFFFFFF);
        drawBlink.Invoke(null, [draw, new ImTextureID(2UL), quad, closure, 0xFFFFFFFFu]);
        for (var i = 0; i < draw.VtxBuffer.Size; i++)
        {
            var v = draw.VtxBuffer[i];
            Check(float.IsFinite(v.Pos.X) && float.IsFinite(v.Pos.Y) && v.Pos.X >= 0 && v.Pos.Y >= 0 && v.Pos.X <= 192 && v.Pos.Y <= 192,
                "A mirrored floating eyelid stays inside the approved sprite cell.");
        }
        var (vertices, triangles) = Snapshot(draw);
        samples.Add(new { left, closure, vertices, triangles }); ImGui.Render();
    }
    Directory.CreateDirectory(output);
    File.WriteAllText(Path.Combine(output, "wink-blink.json"), JsonSerializer.Serialize(samples));

    samples.Clear();
    foreach (var rightSide in new[] { false, true })
    {
        Set(config, "CompanionBoxLeft", rightSide ? .72f : .05f); Set(config, "CompanionBoxRight", rightSide ? .95f : .28f);
        Set(companion, "handTime", 1f); Set(companion, "handWasDragging", false);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var layout = Layout();
        drawHome.Invoke(companion, [draw, layout]);
        var bounds = ((Vector2, Vector2))physics.GetMethod("HomeBounds", statics)!.Invoke(null, [layout])!;
        var position = new Vector2((bounds.Item1.X + bounds.Item2.X) * .5f - 48, bounds.Item2.Y - 96);
        var quad = getFloat.Invoke(atlas, [.2f, rightSide, position, new Vector2(96)])!;
        Vector2 Q(string name) => (Vector2)quad.GetType().GetProperty(name)!.GetValue(quad)!;
        draw.AddImageQuad(new ImTextureID(2UL), Q("A"), Q("B"), Q("C"), Q("D"), Q("UvA"), Q("UvB"), Q("UvC"), Q("UvD"), 0xFFFFFFFF);
        drawHand.Invoke(companion, [draw, Vector2.Zero, new Vector2(640, 440), 1f, .016f, false,
            bounds.Item1 + (bounds.Item2 - bounds.Item1) * new Vector2(.5f, .1f), null]);
        var (vertices, triangles) = Snapshot(draw);
        samples.Add(new { rightSide, vertices, triangles }); ImGui.Render();
    }
    File.WriteAllText(Path.Combine(output, "home-hand.json"), JsonSerializer.Serialize(samples));
}
finally { ImGui.DestroyContext(context); }
Console.WriteLine($"Companion native behavior: {checks} checks passed; comment renders and blink geometry exported. No game process accessed.");

public class LocalProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    { var proxy = Create<T, LocalProxy>(); ((LocalProxy)(object)proxy).Handler = handler; return proxy; }
    public static object Create(Type type, Func<MethodInfo, object?[], object?> handler)
    { var proxy = DispatchProxy.Create(type, typeof(LocalProxy)); ((LocalProxy)proxy).Handler = handler; return proxy; }
}
