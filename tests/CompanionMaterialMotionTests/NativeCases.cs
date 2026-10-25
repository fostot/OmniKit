using System.Numerics;
using Dalamud.Bindings.ImGui;

internal static class NativeCases
{
    public static void RunReactions(Fixture f, string? focusedLook = null)
    {
        var io = ImGui.GetIO(); var position = new Vector2(180);
        var maximumArea = 0f; var minimumArea = float.MaxValue; var maximumOffset = 0f;
        var representatives = f.Looks.GroupBy(look => f.Get(f.Get(f.Motion(f.Text(look, "Avatar"), f.Text(look, "Id")), "Profile"), "Material").ToString())
            .Select(group => group.First()).Concat(f.Looks.Where(look => f.Text(look, "Avatar") == "Orbit").Take(1)).Distinct().ToArray();
        var eyeIds = representatives.Select(look => f.Text(look, "Id")).ToHashSet(StringComparer.Ordinal);
        foreach (var look in f.Looks.Where(look => focusedLook == null || f.Text(look, "Id") == focusedLook))
        {
            var avatar = f.Text(look, "Avatar"); var id = f.Text(look, "Id");
            var state = f.Motion(avatar, id); f.Call(state, "SetIntensity", 1.5f);
            foreach (var edge in new[] { 96f, 192f })
            foreach (var mirror in new[] { false, true })
            foreach (var sign in new[] { -1f, 1f })
            foreach (var phase in new[] { .125f, .375f, .625f, .875f })
            {
                f.Call(state, "SetDynamics", sign * .04f, sign * .03f, sign * .025f);
                io.DisplaySize = new(700); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                draw.AddRectFilled(new(4), new(12), 0xFF987654); var sentinel = f.Copy(draw);
                var (vertices, bounds) = f.Draw(look, state, draw, position, edge, mirror, phase);
                f.Check(sentinel.Select((vertex, index) => Same(vertex, draw.VtxBuffer[index])).All(value => value),
                    id + " maximum controls preserve unrelated draw-list prefix geometry.");
                f.Check(vertices.Length > 0 && vertices.All(vertex => float.IsFinite(vertex.Pos.X) && float.IsFinite(vertex.Pos.Y)
                    && vertex.Pos.X >= position.X - edge * .6f && vertex.Pos.X <= position.X + edge * 1.6f
                    && vertex.Pos.Y >= position.Y - edge * .6f && vertex.Pos.Y <= position.Y + edge * 1.6f),
                    id + " maximum-control geometry fits reserved costume/material padding.");
                if (avatar == "Orbit" || (bool)f.Call(state, "AppliesTo", avatar)!)
                    f.Check(vertices.All(vertex => vertex.Pos.X >= bounds.Minimum.X - .001f && vertex.Pos.Y >= bounds.Minimum.Y - .001f
                        && vertex.Pos.X <= bounds.Maximum.X + .001f && vertex.Pos.Y <= bounds.Maximum.Y + .001f),
                        id + $" actual runtime bounds include every maximum-control vertex: edge={edge},mirror={mirror},sign={sign},phase={phase},reported={bounds},actual="
                            + vertices.Select(vertex => vertex.Pos).Aggregate(Vector2.Min) + ".." + vertices.Select(vertex => vertex.Pos).Aggregate(Vector2.Max));
                else
                {
                    var roll = f.Call(f.Get(look, "NormalAtlas"), "GetRoll", phase, mirror, position, new Vector2(edge))!;
                    var center = (Vector2)f.Get(roll, "Center"); var radius = (float)f.Get(roll, "Radius");
                    f.Check((bool)f.Call(state, "UsesRoll", avatar)! && bounds.Minimum == center - new Vector2(radius)
                        && bounds.Maximum == center + new Vector2(radius),
                        id + " unchanged rigid rolling preserves the intentional circular body hit region and excludes transparent crop corners.");
                }
                if (avatar != "Orbit" && (bool)f.Call(state, "AppliesTo", avatar)!)
                {
                    var ratio = CheckMesh(f, vertices, f.Get(look, "Normal"), edge, id, false);
                    minimumArea = Math.Min(minimumArea, ratio); maximumArea = Math.Max(maximumArea, ratio);
                }
                else if (avatar != "Orbit") f.Check(vertices.Length == 4, id + " preserves its single rigid species body.");
                if (avatar == "Orbit")
                {
                    var matrix = (Matrix3x2)f.Call(state, "OrbitMovement", position + new Vector2(edge * .5f), edge * .35f, phase, mirror, true)!;
                    var x = Vector2.TransformNormal(Vector2.UnitX, matrix); var y = Vector2.TransformNormal(Vector2.UnitY, matrix);
                    f.Check(Math.Abs(x.Length() - 1) < .00001f && Math.Abs(y.Length() - 1) < .00001f && Math.Abs(Vector2.Dot(x, y)) < .00001f,
                        id + " maximum controls keep Orbit's matrix orthonormal.");
                    CheckOrbitAssembly(f, look, state, position, edge, mirror, phase, vertices);
                }
                foreach (var normalized in new[] { new Vector2(.2f, .2f), new Vector2(.8f, .2f), new Vector2(.5f, .9f) })
                {
                    var point = position + normalized * edge;
                    if (avatar != "Orbit") maximumOffset = Math.Max(maximumOffset,
                        Vector2.Distance(point, f.Warp(state, avatar, point, position, new(edge), phase, mirror)) / edge);
                }
                ImGui.Render();
                if (phase == .375f && eyeIds.Contains(id))
                {
                    ImGui.NewFrame(); draw = ImGui.GetForegroundDrawList();
                    var closed = f.Draw(look, state, draw, position, edge, mirror, phase, 1).Vertices;
                    f.Check(closed.Length > vertices.Length && vertices.Select((vertex, index) => Same(vertex, closed[index])).All(value => value),
                        id + " maximum-control blinking appends only eyes and preserves the complete body/ring prefix.");
                    CheckEyes(f, look, state, position, edge, mirror, phase, closed[vertices.Length..]);
                    ImGui.Render();
                }
            }
            f.Call(state, "SetIntensity", 0f);
            foreach (var mirror in new[] { false, true })
            foreach (var phase in new[] { .125f, .375f, .625f, .875f })
            {
                if (avatar == "Orbit") f.Check((Matrix3x2)f.Call(state, "OrbitMovement", position, 72f, phase, mirror, true)! == Matrix3x2.Identity,
                    id + " zero intensity produces the exact identity Orbit pose matrix.");
                else foreach (var normalized in new[] { new Vector2(.2f, .2f), new Vector2(.8f, .7f), new Vector2(.5f, .9f) })
                {
                    var point = position + normalized * 192;
                    f.Check(f.Warp(state, avatar, point, position, new(192), phase, mirror) == point,
                        id + " zero intensity leaves mesh/attachment points exactly unchanged, independently of rolling art.");
                }
            }
            var baseline = f.Motion(avatar, id); f.Call(baseline, "SetIntensity", 1.5f);
            f.Call(state, "SetIntensity", 1.5f); f.Call(state, "SetDynamics", 0f, 0f, 0f);
            if (avatar == "Orbit") f.Check((Matrix3x2)f.Call(state, "OrbitMovement", position, 72f, .375f, false, true)!
                == (Matrix3x2)f.Call(baseline, "OrbitMovement", position, 72f, .375f, false, true)!, id + " zero dynamics restores its unchanged Orbit material pose.");
            else f.Check(f.Warp(state, avatar, position + new Vector2(64, 40), position, new(192), .375f, false)
                == f.Warp(baseline, avatar, position + new Vector2(64, 40), position, new(192), .375f, false),
                id + " disabled reactions retain their existing material motion.");
        }
        RenderReactionDiagnostics(f, representatives);
        Console.WriteLine($"Maximum-control mesh area/source ratio {minimumArea:F4}..{maximumArea:F4}; maximum sampled point offset {maximumOffset:P2} of body size.");
    }

