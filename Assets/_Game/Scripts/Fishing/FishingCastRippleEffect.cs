using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One-shot, horizontal water ring spawned on successful bait or lure landings.
/// Expands from a tiny ripple to a visible 1 m radius and fades completely away.
/// Independent of the five persistent fishing hotspots and upright cast splash.
/// </summary>
public sealed class FishingCastRippleEffect : MonoBehaviour
{
    private const string TexturePath = "Fishing/CastRipple/LureHitsWaterRipple";
    private const string ShaderPath = "Fishing/Hotspots/HotspotParticles";
    private const float StartRadius = 0.08f;
    private const float MaximumRadius = 1f;
    private const float DurationSeconds = 1.25f;
    private const float SurfaceOffset = 0.06f;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int EffectAlphaId = Shader.PropertyToID("_EffectAlpha");
    private static readonly int FollowSurfaceId = Shader.PropertyToID("_FollowSurface");
    private static readonly int SeaLevelId = Shader.PropertyToID("_HotspotSeaLevel");
    private static Texture2D texture;
    private static Material material;
    private static Mesh quadMesh;
    private static bool missingAssetsLogged;

    private OceanWater ocean;
    private Vector3 impactPosition;
    private MeshRenderer rippleRenderer;
    private MaterialPropertyBlock properties;
    private bool overPond;
    private float age;

    /// <summary>Call only after a successful, validated landing on water.</summary>
    public static void Spawn(Vector3 point, OceanWater water)
    {
        if (water == null || !PrepareSharedAssets()) return;

        var gameObject = new GameObject("Cast Water Impact Ripple");
        var ripple = gameObject.AddComponent<FishingCastRippleEffect>();
        ripple.ocean = water;
        ripple.impactPosition = point;
        ripple.overPond = PondWater.TrySurface(point, out _);
        ripple.properties = new MaterialPropertyBlock();

        var filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = quadMesh;

        ripple.rippleRenderer = gameObject.AddComponent<MeshRenderer>();
        ripple.rippleRenderer.sharedMaterial = material;
        ripple.rippleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        ripple.rippleRenderer.receiveShadows = false;
        ripple.rippleRenderer.lightProbeUsage = LightProbeUsage.Off;
        ripple.rippleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        ripple.rippleRenderer.sortingOrder = 10;

        ripple.Apply(0f);
    }

    private static bool PrepareSharedAssets()
    {
        if (texture == null)
            texture = Resources.Load<Texture2D>(TexturePath);
        if (texture == null)
        {
            if (!missingAssetsLogged)
            {
                Debug.LogWarning("Cast ripple PNG missing from Resources/" + TexturePath);
                missingAssetsLogged = true;
            }
            return false;
        }

        if (material == null)
        {
            Shader shader = Resources.Load<Shader>(ShaderPath);
            if (shader == null)
            {
                if (!missingAssetsLogged)
                {
                    Debug.LogWarning("Cast ripple could not load the fishing hotspot shader.");
                    missingAssetsLogged = true;
                }
                return false;
            }

            material = new Material(shader) { name = "Shared Cast Impact Ripple" };
            material.SetTexture(MainTexId, texture);
            material.SetFloat(FollowSurfaceId, 1f);
            material.SetFloat(EffectAlphaId, 1f);
        }

        if (quadMesh == null)
            quadMesh = CreateSubdividedQuad();

        return true;
    }

    private static Mesh CreateSubdividedQuad()
    {
        // Eight subdivisions let the existing hotspot shader follow ocean waves,
        // rather than cutting straight through them as a single flat quad.
        const int subdivisions = 8;
        int edge = subdivisions + 1;
        var vertices = new Vector3[edge * edge];
        var uvs = new Vector2[vertices.Length];
        var colors = new Color[vertices.Length];
        var triangles = new int[subdivisions * subdivisions * 6];

        int triangle = 0;
        for (int y = 0; y < edge; y++)
        for (int x = 0; x < edge; x++)
        {
            int index = y * edge + x;
            float u = x / (float)subdivisions;
            float v = y / (float)subdivisions;
            vertices[index] = new Vector3(u - 0.5f, 0f, v - 0.5f);
            uvs[index] = new Vector2(u, v);
            colors[index] = Color.white;

            if (x == subdivisions || y == subdivisions) continue;
            int a = index;
            int b = index + 1;
            int c = index + edge;
            int d = c + 1;
            triangles[triangle++] = a;
            triangles[triangle++] = c;
            triangles[triangle++] = b;
            triangles[triangle++] = b;
            triangles[triangle++] = c;
            triangles[triangle++] = d;
        }

        var mesh = new Mesh { name = "Cast Ripple Ocean-Following Mesh" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / DurationSeconds);
        Apply(progress);

        if (progress >= 1f)
            Destroy(gameObject);
    }

    private void Apply(float progress)
    {
        if (rippleRenderer == null || ocean == null) return;

        // Ease out to a true one-metre visual radius; fade fully to zero alpha.
        float smooth = progress * progress * (3f - 2f * progress);
        float radius = Mathf.Lerp(StartRadius, MaximumRadius, smooth);
        float opacity = 1f - smooth;

        Vector3 surface = impactPosition;
        surface.y = ocean.GetSurfaceHeight(impactPosition) + SurfaceOffset;
        transform.position = surface;
        transform.rotation = Quaternion.identity;
        float diameter = radius * 2f;
        transform.localScale = new Vector3(diameter, 1f, diameter);

        ocean.CopyWaveProperties(properties);
        properties.SetFloat(FollowSurfaceId, overPond ? 0f : 1f);
        properties.SetFloat(SeaLevelId, ocean.BaseWaterLevel);
        properties.SetFloat(EffectAlphaId, opacity);
        rippleRenderer.SetPropertyBlock(properties);
    }
}
