using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Builds native Unity assets from the committed, evaluated Blender export.</summary>
public static class FishingRodReelSetup
{
    private const string Root = "Assets/_Game/Fishing/RodReel";
    private const string Generated = Root + "/Generated";
    private const string PrefabPath = "Assets/Resources/Fishing/FishingRodReel.prefab";

    [Serializable] private sealed class Source
    {
        public int version;
        public float duration;
        public float[] tip, reelMount;
        public Part[] parts;
    }
    [Serializable] private sealed class Part
    {
        public string name, group;
        public float[] position, vertices, normals, uv;
        public int[] triangles;
        public Frame[] frames;
    }
    [Serializable] private sealed class Frame
    {
        public float time;
        public float[] position, rotation;
    }

    [MenuItem("Tools/Open World/Install Fishing Rod + Animated Reel (One Click)")]
    public static void Install()
    {
        GameObject assembly = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before installing the rod and reel.");
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Source/RodReel.json");
            if (json == null) throw new InvalidOperationException("Missing RodReel.json. Pull all files from GitHub first.");
            Source source = JsonUtility.FromJson<Source>(json.text);
            ValidateSource(source);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The project's URP Lit shader is unavailable.");
            EnsureFolder(Generated);
            EnsureFolder("Assets/Resources/Fishing");
            var rodTexture = LoadTexture("RodTexture");
            var reelTexture = LoadTexture("ReelTexture");
            var rodMaterial = MakeMaterial("RodWood", shader, rodTexture, 0f, 0.32f);
            // Texture includes both silver and black plastic; a low metallic value
            // keeps the dark plastic readable without a fabricated metalness mask.
            var reelMaterial = MakeMaterial("ReelSilverBlack", shader, reelTexture, 0.25f, 0.55f);
            var seatMaterial = MakeMaterial("ReelSeat", shader, null, 0.3f, 0.4f);
            seatMaterial.SetColor("_BaseColor", new Color(0.045f, 0.045f, 0.05f, 1f));
            EditorUtility.SetDirty(seatMaterial);

            assembly = new GameObject("FishingRodReel");
            Transform rod = Child("Rod", assembly.transform);
            Transform reel = Child("ReelMount", assembly.transform);
            reel.localPosition = Vec(source.reelMount);
            foreach (Part part in source.parts)
            {
                Transform node = Child(part.name, part.group == "Rod" ? rod : reel);
                node.localPosition = Vec(part.position);
                Mesh mesh = MakeMesh(part);
                node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = node.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = part.group == "Rod" ? rodMaterial : reelMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            AddSeatCollar(rod, 0.126f, seatMaterial);
            AddSeatCollar(rod, 0.195f, seatMaterial);
            Transform tip = Child("RodTip", rod);
            tip.localPosition = Vec(source.tip);

            AnimationClip clip = MakeClip(source);
            Animation animation = reel.gameObject.AddComponent<Animation>();
            animation.playAutomatically = false;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;
            animation.AddClip(clip, clip.name);
            animation.clip = clip;
            assembly.AddComponent<FishingRodView>().Configure(tip, reel, animation, clip);
            VerifyAssembly(assembly, reel, clip);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(assembly, PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Unity could not save the rod/reel prefab.");
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Fishing rod + animated reel installed. Press Play: hotbar slot 1 equips both; hold REEL during a fight to animate the reel.");
            EditorUtility.DisplayDialog("Fishing rod + reel installed", "Press Play. Your existing rod slot now equips both models. Cast moves the complete assembly; holding REEL during a fight runs the authored reel animation. No scene wiring is needed.", "OK");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorUtility.DisplayDialog("Rod + reel setup failed", error.Message, "OK");
        }
        finally
        {
            if (assembly != null) Object.DestroyImmediate(assembly);
        }
    }

    private static void ValidateSource(Source source)
    {
        if (source == null || source.version != 1 || source.parts == null || source.parts.Length != 6 || source.duration <= 0f)
            throw new InvalidOperationException("Invalid rod/reel source export.");
        var animated = new HashSet<string>();
        foreach (Part p in source.parts)
        {
            if (p.vertices == null || p.vertices.Length % 3 != 0 || p.normals.Length != p.vertices.Length ||
                p.uv.Length != p.vertices.Length / 3 * 2 || p.triangles.Length % 3 != 0)
                throw new InvalidOperationException("Invalid mesh data: " + p.name);
            foreach (int index in p.triangles)
                if (index < 0 || index >= p.vertices.Length / 3)
                    throw new InvalidOperationException("Invalid triangle index: " + p.name);
            if (p.frames.Length == 0) continue;
            if (p.group != "Reel") throw new InvalidOperationException("Animation must never target the rod.");
            animated.Add(p.name);
            Frame first = p.frames[0], last = p.frames[p.frames.Length - 1];
            if (Vector3.Distance(Vec(first.position), Vec(last.position)) > 0.0001f ||
                Quaternion.Angle(Quat(first.rotation), Quat(last.rotation)) > 0.1f)
                throw new InvalidOperationException("Reel animation does not close its loop: " + p.name);
        }
        if (!animated.SetEquals(new[] { "Handle", "Rotor", "Spool" }))
            throw new InvalidOperationException("The export must contain the three authored reel actions.");
    }

    private static Mesh MakeMesh(Part part)
    {
        int count = part.vertices.Length / 3;
        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        var uv = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            vertices[i] = Vec(part.vertices, i * 3);
            normals[i] = Vec(part.normals, i * 3);
            uv[i] = new Vector2(part.uv[i * 2], part.uv[i * 2 + 1]);
        }
        var mesh = new Mesh { name = part.name, vertices = vertices, normals = normals, uv = uv, triangles = part.triangles };
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return Save(mesh, Generated + "/" + part.name + ".asset");
    }

