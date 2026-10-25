using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

if (args.Contains("--poof-only")) { PoofChecks.Run(); return; }

var output = Directory.CreateDirectory(Path.GetFullPath(".local/companion-appearance-tests"));
var assets = Path.GetFullPath("assets");
var variants = new CompanionVariantLibrary(assets);
var holidays = new CompanionHolidayLibrary(assets);
string[] avatars = ["Moonblob", "Wink", "Nova", "Mochi", "Puff", "Orbit", "Ember", "Prism"];
string[] effectAvatars = [.. avatars, "Nib", "Echo", "Fizz", "Glim", "Rune"];
SeasonalCompanionTheme?[] themes = [null, .. Enum.GetValues<SeasonalCompanionTheme>().Cast<SeasonalCompanionTheme?>()];
var checks = 0;
void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException(reason); }
Check(variants.IsAvailable && holidays.IsAvailable, "Reviewed everyday and holiday art load.");
var request = new CompanionAppearanceRequest("Moonblob", "Look1", true, "Auto", "Auto");
var selection = new CompanionAppearanceSelection();
var initial = selection.Update(request, SeasonalCompanionTheme.AllSaintsWake, false);
Check(initial.CanApplyBody && !initial.ManualChange && initial.HolidayTheme == SeasonalCompanionTheme.AllSaintsWake, "Startup initializes even when physically paused.");
Check(!selection.Update(request, SeasonalCompanionTheme.Starlight, false).CanApplyBody, "Automatic event change defers during sleep/carry.");
Check(selection.Update(request, SeasonalCompanionTheme.Starlight, true).CanApplyBody, "Automatic event change applies once the gate opens.");
foreach (var changed in new[] {
    request with { Avatar = "Prism" }, request with { EverydayChoice = "Look4" }, request with { HolidayEnabled = false },
    request with { HolidayBodySelection = "MoonfireFaire" }, request with { SeasonalThemeSelection = "Starlight" },
    request with { CostumeSelection = "Starlight" } })
{
    var state = new CompanionAppearanceSelection(); state.Update(request, null, true);
    var choice = state.Update(changed, SeasonalCompanionTheme.AllSaintsWake, false);
    Check(choice.ManualChange && choice.CanApplyBody, "Every explicit tuple change applies with the sleep/carry gate closed.");
    Check(!state.Update(changed, SeasonalCompanionTheme.Starlight, false).CanApplyBody, "A manual change is consumed once, not a permanent gate bypass.");
}
Check(CompanionAppearanceSelection.ResolveHoliday(true, "Starlight", SeasonalCompanionTheme.AllSaintsWake) == SeasonalCompanionTheme.Starlight, "Specific holiday body wins independently of active accessories/event.");
foreach (var invalid in new[] { "0", "Unknown", "starlight", "", " Auto" })
    Check(CompanionAppearanceSelection.ResolveHoliday(true, invalid, SeasonalCompanionTheme.AllSaintsWake) == null, "Invalid holiday choice fails closed.");
Check(CompanionAppearanceSelection.ResolveHoliday(false, "Starlight", SeasonalCompanionTheme.AllSaintsWake) == null, "Disabled body leaves everyday choice available.");
Check(CompanionAppearanceSelection.ResolveHoliday(true, "Auto", (SeasonalCompanionTheme)999) == null, "Invalid automatic theme fails closed.");

