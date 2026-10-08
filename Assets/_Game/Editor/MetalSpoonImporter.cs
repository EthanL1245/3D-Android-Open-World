using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Installs pre-exported authored geometry/UVs/actions; no Blender or user setup required.</summary>
[InitializeOnLoad]
public static class MetalSpoonImporter
{
    private const string Folder = "Assets/_Game/Fishing/MetalSpoon";
    private const string Prefab = "Assets/Resources/Fishing/MetalSpoon.prefab";
    private static bool importing;
    [Serializable] private sealed class Export { public int version; public float duration, fps; public Vector3 lineAttach; public Part[] parts; }
    [Serializable] private sealed class Part { public string name; public Vector3[] vertices, normals; public Vector2[] uv; public int[] triangles; public Frame[] frames; }
    [Serializable] private sealed class Frame { public float time; public Vector3 position; public Quaternion rotation; }

    static MetalSpoonImporter()
    {
        EditorApplication.delayCall += AutoInstall;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try { EnsureInstalled(); }
            catch (Exception e) { EditorApplication.isPlaying = false; Debug.LogException(e); }
        };
    }

    private static void AutoInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += AutoInstall; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { EnsureInstalled(); }
        catch (Exception e) { Debug.LogError("[METAL SPOON] Install failed: " + e); }
    }

    [MenuItem("Tools/Open World/Rebuild Metal Spoon")]
    public static void Rebuild() { EnsureInstalled(true); }

    public static void EnsureInstalled(bool force = false)
    {
        if (importing) return;
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (!force && Ready(existing)) return;
        importing = true;
        GameObject root = null;
        try
        {
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "/AuthoredSpoon.json");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/SpoonTexture.png");
            if (source == null || texture == null) throw new InvalidDataException("Metal Spoon source or texture is missing.");
            Export data = JsonUtility.FromJson<Export>(source.text);
            if (data.version != 1 || data.parts == null || data.parts.Length != 2)
                throw new InvalidDataException("Unexpected Metal Spoon export.");
            string generated = Folder + "/Generated";
            if (!AssetDatabase.IsValidFolder(generated)) AssetDatabase.CreateFolder(Folder, "Generated");
            Shader shader = Resources.Load<Shader>("Fishing/FishingEquipment");
            if (shader == null) throw new InvalidOperationException("FishingEquipment shader is missing.");
            Material bodyMaterial = Save(new Material(shader) { name = "MetalSpoonSilver" }, generated + "/Body.mat");
            bodyMaterial.SetTexture("_BaseMap", texture);
            bodyMaterial.SetColor("_BaseColor", Color.white);
            bodyMaterial.SetFloat("_Metallic", .95f);
            bodyMaterial.SetFloat("_Smoothness", .88f);
            EditorUtility.SetDirty(bodyMaterial);
            Material hookMaterial = Save(new Material(shader) { name = "MetalSpoonHook" }, generated + "/Hook.mat");
            hookMaterial.SetColor("_BaseColor", new Color(.7f, .73f, .77f));
            hookMaterial.SetFloat("_Metallic", .95f);
            hookMaterial.SetFloat("_Smoothness", .8f);
            EditorUtility.SetDirty(hookMaterial);

            root = new GameObject("MetalSpoon");
            Transform model = Child(root.transform, "AuthoredModel", Vector3.zero, Quaternion.identity);
            AnimationClip clip = new AnimationClip { name = "MetalSpoonRetrieve", frameRate = data.fps, legacy = false };
            foreach (Part part in data.parts)
            {
                if (part.vertices.Length != part.uv.Length || part.vertices.Length != part.normals.Length || part.frames.Length < 2)
                    throw new InvalidDataException("Invalid authored mesh/animation for " + part.name);
                Mesh mesh = new Mesh { name = part.name, vertices = part.vertices, normals = part.normals,
                    uv = part.uv, triangles = part.triangles };
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                mesh = Save(mesh, generated + "/" + part.name + ".asset");
                Transform piece = Child(model, part.name, part.frames[0].position, part.frames[0].rotation);
                piece.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                piece.gameObject.AddComponent<MeshRenderer>().sharedMaterial = part.name == "SpoonBody" ? bodyMaterial : hookMaterial;
                for (int axis = 0; axis < 3; axis++)
                {
                    int component = axis;
                    SetCurve(clip, part, "m_LocalPosition." + "xyz"[axis], f => f.position[component]);
                }
                for (int axis = 0; axis < 4; axis++)
                {
                    int component = axis;
                    SetCurve(clip, part, "m_LocalRotation." + "xyzw"[axis], f => f.rotation[component]);
                }
                if (part.name == "SpoonBody")
                {
                    // Export is relative to this body's pivot; anchor follows its authored motion.
                    Child(piece, "LineAttach", data.lineAttach, Quaternion.identity);
                    Child(piece, "RetrieveFrame", Vector3.zero, Quaternion.identity);
                    // Present the spoon's broad face in shop, index and equipped-lure previews.
                    Child(piece, "PreviewFrame", Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.right));
                }
            }
            clip.EnsureQuaternionContinuity();
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true; settings.startTime = 0f; settings.stopTime = data.duration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip = Save(clip, generated + "/Retrieve.anim");
            string controllerPath = generated + "/Retrieve.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.Length > 0 ? machine.states[0].state : machine.AddState("Retrieve");
            state.motion = clip; state.speed = 1f; machine.defaultState = state;
            EditorUtility.SetDirty(state); EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
            Animator animator = model.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            AssetDatabase.SaveAssets();
            if (!Ready(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)))
                throw new InvalidOperationException("Metal Spoon prefab failed validation.");
            Debug.Log("[METAL SPOON] Installed original UVs, texture, body + hook actions and non-hook line eye. Deep Flash slot/stats retained.");
        }
        finally { if (root != null) Object.DestroyImmediate(root); importing = false; }
    }

    private static bool Ready(GameObject prefab)
    {
        if (prefab == null) return false;
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null ||
            animator.runtimeAnimatorController.animationClips.Length == 0) return false;
        MeshFilter[] meshes = prefab.GetComponentsInChildren<MeshFilter>(true);
        return meshes.Length == 2 && meshes.All(m => m.sharedMesh != null) &&
            prefab.transform.Find("AuthoredModel/SpoonBody/LineAttach") != null &&
            prefab.transform.Find("AuthoredModel/SpoonBody/RetrieveFrame") != null &&
            prefab.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterial != null);
    }

    private static Transform Child(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false); child.localPosition = position; child.localRotation = rotation;
        return child;
    }

    private static T Save<T>(T value, string path) where T : Object
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
        EditorUtility.CopySerialized(value, existing); Object.DestroyImmediate(value);
        EditorUtility.SetDirty(existing); return existing;
    }

    private static void SetCurve(AnimationClip clip, Part part, string property, Func<Frame, float> value)
    {
        AnimationCurve curve = new AnimationCurve(part.frames.Select(f => new Keyframe(f.time, value(f))).ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(part.name, typeof(Transform), property), curve);
    }
}

// Build & Run also installs/validates in batch mode before collecting Resources.
public sealed class MetalSpoonBuildPreparation : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report) { MetalSpoonImporter.EnsureInstalled(); }
}
