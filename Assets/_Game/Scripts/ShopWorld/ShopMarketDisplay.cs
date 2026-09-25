using System.Collections.Generic;
using UnityEngine;

// Fresh catch is a static counter display. No hanging rigs or live animation.
public sealed class ShopMarketDisplay : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();
    private readonly List<Mesh> meshes = new List<Mesh>();

    // Exact local heights for the lowered market counter. Keeping these absolute
    // makes the display idempotent and prevents old runtime fixes from stacking.
    private const float CounterTopY = 0.80f;
    private const float CounterSurfaceTopY = 0.86f;
    private const float TrayCenterY = 0.91f;
    private const float IceCenterY = 1.00f;
    private const float FishBottomY = 1.08f;

    private void Start()
    {
        NormalizeCounter();

        // Upgrade already-installed Quay scenes at runtime too. Delete every old
        // market-display version before building one authoritative frozen display.
        var remove = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child.name == "SeafoodTray" || child.name == "Ice" ||
                child.name == "HangingMarketFish" || child.name == "HangingCord" ||
                child.name == "FreshCatchOnIce")
                remove.Add(child.gameObject);
        }
        foreach (GameObject child in remove)
        {
            child.SetActive(false);
            Destroy(child);
        }

        // All dimensions below are already in the lowered counter's coordinate
        // system. Do NOT apply a second display-root drop.
        var root = new GameObject("FreshCatchOnIce").transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        Material steel = Surface("Brushed steel trays", new Color(0.40f, 0.50f, 0.53f), 0.72f, 0.78f);
        Material ice = Surface("Crushed blue-white ice", new Color(0.88f, 0.97f, 1f), 0.70f, 0f);
        Material chalkboard = Surface("Seafood labels", new Color(0.025f, 0.08f, 0.085f), 0.2f, 0f);

        int[] species = { 0, FishCatalog.RedSnapperId, 3 };
        for (int tray = 0; tray < species.Length; tray++)
        {
            float x = (tray - 1) * 3.3f;

            // Shallow metal tray sitting immediately above the 0.86 m counter.
            Box(root, "Steel tray", new Vector3(x, TrayCenterY, -1.5f), new Vector3(2.95f, 0.10f, 1.55f), steel);
            Box(root, "Front rim", new Vector3(x, 1.00f, -2.25f), new Vector3(2.95f, 0.18f, 0.06f), steel);
            Box(root, "Back rim", new Vector3(x, 1.00f, -0.75f), new Vector3(2.95f, 0.18f, 0.06f), steel);
            for (int side = -1; side <= 1; side += 2)
                Box(root, "Side rim", new Vector3(x + side * 1.445f, 1.00f, -1.5f), new Vector3(0.06f, 0.18f, 1.5f), steel);

            // Give the ice real visible thickness instead of hiding it under fish.
            Box(root, "Ice bed", new Vector3(x, IceCenterY, -1.5f), new Vector3(2.8f, 0.16f, 1.4f), ice);
            BuildCrushedIce(root, x, tray, ice);

            for (int fish = 0; fish < 3; fish++)
                PlaceFish(root, species[tray], new Vector3(x + (fish - 1) * 0.82f, FishBottomY, -1.5f), fish);

            Box(root, "Label board", new Vector3(x, 0.67f, -2.48f), new Vector3(2.4f, 0.34f, 0.055f), chalkboard);
            Label(root, FishCatalog.Get(species[tray]).Name.ToUpperInvariant(), new Vector3(x, 0.67f, -2.515f));
        }
    }

    private void NormalizeCounter()
    {
        foreach (Transform child in transform)
        {
            Vector3 position = child.localPosition;
            Vector3 scale = child.localScale;
            if (child.name == "Counter")
            {
                // Keep the front counter above the deck rather than extending
                // below it after lowering the working surface.
                scale.y = 0.75f;
                position.y = 0.375f;
                child.localScale = scale;
                child.localPosition = position;
            }
            else if (child.name == "Countertop")
            {
                position.y = CounterTopY;
                child.localPosition = position;
            }
            else if (child.name == "CounterInlay")
            {
                position.y = 0.45f;
                child.localPosition = position;
            }
        }
    }

    private void PlaceFish(Transform parent, int species, Vector3 position, int index)
    {
        GameObject fish = FishVisualFactory.CreateFish("Fresh " + FishCatalog.Get(species).Name, parent, species, 1f);

        // Freeze one authored pose. Market fish are display food, never swimmers.
        foreach (var behaviour in fish.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        foreach (var animation in fish.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
        foreach (var animator in fish.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController != null)
            {
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.Rebind();
                animator.Update(0f);
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                if (clips.Length > 0 && clips[0].clip.length > 0f)
                {
                    var clip = clips[0].clip;
                    animator.Play(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0,
                        Mathf.Clamp01(5f / Mathf.Max(1f, clip.frameRate) / clip.length));
                    animator.Update(0f);
                }
            }
            animator.enabled = false;
        }
        foreach (var collider in fish.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var body in fish.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        // Smaller retail-display fish leave the ice clearly visible around them.
        FishWorldSize.SetLength(fish, 0.62f + index * 0.035f);
        fish.transform.localRotation = Quaternion.Euler(0f, index % 2 == 0 ? 8f : -8f, 90f);

        Renderer[] renderers = fish.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        // Put the lowest rendered point directly onto the top of the crushed ice.
        Vector3 target = parent.TransformPoint(position);
        Vector3 correction = new Vector3(target.x - bounds.center.x, target.y - bounds.min.y, target.z - bounds.center.z);
        fish.transform.position += correction;
    }

    private void BuildCrushedIce(Transform parent, float x, int seed, Material material)
    {
        // All angular chips in a tray share one mesh/draw call.
        var random = new System.Random(301 + seed);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        int[] faces = { 0,2,3,1, 4,5,7,6, 0,4,6,2, 1,3,7,5, 0,1,5,4, 2,6,7,3 };
        for (int row = 0; row < 5; row++)
        for (int col = 0; col < 12; col++)
        {
            Vector3 center = new Vector3(x - 1.27f + col * 0.23f, 1.085f, -2.06f + row * 0.28f);
            Vector3 size = new Vector3(
                0.13f + (float)random.NextDouble() * 0.09f,
                0.07f + (float)random.NextDouble() * 0.09f,
                0.15f);
            Quaternion rotation = Quaternion.Euler((float)random.NextDouble() * 30f, (float)random.NextDouble() * 180f, 20f);
            for (int face = 0; face < 6; face++)
            {
                int start = vertices.Count;
                for (int corner = 0; corner < 4; corner++)
                {
                    int c = faces[face * 4 + corner];
                    vertices.Add(center + rotation * Vector3.Scale(size, new Vector3(
                        (c & 1) == 0 ? -0.5f : 0.5f,
                        (c & 2) == 0 ? -0.5f : 0.5f,
                        (c & 4) == 0 ? -0.5f : 0.5f)));
                }
                triangles.AddRange(new[] {start, start + 1, start + 2, start, start + 2, start + 3});
            }
        }
        var mesh = new Mesh { name = "Crushed ice facets" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        meshes.Add(mesh);
        var node = new GameObject("Crushed ice"); node.transform.SetParent(parent, false);
        node.AddComponent<MeshFilter>().sharedMesh = mesh;
        node.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private Material Surface(string label, Color color, float smoothness, float metallic)
    {
        var shader = Resources.Load<Shader>("Fishing/ShopSurface");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        var material = new Material(shader) { name = label };
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", metallic);
        materials.Add(material); return material;
    }

    private void Box(Transform parent, string label, Vector3 position, Vector3 size, Material material)
    {
        var node = GameObject.CreatePrimitive(PrimitiveType.Cube); node.name = label;
        node.transform.SetParent(parent, false); node.transform.localPosition = position; node.transform.localScale = size;
        node.GetComponent<Renderer>().sharedMaterial = material;
        node.GetComponent<Collider>().enabled = false; Destroy(node.GetComponent<Collider>());
    }

    private void Label(Transform parent, string caption, Vector3 position)
    {
        var node = new GameObject("Species label"); node.transform.SetParent(parent, false); node.transform.localPosition = position;
        var text = node.AddComponent<TextMesh>(); text.text = caption; text.fontSize = 64;
        text.characterSize = 0.035f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.color = new Color(0.95f, 0.96f, 0.89f);
        var signShader = Resources.Load<Shader>("Fishing/ShopSign");
        if (signShader == null) signShader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(signShader);
        material.mainTexture = text.font.material.mainTexture; materials.Add(material);
        var renderer = node.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        float width = renderer.bounds.size.x;
        if (width > 2.15f) node.transform.localScale = Vector3.one * (2.15f / width);
    }

    private void OnDestroy()
    {
        foreach (var material in materials) if (material != null) Destroy(material);
        foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
    }
}