    private static AnimationClip MakeClip(Source source)
    {
        var clip = new AnimationClip { name = "AuthoredReeling", legacy = true, frameRate = 96f, wrapMode = WrapMode.Loop };
        string[] axes = { "x", "y", "z", "w" };
        foreach (Part part in source.parts)
        {
            if (part.frames.Length == 0) continue;
            for (int channel = 0; channel < 7; channel++)
            {
                bool rotation = channel >= 3;
                int axis = rotation ? channel - 3 : channel;
                var keys = new Keyframe[part.frames.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    Frame frame = part.frames[i];
                    keys[i] = new Keyframe(frame.time, rotation ? frame.rotation[axis] : frame.position[axis]);
                }
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                clip.SetCurve(part.name, typeof(Transform), (rotation ? "localRotation." : "localPosition.") + axes[axis], curve);
            }
        }
        clip.EnsureQuaternionContinuity();
        return Save(clip, Generated + "/AuthoredReeling.anim");
    }

    private static void VerifyAssembly(GameObject assembly, Transform reel, AnimationClip clip)
    {
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            if (binding.path != "Handle" && binding.path != "Rotor" && binding.path != "Spool")
                throw new InvalidOperationException("Unexpected animation target: " + binding.path);
        Vector3 mount = reel.localPosition;
        Quaternion mountRotation = reel.localRotation;
        Transform tip = assembly.GetComponent<FishingRodView>().RodTip;
        Vector3 tipPosition = tip.position;
        for (int frame = 0; frame <= 120; frame++)
        {
            clip.SampleAnimation(reel.gameObject, clip.length * frame / 120f);
            if (Vector3.Distance(reel.localPosition, mount) > 0.00001f ||
                Quaternion.Angle(reel.localRotation, mountRotation) > 0.001f ||
                Vector3.Distance(tip.position, tipPosition) > 0.00001f)
                throw new InvalidOperationException("Reeling moved the mounting point or rod tip.");
        }
        clip.SampleAnimation(reel.gameObject, 0f);
    }

    private static Texture2D LoadTexture(string name)
    {
        string path = Root + "/Source/" + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture: " + path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.maxTextureSize = 2048;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material MakeMaterial(string name, Shader shader, Texture texture, float metallic, float smoothness)
    {
        var material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        return Save(material, Generated + "/" + name + ".mat");
    }

    private static T Save<T>(T asset, string path) where T : Object
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
        EditorUtility.CopySerialized(asset, existing);
        Object.DestroyImmediate(asset);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void AddSeatCollar(Transform parent, float y, Material material)
    {
        var collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        collar.name = "ReelSeatCollar";
        collar.transform.SetParent(parent, false);
        collar.transform.localPosition = new Vector3(0f, y, 0f);
        collar.transform.localScale = new Vector3(0.028f, 0.005f, 0.028f);
        Object.DestroyImmediate(collar.GetComponent<Collider>());
        var renderer = collar.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Transform Child(string name, Transform parent)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }
    private static Vector3 Vec(float[] values, int offset = 0) => new Vector3(values[offset], values[offset + 1], values[offset + 2]);
    private static Quaternion Quat(float[] v) => new Quaternion(v[0], v[1], v[2], v[3]);
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
}
