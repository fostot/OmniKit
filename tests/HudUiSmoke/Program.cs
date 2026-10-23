using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LauncherUiSmoke;

var pluginPath = Path.GetFullPath(args.ElementAtOrDefault(0) ?? "src/OmniKit/bin/Release/OmniKit.dll");
var output = Path.GetFullPath(args.ElementAtOrDefault(1) ?? ".local/visualharness/hud-font");
var hostPath = Environment.GetEnvironmentVariable("DALAMUD_LIB_PATH") ??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (var directory in new[] { Path.GetDirectoryName(pluginPath)!, hostPath })
    {
        var path = Path.Combine(directory, name.Name + ".dll");
        if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
    }
    return null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginPath);
var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
var uiType = assembly.GetType("OmniKit.Ui.PluginUi", true)!;
var serviceType = assembly.GetType("OmniKit.Interaction.SoftInteractionService", true)!;
var configType = assembly.GetType("OmniKit.Configuration", true)!;
var drawSettings = uiType.GetMethod("DrawSettings", flags)!;
var drawHud = serviceType.GetMethod("DrawHud", flags)!;
var theme = uiType.GetMethod("PushTheme", BindingFlags.Static | BindingFlags.NonPublic)!;

foreach (var globalScale in new[] { 1f, 1.5f, 2f })
{
    foreach (var hidden in new[] { false, true }) RenderSettings(globalScale, hidden);
    RenderSettings(globalScale, hidden:true, interacting:true);
    var heights = new[] { .5f, 1f, 2f }.Select(fontScale => RenderHud(globalScale, fontScale)).ToArray();
    if (heights[1] < heights[0] * 1.7f || heights[2] < heights[1] * 1.7f)
        throw new InvalidOperationException("Native HUD glyph height did not follow the saved font scale.");
    Console.WriteLine($"HUD scale {globalScale}: actual glyph heights at 50/100/200% = {string.Join("/", heights.Select(h => h.ToString("0.0")))}px.");
}
CheckDialogueAndEffects();
Console.WriteLine("PASS: HUD font control is clickable at minimum settings size, including hidden HUD; marked sample previews render without a candidate; native glyph geometry scales at 50/100/200%; previews disappear when editing ends; world interactions hide editing/live HUD and return on idle, unrelated menus/player chat do not; effects and independent outlines render.");

