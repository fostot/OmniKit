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
var bagsOnly = args.Contains("--bags-only");
if (args.Contains("--outline-only"))
{
    CheckOutlineBounds();
    return;
}

if (!bagsOnly)
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
Console.WriteLine(bagsOnly
    ? "PASS: ready HUD retains [E] with passive bags; ordinary pause/keyboard capture hides it; Modify, context menu and drag remain visible; world interaction overrides editing; installed inventory names verified. Synthetic native rendering only."
    : "PASS: HUD font control is clickable at minimum settings size, including hidden HUD; marked sample previews render without a candidate; native glyph geometry scales at 50/100/200%; previews disappear when editing ends; normal HUD hides while paused and shows the key when ready; world interactions override editing; effects and independent outlines render.");

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
    Set(ui,"voice",Activator.CreateInstance(assembly.GetType("OmniKit.Assistant.VoiceInput",true)!, nonPublic:true));
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
    Set(config,"InteractKey",Enum.Parse(configType.GetField("InteractKey")!.FieldType,"E"));
    var interaction=Activator.CreateInstance(serviceType,config)!;
    serviceType.GetProperty("ActionPaused")!.SetValue(interaction,false);
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
        var declaredInventoryNames=new[]{"AddonInventory","AddonInventoryLarge","AddonInventoryExpansion","AddonInventoryGrid"}
            .Select(name=>typeof(AtkUnitBase).Assembly.GetType("FFXIVClientStructs.FFXIV.Client.UI."+name,true)!)
            .SelectMany(type=>type.CustomAttributes.Where(attribute=>attribute.AttributeType.Name=="AddonAttribute"))
            .SelectMany(attribute=>attribute.ConstructorArguments)
            .SelectMany(argument=>argument.Value is IEnumerable<CustomAttributeTypedArgument> values ? values.Select(value=>value.Value) : new[]{argument.Value})
            .ToHashSet();
        foreach(var name in new[]{"Inventory","InventoryLarge","InventoryExpansion","InventoryGrid","InventoryGrid0","InventoryGrid1",
            "InventoryGrid0E","InventoryGrid1E","InventoryGrid2E","InventoryGrid3E"})
            if(!declaredInventoryNames.Contains(name)) throw new InvalidOperationException("Unverified passive bag addon name: "+name);
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
        ImGui.Render(); visibleAddon="Inventory"; addon.IsVisible=true;
        preview.SetValue(interaction,false);
        foreach(var paused in new[]{false,true})
        foreach(var capture in new[]{false,true})
        {
            serviceType.GetProperty("ActionPaused")!.SetValue(interaction,paused);
            ImGui.NewFrame(); ImGui.GetIO().WantCaptureKeyboard=capture; drawHud.Invoke(interaction,null);
            var actual=ImGui.GetForegroundDrawList(); var comparison=ImGui.GetBackgroundDrawList();
            if(!paused&&!capture) comparison.AddText(ImGui.GetFont(),ImGui.GetFontSize(),Vector2.Zero,0xFFFFFFFF,"[E]  Sample NPC",0);
            if(actual.VtxBuffer.Size!=comparison.VtxBuffer.Size)
                throw new InvalidOperationException($"Normal bag HUD should show [E] only when ready, and hide during a real pause: paused={paused}, capture={capture}.");
            ImGui.Render();
            if(!paused&&!capture) renderer.Save(ImGui.GetDrawData(),Path.Combine(output,"hud-bags-keyhint.png"));
        }
        ImGui.GetIO().WantCaptureKeyboard=false;
        preview.SetValue(interaction,true);
        ImGui.NewFrame(); drawHud.Invoke(interaction,null);
        var sample=ImGui.GetForegroundDrawList(); var expected=ImGui.GetBackgroundDrawList();
        expected.AddText(ImGui.GetFont(),ImGui.GetFontSize(),Vector2.Zero,0xFFFFFFFF,"Sample NPC  · Preview",0);
        if(sample.VtxBuffer.Size!=expected.VtxBuffer.Size) throw new InvalidOperationException("Only the actual HUD editor should show the marked keyless sample.");
        ImGui.Render(); renderer.Save(ImGui.GetDrawData(),Path.Combine(output,"hud-editing-sample.png"));
        preview.SetValue(interaction,false);
        // Start from the actual ready HUD, then let Shift pause actions before
        // the mouse gesture. Do not inject a popup or its backing state.
        serviceType.GetProperty("ActionPaused")!.SetValue(interaction,false);
        ImGui.NewFrame(); drawHud.Invoke(interaction,null); ImGui.Render();
        var readyPosition=(Vector2)serviceType.GetField("hudReadyBoundsPosition",flags)!.GetValue(interaction)!;
        var readySize=(Vector2)serviceType.GetField("hudReadyBoundsSize",flags)!.GetValue(interaction)!;
        var io=ImGui.GetIO();
        serviceType.GetProperty("ActionPaused")!.SetValue(interaction,true);
        io.AddKeyEvent(ImGuiKey.ModShift,true); io.AddMousePosEvent(10,10);
        ImGui.NewFrame(); io.WantCaptureKeyboard=true; drawHud.Invoke(interaction,null);
        if(ImGui.GetForegroundDrawList().VtxBuffer.Size!=0 || (bool)serviceType.GetField("hudPopupOpen",flags)!.GetValue(interaction)!)
            throw new InvalidOperationException("Shift outside the last ready HUD must not revive a paused HUD or create its popup.");
        ImGui.Render(); io.WantCaptureKeyboard=false;
        // Use the left edge formerly occupied by [E], which lies outside the
        // narrower keyless editor text. The gesture must still own that click.
        io.AddMousePosEvent(readyPosition.X+1,readyPosition.Y+readySize.Y*.5f);
        for(var frame=0;frame<4;frame++)
        {
            if(frame==1) io.AddMouseButtonEvent(1,true);
            if(frame==2) { io.AddMouseButtonEvent(1,false); io.AddKeyEvent(ImGuiKey.ModShift,false); }
            ImGui.NewFrame();
            drawHud.Invoke(interaction,null);
            if((frame>0 && !(bool)serviceType.GetField("hudPopupOpen",flags)!.GetValue(interaction)!)
                || ImGui.GetForegroundDrawList().VtxBuffer.Size==0
                || !(bool)serviceType.GetProperty("ActionPaused")!.GetValue(interaction)!)
                throw new InvalidOperationException("Shift/right-click must open and retain the HUD menu while game action stays paused.");
            if(frame==3)
            {
                var text=ImGui.GetForegroundDrawList();
                var min=new Vector2(float.MaxValue); var max=new Vector2(float.MinValue);
                for(var i=0;i<text.VtxBuffer.Size;i++)
                { min=Vector2.Min(min,text.VtxBuffer[i].Pos); max=Vector2.Max(max,text.VtxBuffer[i].Pos); }
                var popups=0;
                for(var i=0;i<context.Windows.Size;i++)
                {
                    var window=context.Windows[i];
                    if(!window.Active || window.Hidden || (window.Flags&ImGuiWindowFlags.Popup)==0) continue;
                    ++popups;
                    var end=window.Pos+window.Size;
                    if(window.Pos.X<0 || window.Pos.Y<0 || end.X>io.DisplaySize.X || end.Y>io.DisplaySize.Y
                        || !(end.Y+4<=min.Y || window.Pos.Y>=max.Y+4))
                        throw new InvalidOperationException("HUD menu must fit the viewport and leave a visible gutter from the HUD text.");
                }
                if(popups!=1) throw new InvalidOperationException("Expected exactly one visible HUD context menu.");
            }
            ImGui.Render();
            if(frame==3) renderer.Save(ImGui.GetDrawData(),Path.Combine(output,"hud-shift-entry-menu.png"));
        }
        visibleAddon="Talk";
        ImGui.NewFrame(); drawHud.Invoke(interaction,null);
        if(ImGui.GetForegroundDrawList().VtxBuffer.Size!=0 || (bool)serviceType.GetField("hudPopupOpen",flags)!.GetValue(interaction)!)
            throw new InvalidOperationException("Active dialogue must hide the HUD and close even its modification popup.");
        ImGui.Render(); visibleAddon="Inventory";
        io.AddKeyEvent(ImGuiKey.ModShift,true);
        ImGui.NewFrame(); drawHud.Invoke(interaction,null);
        if(ImGui.GetForegroundDrawList().VtxBuffer.Size!=0)
            throw new InvalidOperationException("Dialogue must invalidate the previous ready HUD gesture bounds.");
        ImGui.Render(); io.AddKeyEvent(ImGuiKey.ModShift,false);
        Set(config,"InteractionHudLocked",false); Set(interaction,"hudDragging",true);
        ImGui.GetIO().AddMouseButtonEvent(0,true);
        ImGui.NewFrame(); drawHud.Invoke(interaction,null);
        if(ImGui.GetForegroundDrawList().VtxBuffer.Size==0 || !(bool)serviceType.GetField("hudDragging",flags)!.GetValue(interaction)!)
            throw new InvalidOperationException("Dragging the HUD must remain visible through its own input pause.");
        ImGui.Render(); ImGui.GetIO().AddMouseButtonEvent(0,false);
        Set(interaction,"hudDragging",false); Set(config,"InteractionHudLocked",true);
        preview.SetValue(interaction,true);
        if(bagsOnly) return;
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

