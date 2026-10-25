using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;

internal static class LayerOrderingChecks
{
    public static unsafe int Run(Assembly plugin, string assets)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
        Type Type(string name) => plugin.GetType("OmniKit." + name, true)!;
        var rendererType = Type("Ui.SeasonalCompanionRenderer");
        var renderer = Activator.CreateInstance(rendererType, [assets])!;
        var roamingType = Type("Ui.RoamingCompanion"); var roamer = RuntimeHelpers.GetUninitializedObject(roamingType);
        roamingType.GetField("seasonalArtwork", instance)!.SetValue(roamer, renderer);
        var layers = roamingType.GetMethod("DrawAccessoryLayers", instance)!;
        var bodyType = Type("Ui.SeasonalBodyTransform"); var fitType = Type("Ui.SeasonalAccessoryFit");
        var getQuad = rendererType.GetMethods().Single(method => method.Name == "TryGetQuad" &&
            method.GetParameters().Length == 4 && method.GetParameters()[2].ParameterType == fitType);
        var placement = Type("Ui.CompanionAccessoryPlacement").GetMethod("Apply")!;
        var theme = Enum.Parse(Type("Ui.SeasonalCompanionTheme"), "AllSaintsWake");
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.DisplaySize = new Vector2(600, 500); io.DeltaTime = 1f / 60;
            io.IniFilename = null; io.LogFilename = null; io.Fonts.AddFontDefault(); io.Fonts.Build();
            foreach (var mirrored in new[] { false, true })
            foreach (var angle in new[] { -45f, 45f })
            {
                var fit = Activator.CreateInstance(fitType, [new Vector2(150, 110), 90f])!;
                var a = new Vector2(mirrored ? 350 : 150, 150); var b = new Vector2(mirrored ? 150 : 350, 150);
                var body = Activator.CreateInstance(bodyType, [new Vector4(50, 50, 250, 250), 250f, a, b, a + new Vector2(0, 200)])!;
                object?[] arguments = [theme, body, fit, null];
                Check((bool)getQuad.Invoke(renderer, arguments)!, "The real All Saints' Wake accessory supports the synthetic body transform.");
                var adjustment = Activator.CreateInstance(Type("CompanionAccessoryAdjustment"),
                    ["Wink", "Default", "AllSaintsWake", .2f, -.1f, 1.2f, angle])!;
                var quad = placement.Invoke(null, [arguments[3], body, fit, adjustment])!;
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); draw.ChannelsSplit(2);
                draw.ChannelsSetCurrent(1);
                draw.AddImage(new ImTextureID(2UL), new Vector2(170, 210), new Vector2(330, 340));
                var result = layers.Invoke(roamer, [draw, new ImTextureID(3UL), quad, theme, 0xFFFFFFFFu]);
                draw.AddImage(new ImTextureID(4UL), new Vector2(230, 330), new Vector2(270, 350));
                draw.ChannelsMerge();
                var commands = new List<ulong>(); var bodyIndices = new List<int>();
                for (var c = 0; c < draw.CmdBuffer.Size; c++)
                {
                    var command = draw.CmdBuffer[c]; if (command.ElemCount == 0) continue;
                    commands.Add(command.TextureId.Handle);
                    if (command.TextureId.Handle == 2)
                        for (var index = 0; index < command.ElemCount; index++)
                            bodyIndices.Add(draw.IdxBuffer[(int)command.IdxOffset + index] + (int)command.VtxOffset);
                }
                Check(commands.SequenceEqual(new ulong[] { 3, 2, 3, 4 }),
                    "After merging, the runtime helper orders rear hat, existing body, front hat, then later foreground paint.");
                Check(bodyIndices.Count == 6 && bodyIndices.Distinct().Count() == 4,
                    "Channel reordering retains exactly the original body quad, with no duplicated body draw.");
                Check(Equals(result, quad.GetType().GetProperty("Bounds")!.GetValue(quad)),
                    "The runtime layer helper returns the complete rotated accessory bounds for hit and layout calculations.");
                ImGui.Render();
            }
        }
        finally { ImGui.DestroyContext(context); }
        return checks;
    }
}