// The production variant selector receives the helper's gate, including during sleep.
var gate = new CompanionAppearanceSelection(); var everyday = new CompanionVariantSelection(variants, _ => 1);
CompanionVariantLook? visible = null;
CompanionVariantLook? Resolve(CompanionAppearanceRequest input, SeasonalCompanionTheme? active, bool canSwitch, double now)
{
    var decision = gate.Update(input, active, canSwitch);
    var ordinary = everyday.Resolve(input.Avatar, input.EverydayChoice, true, 30, now, decision.CanApplyBody);
    if (decision.CanApplyBody) visible = decision.HolidayTheme is { } holiday ? holidays.FindHoliday(input.Avatar, holiday) ?? ordinary : ordinary;
    return visible;
}
Check(Resolve(request, SeasonalCompanionTheme.AllSaintsWake, true, 0)?.Choice == "HolidayAllSaintsWake", "Active holiday body is selected.");
var everydayRequest = request with { EverydayChoice = "Look3", HolidayEnabled = false };
Check(Resolve(everydayRequest, SeasonalCompanionTheme.AllSaintsWake, false, 1)?.Choice == "Look3", "Explicit everyday selection appears during sleep.");
var holidayRequest = everydayRequest with { HolidayEnabled = true, HolidayBodySelection = "Starlight" };
Check(Resolve(holidayRequest, SeasonalCompanionTheme.AllSaintsWake, false, 2)?.Choice == "HolidayStarlight", "Named holiday applies during sleep independently of event.");
Check(everyday.Current?.Choice == "Look3" && holidayRequest.EverydayChoice == "Look3", "Holiday selection preserves the remembered everyday look.");
Check(Resolve(holidayRequest with { HolidayEnabled = false }, null, false, 3)?.Choice == "Look3", "Disabling holiday restores the saved everyday look while asleep.");
var randomRequest = everydayRequest with { EverydayChoice = "Random" };
var firstRandom = Resolve(randomRequest, null, true, 4);
Check(ReferenceEquals(firstRandom, Resolve(randomRequest, null, false, 100)), "Due random rotation still defers while asleep/carrying.");
Check(!ReferenceEquals(firstRandom, Resolve(randomRequest, null, true, 101)), "Due random rotation resumes when safe.");
gate.Reset(); Check(gate.Update(request, null, false).CanApplyBody, "Explicit reset restores initialization.");