void Set(object instance, string field, object? value) => instance.GetType().GetField(field, flags)!.SetValue(instance, value);
object Config()
{
    var config = Activator.CreateInstance(configType)!;
    var persist = configType.GetField("Persist", flags)!;
    persist.SetValue(config, System.Linq.Expressions.Expression.Lambda(persist.FieldType,
        System.Linq.Expressions.Expression.Empty(), System.Linq.Expressions.Expression.Parameter(configType)).Compile());
    return config;
}
unsafe SoftwareRenderer Start(float scale, Vector2 size)
{
    var io = ImGui.GetIO();
    io.DisplaySize = size * scale; io.FontGlobalScale = scale; io.DeltaTime = 1f / 60;
    io.IniFilename = null; io.LogFilename = null;
    io.AddMousePosEvent(-1000,-1000);
    var font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
    if (File.Exists(font)) io.Fonts.AddFontFromFileTTF(font,17); else io.Fonts.AddFontDefault();
    io.Fonts.Build(); ImGui.StyleColorsDark();
    return new SoftwareRenderer();
}
unsafe void RenderSettings(float scale, bool hidden, bool interacting=false)
{
    var config = Config(); Set(config,"InteractionHudEnabled",!hidden);
    Set(config,"InteractionHudScale",1f);
    var interaction = Activator.CreateInstance(serviceType, config)!;
    serviceType.GetProperty("HudHiddenForInteraction")!.SetValue(interaction,interacting);
    var state=(hidden ? "hidden" : "shown")+(interacting ? "-interacting" : "");
    var ui = RuntimeHelpers.GetUninitializedObject(uiType);
    foreach (var field in uiType.GetFields(flags).Where(f => f.FieldType == typeof(string))) field.SetValue(ui,"");
    Set(ui,"config",config); Set(ui,"interaction",interaction); Set(ui,"settingsOpen",true);
    Set(ui,"section",2); Set(ui,"selectSettingsTab",true); Set(ui,"resetHudScroll",true);
    Set(ui,"hotkeys",Activator.CreateInstance(assembly.GetType("OmniKit.Ui.HotkeyCapture",true)!, nonPublic:true));
    var context = ImGui.CreateContext();
    try
    {
        var renderer = Start(scale,new Vector2(560,410));
        var io = ImGui.GetIO();
        for (var frame=0; frame<7; frame++)
        {
            if (frame==4) { io.AddMousePosEvent(90*scale,142*scale); io.AddMouseButtonEvent(0,true); }
            if (frame==5) io.AddMouseButtonEvent(0,false);
            ImGui.NewFrame();
            if (frame>0) ImGui.SetWindowSize("OmniKit Settings###OmniKit.Settings.Minimal",new Vector2(480,330)*scale);
            ImGui.SetNextWindowPos(new Vector2(40,40)*scale); theme.Invoke(null,null);
            try { drawSettings.Invoke(ui,null); }
            finally { ImGui.PopStyleColor(15); ImGui.PopStyleVar(6); }
            ImGui.Render();
        }
        renderer.Save(ImGui.GetDrawData(),Path.Combine(output,$"settings-{state}-{scale:0.0}.png"));
        var actual=(float)configType.GetField("InteractionHudScale")!.GetValue(config)!;
        if (actual>=.9f || actual<.5f) throw new InvalidOperationException($"Font size control was not clickable at minimum size: {scale}, hidden={hidden}, value={actual}.");
        if (!(bool)serviceType.GetProperty("HudPreviewEnabled")!.GetValue(interaction)!) throw new InvalidOperationException("HUD settings did not request a live preview.");
        for (var frame=0;frame<6;frame++)
        {
            io.AddMousePosEvent(200*scale,220*scale); io.AddMouseWheelEvent(0,-20);
            ImGui.NewFrame(); theme.Invoke(null,null);
            try { drawSettings.Invoke(ui,null); }
            finally { ImGui.PopStyleColor(15); ImGui.PopStyleVar(6); }
            ImGui.Render();
        }
        renderer.Save(ImGui.GetDrawData(),Path.Combine(output,$"settings-{state}-bottom-{scale:0.0}.png"));
        // Modify must bring the first (font size) control back into view after scrolling.
        Set(ui,"resetHudScroll",true);
        for (var frame=0;frame<6;frame++)
        {
            if(frame==3) { io.AddMousePosEvent(200*scale,199*scale); io.AddMouseButtonEvent(0,true); }
            if(frame==4) io.AddMouseButtonEvent(0,false);
            ImGui.NewFrame(); theme.Invoke(null,null);
            try { drawSettings.Invoke(ui,null); }
            finally { ImGui.PopStyleColor(15); ImGui.PopStyleVar(6); }
            ImGui.Render();
        }
        renderer.Save(ImGui.GetDrawData(),Path.Combine(output,$"settings-{state}-effect-menu-{scale:0.0}.png"));
        if(!ImGui.IsPopupOpen(string.Empty,ImGuiPopupFlags.AnyPopupId|ImGuiPopupFlags.AnyPopupLevel))
            throw new InvalidOperationException("Effect menu could not open at minimum size after returning from scrolled Modify settings.");
    }
    finally { ImGui.DestroyContext(context); }
}

