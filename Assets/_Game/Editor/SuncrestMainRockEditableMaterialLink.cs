#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Assigns the editable material on the actual imported rock renderer(s) in edit mode.
// Never changes the user's position, rotation, scale, or any protected rocks.
[InitializeOnLoad]
internal static class SuncrestMainRockEditableMaterialLink
{
    private const string MaterialPath = "Assets/Manually Added Assets/Suncrest Beach Main Rock - Editable.mat";
    static SuncrestMainRockEditableMaterialLink()
    {
        EditorApplication.delayCall += Apply;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += Apply;
    }
    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var rock = GameObject.Find("Suncrest Beach Main Rock");
        if (rock == null || !rock.scene.IsValid()) return;
        var editable = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (editable == null) return;
        bool changed = false;
        foreach (var renderer in rock.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0) continue;
            bool hasCustomAssignment = false;
            foreach (var m in materials)
                if (m == editable) hasCustomAssignment = true;
            if (hasCustomAssignment) continue;
            // Only replace imported/default materials, not a later hand-selected asset.
            bool imported = true;
            foreach (var m in materials)
                if (m != null && AssetDatabase.IsMainAsset(m) && AssetDatabase.GetAssetPath(m) != MaterialPath)
                    imported = false;
            if (!imported) continue;
            for (int i = 0; i < materials.Length; i++) materials[i] = editable;
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
            changed = true;
        }
        if (changed) EditorSceneManager.MarkSceneDirty(rock.scene);
    }
}
#endif
