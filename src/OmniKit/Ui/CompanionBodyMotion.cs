using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace OmniKit.Ui;

/// <summary>One connected textured body. No duplicated body or independent limb layers.</summary>
internal sealed class CompanionBodyMotion
{
    private float clock;
    private float activity;
    private float intensity = 1f;
    private float reactionCompression;
    private float reactionLean;
    private float reactionTurn;

    public void SetIntensity(float value) => intensity = float.IsFinite(value) ? Math.Clamp(value, 0, 1.5f) : 1f;
    public void SetDynamics(float compression, float lean, float turn)
    {
        reactionCompression = Bounded(compression, .04f);
        reactionLean = Bounded(lean, .03f);
        reactionTurn = Bounded(turn, .025f);
    }
    private static float Bounded(float value, float limit) => float.IsFinite(value) ? Math.Clamp(value, -limit, limit) : 0;
    private bool HasDynamics => reactionCompression != 0 || reactionLean != 0 || reactionTurn != 0;
    private double materialTime;
    private string profileAvatar = "";
    private string? lookId;
    public CompanionMotionProfile Profile { get; private set; } = new(CompanionBodyMaterial.Species);
    public float Activity => activity;
    public static bool Supports(string avatar) => avatar is "Ember" or "Mochi" or "Puff"
        or "Nib" or "Echo" or "Fizz" or "Glim" or "Rune" or "Wink" or "Nova" or "Prism";
    public float LastDrawStrength { get; private set; } = 1;
    public void SetLook(string avatar, string? id)
    {
        if (profileAvatar == avatar && lookId == id) return;
        profileAvatar = avatar; lookId = id;
        Profile = CompanionMotionProfiles.Resolve(avatar, id);
        // Keep time, travel phase and momentum when a look changes.
    }
    public bool AppliesTo(string avatar) => Supports(avatar)
        || avatar == "Moonblob" && profileAvatar == avatar && Profile.Material != CompanionBodyMaterial.Species;
    public bool UsesRoll(string avatar) => avatar == "Moonblob" && (profileAvatar != avatar || Profile.Rolls);
    public Vector2 WarpAttachment(string avatar, Vector2 point, Vector2 position, Vector2 size, float stride, bool mirrored, bool animate)
        => Vector2.Lerp(point, Warp(avatar, point, position, size, stride, mirrored, animate), LastDrawStrength);
    public void Update(float elapsed, bool animate, bool moving)
    {
        var dt = float.IsFinite(elapsed) ? Math.Clamp(elapsed, 0, .05f) : 0;
        if (!animate) return;
        clock = (clock + dt) % MathF.Tau;
        // Fractional material tempos cannot share the species clock's Tau wrap.
        materialTime += dt;
        activity += (moving ? 1 - activity : -activity) * (1 - MathF.Exp(-dt * 9));
    }

    public Vector2 Warp(string avatar, Vector2 point, Vector2 position, Vector2 size, float stride, bool mirrored, bool animate)
    {
        if (!animate || intensity == 0 || size.X <= 0 || size.Y <= 0) return point;
        var profile = profileAvatar == avatar ? Profile : new(CompanionBodyMaterial.Species);
        var warped = profile.Material == CompanionBodyMaterial.Species
            ? WarpSpecies(avatar, point, position, size, stride, mirrored, profile.Tempo)
            : WarpMaterial(profile, point, position, size, stride, mirrored);
        if (HasDynamics && AppliesTo(avatar))
        {
            // Every vertex and attachment shares the same support and reaction field.
            var support = new Vector2(.5f, .9f);
            var p = (warped - position) / size - support;
            var facing = mirrored ? -1 : 1;
            var rigid = profile.Material is CompanionBodyMaterial.Crystal or CompanionBodyMaterial.Stone or CompanionBodyMaterial.Metal
                || profile.Material == CompanionBodyMaterial.Species && avatar is "Rune" or "Prism";
            if (!rigid)
            {
                p.X *= 1 + reactionCompression;
                p.Y /= 1 + reactionCompression;
                p.X += Math.Clamp(-p.Y, 0, 1) * reactionLean * facing;
            }
            var angle = (reactionTurn + (rigid ? reactionLean * .5f : 0)) * facing;
            p = Vector2.Transform(p, Matrix3x2.CreateRotation(angle));
            warped = position + (p + support) * size;
        }
        var strength = profile.Strength * intensity;
        return strength == 1 ? warped : point + (warped - point) * strength;
    }

