using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit;
using OmniKit.Ui;
var assets=Path.GetFullPath("assets");var output=Directory.CreateDirectory(Path.GetFullPath(".local/seasonal-fit/native"));
var baseArt=new CompanionAnimationLibrary(assets);var naps=new CompanionNapArtwork(assets);var variants=new CompanionVariantLibrary(assets);var fitted=new SeasonalCompanionRenderer(assets);
if(!fitted.IsAvailable||!variants.IsAvailable)throw new Exception("Current fit metadata failed loading.");
var beforeDir=Directory.CreateDirectory(Path.GetFullPath(".local/seasonal-fit/before-assets"));
var original=Path.GetFullPath(".local/seasonal-fit/before-seasonal-accessories.frames.json");
SeasonalCompanionRenderer? before=null;if(File.Exists(original)){File.Copy(original,Path.Combine(beforeDir.FullName,"seasonal-accessories.frames.json"),true);before=new(beforeDir.FullName);}
var native=ImGui.CreateContext();
try{
 var io=ImGui.GetIO();io.DeltaTime=1f/60;unsafe{io.IniFilename=null;io.LogFilename=null;}io.Fonts.AddFontDefault();io.Fonts.Build();var painter=new SoftwareRenderer();var textures=new Dictionary<string,ulong>();
 foreach(var file in variants.Files.Concat(Configuration.AvatarNames.Select(a=>{baseArt.TryGet(a,"Default",out var atlas);return atlas!.File;})).Concat(Configuration.AvatarNames.Select(a=>{naps.TryGet(a,"Default",out var nap);return nap!.File;})).Append("seasonal-accessories.png").Distinct()){
  using var bitmap=new Bitmap(Path.Combine(assets,file));var bits=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);var pixels=new byte[bitmap.Width*bitmap.Height*4];try{for(var y=0;y<bitmap.Height;y++)Marshal.Copy(bits.Scan0+y*bits.Stride,pixels,y*bitmap.Width*4,bitmap.Width*4);}finally{bitmap.UnlockBits(bits);}for(var i=0;i<pixels.Length;i+=4)(pixels[i],pixels[i+2])=(pixels[i+2],pixels[i]);var id=(ulong)textures.Count+2;painter.AddTexture(id,pixels,bitmap.Width,bitmap.Height);textures.Add(file,id);
 }
 var checks=0;var padding=0f;var rollingPadding=0f;var movingPadding=0f;
 void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 int OrbitPose(int pose)=>pose>=24?pose-24+12:pose>=16?pose-8:pose==0?0:Math.Min(pose,7);
 (string File,CompanionFrame Frame,SeasonalBodyTransform Transform) Base(string avatar,int pose,Vector2 position,Vector2 size,bool mirrored){
  if(avatar=="Orbit"){var source=variants.OriginalOrbitCore[OrbitPose(pose)];var coreFrame=source.GetFrame(position,size,mirrored);return(source.File,coreFrame,SeasonalBodyTransform.FromFrame(source.Bounds,source.SourceScale,coreFrame,mirrored));}
  if(pose>=24){naps.TryGet(avatar,"Default",out var sheet);var c=sheet!.Frames[pose-24];var frame=sheet.Frame(pose-24,position,size,1,mirrored);return(sheet.File,frame,SeasonalBodyTransform.FromFrame(new(c.Bounds[0],c.Bounds[1],c.Bounds[2],c.Bounds[3]),sheet.ScaleReference,frame,mirrored));}
  baseArt.TryGet(avatar,"Default",out var atlas);var f=atlas!.GetFrame(pose,mirrored,position,size);return(atlas.File,f,SeasonalBodyTransform.FromFrame(atlas.Frames[pose].Rect,atlas.SourceScale,f,mirrored));
 }
 bool BaseQuad(SeasonalCompanionRenderer renderer,SeasonalCompanionTheme theme,string avatar,int pose,SeasonalBodyTransform body,out SeasonalOverlayQuad q){
  if(avatar=="Orbit"){var source=variants.OriginalOrbitCore[OrbitPose(pose)];return renderer.TryGetQuad(theme,body,new SeasonalAccessoryFit(source.AccessoryAnchor,source.AccessoryHeadWidth),out q);}
  return renderer.TryGetQuad(theme,avatar,pose>=24?pose-24:pose,pose>=24,body,out q);
 }
 void Bound(SeasonalOverlayQuad q,Vector2 size){foreach(var p in new[]{q.A,q.B,q.C,q.D}){Check(float.IsFinite(p.X)&&float.IsFinite(p.Y),"Finite fitted accessory.");padding=Math.Max(padding,Math.Max(Math.Max(-p.X,-p.Y),Math.Max(p.X-size.X,p.Y-size.Y))/size.Y);}}
 foreach(var avatar in Configuration.AvatarNames)foreach(var theme in Enum.GetValues<SeasonalCompanionTheme>())foreach(var mirrored in new[]{false,true})for(var pose=0;pose<28;pose++){
  var body=Base(avatar,pose,Vector2.Zero,new(96),mirrored);Check(BaseQuad(fitted,theme,avatar,pose,body.Transform,out var q),"Base per-pose fit resolves.");Bound(q,new(96));
 }
 foreach(var look in variants.Looks)foreach(var theme in Enum.GetValues<SeasonalCompanionTheme>())foreach(var mirrored in new[]{false,true})foreach(var source in look.Frames){
  var f=source.GetFrame(Vector2.Zero,new(96),mirrored);var body=SeasonalBodyTransform.FromFrame(source.Bounds,source.SourceScale,f,mirrored);
  Check(fitted.TryGetQuad(theme,body,new SeasonalAccessoryFit(source.AccessoryAnchor,source.AccessoryHeadWidth),out var q),"Variant per-body fit resolves.");Bound(q,new(96));
 }
 // Exact full-circle bound: every rigid hat corner traces a circle around the rolling body's centre.
 baseArt.TryGet("Moonblob","Default",out var blob);
 foreach(var atlas in variants.Looks.Where(l=>l.Avatar=="Moonblob").Select(l=>l.NormalAtlas).Prepend(blob!))foreach(var theme in Enum.GetValues<SeasonalCompanionTheme>()){
  var roll=atlas.GetRoll(0,false,Vector2.Zero,new(96));var source=atlas.Frames[0];var body=new SeasonalBodyTransform(source.Rect,atlas.SourceScale,roll.A,roll.B,roll.D);
  var look=variants.Looks.FirstOrDefault(l=>l.Avatar=="Moonblob"&&l.Normal.File==atlas.File&&l.Normal.Bounds==source.Rect);
  SeasonalOverlayQuad q;Check(look==null?fitted.TryGetQuad(theme,"Moonblob",0,false,body,out q):fitted.TryGetQuad(theme,body,new SeasonalAccessoryFit(look.Normal.AccessoryAnchor,look.Normal.AccessoryHeadWidth),out q),"Rolling hat resolves.");
  var radius=new[]{q.A,q.B,q.C,q.D}.Max(p=>Vector2.Distance(p,roll.Center));var closestEdge=Math.Min(Math.Min(roll.Center.X,roll.Center.Y),Math.Min(96-roll.Center.X,96-roll.Center.Y));rollingPadding=Math.Max(rollingPadding,(radius-closestEdge)/96);
 }
 // Caps remain rigid: the actual body warp moves only the measured crown; its translation moves every hat corner.
 var motion=new CompanionBodyMotion();for(var i=0;i<80;i++)motion.Update(.05f,true,true);
 foreach(var avatar in new[]{"Ember","Mochi","Puff"}){
  baseArt.TryGet(avatar,"Default",out var normal);
  Check(fitted.TryGetSourceAnchor(avatar,0,false,out var baseCrown),"Base body exposes its measured crown.");
  foreach(var atlas in variants.Looks.Where(l=>l.Avatar==avatar).Select(l=>l.NormalAtlas).Prepend(normal!)){
   var region=atlas.Frames[0];var look=variants.Looks.FirstOrDefault(l=>l.Avatar==avatar&&l.Normal.File==atlas.File&&l.Normal.Bounds==region.Rect);
   for(var time=0;time<16;time++){for(var j=0;j<8;j++)motion.Update(.05f,true,true);for(var phase=0;phase<16;phase++)foreach(var theme in Enum.GetValues<SeasonalCompanionTheme>()){
    var qbody=atlas.GetFloat(phase/16f,false,Vector2.Zero,new(96));var body=new SeasonalBodyTransform(region.Rect,atlas.SourceScale,qbody.A,qbody.B,qbody.D);SeasonalOverlayQuad hat;
    Check(look==null?fitted.TryGetQuad(theme,avatar,0,false,body,out hat):fitted.TryGetQuad(theme,body,new SeasonalAccessoryFit(look.Normal.AccessoryAnchor,look.Normal.AccessoryHeadWidth),out hat),"Deformed body's hat resolves.");
    var crown=body.Map(look?.Normal.AccessoryAnchor??baseCrown);
    var shift=motion.WarpAttachment(avatar,crown,Vector2.Zero,new(96),phase/16f,false,true)-crown;
    foreach(var corner in new[]{hat.A,hat.B,hat.C,hat.D}){var p=corner+shift;movingPadding=Math.Max(movingPadding,Math.Max(Math.Max(-p.X,-p.Y),Math.Max(p.X-96,p.Y-96))/96);}
   }}
  }
 }
 Check(Math.Max(Math.Max(padding,rollingPadding),movingPadding)<=SeasonalCompanionRenderer.RequiredVisualPaddingFraction,$"Reserved visual gutter contains hats: static={padding:0.0000}, roll={rollingPadding:0.0000}, warp={movingPadding:0.0000}.");
 var testBody=Base("Ember",0,Vector2.Zero,new(96),false).Transform;
 Check(!fitted.TryGetQuad(SeasonalCompanionTheme.AllSaintsWake,testBody,new SeasonalAccessoryFit(new(float.NaN,1),50),out _),"Invalid crown is rejected.");
 Check(!fitted.TryGetQuad(SeasonalCompanionTheme.AllSaintsWake,testBody,new SeasonalAccessoryFit(new(testBody.SourceRect.X,testBody.SourceRect.Y),float.NaN),out _),"Invalid head width is rejected.");
 Check(!fitted.TryGetSourceAnchor("unknown",0,false,out _)&&!fitted.TryGetSourceAnchor("Ember",24,false,out _)&&!fitted.TryGetSourceAnchor("Ember",-1,true,out _),"Unknown bodies and out-of-range crown indexes fail closed.");
 void DrawBase(ImDrawListPtr draw,string avatar,int pose,Vector2 position,Vector2 size,bool mirrored,SeasonalCompanionRenderer renderer,SeasonalCompanionTheme theme=SeasonalCompanionTheme.AllSaintsWake){
  var body=Base(avatar,pose,position,size,mirrored);draw.AddImage(new(textures[body.File]),body.Frame.Position,body.Frame.Position+body.Frame.Size,body.Frame.UvStart,body.Frame.UvEnd);
  if(!BaseQuad(renderer,theme,avatar,pose,body.Transform,out var q))throw new Exception("Base draw fit missing.");SeasonalCompanionRenderer.Draw(draw,new(textures["seasonal-accessories.png"]),q);
 }
 void DrawVariant(ImDrawListPtr draw,CompanionVariantLook look,CompanionVariantPose pose,Vector2 position,Vector2 size,bool mirrored){
  var source=look[pose];var f=source.GetFrame(position,size,mirrored);var first=draw.VtxBuffer.Size;CompanionVariantRenderer.Draw(draw,new(textures[source.File]),f);var bodyVertices=Enumerable.Range(first,4).Select(i=>draw.VtxBuffer[i]).ToArray();
  var body=SeasonalBodyTransform.FromFrame(source.Bounds,source.SourceScale,f,mirrored);if(!fitted.TryGetQuad(SeasonalCompanionTheme.AllSaintsWake,body,new SeasonalAccessoryFit(source.AccessoryAnchor,source.AccessoryHeadWidth),out var q))throw new Exception("Variant draw fit missing.");SeasonalCompanionRenderer.Draw(draw,new(textures["seasonal-accessories.png"]),q);
  Check(Enumerable.Range(0,4).All(i=>draw.VtxBuffer[first+i].Pos==bodyVertices[i].Pos&&draw.VtxBuffer[first+i].Uv==bodyVertices[i].Uv),"Fitting a hat never changes body geometry/UV.");
 }
 var poses=args.Contains("--themes-only")?Array.Empty<string>():args.Contains("--first-pass")?new[]{"normal"}:new[]{"normal","carry","sleep"};
 foreach(var size in new[]{96f,192f})foreach(var mirrored in (args.Contains("--first-pass")?new[]{false}:new[]{false,true}))foreach(var pose in poses){
  var tile=new Vector2(size*1.6f,size*1.8f+24);io.DisplaySize=new(tile.X*5,tile.Y*8+20);ImGui.NewFrame();var draw=ImGui.GetForegroundDrawList();
  for(var row=0;row<8;row++)for(var col=0;col<5;col++){
   var avatar=Configuration.AvatarNames[row];var p=new Vector2(col*tile.X+size*.3f,28+row*tile.Y+size*.55f);draw.AddText(new(col*tile.X+4,row*tile.Y+8),0xFFFFFFFF,avatar+" "+(col==0?"Base":"Look"+col));
   if(col==0)DrawBase(draw,avatar,pose=="normal"?0:pose=="carry"?16:26,p,new(size),mirrored,fitted);
   else DrawVariant(draw,variants.Find(avatar,"Look"+col)!,pose=="normal"?CompanionVariantPose.Neutral:pose=="carry"?CompanionVariantPose.CarrySquirmA:CompanionVariantPose.ClosedBlink,p,new(size),mirrored);
  }
  ImGui.Render();painter.Save(ImGui.GetDrawData(),Path.Combine(output.FullName,$"fit-all40-{pose}-{size:0}-{(mirrored?"left":"right")}.png"));
 }
 {const float size=96;var tile=new Vector2(154,202);var themes=Enum.GetValues<SeasonalCompanionTheme>();io.DisplaySize=new(tile.X*themes.Length,tile.Y*8);ImGui.NewFrame();var draw=ImGui.GetForegroundDrawList();for(var row=0;row<8;row++)for(var col=0;col<themes.Length;col++){var avatar=Configuration.AvatarNames[row];var p=new Vector2(col*tile.X+29,row*tile.Y+67);DrawBase(draw,avatar,0,p,new(size),false,fitted,themes[col]);draw.AddText(new(col*tile.X+5,row*tile.Y+5),0xFFFFFFFF,avatar);draw.AddText(new(col*tile.X+5,row*tile.Y+20),0xFFBBBBBB,themes[col].ToString());}ImGui.Render();painter.Save(ImGui.GetDrawData(),Path.Combine(output.FullName,"all-nine-themes-base96.png"));}
 if(before!=null){const float size=192;var tile=new Vector2(308,376);io.DisplaySize=new(tile.X*2,tile.Y*8);ImGui.NewFrame();var draw=ImGui.GetForegroundDrawList();for(var row=0;row<8;row++)for(var col=0;col<2;col++){var avatar=Configuration.AvatarNames[row];var p=new Vector2(col*tile.X+58,row*tile.Y+110);DrawBase(draw,avatar,0,p,new(size),false,col==0?before:fitted);draw.AddText(new(col*tile.X+8,row*tile.Y+12),0xFFFFFFFF,avatar+(col==0?" previous":" fitted"));}ImGui.Render();painter.Save(ImGui.GetDrawData(),Path.Combine(output.FullName,"base-before-after-192.png"));}
 Console.WriteLine($"Seasonal fit: {checks} checks passed. Static padding={padding:0.0000}; full-circle rolling={rollingPadding:0.0000}; full-strength warp={movingPadding:0.0000}; raw PNGs unchanged.");
}finally{ImGui.DestroyContext(native);}
