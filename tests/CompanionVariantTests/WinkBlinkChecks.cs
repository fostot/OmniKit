using System.Numerics;
using Dalamud.Bindings.ImGui;
using LauncherUiSmoke;
using OmniKit.Ui;

internal static class WinkBlinkChecks
{
    internal static int Run(CompanionVariantLibrary library, SoftwareRenderer painter, Dictionary<string, ulong> textures, string output)
    {
        var checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        var looks = library.Looks.Where(v => v.Avatar == "Wink").ToArray();
        Check(looks.Length == 4, "All four Wink looks load.");
        foreach (var size in new[] { 96f, 192f }) foreach (var mirrored in new[] { false, true })
        {
            var tile = new Vector2(size + 24, size + 44); var io = ImGui.GetIO(); io.DisplaySize = new(tile.X * 5, tile.Y * 4 + 24);
            ImGui.NewFrame(); var draw = ImGui.GetForegroundDrawList(); var affected = new List<RectangleF>();
            for (var row = 0; row < looks.Length; row++)
            {
                var look = looks[row]; var normal = look.Normal; var closed = look[CompanionVariantPose.ClosedBlink];
                draw.AddText(new(10, row * tile.Y + 10), 0xFFFFFFFF, look.Label);
                for (var step = 0; step <= 4; step++)
                {
                    var position = new Vector2(step * tile.X + 12, row * tile.Y + 32);
                    var quad = look.NormalAtlas.GetFloat(.23f, mirrored, position, new(size));
                    draw.AddImageQuad(new(textures[normal.File]), quad.A, quad.B, quad.C, quad.D, quad.UvA, quad.UvB, quad.UvC, quad.UvD);
                    var first = draw.VtxBuffer.Size; var body = Enumerable.Range(first - 4, 4).Select(i => draw.VtxBuffer[i]).ToArray();
                    CompanionVariantRenderer.DrawWinkBlink(draw, new(textures[closed.File]), look, quad, step / 4f, 0xFFFFFFFF);
                    for (var i = 0; i < 4; i++)
                    {
                        var actual = draw.VtxBuffer[first - 4 + i];
                        Check(actual.Pos == body[i].Pos && actual.Uv == body[i].Uv && actual.Col == body[i].Col, "Blink never modifies the original neutral-body vertices or UVs.");
                    }
                    Check(step == 0 ? draw.VtxBuffer.Size == first : draw.VtxBuffer.Size > first, "Open blink emits nothing; closing emits an eyelid.");
                    var b = closed.Body;
                    var patchMin = new Vector2(b.X + (b.Z - b.X) * .40f, b.Y + (b.W - b.Y) * .40f) / closed.SheetSize;
                    var patchMax = new Vector2(b.X + (b.Z - b.X) * .82f, b.Y + (b.W - b.Y) * .51f) / closed.SheetSize;
                    var lidVertices = new List<Vector2>();
                    for (var c = 0; c < draw.CmdBuffer.Size; c++)
                    {
                        var command = draw.CmdBuffer[c];
                        if (command.TextureId.Handle != textures[closed.File]) continue;
                        for (var n = 0u; n < command.ElemCount; n++)
                        {
                            var index = (int)(draw.IdxBuffer[(int)(command.IdxOffset + n)] + command.VtxOffset);
                            if (index < first) continue;
                            var vertex = draw.VtxBuffer[index]; lidVertices.Add(vertex.Pos);
                            Check(vertex.Uv.X >= patchMin.X - .00001f && vertex.Uv.Y >= patchMin.Y - .00001f
                                && vertex.Uv.X <= patchMax.X + .00001f && vertex.Uv.Y <= patchMax.Y + .00001f, "Every lid UV samples this look's own closed forehead strip.");
                        }
                    }
                    var eye = look.EyeEllipse!.Value; var r = normal.Bounds;
                    Vector2 Map(float x, float y) => quad.A + (quad.B - quad.A) * ((x - r.X) / (r.Z - r.X)) + (quad.D - quad.A) * ((y - r.Y) / (r.W - r.Y));
                    var corners = new[] { Map(eye.X-eye.Z-3,eye.Y-eye.W-3),Map(eye.X+eye.Z+3,eye.Y-eye.W-3),Map(eye.X-eye.Z-3,eye.Y+eye.W+3),Map(eye.X+eye.Z+3,eye.Y+eye.W+3) };
                    var minimum = corners.Aggregate(Vector2.Min) - new Vector2(2); var maximum = corners.Aggregate(Vector2.Max) + new Vector2(2);
                    Check(lidVertices.All(p => p.X >= minimum.X && p.X <= maximum.X && p.Y >= minimum.Y && p.Y <= maximum.Y), "Lid geometry stays local to the measured eye.");
                    if (step == 0) affected.Add(new(minimum.X-position.X,minimum.Y-position.Y,maximum.X-minimum.X,maximum.Y-minimum.Y));
                }
            }
            ImGui.Render(); var path = Path.Combine(output, $"wink-blink-{size:0}-{(mirrored ? "left" : "right")}.png");
            painter.Save(ImGui.GetDrawData(), path);
            // Re-render each stage at identical coordinates: translated gallery columns can differ
            // by a few rounding levels in the software triangle interpolator.
            for (var row = 0; row < 4; row++)
            {
                Bitmap? baseline = null;
                try
                {
                    for (var step = 0; step <= 4; step++)
                    {
                        io.DisplaySize = new(size+24); ImGui.NewFrame(); var sampleDraw=ImGui.GetForegroundDrawList();
                        var look=looks[row];var normal=look.Normal;var closed=look[CompanionVariantPose.ClosedBlink];
                        var quad=look.NormalAtlas.GetFloat(.23f,mirrored,new(12),new(size));
                        sampleDraw.AddImageQuad(new(textures[normal.File]),quad.A,quad.B,quad.C,quad.D,quad.UvA,quad.UvB,quad.UvC,quad.UvD);
                        CompanionVariantRenderer.DrawWinkBlink(sampleDraw,new(textures[closed.File]),look,quad,step/4f,0xFFFFFFFF);
                        ImGui.Render();var samplePath=Path.Combine(output,"wink-blink-sample.png");painter.Save(ImGui.GetDrawData(),samplePath);
                        using var sample=new Bitmap(samplePath);
                        if(step==0){baseline=new Bitmap(sample);continue;}
                        var unchanged=true;
                        for(var y=0;y<(int)size&&unchanged;y++)for(var x=0;x<(int)size;x++)
                        {
                            if(affected[row].Contains(x,y))continue;
                            if(baseline!.GetPixel(x+12,y+12)!=sample.GetPixel(x+12,y+12)){unchanged=false;break;}
                        }
                        Check(unchanged,"Actual native pixels outside the eye remain identical across all closure stages.");
                    }
                }
                finally { baseline?.Dispose(); }
            }
        }
        // Non-Wink and non-finite requests are inert; transparent tint also reaches the final arc.
        ImGui.GetIO().DisplaySize = new(256); ImGui.NewFrame(); var invalidDraw = ImGui.GetForegroundDrawList();
        var wink = looks[0]; var invalidQuad = wink.NormalAtlas.GetFloat(0, false, new(20), new(96));
        var start = invalidDraw.VtxBuffer.Size;
        CompanionVariantRenderer.DrawWinkBlink(invalidDraw, new(textures[wink.Normal.File]), wink, invalidQuad, float.NaN, 0xFFFFFFFF);
        CompanionVariantRenderer.DrawWinkBlink(invalidDraw, new(textures[wink.Normal.File]), library.Looks.First(v=>v.Avatar=="Nova"), invalidQuad, 1, 0xFFFFFFFF);
        Check(invalidDraw.VtxBuffer.Size==start,"Invalid closure and another avatar do not draw a lid.");
        CompanionVariantRenderer.DrawWinkBlink(invalidDraw, new(textures[wink.Normal.File]), wink, invalidQuad, 1, 0x00FFFFFF);
        Check(Enumerable.Range(start,invalidDraw.VtxBuffer.Size-start).All(i=>(invalidDraw.VtxBuffer[i].Col>>24)==0),"Lid and arc preserve transparent tint.");
        ImGui.Render();
        return checks;
    }
}
