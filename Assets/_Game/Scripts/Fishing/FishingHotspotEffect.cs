using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Reusable pooled particle effect. The rings conform to the ocean at each mesh vertex.</summary>
[DefaultExecutionOrder(3000)]
public sealed class FishingHotspotEffect : MonoBehaviour
{
    public float ExpiresAt { get; private set; }
    private OceanWater ocean;
    private ParticleSystem rings, bubbles, splashes;
    private ParticleSystemRenderer ringRenderer;
    private MaterialPropertyBlock waves;
    private float nextCluster, nextSplash;

    public void Initialize(OceanWater water, Material rippleMaterial, Material bubbleMaterial, Mesh mesh)
    {
        // Initialize is called by FishingHotspotManager.Start on the main thread.
        // Native Unity objects cannot be created in MonoBehaviour field initializers:
        // Unity also constructs this component while serializing the prefab for builds.
        waves = new MaterialPropertyBlock();
        ocean = water;
        rings = CreateSystem("Ripple Particles", rippleMaterial, 6, 2.5f, 2f * FishingHotspotManager.RippleScale);
        var main = rings.main;
        main.startSpeed = 0f;
        main.startColor = new Color(1f, 1f, 1f, 0.48f);
        var emission = rings.emission; emission.rateOverTime = 0.7f;
        var shape = rings.shape;
        shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f * FishingHotspotManager.RippleScale; shape.rotation = new Vector3(90f, 0f, 0f);
        var size = rings.sizeOverLifetime;
        size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 2.5f));
        ringRenderer = rings.GetComponent<ParticleSystemRenderer>();
        // A subdivided horizontal mesh provides the billboard's flat orientation,
        // but lets the shader bend the enlarged ring over large wave crests.
        ringRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        ringRenderer.mesh = mesh;
        ringRenderer.alignment = ParticleSystemRenderSpace.World;
        ringRenderer.localBounds = new Bounds(Vector3.zero, new Vector3(8f * FishingHotspotManager.RippleScale, 16f, 8f * FishingHotspotManager.RippleScale));

        bubbles = CreateSystem("Bubble Particles", bubbleMaterial, 24, 1.3f, 0.14f);
        main = bubbles.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.20f);
        main.startColor = new Color(1f, 1f, 1f, 0.70f);
        splashes = CreateSystem("Splash Particles", bubbleMaterial, 12, 0.65f, 0.08f);
        main = splashes.main;
        main.gravityModifier = 0.32f;
        main.startColor = new Color(1f, 1f, 1f, 0.75f);
    }

    private ParticleSystem CreateSystem(string label, Material material, int maximum, float lifetime, float size)
    {
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        var ps = child.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false; main.duration = 5f;
        main.startLifetime = lifetime; main.startSpeed = 0f; main.startSize = size;
        main.maxParticles = maximum; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.cullingMode = ParticleSystemCullingMode.Pause;
        var emission = ps.emission; emission.rateOverTime = 0f;
        var shape = ps.shape; shape.enabled = false;
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f),
                new GradientAlphaKey(0.75f, 0.55f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.maxParticleSize = 1f;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
        return ps;
    }

    public void Place(Vector3 position, float expiresAt)
    {
        position.y = ocean.GetSurfaceHeight(position) + 0.05f;
        transform.position = position;
        ExpiresAt = expiresAt;
        gameObject.SetActive(true);
        rings.Play(); bubbles.Play(); splashes.Play();
        // Start with an already visible expanding ring, rather than an empty target.
        rings.Simulate(Random.Range(0.6f, 1.5f), false, true, false);
        rings.Play();
        nextCluster = Time.time + Random.Range(0.2f, 0.8f);
        nextSplash = Time.time + Random.Range(4f, 8f);
    }

    private void LateUpdate()
    {
        if (ocean == null || rings == null) return;
        Vector3 p = transform.position;
        p.y = ocean.GetSurfaceHeight(p) + 0.05f;
        transform.position = p;
        ocean.CopyWaveProperties(waves);
        waves.SetFloat("_HotspotSeaLevel", ocean.BaseWaterLevel);
        ringRenderer.SetPropertyBlock(waves);
        if (Time.time >= nextCluster)
        {
            nextCluster = Time.time + Random.Range(1.3f, 2.8f);
            Vector2 centre = Random.insideUnitCircle * (1.2f * FishingHotspotManager.RippleScale);
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                Vector2 jitter = Random.insideUnitCircle * (0.2f * FishingHotspotManager.RippleScale);
                var particle = new ParticleSystem.EmitParams {
                    position = new Vector3(centre.x + jitter.x, -0.16f, centre.y + jitter.y),
                    velocity = new Vector3(jitter.x * 0.15f, Random.Range(0.20f, 0.34f), jitter.y * 0.15f)
                };
                bubbles.Emit(particle, 1);
            }
        }
        if (Time.time >= nextSplash)
        {
            nextSplash = Time.time + Random.Range(4f, 8f);
            Vector2 centre = Random.insideUnitCircle * (0.9f * FishingHotspotManager.RippleScale);
            for (int i = 0; i < 5; i++)
            {
                Vector2 drift = Random.insideUnitCircle * 0.55f;
                splashes.Emit(new ParticleSystem.EmitParams {
                    position = new Vector3(centre.x, 0.03f, centre.y),
                    velocity = new Vector3(drift.x, Random.Range(0.55f, 0.95f), drift.y)
                }, 1);
            }
        }
    }

    private void OnDisable()
    {
        if (rings != null) rings.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (bubbles != null) bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (splashes != null) splashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public static Mesh CreateRippleMesh()
    {
        const int steps = 12;
        var vertices = new Vector3[(steps + 1) * (steps + 1)];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[steps * steps * 6];
        for (int z = 0; z <= steps; z++)
        for (int x = 0; x <= steps; x++)
        {
            int i = z * (steps + 1) + x;
            uv[i] = new Vector2(x / (float)steps, z / (float)steps);
            vertices[i] = new Vector3(uv[i].x - 0.5f, 0f, uv[i].y - 0.5f);
        }
        int t = 0;
        for (int z = 0; z < steps; z++)
        for (int x = 0; x < steps; x++)
        {
            int a = z * (steps + 1) + x, b = a + steps + 1;
            triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
            triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
        }
        var mesh = new Mesh { name = "Wave Following Ripple Grid", vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
}
