using System.Numerics;
using Dalamud.Bindings.ImGui;
using OmniKit;
using OmniKit.Ui;

namespace LauncherUiSmoke;

internal static class LauncherAppearanceChecks
{
    internal static unsafe int Run(string output, List<string> failures)
    {
        var renders = 0;
        foreach (var scale in new[] { 1f, 1.5f, 2f })
        {
            var context = ImGui.CreateContext();
            try
            {
                var io = ImGui.GetIO(); io.DisplaySize = new Vector2(560, 410) * scale;
                io.DeltaTime = 1f / 60; io.IniFilename = null; io.LogFilename = null;
                io.FontGlobalScale = scale; io.Fonts.AddFontDefault(); io.Fonts.Build();
                ImGui.StyleColorsDark(); ImGui.GetStyle().ScaleAllSizes(scale);
                var renderer = new SoftwareRenderer();
                var config = new Configuration { LauncherWidth = 1000, LauncherTextScale = 1.25f,
                    LauncherResultCount = 10, LauncherCenterX = .8f, LauncherTop = .7f };
                var saves = 0;
                var resetMin = Vector2.Zero; var resetMax = Vector2.Zero;
                UiText.ItemMeasured = (label, min, max, visible) =>
                {
                    if (label == "Reset launcher layout" && visible) { resetMin = min; resetMax = max; }
                };
                for (var frame = 0; frame < 8; ++frame)
                {
                    if (frame == 4)
                    {
                        var center = (resetMin + resetMax) * .5f;
                        io.AddMousePosEvent(center.X, center.Y);
                    }
                    if (frame == 5) io.AddMouseButtonEvent(0, true);
                    if (frame == 6) io.AddMouseButtonEvent(0, false);
                    ImGui.NewFrame();
                    ImGui.SetNextWindowPos(new Vector2(40) * scale);
                    ImGui.SetNextWindowSize(new Vector2(480, 330) * scale);
                    ImGui.Begin("##LauncherAppearanceFixture", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings);
                    LauncherAppearanceUi.Draw(config, () => ++saves);
                    ImGui.End(); ImGui.Render();
                    if (frame is 3 or 7)
                    {
                        renderer.Save(ImGui.GetDrawData(), Path.Combine(output,
                            $"appearance-controls-{(frame == 3 ? "custom" : "reset")}-scale{scale:0.0}.png")); ++renders;
                        var window = ImGuiP.FindWindowByName("##LauncherAppearanceFixture");
                        if (window.ScrollMax.X != 0 || window.ScrollMax.Y != 0 || resetMin.X < window.Pos.X + 4 * scale
                            || resetMax.X > window.Pos.X + window.Size.X - 4 * scale || resetMax.Y > window.Pos.Y + window.Size.Y - 4 * scale)
                            failures.Add($"Appearance controls scale{scale}: controls escape the minimum panel or its gutter.");
                    }
                }
                if (saves != 1 || config.LauncherWidth != 640 || config.LauncherTextScale != 1
                    || config.LauncherResultCount != 7 || config.LauncherCenterX != .5f || config.LauncherTop != .2f)
                    failures.Add($"Appearance controls scale{scale}: Reset did not restore all five defaults and save exactly once.");
                foreach (var row in new[] { 0, 2, 4 })
                {
                    var style = ImGui.GetStyle();
                    var y = 40 * scale + style.WindowPadding.Y + style.CellPadding.Y + ImGui.GetFrameHeight() / 2
                        + row * (ImGui.GetFrameHeight() + style.CellPadding.Y * 2);
                    io.AddMousePosEvent(400 * scale, y);
                    for (var frame = 0; frame < 3; ++frame)
                    {
                        ImGui.NewFrame();
                        ImGui.SetNextWindowPos(new Vector2(40) * scale);
                        ImGui.SetNextWindowSize(new Vector2(480, 330) * scale);
                        ImGui.Begin("##LauncherAppearanceFixture", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings);
                        LauncherAppearanceUi.Draw(config, () => ++saves);
                        ImGui.End(); ImGui.Render();
                    }
                    var shown = false;
                    var windows = ImGui.GetCurrentContext().Windows;
                    for (var i = 0; i < windows.Size; ++i)
                    {
                        var tip = windows[i];
                        if (!tip.Active || tip.Hidden || (tip.Flags & ImGuiWindowFlags.Tooltip) == 0) continue;
                        shown = true;
                        if (tip.Pos.X < 2 || tip.Pos.Y < 2 || tip.Pos.X + tip.Size.X > io.DisplaySize.X - 2
                            || tip.Pos.Y + tip.Size.Y > io.DisplaySize.Y - 2)
                            failures.Add($"Appearance hint row{row} scale{scale}: tooltip leaves the viewport.");
                    }
                    if (!shown) failures.Add($"Appearance hint row{row} scale{scale}: no tooltip was exercised.");
                    renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"appearance-hint-{row}-scale{scale:0.0}.png")); ++renders;
                }
            }
            finally { UiText.ItemMeasured = null; ImGui.DestroyContext(context); }
        }
        return renders;
    }
}
