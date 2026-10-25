using System.Numerics;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

internal static class PoofChecks
{
    public static void Run()
    {
        var output = Directory.CreateDirectory(Path.GetFullPath(".local/companion-appearance-tests"));
        var checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
        var poof = new CompanionPoofTransition();
        var idle = poof.Update("Moonblob", "Moonblob", true, true, 0);
        Check(idle.CanApplyAppearance && !idle.IsActive, "An unchanged avatar needs no transition.");
        Check(!poof.Update("Moonblob", "Wink", false, true, .05f).IsActive, "Unsafe request waits invisibly.");
        var waiting = poof.Update("Moonblob", "Moonblob", false, true, .05f);
        Check(!waiting.CanApplyAppearance && !waiting.IsActive, "An earlier pending request is retained while unsafe.");
        Check(poof.Update("Moonblob", "Moonblob", true, true, 0).IsActive, "Retained request starts when safe.");
        var current = "Moonblob"; var handoffs = 0; var peak = default(CompanionPoofDecision);
        for (var i = 0; i < 24; i++)
        {
            var decision = poof.Update(current, "Wink", true, true, .025f);
            Check(decision.Coverage is >= 0 and <= 1 && decision.Progress is >= 0 and <= 1, "Progress is bounded.");
            if (decision.CanApplyAppearance && decision.AvatarToApply != current)
            { handoffs++; current = decision.AvatarToApply; peak = decision; }
        }
        Check(handoffs == 1 && current == "Wink" && peak.IsActive && peak.Coverage == 1, "Exactly one body handoff occurs under full coverage.");
        Check(!poof.IsActive, "The puff fully clears after reveal.");

        poof.Reset(); poof.Update("Mochi", "Ember", true, true, 0);
        poof.Update("Mochi", "Ember", true, true, .05f);
        var interrupted = poof.Update("Mochi", "Ember", false, true, .05f);
        Check(!interrupted.IsActive && !interrupted.CanApplyAppearance, "Hover/carry/nap cancels a pre-swap cover safely.");
        for (var i = 0; i < 4; i++) Check(!poof.Update("Mochi", "Mochi", false, true, .05f).CanApplyAppearance, "Unsafe frames retain the pending request without applying it.");
        Check(poof.Update("Mochi", "Mochi", true, true, 0).IsActive, "The interrupted target is not lost.");
        for (var i = 0; i < 3; i++) Check(!poof.Update("Mochi", "Ember", true, true, .05f).CanApplyAppearance, "A restarted cover does not inherit the old elapsed time.");
        Check(poof.Update("Mochi", "Ember", true, true, .05f).AvatarToApply == "Ember", "Restart applies the captured target.");
        for (var i = 0; i < 8; i++) poof.Update("Ember", "Ember", false, true, .05f);
        Check(!poof.IsActive, "Becoming unsafe after the swap does not strand a visible cloud.");

        poof.Reset(); poof.Update("Nova", "Puff", true, true, 0);
        for (var i = 0; i < 3; i++) poof.Update("Nova", "Prism", true, true, .05f);
        var captured = poof.Update("Nova", "Prism", true, true, .05f);
        Check(captured.CanApplyAppearance && captured.AvatarToApply == "Puff", "A newer automatic request cannot replace the captured swap target.");
        for (var i = 0; i < 6; i++) poof.Update("Puff", "Prism", true, true, .05f);
        Check(!poof.IsActive, "First reveal completes before a queued second transition.");
        Check(poof.Update("Puff", "Prism", true, true, 0).IsActive, "The latest requested target starts its own cover.");
        var disabled = poof.Update("Puff", "Orbit", false, false, .05f);
        Check(disabled.CanApplyAppearance && disabled.AvatarToApply == "Orbit" && !disabled.IsActive, "Disabled effects/animation permit immediate selection and reset.");
        Check(poof.Update("Orbit", "Orbit", true, true, 0).CanApplyAppearance && !poof.IsActive, "Re-enabling cannot resurrect the old request.");
        poof.Update("Orbit", "Nova", true, true, 0);
        foreach (var dt in new[] { float.NaN, float.PositiveInfinity, -.5f })
            Check(poof.Update("Orbit", "Nova", true, true, dt).Coverage == 0, "Invalid/negative elapsed time cannot advance or flash.");
        Check(!poof.Update("Orbit", "Nova", true, true, 30).CanApplyAppearance, "A long suspended frame cannot skip the cover.");
        poof.Reset();
        Check(!poof.IsActive && poof.Coverage == 0, "Manual-change bypass clears every effect immediately.");

        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); unsafe { io.IniFilename = null; io.LogFilename = null; }
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            io.DeltaTime = 1f / 60; io.Fonts.AddFontDefault(); io.Fonts.Build();
            var renderer = new SoftwareRenderer(); var color = new Vector4(.58f, .72f, 1, .2f);
            poof.Update("Moonblob", "Wink", true, true, 0);
            poof.Update("Moonblob", "Wink", true, true, .05f);
            poof.Update("Moonblob", "Wink", false, false, 0);
            io.DisplaySize = new(300); ImGui.NewFrame();
            var disabledDraw = ImGui.GetForegroundDrawList();
            poof.Draw(disabledDraw, new(50), new(146), color);
            Check(disabledDraw.VtxBuffer.Size == 0, "Disabling an active transition leaves no native geometry.");
            ImGui.Render();
            foreach (var size in new[] { 96f, 192f }) foreach (var ratio in new[] { new Vector2(1), new Vector2(1, 1.5f), new Vector2(1.6f, 1) })
            {
                var min = new Vector2(70); var max = min + size * ratio;
                poof.Reset(); poof.Update("Moonblob", "Wink", true, true, 0);
                var selected = "Moonblob";
                for (var step = 0; step < 24; step++)
                {
                    var decision = poof.Update(selected, "Wink", true, true, .025f);
                    if (decision.CanApplyAppearance) selected = decision.AvatarToApply;
                    io.DisplaySize = new(600); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                    var bounds = poof.Draw(draw, min, max, color);
                    Check(Enumerable.Range(0, draw.VtxBuffer.Size).All(i => {
                        var p = draw.VtxBuffer[i].Pos;
                        return float.IsFinite(p.X) && float.IsFinite(p.Y) && p.X >= bounds.Minimum.X - .01f && p.Y >= bounds.Minimum.Y - .01f
                            && p.X <= bounds.Maximum.X + .01f && p.Y <= bounds.Maximum.Y + .01f;
                    }), "Every native cloud/sparkle vertex stays in the declared gutter.");
                    Check(draw.VtxBuffer.Size < 5000, "Poof geometry remains bounded.");
                    if (!decision.IsActive) Check(draw.VtxBuffer.Size == 0, "Completed/disabled puff leaves no geometry behind.");
                    ImGui.Render();
                }
            }
            // Compare two different solid body/hat fills under the same peak puff.
            // The software rasterizer truncates barycentric RGB/alpha by up to two
            // channel levels. A visible old/new body contribution would be much larger.
            foreach (var size in new[] { 96f, 192f })
            {
                poof.Reset(); poof.Update("Moonblob", "Wink", true, true, 0);
                for (var i = 0; i < 4; i++) poof.Update("Moonblob", "Wink", true, true, .05f);
                io.DisplaySize = new(size * 3 + 100, size * 2 + 100); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                var first = new Vector2(35, 40); var second = first + new Vector2(MathF.Ceiling(size * 1.6f), 0); var bodySize = new Vector2(size, size * 1.3f);
                draw.AddRectFilled(first, first + bodySize, 0xFFFF00FF); draw.AddRectFilled(second, second + bodySize, 0xFF00FF00);
                poof.Draw(draw, first, first + bodySize, color); poof.Draw(draw, second, second + bodySize, color);
                ImGui.Render(); var path = Path.Combine(output.FullName, $"poof-opaque-{size:0}.png"); renderer.Save(ImGui.GetDrawData(), path);
                using var image = new Bitmap(path);
                for (var y = 1; y < (int)bodySize.Y - 1; y += 3) for (var x = 1; x < (int)bodySize.X - 1; x += 3)
                {
                    var a = image.GetPixel((int)first.X + x, (int)first.Y + y); var b = image.GetPixel((int)second.X + x, (int)second.Y + y);
                    Check(Math.Abs(a.R - b.R) <= 2 && Math.Abs(a.G - b.G) <= 2 && Math.Abs(a.B - b.B) <= 2, "Opaque peak conceals body/hat colors within rasterization rounding tolerance.");
                }
            }
            foreach (var size in new[] { 96f, 192f })
            {
                var tile = new Vector2(size * 1.7f, size * 2.12f + 24); io.DisplaySize = new(tile.X * 4, tile.Y * 2);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                float[] times = [0, .06f, .12f, .18f, .23f, .33f, .43f, .49f];
                for (var i = 0; i < times.Length; i++)
                {
                    poof.Reset(); poof.Update("Moonblob", "Wink", true, true, 0); var elapsed = 0f; var currentBody = "Moonblob";
                    while (elapsed < times[i] - .0001f)
                    {
                        var dt = Math.Min(.01f, times[i] - elapsed); elapsed += dt;
                        var decision = poof.Update(currentBody, "Wink", true, true, dt);
                        if (decision.CanApplyAppearance) currentBody = decision.AvatarToApply;
                    }
                    var p = new Vector2(i % 4 * tile.X + size * .35f, i / 4 * tile.Y + size * .35f + 18);
                    draw.AddText(new(i % 4 * tile.X + 6, i / 4 * tile.Y + 4), 0xFFFFFFFF, times[i].ToString("0.00") + " seconds");
                    // Synthetic complete body and hat make the covered union explicit.
                    draw.AddCircleFilled(p + new Vector2(size * .5f, size * .72f), size * .46f, currentBody == "Moonblob" ? 0xFFF0B683 : 0xFF92BFED, 48);
                    draw.AddTriangleFilled(p + new Vector2(size * .20f, size * .28f), p + new Vector2(size * .80f, size * .28f), p + new Vector2(size * .50f, 0), 0xFF9170A3);
                    poof.Draw(draw, p, p + new Vector2(size, size * 1.2f), color);
                }
                ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"poof-cycle-{size:0}.png"));
            }
        }
        finally { ImGui.DestroyContext(context); }
        Console.WriteLine($"Companion poof: {checks} checks passed; pure request gate and native96/192 body/hat coverage. No game, profile or network access.");
    }
}
