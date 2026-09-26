using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Connects the animation already authored inside the user's Raft.blend to the
/// starter raft. No animation curves or timing are recreated here.
/// </summary>
public static class RaftPaddleAnimationSetup
{
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string BlendPath = "Assets/_Game/Boats/Raft/Authored/Raft.blend";
    private const string WrapperPath = "ModelContainer/Uploaded Raft Model";
    private const string MarkerName = "RaftAuthoredPaddleAnimation_v1";

    [InitializeOnLoadMethod]
    private static void AutoApply()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += ApplyIfNeeded;
    }

    [MenuItem("Tools/Open World/Apply Authored Raft Paddle Animation")]
    private static void ApplyOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft Paddle Animation", "Exit Play Mode first.", "OK");
            return;
        }

        if (!ApplyIfNeeded(true))
            EditorUtility.DisplayDialog(
                "Raft Paddle Animation",
                "The authored raft or its animation clip is not available yet. Apply the uploaded raft model first, then retry.",
                "OK"
            );
    }

    private static void ApplyIfNeeded()
    {
        ApplyIfNeeded(false);
    }

    private static bool ApplyIfNeeded(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject blend = AssetDatabase.LoadAssetAtPath<GameObject>(BlendPath);
        if (prefab == null || blend == null) return false;

        Transform prefabWrapper = prefab.transform.Find(WrapperPath);
        RaftPaddleAnimator existing = prefab.GetComponent<RaftPaddleAnimator>();
        if (!force && prefabWrapper != null && prefabWrapper.Find(MarkerName) != null &&
            existing != null && existing.PaddleClip != null && existing.TargetAnimator != null)
            return false;

        AnimationClip clip = FindAuthoredPaddleClip(blend);
        if (clip == null)
        {
            Debug.LogWarning("Raft paddle animation setup found no usable AnimationClip inside the supplied Raft.blend. No boat settings were changed.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform wrapper = root.transform.Find(WrapperPath);
            if (wrapper == null) return false;

            Transform exactModel = FindAuthoredModelRoot(wrapper);
            if (exactModel == null) return false;

            Animator animator = exactModel.GetComponent<Animator>();
            if (animator == null) animator = exactModel.gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;

            RaftPaddleAnimator paddleAnimator = root.GetComponent<RaftPaddleAnimator>();
            if (paddleAnimator == null) paddleAnimator = root.AddComponent<RaftPaddleAnimator>();
            paddleAnimator.TargetAnimator = animator;
            paddleAnimator.PaddleClip = clip;
            paddleAnimator.MinimumForwardSpeed = 0.04f;
            EditorUtility.SetDirty(paddleAnimator);

            Transform oldMarker = wrapper.Find(MarkerName);
            if (oldMarker != null) UnityEngine.Object.DestroyImmediate(oldMarker.gameObject);
            GameObject marker = new GameObject(MarkerName);
            marker.transform.SetParent(wrapper, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            Debug.Log("Raft paddle animation connected to supplied clip '" + clip.name + "'. The authored animation plays at 1x only during active forward raft movement and resets to its authored first frame when propulsion stops.");
            if (force)
                EditorUtility.DisplayDialog(
                    "Raft Paddle Animation Ready",
                    "Using the animation embedded in your Raft.blend exactly as authored. It plays only while the raft is actively moving forward.",
                    "OK"
                );
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform FindAuthoredModelRoot(Transform wrapper)
    {
        for (int i = 0; i < wrapper.childCount; i++)
        {
            Transform child = wrapper.GetChild(i);
            if (child == null) continue;
            if (child.GetComponentInChildren<Renderer>(true) != null) return child;
        }
        return null;
    }

    private static AnimationClip FindAuthoredPaddleClip(GameObject blendRoot)
    {
        Transform paddle = FindPaddleTransform(blendRoot.transform);
        string paddlePath = paddle != null ? AnimationUtility.CalculateTransformPath(paddle, blendRoot.transform) : string.Empty;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(BlendPath);
        AnimationClip best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;
            if (clip.length <= 0.001f) continue;

            float score = Mathf.Min(clip.length, 60f) * 0.01f;
            if (clip.name.IndexOf("take", StringComparison.OrdinalIgnoreCase) >= 0) score += 4f;
            if (clip.name.IndexOf("action", StringComparison.OrdinalIgnoreCase) >= 0) score += 2f;

            EditorCurveBinding[] curves = AnimationUtility.GetCurveBindings(clip);
            for (int b = 0; b < curves.Length; b++)
            {
                string path = curves[b].path ?? string.Empty;
                if (PathsRelated(path, paddlePath)) score += 100f;
                else if (path.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         path.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 50f;
            }

            EditorCurveBinding[] objectCurves = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            for (int b = 0; b < objectCurves.Length; b++)
            {
                string path = objectCurves[b].path ?? string.Empty;
                if (PathsRelated(path, paddlePath)) score += 100f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = clip;
            }
        }

        return best;
    }

    private static bool PathsRelated(string animatedPath, string paddlePath)
    {
        if (string.IsNullOrEmpty(paddlePath)) return false;
        if (string.Equals(animatedPath, paddlePath, StringComparison.Ordinal)) return true;
        if (!string.IsNullOrEmpty(animatedPath) && paddlePath.StartsWith(animatedPath + "/", StringComparison.Ordinal)) return true;
        if (!string.IsNullOrEmpty(animatedPath) && animatedPath.StartsWith(paddlePath + "/", StringComparison.Ordinal)) return true;
        return false;
    }

    private static Transform FindPaddleTransform(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Renderer best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            string name = renderer.name ?? string.Empty;
            if (name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0)
                return renderer.transform;

            Material[] materials = renderer.sharedMaterials;
            for (int m = 0; materials != null && m < materials.Length; m++)
            {
                Material material = materials[m];
                if (material == null) continue;
                string materialName = material.name ?? string.Empty;
                if (materialName.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("paddle", StringComparison.OrdinalIgnoreCase) >= 0)
                    return renderer.transform;
            }

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

        return best != null ? best.transform : null;
    }
}
