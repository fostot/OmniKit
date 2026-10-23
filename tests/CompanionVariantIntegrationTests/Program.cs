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

var plugin=Path.GetFullPath("src/OmniKit/bin/Release/OmniKit.dll");
var host=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"XIVLauncher","addon","Hooks","dev");
AssemblyLoadContext.Default.Resolving+=(context,name)=>{foreach(var dir in new[]{Path.GetDirectoryName(plugin)!,host}){var p=Path.Combine(dir,name.Name+".dll");if(File.Exists(p))return context.LoadFromAssemblyPath(p);}return null;};
var asm=AssemblyLoadContext.Default.LoadFromAssemblyPath(plugin);
const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
Type T(string n)=>asm.GetType("OmniKit."+n,true)!;
object New(string n,params object?[]args)=>Activator.CreateInstance(T(n),F,null,args,null)!;
object Get(object o,string n)=>o.GetType().GetProperty(n,F)!.GetValue(o)!;
object? Field(object o,string n)=>o.GetType().GetField(n,F)!.GetValue(o);
void Set(object o,string n,object? v)=>(o.GetType().GetField(n,F)??o.GetType().GetField("<"+n+">P",F)??o.GetType().GetField("<"+n+">k__BackingField",F))!.SetValue(o,v);
object? Call(object o,string n,params object?[]args)=>o.GetType().GetMethod(n,F)!.Invoke(o,args);
object E(string n,string v)=>Enum.Parse(T(n),v);
var count=0;void Check(bool b,string m){count++;if(!b)throw new InvalidOperationException(m);}
var holiday=args.Contains("--holiday-only");
var assets=Path.GetFullPath("assets");var output=Directory.CreateDirectory(Path.GetFullPath(holiday?".local/companion-holiday-integration":".local/companion-variant-integration"));
var baseLibrary=New("Ui.CompanionVariantLibrary",assets);var library=holiday?New("Ui.CompanionHolidayLibrary",assets):baseLibrary;var looks=((IEnumerable)Get(library,"Looks")).Cast<object>().ToArray();
Check(looks.Length==(holiday?72:32),"Fresh compiled DLL loads the complete requested artwork library.");
var config=New("Configuration");Set(config,"CompanionPalette","Moonlight");Set(config,"AnimationsEnabled",true);
var companion=RuntimeHelpers.GetUninitializedObject(T("Ui.RoamingCompanion"));
Set(companion,"config",config);Set(companion,"variants",baseLibrary);Set(companion,"assetDirectory",assets);
if(holiday)Set(companion,"holidayBodies",library);
var gait=New("Ui.CompanionGait");Set(gait,"Phase",.23f);Set(companion,"gait",gait);
var motion=New("Ui.CompanionBodyMotion");for(var i=0;i<30;i++)Call(motion,"Update",.05f,true,true);Set(companion,"bodyMotion",motion);
var rings=New("Ui.OrbitRingRenderer");for(var i=0;i<40;i++)Call(rings,"Update",.05f,true,1f);Set(companion,"orbitRings",rings);
var routine=New("Ui.CompanionHomeRoutine",1);Set(companion,"homeRoutine",routine);
var context=ImGui.CreateContext();
try{
 var io=ImGui.GetIO();unsafe{io.IniFilename=null;io.LogFilename=null;}io.DeltaTime=1f/60;io.Fonts.AddFontDefault();io.Fonts.Build();io.BackendFlags|=ImGuiBackendFlags.RendererHasVtxOffset;
 var painter=new SoftwareRenderer();var textures=new Dictionary<string,(ulong Id,int Width,int Height)>();
 var requested=new List<string>();
 foreach(var file in ((IEnumerable)Get(library,"Files")).Cast<string>().Concat(new[]{"companion-house.png","companion-grab-hand.png","seasonal-accessories.png"})){
  using var bitmap=new Bitmap(Path.Combine(assets,file));var bits=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);var bytes=new byte[bitmap.Width*bitmap.Height*4];
  try{for(var y=0;y<bitmap.Height;y++)Marshal.Copy(bits.Scan0+y*bits.Stride,bytes,y*bitmap.Width*4,bitmap.Width*4);}finally{bitmap.UnlockBits(bits);}
  for(var n=0;n<bytes.Length;n+=4)(bytes[n],bytes[n+2])=(bytes[n+2],bytes[n]);var id=(ulong)textures.Count+2;painter.AddTexture(id,bytes,bitmap.Width,bitmap.Height);textures.Add(file,(id,bitmap.Width,bitmap.Height));
 }
 object Wrap(Type type,string file)=>LocalProxy.Create(type,(method,_)=>method.Name switch{"get_Handle"=>new ImTextureID(textures[file].Id),"get_Width"=>textures[file].Width,"get_Height"=>textures[file].Height,"GetWrapOrDefault"=>Wrap(method.ReturnType,file),_=>throw new InvalidOperationException("Unexpected texture call: "+method.Name)});
 var services=T("Services");services.GetProperty("Textures",S)!.SetValue(null,LocalProxy.Create<ITextureProvider>((method,values)=>{if(method.Name!="GetFromFile")throw new InvalidOperationException("Unexpected texture-provider call.");var file=Path.GetFileName(values[0]!.ToString()!);requested.Add(file);return Wrap(method.ReturnType,file);}));
 services.GetProperty("Framework",S)!.SetValue(null,LocalProxy.Create<IFramework>((_,_)=>throw new InvalidOperationException("No framework access is permitted in draw tests.")));
 int ImageVertices(ImDrawListPtr draw,ulong id,int first)=>Indices(draw,id).Where(i=>i>=first).Distinct().Count();
 IEnumerable<int> Indices(ImDrawListPtr draw,ulong id){for(var c=0;c<draw.CmdBuffer.Size;c++){var cmd=draw.CmdBuffer[c];if(cmd.TextureId.Handle!=id)continue;for(var n=0u;n<cmd.ElemCount;n++)yield return (int)(draw.IdxBuffer[(int)(cmd.IdxOffset+n)]+cmd.VtxOffset);}}
 void Configure(object? look,string avatar,float time){Set(companion,"currentLook",look);Set(companion,"displayAvatar",avatar);Set(companion,"poseTime",time);Set(companion,"visualGrip",null);requested.Clear();}
 void DrawLook(ImDrawListPtr draw,object?look,string avatar,Vector2 position,float size,bool mirror,string pose,float time){
  Configure(look,avatar,time);var first=draw.VtxBuffer.Size;object?[]args=[draw,position,new Vector2(size),mirror,E("Ui.CompanionPose",pose),0f,null];
  Check((bool)Call(companion,"TryDrawLook",args)!,avatar+" actual look draw succeeds.");var file=look==null?"orbit-core.png":(string)Get(Get(look,"Normal"),"File");var ids=Indices(draw,textures[file].Id).Where(i=>i>=first).Distinct().ToArray();
  Check(ids.Length==(pose=="Walk"&&avatar is "Ember" or "Mochi" or "Puff"?256:4),avatar+" draws exactly one body quad or64mesh cells.");
  Check(requested.Count==1&&requested[0]==file,"Production draw does not silently substitute another look texture.");
  if(avatar=="Orbit")Check(ids.Min()>first&&ids.Max()<draw.VtxBuffer.Size-1,"Orbit draws ring back, core, then ring front.");
  var grip=(Vector2)Field(companion,"visualGrip")!;
  Check(float.IsFinite(grip.X)&&float.IsFinite(grip.Y),"Actual visual grip is finite.");
  if(pose=="Carry"){
   var source=look==null?((IEnumerable)Get(library,"OriginalOrbitCore")).Cast<object>().ElementAt(8):((IEnumerable)Get(look,"Frames")).Cast<object>().ElementAt((int)(time/.4f)%2==0?4:5);
   var sourceUv=(Vector2)Get(source,"Grip")/(Vector2)Get(source,"SheetSize");var aBody=draw.VtxBuffer[ids[0]];var bBody=draw.VtxBuffer[ids[1]];var dBody=draw.VtxBuffer[ids[3]];
   var mappedGrip=aBody.Pos+(bBody.Pos-aBody.Pos)*((sourceUv.X-aBody.Uv.X)/(bBody.Uv.X-aBody.Uv.X))+(dBody.Pos-aBody.Pos)*((sourceUv.Y-aBody.Uv.Y)/(dBody.Uv.Y-aBody.Uv.Y));
   Check(Vector2.Distance(mappedGrip,grip)<.001f,"Actual painted source grip equals Roaming.visualGrip, including Orbit.");
   if(time>=.18f)Check(Vector2.Distance(grip,position+new Vector2(size*.5f))<.001f,"Settled carry is pinned to logical centre.");
   else{var normal=look==null?((IEnumerable)Get(library,"OriginalOrbitCore")).Cast<object>().First():Get(look,"Normal");Vector2 original;
    if(avatar=="Orbit"){var place=T("Ui.OrbitRingRenderer").GetMethod("Place",S)!.Invoke(null,[normal,normal,position,new Vector2(size),mirror])!;var body=T("Ui.SeasonalBodyTransform").GetMethod("FromFrame",S)!.Invoke(null,[Get(normal,"Bounds"),Get(normal,"SourceScale"),Get(place,"Frame"),mirror])!;original=(Vector2)Call(body,"Map",Get(normal,"Grip"))!;}
    else original=(Vector2)Call(normal,"Map",Get(normal,"Grip"),position,new Vector2(size),mirror)!;
    var t=time/.18f;t=t*t*(3-2*t);Check(Vector2.Distance(grip,Vector2.Lerp(original,position+new Vector2(size*.5f),t))<.001f,"Carry pickup eases from original grip.");}
   Set(companion,"handWasDragging",true);Set(companion,"handTime",.3f);var hfirst=draw.VtxBuffer.Size;
   Call(companion,"DrawGrabHand",draw,Vector2.Zero,io.DisplaySize,new Vector2(size),0f,true,null,grip);
   var hv=Indices(draw,textures["companion-grab-hand.png"].Id).Where(i=>i>=hfirst).Distinct().ToArray();Check(hv.Length==4,"Actual hand draws one held image.");
   var a=draw.VtxBuffer[hv[0]].Pos;var b=draw.VtxBuffer[hv[2]].Pos;var contact=a+(b-a)*new Vector2(.5f,.77f);Check(Vector2.Distance(contact,grip)<.001f,"Actual held hand contact equals Roaming.visualGrip.");
  }
 }
 if(!args.Contains("--seasonal-only")){
 foreach(var size in new[]{96f,192f})foreach(var mirror in new[]{false,true})foreach(var pose in args.Contains("--carry-only")?new[]{"Carry"}:new[]{"Walk","Carry"}){
  var tile=new Vector2(size+48,size+90);io.DisplaySize=new(tile.X*4,tile.Y*((looks.Length+3)/4)+30);ImGui.NewFrame();var draw=ImGui.GetForegroundDrawList();
  for(var i=0;i<looks.Length;i++){var look=looks[i];var row=i/4;var col=i%4;var p=new Vector2(col*tile.X+24,row*tile.Y+32);var avatar=(string)Get(look,"Avatar");draw.AddText(new(col*tile.X+6,row*tile.Y+7),0xFFFFFFFF,avatar+" "+Get(look,"Choice"));DrawLook(draw,look,avatar,p,size,mirror,pose,pose=="Carry"?.5f:.3f);}
  ImGui.Render();painter.Save(ImGui.GetDrawData(),Path.Combine(output.FullName,$"actual-{pose}-{size:0}-{(mirror?"left":"right")}.png"));
 }
 // Early-pickup path and original Orbit use the same actual entry point.
 io.DisplaySize=new(640,440);foreach(var look in looks){ImGui.NewFrame();DrawLook(ImGui.GetForegroundDrawList(),look,(string)Get(look,"Avatar"),new(180,100),96,false,"Carry",.09f);ImGui.Render();}
 if(!holiday){foreach(var pose in args.Contains("--carry-only")?new[]{"Carry"}:new[]{"Walk","Carry"}){ImGui.NewFrame();DrawLook(ImGui.GetForegroundDrawList(),null,"Orbit",new(180,100),192,true,pose,.5f);ImGui.Render();painter.Save(ImGui.GetDrawData(),Path.Combine(output.FullName,"original-orbit-"+pose+".png"));}
 foreach(var mirror in new[]{false,true}){ImGui.NewFrame();DrawLook(ImGui.GetForegroundDrawList(),null,"Orbit",new(180,100),96,mirror,"Carry",.09f);ImGui.Render();}}
 // Representative look for every avatar, plus original Orbit: enter, sleep, wake stay on the chosen sheet.
 if(holiday){var napSeasonal=New("Ui.SeasonalCompanionService",config);var napPolicy=New("Ui.SeasonalCompanionPolicy",new object?[]{null});var noFestivals=Array.CreateInstance(T("Ui.SeasonalFestival"),0);Set(napSeasonal,"snapshot",Call(napPolicy,"Resolve",true,"AllSaintsWake",true,false,noFestivals,noFestivals,new DateTimeOffset(2026,10,22,0,0,0,TimeSpan.Zero)));Set(companion,"seasonal",napSeasonal);Set(companion,"seasonalArtwork",New("Ui.SeasonalCompanionRenderer",assets));Set(companion,"seasonalVisible",true);}
 var selectedNaps=looks.Where(l=>(string)Get(l,"Choice")== (holiday?"HolidayAllSaintsWake":"Look1")).Cast<object?>();var napLooks=(holiday?selectedNaps:selectedNaps.Append(null)).ToArray();
 if(!args.Contains("--carry-only"))foreach(var size in new[]{96f,192f})foreach(var mirror in new[]{false,true}){
  var tile=new Vector2(size+40,size+52);io.DisplaySize=new(tile.X*3,tile.Y*napLooks.Length+30);ImGui.NewFrame();var draw=ImGui.GetForegroundDrawList();
  for(var row=0;row<napLooks.Length;row++)for(var stateIndex=0;stateIndex<3;stateIndex++){
   var look=napLooks[row];var avatar=look==null?"Orbit":(string)Get(look,"Avatar");var state=new[]{"Entering","Sleeping","Waking"}[stateIndex];var p=new Vector2(stateIndex*tile.X+20,row*tile.Y+32);
   Configure(look,avatar,.5f);Set(routine,"State",E("Ui.CompanionHomeState",state));Set(routine,"Position",p);Set(routine,"FacingLeft",mirror);Set(routine,"ArtScale",.85f);Set(routine,"stateTime",.4f);Set(companion,"homeVisible",false);
   var layout=New("Ui.CompanionLayout",Vector2.Zero,io.DisplaySize,new Vector2(size));var first=draw.VtxBuffer.Size;object?[]napArgs=[draw,layout,null];Check((bool)Call(companion,"DrawLookNap",napArgs)!,"Actual nap renderer handles selected look.");
   var expected=look==null?"orbit-core.png":(string)Get(((IEnumerable)Get(look,"Frames")).Cast<object>().ElementAt(stateIndex==0?6:stateIndex==1?3:7),"File");
   Check(requested.SequenceEqual(new[]{expected,"companion-house.png"}),"Nap uses same look's enter/closed/wake source and hides accessories inside the house.");Check(ImageVertices(draw,textures[expected].Id,first)==4,"Nap paints one selected body image.");
   draw.AddText(new(stateIndex*tile.X+6,row*tile.Y+7),0xFFFFFFFF,avatar+(look==null?" base ":" ")+new[]{"Enter","Sleep","Wake"}[stateIndex]);
  }
  ImGui.Render();painter.Save(ImGui.GetDrawData(),Path.Combine(output.FullName,$"actual-naps-{size:0}-{(mirror?"left":"right")}.png"));
 }
 }
 if(!args.Contains("--carry-only")){
 Set(routine,"State",E("Ui.CompanionHomeState","Roaming"));
 var seasonal=New("Ui.SeasonalCompanionService",config);var policy=New("Ui.SeasonalCompanionPolicy",new object?[]{null});var festivals=Array.CreateInstance(T("Ui.SeasonalFestival"),0);
 var snapshot=Call(policy,"Resolve",true,"AllSaintsWake",true,false,festivals,festivals,new DateTimeOffset(2026,10,22,0,0,0,TimeSpan.Zero));Set(seasonal,"snapshot",snapshot);
 var seasonalArt=New("Ui.SeasonalCompanionRenderer",assets);Set(companion,"seasonal",seasonal);Set(companion,"seasonalArtwork",seasonalArt);Set(companion,"seasonalVisible",true);
 if(holiday){
  Set(config,"CompanionHolidayBodiesEnabled",true);
  foreach(var look in looks){var avatar=(string)Get(look,"Avatar");var themeName=((string)Get(look,"Choice"))[7..];Set(companion,"displayAvatar",avatar);var baseline=Call(baseLibrary,"Find",avatar,"Look1");
   Set(seasonal,"snapshot",Call(policy,"Resolve",true,themeName,true,false,festivals,festivals,new DateTimeOffset(2026,10,22,0,0,0,TimeSpan.Zero)));
   Call(companion,"ResolveLook",baseline,true);Check(ReferenceEquals(Field(companion,"currentLook"),look),"Actual resolver selects the holiday body for the current theme.");
   Set(config,"CompanionHolidayBodiesEnabled",false);Call(companion,"ResolveLook",baseline,false);Check(ReferenceEquals(Field(companion,"currentLook"),look),"Carry/nap switch lock preserves the current holiday body.");
   Call(companion,"ResolveLook",baseline,true);Check(ReferenceEquals(Field(companion,"currentLook"),baseline),"Disabling holiday bodies restores selected ordinary look after unlock.");Set(config,"CompanionHolidayBodiesEnabled",true);
  }
  Set(companion,"displayAvatar","Moonblob");var baseLook=Call(baseLibrary,"Find","Moonblob","Look1");
  Set(seasonal,"snapshot",Call(policy,"Resolve",false,"Auto",true,false,festivals,festivals,new DateTimeOffset(2026,10,22,0,0,0,TimeSpan.Zero)));Call(companion,"ResolveLook",baseLook,true);Check(ReferenceEquals(Field(companion,"currentLook"),baseLook),"No active theme preserves ordinary look.");
  var empty=Directory.CreateDirectory(Path.GetFullPath(".local/companion-holiday-integration/absent-art"));Set(companion,"holidayBodies",New("Ui.CompanionHolidayLibrary",empty.FullName));Set(seasonal,"snapshot",snapshot);Call(companion,"ResolveLook",baseLook,true);Check(ReferenceEquals(Field(companion,"currentLook"),baseLook),"Missing optional holiday set falls back through actual resolver.");Set(companion,"holidayBodies",library);
 }
 foreach(var mirror in new[]{false,true}){
  const float size=192;var tile=new Vector2(size*2.2f+20,size*2.2f+36);io.DisplaySize=new(tile.X*4,tile.Y*((looks.Length+3)/4)+30);ImGui.NewFrame();var draw=ImGui.GetForegroundDrawList();
  for(var i=0;i<looks.Length;i++){
   var activeTheme=holiday?((string)Get(looks[i],"Choice"))[7..]:"AllSaintsWake";Set(seasonal,"snapshot",Call(policy,"Resolve",true,activeTheme,true,false,festivals,festivals,new DateTimeOffset(2026,10,22,0,0,0,TimeSpan.Zero)));
   var look=looks[i];var avatar=(string)Get(look,"Avatar");var p=new Vector2(i%4*tile.X+size*.6f+10,i/4*tile.Y+size*.6f+26);Configure(look,avatar,.3f);var first=draw.VtxBuffer.Size;object?[]lookArgs=[draw,p,new Vector2(size),mirror,E("Ui.CompanionPose","Walk"),0f,null];Check((bool)Call(companion,"TryDrawLook",lookArgs)!,"Seasonal variant draws through actual DLL.");
   var source=Get(look,"Normal");object body;
   if(avatar=="Orbit"){
    var placed=T("Ui.OrbitRingRenderer").GetMethod("Place",S)!.Invoke(null,[source,source,p,new Vector2(size),mirror])!;
    body=T("Ui.SeasonalBodyTransform").GetMethod("FromFrame",S)!.Invoke(null,[Get(source,"Bounds"),Get(source,"SourceScale"),Get(placed,"Frame"),mirror])!;
   }else{
    var atlas=Get(look,"NormalAtlas");var q=Call(atlas,avatar=="Moonblob"?"GetRoll":"GetFloat",.23f,mirror,p,new Vector2(size))!;
    body=New("Ui.SeasonalBodyTransform",Get(source,"Bounds"),Get(source,"SourceScale"),Get(q,"A"),Get(q,"B"),Get(q,"D"));
   }
   var theme=E("Ui.SeasonalCompanionTheme",activeTheme);var fit=New("Ui.SeasonalAccessoryFit",Get(source,"AccessoryAnchor"),Get(source,"AccessoryHeadWidth"));object?[]quadArgs=[theme,body,fit,null];
   var method=seasonalArt.GetType().GetMethods(F).Single(m=>m.Name=="TryGetQuad"&&m.GetParameters().Length==4&&m.GetParameters()[2].ParameterType==fit.GetType());
   Check((bool)method.Invoke(seasonalArt,quadArgs)!,"Source-specific attachment anchor resolves.");var expected=quadArgs[3]!;
   var ids=Indices(draw,textures["seasonal-accessories.png"].Id).Where(n=>n>=first).Distinct().ToArray();Check(ids.Length==4,"Exactly one seasonal accessory quad is drawn.");
   var crown=(Vector2)Call(body,"Map",Get(source,"AccessoryAnchor"))!;var offset=avatar is "Ember" or "Mochi" or "Puff"?(Vector2)Call(motion,"WarpAttachment",avatar,crown,p,new Vector2(size),.23f,mirror,true)!-crown:Vector2.Zero;
   foreach(var (name,n) in new[]{("A",0),("B",1),("C",2),("D",3)}){
    var target=(Vector2)Get(expected,name)+offset;
    Check(Vector2.Distance(target,draw.VtxBuffer[ids[n]].Pos)<.001f,"Actual seasonal corner uses the fitted crown and one rigid translation.");
   }
   Check(requested.SequenceEqual(new[]{(string)Get(source,"File"),"seasonal-accessories.png"}),"Seasonal body remains on selected neutral sheet.");
   draw.AddText(new(i%4*tile.X+6,i/4*tile.Y+7),0xFFFFFFFF,avatar+" "+Get(look,"Choice"));
  }
  ImGui.Render();painter.Save(ImGui.GetDrawData(),Path.Combine(output.FullName,$"actual-seasonal-192-{(mirror?"left":"right")}.png"));
 }} Console.WriteLine($"Compiled variant integration: {count} checks passed; mode={(holiday?"holiday72looks":args.Contains("--seasonal-only") ? "seasonal64cases" : args.Contains("--carry-only")?"carry":"full")}. No game/framework calls.");
}finally{ImGui.DestroyContext(context);}
public class LocalProxy:DispatchProxy{
 public Func<MethodInfo,object?[],object?> Handler{get;set;}=null!;
 protected override object? Invoke(MethodInfo? method,object?[]?args)=>Handler(method!,args??[]);
 public static T Create<T>(Func<MethodInfo,object?[],object?>handler)where T:class{var p=Create<T,LocalProxy>();((LocalProxy)(object)p).Handler=handler;return p;}
 public static object Create(Type type,Func<MethodInfo,object?[],object?>handler){var p=DispatchProxy.Create(type,typeof(LocalProxy));((LocalProxy)p).Handler=handler;return p;}
}
