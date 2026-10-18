using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Nine numbered user frames, upright over the fish's water position.</summary>
public sealed class FishingLightningStrikeAnimation : MonoBehaviour
{
    private const int FrameCount = 9;
    private const float FrameSeconds = .075f;
    private const float CanvasSizeMetres = 6f;
    // One common anchor for all original 1254-square canvases. Never trim
    // individual frames: that would cause the authored bolts to jump sideways.
    private const float WaterlineFromBottom = 82f / 1254f;
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int AlphaId = Shader.PropertyToID("_EffectAlpha");
    private static Texture2D[] frames;
    private static Material material;
    private static Mesh quad;
    private static bool missingAssets;
    private MeshRenderer display;
    private MaterialPropertyBlock properties;
    private int frame;
    private float frameElapsed;
    private float strikeScale = 1f;
    private AudioSource zapAudio;
    private static readonly AudioClip[] zapClips = new AudioClip[4];
    public bool Finished => frame >= FrameCount && (zapAudio == null || !zapAudio.isPlaying);
    public void SetPaused(bool paused)
    {
        if (zapAudio == null) return;
        if (paused) zapAudio.Pause(); else zapAudio.UnPause();
    }

    public static FishingLightningStrikeAnimation Create(Transform owner)
    {
        if (!PrepareAssets()) return null;
        var root = new GameObject("Lightning Strike Above Water");
        root.transform.SetParent(owner, false);
        var effect = root.AddComponent<FishingLightningStrikeAnimation>();
        root.AddComponent<MeshFilter>().sharedMesh = quad;
        effect.display = root.AddComponent<MeshRenderer>();
        effect.display.sharedMaterial = material;
        effect.display.shadowCastingMode = ShadowCastingMode.Off;
        effect.display.receiveShadows = false;
        effect.display.lightProbeUsage = LightProbeUsage.Off;
        effect.display.reflectionProbeUsage = ReflectionProbeUsage.Off;
        effect.display.sortingOrder = 20;
        effect.properties = new MaterialPropertyBlock();
        root.SetActive(false);
        return effect;
    }

    private static bool PrepareAssets()
    {
        if (missingAssets) return false;
        if (frames == null)
        {
            var loaded = new Texture2D[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                loaded[i] = Resources.Load<Texture2D>("Fishing/LightningStrike/Lightning" + (i + 1).ToString("00"));
                if (loaded[i] == null)
                {
                    Debug.LogWarning("Lightning Strike: missing numbered frame " + (i + 1));
                    missingAssets = true;
                    return false;
                }
            }
            frames = loaded;
        }
        if (material == null)
        {
            var shader = Resources.Load<Shader>("Fishing/Hotspots/HotspotParticles");
            if (shader == null) { missingAssets = true; return false; }
            material = new Material(shader) { name = "Lightning Strike Upright Animation" };
            // Upright bolts must not be flattened onto the wave surface.
            material.SetFloat("_FollowSurface", 0f);
            material.SetFloat(AlphaId, 1f);
        }
        if (quad == null)
        {
            float bottom = -WaterlineFromBottom, top = 1f - WaterlineFromBottom;
            quad = new Mesh { name = "Lightning Fixed Canvas" };
            quad.vertices = new[] {
                new Vector3(-.5f,bottom,0f), new Vector3(.5f,bottom,0f),
                new Vector3(-.5f,top,0f), new Vector3(.5f,top,0f)
            };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quad.triangles = new[] { 0,1,2,1,3,2 };
            quad.RecalculateBounds();
            quad.RecalculateNormals();
        }
        return true;
    }

    public void Restart(int zapIndex)
    {
        strikeScale = zapIndex == 0 ? 1.5f : 1.25f;
        if (zapClips[zapIndex] == null)
            zapClips[zapIndex] = Resources.Load<AudioClip>("Fishing/Audio/LightningZap" + (zapIndex + 1));
        if (zapAudio == null) zapAudio = gameObject.AddComponent<AudioSource>();
        zapAudio.playOnAwake = false;
        zapAudio.loop = false;
        zapAudio.volume = 1f;
        zapAudio.spatialBlend = 1f;
        zapAudio.dopplerLevel = 0f;
        zapAudio.rolloffMode = AudioRolloffMode.Logarithmic;
        zapAudio.minDistance = 12f;
        zapAudio.maxDistance = 120f;
        zapAudio.clip = zapClips[zapIndex];
        frame = 0;
        frameElapsed = 0f;
        gameObject.SetActive(true);
        if (zapAudio.clip != null) zapAudio.Play();
    }

    // The combat visual owner calls this only while its damage timer advances.
    // Menu pauses therefore pause both the sequence and the source of new rings.
    public void Tick(Vector3 point, OceanWater water, Camera view)
    {
        display.enabled = frame < FrameCount;
        if (frame < FrameCount) properties.SetTexture(MainTexId, frames[frame]);
        properties.SetFloat(AlphaId, 1f);
        display.SetPropertyBlock(properties);
        // At most one step per rendered frame, so all nine images appear in order.
        frameElapsed += Time.deltaTime;
        if (frameElapsed >= FrameSeconds)
        {
            frameElapsed = Mathf.Min(frameElapsed - FrameSeconds, FrameSeconds);
            frame = Mathf.Min(frame + 1, FrameCount);
        }
        if (PondWater.TrySurface(point, out float pondHeight)) point.y = pondHeight;
        else if (water != null) point.y = water.GetSurfaceHeight(point);
        point.y += .05f;
        transform.position = point;
        transform.localScale = Vector3.one * (CanvasSizeMetres * strikeScale);
        if (view == null) view = Camera.main;
        if (view == null) return;
        Vector3 direction = view.transform.position - point;
        direction.y = 0f;
        // Local forward points away from the viewer to keep the PNG unmirrored.
        if (direction.sqrMagnitude > .0001f)
            transform.rotation = Quaternion.LookRotation(-direction.normalized, Vector3.up);
    }
}
