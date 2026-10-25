using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class EditorIsolationChecks
{
    public static int Run(Assembly plugin, string assets)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var checks = 0; var serviceCalls = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
        Type Type(string name) => plugin.GetType("OmniKit." + name, true)!;
        void Set(object target, string name, object? value) => target.GetType().GetField(name, instance)!.SetValue(target, value);
        object? Get(object target, string name) => target.GetType().GetField(name, instance)!.GetValue(target);
        Dictionary<FieldInfo, object?> Snapshot(object target) => target.GetType().GetFields(instance).ToDictionary(field => field, field => field.GetValue(target));
        void Unchanged(object target, Dictionary<FieldInfo, object?> before, string reason, string? except = null)
        {
            foreach (var field in before)
                if (field.Key.Name != except) Check(Equals(field.Value, field.Key.GetValue(target)), reason + ": " + field.Key.Name);
        }
        var services = Type("Services").GetProperties(statics).Where(property => property.PropertyType.IsInterface && property.SetMethod != null).ToArray();
        var priorServices = services.ToDictionary(property => property, property => property.GetValue(null));
        try
        {
            foreach (var property in services)
                property.SetValue(null, TextureProxy.Create(property.PropertyType, (method, _) =>
                {
                    serviceCalls++;
                    throw new InvalidOperationException("A game service was called by the isolated editor proof: " + method.Name);
                }));
            var roamingType = Type("Ui.RoamingCompanion");
            var roamer = RuntimeHelpers.GetUninitializedObject(roamingType);
            var states = new List<object>();
            foreach (var name in new[] { "motion", "gait", "physics", "napPhysics", "bodyMotion", "orbitRings", "homeRoutine", "seasonal" })
            {
                var field = roamingType.GetField(name, instance)!;
                var state = RuntimeHelpers.GetUninitializedObject(field.FieldType);
                // Nonzero synthetic state makes any reset/advance observable. No native
                // constructor or game update is needed to exercise the first-line guard.
                foreach (var value in field.FieldType.GetFields(instance).Where(value => !value.IsInitOnly))
                {
                    if (value.FieldType == typeof(float)) value.SetValue(state, 3.25f);
                    else if (value.FieldType == typeof(double)) value.SetValue(state, 7.5d);
                    else if (value.FieldType == typeof(long)) value.SetValue(state, 17L);
                    else if (value.FieldType == typeof(Vector2)) value.SetValue(state, new Vector2(137, 211));
                    else if (value.FieldType == typeof(bool)) value.SetValue(state, true);
                }
                field.SetValue(roamer, state); states.Add(state);
            }
            var home = Get(roamer, "homeRoutine")!;
            Set(home, "<State>k__BackingField", Enum.Parse(Type("Ui.CompanionHomeState"), "Sleeping"));
            Set(home, "<Position>k__BackingField", new Vector2(237, 163));
            Set(home, "stateTime", 8.75f);
            Set(roamer, "animationTime", 13.25f); Set(roamer, "poseTime", 2.5f); Set(roamer, "handTime", .8f);
            Set(roamer, "lastTick", 123456789L);
            roamingType.GetProperty("IsEditingAccessories")!.SetValue(roamer, true);
            var before = Snapshot(roamer); var nested = states.Select(state => (State: state, Before: Snapshot(state))).ToArray();
            roamingType.GetMethod("Update")!.Invoke(roamer, null);
            Unchanged(roamer, before, "Editing Update preserves the roamer", "lastTick");
            foreach (var item in nested) Unchanged(item.State, item.Before, "Editing Update preserves nested state");
            foreach (var scale in new[] { .5f, 1f, 2f })
            {
                roamingType.GetMethod("Draw")!.Invoke(roamer, [scale]);
                Check((long)Get(roamer, "lastTick")! == 0, "Editing Draw clears the render tick so resume cannot integrate the editor's elapsed time.");
                Unchanged(roamer, before, "Editing Draw preserves the roamer", "lastTick");
                foreach (var item in nested) Unchanged(item.State, item.Before, "Editing Draw preserves nested state");
            }
            Check(serviceCalls == 0, "The editing guard never queries services or textures.");

            // Exercise the real look resolver while the same synthetic home remains asleep.
            var configType = Type("Configuration"); var config = Activator.CreateInstance(configType)!;
            configType.GetField("CompanionCostume")!.SetValue(config, "None");
            roamingType.GetFields(instance).Single(field => field.FieldType == configType).SetValue(roamer, config);
            Set(roamer, "assetDirectory", assets); Set(roamer, "displayAvatar", "Wink");
            var variants = Activator.CreateInstance(Type("Ui.CompanionVariantLibrary"), [assets])!;
            var seasonal = Activator.CreateInstance(Type("Ui.SeasonalCompanionService"), [config])!;
            Set(roamer, "seasonal", seasonal);
            var selection = Activator.CreateInstance(Type("Ui.CompanionAppearanceSelection"))!;
            var selectionUpdate = selection.GetType().GetMethod("Update")!;
            var resolve = roamingType.GetMethod("ResolveLook", instance)!;
            var requestType = Type("Ui.CompanionAppearanceRequest");
            var themeType = Type("Ui.SeasonalCompanionTheme");
            var homeBefore = Snapshot(home);
            var clockBefore = new[] { "animationTime", "poseTime", "handTime" }.ToDictionary(name => name, name => Get(roamer, name));
            object Theme(string name) => Enum.Parse(themeType, name);
            void SetTheme(string name)
            {
                var snapshotType = Type("Ui.SeasonalCompanionSnapshot"); var constructor = snapshotType.GetConstructors().Single();
                var emptyThemes = Activator.CreateInstance(constructor.GetParameters()[3].ParameterType);
                Set(seasonal, "snapshot", constructor.Invoke([Theme(name), Enum.Parse(Type("Ui.SeasonalEvidence"), "Manual"),
                    "Synthetic theme", emptyThemes, null]));
            }
            object Request(string look, bool enabled, string body)
            {
                var request = Activator.CreateInstance(requestType, ["Wink", look, enabled, body, "Auto"])!;
                requestType.GetProperty("CostumeSelection")!.SetValue(request, "None");
                return request;
            }
            string? CurrentChoice() => Get(roamer, "currentLook")?.GetType().GetProperty("Choice")!.GetValue(Get(roamer, "currentLook")) as string;
            void Resolve(string look, bool holiday, string body, string theme, bool canSwitch, bool expectedApply, bool expectedManual, string expectedChoice)
            {
                configType.GetField("CompanionVariant")!.SetValue(config, look);
                configType.GetField("CompanionHolidayBodiesEnabled")!.SetValue(config, holiday);
                configType.GetField("CompanionHolidayBody")!.SetValue(config, body);
                SetTheme(theme);
                var decision = selectionUpdate.Invoke(selection, [Request(look, holiday, body), Theme(theme), canSwitch])!;
                var apply = (bool)decision.GetType().GetProperty("CanApplyBody")!.GetValue(decision)!;
                Check(apply == expectedApply && (bool)decision.GetType().GetProperty("ManualChange")!.GetValue(decision)! == expectedManual,
                    "The appearance decision separates manual changes from blocked automatic changes.");
                var baseLook = variants.GetType().GetMethod("Find")!.Invoke(variants, ["Wink", look]);
                Check(baseLook != null, "The selected everyday look is real public artwork.");
                resolve.Invoke(roamer, [baseLook, apply]);
                Check(CurrentChoice() == expectedChoice, "The real sleeping-roamer resolver applies or defers the intended body.");
                Unchanged(home, homeBefore, "Changing appearance preserves sleep state, position, phase and the full nap routine");
                foreach (var clock in clockBefore) Check(Equals(Get(roamer, clock.Key), clock.Value), "Resolving appearance preserves pose/animation clocks.");
            }
            Resolve("Look1", false, "Auto", "Starlight", false, true, false, "Look1");
            Resolve("Look2", false, "Auto", "Starlight", false, true, true, "Look2");
            Resolve("Look2", true, "Starlight", "TheRising", false, true, true, "HolidayStarlight");
            Resolve("Look2", true, "MoonfireFaire", "TheRising", false, true, true, "HolidayMoonfireFaire");
            Resolve("Look2", true, "Auto", "Starlight", false, true, true, "HolidayStarlight");
            Resolve("Look2", true, "Auto", "TheRising", false, false, false, "HolidayStarlight");
            Resolve("Look2", true, "Auto", "TheRising", true, true, false, "HolidayTheRising");
            Check(serviceCalls == 0, "Appearance selection and look resolution never invoke native services or texture loading.");
        }
        finally { foreach (var property in priorServices) property.Key.SetValue(null, property.Value); }
        return checks;
    }
}
