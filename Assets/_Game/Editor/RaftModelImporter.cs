using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Installs the user's authored Raft.blend as the starter raft visual without
/// rebuilding or redesigning it. Geometry, UVs, hierarchy, child transforms and
/// Blender material assignments all come directly from the supplied file.
///
/// The only transform applied outside the authored model is a wrapper used to:
/// 1) map Blender forward to the BoatController forward direction, and
/// 2) place the complete authored raft on the BoatController waterline.
/// Invisible gameplay colliders/interaction anchors are then fitted around it so
/// the original placement, buoyancy, boarding, driving and fishing features remain.
/// </summary>
public static class RaftModelImporter
{
    private const string PackageName = "Raft.zip";
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string Root = "Assets/_Game/Boats/Raft/Authored";
    private const string BlendPath = Root + "/Raft.blend";
    private const string RaftTexturePath = Root + "/Raft Texture.jpg";
    private const string OarTexturePath = Root + "/Oar Texture.jpg";
    private const string VisualWrapperName = "Uploaded Raft Model";

    // BoatController holds the Rigidbody/root at the sampled water surface. The
    // authored Blender scene itself is vertically offset above its origin, so using
    // it at localPosition=0 makes the complete raft float in the air. Keep every
    // transform INSIDE Raft.blend unchanged and translate only the outside wrapper
    // so the bottom of the authored hull sits slightly below the waterline.
    private const float DesiredHullBottomY = -0.35f;

    // Blender +Y is the authored forward direction. Unity's .blend conversion maps
    // that to -Z, so this wrapper yaw makes the seat end the BoatController +Z/front.
    private static readonly Quaternion AuthoredForwardCorrection = Quaternion.Euler(0f, 180f, 0f);

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
                Debug.Log("Authored raft model auto-imported and aligned to the waterline from " + package);
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
        File.WriteAllBytes(BlendPath, blendBytes);
        File.WriteAllBytes(RaftTexturePath, raftTextureBytes);
        File.WriteAllBytes(OarTexturePath, oarTextureBytes);

        // Keep the supplied images directly beside Raft.blend because the Blender
        // scene references them there. No UVs/material slots are reconstructed here.
        AssetDatabase.ImportAsset(RaftTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(OarTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        ConfigureTexture(RaftTexturePath);
        ConfigureTexture(OarTexturePath);

        EditorUtility.DisplayProgressBar("Raft Model", "Importing the exact Blender model", 0.38f);
        AssetDatabase.ImportAsset(BlendPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        GameObject authored = AssetDatabase.LoadAssetAtPath<GameObject>(BlendPath);
        if (authored == null)
            throw new InvalidOperationException("Unity could not import Raft.blend. Make sure Blender is installed and available to Unity, then retry.");

        Renderer[] sourceRenderers = authored.GetComponentsInChildren<Renderer>(true);
        if (sourceRenderers == null || sourceRenderers.Length == 0)
            throw new InvalidDataException("Raft.blend imported without any renderable mesh objects.");

        EditorUtility.DisplayProgressBar("Raft Model", "Placing authored raft on the waterline", 0.72f);
        ApplyToPrefab(authored);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Raft installed from Raft.blend without redesign. Authored geometry/materials remain unchanged, the seat end faces forward, and the hull now sits on the BoatController waterline while original boat mechanics remain intact.");

        if (showDialog)
            EditorUtility.DisplayDialog("Raft Model Installed", "The supplied Blender raft is installed without redesign. Its seat end faces forward and the complete authored model has been aligned to the existing boat waterline. Original placement, buoyancy, boarding, driving and fishing behavior remains in use.", "OK");
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

            // Replace only the visual. Controller/Rigidbody/gameplay supports stay.
            while (model.childCount > 0)
                UnityEngine.Object.DestroyImmediate(model.GetChild(0).gameObject);

            GameObject wrapper = new GameObject(VisualWrapperName);
            wrapper.transform.SetParent(model, false);
            wrapper.transform.localPosition = Vector3.zero;
            wrapper.transform.localRotation = AuthoredForwardCorrection;
            wrapper.transform.localScale = Vector3.one;

            GameObject exactModel = (GameObject)PrefabUtility.InstantiatePrefab(authoredSource, wrapper.transform);
            if (exactModel == null)
                throw new InvalidOperationException("Could not instantiate the imported Raft.blend model.");

            // Do not touch any transform inside the supplied .blend hierarchy.
            exactModel.name = authoredSource.name;

            Bounds initialBounds = CalculateBoundsInRoot(wrapper.transform, root.transform);
            if (initialBounds.size.x < 0.1f || initialBounds.size.y < 0.1f || initialBounds.size.z < 0.1f)
                throw new InvalidDataException("The imported Raft.blend has invalid bounds.");

            // The supplied scene's geometry is authored several units above its
            // Blender origin. BoatController intentionally keeps the prefab root at
            // the dynamic water surface, so align the complete visual as one object.
            // Centering X/Z also keeps the existing Rigidbody/collision footprint
            // beneath the visual. This never changes authored child transforms.
            wrapper.transform.localPosition += new Vector3(
                -initialBounds.center.x,
                DesiredHullBottomY - initialBounds.min.y,
                -initialBounds.center.z
            );

            Bounds alignedBounds = CalculateBoundsInRoot(wrapper.transform, root.transform);
            if (Mathf.Abs(alignedBounds.min.y - DesiredHullBottomY) > 0.08f)
                throw new InvalidOperationException("Could not align the authored raft to the boat waterline.");

            FitInvisibleGameplaySupports(root.transform, alignedBounds);
            FitInteractionAnchors(root, alignedBounds);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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
            Vector3 c = world.center;
            Vector3 e = world.extents;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 worldCorner = c + Vector3.Scale(e, new Vector3(x, y, z));
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

        // Placeholder gunwales are not part of the user's raft. Keep them invisible
        // and non-colliding so the authored model is the only visible geometry.
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
        if (wrapper == null) return false;

        // The previous importer left this wrapper at Y=0, which is exactly the bug
        // that made the authored model float in the air. Force one automatic reapply
        // after this update, then recognize the corrected installation normally.
        bool waterlineAligned = Mathf.Abs(wrapper.localPosition.y) > 0.05f;

        return waterlineAligned &&
               AssetDatabase.LoadAssetAtPath<GameObject>(BlendPath) != null &&
               AssetDatabase.LoadAssetAtPath<Texture2D>(RaftTexturePath) != null &&
               AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath) != null;
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
