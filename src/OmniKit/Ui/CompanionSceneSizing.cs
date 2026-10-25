namespace OmniKit.Ui;

internal static class CompanionSceneSizing
{
    public static float UiScale(float value) => Bounded(value, .25f, 4f, 1f);
    public static float SceneScale(Configuration config) => Bounded(config.CompanionSceneScale, .5f, 2f, 1f);
    public static float HomeEdge(Configuration config, float uiScale) =>
        Bounded(config.CompanionHomeSize, 48f, 384f, 124.8f) * UiScale(uiScale) * SceneScale(config);
    public static float HandEdge(Configuration config, float uiScale) =>
        Bounded(config.CompanionHandSize, 8f, 192f, 38.4f) * UiScale(uiScale) * SceneScale(config);
    private static float Bounded(float value, float minimum, float maximum, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
