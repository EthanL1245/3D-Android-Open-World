using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Installs the user's authored Raft.blend as the starter raft visual without
/// rebuilding or redesigning it. Geometry, UVs, hierarchy and child transforms come
/// directly from the supplied Blender file. The supplied raft/oar textures are then
/// applied onto copies of the authored material slots because Unity's .blend importer
/// does not reliably reconnect external Blender image nodes on every machine.
///
/// Only an outside wrapper is changed so the authored raft is aligned with the
/// existing BoatController forward axis and waterline. The original boat controller,
/// Rigidbody, buoyancy, steering, placement, boarding and fishing behavior remain.
/// </summary>
public static class RaftModelImporter
{
    private const string PackageName = "Raft.zip";
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string Root = "Assets/_Game/Boats/Raft/Authored";
    private const string BlendPath = Root + "/Raft.blend";
    private const string RaftTexturePath = Root + "/Raft Texture.jpg";
    private const string OarTexturePath = Root + "/Oar Texture.jpg";
    private const string MaterialRoot = Root + "/Materials";
    private const string VisualWrapperName = "Uploaded Raft Model";
    private const string RevisionMarkerName = "RaftImport_TextureForward_v3";

    // BoatController keeps the Rigidbody root on OceanWater.GetSurfaceHeight().
    // The supplied Blender scene is authored above its own origin, so translate the
    // complete model as one object until the bottom of the hull sits in the water.
    private const float DesiredHullBottomY = -0.35f;

    [InitializeOnLoadMethod]
    private static void AutoApplyWhenReady()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;
            if (AlreadyApplied()) return;

            string package = FindPackage();
            if (string.IsNullOrEmpty(package)) return;

