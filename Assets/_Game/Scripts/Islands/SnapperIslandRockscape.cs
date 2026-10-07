using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Connected coastal outcrops built only from the six supplied rock meshes.</summary>
public sealed class SnapperIslandRockscape : MonoBehaviour
{
    private const string Root = "Islands/SnapperIsland/Rocks/";
    private readonly GameObject[] rocks = new GameObject[6];
    private Terrain terrain;
    private float sea;
    private Vector3 center;
    private Vector3 arrival;
    private Transform holder;
    private Material material;
    private System.Random rng;
    private int pieceCount;

    public bool Build(Terrain islandTerrain, float waterLevel, Vector3 islandCenter, Vector3 arrivalPoint)
    {
        terrain = islandTerrain;
        sea = waterLevel;
        center = islandCenter;
        arrival = arrivalPoint;
        rng = new System.Random(77341);
        for (int i = 0; i < rocks.Length; i++)
        {
            rocks[i] = Resources.Load<GameObject>(Root + "Rock" + (i + 1));
            if (rocks[i] == null)
            {
                Debug.LogError("[SNAPPER ROCKS] Missing source FBX Rock" + (i + 1));
                return false;
            }
        }

        Material source = Resources.Load<Material>(Root + "WorldRock");
        Texture2D atlas = Resources.Load<Texture2D>(Root + "WeatheredRockAtlas");
        Shader shader = Resources.Load<Shader>(Root + "SnapperRockBoxProjection");
        if (source == null || atlas == null || shader == null) return false;
        // Do not modify a project material when entering Play Mode.
        material = new Material(source) { name = "Snapper weathered coastal stone", shader = shader };
        material.SetTexture("_BaseMap", atlas);
        material.SetColor("_BaseColor", new Color(.92f, .90f, .86f, 1f));
        material.SetFloat("_Tiling", .14f);
        material.SetFloat("_AmbientLift", .12f);
        material.SetFloat("_SeaLevel", sea);
        material.enableInstancing = true;
        holder = new GameObject("Snapper Coastal Rockscape").transform;
        holder.SetParent(transform, false);

        // The ledges overlap across both rows and columns. Broad basal pieces
        // carry smaller fractured crowns; nothing relies on the FBX import scale.
        Band("Rear escarpment", -21f, -31f, 19f, 8.2f, 12.4f, 10.5f, 10.8f, 15f);
        Band("Central cliff", -12f, -34f, 21f, 8.0f, 13.5f, 12.0f, 11.8f, -12f);
        Band("Front ledges", -1f, -33f, 17f, 8.5f, 12.3f, 8.2f, 10.6f, 10f);
        Band("West lower ledge", 10f, -37f, -13f, 7.0f, 10.4f, 6.4f, 8.8f, -20f);
        Band("Rear foot", -30f, -24f, 20f, 8.0f, 10.6f, 6.6f, 9.4f, 25f);

        // Unequal summit blocks make one strong peak left of centre, plus a
        // lower independent eastern buttress, as in the supplied reference.
        Place("Summit west", 0, -10f, -14f, 11f, 13.5f, 9.5f, 5f, -17f, -8f, .24f);
        Place("Summit east", 4, -2f, -17f, 10f, 11.8f, 9.5f, -6f, 37f, 4f, .26f);
        Place("Summit fracture", 2, -7f, -10f, 6.8f, 11.5f, 7.0f, 8f, 115f, -12f, .24f);
        Place("West shoulder", 1, -26f, -8f, 13.8f, 10.2f, 11.5f, -5f, 68f, 9f, .30f);
        Place("East buttress rear", 0, 33f, -13f, 12.5f, 12.8f, 11.5f, 6f, 25f, -6f, .26f);
        Place("East buttress front", 4, 34f, -5f, 13f, 10.5f, 11f, -5f, 71f, 7f, .28f);
        Place("East buttress shoulder", 1, 41f, -8f, 10f, 8.5f, 10.5f, 8f, 140f, -8f, .30f);
        Place("East buttress toe", 5, 37f, 5f, 10.2f, 5.8f, 9.5f, 74f, 28f, 7f, .36f);

        // Angular fallen blocks follow the feet of the actual ledges, leaving
        // the arrival beach and the sandy gully east of the main ridge open.
        Talus("West talus", -30f, 16f, 14f, 5f, 14);
        Talus("Ridge talus", -11f, 9f, 11f, 4f, 12);
        Talus("East talus", 36f, 12f, 10f, 6f, 12);
        Talus("Rear talus", -2f, -33f, 26f, 3f, 16);
        Shore(-49f, 0f, 18f, 8.5f);
        Shore(49f, -10f, 70f, 8f);
        Shore(-40f, 26f, 110f, 7f);
        Shore(40f, 25f, -40f, 6.5f);
        Shore(-31f, -35f, 50f, 7f);
        Shore(25f, -37f, 10f, 6.5f);
        Shore(-16f, 40f, 160f, 4.5f);
        Shore(23f, 38f, 85f, 5f);
        Physics.SyncTransforms();
        Debug.Log("[SNAPPER ROCKS] Built " + pieceCount + " overlapping source-mesh rocks.");
        return true;
    }

