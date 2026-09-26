using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class RaftModelImporter
{
    private const string PackageName = "Raft.zip";
    private const string PrefabPath = "Assets/Resources/Boats/BaseBoat.prefab";
    private const string Root = "Assets/_Game/Boats/Raft/Generated";
    private const string MeshPath = Root + "/RaftModel.asset";
    private const string RaftTexturePath = Root + "/RaftTexture.jpg";
    private const string OarTexturePath = Root + "/OarTexture.jpg";
    private const string RaftMaterialPath = Root + "/RaftWood.mat";
    private const string OarMaterialPath = Root + "/OarWood.mat";
    private const string VisualName = "Uploaded Raft Model";
    private const uint ExpectedTriangleCount = 1516;

    private sealed class ParsedStl
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3Int> faces = new List<Vector3Int>();
        public int[] faceComponent;
        public int[] vertexComponent;
        public int oarComponent;
        public Bounds bounds;
    }

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
                Debug.Log("Custom raft model auto-imported from " + package);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
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
    }

    private static void Apply(string zipPath, bool showDialog)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException("Raft.zip was not found.", zipPath);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            throw new InvalidOperationException("BaseBoat.prefab does not exist yet. Run Tools > Setup Boat System once, then apply the raft model.");

        EditorUtility.DisplayProgressBar("Raft Model", "Reading Raft.zip", 0.10f);
        ReadPackage(zipPath, out byte[] stlBytes, out byte[] raftTextureBytes, out byte[] oarTextureBytes);
        ParsedStl parsed = ParseBinaryStl(stlBytes);

        if (!Directory.Exists(Root)) Directory.CreateDirectory(Root);
        File.WriteAllBytes(RaftTexturePath, raftTextureBytes);
        File.WriteAllBytes(OarTexturePath, oarTextureBytes);
        AssetDatabase.ImportAsset(RaftTexturePath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(OarTexturePath, ImportAssetOptions.ForceUpdate);
        ConfigureTexture(RaftTexturePath);
        ConfigureTexture(OarTexturePath);

        EditorUtility.DisplayProgressBar("Raft Model", "Building exact raft mesh", 0.45f);
        Mesh mesh = BuildMesh(parsed);
        mesh = SaveOrReplaceMesh(mesh);

        Material raftMaterial = BuildMaterial(RaftMaterialPath, AssetDatabase.LoadAssetAtPath<Texture2D>(RaftTexturePath), 0.12f);
        Material oarMaterial = BuildMaterial(OarMaterialPath, AssetDatabase.LoadAssetAtPath<Texture2D>(OarTexturePath), 0.18f);

        EditorUtility.DisplayProgressBar("Raft Model", "Replacing the starter raft", 0.75f);
        ApplyToPrefab(mesh, raftMaterial, oarMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Uploaded raft installed: 1516-triangle model, supplied raft/oar textures, resized gameplay footprint, and existing boat mechanics preserved.");

        if (showDialog)
            EditorUtility.DisplayDialog("Raft Model Installed", "The starter raft now uses your uploaded model. Existing placement, buoyancy, driving, boarding and fishing-on-boat behavior were preserved.", "OK");
    }

    private static void ReadPackage(string zipPath, out byte[] stlBytes, out byte[] raftTextureBytes, out byte[] oarTextureBytes)
    {
        using FileStream file = File.OpenRead(zipPath);
        using ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Read);

        ZipArchiveEntry stl = FindEntry(zip, "Raft.stl");
        ZipArchiveEntry raftTexture = FindEntry(zip, "Raft Texture.jpg");
        ZipArchiveEntry oarTexture = FindEntry(zip, "Oar Texture.jpg");

        if (stl == null || raftTexture == null || oarTexture == null)
            throw new InvalidDataException("Raft.zip must contain Raft.stl, Raft Texture.jpg and Oar Texture.jpg.");

        stlBytes = ReadEntry(stl);
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

    private static ParsedStl ParseBinaryStl(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 84) throw new InvalidDataException("Raft.stl is not a valid binary STL.");
        uint triangleCount = BitConverter.ToUInt32(bytes, 80);
        if (triangleCount != ExpectedTriangleCount || 84L + triangleCount * 50L != bytes.LongLength)
            throw new InvalidDataException("The supplied raft STL does not match the expected 1516-triangle model.");

        ParsedStl result = new ParsedStl();
        Dictionary<Vector3, int> vertexLookup = new Dictionary<Vector3, int>();
        using MemoryStream memory = new MemoryStream(bytes, false);
        using BinaryReader reader = new BinaryReader(memory);
        reader.BaseStream.Position = 84;

        for (int face = 0; face < triangleCount; face++)
        {
            reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle();
            int a = ReadVertex(reader, result.vertices, vertexLookup);
            int b = ReadVertex(reader, result.vertices, vertexLookup);
            int c = ReadVertex(reader, result.vertices, vertexLookup);
            reader.ReadUInt16();
            result.faces.Add(new Vector3Int(a, b, c));
        }

        BuildComponents(result);
        return result;
    }

    private static int ReadVertex(BinaryReader reader, List<Vector3> vertices, Dictionary<Vector3, int> lookup)
    {
        Vector3 vertex = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        if (lookup.TryGetValue(vertex, out int index)) return index;
        index = vertices.Count;
        vertices.Add(vertex);
        lookup.Add(vertex, index);
        return index;
    }

    private static void BuildComponents(ParsedStl stl)
    {
        List<int>[] facesByVertex = new List<int>[stl.vertices.Count];
        for (int i = 0; i < facesByVertex.Length; i++) facesByVertex[i] = new List<int>();
        for (int face = 0; face < stl.faces.Count; face++)
        {
            Vector3Int f = stl.faces[face];
            facesByVertex[f.x].Add(face);
            facesByVertex[f.y].Add(face);
            facesByVertex[f.z].Add(face);
        }

        stl.faceComponent = new int[stl.faces.Count];
        for (int i = 0; i < stl.faceComponent.Length; i++) stl.faceComponent[i] = -1;
        List<int> componentCounts = new List<int>();
        List<Bounds> componentBounds = new List<Bounds>();
        Queue<int> queue = new Queue<int>();

        int component = 0;
        for (int start = 0; start < stl.faces.Count; start++)
        {
            if (stl.faceComponent[start] >= 0) continue;
            int count = 0;
            bool hasBounds = false;
            Bounds bounds = default;
            stl.faceComponent[start] = component;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int face = queue.Dequeue();
                count++;
                Vector3Int f = stl.faces[face];
                int[] ids = { f.x, f.y, f.z };
                for (int n = 0; n < 3; n++)
                {
                    Vector3 v = stl.vertices[ids[n]];
                    if (!hasBounds) { bounds = new Bounds(v, Vector3.zero); hasBounds = true; }
                    else bounds.Encapsulate(v);

                    List<int> linked = facesByVertex[ids[n]];
                    for (int j = 0; j < linked.Count; j++)
                    {
                        int other = linked[j];
                        if (stl.faceComponent[other] >= 0) continue;
                        stl.faceComponent[other] = component;
                        queue.Enqueue(other);
                    }
                }
            }

            componentCounts.Add(count);
            componentBounds.Add(bounds);
            component++;
        }

        stl.oarComponent = -1;
        for (int i = 0; i < componentCounts.Count; i++)
        {
            Vector3 size = componentBounds[i].size;
            if (componentCounts[i] == 92 && size.y > 2.4f && size.x < 0.7f && size.z < 0.5f)
            {
                stl.oarComponent = i;
                break;
            }
        }
        if (stl.oarComponent < 0) throw new InvalidDataException("Could not identify the separate oar in Raft.stl.");

        stl.vertexComponent = new int[stl.vertices.Count];
        for (int i = 0; i < stl.vertexComponent.Length; i++) stl.vertexComponent[i] = -1;
        for (int face = 0; face < stl.faces.Count; face++)
        {
            int c = stl.faceComponent[face];
            Vector3Int f = stl.faces[face];
            stl.vertexComponent[f.x] = c;
            stl.vertexComponent[f.y] = c;
            stl.vertexComponent[f.z] = c;
        }

        stl.bounds = new Bounds(stl.vertices[0], Vector3.zero);
        for (int i = 1; i < stl.vertices.Count; i++) stl.bounds.Encapsulate(stl.vertices[i]);
    }

    private static Mesh BuildMesh(ParsedStl stl)
    {
        Vector3[] vertices = new Vector3[stl.vertices.Count];
        Vector2[] uv = new Vector2[stl.vertices.Count];
        List<int> raftTriangles = new List<int>(stl.faces.Count * 3);
        List<int> oarTriangles = new List<int>(96 * 3);

        float centerX = stl.bounds.center.x;
        float centerForward = stl.bounds.center.y;
        Bounds oarBounds = default;
        bool haveOarBounds = false;
        for (int i = 0; i < stl.vertices.Count; i++)
        {
            Vector3 source = stl.vertices[i];
            if (stl.vertexComponent[i] == stl.oarComponent)
            {
                if (!haveOarBounds) { oarBounds = new Bounds(source, Vector3.zero); haveOarBounds = true; }
                else oarBounds.Encapsulate(source);
            }
        }

        for (int i = 0; i < stl.vertices.Count; i++)
        {
            Vector3 source = stl.vertices[i];
            vertices[i] = new Vector3(source.x - centerX, source.z - 2.42f, source.y - centerForward);

            if (stl.vertexComponent[i] == stl.oarComponent)
            {
                float along = Mathf.InverseLerp(oarBounds.min.y, oarBounds.max.y, source.y);
                float across = Mathf.InverseLerp(oarBounds.min.x, oarBounds.max.x, source.x);
                uv[i] = new Vector2(Mathf.Lerp(0.07f, 0.93f, along), Mathf.Lerp(0.36f, 0.64f, across));
            }
            else
            {
                float x = Mathf.InverseLerp(stl.bounds.min.x, stl.bounds.max.x, source.x);
                float z = Mathf.InverseLerp(stl.bounds.min.y, stl.bounds.max.y, source.y);
                uv[i] = new Vector2(Mathf.Lerp(0.08f, 0.92f, x), Mathf.Lerp(0.08f, 0.92f, z));
            }
        }

        for (int face = 0; face < stl.faces.Count; face++)
        {
            Vector3Int f = stl.faces[face];
            List<int> target = stl.faceComponent[face] == stl.oarComponent ? oarTriangles : raftTriangles;
            target.Add(f.x);
            target.Add(f.z);
            target.Add(f.y);
        }

        Mesh mesh = new Mesh { name = "RaftModel" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(raftTriangles, 0, true);
        mesh.SetTriangles(oarTriangles, 1, true);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh SaveOrReplaceMesh(Mesh fresh)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(fresh, MeshPath);
            return fresh;
        }
        EditorUtility.CopySerialized(fresh, existing);
        UnityEngine.Object.DestroyImmediate(fresh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void ConfigureTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static Material BuildMaterial(string path, Texture2D texture, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No supported lit shader is available.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else material.shader = shader;

        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ApplyToPrefab(Mesh mesh, Material raftMaterial, Material oarMaterial)
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

            GameObject visual = new GameObject(VisualName, typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(model, false);
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { raftMaterial, oarMaterial };
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            ResizeAndHide(root.transform, "Walkable deck", new Vector3(0f, 0.30f, 0f), new Vector3(4.35f, 0.16f, 4.18f));
            ResizeAndHide(root.transform, "Hull collider", new Vector3(0f, 0.03f, 0f), new Vector3(4.55f, 0.55f, 4.35f));

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            int rail = 0;
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != "Low gunwale") continue;
                float side = rail++ == 0 ? -1f : 1f;
                t.localPosition = new Vector3(side * 2.20f, 0.48f, 0f);
                t.localRotation = Quaternion.identity;
                t.localScale = new Vector3(0.12f, 0.24f, 4.28f);
                Renderer oldRenderer = t.GetComponent<Renderer>();
                if (oldRenderer != null) oldRenderer.enabled = false;
            }

            BoatController controller = root.GetComponent<BoatController>();
            if (controller != null)
            {
                if (controller.DriverSeat != null) controller.DriverSeat.localPosition = new Vector3(0.05f, 0.58f, 0.02f);
                if (controller.DeckExit != null) controller.DeckExit.localPosition = new Vector3(0f, 0.48f, -1.70f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
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
        return prefab != null &&
               prefab.transform.Find("ModelContainer/" + VisualName) != null &&
               AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null &&
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