    private static void RenderReactionDiagnostics(Fixture f, object[] chosen)
    {
        foreach (var edge in new[] { 96f, 192f })
        foreach (var sign in new[] { -1f, 1f })
        {
            var tile = new Vector2(edge * 2 + 20, edge * 2 + 44); var io = ImGui.GetIO();
            io.DisplaySize = new(tile.X * 4, tile.Y * MathF.Ceiling(chosen.Length / 2f));
            ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
            for (var index = 0; index < chosen.Length; index++)
            for (var side = 0; side < 2; side++)
            {
                var cell = index * 2 + side; var column = cell % 4; var row = cell / 4;
                var state = f.Motion(f.Text(chosen[index], "Avatar"), f.Text(chosen[index], "Id"));
                f.Call(state, "SetIntensity", 1.5f); f.Call(state, "SetDynamics", sign * .04f, sign * .03f, sign * .025f);
                var at = new Vector2(column * tile.X + edge * .5f + 10, row * tile.Y + edge * .5f + 32);
                f.Draw(chosen[index], state, draw, at, edge, side == 1, .375f, 1);
                draw.AddText(new(column * tile.X + 6, row * tile.Y + 6), 0xFFFFFFFF,
                    f.Text(chosen[index], "Avatar") + "/" + f.Get(f.Get(state, "Profile"), "Material") + (side == 1 ? " L" : " R"));
            }
            ImGui.Render(); f.Painter.Save(ImGui.GetDrawData(), Path.Combine(f.Output, $"maximum-controls-{edge:0}-{(sign < 0 ? "negative" : "positive")}.png"));
        }
    }