unsafe void CheckDialogueAndEffects()
{
    var activeFlags=new HashSet<ConditionFlag>();
    string? visibleAddon=null;
    AtkUnitBase addon=default; addon.IsVisible=true;
    var address=(nint)(&addon);
    var conditions=TestProxy.Create<ICondition>((method,arguments) => method.Name=="get_Item" && activeFlags.Contains((ConditionFlag)arguments[0]!));
    var gui=TestProxy.Create<IGameGui>((method,arguments) => method.Name=="GetAddonByName"
        ? new AtkUnitBasePtr(Equals(arguments[0],visibleAddon) ? address : 0) : null);
    var services=assembly.GetType("OmniKit.Services",true)!;
    services.GetProperty("Conditions",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,conditions);
    services.GetProperty("GameGui",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,gui);
    var config=Config(); Set(config,"SoftInteractEnabled",true); Set(config,"InteractionHudEffect",Enum.Parse(assembly.GetType("OmniKit.Interaction.HudTextEffect",true)!,"None"));
    var interaction=Activator.CreateInstance(serviceType,config)!;
    var candidateType=serviceType.GetNestedType("Candidate",BindingFlags.NonPublic)!;
    Set(interaction,"candidate",Activator.CreateInstance(candidateType,0UL,IntPtr.Zero,"Sample NPC",
        Enum.Parse(assembly.GetType("OmniKit.Interaction.InteractionTargets",true)!,"Npcs")));
    var preview=serviceType.GetProperty("HudPreviewEnabled")!;
    var context=ImGui.CreateContext();
    try
    {
        var renderer=Start(1,new Vector2(900,450));
        var blockedAddons=new[]{"Talk","CutSceneSelectString","Shop","InclusionShop","FreeShop","JournalAccept","JournalResult",
            "RetainerList","RetainerTaskAsk","RetainerTaskResult","Gathering","GatheringMasterpiece"};
        var blockedFlags=new[]{"WatchingCutscene","WatchingCutscene78","OccupiedInCutSceneEvent","OccupiedInEvent",
            "OccupiedInQuestEvent","OccupiedSummoningBell","Gathering","ExecutingGatheringAction"};
        foreach(var name in blockedAddons)
        {
            var nativeType=typeof(AtkUnitBase).Assembly.GetType("FFXIVClientStructs.FFXIV.Client.UI.Addon"+name,true)!;
            var declaredNames=nativeType.CustomAttributes.Where(attribute=>attribute.AttributeType.Name=="AddonAttribute")
                .SelectMany(attribute=>attribute.ConstructorArguments)
                .SelectMany(argument=>argument.Value is IEnumerable<CustomAttributeTypedArgument> values ? values.Select(value=>value.Value) : new[]{argument.Value});
            if(!declaredNames.Contains(name)) throw new InvalidOperationException("Unverified installed native addon name: "+name);
        }
        foreach(var editing in new[]{false,true})
        foreach(var signal in blockedAddons.Select(name=>(Name:name,Addon:true)).Concat(blockedFlags.Select(name=>(Name:name,Addon:false)))
            .Concat(new (string Name,bool Addon)[]{("ChatLog",true),("Inventory",true),("ConfigSystem",true),("Occupied",false),("Casting",false),("None",true)}))
        {
            activeFlags.Clear(); visibleAddon=null; preview.SetValue(interaction,editing);
            if(!signal.Addon) activeFlags.Add(Enum.Parse<ConditionFlag>(signal.Name)); else visibleAddon=signal.Name;
            ImGui.NewFrame(); drawHud.Invoke(interaction,null);
            var hidden=signal.Addon ? blockedAddons.Contains(signal.Name) : blockedFlags.Contains(signal.Name);
            if((ImGui.GetForegroundDrawList().VtxBuffer.Size==0)!=hidden)
                throw new InvalidOperationException($"Incorrect HUD visibility for {signal}, editing={editing}.");
            ImGui.Render();
            activeFlags.Clear(); visibleAddon=null;
            ImGui.NewFrame(); drawHud.Invoke(interaction,null);
            if(ImGui.GetForegroundDrawList().VtxBuffer.Size==0)
                throw new InvalidOperationException($"HUD did not return after {signal} ended, editing={editing}.");
            ImGui.Render();
        }
        // A loaded but invisible Talk addon is not an active conversation.
        activeFlags.Clear(); visibleAddon="Talk"; addon.IsVisible=false;
        ImGui.NewFrame(); drawHud.Invoke(interaction,null);
        if(ImGui.GetForegroundDrawList().VtxBuffer.Size==0) throw new InvalidOperationException("Invisible dialogue addon hid the HUD.");
        ImGui.Render(); visibleAddon=null;
        preview.SetValue(interaction,false);
        ImGui.NewFrame(); drawHud.Invoke(interaction,null);
        var actual=ImGui.GetForegroundDrawList(); var actualCount=actual.VtxBuffer.Size;
        var comparison=ImGui.GetBackgroundDrawList(); comparison.AddText(ImGui.GetFont(),ImGui.GetFontSize(),Vector2.Zero,0xFFFFFFFF,"Sample NPC",0);
        if(actualCount!=comparison.VtxBuffer.Size) throw new InvalidOperationException("Paused live HUD still contains a misleading Preview suffix.");
        ImGui.Render();
        serviceType.GetProperty("ActionPaused")!.SetValue(interaction,false);
        ImGui.NewFrame(); drawHud.Invoke(interaction,null);
        actual=ImGui.GetForegroundDrawList(); comparison=ImGui.GetBackgroundDrawList();
        comparison.AddText(ImGui.GetFont(),ImGui.GetFontSize(),Vector2.Zero,0xFFFFFFFF,"[F]  Sample NPC",0);
        if(actual.VtxBuffer.Size!=comparison.VtxBuffer.Size) throw new InvalidOperationException("The active interaction key hint was changed.");
        ImGui.Render();
        preview.SetValue(interaction,true);
        var baseline=0;
        foreach(var effect in new[]{"None","Shadow","SoftGlow","GentleShimmer"})
        foreach(var outline in new[]{false,true})
        {
            Set(config,"InteractionHudEffect",Enum.Parse(assembly.GetType("OmniKit.Interaction.HudTextEffect",true)!,effect));
            Set(config,"InteractionHudOutline",outline);
            ImGui.NewFrame(); drawHud.Invoke(interaction,null);
            var count=ImGui.GetForegroundDrawList().VtxBuffer.Size;
            if(effect=="None"&&!outline) baseline=count;
            if(count<baseline || ((outline||effect is "Shadow" or "SoftGlow")&&count==baseline))
                throw new InvalidOperationException("A requested HUD effect or independent outline did not render.");
            ImGui.Render(); renderer.Save(ImGui.GetDrawData(),Path.Combine(output,$"effect-{effect}-outline{outline}.png"));
        }
        Set(config,"InteractionHudOutline",false);
        for(var frame=0;frame<80;frame++)
        {
            ImGui.GetIO().DeltaTime=.1f; ImGui.NewFrame(); drawHud.Invoke(interaction,null);
            var color=ImGui.GetForegroundDrawList().VtxBuffer[0].Col;
            if((color>>24)!=255 || (color&255)<210 || ((color>>8)&255)<230 || ((color>>16)&255)<230)
                throw new InvalidOperationException("Shimmer became transparent or too dark for readable text.");
            ImGui.Render();
        }
    }
    finally
    {
        ImGui.DestroyContext(context);
        services.GetProperty("Conditions",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,null);
        services.GetProperty("GameGui",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,null);
    }
}

