using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

internal static class LauncherAppearanceUi
{
    public static void Draw(Configuration config, Action save)
    {
        var changed = false;
        if (ImGui.BeginTable("launcher-appearance", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Visible results").X + 12);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            Row("Width");
            changed |= ImGui.SliderFloat("##launcher-width", ref config.LauncherWidth, 320, 1200, "%.0f px");
            UiText.Hint("Width before the game's UI scale. The launcher always stays inside the viewport.");
            Row("Height");
            changed |= ImGui.SliderFloat("##launcher-height", ref config.LauncherHeight, 24, 120, "%.0f px");
            UiText.Hint("Collapsed height. Reducing it removes space above and below the search text; large text keeps its minimum readable height.");
            Row("Text size");
            changed |= ImGui.SliderFloat("##launcher-text", ref config.LauncherTextScale, .75f, 1.5f, "%.2fx");
            Row("Visible results");
            changed |= ImGui.SliderInt("##launcher-results", ref config.LauncherResultCount, 3, 12);
            UiText.Hint("Limits the visible rows. Scroll or use the arrow keys to reach the remaining results.");
            Row("Horizontal");
            var horizontal = config.LauncherCenterX * 100;
            if (ImGui.SliderFloat("##launcher-x", ref horizontal, 5, 95, "%.0f%%"))
            { config.LauncherCenterX = horizontal / 100; changed = true; }
            Row("From top");
            var top = config.LauncherTop * 100;
            if (ImGui.SliderFloat("##launcher-top", ref top, 0, 90, "%.0f%%"))
            { config.LauncherTop = top / 100; changed = true; }
            UiText.Hint("Preferred position. Expanded results move upward when needed to stay on screen.");
            ImGui.EndTable();
        }
        if (UiText.Button("Reset launcher layout"))
        {
            var defaults = LauncherOptions.Default;
            config.LauncherWidth = defaults.Width;
            config.LauncherHeight = defaults.Height;
            config.LauncherTextScale = defaults.TextScale;
            config.LauncherResultCount = defaults.ResultCount;
            config.LauncherCenterX = defaults.CenterX;
            config.LauncherTop = defaults.Top;
            changed = true;
        }
        if (changed) save();
    }

    private static void Row(string label)
    {
        ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label); ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
    }
}