            try
            {
                Apply(package, false);
                Debug.Log("Authored raft model auto-imported, textured, aligned to the waterline and pointed seat-first from " + package);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        };
    }

    [MenuItem("Tools/Open World/Apply Uploaded Raft Model (One Click)")]
    public static void ApplyOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Raft Model", "Exit Play Mode first.", "OK");
            return;
        }

        string package = FindPackage();
        if (string.IsNullOrEmpty(package))
            package = EditorUtility.OpenFilePanel("Choose Raft.zip", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "zip");
        if (string.IsNullOrEmpty(package)) return;

        try
        {
            Apply(package, true);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Raft Import Failed", e.Message + "\n\nSee the Unity Console for details.", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    public static bool TryApplyAvailablePackage(bool showDialog = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return false;
        string package = FindPackage();
        if (string.IsNullOrEmpty(package)) return false;

        try
        {
            Apply(package, showDialog);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void Apply(string zipPath, bool showDialog)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException("Raft.zip was not found.", zipPath);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            throw new InvalidOperationException("BaseBoat.prefab does not exist yet. Run Tools > Setup Boat System once, then apply the raft model.");

        EditorUtility.DisplayProgressBar("Raft Model", "Reading authored Raft.blend", 0.10f);
        ReadPackage(zipPath, out byte[] blendBytes, out byte[] raftTextureBytes, out byte[] oarTextureBytes);

        if (!LooksLikeBlend(blendBytes))
            throw new InvalidDataException("Raft.blend in the ZIP is not a valid Blender file.");

        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(MaterialRoot);
        File.WriteAllBytes(BlendPath, blendBytes);
        File.WriteAllBytes(RaftTexturePath, raftTextureBytes);
        File.WriteAllBytes(OarTexturePath, oarTextureBytes);

        AssetDatabase.ImportAsset(RaftTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(OarTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        ConfigureTexture(RaftTexturePath);
        ConfigureTexture(OarTexturePath);

        EditorUtility.DisplayProgressBar("Raft Model", "Importing the exact Blender model", 0.35f);
        AssetDatabase.ImportAsset(BlendPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        GameObject authored = AssetDatabase.LoadAssetAtPath<GameObject>(BlendPath);
        if (authored == null)
            throw new InvalidOperationException("Unity could not import Raft.blend. Make sure Blender is installed and available to Unity, then retry.");

        Renderer[] sourceRenderers = authored.GetComponentsInChildren<Renderer>(true);
        if (sourceRenderers == null || sourceRenderers.Length == 0)
            throw new InvalidDataException("Raft.blend imported without any renderable mesh objects.");

        EditorUtility.DisplayProgressBar("Raft Model", "Applying supplied textures and boat alignment", 0.72f);
        ApplyToPrefab(authored);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Raft installed from the supplied Blender file without redesign: authored geometry/UVs/hierarchy preserved, supplied raft/oar textures applied, seat end aligned with BoatController forward, and original floating/driving behavior retained.");

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Raft Model Installed",
                "The supplied raft is now textured with the provided raft/oar images, floats using the existing BoatController, and drives seat-first instead of sideways. The Blender geometry and UVs were not redesigned.",
                "OK"
            );
        }
    }

    private static void ReadPackage(string zipPath, out byte[] blendBytes, out byte[] raftTextureBytes, out byte[] oarTextureBytes)
    {
        using FileStream file = File.OpenRead(zipPath);
        using ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Read);

        ZipArchiveEntry blend = FindEntry(zip, "Raft.blend");
        ZipArchiveEntry raftTexture = FindEntry(zip, "Raft Texture.jpg");
        ZipArchiveEntry oarTexture = FindEntry(zip, "Oar Texture.jpg");

        if (blend == null || raftTexture == null || oarTexture == null)
            throw new InvalidDataException("Raft.zip must contain Raft.blend, Raft Texture.jpg and Oar Texture.jpg.");

        blendBytes = ReadEntry(blend);
        raftTextureBytes = ReadEntry(raftTexture);
        oarTextureBytes = ReadEntry(oarTexture);
    }

    private static ZipArchiveEntry FindEntry(ZipArchive zip, string fileName)
    {
        foreach (ZipArchiveEntry entry in zip.Entries)
            if (string.Equals(Path.GetFileName(entry.FullName), fileName, StringComparison.OrdinalIgnoreCase))
                return entry;
        return null;
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using MemoryStream memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static bool LooksLikeBlend(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 7) return false;
        return bytes[0] == (byte)'B' && bytes[1] == (byte)'L' && bytes[2] == (byte)'E' &&
               bytes[3] == (byte)'N' && bytes[4] == (byte)'D' && bytes[5] == (byte)'E' &&
               bytes[6] == (byte)'R';
    }

    private static void ConfigureTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void ApplyToPrefab(GameObject authoredSource)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform model = root.transform.Find("ModelContainer");
            if (model == null)
            {
                GameObject modelObject = new GameObject("ModelContainer");
                modelObject.transform.SetParent(root.transform, false);
                model = modelObject.transform;
            }

            while (model.childCount > 0)
                UnityEngine.Object.DestroyImmediate(model.GetChild(0).gameObject);

            GameObject wrapper = new GameObject(VisualWrapperName);
            wrapper.transform.SetParent(model, false);
            wrapper.transform.localPosition = Vector3.zero;
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;

            GameObject exactModel = (GameObject)PrefabUtility.InstantiatePrefab(authoredSource, wrapper.transform);
            if (exactModel == null)
                throw new InvalidOperationException("Could not instantiate the imported Raft.blend model.");
            exactModel.name = authoredSource.name;

            wrapper.transform.localRotation = DetermineSeatForwardCorrection(exactModel);

            Bounds initialBounds = CalculateBoundsInRoot(wrapper.transform, root.transform);
            if (initialBounds.size.x < 0.1f || initialBounds.size.y < 0.1f || initialBounds.size.z < 0.1f)
                throw new InvalidDataException("The imported Raft.blend has invalid bounds.");

            wrapper.transform.localPosition += new Vector3(
                -initialBounds.center.x,
                DesiredHullBottomY - initialBounds.min.y,
                -initialBounds.center.z
            );

            Bounds alignedBounds = CalculateBoundsInRoot(wrapper.transform, root.transform);
            if (Mathf.Abs(alignedBounds.min.y - DesiredHullBottomY) > 0.08f)
                throw new InvalidOperationException("Could not align the authored raft to the boat waterline.");

            ApplySuppliedTextures(exactModel);
            FitInvisibleGameplaySupports(root.transform, alignedBounds);
            FitInteractionAnchors(root, alignedBounds);

            GameObject marker = new GameObject(RevisionMarkerName);
            marker.transform.SetParent(wrapper.transform, false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Quaternion DetermineSeatForwardCorrection(GameObject exactModel)
    {
        Renderer[] renderers = exactModel.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return Quaternion.Euler(0f, -90f, 0f);

        bool haveOverall = false;
        Bounds overall = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            if (!haveOverall) { overall = renderer.bounds; haveOverall = true; }
            else overall.Encapsulate(renderer.bounds);
        }

        Renderer seatCandidate = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsOarRenderer(renderer)) continue;

            Bounds b = renderer.bounds;
            float height = b.size.y;
            float horizontalArea = Mathf.Max(0.01f, b.size.x * b.size.z);
            float compactness = height / Mathf.Sqrt(horizontalArea);
            float score = b.max.y + height * 0.8f + compactness * 0.25f;
            if (score > bestScore)
            {
                bestScore = score;
                seatCandidate = renderer;
            }
        }

        if (seatCandidate != null && haveOverall)
        {
            Vector3 offset = seatCandidate.bounds.center - overall.center;
            offset.y = 0f;
            if (offset.sqrMagnitude > 0.04f)
            {
                Vector3 authoredForward;
                if (Mathf.Abs(offset.x) >= Mathf.Abs(offset.z))
                    authoredForward = offset.x >= 0f ? Vector3.right : Vector3.left;
                else
                    authoredForward = offset.z >= 0f ? Vector3.forward : Vector3.back;

                float yaw = Vector3.SignedAngle(authoredForward, Vector3.forward, Vector3.up);
                return Quaternion.Euler(0f, yaw, 0f);
            }
        }

        return Quaternion.Euler(0f, -90f, 0f);
    }

    private static void ApplySuppliedTextures(GameObject exactModel)
    {
        Texture2D raftTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RaftTexturePath);
        Texture2D oarTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath);
        if (raftTexture == null || oarTexture == null)
            throw new InvalidOperationException("The supplied raft/oar textures could not be imported.");

        Directory.CreateDirectory(MaterialRoot);
        AssetDatabase.Refresh();

        Renderer[] renderers = exactModel.GetComponentsInChildren<Renderer>(true);
        Dictionary<string, Material> generated = new Dictionary<string, Material>();
        bool usedRaftTexture = false;
        bool usedOarTexture = false;

        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer renderer = renderers[r];
            if (renderer == null) continue;

            bool rendererLooksLikeOar = IsOarRenderer(renderer);
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                materials = new Material[1];

            for (int m = 0; m < materials.Length; m++)
            {
                Material source = materials[m];
                bool oar = rendererLooksLikeOar || MaterialLooksLikeOar(source);
                Texture2D texture = oar ? oarTexture : raftTexture;
                if (oar) usedOarTexture = true; else usedRaftTexture = true;

                string safeRenderer = SafeAssetName(renderer.name);
                string safeMaterial = SafeAssetName(source != null ? source.name : "Material");
                string key = (oar ? "Oar_" : "Raft_") + safeRenderer + "_" + safeMaterial + "_" + m;

                if (!generated.TryGetValue(key, out Material replacement))
                {
                    string materialPath = MaterialRoot + "/" + key + ".mat";
                    replacement = BuildTexturedMaterial(materialPath, source, texture);
                    generated.Add(key, replacement);
                }
                materials[m] = replacement;
            }

            renderer.sharedMaterials = materials;
        }

        if (!usedRaftTexture)
            throw new InvalidOperationException("No raft renderer was found to receive Raft Texture.jpg.");

        if (!usedOarTexture)
            Debug.LogWarning("Raft imported, but an independently identifiable oar renderer/material was not found. Raft geometry was left intact.");
    }

    private static Material BuildTexturedMaterial(string path, Material source, Texture2D texture)
    {
        Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
        if (fallback == null) fallback = Shader.Find("Standard");
        if (fallback == null) throw new InvalidOperationException("No supported lit shader is available for the raft materials.");

        Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool validSource = source != null && source.shader != null &&
                           !source.shader.name.Contains("InternalError") &&
                           !source.shader.name.Contains("Hidden/Internal");

        if (result == null)
        {
            result = validSource ? new Material(source) : new Material(fallback);
            AssetDatabase.CreateAsset(result, path);
        }
        else if (validSource)
        {
            EditorUtility.CopySerialized(source, result);
        }
        else
        {
            result.shader = fallback;
        }

        result.name = Path.GetFileNameWithoutExtension(path);
        result.mainTexture = texture;
        if (result.HasProperty("_BaseMap")) result.SetTexture("_BaseMap", texture);
        if (result.HasProperty("_MainTex")) result.SetTexture("_MainTex", texture);
        EditorUtility.SetDirty(result);
        return result;
    }

    private static bool MaterialLooksLikeOar(Material material)
    {
        if (material == null) return false;
        if (material.name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        Texture texture = material.mainTexture;
        return texture != null && texture.name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsOarRenderer(Renderer renderer)
    {
        if (renderer == null) return false;
        if (renderer.name.IndexOf("oar", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        Vector3 s = renderer.bounds.size;
        float a = s.x, b = s.y, c = s.z;
        float largest = Mathf.Max(a, Mathf.Max(b, c));
        float smallest = Mathf.Min(a, Mathf.Min(b, c));
        float middle = a + b + c - largest - smallest;

        return largest > 1.7f && largest < 3.5f && middle < 0.9f && largest / Mathf.Max(0.01f, middle) > 3.0f;
    }

    private static string SafeAssetName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "Material";
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++) value = value.Replace(invalid[i], '_');
        value = value.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        return value;
    }

    private static Bounds CalculateBoundsInRoot(Transform visualRoot, Transform boatRoot)
    {
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        bool haveBounds = false;
        Bounds result = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            Bounds world = renderer.bounds;
            Vector3 center = world.center;
            Vector3 extents = world.extents;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                Vector3 local = boatRoot.InverseTransformPoint(worldCorner);
                if (!haveBounds) { result = new Bounds(local, Vector3.zero); haveBounds = true; }
                else result.Encapsulate(local);
            }
        }

        return result;
    }

    private static void FitInvisibleGameplaySupports(Transform root, Bounds bounds)
    {
        float width = Mathf.Max(0.5f, bounds.size.x * 0.94f);
        float length = Mathf.Max(0.5f, bounds.size.z * 0.94f);
        float deckY = bounds.min.y + bounds.size.y * 0.48f;

        ResizeAndHide(root, "Walkable deck", new Vector3(bounds.center.x, deckY, bounds.center.z), new Vector3(width, 0.16f, length));
        ResizeAndHide(root, "Hull collider", new Vector3(bounds.center.x, -0.08f, bounds.center.z), new Vector3(bounds.size.x * 0.98f, 0.55f, bounds.size.z * 0.98f));

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.name != "Low gunwale") continue;
            Renderer r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            Collider c = t.GetComponent<Collider>();
            if (c != null) c.enabled = false;
        }
    }

    private static void FitInteractionAnchors(GameObject root, Bounds bounds)
    {
        BoatController controller = root.GetComponent<BoatController>();
        if (controller == null) return;

        float deckY = bounds.min.y + bounds.size.y * 0.50f;
        float frontZ = bounds.max.z;
        float rearZ = bounds.min.z;

        if (controller.DriverSeat != null)
        {
            controller.DriverSeat.localPosition = new Vector3(bounds.center.x, deckY + 0.18f, frontZ - Mathf.Min(0.65f, bounds.size.z * 0.15f));
            controller.DriverSeat.localRotation = Quaternion.identity;
        }

        if (controller.DeckExit != null)
        {
            controller.DeckExit.localPosition = new Vector3(bounds.center.x, deckY + 0.05f, rearZ + Mathf.Min(0.55f, bounds.size.z * 0.14f));
            controller.DeckExit.localRotation = Quaternion.identity;
        }
    }

    private static void ResizeAndHide(Transform root, string objectName, Vector3 position, Vector3 scale)
    {
        Transform target = FindDeepChild(root, objectName);
        if (target == null) return;
        target.localPosition = position;
        target.localRotation = Quaternion.identity;
        target.localScale = scale;
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == name) return all[i];
        return null;
    }

    private static bool AlreadyApplied()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return false;

        Transform wrapper = prefab.transform.Find("ModelContainer/" + VisualWrapperName);
        if (wrapper == null || wrapper.Find(RevisionMarkerName) == null) return false;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BlendPath) == null ||
            AssetDatabase.LoadAssetAtPath<Texture2D>(RaftTexturePath) == null ||
            AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath) == null)
            return false;

        return wrapper.localPosition.y < -0.05f;
    }

    private static string FindPackage()
    {
        string project = Path.GetDirectoryName(Application.dataPath);
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] folders =
        {
            project,
            Path.Combine(user, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        for (int i = 0; i < folders.Length; i++)
        {
            string folder = folders[i];
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
            string exact = Path.Combine(folder, PackageName);
            if (File.Exists(exact)) return exact;
        }

        string newest = null;
        DateTime newestTime = DateTime.MinValue;
        for (int i = 0; i < folders.Length; i++)
        {
            string folder = folders[i];
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
            string[] candidates;
            try { candidates = Directory.GetFiles(folder, "*Raft*.zip", SearchOption.TopDirectoryOnly); }
            catch { continue; }
            for (int j = 0; j < candidates.Length; j++)
            {
                DateTime time = File.GetLastWriteTimeUtc(candidates[j]);
                if (time > newestTime) { newest = candidates[j]; newestTime = time; }
            }
        }
        return newest;
    }
}