unsafe float RenderHud(float globalScale,float fontScale)
{
    var config=Config(); Set(config,"InteractionHudEnabled",false); Set(config,"SoftInteractEnabled",false);
    Set(config,"InteractionHudScale",fontScale);
    var interaction=Activator.CreateInstance(serviceType,config)!;
    serviceType.GetProperty("HudPreviewEnabled")!.SetValue(interaction,true);
    var context=ImGui.CreateContext();
    try
    {
        var renderer=Start(globalScale,new Vector2(900,450));
        ImGui.NewFrame(); drawHud.Invoke(interaction,null);
        var list=ImGui.GetForegroundDrawList();
        if (list.VtxBuffer.Size==0) throw new InvalidOperationException("No-candidate editing preview did not render.");
        float minY=float.MaxValue,maxY=float.MinValue;
        for(var i=0;i<list.VtxBuffer.Size;i++) { minY=Math.Min(minY,list.VtxBuffer[i].Pos.Y); maxY=Math.Max(maxY,list.VtxBuffer[i].Pos.Y); }
        ImGui.Render();
        renderer.Save(ImGui.GetDrawData(),Path.Combine(output,$"preview-{globalScale:0.0}-font{fontScale:0.0}.png"));
        serviceType.GetProperty("HudPreviewEnabled")!.SetValue(interaction,false);
        ImGui.NewFrame(); drawHud.Invoke(interaction,null);
        if(ImGui.GetForegroundDrawList().VtxBuffer.Size!=0) throw new InvalidOperationException("Hidden HUD preview survived after editing ended.");
        ImGui.Render();
        return maxY-minY;
    }
    finally { ImGui.DestroyContext(context); }
}

public class TestProxy : DispatchProxy
{
    public Func<MethodInfo,object?[],object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args) => Handler(method!,args??[]);
    public static T Create<T>(Func<MethodInfo,object?[],object?> handler) where T:class
    {
        var proxy=Create<T,TestProxy>(); ((TestProxy)(object)proxy).Handler=handler; return proxy;
    }
}