    public static void Run(Fixture f)
    {
        var io = ImGui.GetIO(); var position = new Vector2(180);
        foreach (var look in f.Looks)
        foreach (var edge in new[] { 96f, 192f })
        foreach (var mirror in new[] { false, true })
        {
            var avatar = f.Text(look, "Avatar"); var id = f.Text(look, "Id"); var state = f.Motion(avatar, id);
            foreach (var phase in new[] { .125f, .375f, .625f, .875f })
            {
                io.DisplaySize = new(700); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                var (vertices, bounds) = f.Draw(look, state, draw, position, edge, mirror, phase);
                f.Check(vertices.Length > 0 && vertices.All(v => float.IsFinite(v.Pos.X) && float.IsFinite(v.Pos.Y)
                    && v.Pos.X >= position.X - edge * .6f && v.Pos.X <= position.X + edge * 1.6f
                    && v.Pos.Y >= position.Y - edge * .6f && v.Pos.Y <= position.Y + edge * 1.6f), id + " native geometry fits reserved material/costume padding.");
                if (avatar == "Orbit" || (bool)f.Call(state, "AppliesTo", avatar)!)
                    f.Check(vertices.All(v => v.Pos.X >= bounds.Minimum.X - .001f && v.Pos.Y >= bounds.Minimum.Y - .001f
                        && v.Pos.X <= bounds.Maximum.X + .001f && v.Pos.Y <= bounds.Maximum.Y + .001f), id + " reported mesh/ring bounds cover actual native geometry.");
                if (avatar != "Orbit")
                {
                    if ((bool)f.Call(state, "AppliesTo", avatar)!) CheckMesh(f, vertices, f.Get(look, "Normal"), edge, id);
                    else f.Check(vertices.Length == 4, id + " preserves the original rigid species quad.");
                }
                ImGui.Render();
            }
            io.DisplaySize = new(700); ImGui.NewFrame(); var foreground = ImGui.GetForegroundDrawList();
            var open = f.Draw(look, state, foreground, position, edge, mirror, .375f).Vertices; ImGui.Render();
            ImGui.NewFrame(); foreground = ImGui.GetForegroundDrawList();
            var closed = f.Draw(look, state, foreground, position, edge, mirror, .375f, 1).Vertices;
            f.Check(closed.Length > open.Length && open.Select((v, n) => Same(v, closed[n])).All(v => v), id + " blinking adds only eye geometry and never swaps, scales or duplicates its neutral body.");
            CheckEyes(f, look, state, position, edge, mirror, .375f, closed[open.Length..]);
            if (avatar == "Orbit") CheckOrbitAssembly(f, look, state, position, edge, mirror, .375f, open);
            ImGui.Render();
        }
        RenderDiagnostics(f);
    }

    private static float CheckMesh(Fixture f, ImDrawVert[] vertices, object source, float edge, string label, bool strictArea = true)
    {
        f.Check(vertices.Length == 256, label + " emits exactly one8x8 mesh.");
        var sign = MathF.Sign(Cross(vertices[1].Pos - vertices[0].Pos, vertices[3].Pos - vertices[0].Pos));
        var clean = sign != 0; var joined = true; var minimum = float.MaxValue; var maximum = 0f; var area = 0f;
        for (var cell = 0; cell < 64; cell++)
        {
            var n = cell * 4; var a = vertices[n].Pos; var b = vertices[n + 1].Pos; var c = vertices[n + 2].Pos; var d = vertices[n + 3].Pos;
            var first = Cross(b - a, c - a) * sign; var second = Cross(c - a, d - a) * sign;
            clean &= first > .01f && second > .01f; minimum = Math.Min(minimum, Math.Min(first, second)); maximum = Math.Max(maximum, Math.Max(first, second));
            area += (first + second) * .5f;
            if (cell % 8 < 7) joined &= b == vertices[n + 4].Pos && c == vertices[n + 7].Pos;
            if (cell < 56) joined &= d == vertices[n + 32].Pos && c == vertices[n + 33].Pos;
        }
        f.Check(clean && joined && maximum / minimum < 8, label + " is connected, has no inverted/folded cells and retains bounded local mass.");
        var rect = (Vector4)f.Get(source, "Bounds"); var scale = edge / (float)f.Get(source, "SourceScale");
        var unwarpedArea = (rect.Z - rect.X) * (rect.W - rect.Y) * scale * scale;
        if (strictArea) f.Check(Math.Abs(area / unwarpedArea - 1) < .08f, label + " total textured-mesh area stays within8% of the unchanged source quad.");
        return area / unwarpedArea;
    }

