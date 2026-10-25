using System.Numerics;

namespace OmniKit.Ui;

/// <summary>One identity-wide rear scale, matched to the exact front source before the turn.</summary>
internal static class CompanionHomePoseScale
{
    public static float Correction(float frontWidth, float frontReference, float rearWidth, float rearReference)
    {
        if (!float.IsFinite(frontWidth) || !float.IsFinite(frontReference) || !float.IsFinite(rearWidth)
            || !float.IsFinite(rearReference) || frontWidth <= 0 || frontReference <= 0 || rearWidth <= 0 || rearReference <= 0) return 0;
        var factor = frontWidth / frontReference / (rearWidth / rearReference);
        return float.IsFinite(factor) && factor is >= .25f and <= 2f ? factor : 0;
    }

    public static float NapBodyWidth(string avatar, NapSheet sheet)
    {
        // Largest connected alpha>=128 body in each approved frame-zero source.
        // Exact source metadata gates these measured values; a changed source needs a new review.
        var source = avatar switch
        {
            "Moonblob" => ("companion-naps-abstract.png", 317f, new Vector4(52, 88, 342, 352), 279f),
            "Wink" => ("companion-naps-abstract.png", 353f, new Vector4(48, 390, 392, 670), 323f),
            "Nova" => ("companion-naps-abstract.png", 345f, new Vector4(53, 727, 389, 1031), 324f),
            "Mochi" => ("mochi-poses.png", 335f, new Vector4(23, 1000, 317, 1211), 282f),
            "Puff" => ("puff-poses.png", 336f, new Vector4(12, 1003, 310, 1221), 287f),
            "Ember" => ("ember-poses.png", 353f, new Vector4(40, 959, 313, 1206), 261f),
            "Prism" => ("prism-poses.png", 338f, new Vector4(43, 993, 296, 1200), 242f),
            _ => default
        };
        if (source.Item4 <= 0 || sheet.Avatar != avatar || sheet.File != source.Item1 || sheet.ScaleReference != source.Item2
            || sheet.Frames is not { Length: > 0 } || sheet.Frames[0].Bounds is not { Length: 4 } bounds) return 0;
        return new Vector4(bounds[0], bounds[1], bounds[2], bounds[3]) == source.Item3 ? source.Item4 : 0;
    }
}
