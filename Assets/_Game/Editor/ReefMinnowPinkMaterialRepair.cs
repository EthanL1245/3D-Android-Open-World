using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Repairs the Reef Minnow material independently of the geometry importer.
/// Pink in Unity means the material shader is missing/unsupported, not that the
/// black/blue JPG itself is wrong. This deliberately keeps the already-approved
/// mesh, hooks, animation, LineAttach, scale, orientation and gameplay untouched
/// and replaces only the body material with a fresh URP/Lit material.
/// </summary>
public static class ReefMinnowPinkMaterialRepair
{
    private const string PrefabPath = "Assets/Resources/Fishing/ReefMinnowCrankbait.prefab";
    private const string TexturePath = "Assets/_Game/Fishing/ReefMinnow/Source/BodyTexture.jpg";
    private const string MaterialPath = "Assets/_Game/Fishing/ReefMinnow/ReefMinnowURPBody.mat";
    private const string AutoSessionKey = "OpenWorld.AutoRepair.ReefMinnowPinkMaterial.20260925.v1";

    [InitializeOnLoadMethod]
    private static void AutoRepairAfterPull()
    {
        if (Application.isBatchMode || SessionState.GetBool(AutoSessionKey, false)) return;
        SessionState.SetBool(AutoSessionKey, true);

        // Run one editor tick after the older geometry/visibility repair so this
        // material is always the final authoritative material on Reef Minnow.
        EditorApplication.delayCall += () =>
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) == null) return;

                try
                {
                    Repair(false);
                    Debug.Log("Reef Minnow pink-material repair applied: explicit URP/Lit + black/blue texture.");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        };
    }

    [MenuItem("Tools/Open World/Fix Reef Minnow Pink Material (One Click)")]
    public static void RepairOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Reef Minnow", "Exit Play Mode first.", "OK");
            return;
        }

        try
        {
            Repair(true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Reef Minnow Material Fix Failed", exception.Message + "\n\nSee Console for details.", "OK");
        }
    }

    private static void Repair(bool showDialog)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Reef Minnow prefab is missing. Reimport the Reef Minnow first.");

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
            throw new InvalidOperationException("Reef Minnow black/blue texture is missing at " + TexturePath + ".");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("Universal Render Pipeline/Lit shader was not found in this Unity project.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Reef Minnow Black Blue URP" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            // Assigning the shader explicitly is the key part of the pink fix.
            // Do not clone the previous material because that can preserve a
            // broken/missing shader reference.
            material.shader = shader;
        }

        material.name = "Reef Minnow Black Blue URP";
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.12f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.48f);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f); // keep the lure shell visible from both sides
        material.doubleSidedGI = true;
        material.renderQueue = -1;
        EditorUtility.SetDirty(material);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform authoredModel = FindDeepChild(root.transform, "AuthoredModel");
            if (authoredModel == null)
                throw new InvalidOperationException("Reef Minnow prefab has no AuthoredModel.");

            MeshFilter[] filters = authoredModel.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f != null && f.sharedMesh != null && !IsUnderLightingRig(f.transform, authoredModel))
                .ToArray();
            if (filters.Length < 1)
                throw new InvalidOperationException("Reef Minnow has no visible mesh filters.");

            MeshFilter body = LargestMesh(filters);
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer == null)
                throw new InvalidOperationException("Reef Minnow body has no renderer.");

            Material[] slots = new Material[Mathf.Max(1, bodyRenderer.sharedMaterials.Length)];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            bodyRenderer.sharedMaterials = slots;
            bodyRenderer.enabled = true;
            body.gameObject.SetActive(true);
            authoredModel.gameObject.SetActive(true);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MaterialPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Reef Minnow Material Fixed",
                "Reef Minnow now uses an explicit URP/Lit material with the supplied black/blue texture. Mesh, hooks, animations, LineAttach, size, orientation and fishing mechanics were not changed.",
                "OK");
        }
    }

    private static MeshFilter LargestMesh(MeshFilter[] filters)
    {
        MeshFilter best = filters[0];
        float bestScore = -1f;
        for (int i = 0; i < filters.Length; i++)
        {
            Bounds bounds = filters[i].sharedMesh.bounds;
            Vector3 size = bounds.size;
            float score = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * Mathf.Max(0.000001f, size.x * size.y * size.z);
            if (score > bestScore)
            {
                bestScore = score;
                best = filters[i];
            }
        }
        return best;
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private static bool IsUnderLightingRig(Transform transform, Transform authoredModel)
    {
        for (Transform current = transform; current != null && current != authoredModel; current = current.parent)
            if (current.name == "AuthoredLightingRig") return true;
        return false;
    }
}
