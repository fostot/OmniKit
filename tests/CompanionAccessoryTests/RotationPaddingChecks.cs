using System.Collections;
using System.Numerics;
using System.Reflection;

internal static class RotationPaddingChecks
{
    public static int Run(Assembly plugin, string assets)
    {
        var checks = 0;
        Type Type(string name) => plugin.GetType("OmniKit." + name, true)!;
        object Property(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value)!;
        var placement = Type("Ui.CompanionAccessoryPlacement");
        var adjustmentType = Type("CompanionAccessoryAdjustment");
        var bodyType = Type("Ui.SeasonalBodyTransform"); var fitType = Type("Ui.SeasonalAccessoryFit");
        var rendererType = Type("Ui.SeasonalCompanionRenderer");
        var renderer = Activator.CreateInstance(rendererType, [assets])!;
        var quadMethod = rendererType.GetMethods().Single(method => method.Name == "TryGetQuad" &&
            method.GetParameters().Length == 4 && method.GetParameters()[2].ParameterType == fitType);
        var library = Activator.CreateInstance(Type("Ui.CompanionVariantLibrary"), [assets])!;
        var looks = ((IEnumerable)Property(library, "Looks")).Cast<object>().Where(look => (string)Property(look, "Choice") == "Look1").ToArray();
        if (looks.Length != 8) throw new InvalidOperationException("Eight real public body types are required for the rotated padding check.");
        foreach (var look in looks)
        {
            var avatar = (string)Property(look, "Avatar"); var source = Property(look, "Normal");
            var sourceRect = (Vector4)Property(source, "Bounds");
            var fit = Activator.CreateInstance(fitType, [Property(source, "AccessoryAnchor"), Property(source, "AccessoryHeadWidth")])!;
            foreach (var mirrored in new[] { false, true })
            foreach (var roll in new[] { 0f, MathF.PI / 4, MathF.PI / 2 })
            {
                var frame = source.GetType().GetMethod("GetFrame")!.Invoke(source, [Vector2.Zero, new Vector2(96), mirrored])!;
                var upright = bodyType.GetMethod("FromFrame")!.Invoke(null, [sourceRect, Property(source, "SourceScale"), frame, mirrored])!;
                // Roll the complete source transform, as the runtime does for a rolling body.
                Vector2 Point(string name) => Vector2.Transform((Vector2)Property(upright, name) - new Vector2(48),
                    Matrix3x2.CreateRotation(roll)) + new Vector2(48);
                var body = Activator.CreateInstance(bodyType, [sourceRect, Property(source, "SourceScale"), Point("A"), Point("B"), Point("D")])!;
                var themesChecked = 0;
                foreach (var theme in Enum.GetValues(Type("Ui.SeasonalCompanionTheme")))
                {
                    object?[] arguments = [theme, body, fit, null];
                    if (!(bool)quadMethod.Invoke(renderer, arguments)!) continue;
                    themesChecked++;
                    var original = arguments[3]!;
                    foreach (var angle in new[] { -180f, -90f, -45f, 0f, 45f, 90f, 180f })
                    foreach (var size in new[] { 1f, 3f })
                    {
                        var adjustment = Activator.CreateInstance(adjustmentType, [avatar, "Look1", theme.ToString()!, 1f, 1f, size, angle])!;
                        var padding = (float)placement.GetMethod("Padding")!.Invoke(null, [adjustment])! * 96;
                        var adjusted = placement.GetMethod("Apply")!.Invoke(null, [original, body, fit, adjustment])!;
                        foreach (var corner in new[] { "A", "B", "C", "D" })
                        {
                            var point = (Vector2)Property(adjusted, corner); checks++;
                            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || point.X < -padding - .001f ||
                                point.Y < -padding - .001f || point.X > 96 + padding + .001f || point.Y > 96 + padding + .001f)
                                throw new InvalidOperationException($"{avatar}/{theme}: angle {angle}, size {size}, roll {roll}, mirrored {mirrored} exceeds reserved fit padding.");
                        }
                    }
                }
                checks++;
                if (themesChecked != 9) throw new InvalidOperationException($"{avatar}: all nine reviewed accessories must participate in padding checks.");
            }
        }
        return checks;
    }
}
