using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace LauncherUiSmoke;

/// <summary>Renders actual ImGui draw lists offscreen, without a game, graphics device or window.</summary>
internal sealed unsafe class SoftwareRenderer
{
    private readonly byte[] atlas;
    private readonly int atlasWidth;
    private readonly int atlasHeight;

    public SoftwareRenderer()
    {
        byte* pixels;
        int width, height, bytesPerPixel;
        ImGui.GetIO().Fonts.GetTexDataAsRGBA32(0, &pixels, &width, &height, &bytesPerPixel);
        if (bytesPerPixel != 4) throw new InvalidOperationException("Expected RGBA font atlas.");
        atlasWidth = width;
        atlasHeight = height;
        atlas = new ReadOnlySpan<byte>(pixels, width * height * 4).ToArray();
        ImGui.GetIO().Fonts.SetTexID(0, new ImTextureID(1UL));
    }

    public (int Left, int Top, int Right, int Bottom) Save(ImDrawDataPtr data, string path)
    {
        var width = (int)(data.DisplaySize.X * data.FramebufferScale.X);
        var height = (int)(data.DisplaySize.Y * data.FramebufferScale.Y);
        var image = new byte[checked(width * height * 4)];
        for (var i = 0; i < image.Length; i += 4)
        {
            image[i] = 24; image[i + 1] = 29; image[i + 2] = 38; image[i + 3] = 255;
        }

        for (var listIndex = 0; listIndex < data.CmdListsCount; listIndex++)
        {
            var list = new ImDrawListPtr(data.CmdLists[listIndex]);
            for (var commandIndex = 0; commandIndex < list.CmdBuffer.Size; commandIndex++)
            {
                var command = list.CmdBuffer[commandIndex];
                if (command.UserCallback != null)
                    throw new NotSupportedException("Custom draw callbacks require the game renderer.");
                if (command.TextureId.Handle != 1)
                    throw new NotSupportedException("Only the actual ImGui font atlas is supported by this harness.");
                var clipMin = (new Vector2(command.ClipRect.X, command.ClipRect.Y) - data.DisplayPos) * data.FramebufferScale;
                var clipMax = (new Vector2(command.ClipRect.Z, command.ClipRect.W) - data.DisplayPos) * data.FramebufferScale;
                for (uint element = 0; element + 2 < command.ElemCount; element += 3)
                {
                    var firstIndex = command.IdxOffset + element;
                    var a = list.VtxBuffer[(int)(list.IdxBuffer[(int)firstIndex] + command.VtxOffset)];
                    var b = list.VtxBuffer[(int)(list.IdxBuffer[(int)firstIndex + 1] + command.VtxOffset)];
                    var c = list.VtxBuffer[(int)(list.IdxBuffer[(int)firstIndex + 2] + command.VtxOffset)];
                    a.Pos = (a.Pos - data.DisplayPos) * data.FramebufferScale;
                    b.Pos = (b.Pos - data.DisplayPos) * data.FramebufferScale;
                    c.Pos = (c.Pos - data.DisplayPos) * data.FramebufferScale;
                    DrawTriangle(image, width, height, a, b, c, clipMin, clipMax);
                }
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        WritePng(path, image, width, height);
        var bounds = (Left: width, Top: height, Right: -1, Bottom: -1);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var pixel = (y * width + x) * 4;
            if (image[pixel] == 24 && image[pixel + 1] == 29 && image[pixel + 2] == 38) continue;
            bounds.Left = Math.Min(bounds.Left, x); bounds.Top = Math.Min(bounds.Top, y);
            bounds.Right = Math.Max(bounds.Right, x); bounds.Bottom = Math.Max(bounds.Bottom, y);
        }
        return bounds;
    }

    private void DrawTriangle(byte[] image, int width, int height, ImDrawVert a, ImDrawVert b, ImDrawVert c, Vector2 clipMin, Vector2 clipMax)
    {
        var area = Cross(b.Pos - a.Pos, c.Pos - a.Pos);
        if (Math.Abs(area) < 0.00001f) return;
        if (area < 0) { (b, c) = (c, b); area = -area; }
        var minX = Math.Max(0, (int)MathF.Floor(Math.Max(clipMin.X, Math.Min(a.Pos.X, Math.Min(b.Pos.X, c.Pos.X)))));
        var maxX = Math.Min(width, (int)MathF.Ceiling(Math.Min(clipMax.X, Math.Max(a.Pos.X, Math.Max(b.Pos.X, c.Pos.X)))));
        var minY = Math.Max(0, (int)MathF.Floor(Math.Max(clipMin.Y, Math.Min(a.Pos.Y, Math.Min(b.Pos.Y, c.Pos.Y)))));
        var maxY = Math.Min(height, (int)MathF.Ceiling(Math.Min(clipMax.Y, Math.Max(a.Pos.Y, Math.Max(b.Pos.Y, c.Pos.Y)))));
        for (var y = minY; y < maxY; y++)
        for (var x = minX; x < maxX; x++)
        {
            var point = new Vector2(x + .5f, y + .5f);
            if (point.X < clipMin.X || point.Y < clipMin.Y || point.X >= clipMax.X || point.Y >= clipMax.Y) continue;
            var ea = Cross(b.Pos - point, c.Pos - point);
            var eb = Cross(c.Pos - point, a.Pos - point);
            var ec = Cross(a.Pos - point, b.Pos - point);
            if (!Inside(ea, b.Pos, c.Pos) || !Inside(eb, c.Pos, a.Pos) || !Inside(ec, a.Pos, b.Pos)) continue;
            var wa = ea / area;
            var wb = eb / area;
            var wc = ec / area;
            var uv = a.Uv * wa + b.Uv * wb + c.Uv * wc;
            var tx = Math.Clamp((int)(uv.X * atlasWidth), 0, atlasWidth - 1);
            var ty = Math.Clamp((int)(uv.Y * atlasHeight), 0, atlasHeight - 1);
            var texture = (ty * atlasWidth + tx) * 4;
            var destination = (y * width + x) * 4;
            var alpha = (Channel(a.Col, 24) * wa + Channel(b.Col, 24) * wb + Channel(c.Col, 24) * wc) * atlas[texture + 3] / (255f * 255f);
            for (var channel = 0; channel < 3; channel++)
            {
                var shift = channel * 8;
                var source = (Channel(a.Col, shift) * wa + Channel(b.Col, shift) * wb + Channel(c.Col, shift) * wc) * atlas[texture + channel] / 255f;
                image[destination + channel] = (byte)Math.Clamp((int)(source * alpha + image[destination + channel] * (1 - alpha)), 0, 255);
            }
        }
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static bool Inside(float edge, Vector2 a, Vector2 b) => edge > 0 || (edge == 0 && (a.Y > b.Y || (a.Y == b.Y && a.X < b.X)));
    private static byte Channel(uint color, int shift) => (byte)(color >> shift);

    private static void WritePng(string path, byte[] rgba, int width, int height)
    {
        using var file = File.Create(path);
        file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 6;
        WriteChunk(file, "IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var deflater = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            for (var row = 0; row < height; row++)
            {
                deflater.WriteByte(0);
                deflater.Write(rgba.AsSpan(row * width * 4, width * 4));
            }
        WriteChunk(file, "IDAT"u8, compressed.ToArray());
        WriteChunk(file, "IEND"u8, []);
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(value, data.Length);
        output.Write(value); output.Write(type); output.Write(data);
        uint crc = uint.MaxValue;
        foreach (var octet in type) crc = Crc(crc, octet);
        foreach (var octet in data) crc = Crc(crc, octet);
        BinaryPrimitives.WriteUInt32BigEndian(value, ~crc);
        output.Write(value);
    }

    private static uint Crc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320U);
        return crc;
    }
}
