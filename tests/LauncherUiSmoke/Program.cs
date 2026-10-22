using System.Numerics;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Search;
using OmniKit.Ui;

var keyboardOnly = args.Contains("--keyboard");
var tooltipsOnly = args.Contains("--tooltips");
var output = Path.GetFullPath(args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)) ?? ".local/visualharness/launcher");
var failures = new List<string>();
var observedHeights = new Dictionary<string, int>();
var renders = 0;
var entries = new List<SearchEntry>
{
    Entry("free-company", "Free Company", "Open your Free Company window.", "community guild free company social"),
    Entry("finder", "Community Finder", "Find players and communities on the official website.", "community guild free company social"),
    Entry("fellowship", "Fellowship", "Browse groups that share your interests.", "community guild free company social"),
    Entry("linkshell", "Linkshells", "Open your local linkshell groups.", "community guild free company social"),
};
for (var i = 0; i < 16; i++)
    entries.Add(Entry("long-" + i, $"Very long plugin action {i + 1:00}: a title that should fit without covering the scrollbar or overflowing the panel", "A deliberately lengthy description that must end with an ellipsis before the right edge of its row.", "long scroll"));

if (!tooltipsOnly) VerifyKeyboard();
foreach (var scale in keyboardOnly ? [] : new[] { 1f, 1.5f, 2f })
foreach (var viewport in new[] { new Vector2(1280, 720), new Vector2(640, 480) })
{
    if (tooltipsOnly && (viewport.X != 640 || scale == 1)) continue;
    Render("blank", "", scale, viewport);
    if (tooltipsOnly) { Render("long-scroll", "long", scale, viewport, scroll: true); continue; }
    Render("whitespace", "   ", scale, viewport);
    Render("community", "community", scale, viewport);
    Render("long-scroll", "long", scale, viewport, scroll: true);
    Render("no-match", "unmatched-query-xyz", scale, viewport);
    Render("cleared", "community", scale, viewport, clear: true);
    Render("status", "long", scale, viewport, status: "A long status message is shown here and must remain inside the launcher bounds.");
}
if (failures.Count > 0)
{
    foreach (var failure in failures) Console.WriteLine("FAIL: " + failure);
    Environment.ExitCode = 1;
}
else if (renders > 0) Console.WriteLine($"PASS: {renders} actual launcher renders retain viewport gutters and expected expanded/collapsed heights.");

SearchEntry Entry(string id, string title, string description, string keywords) =>
    new(id, title, description, keywords, "FFXIV", () => { });

unsafe void VerifyKeyboard()
{
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(1280, 720);
        io.DeltaTime = 1f / 60;
        io.IniFilename = null; io.LogFilename = null;
        io.Fonts.AddFontDefault(); io.Fonts.Build();
        _ = new SoftwareRenderer();
        var launcher = new CompactLauncher();
        var query = "community";
        var open = true;
        string? activated = null;
        launcher.Focus();
        for (var frame = 0; frame < 8; frame++)
        {
            if (frame == 3) io.AddKeyEvent(ImGuiKey.DownArrow, true);
            if (frame == 4) io.AddKeyEvent(ImGuiKey.DownArrow, false);
            if (frame == 5) io.AddKeyEvent(ImGuiKey.Enter, true);
            if (frame == 6) io.AddKeyEvent(ImGuiKey.Enter, false);
            ImGui.NewFrame();
            launcher.Draw(ref open, ref query, entries, 1, entry => activated = entry.Id, () => { }, _ => { }, _ => { });
            ImGui.Render();
        }
        if (activated != SearchEngine.Find(entries, query)[1].Id)
            failures.Add("Focused search: Down then Enter did not activate the second current result.");
        else Console.WriteLine("PASS: focused search routes Down + Enter to the second current result.");
    }
    finally { ImGui.DestroyContext(context); }
}

unsafe void Render(string name, string query, float scale, Vector2 viewport, bool scroll = false, bool clear = false, string status = "")
{
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO();
        io.DisplaySize = viewport;
        io.DeltaTime = 1f / 60;
        io.IniFilename = null;
        io.LogFilename = null;
        var font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        if (File.Exists(font)) io.Fonts.AddFontFromFileTTF(font, 17f * scale);
        else { io.Fonts.AddFontDefault(); io.FontGlobalScale = 17f / 13f * scale; }
        io.Fonts.Build();
        var renderer = new SoftwareRenderer();
        ImGui.StyleColorsDark();
        var launcher = new CompactLauncher();
        var open = true;
        for (var frame = 0; frame < 5; frame++)
        {
            if (clear && frame == 2) query = "";
            if (scroll && frame >= 2)
            {
                io.AddMousePosEvent(viewport.X / 2, viewport.Y * .5f);
                io.AddMouseWheelEvent(0, -3);
            }
            ImGui.NewFrame();
            launcher.Draw(ref open, ref query, entries, scale, _ => { }, () => { }, _ => { }, _ => { }, status);
            ImGui.Render();
        }
        var id = $"{name}-{viewport.X}x{viewport.Y}-scale{scale:0.0}";
        renders++;
        var bounds = renderer.Save(ImGui.GetDrawData(), Path.Combine(output, id + ".png"));
        if (bounds.Left < 2 || bounds.Top < 2 || bounds.Right >= viewport.X - 2 || bounds.Bottom >= viewport.Y - 2)
            failures.Add(id + " reaches the viewport edge; inspect for clipping.");
        var key = $"{viewport.X}-{viewport.Y}-{scale}";
        var height = bounds.Bottom - bounds.Top + 1;
        if (name == "blank") observedHeights[key] = height;
        else if (name is "whitespace" or "cleared")
        {
            if (height != observedHeights[key]) failures.Add(id + " did not collapse to the blank search bar height.");
        }
        else if (height <= observedHeights[key]) failures.Add(id + " failed to reveal result content.");
    }
    finally { ImGui.DestroyContext(context); }
}
