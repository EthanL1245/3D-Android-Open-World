using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Plays 18 selected frames from the user-authored 20-frame splash sequence at a successful water landing.
/// Add the matching Splash01..Splash20 textures except Splash14 and Splash17 to Resources/Fishing/CastSplash.
/// This visual is independent of the persistent fishing hotspots and existing audio.
/// </summary>
public sealed class FishingCastSplashAnimation : MonoBehaviour
{
    private const int FrameCount = 18;
    // Preserve original numbering; skip Splash14 and Splash17 intentionally.
    private static readonly int[] FrameNumbers =
        { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 15, 16, 18, 19, 20 };
    private const float FramesPerSecond = 20f;
    private const float WorldWidthMeters = 2.4f;
    // The supplied images have the waterline close to the bottom of the canvas.
    private const float WaterlineFromBottom = 0.15f;
    private const string ResourcePrefix = "Fishing/CastSplash/Splash";
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    private static Texture2D[] frames;
    private static Material sharedMaterial;
    private static Mesh sharedQuad;
    private static bool loggedMissingFrames;

    private OceanWater ocean;
    private Camera viewCamera;
    private Vector3 landingPosition;
    private MeshRenderer splashRenderer;
    private MaterialPropertyBlock properties;
    private int frameIndex;
    private float frameElapsed;

    /// <summary>Called once at the same point as the successful-landing audio.</summary>
    public static void Spawn(Vector3 landing, OceanWater water, Camera view)
    {
        if (!PrepareAssets()) return;

        GameObject root = new GameObject("Cast Water Splash (20 frames)");
        FishingCastSplashAnimation animation = root.AddComponent<FishingCastSplashAnimation>();
        animation.ocean = water;
        animation.viewCamera = view;
        animation.landingPosition = landing;
        animation.properties = new MaterialPropertyBlock();

        var filter = root.AddComponent<MeshFilter>();
        filter.sharedMesh = sharedQuad;
        animation.splashRenderer = root.AddComponent<MeshRenderer>();
        animation.splashRenderer.sharedMaterial = sharedMaterial;
        animation.splashRenderer.shadowCastingMode = ShadowCastingMode.Off;
        animation.splashRenderer.receiveShadows = false;
        animation.splashRenderer.lightProbeUsage = LightProbeUsage.Off;
        animation.splashRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        animation.splashRenderer.sortingOrder = 20;

        animation.ShowFrame(0);
        animation.AlignToWaterAndCamera();
    }

    private static bool PrepareAssets()
    {
        if (frames == null)
        {
            Texture2D[] loaded = new Texture2D[FrameCount];
            for (int i = 0; i < FrameCount; ++i)
            {
                // Preserve sequence without renaming source artwork; 14 and 17 are omitted.
                loaded[i] = Resources.Load<Texture2D>(ResourcePrefix + FrameNumbers[i].ToString("00"));
                if (loaded[i] == null)
                {
                    if (!loggedMissingFrames)
                    {
                        Debug.LogWarning("Casting splash is missing Resources/Fishing/CastSplash/Splash" +
                                         FrameNumbers[i].ToString("00") + ".png. Import the 18 selected splash frames.");
                        loggedMissingFrames = true;
                    }
                    return false;
                }
            }
            frames = loaded;
        }

        if (sharedMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("Fishing/Hotspots/HotspotParticles");
            if (shader == null) shader = Shader.Find("OpenWorld/FishingHotspotParticles");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                Debug.LogWarning("Casting splash: no transparent unlit shader is available.");
                return false;
            }
            sharedMaterial = new Material(shader) { name = "Cast Splash Shared Material" };
            // URP/Unlit fallback defaults to opaque unless explicitly set to transparent.
            if (shader.name == "Universal Render Pipeline/Unlit")
            {
                sharedMaterial.SetFloat("_Surface", 1f);
                sharedMaterial.SetFloat("_Blend", 0f);
                sharedMaterial.SetOverrideTag("RenderType", "Transparent");
                sharedMaterial.renderQueue = (int)RenderQueue.Transparent;
                sharedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                sharedMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                sharedMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                sharedMaterial.SetInt("_ZWrite", 0);
            }
        }

        if (sharedQuad == null)
        {
            float bottom = -WaterlineFromBottom;
            float top = 1f - WaterlineFromBottom;
            sharedQuad = new Mesh { name = "Cast Splash Upright Quad" };
            sharedQuad.vertices = new[] {
                new Vector3(-0.5f, bottom, 0f), new Vector3(0.5f, bottom, 0f),
                new Vector3(-0.5f, top, 0f), new Vector3(0.5f, top, 0f)
            };
            sharedQuad.uv = new[] {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            sharedQuad.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            sharedQuad.RecalculateBounds();
            sharedQuad.RecalculateNormals();
        }
        return true;
    }

    private void ShowFrame(int index)
    {
        if (splashRenderer == null || frames == null) return;
        properties.SetTexture(MainTexId, frames[index]);
        properties.SetTexture(BaseMapId, frames[index]);
        splashRenderer.SetPropertyBlock(properties);
    }

    private void LateUpdate()
    {
        // Advance at most one image per rendered frame: under heavy mobile load,
        // every supplied frame still appears in its intended numeric order.
        frameElapsed += Time.deltaTime;
        if (frameElapsed >= 1f / FramesPerSecond)
        {
            frameElapsed = Mathf.Min(frameElapsed - 1f / FramesPerSecond, 1f / FramesPerSecond);
            ++frameIndex;
            if (frameIndex >= FrameCount)
            {
                Destroy(gameObject);
                return;
            }
            ShowFrame(frameIndex);
        }
        AlignToWaterAndCamera();
    }

    private void AlignToWaterAndCamera()
    {
        Vector3 point = landingPosition;
        if (ocean != null) point.y = ocean.GetSurfaceHeight(point);
        point.y += 0.04f;
        transform.position = point;
        transform.localScale = Vector3.one * WorldWidthMeters;

        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null) return;
        Vector3 toViewer = viewCamera.transform.position - point;
        toViewer.y = 0f;
        if (toViewer.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toViewer.normalized, Vector3.up);
    }
}
