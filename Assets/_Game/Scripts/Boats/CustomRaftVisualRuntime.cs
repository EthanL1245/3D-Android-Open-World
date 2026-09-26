using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

/// <summary>
/// Replaces the starter raft placeholder art with the user-authored Raft.zip model.
/// BoatController, placement, buoyancy and passenger logic stay authoritative; this
/// component only swaps visuals and resizes the existing invisible support colliders.
/// </summary>
[DisallowMultipleComponent]
public sealed class CustomRaftVisualRuntime : MonoBehaviour
{
    public static readonly Vector3 GameplayHullSize = new Vector3(4.7f, 1.2f, 4.5f);
    public const float GameplayDraft = 0.45f;

    private const string VisualName = "Uploaded Raft Model";
    private static Mesh sharedMesh;
    private static Texture2D raftTexture;
    private static Texture2D oarTexture;
    private static Material raftMaterial;
    private static Material oarMaterial;

    public static bool IsRaft(BoatData data)
    {
        return data != null && string.Equals(data.ID, "raft", StringComparison.OrdinalIgnoreCase);
    }

    public static void Ensure(GameObject boat)
    {
        if (boat == null || boat.GetComponent<CustomRaftVisualRuntime>() != null) return;
        boat.AddComponent<CustomRaftVisualRuntime>();
    }

    private void Awake()
    {
        BuildSharedAssets();
        ReplaceVisuals();
        ResizeGameplaySupports();
    }

    private void OnEnable()
    {
        if (transform.Find("ModelContainer/" + VisualName) != null) return;
        BuildSharedAssets();
        ReplaceVisuals();
        ResizeGameplaySupports();
    }

    private static void BuildSharedAssets()
    {
        if (sharedMesh == null) sharedMesh = DecodeMesh();
        if (raftTexture == null) raftTexture = DecodeTexture(CustomRaftRaftTextureData.Payload, "Raft Texture");
        if (oarTexture == null) oarTexture = DecodeTexture(CustomRaftOarTextureData.Payload, "Oar Texture");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        if (raftMaterial == null) raftMaterial = BuildMaterial(shader, raftTexture, "Raft Wood Material", 0.12f);
        if (oarMaterial == null) oarMaterial = BuildMaterial(shader, oarTexture, "Oar Wood Material", 0.18f);
    }

    private void ReplaceVisuals()
    {
        Transform model = transform.Find("ModelContainer");
        if (model == null)
        {
            GameObject modelObject = new GameObject("ModelContainer");
            modelObject.transform.SetParent(transform, false);
            model = modelObject.transform;
        }

        for (int i = 0; i < model.childCount; i++)
        {
            Transform child = model.GetChild(i);
            if (child.name != VisualName) child.gameObject.SetActive(false);
        }

        Transform existing = model.Find(VisualName);
        GameObject visual;
        if (existing == null)
        {
            visual = new GameObject(VisualName, typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(model, false);
        }
        else visual = existing.gameObject;

        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        visual.SetActive(true);

        MeshFilter filter = visual.GetComponent<MeshFilter>();
        MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
        filter.sharedMesh = sharedMesh;
        if (raftMaterial != null && oarMaterial != null)
            renderer.sharedMaterials = new[] { raftMaterial, oarMaterial };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;

        HideRenderer("Walkable deck");
        HideRenderer("Low gunwale");
        HideRenderer("Hull collider");
    }

    private void ResizeGameplaySupports()
    {
        Transform deck = FindDeepChild(transform, "Walkable deck");
        if (deck != null)
        {
            deck.localPosition = new Vector3(0f, 0.30f, 0f);
            deck.localRotation = Quaternion.identity;
            deck.localScale = new Vector3(4.35f, 0.16f, 4.18f);
        }

        Transform hull = FindDeepChild(transform, "Hull collider");
        if (hull != null)
        {
            hull.localPosition = new Vector3(0f, 0.03f, 0f);
            hull.localRotation = Quaternion.identity;
            hull.localScale = new Vector3(4.55f, 0.55f, 4.35f);
        }

        Transform[] all = GetComponentsInChildren<Transform>(true);
        int railIndex = 0;
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.name != "Low gunwale") continue;
            float side = railIndex++ == 0 ? -1f : 1f;
            t.localPosition = new Vector3(side * 2.20f, 0.48f, 0f);
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(0.12f, 0.24f, 4.28f);
            Renderer r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }

        BoatController controller = GetComponent<BoatController>();
        if (controller == null) return;
        if (controller.DriverSeat != null)
            controller.DriverSeat.localPosition = new Vector3(0.05f, 0.58f, 0.02f);
        if (controller.DeckExit != null)
            controller.DeckExit.localPosition = new Vector3(0f, 0.48f, -1.70f);
    }

    private void HideRenderer(string objectName)
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.name != objectName) continue;
            Renderer r = t.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
    }

    private static Transform FindDeepChild(Transform root, string objectName)
    {
        if (root == null) return null;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].name == objectName) return all[i];
        return null;
    }

    private static Mesh DecodeMesh()
    {
        byte[] compressed = Convert.FromBase64String(CustomRaftMeshData.Payload);
        using MemoryStream input = new MemoryStream(compressed);
        using GZipStream gzip = new GZipStream(input, CompressionMode.Decompress);
        using MemoryStream raw = new MemoryStream();
        gzip.CopyTo(raw);
        raw.Position = 0;
        using BinaryReader reader = new BinaryReader(raw);

        if (reader.ReadByte() != (byte)'R' || reader.ReadByte() != (byte)'F' ||
            reader.ReadByte() != (byte)'T' || reader.ReadByte() != (byte)'1')
            throw new InvalidDataException("Invalid embedded raft mesh payload.");

        int vertexCount = reader.ReadInt32();
        int raftIndexCount = reader.ReadInt32();
        int oarIndexCount = reader.ReadInt32();

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        for (int i = 0; i < vertexCount; i++)
            vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        for (int i = 0; i < vertexCount; i++)
            uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());

        int[] raftTriangles = new int[raftIndexCount];
        int[] oarTriangles = new int[oarIndexCount];
        for (int i = 0; i < raftIndexCount; i++) raftTriangles[i] = reader.ReadInt32();
        for (int i = 0; i < oarIndexCount; i++) oarTriangles[i] = reader.ReadInt32();

        Mesh mesh = new Mesh
        {
            name = "Uploaded Raft Mesh",
            hideFlags = HideFlags.DontSave
        };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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

    private static Texture2D DecodeTexture(string payload, string textureName)
    {
        byte[] bytes = Convert.FromBase64String(payload);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, true, false)
        {
            name = textureName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            hideFlags = HideFlags.DontSave
        };
        if (!texture.LoadImage(bytes, false))
            throw new InvalidDataException("Could not decode embedded " + textureName + ".");
        return texture;
    }

    private static Material BuildMaterial(Shader shader, Texture2D texture, string materialName, float smoothness)
    {
        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.DontSave
        };
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }
}