    private Vector2 WarpSpecies(string avatar, Vector2 point, Vector2 position, Vector2 size, float stride, bool mirrored, float tempo)
    {
        float Pulse(float rate, float phase = 0) => tempo == 1 ? MathF.Sin(clock * rate + phase)
            : (float)Math.Sin(materialTime * tempo * rate + phase);
        stride = float.IsFinite(stride) ? stride - MathF.Floor(stride) : 0;
        var local = (point - position) / size;
        var p = local - new Vector2(.5f, .9f);
        var cycle = stride * MathF.Tau;
        var facing = mirrored ? -1 : 1;
        if (avatar == "Ember")
        {
            var pulse = Pulse(3) * .025f + MathF.Sin(cycle) * .065f * activity;
            p.X *= 1 + pulse;
            p.Y /= 1 + pulse;
            var tip = Math.Clamp(-p.Y, 0, 1);
            p.X += (Pulse(4, tip * 5) * .027f + MathF.Sin(cycle) * .025f * activity * facing) * tip * tip;
            p.Y -= Pulse(5, tip * 2) * .018f * tip;
        }
        else if (avatar == "Mochi")
        {
            var compression = MathF.Cos(cycle) * .105f * activity + Pulse(2) * .012f;
            p.X *= 1 + compression;
            p.Y /= 1 + compression;
            p.X += -p.Y * MathF.Sin(cycle) * .045f * activity * facing;
            // A tiny recoil belongs to the body; its supporting plane never moves.
            p.Y -= Math.Max(0, MathF.Sin(cycle)) * .025f * activity;
        }
        else if (avatar == "Puff")
        {
            var height = Math.Clamp(-p.Y, 0, 1);
            p.X *= 1 + Pulse(2, height * 3) * .025f;
            p.Y *= 1 + Pulse(3) * .012f;
        }
        else if (Supports(avatar))
        {
            var height = Math.Clamp(-p.Y, 0, 1);
            var middle = MathF.Sin(height * MathF.PI);
            var compression = 0f; var sway = 0f;
            switch (avatar)
            {
                case "Nib":
                    // Viscous recoil reaches the ink-drop tip after its lower body.
                    compression = Pulse(2) * .018f + MathF.Sin(cycle) * .055f * activity;
                    sway = (Pulse(3, -height * 4) * .016f
                        + MathF.Sin(cycle - height * 3) * .065f * activity) * height * height;
                    break;
                case "Echo":
                    // A soft travelling curl trails through the ghost's continuous silhouette.
                    compression = Pulse(2) * .012f + MathF.Cos(cycle) * .025f * activity;
                    sway = (Pulse(2, -height * 6) * .025f
                        + MathF.Sin(cycle - height * 4) * .045f * activity) * middle;
                    break;
                case "Fizz":
                    // Bubble pressure alternates with a smaller surface wobble.
                    compression = Pulse(3) * .025f + MathF.Cos(cycle) * .06f * activity;
                    sway = Pulse(4, height * 9) * .016f * middle
                        + MathF.Sin(cycle * 2 + height * 6) * .024f * middle * activity;
                    break;
                case "Glim":
                    // Elastic star points flex with one slower, rounded breathing rhythm.
                    compression = Pulse(2) * .022f + MathF.Sin(cycle) * .045f * activity;
                    sway = MathF.Sin(cycle - height * 5) * .027f * height * activity;
                    break;
                case "Rune":
                    // The stone's weight gives a small grounded compression, never a bounce.
                    compression = Pulse(1) * .006f + (1 - MathF.Cos(cycle * 2)) * .018f * activity;
                    sway = MathF.Sin(cycle) * .014f * height * activity;
                    break;
                case "Wink":
                    compression = Pulse(2) * .018f + MathF.Sin(cycle) * .032f * activity;
                    sway = MathF.Sin(cycle) * .018f * height * activity;
                    break;
                case "Nova":
                    // Two gentle spring pulses per travel cycle suit the little burst.
                    compression = Pulse(3) * .025f + MathF.Sin(cycle * 2) * .052f * activity;
                    sway = MathF.Sin(cycle + height * 7) * .025f * height * activity;
                    break;
                case "Prism":
                    // Restrained shear keeps the faceted body recognisable.
                    compression = Pulse(2) * .012f + MathF.Sin(cycle) * .027f * activity;
                    sway = MathF.Sin(cycle + height * 2) * .024f * height * activity;
                    break;
            }
            p.X *= 1 + compression;
            p.Y /= 1 + compression;
            p.X += sway * facing;
        }
        return position + (p + new Vector2(.5f, .9f)) * size;
    }

