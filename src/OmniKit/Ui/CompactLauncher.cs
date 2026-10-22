using System.Numerics;
using Dalamud.Bindings.ImGui;
using OmniKit.Search;

namespace OmniKit.Ui;

/// <summary>The launcher has no idle content beyond its search bar and settings button.</summary>
internal sealed class CompactLauncher
{
    private bool focus;
    private bool resetScroll;
    private int selected;
    public bool SearchHasFocus { get; private set; }
    public void Focus() { focus = true; resetScroll = true; selected = 0; }

    public void Draw(ref bool open, ref string query, IReadOnlyList<SearchEntry> entries,
        float scale, Action<SearchEntry> activate, Action settings, Action<string> companion,
        Action<string> wiki, string status = "")
    {
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var rows = hasQuery ? GetResults(entries, query, companion, wiki) : [];
        var viewport = ImGui.GetMainViewport();
        var width = Math.Min(680 * scale, viewport.WorkSize.X - 24 * scale);
        var padding = 12 * scale;
        var barHeight = ImGui.GetTextLineHeight() + 24 * scale;
        var rowHeight = ImGui.GetTextLineHeight() * 2 + 14 * scale;
        var top = Math.Min(viewport.WorkSize.Y * .23f, 120 * scale);
        var availableHeight = Math.Max(barHeight + padding * 2, viewport.WorkSize.Y - top - 16 * scale);
        var statusHeight = hasQuery && status.Length > 0 ? ImGui.GetTextLineHeightWithSpacing() + 4 * scale : 0;
        var resultsHeight = hasQuery ? Math.Min(Math.Min(rows.Count, 7) * rowHeight, Math.Max(1, availableHeight - barHeight - padding * 3 - statusHeight)) : 0;
        var height = padding * 2 + barHeight + (hasQuery ? resultsHeight + 12 * scale + statusHeight : 0);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
        ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(viewport.WorkSize.X / 2, top),
            ImGuiCond.Always, new Vector2(.5f, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(padding));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 12) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 2 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.085f, .092f, .11f, .99f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.30f, .32f, .37f, .8f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        try
        {
            if (!ImGui.Begin("###OmniKit.Launcher", ref open, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar))
            { ImGui.End(); return; }
            try
            {
                if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.IsKeyPressed(ImGuiKey.Escape))
                { open = false; ImGui.SetWindowFocus((string)null!); return; }
                var origin = ImGui.GetCursorScreenPos();
                var draw = ImGui.GetWindowDrawList();
                var iconWidth = 36 * scale;
                var searchCenter = origin + new Vector2(15 * scale, barHeight / 2 - 2 * scale);
                var tint = ImGui.ColorConvertFloat4ToU32(new Vector4(.48f, .78f, .74f, 1));
                draw.AddCircle(searchCenter, 7 * scale, tint, 24, 2 * scale);
                draw.AddLine(searchCenter + new Vector2(5, 5) * scale, searchCenter + new Vector2(11, 11) * scale, tint, 2 * scale);
                ImGui.SetCursorScreenPos(origin + new Vector2(iconWidth, 0));
                ImGui.SetNextItemWidth(width - padding * 2 - iconWidth * 2 - 8 * scale);
                if (focus) { ImGui.SetKeyboardFocusHere(); focus = false; }
                var submit = ImGui.InputTextWithHint("##search", "Search OmniKit...", ref query, 4000, ImGuiInputTextFlags.EnterReturnsTrue);
                SearchHasFocus = ImGui.IsItemActive();
                var edited = ImGui.IsItemEdited();
                if (edited) { selected = 0; resetScroll = true; }
                if (ImGui.IsItemHovered()) Tooltip("Type a game window, plugin, job, or community. Tab opens your companion.", scale);
                if (SearchHasFocus && ImGui.IsKeyPressed(ImGuiKey.Tab)) { companion(query); open = false; }
                ImGui.SetCursorScreenPos(origin + new Vector2(width - padding * 2 - iconWidth, 0));
                if (ImGui.InvisibleButton("Settings", new Vector2(iconWidth, barHeight))) settings();
                var center = origin + new Vector2(width - padding * 2 - iconWidth / 2, barHeight / 2);
                var gearColor = ImGui.GetColorU32(ImGui.IsItemHovered() ? ImGuiCol.Text : ImGuiCol.TextDisabled);
                draw.AddCircle(center, 7 * scale, gearColor, 24, 2 * scale);
                draw.AddCircle(center, 2.5f * scale, gearColor, 16, 1.5f * scale);
                for (var tooth = 0; tooth < 8; tooth++)
                {
                    var angle = tooth * MathF.PI / 4;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    draw.AddLine(center + direction * 7 * scale, center + direction * 11 * scale, gearColor, 3 * scale);
                }
                if (ImGui.IsItemHovered()) Tooltip("OmniKit settings", scale);
                // Geometry was reserved before Begin. Newly typed content appears on the next frame.
                if (!hasQuery || string.IsNullOrWhiteSpace(query)) return;
                ImGui.SetCursorScreenPos(origin + new Vector2(0, barHeight + 6 * scale));
                draw.AddLine(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(width - padding * 2, 0),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(.26f, .28f, .32f, .75f)));
                ImGui.SetCursorScreenPos(origin + new Vector2(0, barHeight + 12 * scale));
                selected = Math.Clamp(selected, 0, Math.Max(0, rows.Count - 1));
                if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
                {
                    if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) selected = Math.Min(selected + 1, rows.Count - 1);
                    if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)) selected = Math.Max(selected - 1, 0);
                }
                // Enter must resolve the current query, never the previous frame's result.
                if (submit)
                {
                    var current = GetResults(entries, query, companion, wiki);
                    if (current.Count > 0) activate(current[Math.Clamp(selected, 0, current.Count - 1)]);
                }
                ImGui.BeginChild("results", new Vector2(0, resultsHeight), false, ImGuiWindowFlags.None);
                if (resetScroll) { ImGui.SetScrollY(0); resetScroll = false; }
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i]; ImGui.PushID(row.Id);
                    var start = ImGui.GetCursorScreenPos();
                    var rowWidth = ImGui.GetContentRegionAvail().X;
                    if (ImGui.Selectable("##result", i == selected, ImGuiSelectableFlags.None, new Vector2(0, rowHeight)) && !edited) activate(row);
                    if (i == selected && (ImGui.IsKeyPressed(ImGuiKey.DownArrow) || ImGui.IsKeyPressed(ImGuiKey.UpArrow))) ImGui.SetScrollHereY();
                    draw = ImGui.GetWindowDrawList();
                    var inset = new Vector2(10, 6) * scale;
                    draw.AddText(start + inset, ImGui.GetColorU32(ImGuiCol.Text), Fit(row.Title, rowWidth - 20 * scale));
                    draw.AddText(start + inset + new Vector2(0, ImGui.GetTextLineHeight() + 3 * scale), ImGui.GetColorU32(ImGuiCol.TextDisabled),
                        Fit(row.Source + " · " + row.Description, rowWidth - 20 * scale));
                    if (ImGui.IsItemHovered()) Tooltip(row.Title + "\n" + row.Description, scale);
                    ImGui.PopID();
                }
                ImGui.EndChild();
                if (statusHeight > 0)
                {
                    ImGui.TextUnformatted(Fit(status, width - padding * 2));
                    if (ImGui.IsItemHovered()) Tooltip(status, scale);
                }
            }
            finally { ImGui.End(); }
        }
        finally { ImGui.PopStyleColor(3); ImGui.PopStyleVar(5); }
    }

    private static IReadOnlyList<SearchEntry> GetResults(IReadOnlyList<SearchEntry> entries, string query,
        Action<string> companion, Action<string> wiki)
    {
        var results = SearchEngine.Find(entries, query);
        if (results.Count > 0) return results;
        return [new("wiki:search", "Search XIV wiki", query, "", "Wiki", () => wiki(query)),
            new("companion:ask", "Ask your companion", query, "", "OmniKit", () => companion(query))];
    }

    private static string Fit(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width) return text;
        while (text.Length > 1 && ImGui.CalcTextSize(text + "...").X > width) text = text[..^1];
        return text + "...";
    }

    private static void Tooltip(string text, float scale)
    {
        var viewport = ImGui.GetMainViewport();
        var padding = ImGui.GetStyle().WindowPadding * 2;
        var wrap = Math.Max(1, Math.Min(600 * scale, viewport.WorkSize.X - 32 * scale) - padding.X);
        var shown = text.Length > 800 ? text[..800] + "..." : text;
        while (shown.Length > 20 && ImGui.CalcTextSize(shown, false, wrap).Y + padding.Y > viewport.WorkSize.Y - 32 * scale)
            shown = shown[..^13] + "...";
        var size = ImGui.CalcTextSize(shown, false, wrap) + padding;
        var minimum = viewport.WorkPos + new Vector2(8 * scale);
        var maximum = Vector2.Max(minimum, viewport.WorkPos + viewport.WorkSize - size - new Vector2(8 * scale));
        ImGui.SetNextWindowPos(Vector2.Clamp(ImGui.GetIO().MousePos + new Vector2(12 * scale), minimum, maximum));
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
        ImGui.TextUnformatted(shown);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
