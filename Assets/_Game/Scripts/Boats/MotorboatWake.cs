using UnityEngine;
using UnityEngine.Rendering;

// Presentation only. Samples rendered transforms, including interpolated replicas;
// never writes boat transforms, rigidbodies, input, or multiplayer state.
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class MotorboatWake : MonoBehaviour
{
    private const int Capacity = 80, RowVertices = 9;
    private const float Lifetime = 4f, SampleInterval = .05f;
    private struct Point
    {
        public Vector3 Position, Right;
        public float Born, Distance, Strength, Turn;
        public int Strip;
    }
    private readonly Point[] history = new Point[Capacity];
    private readonly Vector3[] vertices = new Vector3[Capacity * RowVertices];
    private readonly Vector2[] uv = new Vector2[Capacity * RowVertices];
    private readonly Color[] colors = new Color[Capacity * RowVertices];
    private readonly int[] indices = new int[(Capacity - 1) * 36];
    private BoatController boat;
    private OceanWater ocean;
    private Camera view;
    private GameObject visual;
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock properties;
    private static Material sharedMaterial;
    private static Texture2D sharedFoam;
    private static int users;
    private int count, first, strip;
    private float nextSample, nextMesh, nextCameraLookup, lastTime, lastYaw, distance, strength, turn;
    private Vector3 lastPosition;
    private bool tracking, emitting, ownsResources;
    private static readonly int BaseLevelId = Shader.PropertyToID("_BaseLevel");
    private static readonly int NowId = Shader.PropertyToID("_WakeTime");

    public static void Attach(BoatController owner, OceanWater water)
    {
        if (!owner.IsMotorboat || water == null) return;
        var wake = owner.GetComponent<MotorboatWake>();
        if (wake == null) wake = owner.gameObject.AddComponent<MotorboatWake>();
        wake.boat = owner;
        wake.ocean = water;
        wake.ResolveView();
    }

    private bool Prepare()
    {
        if (mesh != null) return true;
        if (sharedMaterial == null)
        {
            // Resource reference keeps the shader in Android builds (no Shader.Find).
            var template = Resources.Load<Material>("Boats/MotorboatWake");
            if (template == null)
            {
                Debug.LogError("Motorboat wake material is missing: Resources/Boats/MotorboatWake", this);
                enabled = false;
                return false;
            }
            sharedMaterial = new Material(template) { name = "Shared motorboat foam" };
            sharedFoam = CreateFoam();
            sharedMaterial.mainTexture = sharedFoam;
        }
        users++;
        ownsResources = true;
        // Independent world-space object: old wake never rotates with the hull.
        visual = new GameObject("Motorboat wake (visual only)");
        visual.layer = gameObject.layer;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(visual, gameObject.scene);
        mesh = new Mesh { name = "Bounded motorboat wake" };
        mesh.MarkDynamic();
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = visual.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = sharedMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.enabled = false;
        properties = new MaterialPropertyBlock();
        return true;
    }

    private void LateUpdate()
    {
        if (boat == null || !boat.isActiveAndEnabled || ocean == null)
        {
            ResetTrail();
            return;
        }
        float now = Time.time;
        if (view == null || !view.isActiveAndEnabled)
        {
            // PrototypeWorld's PlayerCamera is intentionally untagged. Do not
            // depend on Camera.main; all local and replica wakes use this player.
            if (now >= nextCameraLookup)
            {
                nextCameraLookup = now + 1f;
                ResolveView();
            }
        }
        // Include the longest possible 108 m trail when deciding to stop work.
        if (view == null || (view.transform.position - transform.position).sqrMagnitude > 250f * 250f)
        {
            ResetTrail();
            return;
        }
        if (now >= nextSample)
        {
            nextSample = now + SampleInterval;
            Sample(now);
        }
        while (count > 0 && now - history[first].Born >= Lifetime)
        {
            first = (first + 1) % Capacity;
            count--;
        }
        if (count < 2)
        {
            if (meshRenderer != null) meshRenderer.enabled = false;
            return;
        }
        if (!Prepare()) return;
        // After OceanWater.LateUpdate: phases match this frame, even between mesh updates.
        ocean.CopyWaveProperties(properties);
        properties.SetFloat(BaseLevelId, ocean.BaseWaterLevel);
        properties.SetFloat(NowId, now);
        meshRenderer.SetPropertyBlock(properties);
        if (now < nextMesh) return;
        float range = mesh.bounds.SqrDistance(view.transform.position);
        int step = range > 65f * 65f ? 2 : 1;
        nextMesh = now + (step == 1 ? .05f : .1f);
        Build(now, step);
    }

    private void ResolveView()
    {
        var player = FindFirstObjectByType<FirstPersonController>();
        view = player != null ? player.ViewCamera : null;
        if (view == null || !view.isActiveAndEnabled) view = Camera.main;
    }

    private void Sample(float now)
    {
        Vector3 position = transform.position;
        position.y = 0f;
        float yaw = transform.eulerAngles.y;
        float elapsed = now - lastTime;
        Vector3 movement = position - lastPosition;
        if (!tracking || elapsed > .4f || movement.magnitude > 40f * elapsed + 1f)
        {
            // Placement, pooling, long frame stalls and network teleports cannot draw streaks.
            ResetTrail();
            tracking = true;
        }
        else if (elapsed > .001f)
        {
            float speed = movement.magnitude / elapsed;
            float target = Mathf.InverseLerp(.7f, 27f, speed);
            strength = Mathf.Lerp(strength, target, 1f - Mathf.Exp(-elapsed * 8f));
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / elapsed;
            turn = Mathf.Lerp(turn, Mathf.Clamp(yawRate / 32f, -1f, 1f), 1f - Mathf.Exp(-elapsed * 6f));
            if (speed > .7f && movement.sqrMagnitude > .0004f)
            {
                if (!emitting) strip++;
                emitting = true;
                distance += movement.magnitude;
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                Point point = new Point {
                    Position = position - forward * (boat.Data.HullSize.z * .49f),
                    Right = Vector3.Cross(Vector3.up, forward), Born = now,
                    Distance = distance, Strength = strength, Turn = turn, Strip = strip
                };
                int slot = (first + count) % Capacity;
                if (count == Capacity) first = (first + 1) % Capacity;
                else count++;
                history[slot] = point;
            }
            else emitting = false;
        }
        lastPosition = position;
        lastYaw = yaw;
        lastTime = now;
    }

    private void Build(float now, int step)
    {
        int rows = 0, triangles = 0, previousStrip = -1;
        Vector3 minimum = new Vector3(float.MaxValue, 0, float.MaxValue);
        Vector3 maximum = new Vector3(float.MinValue, 0, float.MinValue);
        // Always include newest sample even in reduced-detail mode.
        for (int i = 0; i < count; i = Mathf.Min(i + step, count - 1))
        {
            Point p = history[(first + i) % Capacity];
            float age = now - p.Born;
            float turbulence = Mathf.Abs(p.Turn) * Mathf.Clamp01(p.Strength * 3f);
            float spread = age * Mathf.Lerp(.6f, 2.2f, p.Strength);
            for (int ribbon = 0; ribbon < 3; ribbon++)
            {
                int side = ribbon - 1;
                float offset = side == 0 ? 0f : side * (boat.Data.HullSize.x * .32f + spread);
                float width = side == 0 ? Mathf.Lerp(.4f, 1.1f, p.Strength) + age * .25f
                    : .22f + p.Strength * .32f + age * .13f;
                width *= 1f + turbulence * .65f;
                // More broken, wider foam on sharp turns; stronger outer wake.
                offset += Mathf.Sin(p.Distance * 2.1f + age * 3f) * turbulence * .3f;
                float opacity = Mathf.Clamp01((.15f + p.Strength * .65f) *
                    (1f + turbulence * .4f - side * p.Turn * .15f));
                for (int across = 0; across < 3; across++)
                {
                    int v = rows * RowVertices + ribbon * 3 + across;
                    Vector3 point = p.Position + p.Right * (offset + (across - 1) * width);
                    vertices[v] = point;
                    uv[v] = new Vector2(across * .5f, p.Distance * .35f + ribbon * .37f);
                    // Shader fades every frame, including between CPU mesh updates.
                    colors[v] = new Color(p.Born, opacity, turbulence, across == 1 ? 1f : 0f);
                    minimum = Vector3.Min(minimum, point);
                    maximum = Vector3.Max(maximum, point);
                }
                if (rows > 0 && previousStrip == p.Strip)
                {
                    int a = (rows - 1) * RowVertices + ribbon * 3;
                    for (int j = 0; j < 2; j++)
                    {
                        indices[triangles++] = a + j;
                        indices[triangles++] = a + j + RowVertices;
                        indices[triangles++] = a + j + 1;
                        indices[triangles++] = a + j + 1;
                        indices[triangles++] = a + j + RowVertices;
                        indices[triangles++] = a + j + RowVertices + 1;
                    }
                }
            }
            rows++;
            previousStrip = p.Strip;
            if (i == count - 1) break;
        }
        // Remove old indices before a LOD switch shrinks the vertex count.
        mesh.SetTriangles(indices, 0, 0, 0, false);
        mesh.SetVertices(vertices, 0, rows * RowVertices);
        mesh.SetUVs(0, uv, 0, rows * RowVertices);
        mesh.SetColors(colors, 0, rows * RowVertices);
        mesh.SetTriangles(indices, 0, triangles, 0, false);
        Vector3 center = (minimum + maximum) * .5f;
        center.y = ocean.BaseWaterLevel;
        // Conservative allowance for GPU wave displacement and offshore storm waves.
        mesh.bounds = new Bounds(center, maximum - minimum + new Vector3(4f, 24f, 4f));
        meshRenderer.enabled = triangles > 0;
    }

    private static Texture2D CreateFoam()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true) {
            name = "Procedural shared foam", wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear, anisoLevel = 0
        };
        var pixels = new Color32[size * size];
        // Periodic noise makes a seamless tile without assets or global Random state.
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)size, v = y / (float)size;
            float noise = 0f, weight = 0f;
            for (int octave = 0; octave < 3; octave++)
            {
                float frequency = 2 << octave;
                float a = 1f / (1 << octave);
                float px = u * frequency, py = v * frequency;
                float n = Mathf.Lerp(
                    Mathf.Lerp(Mathf.PerlinNoise(px + 13f, py + 7f), Mathf.PerlinNoise(px - frequency + 13f, py + 7f), u),
                    Mathf.Lerp(Mathf.PerlinNoise(px + 13f, py - frequency + 7f), Mathf.PerlinNoise(px - frequency + 13f, py - frequency + 7f), u), v);
                noise += n * a; weight += a;
            }
            byte value = (byte)(Mathf.Clamp01((noise / weight - .28f) * 2.2f) * 255f);
            pixels[y * size + x] = new Color32(value, value, value, 255);
        }
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }

    private void ResetTrail()
    {
        count = first = 0;
        tracking = emitting = false;
        strength = turn = distance = 0f;
        if (meshRenderer != null) meshRenderer.enabled = false;
    }
    private void OnDisable() { ResetTrail(); }
    private void OnDestroy()
    {
        if (visual != null) Destroy(visual);
        if (mesh != null) Destroy(mesh);
        if (!ownsResources) return;
        if (--users == 0)
        {
            Destroy(sharedMaterial);
            Destroy(sharedFoam);
            sharedMaterial = null;
            sharedFoam = null;
        }
    }
}
