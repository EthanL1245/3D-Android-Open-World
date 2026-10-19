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
            UpgradeCockpitCollision();
            MotorboatWaterMaskInstaller.Install(PrefabPath);
            if(explicitRequest) Debug.Log("Motorboat size and cockpit collision are up to date.");
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

            root.transform.localScale = Vector3.one;

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

            root.transform.localScale = Vector3.one * 1.6f;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log(animation == null
                ? "Motorboat model installed, but no imported motor animation clip was found. Check Motorboat_Reference.fbx Animation import."
                : "Motorboat installed: Sailboat.prefab now uses Motorboat_Reference.fbx and loops the '" + animation.name + "' motor animation while moving.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        UpgradeCockpitCollision();
        MotorboatWaterMaskInstaller.Install(PrefabPath);
        AssetDatabase.SaveAssets();
    }

    // Bake a compound of merged solid columns from the actual model surface.
    // A single convex hull (or the old deck box) would seal the cockpit again.
    // Box colliders support the existing dynamic Rigidbody on host and clients;
    // all mesh processing happens once in the editor, never on a phone.
    private static void UpgradeCockpitCollision()
    {
        const string collisionName = "Motorboat surface collision v1";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(prefab.transform.Find(collisionName) != null) return;
        var importer = AssetImporter.GetAtPath(SourcePath) as ModelImporter;
        if(importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var imported = root.transform.Find("ModelContainer/ImportedMotorboat");
            if(imported == null) throw new InvalidOperationException("Motorboat model missing during collision bake.");
            const float cell = 0.10f;
            const float heightStep = 0.025f;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            foreach(var filter in imported.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if(mesh == null) continue;
                // Moving hardware must not become fixed collision over the cockpit.
                bool hardware = false;
                for(var t = filter.transform; t != imported && t != null; t = t.parent)
                {
                    string name = t.name.ToLowerInvariant();
                    if(name.Contains("propell") || name.Contains("steering") || name.Contains("wheel"))
                        hardware = true;
                }
                if(hardware) continue;
                int offset = vertices.Count;
                Matrix4x4 matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach(var vertex in mesh.vertices) vertices.Add(matrix.MultiplyPoint3x4(vertex));
                foreach(int index in mesh.triangles) triangles.Add(offset + index);
            }
            if(vertices.Count == 0) throw new InvalidOperationException("Motorboat has no readable static hull meshes.");
            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            foreach(var vertex in vertices) bounds.Encapsulate(vertex);
            int nx = Mathf.CeilToInt(bounds.size.x / cell);
            int nz = Mathf.CeilToInt(bounds.size.z / cell);
            float[,] heights = new float[nx, nz];
            for(int x = 0; x < nx; x++)
                for(int z = 0; z < nz; z++) heights[x,z] = float.NegativeInfinity;
            for(int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i+1]], c = vertices[triangles[i+2]];
                Vector3 normal = Vector3.Cross(b-a,c-a);
                if(normal.y <= 0.000001f) continue;
                int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x,c.x)-bounds.min.x)/cell),0,nx-1);
                int x1 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.x,b.x,c.x)-bounds.min.x)/cell),0,nx-1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z,b.z,c.z)-bounds.min.z)/cell),0,nz-1);
                int z1 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.z,b.z,c.z)-bounds.min.z)/cell),0,nz-1);
                float denominator = (b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                if(Mathf.Abs(denominator)<0.000001f) continue;
                for(int x=x0; x<=x1; x++)
                for(int z=z0; z<=z1; z++)
                {
                    float px=bounds.min.x+(x+0.5f)*cell, pz=bounds.min.z+(z+0.5f)*cell;
                    float u=((b.z-c.z)*(px-c.x)+(c.x-b.x)*(pz-c.z))/denominator;
                    float v=((c.z-a.z)*(px-c.x)+(a.x-c.x)*(pz-c.z))/denominator;
                    if(u<0f || v<0f || u+v>1f) continue;
                    float y=u*a.y+v*b.y+(1f-u-v)*c.y;
                    heights[x,z]=Mathf.Max(heights[x,z],Mathf.Round(y/heightStep)*heightStep);
                }
            }
            var collision = new GameObject(collisionName);
            collision.transform.SetParent(root.transform,false);
            bool[,] used = new bool[nx,nz];
            int count = 0;
            for(int x=0; x<nx; x++)
            for(int z=0; z<nz; z++)
            {
                float top=heights[x,z];
                if(used[x,z] || float.IsNegativeInfinity(top)) continue;
                int width=1, depth=1;
                while(x+width<nx && !used[x+width,z] && heights[x+width,z]==top) width++;
                bool extend=true;
                while(z+depth<nz && extend)
                {
                    for(int dx=0; dx<width; dx++)
                        if(used[x+dx,z+depth] || heights[x+dx,z+depth]!=top) { extend=false; break; }
                    if(extend) depth++;
                }
                for(int dx=0; dx<width; dx++)
                    for(int dz=0; dz<depth; dz++) used[x+dx,z+dz]=true;
                float bottom=Mathf.Min(bounds.min.y,top-0.05f);
                var box=collision.AddComponent<BoxCollider>();
                box.center=new Vector3(bounds.min.x+(x+width*0.5f)*cell,(top+bottom)*0.5f,
                                       bounds.min.z+(z+depth*0.5f)*cell);
                box.size=new Vector3(width*cell,top-bottom,depth*cell);
                count++;
            }
            if(count==0) throw new InvalidOperationException("Motorboat collision bake produced no surfaces.");
            // Disable every previous solid hull/deck collider only after the bake
            // succeeds. Keep any trigger volumes and all animation references.
            foreach(var collider in root.GetComponentsInChildren<Collider>(true))
                if(collider.gameObject!=collision && !collider.isTrigger) collider.enabled=false;
            var boat=root.GetComponent<BoatController>();
            SnapAnchorToSurface(boat.DriverSeat,root.transform,bounds,heights,cell);
            SnapAnchorToSurface(boat.DeckExit,root.transform,bounds,heights,cell);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            Debug.Log("Motorboat cockpit collision baked from model: " + count + " merged boxes.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void SnapAnchorToSurface(Transform anchor, Transform root, Bounds bounds, float[,] heights, float cell)
    {
        if(anchor==null) return;
        Vector3 point=root.InverseTransformPoint(anchor.position);
        int x=Mathf.Clamp(Mathf.FloorToInt((point.x-bounds.min.x)/cell),0,heights.GetLength(0)-1);
        int z=Mathf.Clamp(Mathf.FloorToInt((point.z-bounds.min.z)/cell),0,heights.GetLength(1)-1);
        float top=heights[x,z];
        if(float.IsNegativeInfinity(top))
            throw new InvalidOperationException("Motorboat boarding/driver anchor is outside the model surface: " + anchor.name);
        point.y=top+0.02f;
        anchor.position=root.TransformPoint(point);
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