    private Vector2 WarpMaterial(CompanionMotionProfile profile, Vector2 point, Vector2 position, Vector2 size, float stride, bool mirrored)
    {
        float Pulse(float rate, float phase = 0) => (float)Math.Sin(materialTime * profile.Tempo * rate + phase);
        stride = float.IsFinite(stride) ? stride - MathF.Floor(stride) : 0;
        var cycle = stride * MathF.Tau;
        var facing = mirrored ? -1 : 1;
        var p = (point - position) / size - new Vector2(.5f, .9f);
        var height = Math.Clamp(-p.Y, 0, 1);
        var middle = MathF.Sin(height * MathF.PI);
        var lag = profile.Lag;
        var compression = 0f; var sway = 0f; var angle = 0f;
        switch (profile.Material)
        {
            case CompanionBodyMaterial.Gel:
                compression = Pulse(1.8f) * .025f + MathF.Cos(cycle) * .11f * activity;
                sway = (Pulse(2.6f, -height * 5 * lag) * .022f
                    + MathF.Sin(cycle - height * 3 * lag) * .055f * activity) * height * height;
                break;
            case CompanionBodyMaterial.Cream:
                // Dense dough compresses at the base and recoils as one soft mass.
                compression = Pulse(1.6f) * .018f + MathF.Cos(cycle) * .085f * activity;
                sway = (Pulse(1.8f, -height * 3 * lag) * .012f
                    + MathF.Sin(cycle - height * 2 * lag) * .032f * activity) * middle;
                break;
            case CompanionBodyMaterial.Fluid:
                compression = Pulse(1.7f) * .022f + MathF.Sin(cycle) * .055f * activity;
                sway = (Pulse(2.3f, -height * 6 * lag) * .025f
                    + MathF.Sin(cycle - height * 4 * lag) * .045f * activity) * middle;
                break;
            case CompanionBodyMaterial.Bubble:
                compression = Pulse(2.1f) * .035f + MathF.Cos(cycle) * .065f * activity;
                sway = (Pulse(3.2f, height * 8 * lag) * .016f
                    + MathF.Sin(cycle * 2 + height * 6 * lag) * .025f * activity) * middle;
                break;
            case CompanionBodyMaterial.Cloud:
                // Broad, overlapping swells billow instead of shaking the face.
                compression = Pulse(1.4f) * .025f + MathF.Sin(cycle) * .035f * activity;
                sway = (Pulse(1.9f, -height * 3 * lag) * .03f
                    + MathF.Sin(cycle - height * 2 * lag) * .035f * activity) * middle;
                break;
            case CompanionBodyMaterial.Wisp:
                compression = Pulse(1.5f) * .018f + MathF.Cos(cycle) * .025f * activity;
                sway = (Pulse(2.1f, -height * 5 * lag) * .033f
                    + MathF.Sin(cycle - height * 4 * lag) * .055f * activity) * middle;
                break;
            case CompanionBodyMaterial.Flame:
                compression = Pulse(2.8f) * .028f + MathF.Sin(cycle) * .065f * activity;
                sway = (Pulse(3.8f, height * 5 * lag) * .037f
                    + MathF.Sin(cycle - height * lag) * .045f * activity) * height * height;
                break;
            case CompanionBodyMaterial.Silk:
                compression = Pulse(1.3f) * .012f + MathF.Cos(cycle) * .025f * activity;
                sway = (Pulse(1.7f, -height * 2.5f * lag) * .02f
                    + MathF.Sin(cycle - height * 2.5f * lag) * .032f * activity) * height * height;
                break;
            case CompanionBodyMaterial.Crystal:
                // Facets stay rigid: the body tips about its support, without a jelly squeeze.
                angle = (Pulse(1.5f) * .009f + MathF.Sin(cycle) * .028f * activity) * facing;
                break;
            case CompanionBodyMaterial.Stone:
                angle = (Pulse(1.1f) * .006f + MathF.Sin(cycle) * .018f * activity) * facing;
                break;
            case CompanionBodyMaterial.Metal:
                angle = (Pulse(1.7f) * .005f + MathF.Sin(cycle) * .023f * activity) * facing;
                break;
        }
        p.X *= 1 + compression; p.Y /= 1 + compression;
        p.X += sway * facing;
        if (angle != 0)
            p = Vector2.Transform(p, Matrix3x2.CreateRotation(angle));
        return position + (p + new Vector2(.5f, .9f)) * size;
    }

