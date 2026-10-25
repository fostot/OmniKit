using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace OmniKit.Ui;

internal static class UiText
{
    internal static readonly Vector4 Accent = new(.48f, .78f, .74f, 1);
    internal static readonly Vector4 Muted = new(.63f, .65f, .69f, 1);
    internal static readonly Vector4 Warning = new(.94f, .75f, .40f, 1);
    internal static readonly Vector4 Error = new(.96f, .51f, .45f, 1);

    // Optional in-process measurement for the offscreen UI harness; unset in the plugin.
    internal static Action<string, Vector2, Vector2, bool>? ItemMeasured { get; set; }
    private static void Measure(string label)
    {
        if (ItemMeasured == null) return;
        var draw = ImGui.GetWindowDrawList();
        var minimum = Vector2.Max(ImGui.GetItemRectMin(), draw.GetClipRectMin());
        var maximum = Vector2.Min(ImGui.GetItemRectMax(), draw.GetClipRectMax());
        ItemMeasured(label, minimum, maximum, ImGui.IsItemVisible() && minimum.X < maximum.X && minimum.Y < maximum.Y);
    }
    internal static bool Button(string label) { var result = ImGui.Button(label); Measure(label); return result; }
    internal static bool SliderFloat(string label, ref float value, float minimum, float maximum, string format)
    {
        var result = ImGui.SliderFloat(label, ref value, minimum, maximum, format, ImGuiSliderFlags.AlwaysClamp);
        Measure(label); return result;
    }
    internal static bool Button(string label, Vector2 size) { var result = ImGui.Button(label, size); Measure(label); return result; }
    internal static bool BeginCombo(string label, string preview) { var result = ImGui.BeginCombo(label, preview); Measure(label); return result; }
    internal static bool BeginTabItem(string label, ImGuiTabItemFlags flags = ImGuiTabItemFlags.None)
    { var result = ImGui.BeginTabItem(label, flags); Measure("tab:" + label); return result; }
    internal static bool Selectable(string label, bool selected = false)
    { var result = ImGui.Selectable(label, selected); Measure(label); return result; }
    internal static bool CollapsingHeader(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
    { var result = ImGui.CollapsingHeader(label, flags); Measure(label); return result; }

    internal static void Line(string text, Vector4? color = null, bool outline = false, string? detail = null)
    {
        var start = ImGui.GetCursorScreenPos();
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var shown = Fit(text, width);
        ImGui.Dummy(new Vector2(width, ImGui.GetTextLineHeight()));
        var draw = ImGui.GetWindowDrawList();
        if (outline)
        {
            var offset = Math.Max(.5f, ImGuiHelpers.GlobalScale * .65f);
            foreach (var direction in new[] { new Vector2(-1,0), new Vector2(1,0), new Vector2(0,-1), new Vector2(0,1) })
                draw.AddText(start + direction * offset, 0x99000000, shown);
        }
        draw.AddText(start, ImGui.ColorConvertFloat4ToU32(color ?? ImGui.GetStyle().Colors[(int)ImGuiCol.Text]), shown);
        if (ImGui.IsItemHovered() && (detail != null || shown != text)) Tooltip(detail ?? text);
    }

    internal static void Status(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var lower = text.ToLowerInvariant();
        var error = new[] { "could not", "failed", "failure", "error", "rejected", "declined" }.Any(lower.Contains);
        var warning = new[] { "unavailable", "not connected", "no model", "choose ", "paused", "off", "not loaded", "unsupported", "not passed", "missing", "canceled", "warning", "not usable", "not active", "must appear", "does not show", "not in your", "ambiguous", "not supported" }.Any(lower.Contains);
        Line(text, error ? Error : warning ? Warning : Accent, outline: true);
    }

    internal static void Help(string label, string text)
    {
        if (CollapsingHeader(label))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Muted);
            ImGui.TextWrapped(text);
            ImGui.PopStyleColor();
        }
    }

    internal static void Hint(string text) { if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) Tooltip(text); }

    internal static string Fit(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width) return text;
        var length = text.Length;
        while (length > 1 && ImGui.CalcTextSize(text[..length] + "…").X > width) length--;
        if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
        return text[..length] + "…";
    }

    internal static void Tooltip(string text)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var viewport = ImGui.GetMainViewport();
        var padding = ImGui.GetStyle().WindowPadding * 2;
        var wrap = Math.Max(1, Math.Min(420 * scale, viewport.WorkSize.X - 24 * scale) - padding.X);
        var shown = text.Length > 1600 ? text[..1600] + "…" : text;
        while (shown.Length > 20 && ImGui.CalcTextSize(shown, false, wrap).Y + padding.Y > viewport.WorkSize.Y - 24 * scale)
            shown = shown[..^13] + "…";
        var size = ImGui.CalcTextSize(shown, false, wrap) + padding;
        var minimum = viewport.WorkPos + new Vector2(8 * scale);
        var maximum = Vector2.Max(minimum, viewport.WorkPos + viewport.WorkSize - size - new Vector2(8 * scale));
        ImGui.SetNextWindowPos(Vector2.Clamp(ImGui.GetMousePos() + new Vector2(12 * scale), minimum, maximum));
        ImGui.BeginTooltip(); ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
        ImGui.TextUnformatted(shown); ImGui.PopTextWrapPos(); ImGui.EndTooltip();
    }
}
