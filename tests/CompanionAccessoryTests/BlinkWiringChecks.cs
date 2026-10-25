using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;

internal static class BlinkWiringChecks
{
    public static unsafe int Run(Assembly plugin, string assets)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var checks = 0; var nativeCalls = 0; var textureCalls = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        Type Type(string name) => plugin.GetType("OmniKit." + name, true)!;
        object? Property(object value, string name) => value.GetType().GetProperty(name, instance)!.GetValue(value);
        void Set(object value, string name, object? field) => value.GetType().GetField(name, instance)!.SetValue(value, field);
        var padding = Type("Ui.CompanionAccessoryPlacement").GetMethod("Padding")!;
        var adjustmentType = Type("CompanionAccessoryAdjustment");
        float Padding(float x, float y, float scale = 1) => (float)padding.Invoke(null,
            [Activator.CreateInstance(adjustmentType, ["Moonblob", "Default", "Starlight", x, y, scale, 0f])])!;
        var basePadding = (float)Type("Ui.SeasonalCompanionRenderer").GetField("RequiredVisualPaddingFraction")!.GetRawConstantValue()!;
        Check(Padding(0, 0) == basePadding, "Default fits retain their existing padding.");
        foreach (var scale in new[] { 1f, 3f })
        {
            var reservedOffset = Padding(1, 1, scale) - basePadding * scale;
            foreach (var angle in new[] { 0f, MathF.PI / 4, MathF.PI / 2 })
            {
                var shifted = Vector2.Transform(Vector2.One, Matrix3x2.CreateRotation(angle));
                Check(reservedOffset + .000001f >= Math.Max(Math.Abs(shifted.X), Math.Abs(shifted.Y)),
                    "Diagonal head-relative offsets remain inside padding through diagonal and quarter-turn rolling poses.");
            }
        }
        var services = Type("Services").GetProperties(statics).Where(property => property.PropertyType.IsInterface && property.SetMethod != null).ToArray();
        var priorServices = services.ToDictionary(property => property, property => property.GetValue(null));
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.DisplaySize = new Vector2(480, 360); io.DeltaTime = 1f / 60;
            io.IniFilename = null; io.LogFilename = null; io.Fonts.AddFontDefault(); io.Fonts.Build();
            var variants = Activator.CreateInstance(Type("Ui.CompanionVariantLibrary"), [assets])!;
            var wardrobe = Activator.CreateInstance(Type("Ui.CompanionWardrobeLibrary"), [assets])!;
            var blinks = Activator.CreateInstance(Type("Ui.CompanionBlinkLibrary"), [assets])!;
            var profiles = ((IEnumerable)Property(blinks, "Profiles")!).Cast<object>()
                .Concat(((IEnumerable)Property(wardrobe, "BlinkProfiles")!).Cast<object>()).ToArray();
            var allowedFiles = profiles.SelectMany(profile => new[] { (string)Property(profile, "NormalFile")!, (string)Property(profile, "ClosedFile")! })
                .Select(file => Path.GetFullPath(Path.Combine(assets, file))).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Check(profiles.Length > 0, "Reviewed public blink profiles load from their hash-bound source artwork.");
            object Wrap(Type type) => TextureProxy.Create(type, (method, _) => method.Name switch
            {
                "get_Handle" => new ImTextureID(2UL), "get_Width" or "get_Height" => 2048,
                "GetWrapOrDefault" => Wrap(method.ReturnType),
                _ => throw new InvalidOperationException("Unexpected texture-wrap call: " + method.Name),
            });
            foreach (var property in services)
                property.SetValue(null, TextureProxy.Create(property.PropertyType, (method, arguments) =>
                {
                    if (property.Name == "Textures" && method.Name == "GetFromFile")
                    {
                        var path = Path.GetFullPath(arguments[0]!.ToString()!);
                        if (!allowedFiles.Contains(path)) throw new InvalidOperationException("Only reviewed public blink texture paths are allowed.");
                        textureCalls++; return Wrap(method.ReturnType);
                    }
                    nativeCalls++; throw new InvalidOperationException("Unexpected game service call: " + method.Name);
                }));
            var configType = Type("Configuration"); var config = Activator.CreateInstance(configType)!;
            var roamerType = Type("Ui.RoamingCompanion"); var roamer = RuntimeHelpers.GetUninitializedObject(roamerType);
            roamerType.GetFields(instance).Single(field => field.FieldType == configType).SetValue(roamer, config);
            Set(roamer, "assetDirectory", assets); Set(roamer, "variants", variants); Set(roamer, "blinks", blinks);
            Set(roamer, "wardrobe", wardrobe);
            var gait = Activator.CreateInstance(Type("Ui.CompanionGait"))!; Set(gait, "<Phase>k__BackingField", .31f); Set(roamer, "gait", gait);
            var motion = Activator.CreateInstance(Type("Ui.CompanionBodyMotion"))!; Set(roamer, "bodyMotion", motion);
            motion.GetType().GetMethod("Update")!.Invoke(motion, [.03f, true, true]);
            Set(roamer, "homeRoutine", Activator.CreateInstance(Type("Ui.CompanionHomeRoutine"), [41])!);
            Set(roamer, "orbitRings", Activator.CreateInstance(Type("Ui.OrbitRingRenderer"))!);
            var blink = roamerType.GetMethod("DrawBlink", instance)!;
            var drawLook = roamerType.GetMethod("TryDrawLook", instance)!;
            var bodyType = Type("Ui.SeasonalBodyTransform");
            var poseType = Type("Ui.CompanionPose");
            var warp = motion.GetType().GetMethod("WarpAttachment")!;
            var supports = motion.GetType().GetMethod("Supports")!;
            var avatars = new[] { "Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism", "Nib", "Echo", "Fizz", "Glim", "Rune" };
            foreach (var avatar in avatars)
            {
                var look = variants.GetType().GetMethod("Find")!.Invoke(variants, [avatar, "Look1"])
                    ?? wardrobe.GetType().GetMethod("FindDefault")!.Invoke(wardrobe, [avatar]);
                Check(look != null, "Each avatar has one representative public everyday look.");
                var normal = Property(look!, "Normal")!;
                var file = (string)Property(normal, "File")!; var bounds = (Vector4)Property(normal, "Bounds")!;
                var profile = blinks.GetType().GetMethod("Find")!.Invoke(blinks, [file, bounds])
                    ?? wardrobe.GetType().GetMethod("FindBlink")!.Invoke(wardrobe, [file, bounds])
                    ?? throw new InvalidOperationException("Runtime blink profile is missing.");
                Check((string)Property(profile, "Avatar")! == avatar, "Runtime lookup selects the exact avatar and normal crop.");
                var eyes = ((IEnumerable)Property(profile, "Eyes")!).Cast<object>().ToArray();
                var sheet = (Vector2)Property(profile, "ClosedSheetSize")!;
                Set(roamer, "displayAvatar", avatar); Set(roamer, "currentLook", look);
                foreach (var edge in new[] { 96f, 192f })
                foreach (var mirrored in new[] { false, true })
                {
                    var label = avatar + " " + edge + (mirrored ? " mirrored" : " forward");
                    var position = new Vector2(100, 80); var size = new Vector2(edge);
                    var frame = normal.GetType().GetMethod("GetFrame")!.Invoke(normal, [position, size, mirrored])!;
                    var body = bodyType.GetMethod("FromFrame")!.Invoke(null, [bounds, (float)Property(normal, "SourceScale")!, frame, mirrored])!;
                    var deform = (bool)supports.Invoke(null, [avatar])!;
                    Vector2 Map(Vector2 point)
                    {
                        var mapped = (Vector2)bodyType.GetMethod("Map")!.Invoke(body, [point])!;
                        return deform ? (Vector2)warp.Invoke(motion, [avatar, mapped, position, size, .31f, mirrored, true])! : mapped;
                    }
                    var envelopes = eyes.Select(eye =>
                    {
                        var ellipse = (Vector4)Property(eye, "Ellipse")!;
                        var corners = new[] { new Vector2(ellipse.X - ellipse.Z - 2, ellipse.Y - ellipse.W - 2),
                            new Vector2(ellipse.X + ellipse.Z + 2, ellipse.Y - ellipse.W - 2),
                            new Vector2(ellipse.X + ellipse.Z + 2, ellipse.Y + ellipse.W + 2),
                            new Vector2(ellipse.X - ellipse.Z - 2, ellipse.Y + ellipse.W + 2) }.Select(Map).ToArray();
                        return (Minimum: corners.Aggregate(Vector2.Min) - Vector2.One, Maximum: corners.Aggregate(Vector2.Max) + Vector2.One);
                    }).ToArray();
                    foreach (var mode in new[] { "enabled", "open", "blink-off", "animation-off", "editing" })
                    {
                        Set(config, "CompanionBlinkEnabled", mode != "blink-off"); Set(config, "AnimationsEnabled", mode != "animation-off");
                        roamerType.GetProperty("IsEditingAccessories")!.SetValue(roamer, mode == "editing");
                        var beforeTextures = textureCalls;
                        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                        blink.Invoke(roamer, [draw, file, body, mode == "open" ? 0f : 1f, 0xFFFFFFFFu, position, size, mirrored, deform]);
                        if (mode != "enabled")
                            Check(draw.VtxBuffer.Size == 0 && textureCalls == beforeTextures, label + ": disabled/open/editor blink emits no geometry or texture request.");
                        else
                        {
                            Check(draw.VtxBuffer.Size > 0 && textureCalls == beforeTextures + 1, label + ": enabled runtime blink resolves one closed texture and emits eyelids.");
                            var confined = true; var samples = true;
                            for (var vertex = 0; vertex < draw.VtxBuffer.Size; vertex++)
                            {
                                var point = draw.VtxBuffer[vertex].Pos;
                                confined &= float.IsFinite(point.X) && float.IsFinite(point.Y) && envelopes.Any(area =>
                                    point.X >= area.Minimum.X && point.X <= area.Maximum.X && point.Y >= area.Minimum.Y && point.Y <= area.Maximum.Y);
                            }
                            for (var commandIndex = 0; commandIndex < draw.CmdBuffer.Size; commandIndex++)
                            {
                                var command = draw.CmdBuffer[commandIndex]; if (command.TextureId.Handle != 2UL) continue;
                                for (var index = 0; index < command.ElemCount; index++)
                                {
                                    var uv = draw.VtxBuffer[(int)(draw.IdxBuffer[(int)command.IdxOffset + index] + command.VtxOffset)].Uv;
                                    samples &= eyes.Any(eye => { var sample = (Vector4)Property(eye, "Sample")!;
                                        return uv.X >= sample.X / sheet.X - .00001f && uv.X <= sample.Z / sheet.X + .00001f &&
                                            uv.Y >= sample.Y / sheet.Y - .00001f && uv.Y <= sample.W / sheet.Y + .00001f; });
                                }
                            }
                            Check(confined && samples, label + ": runtime emits only measured eye geometry and reviewed eyelid UVs, including optional body deformation.");
                        }
                        ImGui.Render();
                    }
                    Set(config, "CompanionBlinkEnabled", true); Set(config, "AnimationsEnabled", true);
                    roamerType.GetProperty("IsEditingAccessories")!.SetValue(roamer, false);
                    foreach (var pose in new[] { "Idle", "Walk" })
                    {
                        (Vector2 Position, Vector2 Uv, uint Color)[] Paint(float closure)
                        {
                            ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                            object?[] arguments = [draw, position, size, mirrored, Enum.Parse(poseType, pose), closure, null];
                            Check((bool)drawLook.Invoke(roamer, arguments)!, label + ": actual TryDrawLook draws the selected body.");
                            var vertices = new (Vector2, Vector2, uint)[draw.VtxBuffer.Size];
                            for (var index = 0; index < vertices.Length; index++)
                            { var vertex = draw.VtxBuffer[index]; vertices[index] = (vertex.Pos, vertex.Uv, vertex.Col); }
                            ImGui.Render(); return vertices;
                        }
                        var open = Paint(0); var closed = Paint(1);
                        Check(open.Length > 0 && closed.Length > open.Length && closed.Take(open.Length).SequenceEqual(open),
                            label + " " + pose + ": full closure adds eyes without replacing or moving any underlying body/ring vertices or UVs.");
                        if (deform)
                        {
                            var atlas = Property(look!, "NormalAtlas")!;
                            var floating = atlas.GetType().GetMethod("GetFloat")!.Invoke(atlas, [.31f, mirrored, position, size])!;
                            var actualBody = Activator.CreateInstance(bodyType, [bounds, (float)Property(normal, "SourceScale")!,
                                Property(floating, "A")!, Property(floating, "B")!, Property(floating, "D")!])!;
                            ImGui.NewFrame(); var expectedDraw = ImGui.GetForegroundDrawList();
                            blink.Invoke(roamer, [expectedDraw, file, actualBody, 1f, 0xFFFFFFFFu, position, size, mirrored, true]);
                            var expected = new (Vector2 Position, Vector2 Uv)[expectedDraw.VtxBuffer.Size];
                            for (var index = 0; index < expected.Length; index++)
                            { var vertex = expectedDraw.VtxBuffer[index]; expected[index] = (vertex.Pos, vertex.Uv); }
                            ImGui.Render();
                            Check(closed.Skip(open.Length).Select(vertex => (vertex.Position, vertex.Uv)).SequenceEqual(expected),
                                label + " " + pose + ": actual eyelids follow the same bounded body-warp callback as the displayed mesh.");
                        }
                    }
                }
            }
            Check(nativeCalls == 0, "The complete blink wiring proof never invokes a native/game service.");
        }
        finally
        {
            foreach (var property in priorServices) property.Key.SetValue(null, property.Value);
            ImGui.DestroyContext(context);
        }
        return checks;
    }
}
