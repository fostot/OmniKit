using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;

internal static class EditorBlinkChecks
{
    public static unsafe int Run(Assembly plugin, string assets)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var checks = 0; var gameCalls = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        Type Type(string name) => plugin.GetType("OmniKit." + name, true)!;
        var services = Type("Services").GetProperties(statics).Where(p => p.PropertyType.IsInterface && p.SetMethod != null).ToArray();
        var previous = services.ToDictionary(p => p, p => p.GetValue(null));
        var configType = Type("Configuration"); var editorType = Type("Ui.CompanionAccessoryEditor");
        var roamingType = Type("Ui.RoamingCompanion"); var previewTime = editorType.GetField("previewTime", instance)!;
        var amount = roamingType.GetMethod("BlinkAmount", statics)!;
        var peak = Enumerable.Range(0, 600).Select(n => n / 200f)
            .First(time => (float)amount.Invoke(null, [time, 3f, true])! >= .999f);
        var output = Path.GetFullPath(".local/visualharness/companion-accessories/editor-blink");
        Directory.CreateDirectory(output);
        try
        {
            var avatars = new[] { "Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism", "Nib", "Echo", "Fizz", "Glim", "Rune" };
            var choices = avatars.Select(avatar => (Avatar: avatar, Look: "Default")).Concat(new[] {
                (Avatar: "Nova", Look: "Look1"), (Avatar: "Moonblob", Look: "CostumeAllSaintsWake"),
                (Avatar: "Orbit", Look: "CostumeStarlight"), (Avatar: "Echo", Look: "CostumeMoonfireFaire") });
            foreach (var choice in choices)
            {
                var context = ImGui.CreateContext();
                try
                {
                    var io = ImGui.GetIO(); io.DisplaySize = new(640, 520); io.DeltaTime = 1f / 60;
                    io.IniFilename = null; io.LogFilename = null; io.Fonts.AddFontDefault(); io.Fonts.Build();
                    io.AddMousePosEvent(-100, -100);
                    var painter = new SoftwareRenderer(); var textureIds = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
                    var requests = new List<string>();
                    object Wrap(Type type, ulong id, int width, int height) => TextureProxy.Create(type, (method, _) => method.Name switch
                    {
                        "get_Handle" => new ImTextureID(id), "get_Width" => width, "get_Height" => height,
                        "GetWrapOrDefault" => Wrap(method.ReturnType, id, width, height),
                        _ => throw new InvalidOperationException("Unexpected synthetic texture call: " + method.Name),
                    });
                    foreach (var service in services)
                        service.SetValue(null, TextureProxy.Create(service.PropertyType, (method, arguments) =>
                        {
                            if (service.Name != "Textures" || method.Name != "GetFromFile")
                            { gameCalls++; throw new InvalidOperationException("Unexpected game service call: " + method.Name); }
                            var path = Path.GetFullPath(arguments[0]!.ToString()!);
                            if (!path.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("Only public project artwork may be read.");
                            requests.Add(path);
                            using var bitmap = new Bitmap(path);
                            if (!textureIds.TryGetValue(path, out var id))
                            {
                                id = (ulong)textureIds.Count + 2; textureIds.Add(path, id);
                                var pixels = new byte[bitmap.Width * bitmap.Height * 4];
                                var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                                try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
                                finally { bitmap.UnlockBits(bits); }
                                for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                                painter.AddTexture(id, pixels, bitmap.Width, bitmap.Height);
                            }
                            return Wrap(method.ReturnType, id, bitmap.Width, bitmap.Height);
                        }));
                    var config = Activator.CreateInstance(configType)!;
                    void Enabled(bool blink, bool animation)
                    {
                        configType.GetField("CompanionBlinkEnabled")!.SetValue(config, blink);
                        configType.GetField("AnimationsEnabled")!.SetValue(config, animation);
                    }
                    configType.GetField("CompanionFourierBlinkEnabled")!.SetValue(config, false);
                    var editor = Activator.CreateInstance(editorType, [config, assets])!;
                    var open = editorType.GetMethod("Open")!; var draw = editorType.GetMethod("Draw")!;
                    var theme = Enum.Parse(Type("Ui.SeasonalCompanionTheme"), "Valentione");
                    void Open() => open.Invoke(editor, [choice.Avatar, choice.Look, theme]);
                    Open();
                    Check((float)previewTime.GetValue(editor)! == 0, choice + ": opening resets the isolated blink clock.");
                    Snapshot Frame(float time, bool blink, bool animation, string? image = null)
                    {
                        Enabled(blink, animation); previewTime.SetValue(editor, time); requests.Clear();
                        ImGui.NewFrame(); draw.Invoke(editor, [1f]);
                        var window = ImGuiP.FindWindowByName("Accessory fit###OmniKit.AccessoryFit");
                        var vertices = new List<Vertex>();
                        var textureSet = textureIds.Where(p => Path.GetFileName(p.Key) != "seasonal-accessories.png").Select(p => p.Value).ToHashSet();
                        var seen = new HashSet<int>(); var list = window.DrawList;
                        for (var commandIndex = 0; commandIndex < list.CmdBuffer.Size; commandIndex++)
                        {
                            var command = list.CmdBuffer[commandIndex]; if (!textureSet.Contains(command.TextureId.Handle)) continue;
                            for (var index = 0; index < command.ElemCount; index++)
                            {
                                var vertexIndex = (int)(list.IdxBuffer[(int)command.IdxOffset + index] + command.VtxOffset);
                                if (!seen.Add(vertexIndex)) continue;
                                var vertex = list.VtxBuffer[vertexIndex]; vertices.Add(new(vertex.Pos, vertex.Uv, vertex.Col));
                            }
                        }
                        ImGui.Render();
                        if (image != null) painter.Save(ImGui.GetDrawData(), Path.Combine(output, choice.Avatar + "-" + choice.Look + "-" + image + ".png"));
                        return new(vertices.ToArray(), requests.Count, (float)previewTime.GetValue(editor)!);
                    }
                    for (var warmup = 0; warmup < 3; warmup++) Frame(0, true, true);
                    var neutral = Frame(0, true, true, "open");
                    var closed = Frame(peak - io.DeltaTime, true, true, "closed");
                    Check(neutral.Vertices.Length == 4 && closed.Vertices.Length > 4,
                        choice + ": the actual editor adds eyelid geometry to one unchanged normal body quad.");
                    Check(closed.Vertices.Take(4).SequenceEqual(neutral.Vertices), choice + ": blinking preserves body positions, UVs and tint.");
                    Check(closed.TextureCalls > neutral.TextureCalls, choice + ": enabled closure requests the measured closed-frame eyelid texture.");
                    Check(closed.Time >= peak - .0001f && closed.Time <= peak + .0001f, choice + ": preview clock advances by the render delta.");
                    foreach (var mode in new[] { (Blink: false, Animate: true), (Blink: true, Animate: false) })
                    {
                        var disabled = Frame(peak, mode.Blink, mode.Animate);
                        Check(disabled.Vertices.SequenceEqual(neutral.Vertices) && disabled.TextureCalls == neutral.TextureCalls,
                            choice + ": disabled blink or animation paints no lids and makes no eyelid texture request.");
                        Check(disabled.Time == peak, choice + ": disabled preview clock remains still.");
                    }
                    Open(); Check((float)previewTime.GetValue(editor)! == 0, choice + ": reopening clears accumulated preview time.");
                }
                finally { ImGui.DestroyContext(context); }
            }
            // The live companion remains suspended even while the isolated fitting preview blinks.
            var roamer = RuntimeHelpers.GetUninitializedObject(roamingType);
            roamingType.GetProperty("IsEditingAccessories")!.SetValue(roamer, true);
            var lastTick = roamingType.GetField("lastTick", instance)!;
            lastTick.SetValue(roamer, Convert.ChangeType(123, lastTick.FieldType));
            var clocks = new[] { "animationTime", "poseTime" }.Select(name => roamingType.GetField(name, instance)!).ToArray();
            foreach (var field in clocks) field.SetValue(roamer, Convert.ChangeType(17, field.FieldType));
            roamingType.GetMethod("Update")!.Invoke(roamer, null);
            roamingType.GetMethod("Draw")!.Invoke(roamer, [1f]);
            Check(clocks.All(field => Convert.ToDouble(field.GetValue(roamer)) == 17) && Convert.ToDouble(lastTick.GetValue(roamer)) == 0,
                "The live companion guard never touches uninitialized motion/physics/nap state and resets its resume tick.");
            Check(gameCalls == 0, "The complete preview proof invokes no game service.");
        }
        finally { foreach (var pair in previous) pair.Key.SetValue(null, pair.Value); }
        return checks;
    }

    private readonly record struct Vertex(Vector2 Position, Vector2 Uv, uint Color);
    private sealed record Snapshot(Vertex[] Vertices, int TextureCalls, float Time);
}
