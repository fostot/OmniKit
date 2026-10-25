using System.Globalization;
using System.Numerics;

namespace OmniKit.Ui;

internal readonly record struct CompanionSpeechText(string Text, Vector2 Size);

/// <summary>Fits plain speech without splitting a Unicode text element.</summary>
internal static class CompanionSpeechLayout
{
    public static CompanionSpeechText Fit(string text, float width, float height, Func<string, float, Vector2> measure)
    {
        if (string.IsNullOrEmpty(text) || !float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0)
            return new("", Vector2.Zero);
        bool Fits(Vector2 size) => float.IsFinite(size.X) && float.IsFinite(size.Y)
            && size.X >= 0 && size.Y >= 0 && size.X <= width + .01f && size.Y <= height + .01f;
        var full = measure(text, width);
        if (Fits(full)) return new(text, full);
        const string suffix = "...";
        var minimum = measure(suffix, width);
        if (!Fits(minimum)) return new("", Vector2.Zero);
        var boundaries = StringInfo.ParseCombiningCharacters(text);
        var low = 0; var high = boundaries.Length;
        var best = new CompanionSpeechText(suffix, minimum);
        while (low <= high)
        {
            var count = low + (high - low) / 2;
            var end = count < boundaries.Length ? boundaries[count] : text.Length;
            var candidate = text[..end].TrimEnd() + suffix;
            var size = measure(candidate, width);
            if (Fits(size)) { best = new(candidate, size); low = count + 1; }
            else high = count - 1;
        }
        return best;
    }
}
