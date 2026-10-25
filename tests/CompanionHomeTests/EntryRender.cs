using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

internal static class EntryRender
{
    public static void Run(string root, string label)
    {
        var assets = Path.Combine(root, "assets");
        var output = Path.Combine(root, ".local", "home-entry-audit", label);
        var homes = new CompanionHomeStyleLibrary(assets);
        var naps = new CompanionNapArtwork(assets);
        if (!naps.TryGet("Wink", "Default", out var nap) || nap == null) throw new InvalidOperationException("Missing nap fixture.");
        var context = ImGui.CreateContext();
        var io = ImGui.GetIO(); io.DeltaTime = 1f / 60;
        unsafe { io.IniFilename = null; io.LogFilename = null; }
        io.Fonts.AddFontDefault(); io.Fonts.Build();
        var renderer = new SoftwareRenderer(); var textures = new Dictionary<string, ulong>();
        foreach (var file in new[] { "companion-house.png", "homes-standard.png", "homes-holiday.png", nap.File })
        {
            using var image = new Bitmap(Path.Combine(assets, file));
            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = new byte[image.Width * image.Height * 4];
            try { for (var y = 0; y < image.Height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * image.Width * 4, image.Width * 4); }
            finally { image.UnlockBits(data); }
            for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            var id = (ulong)textures.Count + 2; textures.Add(file, id); renderer.AddTexture(id, pixels, image.Width, image.Height);
        }
        try
        {
            foreach (var edge in new[] { 96f, 192f })
            foreach (var id in CompanionHomeStyleLibrary.HomeIds.Where(value => value != "Random"))
            {
                var tile = new Vector2(edge * 2.5f + 24, edge * 1.7f + 36);
                io.DisplaySize = tile * new Vector2(6, 2);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                for (var row = 0; row < 2; row++)
                for (var stage = 0; stage < 6; stage++)
                {
                    var mirrored = row == 1; var top = tile * new Vector2(stage, row);
                    var house = new CompanionHomeRect(top + new Vector2(edge * .7f, 28), top + new Vector2(edge * 2, 28 + edge * 1.3f));
                    var style = homes.GetHome(id).Place(house.Minimum, house.Maximum, mirrored);
                    var layout = new CompanionLayout(top, top + tile, new(edge));
                    var routine = Sample(layout, house, style, stage);
                    draw.PushClipRect(top, top + tile, true);
                    var houseTexture = new ImTextureID(textures[style.File]);
                    CompanionHomeRenderer.DrawHouse(draw, houseTexture, house, mirrored, style);
                    var before = draw.VtxBuffer.Size;
                    if (routine.UsesNapArt)
                        CompanionHomeRenderer.DrawNap(draw, new(textures[nap.File]), houseTexture, nap, routine,
                            layout.SpriteSize, house, mirrored, styleFrame: style);
                    else
                    {
                        // The approach marker uses the awake entry pose at the actual
                        // navigation position; the sleeping nap pose is not walking art.
                        var awake = nap.Frame(0, routine.Position, layout.SpriteSize, 1, routine.FacingLeft);
                        draw.AddImage(new(textures[nap.File]), awake.Position, awake.Position + awake.Size, awake.UvStart, awake.UvEnd);
                    }
                    if (routine.State == CompanionHomeState.Entering && routine.InFrontOfHome && draw.VtxBuffer.Size - before != 4)
                        throw new InvalidOperationException("Walls were painted over the foreground entry pose.");
                    if (routine.State == CompanionHomeState.Entering && !routine.InFrontOfHome
                        && (routine.ArtScale > CompanionHomeRoutine.IndoorScale + .001f
                            || routine.Position.Y + edge * .9f > style.Threshold.Y + .01f || draw.VtxBuffer.Size - before <= 4))
                        throw new InvalidOperationException("The masked entry crossed an unverified part of the house.");
                    draw.AddText(top + new Vector2(5, 5), 0xFFFFFFFF, $"{id} {(mirrored ? "left" : "right")} / {stage}");
                    draw.PopClipRect();
                }
                ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"{id}-{edge:0}.png"));
            }
        }
        finally { ImGui.DestroyContext(context); }
        Console.WriteLine("Entry renderer: 26 galleries, 312 actual production house/nap samples; no game services.");
    }

    private static CompanionHomeRoutine Sample(CompanionLayout layout, CompanionHomeRect house, CompanionHomeStyleFrame style, int stage)
    {
        var routine = new CompanionHomeRoutine(17); routine.Reset(15);
        var start = CompanionHomeGeometry.Corner(layout, house, style.Mirrored, true);
        var outside = style.OutsidePosition(layout.SpriteSize, grounded: true);
        for (var tick = 0; tick < 2500; tick++)
        {
            routine.Update(.025f, true, false, start, style.Mirrored, layout, house, style.Mirrored, 15, 5,
                layout.Minimum, layout.Maximum, styleFrame: style, navigator: Navigate);
            if (stage == 0 && routine.State == CompanionHomeState.Approaching && Vector2.Distance(routine.Position, outside) < layout.SpriteSize.X * .3f) return routine;
            if (stage is >= 1 and <= 4 && routine.State == CompanionHomeState.Entering && routine.SleepPhase >= new[] { 0f, .025f, .4f, .95f, 1.7f }[stage]) return routine;
            if (stage == 5 && routine.Sleeping) return routine;
        }
        throw new InvalidOperationException("The grounded synthetic approach could not reach its entry sample.");

        CompanionHomeTravel Navigate(Vector2 current, Vector2 target, float dt, bool forecourt)
        {
            var delta = target - current; var distance = delta.Length();
            var position = distance < dt * layout.SpriteSize.X ? target : current + delta / distance * dt * layout.SpriteSize.X;
            return new(position, delta.X < 0, CompanionPose.Walk, false);
        }
    }
}
