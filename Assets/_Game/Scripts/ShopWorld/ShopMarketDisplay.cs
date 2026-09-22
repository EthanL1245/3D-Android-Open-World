using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Static Fresh Catch Market display. This component is already serialized on the
// FRESH CATCH MARKET object in the generated Tideglass scene, so keep all market
// presentation through this one code path instead of competing runtime builders.
public sealed class ShopMarketDisplay : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();
    private readonly List<Mesh> meshes = new List<Mesh>();
    private Coroutine rebuildRoutine;

    private const float CounterTopY = 0.80f;
    private const float TrayY = 0.91f;
    private const float IceY = 1.01f;
    private const float FishBottomY = 1.10f;

    private void Start()
    {
        RebuildNow();
    }

    public void RebuildNow()
    {
        if (!isActiveAndEnabled)
            enabled = true;

        if (rebuildRoutine != null)
            StopCoroutine(rebuildRoutine);

        rebuildRoutine = StartCoroutine(RebuildRoutine());
    }

    private IEnumerator RebuildRoutine()
    {
        NormalizeExistingCounterDisplay();
        RemoveOldRuntimeRoots();

        // Destroy is deferred until the end of the frame. Waiting prevents an
        // older runtime display from overlapping or deleting the replacement.
        yield return null;

        var root = new GameObject("FreshCatchOnIce").transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        Material iceMaterial = Surface(
            "Fresh Catch crushed ice",
            new Color(0.88f, 0.97f, 1f),
            0.72f,
            0f);
        Material boardMaterial = Surface(
            "Fresh Catch labels",
            new Color(0.025f, 0.08f, 0.085f),
            0.20f,
            0f);

        int[] species = { 0, FishCatalog.RedSnapperId, 3 };
        var pending = new List<PendingFish>();

        // These X positions come directly from the user's generated
        // TideglassShopWorld.unity: the three authored SeafoodTray centers are
        // -3.1, 0 and +3.1. Build on those exact trays instead of guessing.
        for (int tray = 0; tray < species.Length; tray++)
        {
            float x = (tray - 1) * 3.10f;

            BuildCrushedIce(root, x, tray, iceMaterial);

            for (int n = 0; n < 3; n++)
            {
                GameObject fish = FishVisualFactory.CreateFish(
                    "Fresh " + FishCatalog.Get(species[tray]).Name + " " + (n + 1),
                    root,
                    species[tray],
                    1f);

                if (fish != null)
                {
                    fish.SetActive(true);
                    pending.Add(new PendingFish
                    {
                        fish = fish,
                        targetLocal = new Vector3(
                            x + (n - 1) * 0.68f,
                            FishBottomY,
                            -1.50f),
                        desiredLength = 0.46f + n * 0.035f,
                        index = n
                    });
                }
            }

            Box(
                root,
                "Label board",
                new Vector3(x, 0.67f, -2.48f),
                new Vector3(2.35f, 0.30f, 0.055f),
                boardMaterial);
            Label(
                root,
                FishCatalog.Get(species[tray]).Name.ToUpperInvariant(),
                new Vector3(x, 0.67f, -2.515f));
        }

        // Imported fish prefabs use Awake/OnEnable for rig/material setup. Let
        // those callbacks finish before freezing the Animator. Do NOT disable all
        // MonoBehaviours: the old implementation could interrupt prefab visual
        // initialization and leave a perfectly valid fish with no visible mesh.
        yield return null;
        yield return null;

        for (int i = 0; i < pending.Count; i++)
            FreezeAndPlace(root, pending[i]);

        rebuildRoutine = null;
    }

    private void NormalizeExistingCounterDisplay()
    {
        foreach (Transform child in transform)
        {
            if (child == null)
                continue;

            Vector3 p = child.localPosition;
            Vector3 s = child.localScale;

            if (child.name == "Counter")
            {
                s.y = 0.75f;
                p.y = 0.375f;
                child.localScale = s;
                child.localPosition = p;
            }
            else if (child.name == "Countertop")
            {
                p.y = CounterTopY;
                child.localPosition = p;
            }
            else if (child.name == "CounterInlay")
            {
                p.y = 0.45f;
                child.localPosition = p;
            }
            else if (child.name == "SeafoodTray")
            {
                // Keep the scene-authored tray rather than deleting it. This is
                // a visible fallback even if a fish prefab ever fails to load.
                p.y = TrayY;
                s.y = 0.10f;
                child.localPosition = p;
                child.localScale = s;
                child.gameObject.SetActive(true);
                EnableRenderers(child);
            }
            else if (child.name == "Ice")
            {
                p.y = IceY;
                s.y = 0.11f;
                child.localPosition = p;
                child.localScale = s;
                child.gameObject.SetActive(true);
                EnableRenderers(child);
            }
        }
    }

    private void RemoveOldRuntimeRoots()
    {
        var remove = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child == null)
                continue;

            if (child.name == "FreshCatchOnIce" ||
                child.name == "FreshCatchRuntimeDisplay" ||
                child.name == "HangingMarketFish" ||
                child.name == "HangingCord")
                remove.Add(child.gameObject);
        }

        for (int i = 0; i < remove.Count; i++)
            if (remove[i] != null)
            {
                remove[i].SetActive(false);
                Destroy(remove[i]);
            }
    }

    private void FreezeAndPlace(Transform parent, PendingFish pending)
    {
        GameObject fish = pending.fish;
        if (fish == null)
            return;

        fish.SetActive(true);

        // Freeze the authored rig without disabling presentation scripts or
        // GameObjects. Leaving the hierarchy alive is substantially safer for
        // imported skinned fish than switching every MonoBehaviour off.
        Animator[] animators = fish.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            // Keep the locally requested straight resting pose (frame 5).
            if (animator.runtimeAnimatorController != null)
            {
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                if (clips.Length > 0 && clips[0].clip != null && clips[0].clip.length > 0f)
                {
                    var clip = clips[0].clip;
                    animator.Play(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0,
                        Mathf.Clamp01(5f / Mathf.Max(1f, clip.frameRate) / clip.length));
                    animator.Update(0f);
                }
            }
            animator.speed = 0f;
        }

        foreach (Animation animation in fish.GetComponentsInChildren<Animation>(true))
            if (animation != null)
                animation.enabled = false;

        foreach (Collider collider in fish.GetComponentsInChildren<Collider>(true))
            if (collider != null)
                collider.enabled = false;

        foreach (Rigidbody body in fish.GetComponentsInChildren<Rigidbody>(true))
        {
            if (body == null)
                continue;
            body.isKinematic = true;
            body.useGravity = false;
        }

        // Use the project's normal real-world fish scaler first.
        FishWorldSize.SetLength(fish, pending.desiredLength);

        // Retail fish lie on their side. Rotation happens before final bounds
        // measurement so the bottom placed on the ice is the actual rendered
        // bottom in the finished pose.
        fish.transform.localRotation = Quaternion.Euler(
            0f,
            pending.index % 2 == 0 ? 8f : -8f,
            90f);

        if (!TryGetVisibleBounds(fish, out Bounds bounds))
        {
            Debug.LogError(
                "Fresh Catch Market: " + fish.name +
                " instantiated but has no active visible Renderer.",
                fish);
            return;
        }

        // Guard against an imported rig-unit mismatch. If the normal scaler ever
        // produces an obviously microscopic or giant market fish, normalize by
        // its actual visible world bounds rather than letting it disappear.
        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longest < 0.18f || longest > 0.90f)
        {
            float factor = pending.desiredLength / Mathf.Max(0.0001f, longest);
            fish.transform.localScale *= factor;
            if (!TryGetVisibleBounds(fish, out bounds))
                return;
        }

        Vector3 target = parent.TransformPoint(pending.targetLocal);
        fish.transform.position += new Vector3(
            target.x - bounds.center.x,
            target.y - bounds.min.y,
            target.z - bounds.center.z);

        // Skinned meshes should continue rendering even though their Animator is
        // paused and the player can stand very close to the counter.
        Renderer[] renderers = fish.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            renderers[i].enabled = true;
            if (renderers[i] is SkinnedMeshRenderer skinned)
                skinned.updateWhenOffscreen = true;
        }
    }

    private static bool TryGetVisibleBounds(GameObject fish, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        // IMPORTANT: do not include inactive alternate meshes/LODs. The previous
        // version used GetComponentsInChildren(..., true), so an inactive imported
        // renderer could corrupt the bounds and move an otherwise visible fish far
        // away from its tray.
        Renderer[] renderers = fish.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
                bounds.Encapsulate(renderer.bounds);
        }

        return found;
    }

    private static void EnableRenderers(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].gameObject.activeInHierarchy)
                renderers[i].enabled = true;
    }

    private void BuildCrushedIce(Transform parent, float x, int seed, Material material)
    {
        var random = new System.Random(801 + seed);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        int[] faces = { 0,2,3,1, 4,5,7,6, 0,4,6,2, 1,3,7,5, 0,1,5,4, 2,6,7,3 };

        for (int row = 0; row < 4; row++)
        for (int col = 0; col < 10; col++)
        {
            Vector3 center = new Vector3(
                x - 1.05f + col * 0.235f,
                1.075f,
                -1.98f + row * 0.32f);
            Vector3 size = new Vector3(
                0.13f + (float)random.NextDouble() * 0.08f,
                0.06f + (float)random.NextDouble() * 0.07f,
                0.16f);
            Quaternion rotation = Quaternion.Euler(
                (float)random.NextDouble() * 24f,
                (float)random.NextDouble() * 180f,
                (float)random.NextDouble() * 18f);

            for (int face = 0; face < 6; face++)
            {
                int start = vertices.Count;
                for (int corner = 0; corner < 4; corner++)
                {
                    int c = faces[face * 4 + corner];
                    vertices.Add(center + rotation * Vector3.Scale(
                        size,
                        new Vector3(
                            (c & 1) == 0 ? -0.5f : 0.5f,
                            (c & 2) == 0 ? -0.5f : 0.5f,
                            (c & 4) == 0 ? -0.5f : 0.5f)));
                }
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }

        Mesh mesh = new Mesh { name = "Fresh Catch crushed ice" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshes.Add(mesh);

        GameObject node = new GameObject("Crushed ice");
        node.transform.SetParent(parent, false);
        node.AddComponent<MeshFilter>().sharedMesh = mesh;
        node.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private Material Surface(string label, Color color, float smoothness, float metallic)
    {
        Shader shader = Resources.Load<Shader>("Fishing/ShopSurface");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");

        Material material = new Material(shader) { name = label };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        materials.Add(material);
        return material;
    }

    private static GameObject Box(Transform parent, string label, Vector3 position, Vector3 size, Material material)
    {
        GameObject node = GameObject.CreatePrimitive(PrimitiveType.Cube);
        node.name = label;
        node.transform.SetParent(parent, false);
        node.transform.localPosition = position;
        node.transform.localScale = size;
        node.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = node.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }
        return node;
    }

    private void Label(Transform parent, string caption, Vector3 position)
    {
        GameObject node = new GameObject("Species label");
        node.transform.SetParent(parent, false);
        node.transform.localPosition = position;

        TextMesh text = node.AddComponent<TextMesh>();
        text.text = caption;
        text.fontSize = 64;
        text.characterSize = 0.031f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = new Color(0.95f, 0.96f, 0.89f);

        Shader sign = Resources.Load<Shader>("Fishing/ShopSign");
        if (sign == null)
            sign = Shader.Find("Universal Render Pipeline/Unlit");

        Material material = new Material(sign);
        material.mainTexture = text.font.material.mainTexture;
        materials.Add(material);
        var renderer = node.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        // Preserve the local fix: long names stay inside their label boards.
        float width = renderer.bounds.size.x;
        if (width > 2.15f) node.transform.localScale = Vector3.one * (2.15f / width);
    }

    private void OnDestroy()
    {
        if (rebuildRoutine != null)
            StopCoroutine(rebuildRoutine);

        foreach (Material material in materials)
            if (material != null) Destroy(material);
        foreach (Mesh mesh in meshes)
            if (mesh != null) Destroy(mesh);
    }

    private sealed class PendingFish
    {
        public GameObject fish;
        public Vector3 targetLocal;
        public float desiredLength;
        public int index;
    }
}
