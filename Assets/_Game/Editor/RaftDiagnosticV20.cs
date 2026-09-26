using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Non-destructive raft diagnostic. This deliberately changes NOTHING on the raft.
/// It dumps every renderer/submesh/material/texture plus animation binding paths,
/// writes the report to RaftV20Diagnostic.txt at the project root, and copies the
/// complete report to the clipboard so it can be pasted into chat in one step.
/// </summary>
public static class RaftDiagnosticV20
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string AuthoredBlendPath = "Assets/_Game/Boats/Raft/Authored/Raft.blend";
    private const string OarTexturePath = "Assets/_Game/Boats/Raft/UserTextures/Oar Texture.jpg";
    private const string RaftTexturePath = "Assets/_Game/Boats/Raft/Authored/Raft Texture.jpg";

    [InitializeOnLoadMethod]
    private static void Ready()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Debug.Log("[RAFT V20] Diagnostic ready. Use Tools/Open World/Dump Exact Raft Diagnostic (v20). It changes nothing and copies the full report to your clipboard.");
        };
    }

    [MenuItem("Tools/Open World/Dump Exact Raft Diagnostic (v20)")]
    private static void Dump()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft v20", "Exit Play Mode first, then run this again.", "OK");
            return;
        }

        var report = new StringBuilder(32768);
        report.AppendLine("=== RAFT V20 EXACT DIAGNOSTIC ===");
        report.AppendLine("Unity=" + Application.unityVersion);
        report.AppendLine("Project=" + Directory.GetParent(Application.dataPath)?.FullName);
        report.AppendLine("OarTexture=" + OarTexturePath + " exists=" + (AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath) != null));
        report.AppendLine("RaftTexture=" + RaftTexturePath + " exists=" + (AssetDatabase.LoadAssetAtPath<Texture2D>(RaftTexturePath) != null));

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Finish(report.AppendLine("ERROR: BaseBoat.prefab missing."));
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null)
            {
                Finish(report.AppendLine("ERROR: Uploaded Raft Model wrapper missing."));
                return;
            }

            report.AppendLine();
            report.AppendLine("--- PREFAB RENDERER / SUBMESH MAP ---");
            Renderer[] renderers = wrapper.GetComponentsInChildren<Renderer>(true);
            for (int ri = 0; ri < renderers.Length; ri++)
            {
                Renderer r = renderers[ri];
                Mesh mesh = GetMesh(r);
                string path = AnimationUtility.CalculateTransformPath(r.transform, root.transform);
                string rendererBounds = "center=" + root.transform.InverseTransformPoint(r.bounds.center).ToString("F4") + " size=" + r.bounds.size.ToString("F4");
                report.AppendLine($"R{ri} path={path} type={r.GetType().Name} enabled={r.enabled} active={r.gameObject.activeInHierarchy} {rendererBounds}");
                report.AppendLine($"  transform localPos={r.transform.localPosition:F4} localEuler={r.transform.localEulerAngles:F2} localScale={r.transform.localScale:F4}");

                Material[] mats = r.sharedMaterials ?? Array.Empty<Material>();
                if (mesh == null)
                {
                    report.AppendLine("  mesh=NULL mats=" + mats.Length);
                    continue;
                }

                report.AppendLine($"  mesh={mesh.name} meshAsset={AssetDatabase.GetAssetPath(mesh)} verts={mesh.vertexCount} submeshes={mesh.subMeshCount} readable={mesh.isReadable}");
                int slots = Mathf.Max(mesh.subMeshCount, mats.Length);
                for (int si = 0; si < slots; si++)
                {
                    Material m = si < mats.Length ? mats[si] : null;
                    string matPath = m != null ? AssetDatabase.GetAssetPath(m) : "NULL";
                    string tex = TextureName(m);
                    string texPath = TexturePath(m);
                    string classification = ClassifyTexture(m);
                    string topo = si < mesh.subMeshCount ? mesh.GetTopology(si).ToString() : "NO_SUBMESH";
                    long indices = si < mesh.subMeshCount ? (long)mesh.GetIndexCount(si) : 0;
                    int tris = si < mesh.subMeshCount && mesh.GetTopology(si) == MeshTopology.Triangles ? (int)(indices / 3) : 0;
                    string sb = TrySubmeshBounds(r, mesh, si, root.transform, out Bounds b)
                        ? " center=" + b.center.ToString("F4") + " size=" + b.size.ToString("F4")
                        : "";
                    report.AppendLine($"  S{si} topo={topo} indices={indices} tris={tris}{sb} mat={(m != null ? m.name : "NULL")} matAsset={matPath} tex={tex} texAsset={texPath} class={classification}");
                }
            }

            report.AppendLine();
            report.AppendLine("--- TRANSFORM TREE (names matter for animation) ---");
            foreach (Transform t in wrapper.GetComponentsInChildren<Transform>(true))
            {
                string p = AnimationUtility.CalculateTransformPath(t, root.transform);
                report.AppendLine($"T path={p} activeSelf={t.gameObject.activeSelf} localPos={t.localPosition:F4} localEuler={t.localEulerAngles:F2} localScale={t.localScale:F4}");
            }

            report.AppendLine();
            report.AppendLine("--- ANIMATOR / CLIP BINDINGS ---");
            Animator[] animators = wrapper.GetComponentsInChildren<Animator>(true);
            if (animators.Length == 0) report.AppendLine("No Animator components under wrapper.");
            for (int ai = 0; ai < animators.Length; ai++)
            {
                Animator a = animators[ai];
                string ap = AnimationUtility.CalculateTransformPath(a.transform, root.transform);
                RuntimeAnimatorController c = a.runtimeAnimatorController;
                report.AppendLine($"Animator{ai} path={ap} controller={(c != null ? c.name : "NULL")} asset={(c != null ? AssetDatabase.GetAssetPath(c) : "NULL")}");
                if (c == null) continue;
                foreach (AnimationClip clip in c.animationClips.Where(x => x != null).Distinct())
                {
                    report.AppendLine($"  Clip name={clip.name} asset={AssetDatabase.GetAssetPath(clip)} length={clip.length:F3} frameRate={clip.frameRate:F1}");
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    foreach (var g in bindings.GroupBy(x => x.path).OrderBy(x => x.Key))
                    {
                        string props = string.Join(",", g.Select(x => x.propertyName).Distinct().OrderBy(x => x));
                        report.AppendLine($"    BIND path={g.Key} type={string.Join(",", g.Select(x => x.type != null ? x.type.Name : "NULL").Distinct())} props={props}");
                    }
                    foreach (var ob in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                        report.AppendLine($"    OBJ_BIND path={ob.path} type={(ob.type != null ? ob.type.Name : "NULL")} prop={ob.propertyName}");
                }
            }

            report.AppendLine();
            report.AppendLine("--- LEGACY ANIMATION COMPONENTS ---");
            foreach (Animation a in wrapper.GetComponentsInChildren<Animation>(true))
            {
                string p = AnimationUtility.CalculateTransformPath(a.transform, root.transform);
                report.AppendLine("Animation path=" + p);
                foreach (AnimationState s in a) if (s != null && s.clip != null)
                    report.AppendLine($"  state={s.name} clip={s.clip.name} asset={AssetDatabase.GetAssetPath(s.clip)}");
            }

            report.AppendLine();
            report.AppendLine("--- GENERATED RAFT ASSETS CURRENTLY PRESENT ---");
            const string generatedFolder = "Assets/_Game/Boats/Raft/UserTextures";
            if (AssetDatabase.IsValidFolder(generatedFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("", new[] { generatedFolder }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    Object o = AssetDatabase.LoadMainAssetAtPath(p);
                    if (o is Mesh gm)
                        report.AppendLine($"GEN MESH {p} name={gm.name} verts={gm.vertexCount} submeshes={gm.subMeshCount}");
                    else if (o is Material mm)
                        report.AppendLine($"GEN MAT {p} name={mm.name} tex={TextureName(mm)} class={ClassifyTexture(mm)}");
                    else if (o is Texture)
                        report.AppendLine($"GEN TEX {p} name={o.name}");
                }
            }

            report.AppendLine();
            report.AppendLine("--- AUTHORED RAFT.BLEND RENDERERS ---");
            GameObject authored = AssetDatabase.LoadAssetAtPath<GameObject>(AuthoredBlendPath);
            if (authored == null) report.AppendLine("Raft.blend could not be loaded as GameObject.");
            else
            {
                Renderer[] ars = authored.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < ars.Length; i++)
                {
                    Renderer r = ars[i];
                    Mesh m = GetMesh(r);
                    string p = AnimationUtility.CalculateTransformPath(r.transform, authored.transform);
                    report.AppendLine($"AUTH R{i} path={p} mesh={(m != null ? m.name : "NULL")} verts={(m != null ? m.vertexCount : 0)} submeshes={(m != null ? m.subMeshCount : 0)} mats={string.Join(",", (r.sharedMaterials ?? Array.Empty<Material>()).Select(x => x != null ? x.name + "/" + TextureName(x) : "NULL"))}");
                }
            }
        }
        catch (Exception e)
        {
            report.AppendLine("EXCEPTION: " + e);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Finish(report);
    }

    private static void Finish(StringBuilder report)
    {
        string text = report.ToString();
        string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        string path = Path.Combine(root, "RaftV20Diagnostic.txt");
        try { File.WriteAllText(path, text); } catch (Exception e) { Debug.LogError("[RAFT V20] Could not write report file: " + e.Message); }
        EditorGUIUtility.systemCopyBuffer = text;

        string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
        foreach (string line in lines)
            if (!string.IsNullOrWhiteSpace(line)) Debug.Log("[RAFT V20 MAP] " + line.TrimEnd('\r'));

        Debug.Log("[RAFT V20] DONE — NO MATERIALS OR MESHES WERE CHANGED. Full report copied to clipboard and written to: " + path);
        EditorUtility.DisplayDialog("Raft v20 Diagnostic Complete",
            "Nothing was changed. The full report is already copied to your clipboard. Just paste it into ChatGPT.\n\nA copy was also written to:\n" + path,
            "OK");
    }

    private static string ClassifyTexture(Material m)
    {
        if (m == null) return "NULL";
        string n = (m.name ?? "") + " " + TextureName(m) + " " + TexturePath(m);
        if (n.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0) return "OAR";
        if (n.IndexOf("raft", StringComparison.OrdinalIgnoreCase) >= 0) return "RAFT";
        return "OTHER";
    }

    private static string TextureName(Material m)
    {
        Texture t = GetTexture(m);
        return t != null ? t.name : "none";
    }

    private static string TexturePath(Material m)
    {
        Texture t = GetTexture(m);
        return t != null ? AssetDatabase.GetAssetPath(t) : "none";
    }

    private static Texture GetTexture(Material m)
    {
        if (m == null) return null;
        Texture t = m.mainTexture;
        if (t == null && m.HasProperty("_BaseMap")) t = m.GetTexture("_BaseMap");
        if (t == null && m.HasProperty("_MainTex")) t = m.GetTexture("_MainTex");
        return t;
    }

    private static bool TrySubmeshBounds(Renderer renderer, Mesh mesh, int slot, Transform root, out Bounds bounds)
    {
        bounds = default;
        if (mesh == null || slot < 0 || slot >= mesh.subMeshCount) return false;
        Vector3[] verts;
        int[] idx;
        try { verts = mesh.vertices; idx = mesh.GetIndices(slot); }
        catch { return false; }
        bool have = false;
        foreach (int i in idx)
        {
            if (i < 0 || i >= verts.Length) continue;
            Vector3 p = root.InverseTransformPoint(renderer.transform.TransformPoint(verts[i]));
            if (!have) { bounds = new Bounds(p, Vector3.zero); have = true; }
            else bounds.Encapsulate(p);
        }
        return have;
    }

    private static Mesh GetMesh(Renderer renderer)
    {
        if (renderer == null) return null;
        if (renderer is SkinnedMeshRenderer smr) return smr.sharedMesh;
        MeshFilter mf = renderer.GetComponent<MeshFilter>();
        return mf != null ? mf.sharedMesh : null;
    }
}
