using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit;
using OmniKit.Ui;

PolicyChecks.Run();
var root=Path.GetFullPath(".local/seasonal-companion-tests");
var assetRoot=Path.GetFullPath("assets");
var output=Path.Combine(root,"native"); Directory.CreateDirectory(output);
var data=JsonSerializer.Deserialize<AccessoryDocument>(File.ReadAllText(Path.Combine(assetRoot,"seasonal-accessories.frames.json")))!;
var seasonal=new SeasonalCompanionRenderer(assetRoot);
if(!seasonal.IsAvailable) throw new Exception("Seasonal metadata failed validation.");
var library=new CompanionAnimationLibrary(assetRoot); var naps=new CompanionNapArtwork(assetRoot);
if(library.Count!=8) throw new Exception("Expected all eight current companion atlases.");
var native=ImGui.CreateContext();
try
{
    var io=ImGui.GetIO(); io.DeltaTime=1f/60;
    unsafe { io.IniFilename=null; io.LogFilename=null; }
    io.Fonts.AddFontDefault(); io.Fonts.Build();
    var renderer=new SoftwareRenderer(); var textures=new Dictionary<string,ulong>();
    foreach(var avatar in Configuration.AvatarNames)
    {
        library.TryGet(avatar,"Default",out var atlas); Load(atlas!.File,assetRoot);
        if(!naps.TryGet(avatar,"Default",out var nap)||nap is null) throw new Exception("Nap missing.");
        Load(nap.File,assetRoot);
    }
    Load(data.File,assetRoot);
    var checks=0; var padding=0f;
    foreach(var profile in data.Profiles)
    foreach(var theme in data.Themes)
    foreach(var size in new[]{48f,96f,192f})
    foreach(var mirrored in new[]{false,true})
    {
        for(var pose=0;pose<28;pose++)
        {
            var result=Geometry(profile,theme,pose,mirrored,Vector2.Zero,new Vector2(size));
            if(result.Any(p=>!float.IsFinite(p.X)||!float.IsFinite(p.Y))) throw new Exception("Nonfinite accessory.");
            foreach(var p in result) padding=Math.Max(padding,Math.Max(Math.Max(-p.X,-p.Y),Math.Max(p.X-size,p.Y-size))/size);
            checks++;
        }
        foreach(var phase in new[]{0f,.25f,.5f,.75f})
        {
            var points=Geometry(profile,theme,0,mirrored,Vector2.Zero,new Vector2(size),phase);
            foreach(var p in points) padding=Math.Max(padding,Math.Max(Math.Max(-p.X,-p.Y),Math.Max(p.X-size,p.Y-size))/size);
            checks++;
        }
    }
    // Preserve complete body size; attachment overflow is explicit visual-bound padding.
    if(padding>SeasonalCompanionRenderer.RequiredVisualPaddingFraction) throw new Exception("Accessory exceeds declared visual bounds: "+padding);
    foreach(var size in new[]{48f,96f,192f})
    {
        var tile=new Vector2(size*1.5f,size*1.55f+20);
        io.DisplaySize=new Vector2(tile.X*9,tile.Y*8+22);
        ImGui.NewFrame(); var draw=ImGui.GetForegroundDrawList();
        var labels=new[]{"New year","Hearts","Blossoms","Eggs","Gold","Summer","Rising","Halloween","Starlight"};
        for(var col=0;col<9;col++) draw.AddText(new Vector2(col*tile.X+3,4),0xFFFFFFFF,labels[col]);
        for(var row=0;row<8;row++)
        for(var col=0;col<9;col++)
        {
            var p=new Vector2(col*tile.X+size*.25f,22+row*tile.Y+size*.25f);
            Draw(draw,data.Profiles[row],data.Themes[col],0,false,p,new Vector2(size));
            draw.AddText(new Vector2(col*tile.X+3,22+row*tile.Y+size*1.3f),0xFFEEEEEE,data.Profiles[row].Avatar);
        }
        ImGui.Render(); renderer.Save(ImGui.GetDrawData(),Path.Combine(output,$"all-eight-nine-themes-{size:0}.png"));
    }
    foreach(var themeId in new[]{"AllSaintsWake","Starlight"})
    foreach(var mirrored in new[]{false,true})
    {
        const float size=96;
        var theme=data.Themes.Single(t=>t.Id==themeId);
        var poses=new[]{0,16,18,20,22,24,26};
        var tile=new Vector2(144,168);io.DisplaySize=new Vector2(tile.X*poses.Length,tile.Y*8+22);
        ImGui.NewFrame();var draw=ImGui.GetForegroundDrawList();
        draw.AddText(new Vector2(8,4),0xFFFFFFFF,$"{themeId} / {(mirrored?"left":"right")} / open,carry,fall,land,push,enter,sleep");
        for(var row=0;row<8;row++)
        for(var col=0;col<poses.Length;col++)
        {
            var p=new Vector2(col*tile.X+24,22+row*tile.Y+24);
            Draw(draw,data.Profiles[row],theme,poses[col],mirrored,p,new Vector2(size));
            draw.AddText(new Vector2(col*tile.X+3,22+row*tile.Y+140),0xFFEEEEEE,data.Profiles[row].Avatar);
        }
        ImGui.Render();renderer.Save(ImGui.GetDrawData(),Path.Combine(output,$"{themeId.ToLowerInvariant()}-poses-{(mirrored?"left":"right")}.png"));
    }
    // Show shared whole-body transforms: no overlay phase or independent jiggle.
    io.DisplaySize=new Vector2(6*144,8*168+22);ImGui.NewFrame();var motionDraw=ImGui.GetForegroundDrawList();
    var halloween=data.Themes.Single(t=>t.Id=="AllSaintsWake");
    for(var row=0;row<8;row++)
    for(var col=0;col<6;col++)
    {
        var phase=col/6f;var p=new Vector2(col*144+24,22+row*168+24);
        Draw(motionDraw,data.Profiles[row],halloween,0,false,p,new Vector2(96),phase);
        motionDraw.AddText(new Vector2(col*144+3,22+row*168+140),0xFFEEEEEE,data.Profiles[row].Avatar+" "+phase.ToString("0.00"));
    }
    ImGui.Render();renderer.Save(ImGui.GetDrawData(),Path.Combine(output,"shared-motion-phases.png"));
    File.WriteAllText(Path.Combine(root,"geometry-result.json"),JsonSerializer.Serialize(new{Checks=checks,MaximumExtraPaddingFraction=padding,RecommendedPaddingFraction=MathF.Ceiling(padding*100)/100},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"PASS: {checks} source-linked attachment geometry checks; native texture sheets for 9 themes x 8 avatars at 48/96/192, reaction/nap facings and shared-motion phases. Extra sampled padding {padding:0.000} cell; continuous bound fits 0.23; no body resized.");

    void Load(string file,string directory)
    {
        if(textures.ContainsKey(file))return;
        using var bitmap=new Bitmap(Path.Combine(directory,file));
        var bits=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        var pixels=new byte[bitmap.Width*bitmap.Height*4];
        try{for(var y=0;y<bitmap.Height;y++)Marshal.Copy(bits.Scan0+y*bits.Stride,pixels,y*bitmap.Width*4,bitmap.Width*4);}finally{bitmap.UnlockBits(bits);}
        for(var i=0;i<pixels.Length;i+=4)(pixels[i],pixels[i+2])=(pixels[i+2],pixels[i]);
        var id=(ulong)textures.Count+2;renderer.AddTexture(id,pixels,bitmap.Width,bitmap.Height);textures.Add(file,id);
    }
    void Draw(ImDrawListPtr draw,Profile profile,Theme theme,int pose,bool mirrored,Vector2 position,Vector2 size,float? phase=null)
    {
        library.TryGet(profile.Avatar,"Default",out var atlas);
        if(phase is {} value)
        {
            if(profile.Avatar=="Moonblob") { var q=atlas!.GetRoll(value,mirrored,position,size);draw.AddImageQuad(new ImTextureID(textures[atlas.File]),q.A,q.B,q.C,q.D,q.UvA,q.UvB,q.UvC,q.UvD,0xFFFFFFFF); }
            else {var q=atlas!.GetFloat(value,mirrored,position,size);draw.AddImageQuad(new ImTextureID(textures[atlas.File]),q.A,q.B,q.C,q.D,q.UvA,q.UvB,q.UvC,q.UvD,0xFFFFFFFF);}
        }
        else
        {
            CompanionFrame frame;string file;
            if(pose>=24){naps.TryGet(profile.Avatar,"Default",out var nap);frame=nap!.Frame(pose-24,position,size,1,mirrored);file=nap.File;}
            else{frame=atlas!.GetFrame(pose,mirrored,position,size);file=atlas.File;}
            draw.AddImage(new ImTextureID(textures[file]),frame.Position,frame.Position+frame.Size,frame.UvStart,frame.UvEnd,0xFFFFFFFF);
        }
        var overlay=Overlay(profile,theme,pose,mirrored,position,size,phase);
        SeasonalCompanionRenderer.Draw(draw,new ImTextureID(textures[data.File]),overlay);
    }
    Vector2[] Geometry(Profile profile,Theme theme,int pose,bool mirrored,Vector2 position,Vector2 size,float? phase=null)
    {
        var q=Overlay(profile,theme,pose,mirrored,position,size,phase);
        return [q.A,q.B,q.C,q.D];
    }
    SeasonalOverlayQuad Overlay(Profile profile,Theme theme,int pose,bool mirrored,Vector2 position,Vector2 size,float? phase=null)
    {
        library.TryGet(profile.Avatar,"Default",out var atlas);
        Vector2 attach;float sourceScale;Vector4 rect;CompanionFrame frame;
        if(pose>=24){naps.TryGet(profile.Avatar,"Default",out var nap);attach=V(profile.NapSourceAnchors[pose-24]);sourceScale=nap!.ScaleReference;var c=nap.Frames[pose-24];rect=new Vector4(c.Bounds[0],c.Bounds[1],c.Bounds[2],c.Bounds[3]);frame=nap.Frame(pose-24,position,size,1,mirrored);}
        else{attach=V(profile.NormalSourceAnchors[pose]);sourceScale=atlas!.SourceScale;rect=atlas.Frames[pose].Rect;frame=atlas.GetFrame(pose,mirrored,position,size);}
        var transform=SeasonalBodyTransform.FromFrame(rect,sourceScale,frame,mirrored);
        if(phase is {} value)
        {
            Vector2 a,b,d;
            if(profile.Avatar=="Moonblob"){var q=atlas!.GetRoll(value,mirrored,position,size);(a,b,d)=(q.A,q.B,q.D);}
            else{var q=atlas!.GetFloat(value,mirrored,position,size);(a,b,d)=(q.A,q.B,q.D);}
            transform=new SeasonalBodyTransform(rect,sourceScale,a,b,d);
        }
        if(!seasonal.TryGetQuad(Enum.Parse<SeasonalCompanionTheme>(theme.Id),profile.Avatar,pose>=24?pose-24:pose,pose>=24,transform,out var quad))
            throw new Exception("Production accessory rejected valid pose: "+profile.Avatar+" / "+pose);
        // Exact full-circle roll extrema from source corner radii; no finite-phase sampling claim.
        if(profile.Avatar=="Moonblob" && phase.HasValue)
        {
            var roll=atlas!.GetRoll(phase.Value,mirrored,position,size);
            var radius=new[]{quad.A,quad.B,quad.C,quad.D}.Max(p=>Vector2.Distance(p,roll.Center));
            var min=roll.Center-new Vector2(radius);var max=roll.Center+new Vector2(radius);
            var margin=SeasonalCompanionRenderer.RequiredVisualPaddingFraction*size.X;
            if(min.X<position.X-margin||min.Y<position.Y-margin||max.X>position.X+size.X+margin||max.Y>position.Y+size.Y+margin)
                throw new Exception("Continuous roll exceeds visual padding.");
        }
        return quad;
    }
}
finally{ImGui.DestroyContext(native);}
static Vector2 V(float[]v)=>new(v[0],v[1]);
sealed class AccessoryDocument { public string File{get;set;}="";public float Width{get;set;}public float Height{get;set;}public Theme[]Themes{get;set;}=[];public Profile[]Profiles{get;set;}=[]; }
sealed class Theme { public string Id{get;set;}="";public float[]Bounds{get;set;}=[];public float[]Pivot{get;set;}=[];public float WidthFraction{get;set;} }
sealed class Profile { public string Avatar{get;set;}="";public float[][]NormalSourceAnchors{get;set;}=[];public float[][]NapSourceAnchors{get;set;}=[]; }
