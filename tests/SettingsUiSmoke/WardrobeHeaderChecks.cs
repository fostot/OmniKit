using System.Drawing.Imaging;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;

internal static class WardrobeHeaderChecks
{
    internal static unsafe void Draw(Assembly plugin, object ui, SoftwareRenderer painter, float scale, string output)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var services = plugin.GetType("OmniKit.Services", true)!.GetProperties(flags)
            .Where(p => p.PropertyType.IsInterface && p.SetMethod != null).ToArray();
        var previous = services.ToDictionary(p => p, p => p.GetValue(null));
        var artPath = Path.GetFullPath("assets/nib-wardrobe.png");
        const ulong textureId = 200;
        var requests = 0;
        using var bitmap = new Bitmap(artPath);
        var bits = bitmap.LockBits(new(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
        finally { bitmap.UnlockBits(bits); }
        for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        painter.AddTexture(textureId, pixels, bitmap.Width, bitmap.Height);
        object Wrap(Type type) => WardrobeTextureProxy.Create(type, (method, _) => method.Name switch
        {
            "get_Handle" => new ImTextureID(textureId), "get_Width" => bitmap.Width, "get_Height" => bitmap.Height,
            "GetWrapOrDefault" => Wrap(method.ReturnType),
            _ => throw new InvalidOperationException("Unexpected texture wrapper operation: " + method.Name),
        });
        try
        {
            foreach (var property in services)
                property.SetValue(null, WardrobeTextureProxy.Create(property.PropertyType, (method, arguments) =>
                {
                    if (property.Name != "Textures" || method.Name != "GetFromFile"
                        || !Path.GetFullPath(arguments[0]!.ToString()!).Equals(artPath, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Header proof permits only the public Nib portrait texture.");
                    requests++; return Wrap(method.ReturnType);
                }));
            ImGui.GetIO().AddMousePosEvent(-100, -100);
            ImGui.NewFrame(); ImGui.SetNextWindowPos(new Vector2(40, 40) * scale);
            ImGui.SetNextWindowSize(new Vector2(480, 330) * scale);
            ImGui.Begin("Nib portrait proof", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoInputs);
            var contentMinimum = ImGui.GetWindowPos() + ImGui.GetStyle().WindowPadding;
            var contentMaximum = ImGui.GetWindowPos() + ImGui.GetWindowSize() - ImGui.GetStyle().WindowPadding;
            var list = ImGui.GetWindowDrawList();
            ui.GetType().GetMethod("Header", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(ui, ["Public wardrobe portrait"]);
            var vertices = new HashSet<int>();
            for (var commandIndex = 0; commandIndex < list.CmdBuffer.Size; commandIndex++)
            {
                var command = list.CmdBuffer[commandIndex]; if (command.TextureId.Handle != textureId) continue;
                for (var index = 0; index < command.ElemCount; index++)
                {
                    var vertexIndex = (int)(list.IdxBuffer[(int)command.IdxOffset + index] + command.VtxOffset);
                    vertices.Add(vertexIndex); var p = list.VtxBuffer[vertexIndex].Pos;
                    if (p.X < contentMinimum.X || p.Y < contentMinimum.Y || p.X > contentMaximum.X || p.Y > contentMaximum.Y)
                        throw new InvalidOperationException("Nib's real portrait escaped the actual header content gutter.");
                }
            }
            ImGui.End(); ImGui.Render();
            if (requests != 1 || vertices.Count != 4) throw new InvalidOperationException("Header did not draw one real Nib portrait quad.");
            painter.Save(ImGui.GetDrawData(), Path.Combine(output, $"wardrobe-nib-header-scale{scale:0.0}.png"));
        }
        finally { foreach (var entry in previous) entry.Key.SetValue(null, entry.Value); }
    }
}

public class WardrobeTextureProxy : DispatchProxy
{
    private Func<MethodInfo, object?[], object?> handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => handler(method!, args ?? []);
    internal static object Create(Type type, Func<MethodInfo, object?[], object?> handler)
    {
        var proxy = Create(type, typeof(WardrobeTextureProxy)); ((WardrobeTextureProxy)proxy).handler = handler; return proxy;
    }
}