    private void Band(string label, float z, float left, float right, float spacing,
        float width, float height, float depth, float yaw)
    {
        for (float x = left; x <= right; x += spacing)
        {
            float px = x + N(-1.0f, 1.0f);
            float pz = z + N(-1.2f, 1.2f);
            float taper = Mathf.Lerp(.78f, 1f, 1f - Mathf.Abs(x + 7f) / 55f);
            Place(label, rng.Next(6), px, pz, width * N(.93f, 1.08f), height * taper * N(.9f, 1.1f),
                depth * N(.93f, 1.08f), N(-10f, 10f), yaw + N(-32f, 32f), N(-12f, 12f), .32f);
            // A sideways slab stitches the lower joints between adjacent blocks.
            Place(label + " basal slab", rng.Next(6), px + spacing * .42f, pz + 3.5f,
                width * .70f, height * .72f, depth * .60f,
                78f + N(-12f, 12f), yaw + N(-40f, 40f), N(-15f, 15f), .40f);
        }
    }

    private void Talus(string label, float x, float z, float rx, float rz, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = N(0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(N(.08f, 1f));
            float size = N(2.2f, 4.8f);
            Place(label, rng.Next(6), x + Mathf.Cos(angle) * rx * radius, z + Mathf.Sin(angle) * rz * radius,
                size, size * N(.8f, 1.3f), size * N(.75f, 1.1f), N(45f, 100f), N(0f, 360f), N(-20f, 20f), .40f);
        }
    }

    private void Shore(float x, float z, float yaw, float size)
    {
        for (int i = 0; i < 4; i++)
        {
            float scale = i == 0 ? 1f : N(.42f, .72f);
            Place("Wave-worn shoreline", (pieceCount + i) % 6,
                x + (i == 0 ? 0f : N(-4f, 4f)), z + (i == 0 ? 0f : N(-3f, 3f)),
                size * scale, size * scale * .9f, size * scale * .85f,
                i % 2 == 0 ? 70f : 12f, yaw + i * 63f, N(-16f, 16f), .38f, true);
        }
    }

    private void Place(string label, int asset, float x, float z, float width, float height, float depth,
        float pitch, float yaw, float roll, float burial, bool shoreline = false)
    {
        Vector3 p = center + new Vector3(x, 0f, z);
        // Check the footprint, not just the pivot, so large tilted rocks cannot
        // overhang the teleport spot or block the first steps onto the island.
        float footprint = Mathf.Sqrt(width * width + height * height + depth * depth) * .5f;
        if (Vector3.ProjectOnPlane(p - arrival, Vector3.up).magnitude < footprint + 5f) return;
        GameObject pivot = new GameObject(label + " " + (++pieceCount));
        pivot.transform.SetParent(holder, false);
        GameObject model = Instantiate(rocks[asset], pivot.transform);
        model.transform.localPosition = Vector3.zero;
        // Retain any source axis conversion; normalize the resulting bounds.
        Bounds original = WorldBounds(model);
        Vector3 size = original.size;
        if (Mathf.Min(size.x, Mathf.Min(size.y, size.z)) < .0001f)
        {
            Destroy(pivot);
            return;
        }
        pivot.transform.localScale = new Vector3(width / size.x, height / size.y, depth / size.z);
        Bounds scaled = WorldBounds(model);
        model.transform.position -= scaled.center;
        pivot.transform.rotation = Quaternion.Euler(pitch, yaw, roll);
        pivot.transform.position = new Vector3(p.x, Ground(p), p.z);
        Bounds oriented = WorldBounds(model);
        float bottom = Ground(p) - oriented.size.y * burial;
        if (shoreline)
        {
            // Seat on the coastal floor, with genuinely submerged bases.
            bottom = Mathf.Min(bottom, sea - oriented.size.y * .35f);
        }
        pivot.transform.position += Vector3.up * (bottom - oriented.min.y);
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0) materials = new Material[1];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Destroy(collider);
        // The originals have only 80-110 triangles. Exact static colliders are
        // cheap here and avoid oversized invisible rotated box obstacles.
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }
        foreach (Transform child in pivot.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = true;
    }

    private static Bounds WorldBounds(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private float Ground(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
    private float N(float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());
    private void OnDestroy() { if (material != null) Destroy(material); }
}
