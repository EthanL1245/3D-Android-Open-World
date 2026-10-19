using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Runs after compilation/import on the developer's Unity machine. The original
// Sailboat asset ID and prefab path are preserved for existing inventory saves,
// shop catalog references and network boat-kind snapshots.
[InitializeOnLoad]
public static class MotorboatSailboatInstaller
{
    private const string SourcePath = "Assets/Manually Added Assets/Motorboat_Reference.fbx";
    private const string PrefabPath = "Assets/Resources/Boats/Sailboat.prefab";
    private const string ControllerPath = "Assets/Resources/Boats/MotorboatPropeller.controller";
    private const string HullMaterialPath = "Assets/Manually Added Assets/Materials/Motorboat_Hull_Flake.mat";
    private const string CarpetMaterialPath = "Assets/Manually Added Assets/Materials/Motorboat_Deck_Carpet.mat";

    static MotorboatSailboatInstaller()
    {
        EditorApplication.delayCall += AutoInstall;
    }

    private static void AutoInstall()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoInstall;
            return;
        }
        try { Install(false); }
        catch(Exception e) { Debug.LogError("Motorboat automatic setup failed: " + e); }
    }

    // Also installs in batch-mode builds where Unity may skip the editor delay call.
    internal static void InstallBeforeBuild() { Install(false); }

    [MenuItem("Tools/Open World/Install Motorboat (Replaces Sailboat)")]
    public static void InstallFromMenu()
    {
        Install(true);
    }

    private static void Install(bool explicitRequest)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if(explicitRequest) Debug.LogWarning("Exit Play Mode before installing the motorboat.");
            return;
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(model == null || prefab == null)
        {
            if(explicitRequest) Debug.LogError("Missing motorboat FBX or Sailboat prefab; check the Manually Added Assets folder.");
            return;
        }
        if(prefab.transform.Find("ModelContainer/ImportedMotorboat") != null)
        {
            if(explicitRequest) Debug.Log("The motorboat is already installed in Sailboat.prefab.");
            return;
        }

        // Loop the imported motor/propeller animation for as long as the boat moves.
        var importer = AssetImporter.GetAtPath(SourcePath) as ModelImporter;
        if(importer != null)
        {
            var importedClips = importer.clipAnimations;
            if(importedClips == null || importedClips.Length == 0)
                importedClips = importer.defaultClipAnimations;
            bool needsLooping = false;
            foreach(var clip in importedClips)
            {
                if(clip.loopTime) continue;
                clip.loopTime = true;
                needsLooping = true;
            }
            if(needsLooping)
            {
                importer.clipAnimations = importedClips;
                importer.SaveAndReimport();
                model = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            }
        }

        var animation = AssetDatabase.LoadAllAssetsAtPath(SourcePath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(clip => ClipPriority(clip.name))
            .FirstOrDefault();
        var animationController = animation != null ? CreateAnimatorController(animation) : null;

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var boat = root.GetComponent<BoatController>();
            var visuals = root.transform.Find("ModelContainer");
            if(boat == null || visuals == null) throw new InvalidOperationException("The original sailboat's boat controller/model container is missing.");

            // Delete only the sailboat's old decorative primitives; its deck,
            // collision hull, water clearance and boat physics are retained.
            while(visuals.childCount > 0)
                Object.DestroyImmediate(visuals.GetChild(0).gameObject);

            var instance = PrefabUtility.InstantiatePrefab(model, visuals) as GameObject;
            if(instance == null) throw new InvalidOperationException("Unity could not instantiate the supplied motorboat FBX.");
            instance.name = "ImportedMotorboat";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if(!TryBounds(instance, out Bounds original))
                throw new InvalidOperationException("Motorboat FBX has no visible mesh renderers.");
            // The original hull and clearance volume are 4 x 7 metres.
            // Rotate an X-long export to the game's +Z-forward boat convention.
            if(original.size.x > original.size.z * 1.15f)
            {
                instance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                TryBounds(instance, out original);
            }
            float fit = Mathf.Min(3.85f / Mathf.Max(0.01f, original.size.x),
                                  6.8f / Mathf.Max(0.01f, original.size.z));
            fit = Mathf.Clamp(fit, 0.01f, 100f);
            instance.transform.localScale = Vector3.one * fit;
            // Keep the propeller below the water line while locating the deck
            // above the original walkable platform at y = 0.55.
            if(!TryBounds(instance, out Bounds scaled))
                throw new InvalidOperationException("Failed to size imported motorboat.");
            instance.transform.localPosition =
                new Vector3(-scaled.center.x, -0.55f - scaled.min.y, -scaled.center.z);

            // Keep walkable/collision primitives but don't draw the old boat's
            // simplified deck and gunwales through the replacement model.
            foreach(Transform child in root.transform)
            {
                if(child.name != "Walkable deck" && child.name != "Low gunwale") continue;
                var renderer = child.GetComponent<Renderer>();
                if(renderer != null) renderer.enabled = false;
            }

            RemapMaterials(instance);
            PositionDriverBehindWheel(boat, instance);

            var animator = instance.GetComponent<Animator>() ?? instance.GetComponentInChildren<Animator>(true);
            if(animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = animationController;
            animator.applyRootMotion = false;
            var engine = root.GetComponent<MotorboatEngineAnimation>() ?? root.AddComponent<MotorboatEngineAnimation>();
            engine.MotorAnimator = animator;
            engine.FallbackPropeller = instance.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t != instance.transform &&
                    t.name.IndexOf("propeller", StringComparison.OrdinalIgnoreCase) >= 0);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log(animation == null
                ? "Motorboat model installed, but no imported motor animation clip was found. Check Motorboat_Reference.fbx Animation import."
                : "Motorboat installed: Sailboat.prefab now uses Motorboat_Reference.fbx and loops the '" + animation.name + "' motor animation while moving.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

    private static int ClipPriority(string name)
    {
        string lower = name.ToLowerInvariant();
        if(lower.Contains("propeller") || lower.Contains("propell")) return 5;
        if(lower.Contains("motor") && (lower.Contains("spin") || lower.Contains("run"))) return 5;
        if(lower.Contains("engine")) return 4;
        if(lower.Contains("motor")) return 3;
        if(lower.Contains("spin")) return 2;
        return 1; // Blender often exports a single clip named "Take 001".
    }

    private static AnimatorController CreateAnimatorController(AnimationClip clip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if(controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        AnimatorState state = null;
        foreach(var child in machine.states)
            if(child.state.name == "Motor Running") { state = child.state; break; }
        if(state == null) state = machine.AddState("Motor Running");
        state.motion = clip;
        machine.defaultState = state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static bool TryBounds(GameObject model, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if(!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
            if(!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found && bounds.size.sqrMagnitude > 0.001f;
    }

    private static void RemapMaterials(GameObject imported)
    {
        var hull = AssetDatabase.LoadAssetAtPath<Material>(HullMaterialPath);
        var carpet = AssetDatabase.LoadAssetAtPath<Material>(CarpetMaterialPath);
        foreach(var renderer in imported.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for(int i = 0; i < materials.Length; i++)
            {
                if(materials[i] == null) continue;
                string name = materials[i].name.ToLowerInvariant();
                Material replacement = name.Contains("hull_flake") ? hull :
                                       name.Contains("deck_carpet") ? carpet : null;
                if(replacement == null || replacement == materials[i]) continue;
                materials[i] = replacement;
                changed = true;
            }
            if(changed) renderer.sharedMaterials = materials;
        }
    }

    private static void PositionDriverBehindWheel(BoatController boat, GameObject imported)
    {
        // Prefer the FBX steering wheel; its world position follows the model's
        // automatic scaling/centering. Fallback is the starboard cockpit position.
        Vector3 seat = new Vector3(0.7f, 0.57f, 0.6f);
        Transform wheel = imported.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.IndexOf("steering", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(t => t.name.IndexOf("steering", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
            .FirstOrDefault();
        if(wheel != null)
        {
            var wheelRenderer = wheel.GetComponent<Renderer>() ?? wheel.GetComponentInChildren<Renderer>();
            Vector3 position = wheelRenderer != null ? wheelRenderer.bounds.center : wheel.position;
            var local = boat.transform.InverseTransformPoint(position);
            seat = new Vector3(Mathf.Clamp(local.x, -1.3f, 1.3f),
                               0.57f,
                               Mathf.Clamp(local.z - 0.65f, -2.4f, 2.2f));
        }
        if(boat.DriverSeat != null) boat.DriverSeat.localPosition = seat;
    }
}

internal sealed class MotorboatBeforeBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        MotorboatSailboatInstaller.InstallBeforeBuild();
    }
}
