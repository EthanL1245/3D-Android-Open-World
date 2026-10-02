using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Uses the exact user-supplied sand, grass and stone images stored as chunked
/// base64 TextAssets under Resources. This replaces terrain diffuse textures at
/// runtime without changing terrain geometry, alphamaps, tiling, normals or physics.
/// It also catches the two runtime-generated Brinebreak/underwater stone layers.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class AuthoredTerrainSurfaceTextures : MonoBehaviour
{
    private const string DataRoot = "Environment/SurfaceTextureData/";

    private static Texture2D sand;
    private static Texture2D grass;
    private static Texture2D stone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<AuthoredTerrainSurfaceTextures>() != null) return;
        var go = new GameObject("Authored Terrain Surface Textures");
        DontDestroyOnLoad(go);
        go.AddComponent<AuthoredTerrainSurfaceTextures>();
    }

    private void Start()
    {
        ApplyAll();
    }

    private void OnEnable()
    {
        // Start is the important pass because IslandExpansionWorld builds at -600.
        // This extra delayed pass also covers scene/domain timing differences.
        Invoke(nameof(ApplyAll), 0.15f);
    }

    public void ApplyAll()
    {
        EnsureTextures();
        if (sand == null || grass == null || stone == null) return;

        int changed = 0;
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        foreach (Terrain terrain in terrains)
        {
            if (terrain == null || terrain.terrainData == null) continue;
            TerrainLayer[] layers = terrain.terrainData.terrainLayers;
            if (layers == null || layers.Length == 0) continue;

            for (int i = 0; i < layers.Length; i++)
            {
                TerrainLayer layer = layers[i];
                if (layer == null) continue;

                Texture2D replacement = ChooseTexture(layer.name);

                // IslandExpansionWorld appends two generated stone layers. The first
                // historically had no name, so identify the final pair as stone too.
                if (replacement == null && IslandExpansionWorld.Active != null &&
                    IslandExpansionWorld.Active.Terrain == terrain && i >= layers.Length - 2)
                {
                    replacement = stone;
                }

                if (replacement != null && layer.diffuseTexture != replacement)
                {
                    layer.diffuseTexture = replacement;
                    changed++;
                }
            }
        }

        Debug.Log("[TERRAIN TEXTURES] Applied supplied sand/grass/stone textures to " + changed + " terrain layer(s). Existing paint, tile sizes, normals and terrain geometry were preserved.");
    }

    private static Texture2D ChooseTexture(string layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return null;
        string n = layerName.ToLowerInvariant();
        if (n.Contains("sand")) return sand;
        if (n.Contains("grass") || n.Contains("meadow")) return grass;
        if (n.Contains("rock") || n.Contains("stone") || n.Contains("limestone")) return stone;
        return null;
    }

    private static void EnsureTextures()
    {
        if (sand == null) sand = LoadChunked("Sand", 4);
        if (grass == null) grass = LoadChunked("Grass", 3);
        if (stone == null) stone = LoadChunked("Stone", 3);
    }

    private static Texture2D LoadChunked(string prefix, int chunkCount)
    {
        var encoded = new StringBuilder(chunkCount * 7000);
        for (int i = 0; i < chunkCount; i++)
        {
            string resourcePath = DataRoot + prefix + "_" + i.ToString("00");
            TextAsset chunk = Resources.Load<TextAsset>(resourcePath);
            if (chunk == null || string.IsNullOrWhiteSpace(chunk.text))
            {
                Debug.LogError("[TERRAIN TEXTURES] Missing supplied texture data: " + resourcePath);
                return null;
            }
            encoded.Append(chunk.text.Trim());
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(encoded.ToString());
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, true, false)
            {
                name = "User supplied " + prefix,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                Destroy(texture);
                Debug.LogError("[TERRAIN TEXTURES] Could not decode supplied " + prefix + " texture.");
                return null;
            }
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            return texture;
        }
        catch (Exception ex)
        {
            Debug.LogError("[TERRAIN TEXTURES] Failed loading supplied " + prefix + " texture: " + ex.Message);
            return null;
        }
    }
}
