using System.Drawing.Imaging;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using LauncherUiSmoke;
using static IntegrationFixture;

internal static class NativeExitChecks
{
    internal static unsafe void Run(IntegrationFixture f)
    {
        var library = f.New("Ui.CompanionHomeStyleLibrary", Path.GetFullPath("assets"));
        var artwork = f.New("Ui.CompanionNapArtwork", Path.GetFullPath("assets"));
        object?[] lookup = ["Wink", "Default", null];
        f.Check((bool)artwork.GetType().GetMethod("TryGet")!.Invoke(artwork, lookup)!, "Approved nap artwork loads.");
        var sheet = lookup[2]!;
        var painterType = f.Type("Ui.CompanionHomeRenderer");
        var homeDraw = painterType.GetMethod("DrawHouse", Static)!;
        var indoorScale = (float)f.Type("Ui.CompanionHomeRoutine").GetField("IndoorScale", Static)!.GetRawConstantValue()!;
        f.Check(indoorScale > 0 && indoorScale < .558f, "Indoor art uses the smaller shared cap before adapting to each measured doorway.");
        var variants = f.New("Ui.CompanionVariantLibrary", Path.GetFullPath("assets"));
        var authoredLook = Call(variants, "Find", "Wink", "Look1")!;
        f.Check(authoredLook != null, "The authored nap path has an actual selected look.");
        var seasonal = RuntimeHelpers.GetUninitializedObject(f.Type("Ui.SeasonalCompanionService"));
        var themeType = f.Type("Ui.SeasonalCompanionTheme");
        // Bows remain the independent attachment path; hats now live in complete costume art.
        Set(seasonal, "snapshot", f.New("Ui.SeasonalCompanionSnapshot", Enum.Parse(themeType, "Valentione"),
            Enum.Parse(f.Type("Ui.SeasonalEvidence"), "Manual"), "Synthetic appearance fixture",
            Activator.CreateInstance(typeof(ImmutableArray<>).MakeGenericType(themeType)), null));
        var homes = new[] { "Default", "CrystalPod", "CloudCushion", "LanternHut", "AllSaintsWake", "Starlight", "Heavensturn", "Valentione", "LittleLadies", "HatchingTide", "MakeItRain", "MoonfireFaire", "TheRising" };
        var context = ImGui.CreateContext();
        try
        {
            var io = ImGui.GetIO(); io.IniFilename = null; io.LogFilename = null; io.DeltaTime = 1f / 60;
            io.Fonts.AddFontDefault(); io.Fonts.Build(); var renderer = new SoftwareRenderer();
            var textures = new Dictionary<string, ulong>();
            foreach (var file in new[] { "companion-house.png", "homes-standard.png", "homes-holiday.png", (string)Get(sheet, "File"), "seasonal-accessories.png", "wink-variants.png" })
            {
                using var bitmap = new Bitmap(Path.Combine("assets", file));
                var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var bytes = new byte[bitmap.Width * bitmap.Height * 4];
                try { for (var y = 0; y < bitmap.Height; y++) Marshal.Copy(bits.Scan0 + y * bits.Stride, bytes, y * bitmap.Width * 4, bitmap.Width * 4); }
                finally { bitmap.UnlockBits(bits); }
                for (var i = 0; i < bytes.Length; i += 4) (bytes[i], bytes[i + 2]) = (bytes[i + 2], bytes[i]);
                var id = (ulong)textures.Count + 2; textures.Add(file, id); renderer.AddTexture(id, bytes, bitmap.Width, bitmap.Height);
            }
            object Texture(Type type, ulong handle) => TextureProxy.Create(type, (method, _) => method.Name switch
            {
                "get_Handle" => new ImTextureID(handle), "get_Width" or "get_Height" => 1280,
                "GetWrapOrDefault" => Texture(method.ReturnType, handle),
                _ => throw new InvalidOperationException("Unexpected texture stub member."),
            });
            f.Type("Services").GetProperty("Textures", Static)!.SetValue(null, TextureProxy.Create(typeof(ITextureProvider), (method, values) =>
            {
                if (method.Name != "GetFromFile") throw new InvalidOperationException("Unexpected service access.");
                return Texture(method.ReturnType, textures[Path.GetFileName(values[0]!.ToString()!)]);
            }));
            var output = Path.GetFullPath(".local/visualharness/companion-home-integration/exits"); Directory.CreateDirectory(output);
            foreach (var size in new[] { 96f, 192f })
            foreach (var mirrored in new[] { false, true })
            {
                var tile = new Vector2(size * 3.65f + 24, size * 1.65f + 48);
                io.DisplaySize = tile * new Vector2(4, homes.Length);
                ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                for (var row = 0; row < homes.Length; row++)
                {
                    var config = f.New("Configuration"); Set(config, "CompanionHolidayHomeEnabled", false);
                    var companion = f.Companion(config, library); Set(companion, "houseMirrored", mirrored);
                    Set(companion, "napArtwork", artwork); Set(companion, "displayAvatar", "Wink");
                    Set(companion, "homeVisible", true); Set(companion, "seasonalVisible", true);
                    Set(companion, "seasonal", seasonal);
                    Set(companion, "seasonalArtwork", f.New("Ui.SeasonalCompanionRenderer", Path.GetFullPath("assets")));
                    var routine = Field(companion, "homeRoutine"); var sprite = new Vector2(size);
                    var viewport = new Vector2(size * 8, size * 5);
                    var style = Call(library, "GetHome", homes[row])!;
                    var frame = Call(style, "Place", new Vector2(size * 3, size), new Vector2(size * 4.3f, size * 2.3f), mirrored)!;
                    var minimum = (Vector2)Get(frame, "Minimum"); var maximum = (Vector2)Get(frame, "Maximum");
                    var house = f.New("Ui.CompanionHomeRect", minimum, maximum);
                    var layout = f.New("Ui.CompanionLayout", new Vector2(4), new Vector2(viewport.X - 4, maximum.Y + 4 + size * .1f), sprite);
                    var destination = new Vector2(mirrored ? minimum.X - size * 1.6f - 4 : maximum.X + size * .6f + 4, maximum.Y + 4 - size * .9f);
                    Set(companion, "homeStyleFrame", frame);
                    Call(companion, "PrepareHomeNavigation", Array.CreateInstance(f.Type("Ui.CompanionSurface"), 0), layout, house,
                        Vector2.Zero, viewport, f.New("Ui.CompanionPhysicsOptions", true, false, true, true, true));
                    var delegateType = typeof(Func<,,,,>).MakeGenericType(typeof(Vector2), typeof(Vector2), typeof(float), typeof(bool), f.Type("Ui.CompanionHomeTravel"));
                    var navigator = companion.GetType().GetMethod("NavigateHome", Instance)!.CreateDelegate(delegateType, companion);
                    // Seed only the already-sleeping boundary. All departure states and travel use production code.
                    Call(routine, "Update", 0f, true, false, destination, mirrored, layout, house, mirrored, 15f, 5f,
                        Vector2.Zero, viewport, true, frame, navigator);
                    // Let the real entry boundary choose this style's adaptive indoor scale.
                    Set(routine, "<State>k__BackingField", Enum.Parse(f.Type("Ui.CompanionHomeState"), "Approaching"));
                    Set(routine, "approachLeg", 1);
                    Set(routine, "<Position>k__BackingField", Call(frame, "OutsidePosition", sprite, 4f, true));
                    Call(routine, "Update", 1f / 60, true, false, destination, mirrored, layout, house, mirrored, 15f, 5f,
                        Vector2.Zero, viewport, true, frame, navigator);
                    f.Check(Get(routine, "State").ToString() == "Entering", "The actual front threshold initializes the adaptive scale.");
                    var targetIndoorScale = (float)Get(routine, "TargetIndoorScale");
                    var opening = (Vector2)Get(frame, "OpeningMaximum") - (Vector2)Get(frame, "OpeningMinimum");
                    f.Check(targetIndoorScale > 0 && targetIndoorScale <= indoorScale
                        && targetIndoorScale * size <= opening.X * .78f + .001f && targetIndoorScale * size <= opening.Y * .75f + .001f,
                        "The actual indoor target obeys the smaller cap and fits this measured doorway before departure.");
                    Set(routine, "<State>k__BackingField", Enum.Parse(f.Type("Ui.CompanionHomeState"), "Sleeping"));
                    Set(routine, "<Position>k__BackingField", Call(frame, "BedPosition", sprite));
                    Set(routine, "<ArtScale>k__BackingField", targetIndoorScale); Set(routine, "returnDestination", destination);
                    Call(routine, "BeginLeaving", layout, house, mirrored, frame);
                    var threshold = (Vector2)Field(routine, "leaveTarget");
                    var clearance = (Vector2)Field(routine, "leaveClearance");
                    var expectedClearance = Vector2.Clamp(threshold + new Vector2((mirrored ? -1 : 1) * size * .35f, 0), Vector2.Zero, viewport - sprite);
                    f.Check(Vector2.Distance(clearance, expectedClearance) < .0001f, "The outward step is exactly .35 logical cells, clamped to the viewport.");
                    var samples = new object?[4]; var completed = false;
                    for (var tick = 0; tick < 3000; tick++)
                    {
                        Call(routine, "Update", 1f / 60, true, false, destination, mirrored, layout, house, mirrored, 15f, 5f,
                            Vector2.Zero, viewport, true, frame, navigator);
                        var state = Get(routine, "State").ToString(); var scale = (float)Get(routine, "ArtScale");
                        var leg = (int)Field(routine, "approachLeg"); var seconds = (float)Field(routine, "stateTime");
                        if (state == "Leaving")
                        {
                            f.Check((bool)Get(routine, "UsesNapArt") == (leg < 3), "Nap art covers door exit, outward step and growth; ordinary travel resumes afterward.");
                            f.Check((bool)Get(routine, "InFrontOfHome") == (leg >= 1), "Only the door exit retains the wall mask.");
                            f.Check((bool)Get(routine, "AccessoriesVisible") == (leg >= 2), "Accessories stay hidden through the outward step and return at growth.");
                            if (leg < 2) f.Check(Math.Abs(scale - targetIndoorScale) < .00001f, "The visitor stays at its adaptive indoor scale through the door and outward step.");
                            if (leg == 1)
                            {
                                var position = (Vector2)Get(routine, "Position");
                                f.Check(Math.Abs(position.Y - threshold.Y) < .0001f && position.X >= Math.Min(threshold.X, clearance.X) - .0001f
                                    && position.X <= Math.Max(threshold.X, clearance.X) + .0001f, "The outward step stays on its ground lane.");
                            }
                            if (leg == 2) f.Check(Vector2.Distance((Vector2)Get(routine, "Position"), clearance) < .0001f,
                                "Growth remains fixed at the outside clearance point.");
                            if (leg == 3) f.Check(Math.Abs(scale - 1) < .00001f, "The ordinary return walk has full scale.");
                            if (leg == 0 && seconds >= .7f) Capture(0);
                            if (leg == 1 && seconds >= .35f) Capture(1);
                            if (leg == 2 && seconds >= .4f) Capture(2);
                            if (leg == 2 && seconds >= .78f) Capture(3);
                        }
                        if (state == "Roaming") { completed = true; break; }
                    }
                    f.Check(completed && Vector2.Distance((Vector2)Get(routine, "Position"), destination) < .1f,
                        "The actual exit finishes growth then returns through grounded navigation.");
                    f.Check(samples.All(s => s != null), "All four actual exit stages were reached.");
                    for (var column = 0; column < 4; column++)
                    {
                        var cell = tile * new Vector2(column, row); var offset = cell + new Vector2(size * 1.15f + 12, 28) - minimum;
                        var shiftedFrame = f.New("Ui.CompanionHomeStyleFrame", style, minimum + offset, maximum + offset, mirrored);
                        var shiftedHouse = f.New("Ui.CompanionHomeRect", minimum + offset, maximum + offset);
                        var sample = samples[column]!; Set(sample, "<Position>k__BackingField", (Vector2)Get(sample, "Position") + offset);
                        Set(companion, "homeRoutine", sample); Set(companion, "homeStyleFrame", shiftedFrame);
                        draw.PushClipRect(cell, cell + tile, true);
                        draw.AddText(cell + new Vector2(7, 5), 0xFFFFFFFF, homes[row] + " / " + new[] { "Small door exit", "Small outward step", "Grow in front", "End growth in front" }[column]);
                        var texture = new ImTextureID(textures[(string)Get(frame, "File")]);
                        homeDraw.Invoke(null, [draw, texture, shiftedHouse, mirrored, shiftedFrame]);
                        var result = DrawActual(false, true);
                        f.Check(result.Item1.X >= cell.X && result.Item1.Y >= cell.Y + 20 && result.Item2.X <= cell.X + tile.X && result.Item2.Y <= cell.Y + tile.Y,
                            "Exit render stays inside its complete visible tile with a label gutter.");
                        draw.PopClipRect();

                        // The selected-look branch and disabled accessory setting use the same actual runtime methods.
                        // Clip these extra proofs away from the visible contact sheet.
                        draw.PushClipRect(Vector2.Zero, Vector2.Zero, true);
                        DrawActual(false, false); DrawActual(true, true); DrawActual(true, false);
                        draw.PopClipRect();

                        (Vector2, Vector2) DrawActual(bool selectedLook, bool accessoriesEnabled)
                        {
                            Set(companion, "currentLook", selectedLook ? authoredLook : null);
                            Set(companion, "seasonalVisible", accessoriesEnabled);
                            var firstIndex = draw.IdxBuffer.Size;
                            object?[] arguments = [draw, layout, null];
                            // Roaming.Draw owns this splitter around the body/attachment passes.
                            // Directly invoking its nested painter must reproduce that caller contract.
                            draw.ChannelsSplit(2); draw.ChannelsSetCurrent(1);
                            try { f.Check((bool)companion.GetType().GetMethod("DrawNap", Instance)!.Invoke(companion, arguments)!, "Actual nap draw produces art."); }
                            finally { draw.ChannelsMerge(); }
                            var drawnTextures = new List<ulong>();
                            for (var i = 0; i < draw.CmdBuffer.Size; i++)
                            {
                                var command = draw.CmdBuffer[i];
                                if (command.ElemCount > 0 && command.IdxOffset + command.ElemCount > firstIndex)
                                    drawnTextures.Add(command.TextureId.Handle);
                            }
                            var houseId = textures[(string)Get(frame, "File")];
                            f.Check(drawnTextures.Contains(houseId) == !(bool)Get(sample, "InFrontOfHome"),
                                "Actual runtime redraws walls only inside; foreground growth cannot be clipped by a later house pass.");
                            f.Check(drawnTextures.Contains(textures["seasonal-accessories.png"]) == (accessoriesEnabled && (bool)Get(sample, "AccessoriesVisible")),
                                "Both actual nap paths hide indoor accessories and restore them outside only when enabled.");
                            return ((Vector2, Vector2))arguments[2]!;
                        }
                    }
                    void Capture(int stage) => samples[stage] ??= typeof(object).GetMethod("MemberwiseClone", Instance)!.Invoke(routine, null)!;
                }
                ImGui.Render(); renderer.Save(ImGui.GetDrawData(), Path.Combine(output, $"actual-exits-{size:0}-{(mirrored ? "left" : "right")}.png"));
            }
        }
        finally { ImGui.DestroyContext(context); }
    }
}