    public Matrix3x2 OrbitMovement(Vector2 center, float radius, float stride, bool mirrored, bool animate)
    {
        if (intensity == 0 || !ValidOrbit(center, radius, stride, activity, animate)) return Matrix3x2.Identity;
        var profile = profileAvatar == "Orbit" ? Profile : new(CompanionBodyMaterial.Species);
        var species = profile.Material == CompanionBodyMaterial.Species && profile.Tempo == 1 && profile.Strength == 1;
        if (species && intensity == 1 && !HasDynamics)
            return RigidTravel(center, radius, stride, activity, mirrored, animate);
        var phase = (stride - MathF.Floor(stride)) * MathF.Tau;
        var rigid = profile.Material is CompanionBodyMaterial.Stone or CompanionBodyMaterial.Metal or CompanionBodyMaterial.Crystal;
        var weight = rigid ? .55f : 1f;
        var facing = mirrored ? -1 : 1;
        var breath = species ? 0 : (float)Math.Sin(materialTime * profile.Tempo * 1.3);
        var angle = (MathF.Sin(phase) * .055f * activity + breath * .012f) * profile.Strength * weight * facing;
        var offset = new Vector2(MathF.Sin(phase) * radius * .025f * activity * facing,
            (MathF.Sin(phase * 2) * .035f * activity + breath * .018f) * radius) * profile.Strength * weight;
        // Orbit's body, painted hat and rings share this rigid transform.
        angle = (angle + (reactionTurn + reactionLean * .5f) * facing) * intensity;
        offset *= intensity;
        return Matrix3x2.CreateTranslation(-center) * Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(center + offset);
    }

    internal static Matrix3x2 RigidTravel(Vector2 center, float radius, float stride, float activity, bool mirrored, bool animate)
    {
        if (!ValidOrbit(center, radius, stride, activity, animate) || activity <= 0) return Matrix3x2.Identity;
        var phase = (stride - MathF.Floor(stride)) * MathF.Tau;
        var strength = Math.Clamp(activity, 0, 1); var facing = mirrored ? -1 : 1;
        var angle = MathF.Sin(phase) * .055f * strength * facing;
        var offset = new Vector2(MathF.Sin(phase) * radius * .025f * facing, MathF.Sin(phase * 2) * radius * .035f) * strength;
        return Matrix3x2.CreateTranslation(-center) * Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(center + offset);
    }