unsafe void CheckOutlineBounds()
{
    var count = 0;
    var candidateType = serviceType.GetNestedType("Candidate", BindingFlags.NonPublic)!;
    var category = Enum.Parse(assembly.GetType("OmniKit.Interaction.InteractionTargets", true)!, "Npcs");
    var effectType = assembly.GetType("OmniKit.Interaction.HudTextEffect", true)!;
    foreach (var scale in new[] { 1f, 1.5f })
    {
        var context = ImGui.CreateContext();
        try
        {
            var renderer = Start(scale, new Vector2(480, 330));
            var config = Config(); Set(config, "SoftInteractEnabled", true);
            Set(config, "InteractionHudOutline", true); Set(config, "InteractionHudEffectIntensity", 2f);
            Set(config, "InteractionHudScale", 2f);
            var interaction = Activator.CreateInstance(serviceType, config)!;
            var io = ImGui.GetIO();
            foreach (var effect in new[] { "None", "ChromaticGlitch", "Flame", "Frost" })
            foreach (var thickness in new[] { .5f, 4f })
            foreach (var longName in new[] { false, true })
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            {
                Set(config, "InteractionHudOutlineThickness", thickness);
                Set(config, "InteractionHudEffect", Enum.Parse(effectType, effect));
                Set(config, "InteractionHudOffsetX", corner.X * 10000); Set(config, "InteractionHudOffsetY", corner.Y * 10000);
                var name = longName ? string.Concat(Enumerable.Repeat("Étoile Moonlit Guild Vendor ", 12)) : "Étoile AV";
                Set(interaction, "candidate", Activator.CreateInstance(candidateType, 0UL, IntPtr.Zero, name, category));
                serviceType.GetProperty("ActionPaused")!.SetValue(interaction, false);
                io.AddKeyEvent(ImGuiKey.ModShift, false); io.AddMousePosEvent(-1000, -1000);
                ImGui.NewFrame(); io.WantCaptureKeyboard = false; drawHud.Invoke(interaction, null);
                var position = (Vector2)serviceType.GetField("hudReadyBoundsPosition", flags)!.GetValue(interaction)!;
                var size = (Vector2)serviceType.GetField("hudReadyBoundsSize", flags)!.GetValue(interaction)!;
                var list = ImGui.GetForegroundDrawList();
                if (list.VtxBuffer.Size == 0 || position.X < 7.9f || position.Y < 7.9f
                    || position.X + size.X > io.DisplaySize.X - 7.9f || position.Y + size.Y > io.DisplaySize.Y - 7.9f)
                    throw new InvalidOperationException("Thick outline HUD bounds must fit the viewport with the same eight-pixel gutter.");
                for (var i = 0; i < list.VtxBuffer.Size; i++)
                {
                    var p = list.VtxBuffer[i].Pos;
                    if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || p.X < position.X - .01f || p.Y < position.Y - .01f
                        || p.X > position.X + size.X + .01f || p.Y > position.Y + size.Y + .01f)
                        throw new InvalidOperationException("Actual outlined/effected glyphs must fit the cached Shift hit bounds, including elided names.");
                }
                ImGui.Render();
                if (effect == "ChromaticGlitch" && thickness == 4 && !longName && corner == Vector2.One)
                    renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"outline-edge-{scale:0.0}.png"));
                serviceType.GetProperty("ActionPaused")!.SetValue(interaction, true);
                io.AddKeyEvent(ImGuiKey.ModShift, true); io.AddMousePosEvent(position.X + size.X * .5f, position.Y + size.Y * .5f);
                // Native ImGui trickles key/mouse transitions across frames.
                ImGui.NewFrame(); io.WantCaptureKeyboard = false; drawHud.Invoke(interaction, null); ImGui.Render();
                ImGui.NewFrame(); io.WantCaptureKeyboard = false; drawHud.Invoke(interaction, null);
                var hitWindow = Enumerable.Range(0, context.Windows.Size).Select(i => context.Windows[i])
                    .SingleOrDefault(w => w.Active && (System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)w.Name) ?? "")
                        .StartsWith("##OmniKitVisibleTargetControls", StringComparison.Ordinal));
                if (hitWindow.Handle == null || hitWindow.Pos.X < 0 || hitWindow.Pos.Y < 0
                    || hitWindow.Pos.X + hitWindow.Size.X > io.DisplaySize.X || hitWindow.Pos.Y + hitWindow.Size.Y > io.DisplaySize.Y)
                    throw new InvalidOperationException($"Actual Shift editor hitbox must remain available and on-screen: scale={scale}, effect={effect}, thickness={thickness}, long={longName}, corner={corner}, shift={io.KeyShift}, found={hitWindow.Handle != null}, ready={position}/{size}.");
                ImGui.Render(); count++;
            }
        }
        finally { ImGui.DestroyContext(context); }
    }
    Console.WriteLine($"PASS: {count} native HUD outline edge/elision/Shift-hitbox cases at 1x/1.5x; no game invoked.");
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
