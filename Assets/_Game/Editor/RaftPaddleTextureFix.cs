using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Completes the material assignment on the authored raft paddle only.
///
/// RaftModelImporter intentionally preserves the supplied Blender geometry/UVs and
/// all of the existing boat gameplay. Some Unity .blend imports expose fewer material
/// slots than the paddle mesh has submeshes, which can leave part of the paddle using
/// Unity's fallback material. This post-fix expands the paddle's material array to
/// every submesh and assigns the supplied Oar Texture.jpg to every paddle slot.
/// Nothing else on the raft is changed.
/// </summary>
public sealed class RaftPaddleTextureFix : AssetPostprocessor
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string OarTexturePath = "Assets/_Game/Boats/Raft/Authored/Oar Texture.jpg";
    private const string MaterialFolder = "Assets/_Game/Boats/Raft/Authored/Materials";
    private const string PaddleMaterialPath = MaterialFolder + "/PaddleComplete.mat";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string MarkerName = "RaftPaddleTextureComplete_v1";

    [InitializeOnLoadMethod]
    private static void QueueInitialFix()
    {
        // Run one editor tick after the main raft importer gets its own delayCall.
        EditorApplication.delayCall += () => EditorApplication.delayCall += ApplyIfNeeded;
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool relevant = false;
        for (int i = 0; i < importedAssets.Length; i++)
        {
            string path = importedAssets[i];
            if (path == PrefabPath || path == OarTexturePath || path.EndsWith("/Raft.blend", StringComparison.OrdinalIgnoreCase))
            {
                relevant = true;
                break;
            }
        }

        if (relevant) EditorApplication.delayCall += ApplyIfNeeded;
    }

    [MenuItem("Tools/Open World/Repair Raft Paddle Texture")]
    private static void RepairOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft Paddle", "Exit Play Mode first.", "OK");
            return;
        }

        bool changed = ApplyIfNeeded(true);
        if (!changed)
            EditorUtility.DisplayDialog("Raft Paddle", "The paddle is already fully textured, or the authored raft has not been imported yet.", "OK");
    }

    private static void ApplyIfNeeded()
    {
        ApplyIfNeeded(false);
    }

    private static bool ApplyIfNeeded(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Texture2D oarTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath);
        if (prefab == null || oarTexture == null) return false;

        Transform prefabWrapper = prefab.transform.Find(WrapperPath);
        if (prefabWrapper == null) return false;
        if (!force && prefabWrapper.Find(MarkerName) != null) return false;

        Directory.CreateDirectory(MaterialFolder);
        AssetDatabase.Refresh();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            Renderer paddle = FindPaddleRenderer(wrapper);
            if (paddle == null)
            {
                Debug.LogWarning("Raft paddle texture repair could not identify the authored paddle renderer. No raft objects were changed.");
                return false;
            }

            Material paddleMaterial = BuildPaddleMaterial(oarTexture);
            int subMeshCount = GetSubMeshCount(paddle);
            Material[] current = paddle.sharedMaterials;
            int slotCount = Mathf.Max(1, Mathf.Max(subMeshCount, current != null ? current.Length : 0));
            Material[] completed = new Material[slotCount];
            for (int i = 0; i < completed.Length; i++) completed[i] = paddleMaterial;

            // This is the only visible change: every submesh of the existing authored
            // paddle now uses the supplied paddle texture. Geometry, UVs, transforms,
            // raft materials, orientation, waterline and gameplay components remain.
            paddle.sharedMaterials = completed;

            Transform oldMarker = wrapper.Find(MarkerName);
            if (oldMarker != null) UnityEngine.Object.DestroyImmediate(oldMarker.gameObject);
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(wrapper, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log("Raft paddle texture completed: Oar Texture.jpg is assigned to every paddle submesh; all other raft visuals and boat behavior were left unchanged.");
            if (force)
                EditorUtility.DisplayDialog("Raft Paddle Fixed", "The supplied Oar Texture.jpg is now assigned to every paddle submesh. Nothing else on the raft was changed.", "OK");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Renderer FindPaddleRenderer(Transform wrapper)
    {
        Renderer[] renderers = wrapper.GetComponentsInChildren<Renderer>(true);
        Renderer best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            string name = renderer.name ?? string.Empty;
            if (name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0)
                return renderer;

            Material[] materials = renderer.sharedMaterials;
            if (materials != null)
            {
                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if (material == null) continue;
                    string materialName = material.name ?? string.Empty;
                    if (materialName.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        materialName.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0)
                        return renderer;
                }
            }

            // The supplied paddle is the long, narrow authored mesh. The raft body
            // is wider than this range and the seat is much less elongated, making
            // this fallback specific to the paddle without depending on Blender's
            // generic Plane/Plane.001/Plane.002 names.
            Vector3 size = renderer.bounds.size;
            float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            float middle = size.x + size.y + size.z - largest - smallest;
            float ratio = largest / Mathf.Max(0.01f, middle);

            if (largest < 1.7f || largest > 3.6f || middle > 1.0f || ratio < 3.0f) continue;

            float score = ratio * 10f + largest - middle;
            if (score > bestScore)
            {
                bestScore = score;
                best = renderer;
            }
        }

        return best;
    }

    private static int GetSubMeshCount(Renderer renderer)
    {
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null && skinned.sharedMesh != null)
            return skinned.sharedMesh.subMeshCount;

        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
            return filter.sharedMesh.subMeshCount;

        return 0;
    }

    private static Material BuildPaddleMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No supported lit shader is available for the paddle material.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(PaddleMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, PaddleMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.name = "PaddleComplete";
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.28f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
