using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Restores the two treble-hook meshes from the original proven-working lure FBX
/// while leaving the corrected body mesh/UVs, animation transforms, lighting,
/// LineAttach, materials, scale and runtime movement completely untouched.
///
/// The corrected STL/Blend swap only needed the corrected body/UVs. Re-baking the
/// hook geometry into the animated transforms could offset the belly hook. The
/// original hook meshes already matched those transforms exactly, so this repair
/// puts those exact hook meshes back on the existing animated hook transforms.
/// </summary>
public static class LiplessCrankbaitHookAttachmentRepair
{
    private const string PrefabPath = "Assets/Resources/Fishing/LiplessCrankbaitGreenStriped.prefab";
    private const string OriginalFbxPath = "Assets/_Game/Fishing/LiplessCrankbait/Source/LiplessCrankbaitGreenStriped.fbx";

    [InitializeOnLoadMethod]
    private static void AutoRepairAfterCompile()
    {
        EditorApplication.delayCall += () => Repair(false);
    }

    [MenuItem("Tools/Open World/Repair Lipless Crankbait Hook Attachment (One Click)")]
    public static void RepairOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Crankbait Hook Repair", "Exit Play Mode first.", "OK");
            return;
        }

        bool changed = Repair(true);
        if (changed)
        {
            EditorUtility.DisplayDialog(
                "Crankbait Hook Repaired",
                "Restored the original belly/tail hook meshes onto the existing animated hook transforms. The corrected lure body/texture, animation, LineAttach, lighting, size and movement were left unchanged.",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Crankbait Hook Repair",
                "The hook meshes are already attached to the original animated hook geometry, or the working lure source has not been imported yet.",
                "OK");
        }
    }

    private static bool Repair(bool logProblems)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return false;

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalFbxPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (source == null || prefab == null)
        {
            if (logProblems)
                Debug.LogWarning("Crankbait hook repair skipped because the original FBX or working prefab is missing.");
            return false;
        }

        MeshFilter[] sourceFilters = source.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f != null && f.sharedMesh != null)
            .ToArray();
        if (sourceFilters.Length != 3)
        {
            if (logProblems)
                Debug.LogWarning("Crankbait hook repair expected body + two hooks in the original FBX, but found " + sourceFilters.Length + " mesh parts.");
            return false;
        }

        Dictionary<string, MeshFilter> sourceByName = BuildMap(sourceFilters);
        if (sourceByName.Count != 3)
            return false;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform authoredModel = FindDeepChild(root.transform, "AuthoredModel");
            if (authoredModel == null)
                return false;

            MeshFilter[] targetFilters = authoredModel.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f != null && f.sharedMesh != null && !IsUnderLightingRig(f.transform, authoredModel))
                .ToArray();
            if (targetFilters.Length != 3)
            {
                if (logProblems)
                    Debug.LogWarning("Crankbait hook repair expected three live lure mesh parts, but found " + targetFilters.Length + ".");
                return false;
            }

            MeshFilter body = FindLargestPart(targetFilters);
            if (body == null)
                return false;

            Transform lineAttach = FindDeepChild(root.transform, "LineAttach");
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (lineAttach == null || animator == null || animator.runtimeAnimatorController == null)
                return false;

            Transform lineParent = lineAttach.parent;
            Vector3 linePosition = lineAttach.localPosition;
            Quaternion lineRotation = lineAttach.localRotation;
            Vector3 lineScale = lineAttach.localScale;
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            Vector3 rootPosition = root.transform.localPosition;
            Quaternion rootRotation = root.transform.localRotation;
            Vector3 rootScale = root.transform.localScale;

            Dictionary<Transform, TransformSnapshot> snapshots = new Dictionary<Transform, TransformSnapshot>();
            for (int i = 0; i < targetFilters.Length; i++)
                snapshots[targetFilters[i].transform] = new TransformSnapshot(targetFilters[i].transform);

            bool changed = false;
            for (int i = 0; i < targetFilters.Length; i++)
            {
                MeshFilter target = targetFilters[i];
                if (target == body)
                    continue; // corrected body/UVs stay exactly as they are

                string key = Normalize(target.transform.name);
                if (!sourceByName.TryGetValue(key, out MeshFilter originalHook))
                {
                    if (logProblems)
                        Debug.LogWarning("Could not match original hook mesh for animated transform '" + target.transform.name + "'.");
                    return false;
                }

                if (target.sharedMesh != originalHook.sharedMesh)
                {
                    target.sharedMesh = originalHook.sharedMesh;
                    changed = true;
                }
            }

            // Hard invariants: this repair is mesh-reference-only.
            if (lineAttach.parent != lineParent ||
                !Approximately(lineAttach.localPosition, linePosition) ||
                Quaternion.Angle(lineAttach.localRotation, lineRotation) > 0.0001f ||
                !Approximately(lineAttach.localScale, lineScale))
                throw new InvalidOperationException("Hook repair attempted to alter LineAttach. Save aborted.");

            Animator animatorAfter = root.GetComponentInChildren<Animator>(true);
            if (animatorAfter == null || animatorAfter.runtimeAnimatorController != controller)
                throw new InvalidOperationException("Hook repair attempted to alter the lure Animator/controller. Save aborted.");

            if (!Approximately(root.transform.localPosition, rootPosition) ||
                Quaternion.Angle(root.transform.localRotation, rootRotation) > 0.0001f ||
                !Approximately(root.transform.localScale, rootScale))
                throw new InvalidOperationException("Hook repair attempted to alter lure root transform. Save aborted.");

            foreach (var pair in snapshots)
                if (!pair.Value.Matches(pair.Key))
                    throw new InvalidOperationException("Hook repair attempted to alter animated transform '" + pair.Key.name + "'. Save aborted.");

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Lipless crankbait: restored original belly/tail hook meshes; corrected body remains unchanged.");
            }

            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static MeshFilter FindLargestPart(MeshFilter[] filters)
    {
        MeshFilter best = null;
        float bestScore = -1f;
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter == null || filter.sharedMesh == null) continue;
            Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, Abs(filter.transform.lossyScale));
            float score = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) *
                          Mathf.Max(0.000001f, Mathf.Abs(size.x * size.y * size.z));
            if (score > bestScore)
            {
                bestScore = score;
                best = filter;
            }
        }
        return best;
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static Dictionary<string, MeshFilter> BuildMap(IEnumerable<MeshFilter> filters)
    {
        var map = new Dictionary<string, MeshFilter>(StringComparer.OrdinalIgnoreCase);
        foreach (MeshFilter filter in filters)
        {
            string key = Normalize(filter.transform.name);
            if (map.ContainsKey(key))
                return new Dictionary<string, MeshFilter>();
            map[key] = filter;
        }
        return map;
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty).Trim().Replace(" ", string.Empty);
    }

    private static bool IsUnderLightingRig(Transform transform, Transform authoredModel)
    {
        Transform current = transform;
        while (current != null && current != authoredModel)
        {
            if (current.name == "AuthoredLightingRig") return true;
            current = current.parent;
        }
        return false;
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= 0.0000000001f;
    }

    private readonly struct TransformSnapshot
    {
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly Vector3 scale;

        public TransformSnapshot(Transform transform)
        {
            position = transform.localPosition;
            rotation = transform.localRotation;
            scale = transform.localScale;
        }

        public bool Matches(Transform transform)
        {
            return Approximately(transform.localPosition, position) &&
                   Quaternion.Angle(transform.localRotation, rotation) <= 0.0001f &&
                   Approximately(transform.localScale, scale);
        }
    }
}
