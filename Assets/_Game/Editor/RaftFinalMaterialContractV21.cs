using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Deterministic final raft material repair based on the exact v20 diagnostic map.
///
/// The imported hierarchy already tells us what moves:
///   Raft/Empty.002/Plane.001 = left moving oar assembly
///   Raft/Empty.003/Plane.002 = right moving oar assembly
///   Raft/Plane             = stationary raft/support geometry
///
/// Therefore there is no need for any more geometric guessing. Every material slot on
/// the two moving-oar renderers uses Oar Texture. Every material slot on the stationary
/// raft renderer uses the already-correct raft material from its slot 0. This also
/// fixes the thin underside/support strip (Raft/Plane submesh 1) without touching UVs,
/// vertices, transforms, colliders, animation, or gameplay.
/// </summary>
public static class RaftFinalMaterialContractV21
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string LeftOarPath = "Raft/Empty.002/Plane.001";
    private const string RightOarPath = "Raft/Empty.003/Plane.002";
    private const string StationaryRaftPath = "Raft/Plane";
    private const string OarTexturePath = "Assets/_Game/Boats/Raft/UserTextures/Oar Texture.jpg";
    private const string OarMaterialPath = "Assets/_Game/Boats/Raft/UserTextures/OarUserTexture.mat";
    private const string MarkerName = "RaftFinalMaterialContract_v21";

    [InitializeOnLoadMethod]
    private static void Queue()
    {
        EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Open World/Apply Final Raft + Oar Materials (v21)")]
    private static void Force()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v21", "Exit Play Mode first.", "OK");
            return;
        }
        Apply(true);
    }

    private static bool Apply(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[RAFT V21] BaseBoat.prefab is missing.");
            return false;
        }

        Transform readWrapper = prefab.transform.Find(WrapperPath);
        if (readWrapper == null)
        {
            Debug.LogError("[RAFT V21] Uploaded Raft Model wrapper is missing.");
            return false;
        }
        if (!force && readWrapper.Find(MarkerName) != null) return false;

        Material oarMaterial = LoadOrBuildOarMaterial();
        if (oarMaterial == null)
        {
            Debug.LogError("[RAFT V21] Could not load/build OarUserTexture material.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            Renderer left = FindRenderer(wrapper, LeftOarPath);
            Renderer right = FindRenderer(wrapper, RightOarPath);
            Renderer raft = FindRenderer(wrapper, StationaryRaftPath);
            if (left == null || right == null || raft == null)
            {
                Debug.LogError("[RAFT V21] Exact v20 renderer paths were not found. Nothing was changed. " +
                    "left=" + (left != null) + " right=" + (right != null) + " raft=" + (raft != null));
                return false;
            }

            Material[] raftMats = raft.sharedMaterials ?? Array.Empty<Material>();
            if (raftMats.Length == 0 || raftMats[0] == null || !UsesRaftTexture(raftMats[0]))
            {
                Debug.LogError("[RAFT V21] Stationary raft slot 0 is not the known-good raft material. Refusing to guess.");
                return false;
            }
            Material raftMaterial = raftMats[0];

            // Exact hierarchy contract: both complete child renderers move as the oars.
            SetEverySlot(left, oarMaterial);
            SetEverySlot(right, oarMaterial);

            // Exact hierarchy contract: the main Plane is stationary raft/support geometry.
            // In the v20 map, its S1 is the thin 2.6749m-wide underside/support strip.
            SetEverySlot(raft, raftMaterial);

            Transform old = wrapper.Find(MarkerName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            new GameObject(MarkerName).transform.SetParent(wrapper, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Serialized verification after save.
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform savedWrapper = saved != null ? saved.transform.Find(WrapperPath) : null;
            Renderer savedLeft = savedWrapper != null ? FindRenderer(savedWrapper, LeftOarPath) : null;
            Renderer savedRight = savedWrapper != null ? FindRenderer(savedWrapper, RightOarPath) : null;
            Renderer savedRaft = savedWrapper != null ? FindRenderer(savedWrapper, StationaryRaftPath) : null;

            bool ok = AllSlotsUseOar(savedLeft) && AllSlotsUseOar(savedRight) && AllSlotsUseRaft(savedRaft);
            if (!ok)
            {
                Debug.LogError("[RAFT V21] Save verification failed. leftOar=" + AllSlotsUseOar(savedLeft) +
                    " rightOar=" + AllSlotsUseOar(savedRight) + " stationaryRaft=" + AllSlotsUseRaft(savedRaft));
                return false;
            }

            Debug.Log("[RAFT V21] SUCCESS — exact v20 hierarchy contract applied. " +
                "LEFT oar all slots=Oar Texture; RIGHT oar all slots=Oar Texture; " +
                "stationary Raft/Plane all slots=Raft Texture (including the underside/support strip). " +
                "No geometry, UVs, transforms, colliders, animation or gameplay were changed.");

            if (force)
                EditorUtility.DisplayDialog("Raft v21 Complete",
                    "Both complete moving oars now use the oar texture. The main stationary raft, including the thin underside/support strip, uses the raft wood texture.",
                    "OK");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[RAFT V21] Exception: " + e);
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Renderer FindRenderer(Transform wrapper, string relativePath)
    {
        Transform t = wrapper.Find(relativePath);
        return t != null ? t.GetComponent<Renderer>() : null;
    }

    private static void SetEverySlot(Renderer renderer, Material material)
    {
        Material[] mats = renderer.sharedMaterials ?? Array.Empty<Material>();
        Mesh mesh = GetMesh(renderer);
        int count = Mathf.Max(mats.Length, mesh != null ? mesh.subMeshCount : 0);
        if (count <= 0) count = 1;
        Material[] updated = new Material[count];
        for (int i = 0; i < count; i++) updated[i] = material;
        renderer.sharedMaterials = updated;
        EditorUtility.SetDirty(renderer);
    }

    private static Material LoadOrBuildOarMaterial()
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath);
        if (tex == null) return null;

        Material m = AssetDatabase.LoadAssetAtPath<Material>(OarMaterialPath);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            m = new Material(shader) { name = "OarUserTexture" };
            AssetDatabase.CreateAsset(m, OarMaterialPath);
        }

        m.name = "OarUserTexture";
        m.mainTexture = tex;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .24f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static bool AllSlotsUseOar(Renderer renderer)
    {
        if (renderer == null) return false;
        Material[] mats = renderer.sharedMaterials ?? Array.Empty<Material>();
        if (mats.Length == 0) return false;
        foreach (Material m in mats) if (!UsesOarTexture(m)) return false;
        return true;
    }

    private static bool AllSlotsUseRaft(Renderer renderer)
    {
        if (renderer == null) return false;
        Material[] mats = renderer.sharedMaterials ?? Array.Empty<Material>();
        if (mats.Length == 0) return false;
        foreach (Material m in mats) if (!UsesRaftTexture(m)) return false;
        return true;
    }

    private static bool UsesOarTexture(Material m)
    {
        Texture t = GetTexture(m);
        return t != null && (t.name ?? string.Empty).IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool UsesRaftTexture(Material m)
    {
        Texture t = GetTexture(m);
        return t != null && (t.name ?? string.Empty).IndexOf("raft texture", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Texture GetTexture(Material m)
    {
        if (m == null) return null;
        Texture t = m.mainTexture;
        if (t == null && m.HasProperty("_BaseMap")) t = m.GetTexture("_BaseMap");
        if (t == null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
        return t;
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer == null) return null;
        if (renderer is SkinnedMeshRenderer smr) return smr.sharedMesh;
        MeshFilter mf = renderer.GetComponent<MeshFilter>();
        return mf != null ? mf.sharedMesh : null;
    }
}
