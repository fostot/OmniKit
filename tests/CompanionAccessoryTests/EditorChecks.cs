using System.Collections;
using System.Drawing.Imaging;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;

internal static class EditorChecks
{
    public static unsafe int Run(Assembly plugin, string assets)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        Type Type(string name) => plugin.GetType("OmniKit." + name, true)!;
        var editorType = Type("Ui.CompanionAccessoryEditor");
        var configType = Type("Configuration");
        var textureProperty = Type("Services").GetProperty("Textures", statics)!;
        var measuredProperty = Type("Ui.UiText").GetProperty("ItemMeasured", statics)!;
        foreach (var scale in new[] { 1f, 1.5f, 2f })
        foreach (var choice in new[] { (Avatar: "Wink", Look: "Default"), (Avatar: "Orbit", Look: "Look2"), (Avatar: "Ember", Look: "HolidayStarlight") })
        {
            var context = ImGui.CreateContext();
            try
            {
                var label = choice.Avatar + "-" + choice.Look + "-" + scale.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                var config = Activator.CreateInstance(configType)!;
                configType.GetField("CompanionBlinkEnabled")!.SetValue(config, false);
                var saves = 0; var failSave = false;
                Action save = () => { if (failSave) throw new InvalidOperationException("Synthetic write failure"); saves++; };
                var persist = Expression.Lambda(typeof(Action<>).MakeGenericType(configType),
                    Expression.Call(Expression.Constant(save), typeof(Action).GetMethod("Invoke")!), Expression.Parameter(configType)).Compile();
                configType.GetField("Persist", instance)!.SetValue(config, persist);
                IList Fits() => (IList)configType.GetField("CompanionAccessoryFits")!.GetValue(config)!;
                var editor = Activator.CreateInstance(editorType, [config, assets])!;
                var open = editorType.GetMethod("Open")!; var drawMethod = editorType.GetMethod("Draw")!;
                var theme = Enum.Parse(Type("Ui.SeasonalCompanionTheme"), "Valentione");
                void Open() => open.Invoke(editor, [choice.Avatar, choice.Look, theme]);
                bool State(string name) => (bool)editorType.GetProperty(name)!.GetValue(editor)!;
                Open();
                var io = ImGui.GetIO(); io.DisplaySize = new Vector2(480, 330) * scale;
                io.DeltaTime = 1f / 60; io.FontGlobalScale = scale; io.IniFilename = null; io.LogFilename = null;
                io.Fonts.AddFontDefault(); io.Fonts.Build(); ImGui.StyleColorsDark(); ImGui.GetStyle().ScaleAllSizes(scale);
                var renderer = new SoftwareRenderer(); var textureIds = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
                object Wrap(Type type, ulong id, int width, int height) => TextureProxy.Create(type, (method, _) => method.Name switch
                {
                    "get_Handle" => new ImTextureID(id), "get_Width" => width, "get_Height" => height,
                    "GetWrapOrDefault" => Wrap(method.ReturnType, id, width, height),
                    _ => throw new InvalidOperationException("Unexpected synthetic texture call: " + method.Name),
                });
                textureProperty.SetValue(null, TextureProxy.Create(textureProperty.PropertyType, (method, arguments) =>
                {
                    if (method.Name != "GetFromFile") throw new InvalidOperationException("Unexpected texture-provider method.");
                    var path = Path.GetFullPath(arguments[0]!.ToString()!);
                    if (!path.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Only public project artwork may be opened by this fixture.");
                    using var bitmap = new Bitmap(path);
                    if (!textureIds.TryGetValue(path, out var id))
                    {
                        id = (ulong)textureIds.Count + 2; textureIds.Add(path, id);
                        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        try
                        {
                            var pixels = new byte[bitmap.Width * bitmap.Height * 4]; var row = new byte[bitmap.Width * 4];
                            for (var y = 0; y < bitmap.Height; y++)
                            {
                                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                                for (var x = 0; x < bitmap.Width; x++)
                                { var target = (y * bitmap.Width + x) * 4; var source = x * 4;
                                    pixels[target] = row[source + 2]; pixels[target + 1] = row[source + 1];
                                    pixels[target + 2] = row[source]; pixels[target + 3] = row[source + 3]; }
                            }
                            renderer.AddTexture(id, pixels, bitmap.Width, bitmap.Height);
                        }
                        finally { bitmap.UnlockBits(data); }
                    }
                    return Wrap(method.ReturnType, id, bitmap.Width, bitmap.Height);
                }));
                var measured = new Dictionary<string, (Vector2 Min, Vector2 Max, bool Visible)>();
                measuredProperty.SetValue(null, (Action<string, Vector2, Vector2, bool>)((name, min, max, visible) => measured[name] = (min, max, visible)));
                void Frame(Vector2? mouse = null, bool? down = null)
                {
                    if (mouse.HasValue) io.AddMousePosEvent(mouse.Value.X, mouse.Value.Y);
                    if (down.HasValue) io.AddMouseButtonEvent(0, down.Value);
                    measured.Clear(); ImGui.NewFrame(); drawMethod.Invoke(editor, [scale]); ImGui.Render();
                }
                for (var frame = 0; frame < 4; frame++) Frame(new(-100, -100), false);
                ImGuiWindowPtr Window() => ImGuiP.FindWindowByName("Accessory fit###OmniKit.AccessoryFit");
                Vector2[] ImageVertices(bool accessory)
                {
                    var window = Window(); var vertices = new List<Vector2>();
                    foreach (var pair in textureIds)
                    {
                        if (Path.GetFileName(pair.Key).Equals("seasonal-accessories.png", StringComparison.Ordinal) != accessory) continue;
                        var indices = new HashSet<int>(); var draw = window.DrawList;
                        for (var c = 0; c < draw.CmdBuffer.Size; c++)
                        {
                            var command = draw.CmdBuffer[c]; if (command.TextureId.Handle != pair.Value) continue;
                            for (var index = 0; index < command.ElemCount; index++)
                            {
                                var vertex = (int)(draw.IdxBuffer[(int)command.IdxOffset + index] + command.VtxOffset);
                                if (!indices.Add(vertex)) continue;
                                var point = draw.VtxBuffer[vertex].Pos; vertices.Add(point);
                                Check(point.X >= command.ClipRect.X - .01f && point.X <= command.ClipRect.Z + .01f &&
                                    point.Y >= command.ClipRect.Y - .01f && point.Y <= command.ClipRect.W + .01f,
                                    label + ": complete painted quads fit inside the stage clip rectangle.");
                            }
                        }
                    }
                    return vertices.ToArray();
                }
                void Bounds()
                {
                    var window = Window();
                    Check(window.Active && !window.Hidden && window.ScrollMax == Vector2.Zero, label + ": the fitting window is fully visible without scrolling.");
                    Check(window.Pos.X >= 12 * scale - .01f && window.Pos.Y >= 12 * scale - .01f &&
                        window.Pos.X + window.Size.X <= io.DisplaySize.X - 12 * scale + .01f &&
                        window.Pos.Y + window.Size.Y <= io.DisplaySize.Y - 12 * scale + .01f, label + ": viewport gutters remain clear.");
                    foreach (var name in new[] { "##fit-avatar", "##fit-body", "##fit-accessory", "##fit-size", "##fit-angle", "Save fits", "Reset fit", "Fit view", "Cancel" })
                    {
                        Check(measured.TryGetValue(name, out var item) && item.Visible && item.Max.Y - item.Min.Y >= ImGui.GetFrameHeight() - .01f,
                            label + ": complete fitting control is visible: " + name);
                        Check(item.Min.X >= window.Pos.X + ImGui.GetStyle().WindowPadding.X - .01f &&
                            item.Max.X <= window.Pos.X + window.Size.X - ImGui.GetStyle().WindowPadding.X + .01f,
                            label + ": fitting controls retain horizontal window gutters: " + name);
                    }
                    var sizeControl = measured["##fit-size"]; var angleControl = measured["##fit-angle"];
                    Check(Math.Abs(sizeControl.Min.Y - angleControl.Min.Y) < .01f &&
                        angleControl.Min.X - sizeControl.Max.X >= ImGui.GetStyle().ItemSpacing.X - .01f,
                        label + ": the size and angle controls share one row with a visible gutter.");
                    // Native cursor positioning rounds fractional font heights to pixels.
                    Check(angleControl.Max.Y + ImGui.GetStyle().ItemSpacing.Y <= measured["Save fits"].Min.Y + 1f,
                        $"{label}: angle hit targets stay above the separate save-button row (angle bottom {angleControl.Max.Y}, save top {measured["Save fits"].Min.Y}, spacing {ImGui.GetStyle().ItemSpacing.Y}).");
                    var body = ImageVertices(false); var hat = ImageVertices(true);
                    Check(body.Length == 4 && hat.Length >= 4 && hat.Length % 4 == 0,
                        label + ": the stage draws one neutral body plus the accessory's complementary layer mesh.");
                    Check(body.All(point => point.X > window.Pos.X && point.X < window.Pos.X + window.Size.X &&
                        point.Y > window.Pos.Y && point.Y < window.Pos.Y + window.Size.Y), label + ": neutral rest pose stays inside its centered stage.");
                }
                Bounds();
                var stationary = ImageVertices(false);
                for (var frame = 0; frame < 8; frame++) Frame();
                Check(stationary.SequenceEqual(ImageVertices(false)), label + ": the neutral fitting body remains stationary as time advances.");
                renderer.Save(ImGui.GetDrawData(), Path.GetFullPath(".local/visualharness/companion-accessories/editor-" + label + ".png"));
                if (choice.Avatar != "Wink") continue;
                void Click(string name)
                {
                    var item = measured[name]; var point = (item.Min + item.Max) * .5f;
                    Frame(point, false); Frame(point, true); Frame(point, false); Frame(new(-100, -100));
                }
                object Current() => editorType.GetProperty("Current", instance)!.GetValue(editor)!;
                float FitValue(object value, string name) => (float)value.GetType().GetProperty(name)!.GetValue(value)!;
                void Slider(string name, float fraction)
                {
                    var item = measured[name]; var point = new Vector2(item.Min.X + (item.Max.X - item.Min.X) * fraction,
                        (item.Min.Y + item.Max.Y) * .5f);
                    Frame(point, false); Frame(point, true); Frame(point, false); Frame(new(-100, -100));
                }
                foreach (var selector in new[] { "##fit-avatar", "##fit-body", "##fit-accessory" })
                {
                    Click(selector); Frame();
                    var windows = ImGui.GetCurrentContext().Windows; var popupCount = 0;
                    for (var index = 0; index < windows.Size; index++)
                    {
                        var popup = windows[index];
                        if (!popup.Active || popup.Hidden || (popup.Flags & ImGuiWindowFlags.Popup) == 0) continue;
                        popupCount++;
                        Check(popup.Pos.X >= 0 && popup.Pos.Y >= 0 && popup.Pos.X + popup.Size.X <= io.DisplaySize.X + .01f &&
                            popup.Pos.Y + popup.Size.Y <= io.DisplaySize.Y + .01f,
                            label + ": disclosed selector menu stays within the viewport.");
                    }
                    Check(popupCount == 1, label + ": the actual selector opens one readable menu.");
                    renderer.Save(ImGui.GetDrawData(), Path.GetFullPath(".local/visualharness/companion-accessories/editor-" + label + "-" + selector[2..] + ".png"));
                    Frame(new(2, 2), false); Frame(new(2, 2), true); Frame(new(2, 2), false); Frame();
                    Check(State("IsOpen") && Fits().Count == 0 && saves == 0, label + ": dismissing a menu leaves the editor and saved preferences intact.");
                }
                var hatVertices = ImageVertices(true); var grab = hatVertices.Aggregate(Vector2.Zero, (sum, point) => sum + point) / hatVertices.Length;
                var bodyBeforeDrag = ImageVertices(false);
                Frame(grab, false); Frame(grab, true); Frame(grab + new Vector2(9, -4) * scale, true);
                Check(bodyBeforeDrag.SequenceEqual(ImageVertices(false)), label + ": dragging a fit never changes body scale or position.");
                Frame(grab + new Vector2(9, -4) * scale, false);
                Check(bodyBeforeDrag.SequenceEqual(ImageVertices(false)), label + ": releasing a fit never changes body scale or position.");
                Check(State("IsDirty") && Fits().Count == 0 && saves == 0, label + ": native dragging changes the draft only.");
                var beforeAngleBody = ImageVertices(false); var beforeAngleHat = ImageVertices(true);
                var beforeAngleFit = Current();
                Slider("##fit-angle", .64f);
                var changedAngle = FitValue(Current(), "RotationDegrees");
                Check(changedAngle is > 25 and < 80 && !beforeAngleHat.SequenceEqual(ImageVertices(true)),
                    label + ": the native Angle slider rotates the painted accessory.");
                Check(beforeAngleBody.SequenceEqual(ImageVertices(false)) &&
                    new[] { "OffsetX", "OffsetY", "Scale" }.All(name => FitValue(beforeAngleFit, name) == FitValue(Current(), name)),
                    label + ": changing angle keeps the body, measured crown offset and uniform scale unchanged.");
                Check(State("IsDirty") && Fits().Count == 0 && saves == 0,
                    label + ": angle changes remain draft-only until the explicit save.");
                Bounds();
                renderer.Save(ImGui.GetDrawData(), Path.GetFullPath(".local/visualharness/companion-accessories/editor-rotated-" + label + ".png"));
                Click("Save fits");
                Check(!State("IsDirty") && Fits().Count == 1 && saves == 1, label + ": Save writes the fitted preference exactly once.");
                var savedFits = Fits(); var savedFit = savedFits[0];
                Check(FitValue(savedFit!, "RotationDegrees") == changedAngle, label + ": Save retains the native slider's exact angle.");
                Click("Cancel"); Open(); Frame(); Frame();
                Check(FitValue(Current(), "RotationDegrees") == changedAngle && !State("IsDirty") && saves == 1,
                    label + ": reopening loads the persisted rotation without another write.");
                var fitted = ImageVertices(true); var corner = fitted.Aggregate(Vector2.Max);
                var beforeResize = ImageVertices(false);
                var oldScale = (float)savedFit!.GetType().GetProperty("Scale")!.GetValue(savedFit)!;
                Frame(corner, false); Frame(corner, true); Frame(corner + new Vector2(0, 10 * scale), true);
                Frame(corner + new Vector2(0, 10 * scale), false); Frame(new(-100, -100));
                var currentFit = Current();
                Check((float)currentFit.GetType().GetProperty("Scale")!.GetValue(currentFit)! > oldScale,
                    label + ": a vertical corner drag changes uniform scale through the two-axis resize projection.");
                Check(beforeResize.SequenceEqual(ImageVertices(false)) && saves == 1 && ReferenceEquals(savedFits, Fits()),
                    label + ": resizing preserves body geometry and remains an unsaved draft.");
                Check(FitValue(currentFit, "RotationDegrees") == changedAngle, label + ": resizing a rotated accessory keeps its saved angle.");
                Slider("##fit-angle", .001f);
                Check(FitValue(Current(), "RotationDegrees") == -180, label + ": the minimum slider position reaches -180 degrees.");
                Bounds();
                Slider("##fit-angle", .999f);
                Check(FitValue(Current(), "RotationDegrees") == 180, label + ": the maximum slider position reaches 180 degrees.");
                Bounds();
                Click("Reset fit");
                Check(FitValue(Current(), "RotationDegrees") == 0, label + ": Reset returns the draft rotation to zero.");
                Check(State("IsDirty") && ReferenceEquals(savedFits, Fits()) && Equals(savedFit, Fits()[0]), label + ": resetting remains an unsaved draft.");
                Click("Cancel");
                Check(!State("IsOpen") && saves == 1 && ReferenceEquals(savedFits, Fits()), label + ": Cancel preserves the last saved fit.");
                Open(); Frame(); Frame(); Click("Reset fit"); failSave = true;
                Click("Save fits");
                Check(State("IsDirty") && saves == 1 && ReferenceEquals(savedFits, Fits()), label + ": a rejected persistence callback restores the prior list.");
                Bounds();
            }
            finally
            {
                measuredProperty.SetValue(null, null); textureProperty.SetValue(null, null); ImGui.DestroyContext(context);
            }
        }
        return checks;
    }
}

public class TextureProxy : DispatchProxy
{
    private Func<MethodInfo, object?[], object?> handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => handler(method!, args ?? []);
    public static object Create(Type type, Func<MethodInfo, object?[], object?> handler)
    {
        var proxy = Create(type, typeof(TextureProxy)); ((TextureProxy)proxy).handler = handler; return proxy;
    }
}
