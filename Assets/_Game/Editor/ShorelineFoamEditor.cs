using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(ShorelineFoam))]
public sealed class ShorelineFoamInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("These retired baked strips are hidden automatically. Foam now comes from the ocean shader and follows the moving waterline. No regeneration is needed. Adjust Water Contact Foam on OceanWater.", MessageType.Info);
        if (GUILayout.Button("Remove Retired Baked Foam From Scene"))
            Undo.DestroyObjectImmediate(((ShorelineFoam)target).gameObject);
    }
}

public static class ShorelineFoamEditor
{
    [MenuItem("Tools/Open World/Shoreline Foam/Remove Retired Baked Foam From Scene")]
    public static void RemoveRetiredFoam()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        Scene scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var foam in root.GetComponentsInChildren<ShorelineFoam>(true))
                if (foam != null) Undo.DestroyObjectImmediate(foam.gameObject);
        // Saved bake assets are deliberately retained for Undo and other scenes.
    }
}
