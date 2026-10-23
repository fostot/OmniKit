using OmniKit.Ui;

internal static class FourierBlinkChecks
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); checks++; }
        var curve = FourierBlink.Samples;

        Check(curve.Length == 257 && curve[0] == 0 && curve[^1] == 0, "Cached curve has exact open endpoints.");
        for (var i = 0; i < curve.Length; i++)
        {
            Check(float.IsFinite(curve[i]) && curve[i] is >= 0 and <= 1, "Every cached closure is finite and bounded.");
            if (i > 0 && i <= FourierBlink.CloseSample) Check(curve[i] >= curve[i - 1], "Closing is monotone without Fourier ringing reversals.");
            if (i >= FourierBlink.CloseSample && i <= FourierBlink.OpenSample) Check(curve[i] == 1, "The entire hold is exactly closed.");
            if (i > FourierBlink.OpenSample) Check(curve[i] <= curve[i - 1], "Opening is monotone without Fourier ringing reversals.");
        }
        Check(FourierBlink.CloseSample < FourierBlink.SampleCount - FourierBlink.OpenSample,
            "The eyelid closes faster than it reopens.");
        foreach (var interval in new[] { 1f, 3f, 10f })
        foreach (var rate in new[] { 30, 60, 144, 240 })
        {
            var seenClosed = false;
            for (var frame = 0; frame <= interval * rate * 2; frame++)
            {
                var seconds = (float)frame / rate;
                var value = FourierBlink.Amount(seconds, interval, true);
                Check(float.IsFinite(value) && value is >= 0 and <= 1, "Frame-sampled closure remains bounded.");
                Check(FourierBlink.Amount(seconds, interval, false) == 0, "Disabled animations or blinking hold the eye open.");
                seenClosed |= value == 1;
                var phase = (double)seconds % interval;
                if (phase < FourierBlink.Start || phase > FourierBlink.Start + (double)FourierBlink.Duration)
                    Check(value == 0, "The long quiet interval is exactly open at all frame rates.");
            }
            Check(seenClosed, "Even a 30Hz display samples the short full-closure hold.");
        }
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Check(FourierBlink.Amount(invalid, 3, true) == 0, "Invalid time is safely open.");
            Check(FourierBlink.Amount(.35f, invalid, true) == FourierBlink.Amount(.35f, 3, true), "Invalid interval uses the existing three-second default.");
        }
        Check(FourierBlink.Amount(-100, 3, true) == 0 && FourierBlink.Amount(0, 3, true) == 0,
            "Negative time and initialization are open.");
        Check(FourierBlink.Amount(.35f, .2f, true) == FourierBlink.Amount(.35f, 1, true)
            && FourierBlink.Amount(.35f, 99, true) == FourierBlink.Amount(.35f, 10, true), "Intervals use the existing 1..10 range.");
        foreach (var t in new[] { 0f, FourierBlink.Start, FourierBlink.Start + FourierBlink.Duration, 1f, 3f, 10f })
        {
            Check(FourierBlink.Amount(t, 1, true) == 0, "Open seam and inactive endpoints are exact.");
            Check(Math.Abs(FourierBlink.Amount(t - .00001f, 1, true) - FourierBlink.Amount(t + .00001f, 1, true)) < .0001f,
                "The open seam is continuous on both sides.");
        }
        const float derivativeStep = .0001f;
        float Speed(float at) => Math.Abs(FourierBlink.Amount(at + derivativeStep, 3, true)
            - FourierBlink.Amount(at - derivativeStep, 3, true)) / (2 * derivativeStep);
        var edgeSlope = Math.Max(Speed(FourierBlink.Start), Speed(FourierBlink.Start + FourierBlink.Duration));
        var holdSlope = Math.Max(Speed(FourierBlink.Start + FourierBlink.Duration * FourierBlink.CloseSample / FourierBlink.SampleCount),
            Speed(FourierBlink.Start + FourierBlink.Duration * FourierBlink.OpenSample / FourierBlink.SampleCount));
        Check(edgeSlope < .05f && holdSlope < .05f, $"Eyelid velocity eases near open and closed states instead of snapping at the old linear corners: {edgeSlope}, {holdSlope}.");
        var previous = 0f;
        for (var i = 0; i <= 3000; i++)
        {
            var time = FourierBlink.Start + FourierBlink.Duration * i / 3000;
            var value = FourierBlink.Amount(time, 3, true);
            var sample = i * FourierBlink.SampleCount / 3000f;
            if (sample <= FourierBlink.CloseSample) Check(value + .000001f >= previous, "Interpolation never reverses the closing ramp.");
            if (sample > FourierBlink.OpenSample) Check(value <= previous + .000001f, "Interpolation never reverses the opening ramp.");
            Check(value is >= 0 and <= 1, "Hermite interpolation never overshoots the physical eyelid bounds.");
            previous = value;
        }
        for (var i = 0; i < 1000; i++) _ = FourierBlink.Amount(i / 144f, 3, true);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        float checksum = 0;
        for (var i = 0; i < 100_000; i++) checksum += FourierBlink.Amount(i / 144f, 3, true);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(bytes == 0 && checksum > 0, "Runtime lookup allocates nothing and executes no DFT work.");
        return checks;
    }
}
