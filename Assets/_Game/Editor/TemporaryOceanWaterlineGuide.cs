using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only, temporary water-level guide for authoring islands.
/// Renders a transparent 10 km x 10 km blue plane in Scene view, centered on
/// world X/Z zero and following the actual OceanWater.BaseWaterLevel.
/// Does not create a GameObject, change the scene, or affect Play Mode/builds.
/// </summary>
[InitializeOnLoad]
internal static class TemporaryOceanWaterlineGuide
{
    private const string MenuPath = "Tools/Open World/Waterline Guide/Show Temporary Blue Plane";
    private const string PreferenceKey = "OpenWorld.TemporaryOceanWaterlineGuide.Enabled";
    private const float HalfExtent = 5000f;

    // Slight visual elevation prevents z-fighting with the flat ocean mesh.
    // The reference still represents OceanWater.BaseWaterLevel, not wave crests.
    private const float VisualOffset = 0.035f;

    private static OceanWater cachedOcean;
    private static int cachedSceneHandle = -1;

    static TemporaryOceanWaterlineGuide()
    {
        SceneView.duringSceneGui += Draw;
        EditorApplication.hierarchyChanged += InvalidateOcean;
        EditorSceneManager.sceneOpened += (scene, mode) => InvalidateOcean();
        EditorSceneManager.sceneClosed += scene => InvalidateOcean();
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        EditorPrefs.SetBool(PreferenceKey, !IsEnabled);
        SceneView.RepaintAll();
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggle()
    {
        Menu.SetChecked(MenuPath, IsEnabled);
        return true;
    }

    private static bool IsEnabled => EditorPrefs.GetBool(PreferenceKey, true);

    private static void InvalidateOcean()
    {
        cachedOcean = null;
        cachedSceneHandle = -1;
        SceneView.RepaintAll();
    }

    private static OceanWater FindOceanIn(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        if (cachedSceneHandle == scene.handle && cachedOcean != null &&
            cachedOcean.gameObject.scene == scene)
            return cachedOcean;

        cachedSceneHandle = scene.handle;
        cachedOcean = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            cachedOcean = root.GetComponentInChildren<OceanWater>(true);
            if (cachedOcean != null)
                break;
        }
        return cachedOcean;
    }

    private static void Draw(SceneView view)
    {
        if (!IsEnabled || EditorApplication.isPlayingOrWillChangePlaymode ||
            Event.current.type != EventType.Repaint)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
            return;

        OceanWater ocean = FindOceanIn(scene);
        if (ocean == null)
            return;

        float y = ocean.BaseWaterLevel + VisualOffset;
        Vector3[] corners =
        {
            new Vector3(-HalfExtent, y, -HalfExtent),
            new Vector3(-HalfExtent, y, HalfExtent),
            new Vector3(HalfExtent, y, HalfExtent),
            new Vector3(HalfExtent, y, -HalfExtent)
        };

        CompareFunction previousDepthTest = Handles.zTest;
        try
        {
            // Respect terrain and rock depth: solid objects above sea level
            // remain visible, while underwater surfaces get a subtle blue tint.
            Handles.zTest = CompareFunction.LessEqual;
            Handles.DrawSolidRectangleWithOutline(
                corners,
                new Color(0.07f, 0.53f, 0.95f, 0.19f),
                new Color(0.12f, 0.55f, 1f, 0.55f));
        }
        finally
        {
            Handles.zTest = previousDepthTest;
        }
    }
}
