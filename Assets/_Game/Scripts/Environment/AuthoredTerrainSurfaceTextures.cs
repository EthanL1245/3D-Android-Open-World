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
        foreach(Terrain terrain in FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            ApplyToTerrain(terrain);
    }

    public static void ApplyToTerrain(Terrain terrain)
    {
        // Saved fishing maps own persistent TerrainData and hand-painted layers.
        // Never swap them for temporary runtime copies when PrototypeWorld loads.
        // The one-time editor recovery bakes stable layers into the scene instead.
        if (Application.isPlaying && terrain != null)
        {
            var world = FindFirstObjectByType<IslandExpansionWorld>(FindObjectsInactive.Include);
            if (world != null && world.HasSavedLayout && world.Terrain == terrain)
                return;
        }
        EnsureTextures();
        if(sand==null || grass==null || stone==null || terrain==null || terrain.terrainData==null)return;
        TerrainData data=terrain.terrainData;
        TerrainLayer[] layers=data.terrainLayers;
        bool changed=false;
        for(int i=0;i<layers.Length;i++)
        {
            TerrainLayer layer=layers[i];
            Texture2D replacement=layer!=null?ChooseTexture(layer.name):null;
            // Old expanded maps used a nameless stone layer at index 3. Appending
            // Snapper's old layer shifted the "last two" heuristic; use roles.
            if(replacement==null && layers.Length>=5)
                replacement=i==0?sand:i==1?grass:(i>=2?stone:null);
            if(replacement==null || (layer!=null && layer.diffuseTexture==replacement))continue;
            var copy=layer!=null?Instantiate(layer):new TerrainLayer();
            copy.name=layer!=null?layer.name:(i==0?"Warm sand":i==1?"Coastal meadow":"Restored stone");
            copy.diffuseTexture=replacement;
            if(layer==null)copy.tileSize=Vector2.one*(i==0?5f:i==1?6f:9f);
            layers[i]=copy;
            changed=true;
        }
        if(!changed)return;
        // Assign copies, not shared terrain layer assets; preserve weights across
        // Unity's layer assignment. This also works during editor migration.
        float[,,] paint=data.GetAlphamaps(0,0,data.alphamapWidth,data.alphamapHeight);
        if(Application.isPlaying)
        {
            data=Instantiate(data);
            terrain.terrainData=data;
            var collider=terrain.GetComponent<TerrainCollider>();
            if(collider!=null)collider.terrainData=data;
        }
        data.terrainLayers=layers;
        data.SetAlphamaps(0,0,paint);
        terrain.Flush();
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