    private static void CheckOrbitAssembly(Fixture f, object look, object motion, Vector2 position, float edge, bool mirror, float phase, ImDrawVert[] actual)
    {
        var source = f.Get(look, "Normal"); var placement = f.StaticCall("OrbitRingRenderer", "Place", source, source, position, new Vector2(edge), mirror)!;
        var center = (Vector2)f.Get(placement, "Center"); var radius = (float)f.Get(placement, "Radius"); var frame = f.Get(placement, "Frame");
        var matrix = (Matrix3x2)f.Call(motion, "OrbitMovement", center, radius, phase, mirror, true)!;
        var accent = (Vector4)f.StaticCall("ProceduralCompanion", "Palette", "Moonlight")!;
        var tint = ImGui.ColorConvertFloat4ToU32(Vector4.Lerp(Vector4.One, accent, .36f));
        var theme = f.StaticCall("OrbitRingRenderer", "ThemeForLook", f.Text(look, "Choice"));
        var draw = ImGui.GetBackgroundDrawList(); draw.PushClipRect(new(-1), new(-1), true); var first = draw.VtxBuffer.Size;
        f.Call(f.Rings, "DrawPosed", draw, center, radius, accent, false, theme, false, mirror, matrix);
        var a = (Vector2)f.Get(frame, "Position"); var size = (Vector2)f.Get(frame, "Size"); var uv0 = (Vector2)f.Get(frame, "UvStart"); var uv1 = (Vector2)f.Get(frame, "UvEnd");
        draw.AddImageQuad(f.Texture(f.Text(source, "File")), Vector2.Transform(a, matrix), Vector2.Transform(a + new Vector2(size.X, 0), matrix),
            Vector2.Transform(a + size, matrix), Vector2.Transform(a + new Vector2(0, size.Y), matrix), uv0, new(uv1.X, uv0.Y), uv1, new(uv0.X, uv1.Y), tint);
        f.Call(f.Rings, "DrawPosed", draw, center, radius, accent, true, theme, false, mirror, matrix);
        var expected = f.Copy(draw, first); draw.PopClipRect();
        f.Check(actual.Length == expected.Length && actual.Select((v, n) => Same(v, expected[n])).All(v => v),
            f.Text(look, "Id") + " native planet, painted hat and both ring passes use the same material-aware rigid matrix.");
    }

