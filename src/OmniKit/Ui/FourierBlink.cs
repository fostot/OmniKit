namespace OmniKit.Ui;

/// <summary>One cached low-pass blink curve; rendering performs only a bounded interpolation.</summary>
internal static class FourierBlink
{
    internal const int SampleCount = 256;
    internal const int CloseSample = 56;
    internal const int OpenSample = 96;
    internal const float Start = .3f;
    internal const float Duration = .3f;
    private static readonly float[] lookup = BuildLookup();
    private static readonly float[] tangents = BuildTangents();
    internal static ReadOnlySpan<float> Samples => lookup;

    internal static float Amount(float seconds, float interval, bool enabled)
    {
        if (!enabled || !float.IsFinite(seconds)) return 0;
        interval = Math.Clamp(float.IsFinite(interval) ? interval : 3, 1, 10);
        var active = (double)Math.Max(0, seconds) % interval - Start;
        // The periodic DFT must never leak its low-amplitude ringing into the
        // quiet interval. These also make the wrap seam exactly open.
        if (active <= 0 || active >= Duration) return 0;
        var index = active / Duration * SampleCount;
        var first = Math.Clamp((int)index, 0, SampleCount - 1);
        var t = (float)(index - first); var t2 = t * t; var t3 = t2 * t;
        var a = lookup[first]; var b = lookup[first + 1];
        // Cached monotone Hermite tangents remove linear-lookup velocity joins.
        var value = (2 * t3 - 3 * t2 + 1) * a + (t3 - 2 * t2 + t) * tangents[first]
            + (-2 * t3 + 3 * t2) * b + (t3 - t2) * tangents[first + 1];
        return Math.Clamp(value, Math.Min(a, b), Math.Max(a, b));
    }

    private static float[] BuildLookup()
    {
        const int harmonics = 16;
        var source = new double[SampleCount];
        for (var i = 0; i < source.Length; i++)
            source[i] = i < CloseSample ? Ease((double)i / CloseSample)
                : i <= OpenSample ? 1 : 1 - Ease((double)(i - OpenSample) / (SampleCount - OpenSample));
        Span<double> cosine = stackalloc double[harmonics + 1];
        Span<double> sine = stackalloc double[harmonics + 1];
        // One DFT at type initialization. Retain low frequencies, with a cosine
        // taper over the last four harmonics instead of a sharp spectral cutoff.
        for (var k = 0; k <= harmonics; k++)
        {
            var weight = k <= 12 ? 1 : .5 * (1 + Math.Cos(Math.PI * (k - 12) / 4));
            double real = 0, imaginary = 0;
            for (var n = 0; n < SampleCount; n++)
            {
                var angle = Math.Tau * k * n / SampleCount;
                real += source[n] * Math.Cos(angle);
                imaginary += source[n] * Math.Sin(angle);
            }
            cosine[k] = real / SampleCount * weight;
            sine[k] = imaginary / SampleCount * weight;
        }
        var filtered = new double[SampleCount + 1];
        for (var n = 0; n <= SampleCount; n++)
        {
            var value = cosine[0];
            for (var k = 1; k <= harmonics; k++)
            {
                var angle = Math.Tau * k * n / SampleCount;
                value += 2 * (cosine[k] * Math.Cos(angle) + sine[k] * Math.Sin(angle));
            }
            filtered[n] = value;
        }
        var result = new float[SampleCount + 1];
        // Normalize each ramp at its actual filtered endpoints, then project
        // tiny Fourier ripples onto monotone closure/opening. Full closure and
        // zero closure are deliberate semantic constraints, not approximations.
        for (var i = 1; i < CloseSample; i++)
            result[i] = Math.Max(result[i - 1], (float)Math.Clamp(
                (filtered[i] - filtered[0]) / (filtered[CloseSample] - filtered[0]), 0, 1));
        for (var i = CloseSample; i <= OpenSample; i++) result[i] = 1;
        for (var i = OpenSample + 1; i < SampleCount; i++)
            result[i] = Math.Min(result[i - 1], (float)Math.Clamp(
                (filtered[i] - filtered[SampleCount]) / (filtered[OpenSample] - filtered[SampleCount]), 0, 1));
        return result;
    }

    private static float[] BuildTangents()
    {
        var result = new float[SampleCount + 1];
        for (var i = 1; i < SampleCount; i++)
        {
            var before = lookup[i] - lookup[i - 1];
            var after = lookup[i + 1] - lookup[i];
            if (before * after > 0) result[i] = 2 * before * after / (before + after);
        }
        // Open endpoints and the full-closure plateau have zero velocity.
        return result;
    }

    private static double Ease(double amount) => amount * amount * amount * (amount * (amount * 6 - 15) + 10);
}
