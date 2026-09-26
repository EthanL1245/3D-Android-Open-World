using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs immediately before RaftMaterialRecoveryV9. Older repairs can leave several
/// material slots pointing at OarUserTexture even though only the FINAL split submesh
/// is actually the dedicated oar slot. Normalize that state first so v9 always reads
/// the last OarSplit submesh as its oar hint, never a contaminated raft slot.
/// </summary>
public static class RaftMaterialPreRecoveryV9
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string RaftTexturePath = "Assets/_Game/Boats/Raft/Authored/Raft Texture.jpg";
    private const string OarTexturePath = "Assets/_Game/Boats/Raft/UserTextures/Oar Texture.jpg";
    private const string AssetFolder = "Assets/_Game/Boats/Raft/UserTextures";
    private const string RaftMaterialPath = AssetFolder + "/RaftRecoveryPrep_v9.mat";
    private const string OarMaterialPath = AssetFolder + "/OarUserTexture.mat";
    private const string FinalMarker = "RaftMaterialRecovery_v9";

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                    EditorApplication.delayCall += () =>
                        EditorApplication.delayCall += () =>
                            EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Texture2D raftTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RaftTexturePath);
        Texture2D oarTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath);
        if (prefab == null || raftTexture == null || oarTexture == null) return;

        Transform prefabWrapper = prefab.transform.Find(WrapperPath);
        if (prefabWrapper == null || prefabWrapper.Find(FinalMarker) != null) return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return;
            Renderer target = wrapper.GetComponentsInChildren<Renderer>(true)
                .Where(r => GetMesh(r) != null && GetMesh(r).name.IndexOf("OarSplit", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(r => GetMesh(r).vertexCount)
                .FirstOrDefault();
            Mesh mesh = GetMesh(target);
            if (target == null || mesh == null || mesh.subMeshCount < 2) return;

            Material raftMaterial = BuildMaterial(RaftMaterialPath, raftTexture, "RaftRecoveryPrep_v9", null);
            Material existingOar = AssetDatabase.LoadAssetAtPath<Material>(OarMaterialPath);
            Material oarMaterial = BuildMaterial(OarMaterialPath, oarTexture, "OarUserTexture", existingOar);

            Material[] materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length - 1; i++) materials[i] = raftMaterial;
            materials[materials.Length - 1] = oarMaterial;
            target.sharedMaterials = materials;
            EditorUtility.SetDirty(target);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[RAFT V9 PREP] Reset contaminated split materials: all original submeshes use Raft Texture.jpg; only final split submesh uses Oar Texture.jpg. Full source rebuild follows in v9.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Material BuildMaterial(string path, Texture2D texture, string name, Material template)
    {
        Directory.CreateDirectory(AssetFolder);
        Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
        if (fallback == null) fallback = Shader.Find("Standard");
        if (fallback == null) return template;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = template != null ? new Material(template) : new Material(fallback);
            AssetDatabase.CreateAsset(material, path);
        }
        material.name = name;
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer == null) return null;
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) return skinned.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }
}
