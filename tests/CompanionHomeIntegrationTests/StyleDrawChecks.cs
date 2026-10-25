using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using LauncherUiSmoke;
using static IntegrationFixture;

internal static class StyleDrawChecks
{
    internal static unsafe void Run(IntegrationFixture f)
    {
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.IniFilename = null; io.LogFilename = null; io.DeltaTime = 1f / 60;
            io.Fonts.AddFontDefault(); io.Fonts.Build();
            var painter = new SoftwareRenderer();
            var textureIds = new Dictionary<string, ulong>(StringComparer.Ordinal);
            foreach (var file in new[] { "companion-house.png", "homes-standard.png", "homes-holiday.png", "companion-grab-hand.png", "hands-styles.png" })
            {
                using var bitmap = new Bitmap(Path.Combine("assets", file));
                var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var bytes = new byte[bitmap.Width * bitmap.Height * 4]; Marshal.Copy(bits.Scan0, bytes, 0, bytes.Length); bitmap.UnlockBits(bits);
                for (var i = 0; i < bytes.Length; i += 4) (bytes[i], bytes[i + 2]) = (bytes[i + 2], bytes[i]);
                var id = (ulong)textureIds.Count + 2; textureIds.Add(file, id); painter.AddTexture(id, bytes, bitmap.Width, bitmap.Height);
            }
            string? requestedTexture = null;
            object Texture(Type type, ulong handle) => TextureProxy.Create(type, (method, _) => method.Name switch
            {
                "get_Handle" => new ImTextureID(handle), "get_Width" or "get_Height" => 1280,
                "GetWrapOrDefault" => Texture(method.ReturnType, handle),
                _ => throw new InvalidOperationException("Unexpected texture stub member."),
            });
            f.Type("Services").GetProperty("Textures", Static)!.SetValue(null, TextureProxy.Create(typeof(ITextureProvider), (method, values) =>
            {
                if (method.Name != "GetFromFile") throw new InvalidOperationException("Unexpected service access.");
                requestedTexture = Path.GetFileName(values[0]!.ToString()!);
                return Texture(method.ReturnType, textureIds[requestedTexture]);
            }));
            var library = f.New("Ui.CompanionHomeStyleLibrary", Path.GetFullPath("assets"));
            var config = f.New("Configuration"); Set(config, "CompanionHolidayHomeEnabled", false);
            var companion = f.Companion(config, library); var routine = Field(companion, "homeRoutine");
            var homes = new[] { "Default", "CrystalPod", "CloudCushion", "LanternHut", "AllSaintsWake", "Starlight", "Heavensturn", "Valentione", "LittleLadies", "HatchingTide", "MakeItRain", "MoonfireFaire", "TheRising" };
            var output = Path.GetFullPath(".local/visualharness/companion-home-integration"); Directory.CreateDirectory(output);
            foreach (var edge in new[] { 96f, 192f })
            {
                var cell = new Vector2(edge + 28, edge + 36); io.DisplaySize = cell * new Vector2(4, 7);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                for (var i = 0; i < homes.Length * 2; i++)
                {
                    var mirrored = i >= homes.Length; var id = homes[i % homes.Length];
                    var at = new Vector2(i % 4 * cell.X, i / 4 * cell.Y) + new Vector2(14, 24);
                    var style = Call(library, "GetHome", id)!;
                    var frame = Call(style, "Place", at, at + new Vector2(edge), mirrored)!;
                    Set(companion, "homeStyleFrame", frame); Set(companion, "houseMirrored", mirrored);
                    var layout = f.New("Ui.CompanionLayout", Vector2.Zero, io.DisplaySize, new Vector2(edge));
                    draw.AddText(at - new Vector2(0, 18), 0xFFFFFFFF, id + (mirrored ? " / left" : " / right"));
                    var first = draw.VtxBuffer.Size;
                    Call(companion, "DrawHome", draw, layout);
                    f.Check(requestedTexture == (string)Get(frame, "File"), "Actual DrawHome requests the selected style's atlas.");
                    f.Check(draw.VtxBuffer.Size - first == 4, "Actual selected house draws one cropped quad.");
                    var quad = Get(frame, "Image");
                    for (var vertex = 0; vertex < 4; vertex++)
                    {
                        var letter = "ABCD"[vertex].ToString();
                        f.Check(Vector2.Distance(draw.VtxBuffer[first + vertex].Pos, (Vector2)Get(quad, letter)) < .001f
                            && Vector2.Distance(draw.VtxBuffer[first + vertex].Uv, (Vector2)Get(quad, "Uv" + letter)) < .00001f,
                            "Actual house preserves the selected crop, aspect fit and mirror.");
                    }
                }
                ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output, $"actual-homes-{edge:0}.png"));
            }
            var hands = new[] { "Default", "IvoryChibi", "StarlightGlove", "MagicalPaw", "HologramMitt" };
            foreach (var edge in new[] { 96f, 192f })
            {
                var cell = new Vector2(edge + 50, edge * 1.4f + 76); io.DisplaySize = cell * new Vector2(4, 5);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                for (var h = 0; h < hands.Length; h++)
                for (var p = 0; p < 4; p++)
                {
                    var at = new Vector2(p * cell.X, h * cell.Y); var center = at + new Vector2(cell.X * .5f, edge + 50);
                    Set(config, "CompanionHandStyle", hands[h]); Set(config, "CompanionHandSize", edge); Set(config, "CompanionSceneScale", 2f);
                    Set(config, "CompanionSize", edge == 96 ? 192f : 48f);
                    var selection = Field(companion, "styleSelection"); var hand = Call(selection, "BeginPickup", hands[h])!;
                    var pose = Enum.ToObject(f.Type("Ui.CompanionHandPose"), p); var metadata = Call(hand, "Pose", pose)!;
                    var time = p switch { 0 => 1f, 1 => .1f, 2 => .3f, _ => .05f };
                    var dragging = p is 1 or 2;
                    Set(companion, "handTime", time); Set(companion, "handWasDragging", dragging); Set(companion, "handPoint", center);
                    draw.AddText(at + new Vector2(7, 5), 0xFFFFFFFF, hands[h] + " / " + pose);
                    draw.AddCircleFilled(center, edge * .25f, 0xFFB5CBAE, 32);
                    var first = draw.VtxBuffer.Size;
                    Call(companion, "DrawGrabHand", draw, Vector2.Zero, io.DisplaySize, .5f, 0f, dragging,
                        p == 0 ? center : null, dragging ? center : null);
                    f.Check(draw.VtxBuffer.Size - first == 4 && requestedTexture == (string)Get(metadata, "File"),
                        "Actual hand cue uses the selected pose and texture.");
                    var sourceScale = edge / (float)Get(metadata, "SourceUnitsPerLogicalCell");
                    var contact = draw.VtxBuffer[first].Pos + (Vector2)Get(metadata, "GripInCropPixels") * sourceScale;
                    var expected = center - new Vector2(0, p == 1 ? edge * .1f : p == 3 ? edge * .07f : 0);
                    f.Check(Vector2.Distance(contact, expected) < .001f, "Actual hand's measured finger contact matches the body/house center and cue lift.");
                    f.Check(Vector2.Distance(draw.VtxBuffer[first + 2].Pos - draw.VtxBuffer[first].Pos,
                        (Vector2)Get(metadata, "CropSize") * sourceScale) < .001f, "Actual hand keeps fixed source scale between poses.");
                    for (var vertex = first; vertex < draw.VtxBuffer.Size; vertex++)
                    {
                        var point = draw.VtxBuffer[vertex].Pos;
                        f.Check(point.X >= at.X && point.Y >= at.Y + 22 && point.X <= at.X + cell.X && point.Y <= at.Y + cell.Y,
                            "The diagnostic hand has a complete visible crop and a clear label gutter.");
                    }
                    draw.AddCircle(center, 3, 0xFF88EEFF, 16, 1);
                }
                ImGui.Render(); painter.Save(ImGui.GetDrawData(), Path.Combine(output, $"actual-hands-{edge:0}.png"));
            }

            // Exercise caching through the actual roaming entry points, not only the selector helper.
            io.DisplaySize = new(640, 440);
            var box = f.New("Ui.CompanionHomeRect", new Vector2(460, 280), new Vector2(585, 405));
            Set(config, "CompanionHomeStyle", "Random");
            Call(companion, "UpdateHomeStyle", box, Vector2.Zero, io.DisplaySize);
            var selectedHome = Get(Field(companion, "homeStyleFrame"), "Style");
            for (var tick = 0; tick < 60; tick++)
            {
                Call(companion, "UpdateHomeStyle", box, Vector2.Zero, io.DisplaySize);
                f.Check(ReferenceEquals(selectedHome, Get(Field(companion, "homeStyleFrame"), "Style")), "Actual house Random stays cached across frames.");
            }
            Set(routine, "<State>k__BackingField", Enum.Parse(f.Type("Ui.CompanionHomeState"), "Sleeping"));
            Set(config, "CompanionHomeStyle", "LanternHut");
            Call(companion, "UpdateHomeStyle", box, Vector2.Zero, io.DisplaySize);
            f.Check(ReferenceEquals(selectedHome, Get(Field(companion, "homeStyleFrame"), "Style")), "Changing home choice during sleep preserves the occupied house.");
            Set(routine, "<State>k__BackingField", Enum.Parse(f.Type("Ui.CompanionHomeState"), "Roaming"));
            Call(companion, "UpdateHomeStyle", box, Vector2.Zero, io.DisplaySize);
            f.Check((string)Get(Get(Field(companion, "homeStyleFrame"), "Style"), "Id") == "LanternHut", "Pending house choice applies once the companion is outside.");

            Set(config, "CompanionHandStyle", "Random"); Set(config, "CompanionHandSize", 38.4f); Set(config, "CompanionSceneScale", 1f);
            Set(companion, "handWasDragging", false);
            object? selectedHand = null;
            for (var tick = 0; tick < 25; tick++)
            {
                ImGui.NewFrame();
                Call(companion, "DrawGrabHand", ImGui.GetForegroundDrawList(), Vector2.Zero, io.DisplaySize,
                    1f, .016f, tick < 20, null, new Vector2(320, 220));
                var current = Get(Field(companion, "styleSelection"), "CurrentHand"); selectedHand ??= current;
                f.Check(ReferenceEquals(selectedHand, current), "Actual random hand stays fixed through one pickup, hold and release.");
                ImGui.Render();
            }
        }
        finally { ImGui.DestroyContext(context); }
    }
}

public class TextureProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static object Create(Type type, Func<MethodInfo, object?[], object?> handler)
    { var proxy = DispatchProxy.Create(type, typeof(TextureProxy)); ((TextureProxy)proxy).Handler = handler; return proxy; }
}
