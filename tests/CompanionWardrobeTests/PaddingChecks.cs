using System.Numerics;
using OmniKit.Ui;

internal static class PaddingChecks
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        var sheets = new Dictionary<string, Vector2> { ["synthetic.png"] = new(500) };
        CompanionVariantLibrary.Frame Frame() => new()
        {
            File = "synthetic.png", State = "Neutral", Bounds = [100,130,220,262], BodyBounds = [120,180,200,260],
            Anchor = [160,250], Grip = [160,145], AccessoryAnchor = [160,185], AccessoryHeadWidth = 64,
        };
        var source = Frame();
        Check(CompanionVariantLibrary.ParseFrame(source,100,sheets) == null, "Existing default parsing still rejects full crops outside the original body cell.");
        var padded = CompanionVariantLibrary.ParseFrame(source,100,sheets,CompanionWardrobeLibrary.VisualPaddingFraction);
        Check(padded != null && padded.SourceScale == 100 && padded.Body == new Vector4(120,180,200,260),
            "Wardrobe headroom preserves the measured body size and its source scale.");
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -.01f, .601f })
            Check(CompanionVariantLibrary.ParseFrame(source,100,sheets,value) == null, "Invalid padding is rejected rather than weakening crop validation.");
        foreach (var edge in new[] { 96f,192f }) foreach (var mirrored in new[] { false,true })
        {
            var frame = padded!.GetFrame(Vector2.Zero,new(edge),mirrored);
            Check(frame.Position.Y < 0 && frame.Position.Y >= -edge*.5f,
                "A tall painted hat uses reserved headroom above the logical body cell.");
            Check(Math.Abs(Vector2.Distance(padded.Map(new(120,220),Vector2.Zero,new(edge),mirrored),
                padded.Map(new(200,220),Vector2.Zero,new(edge),mirrored)) - edge*.8f) < .001f,
                "Extra hat pixels do not reduce the body's visible width at either size or facing.");
        }
        var oversized = Frame(); oversized.BodyBounds = [100,180,220,260];
        Check(CompanionVariantLibrary.ParseFrame(oversized,100,sheets,.5f) == null, "Visual padding cannot admit oversized body mass.");
        var outside = Frame(); outside.Bounds = [50,100,270,262];
        Check(CompanionVariantLibrary.ParseFrame(outside,100,sheets,.5f) == null, "The full crop still has a finite reserved-padding boundary.");
        var ordinary = Frame(); ordinary.Bounds = [110,165,210,260]; ordinary.Grip = [160,175];
        Check(CompanionVariantLibrary.ParseFrame(ordinary,100,sheets) is { } before &&
            before == CompanionVariantLibrary.ParseFrame(ordinary,100,sheets,0), "Omitting padding retains exact zero-padding frame behavior.");
        return checks;
    }
}