var context = ImGui.CreateContext();
try
{
    var io = ImGui.GetIO(); unsafe { io.IniFilename = null; io.LogFilename = null; }
    io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
    io.Fonts.AddFontDefault(); io.Fonts.Build(); io.DeltaTime = 1f / 60;
    var renderer = new SoftwareRenderer();
    var color = new Vector4(.67f, .87f, .97f, 1);
    (Vector2 P, uint C)[] Sample(string avatar, SeasonalCompanionTheme? theme, float size, float time, bool animate,
        bool magic = true, bool editor = false, bool mirrored = false)
    {
        io.DisplaySize = new(320); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        var origin = new Vector2(50); var bounds = CompanionSignatureEffects.Draw(draw, avatar, theme, origin, new(size), color, time, magic, animate, editor, mirrored);
        var vertices = Enumerable.Range(0, draw.VtxBuffer.Size).Select(i => (draw.VtxBuffer[i].Pos, draw.VtxBuffer[i].Col)).ToArray();
        Check(vertices.All(v => float.IsFinite(v.Pos.X) && float.IsFinite(v.Pos.Y)
            && v.Pos.X >= bounds.Minimum.X - .01f && v.Pos.Y >= bounds.Minimum.Y - .01f
            && v.Pos.X <= bounds.Maximum.X + .01f && v.Pos.Y <= bounds.Maximum.Y + .01f), "Every native effect vertex fits its declared reserved gutter.");
        ImGui.Render(); return vertices;
    }
    foreach (var avatar in effectAvatars) foreach (var theme in themes) foreach (var size in new[] { 96f, 192f }) foreach (var mirror in new[] { false, true })
    {
        var atZero = Sample(avatar, theme, size, 0, true, mirrored: mirror);
        Check(atZero.Length > 0 && atZero.Length < 5000, "Effects have bounded native geometry.");
        var later = Sample(avatar, theme, size, 3, true, mirrored: mirror);
        Check(!atZero.SequenceEqual(later), "Each companion has continuous clock-driven motion.");
        var next = Sample(avatar, theme, size, 3 + 1f / 60, true, mirrored: mirror);
        Check(later.Length == next.Length && later.Zip(next).All(v => Vector2.Distance(v.First.P, v.Second.P) <= size * .006f
            && v.First.C == v.Second.C), "One animation step has bounded movement and no opacity/color flashes.");
        Check(Sample(avatar, theme, size, 10, false, mirrored: mirror).SequenceEqual(Sample(avatar, theme, size, 500, false, mirrored: mirror)), "Animation off freezes both geometry and color.");
        Check(Sample(avatar, theme, size, 3, true, magic: false, mirrored: mirror).Length == 0, "Magic off produces no vertices.");
        Check(Sample(avatar, theme, size, 3, true, editor: true, mirrored: mirror).Length == 0, "Appearance editor bypass produces no vertices.");
        Check(Sample(avatar, theme, size, float.NaN, true, mirrored: mirror).SequenceEqual(atZero), "Invalid clock returns deterministic still art.");
    }
    var signatures = effectAvatars.Select(a => Sample(a, null, 96, 1, true)).ToArray();
    Check(signatures.Select(v => string.Join(';', v.Select(p => p.P.ToString()))).Distinct().Count() == 13, "Thirteen companions have distinct geometry, not just palette changes.");
    var holidayShapes = themes.Select(t => Sample("Moonblob", t, 96, 1, true)).ToArray();
    Check(holidayShapes.Select(v => string.Join(';', v.Select(p => p.P.ToString()))).Distinct().Count() == 10, "Nine holidays add distinct motifs to ordinary effects.");
    Check(Sample("unknown", null, 96, 0, true).Length == 0, "Unknown companion produces no fallback effects.");
    Check(Sample("Nova", null, -1, 0, true).Length == 0, "Invalid dimensions draw nothing.");

    var textures = new Dictionary<string, ulong>();
    foreach (var file in holidays.Files.Concat(variants.Files).Distinct())
    {
        using var bitmap = new Bitmap(Path.Combine(assets, file));
        var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, pixels, y * bitmap.Width * 4, bitmap.Width * 4); }
        finally { bitmap.UnlockBits(bits); }
        for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        var id = (ulong)textures.Count + 2; renderer.AddTexture(id, pixels, bitmap.Width, bitmap.Height); textures.Add(file, id);
    }
    foreach (var size in new[] { 96f, 192f }) foreach (var mirror in new[] { false, true })
    {
        var tile = new Vector2(size * 1.4f, size * 1.4f + 20); io.DisplaySize = new(tile.X * 8, tile.Y * 10);
        ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
        for (var row = 0; row < themes.Length; row++) for (var column = 0; column < avatars.Length; column++)
        {
            var avatar = avatars[column]; var theme = themes[row]; var origin = new Vector2(column * tile.X + size * .2f, row * tile.Y + size * .2f + 15);
            draw.AddText(new(column * tile.X + 5, row * tile.Y + 3), 0xFFFFFFFF, avatar);
            draw.AddText(new(column * tile.X + 5, row * tile.Y + 16), 0xFFA8B2BB, theme?.ToString() ?? "Everyday");
            CompanionSignatureEffects.Draw(draw, avatar, theme, origin, new(size), color, 3, true, true, false, mirror);
            var look = theme is { } value ? holidays.FindHoliday(avatar, value)! : variants.Find(avatar, "Look1")!;
            CompanionVariantRenderer.Draw(draw, new(textures[look.Normal.File]), look.Normal.GetFrame(origin, new(size), mirror));
        }
        ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output.FullName, $"signatures-{size:0}-{(mirror ? "left" : "right")}.png"));
    }
}
finally { ImGui.DestroyContext(context); }
Console.WriteLine($"Companion appearance: {checks} checks passed; source-linked selection and actual ImGui effects at96/192, both facings. No game/network/profile access.");