    private static bool ValidOrbit(Vector2 center, float radius, float stride, float activity, bool animate)
        => animate && float.IsFinite(activity) && float.IsFinite(stride) && float.IsFinite(radius) && radius > 0
            && float.IsFinite(center.X) && float.IsFinite(center.Y);

    public (Vector2 Minimum, Vector2 Maximum) Draw(ImDrawListPtr draw, ImTextureID texture, string avatar,
        CompanionFloatQuad quad, Vector2 position, Vector2 cellSize, float stride, bool mirrored, bool animate, uint tint)
        => DrawPadded(draw, texture, avatar, quad, position, cellSize, stride, mirrored, animate, tint, 0);

    public (Vector2 Minimum, Vector2 Maximum) DrawPadded(ImDrawListPtr draw, ImTextureID texture, string avatar,
        CompanionFloatQuad quad, Vector2 position, Vector2 cellSize, float stride, bool mirrored, bool animate, uint tint, float visualPadding)
    {
        const int divisions = 8;
        Span<Vector2> points = stackalloc Vector2[(divisions + 1) * (divisions + 1)];
        Span<Vector2> originals = stackalloc Vector2[points.Length];
        var strength = 1f;
        var padding = cellSize * (float.IsFinite(visualPadding) ? Math.Clamp(visualPadding, 0, .5f) : 0);
        var limitMinimum = position - padding; var limitMaximum = position + cellSize + padding;
        var minimum = new Vector2(float.MaxValue); var maximum = new Vector2(float.MinValue);
        for (var y = 0; y <= divisions; y++)
        for (var x = 0; x <= divisions; x++)
        {
            var u = x / (float)divisions; var v = y / (float)divisions;
            var p = Vector2.Lerp(Vector2.Lerp(quad.A, quad.B, u), Vector2.Lerp(quad.D, quad.C, u), v);
            var index = y * (divisions + 1) + x;
            originals[index] = p;
            var warped = Warp(avatar, p, position, cellSize, stride, mirrored, animate);
            points[index] = warped;
            var offset = warped - p;
            // Preserve one connected body and its source scale. When a reviewed
            // crop has little spare room, reduce the deformation as a whole
            // instead of clipping tips or independently pinning edge vertices.
            strength = Math.Min(strength, Available(p.X, offset.X, limitMinimum.X, limitMaximum.X));
            strength = Math.Min(strength, Available(p.Y, offset.Y, limitMinimum.Y, limitMaximum.Y));
        }
        LastDrawStrength = strength;
        for (var index = 0; index < points.Length; index++)
        {
            var p = points[index] = Vector2.Lerp(originals[index], points[index], strength);
            minimum = Vector2.Min(minimum, p); maximum = Vector2.Max(maximum, p);
        }
        for (var y = 0; y < divisions; y++)
        for (var x = 0; x < divisions; x++)
        {
            var u = x / (float)divisions; var v = y / (float)divisions;
            var nextU = (x + 1) / (float)divisions; var nextV = (y + 1) / (float)divisions;
            var i = y * (divisions + 1) + x;
            draw.AddImageQuad(texture, points[i], points[i + 1], points[i + divisions + 2], points[i + divisions + 1],
                Uv(u, v), Uv(nextU, v), Uv(nextU, nextV), Uv(u, nextV), tint);
        }
        return (minimum, maximum);

        static float Available(float original, float offset, float first, float last)
            => offset > 0 ? Math.Clamp((last - original) / offset, 0, 1)
                : offset < 0 ? Math.Clamp((first - original) / offset, 0, 1) : 1;
        Vector2 Uv(float u, float v) => Vector2.Lerp(Vector2.Lerp(quad.UvA, quad.UvB, u), Vector2.Lerp(quad.UvD, quad.UvC, u), v);
    }
}
