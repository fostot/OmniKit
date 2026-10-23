using System.Numerics;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Interaction;

var checks = 0;
void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
var output = Path.GetFullPath(args.ElementAtOrDefault(0) ?? ".local/visualharness/hud-effects");
var context = ImGui.CreateContext();
try
{
    unsafe
    {
        var io = ImGui.GetIO(); io.IniFilename = null; io.LogFilename = null;
        io.DisplaySize = new Vector2(1000, 820); io.DeltaTime = 1f / 60;
        var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        if (File.Exists(fontPath)) io.Fonts.AddFontFromFileTTF(fontPath, 24); else io.Fonts.AddFontDefault();
        io.Fonts.Build();
    }
    var renderer = new SoftwareRenderer();
    ImGui.NewFrame();
    foreach (var text in new[] { "Sample NPC", "[Ctrl+E]  Guild vendor", "Étoile · Ω", "旅人 商店", "A😀 B", "e\u0301  a", "One\tTwo" })
    {
        var measured = HudTextRenderer.Measure(ImGui.GetFont(), ImGui.GetFontSize(), text);
        var native = ImGui.CalcTextSize(text);
        Check(Math.Abs(measured.Size.X - native.X) <= 1.01f, $"Glyph advances must match native Unicode text: {text} ({measured.Size.X} vs {native.X}).");
        var shortened = HudTextRenderer.Elide(ImGui.GetFont(), 24, text, measured.Size.X * .6f);
        Check(!shortened.Any(char.IsSurrogate) || shortened.EnumerateRunes().All(r => r.Value != 0xFFFD), "Elision never splits a Unicode scalar.");
    }
    ImGui.Render();

    (Vector2[] Positions, uint[] Colors) Render(HudTextEffect effect, float intensity, float speed, bool animate, bool outline, double time, float size)
    {
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var position = new Vector2(50, 50);
        HudTextRenderer.Draw(draw, ImGui.GetFont(), size, position, "[E]  ", "Étoile 旅人 😀", " · Preview", effect, outline, intensity, speed, animate, time);
        var positions = new Vector2[draw.VtxBuffer.Size]; var colors = new uint[positions.Length];
        var metrics = HudTextRenderer.Measure(ImGui.GetFont(), size, "[E]  Étoile 旅人 😀 · Preview");
        var padding = HudTextRenderer.Padding(effect, size, intensity, outline);
        for (var i = 0; i < positions.Length; i++)
        {
            positions[i] = draw.VtxBuffer[i].Pos; colors[i] = draw.VtxBuffer[i].Col;
            Check(float.IsFinite(positions[i].X) && float.IsFinite(positions[i].Y)
                && positions[i].X >= position.X + metrics.InkMinimum.X - padding.X - .01f
                && positions[i].Y >= position.Y + metrics.InkMinimum.Y - padding.Y - .01f
                && positions[i].X <= position.X + metrics.InkMaximum.X + padding.X + .01f
                && positions[i].Y <= position.Y + metrics.InkMaximum.Y + padding.Y + .01f,
                $"{effect} geometry escaped its declared static bounds at time {time}.");
        }
        ImGui.Render(); return (positions, colors);
    }
    foreach (var effect in Enum.GetValues<HudTextEffect>())
    {
        foreach (var size in new[] { 12f, 24f, 48f })
        foreach (var outline in new[] { false, true })
        foreach (var intensity in new[] { 0f, 1f, 2f })
        {
            var frozen = Render(effect, intensity, .1f, false, outline, 0, size);
            var later = Render(effect, intensity, 3, false, outline, 99, size);
            Check(frozen.Positions.SequenceEqual(later.Positions) && frozen.Colors.SequenceEqual(later.Colors), "Freezing effects is deterministic and independent of time/speed.");
            foreach (var time in new[] { .13, .8, 2.1, 5.2 }) Render(effect, intensity, 3, true, outline, time, size);
        }
        var a = Render(effect, 1, 1, true, false, .2, 24);
        var b = Render(effect, 1, 1, true, false, 1.9, 24);
        if (effect is not (HudTextEffect.None or HudTextEffect.Shadow or HudTextEffect.SoftGlow))
            Check(!a.Positions.SequenceEqual(b.Positions) || !a.Colors.SequenceEqual(b.Colors), $"{effect} must actually animate.");
        var none = Render(HudTextEffect.None, 1, 1, true, false, 0, 24);
        Check(a.Positions.Length <= none.Positions.Length * 20, "Effects keep a bounded pass budget.");
        var zero = Render(effect, 0, 1, true, false, 1, 24);
        Check(zero.Positions.SequenceEqual(none.Positions) && zero.Colors.SequenceEqual(none.Colors), "Zero intensity removes effect decoration and color changes.");
    }
    foreach (var time in new[] { 0d, 1.5d, 3d })
    {
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var row = 0;
        foreach (var effect in Enum.GetValues<HudTextEffect>())
        {
            var y = 20 + row++ * 70;
            draw.AddText(new Vector2(20, y + 12), 0xFFB7BEC9, effect.ToString());
            HudTextRenderer.Draw(draw, ImGui.GetFont(), 32, new Vector2(250, y + 10), "[E]  ", "Moonlit Guild Vendor", string.Empty,
                effect, true, 1.5f, 1, true, time);
        }
        ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"effects-{time:0.0}.png"));
    }
    Console.WriteLine($"HUD effects: {checks} checks passed; native font-atlas contact sheets saved.");
}
finally { ImGui.DestroyContext(context); }