    private static void CheckEyes(Fixture f, object look, object motion, Vector2 position, float edge, bool mirror, float phase, ImDrawVert[] actual)
    {
        var normal = f.Get(look, "Normal"); var avatar = f.Text(look, "Avatar"); var rect = (Vector4)f.Get(normal, "Bounds"); var scale = (float)f.Get(normal, "SourceScale");
        var profile = f.Call(f.Blinks, "Find", f.Text(normal, "File"), rect) ?? f.Call(f.Wardrobe, "FindBlink", f.Text(normal, "File"), rect);
        f.Check(profile != null, "Every changed look has its own exact-source blink profile.");
        object body;
        if (avatar == "Orbit")
        {
            var placed = f.StaticCall("OrbitRingRenderer", "Place", normal, normal, position, new Vector2(edge), mirror)!;
            var initial = f.StaticCall("SeasonalBodyTransform", "FromFrame", rect, scale, f.Get(placed, "Frame"), mirror)!;
            var matrix = (Matrix3x2)f.Call(motion, "OrbitMovement", f.Get(placed, "Center"), f.Get(placed, "Radius"), phase, mirror, true)!;
            body = f.New("SeasonalBodyTransform", rect, scale, Vector2.Transform((Vector2)f.Get(initial, "A"), matrix),
                Vector2.Transform((Vector2)f.Get(initial, "B"), matrix), Vector2.Transform((Vector2)f.Get(initial, "D"), matrix));
        }
        else
        {
            var atlas = f.Get(look, "NormalAtlas"); var rolls = (bool)f.Call(motion, "UsesRoll", avatar)!;
            var quad = f.Call(atlas, rolls ? "GetRoll" : "GetFloat", phase, mirror, position, new Vector2(edge))!;
            body = f.New("SeasonalBodyTransform", rect, scale, f.Get(quad, "A"), f.Get(quad, "B"), f.Get(quad, "D"));
        }
        Func<Vector2, Vector2>? deform = avatar != "Orbit" && (bool)f.Call(motion, "AppliesTo", avatar)!
            ? point => (Vector2)f.Call(motion, "WarpAttachment", avatar, point, position, new Vector2(edge), phase, mirror, true)! : null;
        var palette = (Vector4)f.StaticCall("ProceduralCompanion", "Palette", "Moonlight")!;
        var tint = ImGui.ColorConvertFloat4ToU32(Vector4.Lerp(Vector4.One, palette, .36f));
        var draw = ImGui.GetBackgroundDrawList(); draw.PushClipRect(new(-1), new(-1), true); var first = draw.VtxBuffer.Size;
        f.StaticCall("CompanionBlinkRenderer", "Draw", draw, f.Texture(f.Text(profile!, "ClosedFile")), profile, body, 1f, tint, deform);
        var expected = f.Copy(draw, first); draw.PopClipRect();
        f.Check(actual.Length == expected.Length && actual.Select((v, n) => Vector2.Distance(v.Pos, expected[n].Pos) < .005f
            && v.Col == expected[n].Col && v.Uv == expected[n].Uv).All(v => v), f.Text(look, "Id") + " actual eyes follow the matching material warp/rigid Orbit matrix and same-palette source.");
    }

    private static void RenderDiagnostics(Fixture f)
    {
        string[] ids = ["OceanOpal", "LavenderGeode", "TideGlass", "TwilightVelvet", "PeachParfait", "RainPearl", "Moonfire", "VioletWisp", "OceanMarble", "AmethystSky"];
        var chosen = ids.Select(id => f.Looks.Single(look => f.Text(look, "Id") == id)).ToList();
        chosen.Add(f.Looks.First(look => f.Text(look, "Avatar") == "Ember" && f.Text(look, "Choice") == "HolidayStarlight"));
        chosen.Add(f.Looks.First(look => f.Text(look, "Avatar") == "Puff" && f.Text(look, "Choice") == "CostumeAllSaintsWake"));
        foreach (var edge in new[] { 96f, 192f })
        {
            var states = chosen.Select(look => f.Motion(f.Text(look, "Avatar"), f.Text(look, "Id"))).ToArray();
            var tile = new Vector2(edge * 2 + 20, edge * 2 + 35); var io = ImGui.GetIO();
            for (var step = 0; step < 8; step++)
            {
                io.DisplaySize = new(tile.X * 4, tile.Y * 6); ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList();
                for (var index = 0; index < chosen.Count; index++) for (var side = 0; side < 2; side++)
                {
                    var cell = index * 2 + side; var column = cell % 4; var row = cell / 4;
                    var position = new Vector2(column * tile.X + edge * .5f + 10, row * tile.Y + edge * .5f + 25);
                    f.Draw(chosen[index], states[index], draw, position, edge, side == 1, step / 8f, step is 3 or 4 ? 1 : 0);
                    var choice = f.Text(chosen[index], "Choice");
                    var label = choice.StartsWith("Holiday", StringComparison.Ordinal) || choice.StartsWith("Costume", StringComparison.Ordinal)
                        ? choice[7..] : f.Text(chosen[index], "Id");
                    draw.AddText(new(column * tile.X + 4, row * tile.Y + 4), 0xFFFFFFFF, f.Text(chosen[index], "Avatar") + "/" + label + (side == 1 ? " L" : " R"));
                }
                ImGui.Render(); f.Painter.Save(ImGui.GetDrawData(), Path.Combine(f.Output, $"materials-{edge:0}-{step}.png"));
                foreach (var state in states) for (var tick = 0; tick < 9; tick++) f.Call(state, "Update", 1f / 60, true, true);
            }
        }
    }
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static bool Same(ImDrawVert a, ImDrawVert b) => a.Pos == b.Pos && a.Uv == b.Uv && a.Col == b.Col;
}
