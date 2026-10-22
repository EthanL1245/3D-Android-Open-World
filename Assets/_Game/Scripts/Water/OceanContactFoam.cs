using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared water-shader texture and bounded exclusion list. Does not
/// sample shorelines, run physics, generate meshes, or add render passes.</summary>
public static class OceanContactFoam
{
    private const int Capacity = 16;
    private const float FadeEnd = 120f;
    private struct Exclusion
    {
        public Transform root;
        public Bounds localBounds;
    }
    private static readonly List<Exclusion> exclusions = new List<Exclusion>();
    private static readonly Vector4[] regions = new Vector4[Capacity];
    private static Texture2D texture;
    private static bool triedTexture;
    private static readonly int TextureId = Shader.PropertyToID("_ContactFoamTex");
    private static readonly int ParamsId = Shader.PropertyToID("_ContactFoamParams");
    private static readonly int RegionsId = Shader.PropertyToID("_ContactFoamExclusions");
    private static readonly int CountId = Shader.PropertyToID("_ContactFoamExclusionCount");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTexture()
    {
        texture = null;
        triedTexture = false;
        // Keep live entries when Enter Play Mode has scene reload disabled.
        // Destroyed entries are removed by Apply, and registration de-duplicates.
    }

    // Call after the hull/dock hierarchy is fitted. This is a visual mask only;
    // no colliders, layers, materials or boat mechanics are changed.
    public static void RegisterExclusion(Transform root)
    {
        if (root == null) return;
        Bounds local = default;
        bool found = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
            Bounds bounds = renderer.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 p = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                p = root.InverseTransformPoint(p);
                if (!found) { local = new Bounds(p, Vector3.zero); found = true; }
                else local.Encapsulate(p);
            }
        }
        if (!found) return;
        for (int i = exclusions.Count - 1; i >= 0; i--)
            if (exclusions[i].root == null || exclusions[i].root == root) exclusions.RemoveAt(i);
        exclusions.Add(new Exclusion { root = root, localBounds = local });
    }

    public static void Apply(MaterialPropertyBlock block, Vector3 observer, float width, float strength, float scale)
    {
        if (!triedTexture || texture == null)
        {
            // Only retry once per play session if the resource is missing.
            if (!triedTexture) texture = Resources.Load<Texture2D>("Water/ContactFoam");
            triedTexture = true;
        }
        int count = 0;
        bool overflow = false;
        for (int i = exclusions.Count - 1; i >= 0; i--)
        {
            var entry = exclusions[i];
            if (entry.root == null) { exclusions.RemoveAt(i); continue; }
            if (!entry.root.gameObject.activeInHierarchy) continue;
            Matrix4x4 matrix = entry.root.localToWorldMatrix;
            Vector3 center = matrix.MultiplyPoint3x4(entry.localBounds.center);
            Vector3 extent = entry.localBounds.extents;
            // Conservative world AABB, following hull yaw/pitch/roll every frame.
            float x = Mathf.Abs(matrix.m00)*extent.x + Mathf.Abs(matrix.m01)*extent.y + Mathf.Abs(matrix.m02)*extent.z + 1.6f;
            float z = Mathf.Abs(matrix.m20)*extent.x + Mathf.Abs(matrix.m21)*extent.y + Mathf.Abs(matrix.m22)*extent.z + 1.6f;
            float dx = Mathf.Max(0, Mathf.Abs(observer.x-center.x)-x);
            float dz = Mathf.Max(0, Mathf.Abs(observer.z-center.z)-z);
            if (dx*dx + dz*dz > (FadeEnd+40)*(FadeEnd+40)) continue;
            if (count == Capacity) { overflow = true; break; }
            regions[count++] = new Vector4(center.x, center.z, x, z);
        }
        // Never allow unmasked boat contact foam in an unusually crowded view.
        // Overflow disables this cosmetic effect, not any gameplay or wake.
        float enabledStrength = texture != null && !overflow ? Mathf.Clamp01(strength) : 0f;
        block.SetTexture(TextureId, texture != null ? texture : Texture2D.blackTexture);
        block.SetVector(ParamsId, new Vector4(Mathf.Clamp(width,.1f,1.5f), enabledStrength, Mathf.Clamp(scale,.2f,3f), FadeEnd));
        block.SetFloat(CountId, count);
        block.SetVectorArray(RegionsId, regions);
    }
}
